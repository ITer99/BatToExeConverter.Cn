namespace BatToExeConverter.Cn;

internal sealed record ConversionOptions(
    string SourceBatchPath,
    string OutputExePath,
    string ApplicationTitle,
    bool TrayMode,
    bool HideWindow,
    bool KillOnExit,
    bool RunAsAdministrator,
    string? HomePageUrl,
    string? IconPath);
