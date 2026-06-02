param(
    [string]$InstallerPath = "artifacts\installer\ThinksnapSetup.exe",
    [string]$OutputPath = "artifacts\installer\ThinksnapSetup.exe.sha256.txt"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $InstallerPath)) {
    throw "Installer not found: $InstallerPath"
}

$installer = Get-Item -LiteralPath $InstallerPath
$hash = Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256
$line = "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), $installer.Name

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

Set-Content -LiteralPath $OutputPath -Value $line -Encoding ASCII
Write-Output $line
