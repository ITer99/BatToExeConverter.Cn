using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BatToExeConverter.Cn;

internal sealed class ConverterService
{
    public async Task ConvertAsync(ConversionOptions options, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        options = options with { HomePageUrl = NormalizeHomePageUrl(options.HomePageUrl) ?? DetectHomePageUrl(options.SourceBatchPath) };
        Validate(options);

        var assemblyName = BuildAssemblyName(options.OutputExePath);
        var appId = BuildAppId(File.ReadAllBytes(options.SourceBatchPath), options.ApplicationTitle);
        var buildRoot = Path.Combine(Path.GetTempPath(), "BatToExeCnBuild", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(buildRoot);

        progress?.Report("准备临时构建目录：" + buildRoot);

        try
        {
            var batchBytes = File.ReadAllBytes(options.SourceBatchPath);
            var batchBase64 = Convert.ToBase64String(batchBytes);
            var programFile = Path.Combine(buildRoot, "Program.cs");
            var compiledExe = Path.Combine(buildRoot, assemblyName + ".exe");

            if (!string.IsNullOrWhiteSpace(options.HomePageUrl))
            {
                progress?.Report("已自动识别主页地址：" + options.HomePageUrl);
            }

            File.WriteAllText(programFile, RunnerTemplate.Create(options, batchBase64, appId), Encoding.UTF8);

            if (!string.IsNullOrWhiteSpace(options.IconPath))
            {
                File.Copy(options.IconPath, Path.Combine(buildRoot, "app.ico"), overwrite: true);
            }

            if (options.RunAsAdministrator)
            {
                File.WriteAllText(Path.Combine(buildRoot, "app.manifest"), BuildManifest("requireAdministrator"), Encoding.UTF8);
            }

            progress?.Report("调用 Windows .NET Framework 编译器生成 EXE。");
            var exitCode = await RunCscCompileAsync(programFile, compiledExe, options, progress, cancellationToken);
            if (exitCode != 0)
            {
                throw new InvalidOperationException("生成器编译失败，临时目录已保留：" + buildRoot);
            }

            if (!File.Exists(compiledExe))
            {
                throw new FileNotFoundException("编译完成但没有找到生成的 EXE。", compiledExe);
            }

            var outputDirectory = Path.GetDirectoryName(options.OutputExePath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.Copy(compiledExe, options.OutputExePath, overwrite: true);
            progress?.Report("已写入输出文件：" + options.OutputExePath);

            TryDeleteDirectory(buildRoot);
        }
        catch
        {
            progress?.Report("构建失败，临时目录保留用于排查：" + buildRoot);
            throw;
        }
    }

    private static void Validate(ConversionOptions options)
    {
        if (!File.Exists(options.SourceBatchPath))
        {
            throw new FileNotFoundException("批处理文件不存在。", options.SourceBatchPath);
        }

        var extension = Path.GetExtension(options.SourceBatchPath);
        if (!string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("源文件必须是 .bat 或 .cmd。");
        }

        if (!string.IsNullOrWhiteSpace(options.IconPath) && !File.Exists(options.IconPath))
        {
            throw new FileNotFoundException("图标文件不存在。", options.IconPath);
        }

        if (!string.IsNullOrWhiteSpace(options.HomePageUrl) &&
            (!Uri.TryCreate(options.HomePageUrl, UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new InvalidOperationException("主页地址必须是 http 或 https 地址。");
        }
    }

    private static string? NormalizeHomePageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = "http://" + trimmed;
        }

        return trimmed;
    }

    private static string? DetectHomePageUrl(string sourceBatchPath)
    {
        var bytes = File.ReadAllBytes(sourceBatchPath);
        var texts = new[]
        {
            Encoding.UTF8.GetString(bytes),
            Encoding.Default.GetString(bytes),
        };

        foreach (var text in texts)
        {
            var detected = DetectDirectLocalHomePageUrlFromText(text);
            if (!string.IsNullOrWhiteSpace(detected))
            {
                return detected;
            }
        }

        var packageJsonPath = Path.Combine(Path.GetDirectoryName(sourceBatchPath) ?? string.Empty, "package.json");
        if (File.Exists(packageJsonPath))
        {
            var packageJson = File.ReadAllText(packageJsonPath, Encoding.UTF8);
            var detected = DetectHomePageUrlFromProjectText(packageJson);
            if (!string.IsNullOrWhiteSpace(detected))
            {
                return detected;
            }
        }

        foreach (var text in texts)
        {
            var detected = DetectHomePageUrlFromProjectText(text);
            if (!string.IsNullOrWhiteSpace(detected))
            {
                return detected;
            }
        }

        return null;
    }

    private static string? DetectDirectLocalHomePageUrlFromText(string text)
    {
        var localUrl = FindLocalUrl(text);
        if (!string.IsNullOrWhiteSpace(localUrl))
        {
            return localUrl;
        }

        var localhostMatch = Regex.Match(text, @"(?:localhost|127\.0\.0\.1|0\.0\.0\.0)\s*:\s*(\d{2,5})", RegexOptions.IgnoreCase);
        if (localhostMatch.Success)
        {
            return BuildLocalhostUrl(localhostMatch.Groups[1].Value);
        }

        var portMatch = Regex.Match(text, @"(?:--port|-p|/p|PORT\s*=|set\s+PORT\s*=)\s*[""']?(\d{2,5})", RegexOptions.IgnoreCase);
        if (portMatch.Success)
        {
            return BuildLocalhostUrl(portMatch.Groups[1].Value);
        }

        return null;
    }

    private static string? DetectHomePageUrlFromProjectText(string text)
    {
        var direct = DetectDirectLocalHomePageUrlFromText(text);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        var lower = text.ToLowerInvariant();
        var pythonServerMatch = Regex.Match(lower, @"python(?:3)?\s+-m\s+http\.server(?:\s+(\d{2,5}))?", RegexOptions.IgnoreCase);
        if (pythonServerMatch.Success)
        {
            return BuildLocalhostUrl(pythonServerMatch.Groups[1].Success ? pythonServerMatch.Groups[1].Value : "8000");
        }

        if (lower.Contains("vite"))
        {
            return "http://localhost:5173/";
        }

        if (lower.Contains("astro"))
        {
            return "http://localhost:4321/";
        }

        if (lower.Contains("ng serve") || lower.Contains("@angular/cli"))
        {
            return "http://localhost:4200/";
        }

        if (lower.Contains("vue-cli-service serve"))
        {
            return "http://localhost:8080/";
        }

        if (lower.Contains("http-server"))
        {
            return "http://localhost:8080/";
        }

        if (lower.Contains("next dev") ||
            lower.Contains("next start") ||
            lower.Contains("nuxt dev") ||
            lower.Contains("react-scripts start") ||
            lower.Contains("npm run dev") ||
            lower.Contains("npm start") ||
            lower.Contains("pnpm dev") ||
            lower.Contains("yarn dev"))
        {
            return "http://localhost:3000/";
        }

        return null;
    }

    private static string? FindLocalUrl(string text)
    {
        foreach (Match match in Regex.Matches(text, @"https?://[^\s""'<>]+", RegexOptions.IgnoreCase))
        {
            var url = TrimUrl(match.Value);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (!IsLocalHost(uri.Host))
            {
                continue;
            }

            return NormalizeLocalUrl(uri);
        }

        return null;
    }

    private static string BuildLocalhostUrl(string port)
    {
        return "http://localhost:" + port + "/";
    }

    private static bool IsLocalHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLocalUrl(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Host = "localhost",
        };

        if (string.IsNullOrWhiteSpace(builder.Path))
        {
            builder.Path = "/";
        }

        return builder.Uri.ToString();
    }

    private static string TrimUrl(string value)
    {
        return value.TrimEnd('.', ',', ';', ')', ']', '}');
    }

    private static string BuildProjectFile(string assemblyName, ConversionOptions options)
    {
        var iconLine = string.IsNullOrWhiteSpace(options.IconPath)
            ? string.Empty
            : "    <ApplicationIcon>app.ico</ApplicationIcon>" + Environment.NewLine;

        var manifestLine = options.RunAsAdministrator
            ? "    <ApplicationManifest>app.manifest</ApplicationManifest>" + Environment.NewLine
            : string.Empty;

        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>{EscapeXml(assemblyName)}</AssemblyName>
{iconLine}{manifestLine}  </PropertyGroup>
</Project>
""";
    }

    private static string BuildManifest(string executionLevel)
    {
        return $"""
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="BatToExeCn.generated" />
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="{executionLevel}" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
""";
    }

    private static async Task<int> RunCscCompileAsync(string programFile, string outputExe, ConversionOptions options, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var buildRoot = Path.GetDirectoryName(programFile) ?? Path.GetTempPath();
        var startInfo = new ProcessStartInfo
        {
            FileName = FindCscPath(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("/nologo");
        startInfo.ArgumentList.Add("/target:winexe");
        startInfo.ArgumentList.Add("/platform:x64");
        startInfo.ArgumentList.Add("/optimize+");
        startInfo.ArgumentList.Add("/utf8output");
        startInfo.ArgumentList.Add("/out:" + outputExe);
        startInfo.ArgumentList.Add("/reference:System.dll");
        startInfo.ArgumentList.Add("/reference:System.Core.dll");
        startInfo.ArgumentList.Add("/reference:System.Drawing.dll");
        startInfo.ArgumentList.Add("/reference:System.Windows.Forms.dll");

        if (!string.IsNullOrWhiteSpace(options.IconPath))
        {
            startInfo.ArgumentList.Add("/win32icon:" + Path.Combine(buildRoot, "app.ico"));
        }

        if (options.RunAsAdministrator)
        {
            startInfo.ArgumentList.Add("/win32manifest:" + Path.Combine(buildRoot, "app.manifest"));
        }

        startInfo.ArgumentList.Add(programFile);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                progress?.Report(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                progress?.Report(eventArgs.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("未找到 dotnet 命令，请先安装 .NET 9 SDK。", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    private static string FindCscPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v4.0.30319", "csc.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("未找到 Windows .NET Framework C# 编译器 csc.exe。");
    }

    private static string BuildAssemblyName(string outputExePath)
    {
        var rawName = Path.GetFileNameWithoutExtension(outputExePath);
        var builder = new StringBuilder(rawName.Length);

        foreach (var ch in rawName)
        {
            if ((ch >= 'a' && ch <= 'z') ||
                (ch >= 'A' && ch <= 'Z') ||
                (ch >= '0' && ch <= '9') ||
                ch == '_' ||
                ch == '-' ||
                ch == '.')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('_');
            }
        }

        var result = builder.ToString().Trim('_', '.', '-');
        if (string.IsNullOrWhiteSpace(result))
        {
            result = "BatRunner";
        }

        if (char.IsDigit(result[0]))
        {
            result = "BatRunner_" + result;
        }

        return result.Length > 60 ? result[..60] : result;
    }

    private static string BuildAppId(byte[] batchBytes, string applicationTitle)
    {
        using var sha256 = SHA256.Create();
        var titleBytes = Encoding.UTF8.GetBytes(applicationTitle);
        var combined = new byte[batchBytes.Length + titleBytes.Length];
        Buffer.BlockCopy(batchBytes, 0, combined, 0, batchBytes.Length);
        Buffer.BlockCopy(titleBytes, 0, combined, batchBytes.Length, titleBytes.Length);
        var hash = sha256.ComputeHash(combined);
        return "BatToExeCn_" + Convert.ToHexString(hash, 0, 8);
    }

    private static string EscapeXml(string value)
    {
        return SecurityElement.Escape(value) ?? value;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // The generated EXE is already copied; a locked temp file is not fatal.
        }
    }
}
