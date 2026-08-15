# FanucRobotDemo-SH 说明

本目录包含 FANUC SNPX 协议相关的通讯库与演示、模拟项目：

| 项目 | 类型 | 说明 |
|------|------|------|
| `FanucRobotInterface` | 类库 | **FANUC SNPX 通讯库**（客户端 + 服务端），通过 TCP 连接 FANUC 控制器 |
| `FanucRobotDemo` | WPF 客户端 | 连接 FANUC 控制器的演示程序（连 `FanucRobotInterface.dll` 库） |
| `FanucRobotServerSH` | WPF 服务器 | **FANUC 控制器模拟器**（SNPX Server），带图形界面，可被 Demo 客户端连接测试 |
| `FanucRobotServerSmokeTest` | 控制台 | 服务器引擎的自动化冒烟测试，验证各类数据读写闭环 |

> 目标框架：`FanucRobotInterface`（类库）多目标 **net8.0;netstandard2.0;net461**；`FanucRobotDemo`（WPF 客户端）为 **net8.0-windows**；`FanucRobotServerSH`（WPF 服务器）与 `FanucRobotServerSmokeTest` 为 **net9.0-windows**。均需对应 .NET SDK。

### 客户端 FanucRobotDemo 构建与运行

```bat
cd FanucRobotDemo
dotnet build
dotnet run
```

运行后填写服务器 IP（如 `127.0.0.1`）与端口（默认 `60008`），点"连接"即可。

## FanucRobotInterface —— FANUC SNPX 通讯库

一个基于 SNPX 协议的 FANUC 机器人通讯库，通过 TCP 与 FANUC 机器人控制器（R-J3iB ~ R-30iB Plus 系列）交互。客户端（`FanucRobotDemo`）与服务器（`FanucRobotServerSH`）共用此库。

### 功能

- **SNPX 客户端**：TCP 握手（连接/会话/`CLRASG` 清址），56 字节帧头的读写构造，各类数据的读写管理器与信号封装：
  - 寄存器 R、字符串寄存器 SR、标志寄存器 F
  - 位信号（DI/DO/RI/RO/UI/UO/SI/SO/WI/WO 等）、模拟信号（AI/AO）、组信号（GI/GO）
  - 位置（POS 位姿、PR 位置寄存器）、系统变量（$VAR）
  - 任务、报警、注释
- **SNPX 服务端**：内置 `Server/` 下的服务端实现（`SnpxServer.cs`、`SnpxFrame.cs`、`Simulation/`），供模拟器项目使用，实现读写闭环。
- **类型封装**：`FanucRobotClient`（实现 `IFanucRobotClient`）统一对外，同步与异步接口。

### 目标框架与依赖

- 多目标 **net8.0;netstandard2.0;net461**，版本 **1.2.2**，`GenerateDocumentationFile=true`（随构建生成 `.xml` 文档）。
- 依赖：`System.Memory`、`System.Threading.Tasks.Extensions`、`System.ValueTuple`（针对旧目标框架补齐）。

### 构建

```bat
cd FanucRobotInterface
dotnet build
```

### 引用关系

`FanucRobotDemo`、`FanucRobotServerSH`、`FanucRobotServerSmokeTest` 三个项目均通过 `<Reference HintPath>` 引用 `libs\FanucRobotInterface.dll` 及其 `.xml` 文档（非项目引用），因此 `libs/` 下的 dll/xml 需随仓库一并提交。

## FanucRobotServerSH —— FANUC 控制器模拟器

一个状态化的 SNPX 协议服务器，模拟 FANUC 机器人控制器，供 `FanucRobotDemo` 等客户端连接并测试全部功能。

### 功能

- **状态化 SETASG 引擎**：解析客户端的 `SETASG`/`CLRASG`，维护「%R 地址 ↔ 逻辑变量」映射，对 %R 的读写落到真实模拟值，实现读写闭环。
- **完整数据模拟**：数值寄存器 R、字符串寄存器 SR、标志寄存器 F、位信号（DI/DO/RI/RO 等）、模拟信号（AI/AO）、组信号（GI/GO）、当前位姿 POS、位置寄存器 PR、系统变量（$SYSNAME 等）、任务、报警。
- **四大功能页**：
  1. 连接监控 —— 监听端口/启停、已连接客户端列表、事件日志
  2. 寄存器/信号 —— R/SR/F 编辑，DI/DO/AI/AO 查看修改
  3. 位置/系统变量 —— POS/PR 六轴坐标编辑、系统变量读写
  4. 报警/任务 —— 报警列表与任务状态配置，供客户端读取测试

### 构建与运行

```bat
cd FanucRobotServerSH
dotnet build
dotnet run
```

启动后默认监听 `0.0.0.0:60008`。随后运行 `FanucRobotDemo`（或任意 SNPX 客户端）连接 `127.0.0.1:60008` 即可测试。

> 说明：本项目 XAML 引用了本地 UserControl，会触发 WPF 两遍编译（wpftmp 临时项目）。为避免其重复编译自动生成的 AssemblyInfo 导致 `CS0579`，在 csproj 中设置了 `GenerateAssemblyInfo=false` 与 `GenerateTargetFrameworkAttribute=false`，改由根目录 `AssemblyInfo.cs` 手动提供全部程序集特性。

### 冒烟测试

`FanucRobotServerSmokeTest` 是独立控制台项目，启动服务器引擎并连接客户端，逐一验证 R/SR/F/DI/DO/POS/PR/$VAR/报警/任务 的读写闭环。需先构建服务器：

```bat
cd FanucRobotServerSH
dotnet build
cd ..\FanucRobotServerSmokeTest
dotnet run
```

全部通过时输出 `=== 全部测试通过 ===`。

> 该测试项目引用的是服务器 `bin\Debug\net9.0-windows\FanucRobotServerSH.dll` 的编译产物，请先构建服务器再运行测试。

## 项目参考与免责声明

本项目参考了以下开源项目：

- [EERichardji/RJi.FanucRobot.Interface](https://github.com/EERichardji/RJi.FanucRobot.Interface) —— FANUC SNPX 通讯库（协议实现参考）
- [linxiang0308/FanucRobotDemo](https://github.com/linxiang0308/FanucRobotDemo) —— FANUC 机器人演示程序（Demo 参考）

由 CodeBuddy + DeepSeek 通过 Vibe Coding 编写。本项目仅作为学习用途，用于研究 FANUC SNPX 通讯协议与机器人控制相关知识，不对任何因使用本仓库内容造成的后果负责。
