# 架构说明

本文面向准备修改生成流程、托盘运行时或主页识别逻辑的贡献者。

## 设计目标

- 所有可维护源码都在仓库中，不依赖旧版闭源 EXE。
- 转换过程在离线 Windows 环境可用，不依赖 NuGet 或 `dotnet publish`。
- 生成的目标程序是单文件 x64 EXE，运行时不要求原始 BAT/CMD 位于旁边。
- 托盘退出能清理批处理启动的服务进程，而不只是关闭托盘图标。
- 主页入口只面向本地服务，避免把文档站和下载站误识别为应用主页。

## 模块边界

### `Program.cs`

应用入口。无参数时启动 WinForms；有参数时进入 CLI。

### `UI/MainForm.cs`

负责图形界面、拖放、字段联动、选项收集、生成进度和结果展示。该层不实现编译细节。

### `Cli/CliMode.cs`

负责命令行参数解析、帮助文本、默认值和退出码。解析完成后与 UI 一样构造 `ConversionOptions` 并调用转换服务。

### `Conversion/ConversionOptions.cs`

一次转换任务的不可变参数模型，包括输入、输出、标题、托盘、隐藏窗口、进程清理、管理员权限、主页和图标。

### `Conversion/ConverterService.cs`

转换流程的核心编排层：

1. 校验输入和图标。
2. 规范化或自动识别主页地址。
3. 读取批处理原始字节并编码为 Base64。
4. 使用 `RunnerTemplate` 生成临时 C# 源码。
5. 写入可选图标和管理员清单。
6. 调用 Windows `.NET Framework` 的 `csc.exe`。
7. 把编译结果复制到目标路径。
8. 尝试清理临时构建目录。

### `Runtime/RunnerTemplate.cs`

这是“生成出来的 EXE”所使用的源代码模板，不是转换器自身的普通运行时代码。模板需要兼容 Windows `.NET Framework` 编译器支持的语言和 API 范围。

模板内包含：

- 批处理释放与 `cmd.exe` 启动
- 唯一临时脚本文件名和旧文件清理
- 标准输出、错误输出和日志写入
- `NotifyIcon` 与托盘菜单
- Windows Job Object 进程组管理
- 主页、日志和程序目录打开逻辑

## 转换数据流

```text
BAT/CMD 文件
    │
    ▼
UI 或 CLI 构造 ConversionOptions
    │
    ▼
ConverterService 校验并识别本地主页
    │
    ▼
RunnerTemplate 生成临时 Program.cs
    │
    ▼
Windows csc.exe 编译 x64 WinExe
    │
    ▼
复制为用户指定的输出 EXE
```

## 目标 EXE 运行流程

```text
启动目标 EXE
    │
    ├─ 普通模式：释放脚本 -> 运行 -> 等待退出 -> 返回退出码
    │
    └─ 托盘模式：创建 Job Object -> 运行脚本 -> 托盘持续管理
                                      │
                                      ├─ 重新运行
                                      ├─ 停止进程组
                                      ├─ 打开本地主页
                                      ├─ 打开日志
                                      └─ 退出并按配置清理进程组
```

## 主页识别顺序

1. 使用用户显式提供的主页地址。
2. 从 BAT/CMD 文本中提取 `localhost`、`127.0.0.1`、`0.0.0.0` 或端口参数。
3. 读取脚本同目录的 `package.json`。
4. 根据 Vite、Next.js、Astro、Angular、Vue CLI、React Scripts、Python HTTP Server 等常见命令推断默认端口。
5. 未识别到本地地址时不显示“打开主页”。

外部 URL 会被忽略，这是有意的安全和体验边界。

## 进程清理策略

只终止入口 `cmd.exe` 不足以关闭它启动的 Node.js、Python 或其他服务。托盘运行时始终创建 Job Object，并把入口进程加入该进程组。默认模式设置 `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`；保留进程模式不设置该标志。

停止脚本或退出托盘时，运行时关闭 Job Object，从而强制终止仍属于该组的子进程。这不是优雅关闭通知，已经在 Job 外独立运行的进程也不受影响。修改这部分代码时，验证标准应是“实际子服务进程已退出”，不能只检查托盘图标是否消失。

`KillOnExit=false` 时，退出托盘只关闭无 kill-on-close 标志的 Job 句柄，子进程可继续运行；用户主动点击“停止脚本”或“重新运行脚本”时仍调用 `TerminateJobObject` 显式终止旧进程组。

## 编译约束

- 转换器自身使用 .NET 9 和当前 C# 编译器构建。
- `RunnerTemplate` 生成的源码由 Windows `.NET Framework` `csc.exe` 编译。
- 不要在模板中使用旧编译器不支持的语言语法或只存在于现代 .NET 的 API。
- 生成流程必须保持无 NuGet 依赖。
- 修改模板后必须运行 `scripts/verify.ps1`，并对托盘子进程清理进行人工或专项冒烟测试。

## 临时文件与日志

- 构建临时目录：`%TEMP%\BatToExeCnBuild\<GUID>`
- 运行日志：`%LOCALAPPDATA%\BatToExeCn\Logs\`
- 运行时脚本：优先写入目标 EXE 目录，失败时回退到 `%TEMP%\BatToExeCn\<AppId>\`

临时脚本使用每次运行唯一文件名，避免固定隐藏文件被占用或拒绝覆盖。下一次启动会尽力删除同一应用的旧脚本；异常退出后当前脚本可能残留。

批处理进程的 `WorkingDirectory` 固定为目标 EXE 目录。普通相对路径从该目录解析，而 `%~dp0` 从实际脚本释放目录解析；回退到 `%TEMP%` 时两者会不同。

当前模板不转发目标 EXE 收到的命令行参数。普通模式返回批处理退出码；托盘模式正常退出返回 `0`。隐藏和托盘运行不应依赖交互式标准输入。
