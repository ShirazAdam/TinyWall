[CmdletBinding()]
param([switch] $UsePublishedFiles)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Path (Join-Path $root 'artifacts\installer') -Force | Out-Null
if (-not $UsePublishedFiles) {
    & (Join-Path $PSScriptRoot 'build-package.ps1') -Architecture win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Publishing the MSI payload failed.' }
}
& (Join-Path $PSScriptRoot 'build-package.ps1') -Architecture win-x64 -VerifyArchive
if ($LASTEXITCODE -ne 0) { throw 'The application archive failed verification.' }
& (Join-Path $PSScriptRoot 'update-msi-payload.ps1')
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $vs) { throw 'An existing Visual Studio installation is required.' }
$devenv = Join-Path $vs 'Common7\IDE\devenv.com'
if (-not (Test-Path -LiteralPath $devenv)) { throw 'Visual Studio devenv.com was not found.' }
Push-Location $root
try {
    & $devenv TinyWallSetup\TinyWallSetup.sln /Rebuild 'Release|Default' /Out artifacts\installer\msi-build.log
    if ($LASTEXITCODE -ne 0) { throw 'Visual Studio MSI compilation failed; see artifacts\installer\msi-build.log.' }
}
finally { Pop-Location }
$msi = Join-Path $root 'TinyWallSetup\Release\TinyWallSetup.msi'
if (-not (Test-Path -LiteralPath $msi)) { throw 'Visual Studio did not produce the MSI.' }
& powershell.exe -NoProfile -File (Join-Path $PSScriptRoot 'test-msi.ps1') -Path $msi
if ($LASTEXITCODE -ne 0) { throw 'The compiled MSI failed validation.' }
$hash = (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash
Set-Content -LiteralPath "$msi.sha256" -Value "$hash  TinyWallSetup.msi" -Encoding ascii
Write-Output "Built Visual Studio MSI without installing it: $msi"
