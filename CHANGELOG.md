# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project intends to follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.0] - 2026-07-10

### Added

- Maintainable .NET 9 WinForms source under `src/BatToExeConverter.Cn/`.
- Chinese graphical interface and command-line mode.
- Drag-and-drop BAT/CMD input.
- Output file-name synchronization with the application title.
- Custom application icon and administrator manifest support.
- Generated system-tray runtime with rerun, stop, log, directory, home-page, and exit commands.
- Automatic local home-page detection for common development servers.
- Windows Job Object process-group cleanup.
- Process groups remain explicitly stoppable and restartable even when tray exit is configured to preserve child processes.
- Unique runtime script files with a temporary-directory fallback.
- Root solution, verification script, sample BAT, CI workflow, and open-source documentation.

### Changed

- Generation uses the local Windows `.NET Framework` C# compiler instead of `dotnet publish`.
- The original upstream executable is archived under `legacy/` and is no longer part of the active implementation.

### Fixed

- Tray exit now terminates child services instead of closing only the tray wrapper.
- External documentation links are no longer mistaken for the local application home page.
- Generated programs no longer overwrite one fixed hidden runtime script file.

[Unreleased]: https://github.com/ITer99/BatToExeConverter.Cn/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/ITer99/BatToExeConverter.Cn/releases/tag/v0.1.0
