param(
    [string]$InstallerPath = "artifacts\installer\ThinksnapSetup.exe",
    [string]$CertificateThumbprint,
    [string]$PfxPath,
    [securestring]$PfxPassword,
    [string]$Description = "Thinksnap Installer",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$SignToolPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $InstallerPath)) {
    throw "Installer not found: $InstallerPath"
}

function Resolve-SignTool {
    param([string]$RequestedPath)

    if ($RequestedPath) {
        if (-not (Test-Path -LiteralPath $RequestedPath)) {
            throw "signtool.exe not found: $RequestedPath"
        }

        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    $fromPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $kitRoots = @(
        "C:\Program Files (x86)\Windows Kits\10\bin",
        "C:\Program Files\Windows Kits\10\bin"
    )

    foreach ($root in $kitRoots) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        $tool = Get-ChildItem -LiteralPath $root -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($tool) {
            return $tool.FullName
        }
    }

    throw "signtool.exe was not found. Install the Windows SDK or pass -SignToolPath."
}

$resolvedInstaller = (Resolve-Path -LiteralPath $InstallerPath).Path
$resolvedSignTool = Resolve-SignTool -RequestedPath $SignToolPath

$baseArgs = @(
    "sign",
    "/fd", "SHA256",
    "/td", "SHA256",
    "/tr", $TimestampUrl,
    "/d", $Description
)

$passwordText = $null
$passwordHandle = [IntPtr]::Zero

try {
    if ($PfxPath) {
        if (-not (Test-Path -LiteralPath $PfxPath)) {
            throw "PFX certificate not found: $PfxPath"
        }

        if (-not $PfxPassword) {
            $PfxPassword = Read-Host "PFX password" -AsSecureString
        }

        $passwordHandle = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PfxPassword)
        $passwordText = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordHandle)
        $signArgs = $baseArgs + @("/f", (Resolve-Path -LiteralPath $PfxPath).Path, "/p", $passwordText, $resolvedInstaller)
    }
    elseif ($CertificateThumbprint) {
        $signArgs = $baseArgs + @("/sha1", $CertificateThumbprint, $resolvedInstaller)
    }
    else {
        $signArgs = $baseArgs + @("/a", $resolvedInstaller)
    }

    & $resolvedSignTool @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($passwordHandle -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordHandle)
    }
}

& $resolvedSignTool verify /pa /v $resolvedInstaller
if ($LASTEXITCODE -ne 0) {
    throw "signtool verify failed with exit code $LASTEXITCODE."
}
