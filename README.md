<p align="center">
  <img src="./src/BatToExeConverter.Cn/Assets/app-icon.png" width="128" alt="BAT 转 EXE 中文增强版图标">
</p>

<h1 align="center">BAT 转 EXE 中文增强版</h1>

<p align="center">
  一个可维护、可从源码构建的 Windows BAT/CMD 打包工具，提供中文界面、系统托盘运行、本地主页识别和进程组安全退出。
</p>

<p align="center">
  <a href="./README.en.md">English</a> ·
  <a href="#快速开始">快速开始</a> ·
  <a href="#命令行用法">命令行</a> ·
  <a href="./docs/ARCHITECTURE.md">架构说明</a> ·
  <a href="./CONTRIBUTING.md">参与贡献</a>
</p>

## 项目定位

本项目把已有的 `.bat` / `.cmd` 脚本封装为 Windows x64 EXE。它适合启动本地网页服务、开发工具、运维脚本或需要托盘控制的批处理程序。

这不是脚本加密器，也不会隐藏脚本逻辑。生成的 EXE 仍包含原始批处理内容，请不要用它存放密码、令牌或其他敏感信息。

## 主要功能

| 功能 | 说明 |
| --- | --- |
| 中文桌面界面 | 文件、选项、日志和错误提示均为中文 |
| 拖放导入 | 直接把 `.bat` / `.cmd` 拖入窗口 |
| 名称联动 | 修改程序名称时自动同步输出 EXE 文件名 |
| 单文件生成 | 批处理内容直接嵌入生成的 EXE，不依赖旁边的原始脚本 |
| 系统托盘 | 支持重新运行、停止脚本、打开主页、打开日志、打开程序目录和退出 |
| 本地主页识别 | 自动识别 `localhost`、端口参数和常见前端开发服务器 |
| 进程组退出 | 使用 Windows Job Object 管理批处理启动的子进程，退出托盘时可一并清理 |
| 后台日志 | 隐藏窗口时把输出写入 `%LOCALAPPDATA%\BatToExeCn\Logs\` |
| 图标与权限 | 支持自定义 `.ico`，可生成请求管理员权限的程序 |
| 命令行模式 | 可在脚本或 CI 中自动生成 EXE |

## 系统要求

开发和构建本项目：

- Windows 10 / 11 x64
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows `.NET Framework` 4.8 C# 编译器，当前验证路径为 `Framework64\v4.0.30319\csc.exe`

运行仓库构建出的转换器需要 .NET 9 Desktop Runtime。生成出的目标 EXE 使用 Windows `.NET Framework` 运行时；本项目把 4.8 作为最低支持基线，4.8.1 可兼容运行，4.7.x 及更早版本不在支持范围内。

如果提示“未找到 Windows .NET Framework C# 编译器”，请安装或修复 .NET Framework 4.8，并确认以下任一路径存在：

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
```

## 快速开始

```powershell
git clone https://github.com/ITer99/BatToExeConverter.Cn.git
cd BatToExeConverter.Cn
dotnet build .\BatToExeConverter.sln -c Release
```

构建完成后运行：

```powershell
.\src\BatToExeConverter.Cn\bin\Release\net9.0-windows\BatToExeConverter.Cn.exe
```

也可以直接从源码启动：

```powershell
dotnet run --project .\src\BatToExeConverter.Cn\BatToExeConverter.Cn.csproj
```

当前新版以源码构建为准，尚未提供签名安装包。`legacy/` 和历史 Release 中的旧二进制不代表当前源码功能。

## 图形界面用法

1. 选择或拖入 `.bat` / `.cmd` 文件。
2. 设置程序名称，输出文件名会自动同步。
3. 按需设置程序图标、托盘模式、隐藏窗口和管理员权限。
4. 点击“开始生成”。
5. 在日志区域确认主页识别和输出路径。

托盘模式下，生成的程序会提供以下右键菜单：

- 重新运行脚本
- 停止脚本
- 打开主页（识别到本地网页服务时显示）
- 打开日志
- 打开程序目录
- 退出

## 命令行用法

为了在终端中稳定显示生成日志，推荐通过 DLL 调用：

```powershell
dotnet .\src\BatToExeConverter.Cn\bin\Release\net9.0-windows\BatToExeConverter.Cn.dll `
  --build .\samples\hello-world.bat `
  --output .\artifacts\hello-world.exe `
  --title "Hello World"
```

托盘网页程序示例：

```powershell
dotnet .\src\BatToExeConverter.Cn\bin\Release\net9.0-windows\BatToExeConverter.Cn.dll `
  --build .\start.bat `
  --output .\MyWebApp.exe `
  --title "My Web App" `
  --tray `
  --hidden
```

| 参数 | 说明 |
| --- | --- |
| `--build` / `--input` / `-i` | 输入 `.bat` / `.cmd` 文件 |
| `--output` / `-o` | 输出 EXE 路径；省略时与输入文件同名 |
| `--title` / `-t` | 程序名称 |
| `--icon` | 自定义 `.ico` 文件 |
| `--homepage` / `--home` | 指定托盘“打开主页”地址 |
| `--tray` | 启用系统托盘模式 |
| `--hidden` | 隐藏批处理窗口 |
| `--admin` | 请求管理员权限 |
| `--no-kill-on-exit` | 退出托盘时不自动清理脚本进程组 |
| `--help` / `-h` | 显示帮助 |

相对路径会转换为绝对路径，缺失的输出目录会自动创建，已有输出 EXE 会被覆盖。CLI 退出码为：`0` 表示成功或帮助，`1` 表示生成或参数错误，`2` 表示缺少输入文件。

## 主页自动识别

未手动指定 `--homepage` 时，转换器只把本地服务识别为主页，不会把 Node.js、GitHub 或文档站等外部链接误判为应用主页。

当前支持：

- `http://localhost:3000/`、`127.0.0.1:3000`、`0.0.0.0:3000`
- `--port 3000`、`-p 3000`、`set PORT=3000`
- 同目录 `package.json`
- Vite、Next.js、Nuxt、Astro、Angular、Vue CLI、React Scripts
- `npm start`、`npm run dev`、`pnpm dev`、`yarn dev`
- `python -m http.server`

读取 `package.json` 时仍只接受本地 URL、端口和已知开发服务器命令；其中指向外部网站的 `homepage` 字段不会直接作为托盘主页。

## 目录结构

```text
.
├─ .github/                         GitHub Actions 与 Issue 模板
├─ docs/                            架构和开发文档
├─ legacy/                          原仓库二进制归档，不参与构建
├─ samples/                         可直接用于验证的 BAT 示例
├─ scripts/                         本地构建与冒烟验证脚本
├─ src/
│  └─ BatToExeConverter.Cn/
│     ├─ Assets/                    应用图标
│     ├─ Cli/                       命令行解析与入口
│     ├─ Conversion/                参数模型、检测和编译流程
│     ├─ Runtime/                   生成目标 EXE 的运行时模板
│     ├─ UI/                        WinForms 主界面
│     ├─ Program.cs                 应用入口
│     ├─ app.manifest               转换器清单
│     └─ BatToExeConverter.Cn.csproj
├─ BatToExeConverter.sln
├─ CHANGELOG.md
├─ CONTRIBUTING.md
├─ SECURITY.md
└─ LICENSE
```

更详细的模块边界和生成流程见 [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md)。

## 工作原理

1. 读取输入脚本的原始字节并编码到生成器模板中。
2. 写出临时 C# 源文件和可选的图标、管理员清单。
3. 调用 Windows `.NET Framework` 自带的 `csc.exe` 编译为 x64 WinExe。
4. 目标 EXE 运行时写出唯一临时脚本，再通过 `cmd.exe` 执行。
5. 托盘模式使用 Job Object 托管进程组，并提供日志、主页和退出控制。

生成阶段不依赖 NuGet，也不会调用 `dotnet publish`。

这里的“单文件”只表示原始 BAT/CMD 已嵌入目标 EXE。Node.js、Python、npm 包、`node_modules`、网页静态资源、配置文件以及脚本调用的其他 EXE 都不会自动打包，仍需随程序部署或预先安装。

运行时会释放一个隐藏的临时 BAT/CMD：下次启动会尽力清理同一应用的旧脚本；异常退出时文件可能暂时残留。脚本优先写入 EXE 目录，不可写时回退到 `%TEMP%\BatToExeCn\<AppId>\`。

批处理的工作目录固定为目标 EXE 所在目录，因此普通相对路径从 EXE 目录解析。`%~dp0` 指向实际释放脚本的目录：通常也是 EXE 目录，但在只读目录回退时会变成上述 `%TEMP%` 目录。需要同目录资源时，建议使用普通相对路径并把资源部署在 EXE 旁边，不要依赖 `%~dp0` 在回退场景中仍指向 EXE。

Job Object 的显式终止属于强制终止，不会向服务发送优雅关闭信号。正常由入口脚本创建并留在该 Job 中的子进程会被清理，已经在 Job 外运行的独立进程不受影响。

托盘模式始终建立进程组。默认退出会强制终止该组；使用 `--no-kill-on-exit` 时，退出只释放管理句柄并保留进程，但“停止脚本”和“重新运行脚本”仍会显式终止旧进程组，避免重复启动服务。

当前生成的 EXE 不会把自身收到的命令行参数转交给 BAT/CMD。普通模式会把批处理退出码作为 EXE 退出码；托盘模式在正常退出时返回 `0`。需要交互式输入的脚本只适合可见窗口模式，隐藏或托盘程序应设计为非交互运行。

## 验证

仓库提供统一验证脚本：

```powershell
.\scripts\verify.ps1
```

它会依次检查解决方案构建、CLI 帮助以及示例 EXE 生成。

脚本支持 Windows PowerShell 5.1 和 PowerShell 7。它不会启动 GUI，也不会执行生成的示例程序；托盘交互和真实子进程清理仍需按 [CONTRIBUTING.md](./CONTRIBUTING.md) 做专项验证。

## 已知边界

- 目前只生成 Windows x64 程序。
- 不提供脚本加密、混淆、密码保护或杀毒软件规避功能。
- 批处理本身具有与当前用户相同的系统权限，只应打包可信脚本。
- 某些安全软件可能对动态释放脚本或启动子进程的程序产生误报；请通过代码审查、签名和可信发布渠道降低风险。
- 管理员模式会让目标程序请求提权，应只在脚本确实需要时启用。

## 参与贡献

欢迎提交 Issue 和 Pull Request。开始前请阅读 [CONTRIBUTING.md](./CONTRIBUTING.md)。涉及安全问题时请按 [SECURITY.md](./SECURITY.md) 私下报告，不要直接公开利用细节。

## 历史说明

原仓库只包含 `Bat_To_Exe_Converter_x64.exe`、README 和许可证，没有可维护源码。当前实现是在 `src/BatToExeConverter.Cn/` 中重新建立的独立开源代码，不调用旧版 EXE。原始二进制仅保存在 `legacy/` 供来源追溯。

## 许可证

本项目沿用仓库现有的 [MIT License](./LICENSE)。发布修改版本时请保留原始版权声明和许可证文本。
