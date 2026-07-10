using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BatToExeConverter.Cn;

internal sealed class MainForm : Form
{
    private readonly ConverterService _converter;
    private readonly TextBox _sourcePath = new();
    private readonly TextBox _outputPath = new();
    private readonly TextBox _appTitle = new();
    private readonly TextBox _iconPath = new();
    private readonly CheckBox _trayMode = new();
    private readonly CheckBox _hideWindow = new();
    private readonly CheckBox _killOnExit = new();
    private readonly CheckBox _runAsAdmin = new();
    private readonly TextBox _log = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _buildButton = new();
    private readonly Button _openOutputButton = new();
    private bool _syncingOutputPath;

    public MainForm(ConverterService converter)
    {
        _converter = converter;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "BAT 转 EXE 中文增强版";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 650);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            AutoSize = true,
            Text = "BAT 转 EXE 中文增强版",
            Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
        };

        var description = new Label
        {
            AutoSize = true,
            Text = "将 .bat / .cmd 脚本打包成 Windows EXE，可拖入批处理文件，可选生成常驻系统托盘程序。",
            ForeColor = Color.FromArgb(80, 80, 80),
            Margin = new Padding(0, 0, 0, 18),
        };

        var header = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
        };
        header.Controls.Add(title);
        header.Controls.Add(description);
        root.Controls.Add(header, 0, 0);

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 6,
            Margin = new Padding(0, 0, 0, 14),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

        AddPathRow(grid, 0, "批处理文件", _sourcePath, "浏览...", BrowseSource);
        AddPathRow(grid, 1, "输出 EXE", _outputPath, "保存为...", BrowseOutput);
        AddTextRow(grid, 2, "程序名称", _appTitle);
        AddPathRow(grid, 3, "程序图标", _iconPath, "选择...", BrowseIcon);

        _sourcePath.PlaceholderText = "选择或拖入 .bat / .cmd 文件";
        _outputPath.PlaceholderText = "选择生成后的 .exe 路径";
        _appTitle.PlaceholderText = "显示在窗口、托盘提示和日志中的名称";
        _iconPath.PlaceholderText = "可选，仅支持 .ico";
        _appTitle.TextChanged += (_, _) => UpdateOutputPathFromTitle();

        var optionsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 0),
        };

        _trayMode.Text = "生成系统托盘程序";
        _trayMode.AutoSize = true;
        _trayMode.Margin = new Padding(0, 0, 24, 8);
        _trayMode.CheckedChanged += (_, _) =>
        {
            if (_trayMode.Checked)
            {
                _hideWindow.Checked = true;
            }
        };

        _hideWindow.Text = "隐藏运行批处理窗口";
        _hideWindow.AutoSize = true;
        _hideWindow.Margin = new Padding(0, 0, 24, 8);

        _killOnExit.Text = "退出托盘时终止脚本";
        _killOnExit.Checked = true;
        _killOnExit.AutoSize = true;
        _killOnExit.Margin = new Padding(0, 0, 24, 8);

        _runAsAdmin.Text = "请求管理员权限";
        _runAsAdmin.AutoSize = true;
        _runAsAdmin.Margin = new Padding(0, 0, 24, 8);

        optionsPanel.Controls.Add(_trayMode);
        optionsPanel.Controls.Add(_hideWindow);
        optionsPanel.Controls.Add(_killOnExit);
        optionsPanel.Controls.Add(_runAsAdmin);

        grid.Controls.Add(new Label { Text = "生成选项", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 0, 0) }, 0, 4);
        grid.Controls.Add(optionsPanel, 1, 4);
        grid.SetColumnSpan(optionsPanel, 2);

        var note = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(96, 96, 96),
            Text = "托盘模式会自动识别网页地址，并提供“打开主页 / 重新运行脚本 / 打开日志 / 退出”菜单。",
            Margin = new Padding(0, 4, 0, 0),
        };
        grid.Controls.Add(note, 1, 5);
        grid.SetColumnSpan(note, 2);
        root.Controls.Add(grid, 0, 1);

        var logPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        logPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        logPanel.Controls.Add(new Label
        {
            Text = "生成日志",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8),
        }, 0, 0);

        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Dock = DockStyle.Fill;
        _log.BackColor = Color.White;
        _log.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
        logPanel.Controls.Add(_log, 0, 1);
        root.Controls.Add(logPanel, 0, 2);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 14, 0, 0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        _progress.Dock = DockStyle.Fill;
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.Visible = false;
        _progress.MarqueeAnimationSpeed = 28;
        footer.Controls.Add(_progress, 0, 0);

        _openOutputButton.Text = "打开位置";
        _openOutputButton.Enabled = false;
        _openOutputButton.Dock = DockStyle.Fill;
        _openOutputButton.Click += (_, _) => OpenOutputLocation();
        footer.Controls.Add(_openOutputButton, 1, 0);

        _buildButton.Text = "开始生成";
        _buildButton.Dock = DockStyle.Fill;
        _buildButton.Click += BuildButton_Click;
        footer.Controls.Add(_buildButton, 2, 0);
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
        RegisterBatchFileDropTarget(this);
    }

    private static void AddPathRow(TableLayoutPanel grid, int row, string labelText, TextBox textBox, string buttonText, EventHandler clickHandler)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 4, 8, 4);

        var button = new Button
        {
            Text = buttonText,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
        };
        button.Click += clickHandler;

        grid.Controls.Add(new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 0) }, 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.Controls.Add(button, 2, row);
    }

    private static void AddTextRow(TableLayoutPanel grid, int row, string labelText, TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 4, 8, 4);
        grid.Controls.Add(new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 0) }, 0, row);
        grid.Controls.Add(textBox, 1, row);
        grid.SetColumnSpan(textBox, 2);
    }

    private void BrowseSource(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "批处理文件 (*.bat;*.cmd)|*.bat;*.cmd|所有文件 (*.*)|*.*",
            Title = "选择批处理文件",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        LoadSourceFile(dialog.FileName);
    }

    private void BrowseOutput(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Windows 程序 (*.exe)|*.exe",
            Title = "选择输出 EXE 路径",
            FileName = GetOutputFileNameSuggestion(),
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputPath.Text = dialog.FileName;
        }
    }

    private void BrowseIcon(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "图标文件 (*.ico)|*.ico|所有文件 (*.*)|*.*",
            Title = "选择程序图标",
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _iconPath.Text = dialog.FileName;
        }
    }

    private void LoadSourceFile(string fileName)
    {
        if (!IsBatchFile(fileName))
        {
            MessageBox.Show(this, "请拖入或选择 .bat / .cmd 文件。", "文件类型不支持", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _sourcePath.Text = fileName;

        if (string.IsNullOrWhiteSpace(_appTitle.Text))
        {
            _appTitle.Text = Path.GetFileNameWithoutExtension(fileName);
        }

        UpdateOutputPathFromTitle();
    }

    private void UpdateOutputPathFromTitle()
    {
        if (_syncingOutputPath)
        {
            return;
        }

        var title = _appTitle.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var fileName = SanitizeFileName(title);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "output";
        }

        var directory = ResolveOutputDirectory();
        var output = string.IsNullOrWhiteSpace(directory)
            ? fileName + ".exe"
            : Path.Combine(directory, fileName + ".exe");

        try
        {
            _syncingOutputPath = true;
            _outputPath.Text = output;
        }
        finally
        {
            _syncingOutputPath = false;
        }
    }

    private string ResolveOutputDirectory()
    {
        var output = _outputPath.Text.Trim();
        if (!string.IsNullOrWhiteSpace(output))
        {
            var directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        var source = _sourcePath.Text.Trim();
        if (!string.IsNullOrWhiteSpace(source))
        {
            var directory = Path.GetDirectoryName(source);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        return string.Empty;
    }

    private string GetOutputFileNameSuggestion()
    {
        var title = _appTitle.Text.Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            var fileName = SanitizeFileName(title);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName + ".exe";
            }
        }

        var source = _sourcePath.Text.Trim();
        if (!string.IsNullOrWhiteSpace(source))
        {
            return Path.GetFileNameWithoutExtension(source) + ".exe";
        }

        return "output.exe";
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }

        return value.Trim().TrimEnd('.');
    }

    private static bool IsBatchFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase);
    }

    private void RegisterBatchFileDropTarget(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += BatchFile_DragEnter;
        control.DragDrop += BatchFile_DragDrop;

        foreach (Control child in control.Controls)
        {
            RegisterBatchFileDropTarget(child);
        }
    }

    private void BatchFile_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = TryGetDroppedBatchFile(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void BatchFile_DragDrop(object? sender, DragEventArgs e)
    {
        if (TryGetDroppedBatchFile(e.Data, out var fileName))
        {
            LoadSourceFile(fileName);
        }
    }

    private static bool TryGetDroppedBatchFile(IDataObject? data, out string fileName)
    {
        fileName = string.Empty;
        if (data == null || !data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        if (data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return false;
        }

        foreach (var file in files)
        {
            if (IsBatchFile(file))
            {
                fileName = file;
                return true;
            }
        }

        return false;
    }

    private async void BuildButton_Click(object? sender, EventArgs e)
    {
        var source = _sourcePath.Text.Trim();
        var output = _outputPath.Text.Trim();
        var title = _appTitle.Text.Trim();

        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            MessageBox.Show(this, "请先选择有效的 .bat 或 .cmd 文件。", "缺少批处理文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            MessageBox.Show(this, "请先选择输出 EXE 路径。", "缺少输出路径", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = Path.GetFileNameWithoutExtension(source);
        }

        var icon = _iconPath.Text.Trim();
        var options = new ConversionOptions(
            source,
            output,
            title,
            _trayMode.Checked,
            _hideWindow.Checked || _trayMode.Checked,
            _killOnExit.Checked,
            _runAsAdmin.Checked,
            null,
            string.IsNullOrWhiteSpace(icon) ? null : icon);

        SetBusy(true);
        _openOutputButton.Enabled = false;
        AppendLog("开始生成。");

        try
        {
            await _converter.ConvertAsync(options, new Progress<string>(AppendLog), CancellationToken.None);
            AppendLog("生成完成：" + output);
            _openOutputButton.Enabled = true;
            MessageBox.Show(this, "EXE 已生成。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppendLog("生成失败：" + ex.Message);
            MessageBox.Show(this, ex.Message, "生成失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _progress.Visible = busy;
        _buildButton.Enabled = !busy;
        _buildButton.Text = busy ? "生成中..." : "开始生成";
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), message);
            return;
        }

        _log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }

    private void OpenOutputLocation()
    {
        var output = _outputPath.Text.Trim();
        if (!File.Exists(output))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "/select,\"" + output + "\"",
            UseShellExecute = true,
        });
    }

}
