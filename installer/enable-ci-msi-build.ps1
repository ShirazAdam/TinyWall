[CmdletBinding()]
param()

# Visual Studio Installer Projects require out-of-process builds to be disabled
# before devenv.com can build a .vdproj from the command line on a clean machine.
$ErrorActionPreference = 'Stop'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $vs) { throw 'An existing Visual Studio installation is required.' }

$tool = Get-ChildItem -Path (Join-Path $vs 'Common7\IDE') -Filter DisableOutOfProcBuild.exe -Recurse -File -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $tool) { throw 'DisableOutOfProcBuild.exe was not found; install the Visual Studio Installer Projects extension.' }

Push-Location $tool.DirectoryName
try {
    & $tool.FullName
    if ($LASTEXITCODE -ne 0) { throw "DisableOutOfProcBuild.exe failed with exit code $LASTEXITCODE." }
}
finally { Pop-Location }
Write-Output "Disabled out-of-process Installer Projects builds for Visual Studio at $vs"
