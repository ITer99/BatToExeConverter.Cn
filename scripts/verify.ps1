[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "BatToExeConverter.sln"
$runner = Join-Path $root "src\BatToExeConverter.Cn\bin\$Configuration\net9.0-windows\BatToExeConverter.Cn.dll"
$sample = Join-Path $root "samples\hello-world.bat"
$verifyRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("BatToExeConverter.Verify." + [Guid]::NewGuid().ToString("N"))
$output = Join-Path $verifyRoot "hello-world.exe"

try {
    & dotnet build $solution -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Solution build failed."
    }

    & dotnet $runner --help
    if ($LASTEXITCODE -ne 0) {
        throw "CLI help check failed."
    }

    New-Item -ItemType Directory -Path $verifyRoot -Force | Out-Null
    & dotnet $runner --build $sample --output $output --title "Open Source Smoke Test"
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) {
        throw "Sample EXE generation check failed."
    }

    Write-Host "Verification passed: build, CLI help, and sample EXE generation are working."
}
finally {
    if (Test-Path -LiteralPath $verifyRoot) {
        Remove-Item -LiteralPath $verifyRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
