# 参与贡献

感谢你改进 BAT 转 EXE 中文增强版。提交前请先确认改动属于当前项目边界，并能在 Windows 上实际验证。

## 开发环境

- Windows 10 / 11 x64
- .NET 9 SDK
- Git
- Windows `.NET Framework` 4.x C# 编译器

克隆后执行：

```powershell
dotnet build .\BatToExeConverter.sln -c Release
.\scripts\verify.ps1
```

## 代码目录

- `UI/`：WinForms 界面和交互
- `Cli/`：命令行参数和退出码
- `Conversion/`：输入校验、主页识别、临时构建和编译
- `Runtime/`：写入目标 EXE 的运行时模板
- `samples/`：不依赖第三方软件的验证样例
- `docs/`：设计和维护文档

## 修改原则

1. 保持变更聚焦，不在功能提交中混入无关重构。
2. 中文界面和英文 README 的功能描述需要与代码一致。
3. 新增 CLI 参数时，同时更新 `--help`、中英文 README 和验证脚本。
4. `RunnerTemplate.cs` 必须兼容 Windows `.NET Framework` 旧编译器，不要直接使用只在现代 C# 或现代 .NET 中可用的语法和 API。
5. 生成流程保持离线可用，不新增运行时 NuGet 依赖。
6. 不加入脚本加密、杀毒规避、权限绕过或隐蔽持久化功能。
7. 文本文件使用 UTF-8，遵循 `.editorconfig` 和 `.gitattributes`。
8. 冒烟测试必须使用有明确超时的无害进程，不要通过隐藏 PowerShell、无限循环、编码命令或其他近似恶意软件的行为模拟进程树，也不要尝试绕过安全软件。

## 验证要求

所有 Pull Request 至少需要：

```powershell
.\scripts\verify.ps1
```

涉及托盘或进程管理时，还需要验证：

- 托盘菜单可正常重新运行和停止脚本。
- 退出托盘后，BAT 启动的 Node.js、Python 或其他子服务不再存活。
- 使用“不终止脚本”退出托盘时服务继续运行，但“停止脚本”和“重新运行脚本”仍能终止完整旧进程组。
- “打开主页”只指向预期的本地服务。
- 日志能记录入口脚本的输出和退出状态。

涉及 UI 时，请在 Windows 缩放 100% 和至少一种高 DPI 设置下检查文字截断、按钮尺寸和拖放行为。

## Issue 与 Pull Request

提交 Bug 时请附上：

- Windows 版本
- .NET SDK 版本（`dotnet --info`）
- 可最小复现的 BAT/CMD 内容
- 转换器日志或生成程序日志
- 是否启用托盘、隐藏窗口、管理员权限和自定义图标

请勿在 Issue 中粘贴密码、访问令牌、内网地址或其他敏感信息。

Pull Request 建议保持一个主题，并在描述中说明：问题、修改方式、验证命令和剩余风险。
