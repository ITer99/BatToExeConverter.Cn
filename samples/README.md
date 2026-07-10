# 示例

`hello-world.bat` 是用于验证生成流程的最小示例，不依赖第三方软件。

在仓库根目录执行：

```powershell
dotnet build .\BatToExeConverter.sln -c Release
dotnet .\src\BatToExeConverter.Cn\bin\Release\net9.0-windows\BatToExeConverter.Cn.dll `
  --build .\samples\hello-world.bat `
  --output .\artifacts\hello-world.exe `
  --title "Hello World"
```

`artifacts/` 已加入 `.gitignore`，适合作为本地生成结果目录。
