using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;

// 手动提供程序集特性。
// 因本项目 XAML 引用本地类型会触发 WPF 两遍编译（wpftmp 临时项目），
// 自动生成的 AssemblyInfo 会被重复编译导致 CS0579，故在 csproj 中禁用自动生成，
// 改由本文件统一提供全部特性。

[assembly: AssemblyTitle("FanucRobotServerSH")]
[assembly: AssemblyDescription("FANUC SNPX 控制器模拟器（WPF）")]
[assembly: AssemblyProduct("FanucRobotServerSH")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

// 目标框架特性（默认由 GenerateTargetFrameworkAttribute 生成，此处手动补充）
[assembly: TargetFramework(".NETCoreApp,Version=v9.0", FrameworkDisplayName = ".NET 9.0")]

// 主题信息（WPF 默认黑色主题，此处声明浅色）
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

[assembly: CLSCompliant(false)]
