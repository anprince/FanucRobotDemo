using FanucRobotInterface;
using FanucRobotInterface.Common.Configuration;
using FanucRobotInterface.Server;
using FanucRobotInterface.Server.Simulation;

// 独立冒烟测试控制台：验证服务器引擎与 FanucRobotClient 的读写闭环。
int port = 61008;
int failures = 0;

// --client-only 模式：只连接外部已运行的 WPF 服务端（60008）并做读写，不启动本地服务器
if (args.Length > 0 && args[0] == "--client-only")
{
    port = 60008;
    var extConfig = new RobotConnectionConfig { IpAddress = "127.0.0.1", Port = port, ConnectionTimeoutMs = 5000, ReadWriteTimeoutMs = 5000 };
    var extClient = new FanucRobotClient(extConfig);
    try
    {
        if (!await extClient.ConnectAsync())
        {
            Console.WriteLine("FAIL: 连接外部服务端失败");
            return 1;
        }
        Console.WriteLine("OK: 连接外部服务端成功，握手完成");
        extClient.NumReg.Write(1, 456.7f);
        float r1 = extClient.NumReg.Read(1);
        Check(Math.Abs(r1 - 456.7f) < 0.001f, $"R[1] 读写 = {r1}");
        // 单个 R[3]、R[4] 读取（默认值 3.14 / 9.99，验证共享 R[1] 变量偏移正确）
        float r3 = extClient.NumReg.Read(3);
        float r4 = extClient.NumReg.Read(4);
        Check(Math.Abs(r3 - 3.14f) < 0.001f, $"R[3] 默认 = {r3}");
        Check(Math.Abs(r4 - 9.99f) < 0.001f, $"R[4] 默认 = {r4}");
        // 单写 R[3] 后批量读 R[1..4]，验证共享存储
        extClient.NumReg.Write(3, 777.7f);
        var rBatch = extClient.NumReg.ReadBatch(1, 4);
        Check(Math.Abs(rBatch[2] - 777.7f) < 0.001f, $"R[3] 写 777.7 批量读 = {rBatch[2]}");
        Check(Math.Abs(rBatch[0] - 456.7f) < 0.001f, $"R[1] 批量读 = {rBatch[0]}");
        // DI 与 DO 相互独立：写 DO[1]=ON 后，DO[1] 读应为 ON，DI[1] 读应为 OFF（输入区不受输出影响）
        extClient.DO.WriteSingle(1, true);
        Check(extClient.DO.ReadSingle(1), "DO[1] 写后读取 = ON");
        Check(!extClient.DI.ReadSingle(1), "DI[1] 与 DO[1] 独立，应保持 OFF");
        // 连续多次写 DO（验证帧同步 + 输出区独立性）
        extClient.DO.WriteSingle(2, true);
        extClient.DO.WriteSingle(3, true);
        Check(extClient.DO.ReadSingle(2), "DO[2] 第二次写成功");
        Check(extClient.DO.ReadSingle(3), "DO[3] 第三次写成功");
        Check(!extClient.DI.ReadSingle(2), "DI[2] 不受 DO[2] 影响");
        // 系统变量按类型读写
        extClient.SystemVariables.WriteBool("$SS_ENB", true);
        Check(extClient.SystemVariables.ReadBool("$SS_ENB"), "$SS_ENB bool 读写 = true");
        extClient.SystemVariables.WriteInt("$FASTLPOINT_ENB", 42);
        Check(extClient.SystemVariables.ReadInt("$FASTLPOINT_ENB") == 42, "$FASTLPOINT_ENB int 读写 = 42");
        extClient.SystemVariables.WriteString("$SYSNAME", "SIM-ROBOT-01");
        Check(extClient.SystemVariables.ReadString("$SYSNAME").TrimEnd('\0') == "SIM-ROBOT-01", "$SYSNAME string 读写");
        // AI 与 AO 相互独立：写 AO[1] 后 AO 读为 500，AI[1] 应保持 0（输入区与输出区独立）
        extClient.AO.WriteSingle(1, 500);
        Check(extClient.AO.ReadSingle(1) == 500, "AO[1] 写后读取 = 500");
        Check(extClient.AI.ReadSingle(1) == 0, "AI[1] 与 AO[1] 独立，应保持 0");
        // 批量读 DO[1..8]（位读 selector=70，输出区）
        var doBatch = extClient.DO.Read(1, 8);
        Check(doBatch.Length == 8 && doBatch[0] && doBatch[1] && doBatch[2],
              $"DO[1..8] 批量 = [{string.Join(',', doBatch.Select(b => b ? 1 : 0))}]");
        var pos = extClient.Position.ReadWorldPosition();
        Check(Math.Abs(pos.X - 500f) < 0.001f, $"POS[1] X = {pos.X}");
        var alarms = extClient.Alarm.Read(3);
        Check(alarms.Length == 3 && alarms[0].AlarmNumber == 1001, $"报警首条 = {alarms[0].AlarmNumber}");
        extClient.Disconnect();
        Console.WriteLine(failures == 0 ? "=== 外部服务端验证通过 ===" : $"=== {failures} 项失败 ===");
    }
    catch (Exception ex)
    {
        Console.WriteLine("异常: " + ex);
        return 1;
    }
    finally
    {
        extClient.Dispose();
    }
    return failures == 0 ? 0 : 1;
}

var controller = new SimulatedController();
controller.InitializeDefaults();
var server = new SnpxServer(controller);
server.Log += msg => Console.WriteLine("  [SVR] " + msg);
if (!server.Start(port))
{
    Console.WriteLine("FAIL: 服务器无法启动");
    return 1;
}
Console.WriteLine("OK: 服务器已启动 0.0.0.0:" + port);

await Task.Delay(300);

// --serve 模式：仅保持监听，不执行自测，供 FanucRobotDemo 客户端连接验证
if (args.Length > 0 && args[0] == "--serve")
{
    Console.WriteLine("===== 持续监听模式 =====");
    Console.WriteLine("请用 FanucRobotDemo 客户端连接 127.0.0.1:" + port);
    Console.WriteLine("按回车停止监听并退出 ...");
    await Task.Delay(TimeSpan.FromSeconds(60));
    server.Stop();
    Console.WriteLine("60 秒后自动停止。");
    return 0;
}

var config = new RobotConnectionConfig { IpAddress = "127.0.0.1", Port = port, ConnectionTimeoutMs = 5000, ReadWriteTimeoutMs = 5000 };
var client = new FanucRobotClient(config);

try
{
    // 与 FanucRobotDemo 一致：使用异步 ConnectAsync
    if (!await client.ConnectAsync())
    {
        Console.WriteLine("FAIL: 客户端连接失败");
        return 1;
    }
    Console.WriteLine("OK: 客户端连接成功，握手完成");

    // 1. R 数值寄存器
    client.NumReg.Write(1, 123.5f);
    float r = client.NumReg.Read(1);
    Check(Math.Abs(r - 123.5f) < 0.001f, $"R[1] 读写 = {r}");

    // 2. 批量 R
    client.NumReg.WriteBatch(5, new float[] { 1.0f, 2.0f, 3.0f });
    var rBatch = client.NumReg.ReadBatch(5, 3);
    Check(rBatch.Length == 3 && Math.Abs(rBatch[0] - 1.0f) < 0.001f && Math.Abs(rBatch[2] - 3.0f) < 0.001f, $"R[5..7] 批量 = {string.Join(',', rBatch)}");

    // 3. SR 字符串
    client.StrReg.Write(1, "HELLO-SIM");
    string sr = client.StrReg.Read(1);
    Check(sr.TrimEnd('\0') == "HELLO-SIM", $"SR[1] 读写 = '{sr}'");

    // 4. F 标志
    client.Flag.Write(3, true);
    bool f = client.Flag.Read(3);
    Check(f, $"F[3] 读写 = {f}");

    // 5. DI/DO 位信号（输入区 I 与输出区 Q 相互独立）
    // 真实 FANUC 控制器 DI 与 DO 是相互独立的物理点：写 DO[1] 只影响输出区 Q，
    // DO[1] 读取应为 ON，而 DI[1] 读取应保持 OFF（不因写 DO 而变化）。
    client.DO.WriteSingle(1, true);
    bool do1 = client.DO.ReadSingle(1);
    Check(do1, $"DO[1] 写后读取 = {do1}");
    bool di1 = client.DI.ReadSingle(1);
    Check(!di1, $"DI[1] 与 DO[1] 独立，应保持 OFF = {di1}");
    client.DO.WriteSingle(2, true);
    client.DO.WriteSingle(3, true);
    var doBatch = client.DO.Read(1, 8);
    Check(doBatch.Length == 8 && doBatch[0] && doBatch[1] && doBatch[2],
          $"DO[1..8] 批量 = [{string.Join(',', doBatch.Select(b => b ? 1 : 0))}]");
    bool di2 = client.DI.ReadSingle(2);
    Check(!di2, $"DI[2] 不受 DO[2] 影响 = {di2}");

    // 6. 位置 POS[1] 读取（默认示例位姿 X=500）
    var pos = client.Position.ReadWorldPosition();
    Check(Math.Abs(pos.X - 500f) < 0.001f, $"POS[1] X = {pos.X}");

    // 7. 位置写入 PR
    var joint = new FanucRobotInterface.Common.Data.JointPosition { J1 = 10, J2 = 20, J3 = 30, J4 = 40, J5 = 50, J6 = 60 };
    client.PosReg.WriteJoint(1, joint);
    var pr = client.PosReg.Read(1);
    Check(Math.Abs(pr.Joint.J1 - 10f) < 0.001f, $"PR[1] J1 写入读取 = {pr.Joint.J1}");

    // 8. 系统变量（字符串）
    client.SystemVariables.WriteString("$SYSNAME", "SIM-ROBOT-01");
    string sys = client.SystemVariables.ReadString("$SYSNAME");
    Check(sys.TrimEnd('\0') == "SIM-ROBOT-01", $"$SYSNAME 读写 = '{sys}'");

    // 9. 系统变量（int）
    client.SystemVariables.WriteInt("$FASTLPOINT_ENB", 42);
    int sv = client.SystemVariables.ReadInt("$FASTLPOINT_ENB");
    Check(sv == 42, $"$FASTLPOINT_ENB int 读写 = {sv}");

    // 10. 报警读取（默认有 2 条示例报警，读取 3 个槽位）
    var alarms = client.Alarm.Read(3);
    Check(alarms.Length == 3 && alarms[0].AlarmNumber == 1001, $"报警读取 首条编号 = {alarms[0].AlarmNumber}");

    // 11. 任务读取
    var task = client.Task.Read(1);
    Check(task.ProgName.TrimEnd('\0') == "MAIN" && task.State == 0, $"任务读取 = {task.ProgName}/{task.LineNumber}/state={task.State}");

    // 12. 组信号 GI/GO（16 位，address=index 无 +1000 偏移）
    client.GO.WriteSingle(1, 0xABCD);
    int go1 = client.GO.ReadSingle(1);
    Check(go1 == 0xABCD, $"GO[1] 写后读取 = 0x{go1:X4}");
    int gi1 = client.GI.ReadSingle(1);
    Check(gi1 == 0, $"GI[1] 与 GO[1] 独立，应保持 0 = {gi1}");
    // 组信号批量
    client.GO.Write(1, new[] { 10, 20, 30 });
    var goBatch = client.GO.Read(1, 3);
    Check(goBatch.Length == 3 && goBatch[0] == 10 && goBatch[2] == 30,
          $"GO[1..3] 批量 = [{string.Join(',', goBatch)}]");

    // 13. 扩展数字信号 RI/RO（baseOffset=5000，RI 输入区 / RO 输出区独立）
    client.RO.WriteSingle(1, true);
    Check(client.RO.ReadSingle(1), "RO[1] 写后读取 = ON");
    Check(!client.RI.ReadSingle(1), "RI[1] 与 RO[1] 独立，应保持 OFF");
    // 其它类别（UI/UO/SI/SO/WI/WO/WSI/WSO）读写
    client.UO.WriteSingle(2, true);
    Check(client.UO.ReadSingle(2), "UO[2] 写后读取 = ON");
    client.SO.WriteSingle(0, true);  // SI/SO 索引从 0 开始
    Check(client.SO.ReadSingle(0), "SO[0] 写后读取 = ON");
    client.WO.WriteSingle(1, true);
    Check(client.WO.ReadSingle(1), "WO[1] 写后读取 = ON");
    client.WSO.WriteSingle(1, true);
    Check(client.WSO.ReadSingle(1), "WSO[1] 写后读取 = ON");

    // 14. PMC 信号（selector=76）
    client.Pmc.WriteRelay(1, true);
    Check(client.Pmc.ReadRelay(1), "PMC R[1] 写后读取 = ON");
    client.Pmc.WriteKeep(2, true);
    Check(client.Pmc.ReadKeep(2), "PMC K[2] 写后读取 = ON");
    Check(!client.Pmc.ReadKeep(1), "PMC K[1] 未写应保持 OFF");
    // PMC D 数据（selector=10，address=10000+index）
    client.Pmc.WriteData(1, 1234);
    int d1 = client.Pmc.ReadData(1);
    Check(d1 == 1234, $"PMC D[1] 写后读取 = {d1}");
    // PMC D 批量
    client.Pmc.WriteDatas(2, new[] { 100, 200, 300 });
    var dBatch = client.Pmc.ReadDatas(2, 3);
    Check(dBatch.Length == 3 && dBatch[0] == 100 && dBatch[2] == 300,
          $"PMC D[2..4] 批量 = [{string.Join(',', dBatch)}]");

    // 15. 模拟信号 AI/AO（address=channel+1000，每通道 2 字 32 位）
    client.AO.WriteSingle(1, 500);
    Check(client.AO.ReadSingle(1) == 500, "AO[1] 写后读取 = 500");
    Check(client.AI.ReadSingle(1) == 0, "AI[1] 与 AO[1] 独立，应保持 0");

    client.Disconnect();
    Console.WriteLine();
    Console.WriteLine(failures == 0 ? "=== 全部测试通过 ===" : $"=== 有 {failures} 项失败 ===");
}
catch (Exception ex)
{
    Console.WriteLine("异常: " + ex);
    return 1;
}
finally
{
    client.Dispose();
    server.Stop();
}

return failures == 0 ? 0 : 1;

void Check(bool ok, string message)
{
    if (ok)
    {
        Console.WriteLine("  PASS: " + message);
    }
    else
    {
        Console.WriteLine("  FAIL: " + message);
        failures++;
    }
}
