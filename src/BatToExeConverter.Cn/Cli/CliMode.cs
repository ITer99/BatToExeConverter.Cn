using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BatToExeConverter.Cn;

internal static class CliMode
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (HasSwitch(args, "--help") || HasSwitch(args, "-h"))
            {
                PrintHelp();
                return 0;
            }

            var parsed = Parse(args);
            if (string.IsNullOrWhiteSpace(parsed.Input))
            {
                Console.Error.WriteLine("缺少输入文件。");
                PrintHelp();
                return 2;
            }

            var input = Path.GetFullPath(parsed.Input);
            var output = string.IsNullOrWhiteSpace(parsed.Output)
                ? Path.ChangeExtension(input, ".exe")
                : Path.GetFullPath(parsed.Output);
            var title = string.IsNullOrWhiteSpace(parsed.Title)
                ? Path.GetFileNameWithoutExtension(input)
                : parsed.Title;

            var options = new ConversionOptions(
                input,
                output,
                title,
                parsed.Tray,
                parsed.Hidden || parsed.Tray,
                parsed.KillOnExit,
                parsed.Admin,
                parsed.HomePageUrl,
                string.IsNullOrWhiteSpace(parsed.Icon) ? null : Path.GetFullPath(parsed.Icon));

            var converter = new ConverterService();
            await converter.ConvertAsync(options, new Progress<string>(Console.WriteLine), CancellationToken.None);
            Console.WriteLine("生成完成：" + output);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("生成失败：" + ex.Message);
            return 1;
        }
    }

    private static ParsedArgs Parse(string[] args)
    {
        var parsed = new ParsedArgs();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--build":
                case "--input":
                case "-i":
                    parsed.Input = ReadValue(args, ref i, arg);
                    break;
                case "--output":
                case "-o":
                    parsed.Output = ReadValue(args, ref i, arg);
                    break;
                case "--title":
                case "-t":
                    parsed.Title = ReadValue(args, ref i, arg);
                    break;
                case "--icon":
                    parsed.Icon = ReadValue(args, ref i, arg);
                    break;
                case "--homepage":
                case "--home":
                    parsed.HomePageUrl = ReadValue(args, ref i, arg);
                    break;
                case "--tray":
                    parsed.Tray = true;
                    break;
                case "--hidden":
                    parsed.Hidden = true;
                    break;
                case "--admin":
                    parsed.Admin = true;
                    break;
                case "--no-kill-on-exit":
                    parsed.KillOnExit = false;
                    break;
                default:
                    if (!arg.StartsWith("-", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(parsed.Input))
                    {
                        parsed.Input = arg;
                        break;
                    }

                    throw new InvalidOperationException("未知参数：" + arg);
            }
        }

        return parsed;
    }

    private static string ReadValue(string[] args, ref int index, string switchName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("参数 " + switchName + " 缺少值。");
        }

        index++;
        return args[index];
    }

    private static bool HasSwitch(string[] args, string switchName)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, switchName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
BAT 转 EXE 中文增强版

用法：
  BatToExeConverter.Cn.exe --build <script.bat> --output <output.exe> [选项]

选项：
  --tray              生成系统托盘程序
  --hidden            隐藏批处理窗口
  --admin             请求管理员权限
  --title <name>      设置程序名称
  --icon <app.ico>    设置程序图标
  --homepage <url>    设置托盘主页地址，不设置时自动识别
  --no-kill-on-exit   退出托盘时不终止正在运行的脚本
""");
    }

    private sealed class ParsedArgs
    {
        public string? Input { get; set; }
        public string? Output { get; set; }
        public string? Title { get; set; }
        public string? Icon { get; set; }
        public string? HomePageUrl { get; set; }
        public bool Tray { get; set; }
        public bool Hidden { get; set; }
        public bool Admin { get; set; }
        public bool KillOnExit { get; set; } = true;
    }
}
