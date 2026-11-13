[CmdletBinding()]
param(
    [ValidateSet('All', 'win-x86', 'win-x64')]
    [string] $Architecture = 'All',
    [switch] $UsePublishedFiles
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $root 'artifacts\installer'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$architectures = if ($Architecture -eq 'All') { @('win-x86', 'win-x64') } else { @($Architecture) }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $vs) { throw 'An existing Visual Studio installation is required.' }
$devenv = Join-Path $vs 'Common7\IDE\devenv.com'
if (-not (Test-Path -LiteralPath $devenv)) { throw 'Visual Studio devenv.com was not found.' }
Push-Location $root
try {
    foreach ($runtime in $architectures) {
        if (-not $UsePublishedFiles) {
            & (Join-Path $PSScriptRoot 'build-package.ps1') -Architecture $runtime
            if ($LASTEXITCODE -ne 0) { throw "Publishing the $runtime MSI payload failed." }
        }
        & (Join-Path $PSScriptRoot 'build-package.ps1') -Architecture $runtime -VerifyArchive
        if ($LASTEXITCODE -ne 0) { throw "The $runtime application archive failed verification." }
        & (Join-Path $PSScriptRoot 'update-msi-payload.ps1') -Architecture $runtime
        $buildDirectory = Join-Path $outputDirectory $runtime
        Copy-Item -LiteralPath (Join-Path $root 'TinyWallSetup\TinyWallSetup.sln') -Destination $buildDirectory
        $log = Join-Path $buildDirectory 'msi-build.log'
        & $devenv (Join-Path $buildDirectory 'TinyWallSetup.sln') /Rebuild 'Release|Default' /Out $log
        if ($LASTEXITCODE -ne 0) { throw "Visual Studio $runtime MSI compilation failed; see $log." }
        $msi = Join-Path $buildDirectory 'Release\TinyWallSetup.msi'
        if (-not (Test-Path -LiteralPath $msi)) { throw "Visual Studio did not produce the $runtime MSI." }
        & powershell.exe -NoProfile -File (Join-Path $PSScriptRoot 'test-msi.ps1') -Path $msi -Architecture $runtime
        if ($LASTEXITCODE -ne 0) { throw "The compiled $runtime MSI failed validation." }
        [xml] $project = Get-Content -LiteralPath (Join-Path $root 'ModernTinyWall\ModernTinyWall.csproj') -Raw
        $version = $project.Project.PropertyGroup.Version | Where-Object { $_ }
        $output = Join-Path $outputDirectory "ModernTinyWall-$version-$runtime.msi"
        Copy-Item -LiteralPath $msi -Destination $output -Force
        $hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
        Set-Content -LiteralPath "$output.sha256" -Value "$hash  $([IO.Path]::GetFileName($output))" -Encoding ascii
        Write-Output "Built Visual Studio $runtime MSI without installing it: $output"
    }
}
finally { Pop-Location }
