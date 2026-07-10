using System;
using System.Windows.Forms;

namespace BatToExeConverter.Cn;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            return CliMode.RunAsync(args).GetAwaiter().GetResult();
        }

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(new ConverterService()));
        return 0;
    }
}
