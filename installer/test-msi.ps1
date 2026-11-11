[CmdletBinding()]
param(
    [string] $Path
)

$ErrorActionPreference = 'Stop'
if (-not $Path) { $Path = Join-Path $PSScriptRoot '..\TinyWallSetup\Release\TinyWallSetup.msi' }
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase((Resolve-Path -LiteralPath $Path).Path, 0)
function Get-Rows([string] $Sql, [int] $Columns) {
    $view = $database.OpenView($Sql)
    try {
        [void] $view.Execute()
        while ($record = $view.Fetch()) {
            $values = for ($i = 1; $i -le $Columns; $i++) { $record.StringData($i) }
            ,$values
        }
    }
    finally { [void] $view.Close() }
}
try {
    $files = @(Get-Rows 'SELECT `File`, `FileName`, `FileSize`, `Component_` FROM `File`' 4)
    $actions = @(Get-Rows 'SELECT `Action`, `Type`, `Source`, `Target` FROM `CustomAction`' 4)
    $sequence = @(Get-Rows 'SELECT `Action`, `Condition`, `Sequence` FROM `InstallExecuteSequence`' 3)
    foreach ($row in $actions) { Write-Output ($row -join ' | ') }
    foreach ($row in $sequence) {
        if ($row[0] -match 'TinyWall|RemoveExistingProducts|InstallServices|InstallFinalize|RemoveFiles') {
            Write-Output ($row -join ' | ')
        }
    }
    $expected = @(Get-ChildItem (Join-Path $PSScriptRoot '..\artifacts\publish\TinyWallDual\win-x64') -File -Recurse)
    if ($files.Count -ne $expected.Count) { throw "Wrong MSI file count: $($files.Count)." }
    $directories = @{}
    foreach ($row in (Get-Rows 'SELECT `Directory`, `Directory_Parent`, `DefaultDir` FROM `Directory`' 3)) {
        $directories[$row[0]] = $row
    }
    $components = @{}
    foreach ($row in (Get-Rows 'SELECT `Component`, `Directory_` FROM `Component`' 2)) {
        $components[$row[0]] = $row[1]
    }
    function Get-RelativeDirectory([string] $Key) {
        if ($Key -eq 'TARGETDIR') { return '' }
        if (-not $directories.ContainsKey($Key)) { throw "Missing MSI directory: $Key" }
        $row = $directories[$Key]
        $parent = Get-RelativeDirectory $row[1]
        $name = (($row[2] -split ':')[0] -split '\|')[-1]
        if ($name -eq '.') { return $parent }
        if ($parent) { return "$parent\$name" }
        return $name
    }
    $payload = (Resolve-Path (Join-Path $PSScriptRoot '..\artifacts\publish\TinyWallDual\win-x64')).Path
    $seen = @{}
    foreach ($row in $files) {
        $directory = Get-RelativeDirectory $components[$row[3]]
        $name = ($row[1] -split '\|')[-1]
        $relative = if ($directory) { "$directory\$name" } else { $name }
        if ($seen.ContainsKey($relative)) { throw "Duplicate MSI destination: $relative" }
        $seen[$relative] = $true
        $source = Get-Item -LiteralPath (Join-Path $payload $relative)
        if ($source.Length -ne [long] $row[2]) { throw "Wrong MSI file size: $relative" }
    }
    foreach ($name in @('TinyWall.exe', 'ModernTinyWall.exe', 'profiles.db', 'ModernTinyWall.pri', 'coreclr.dll')) {
        if (@($files | Where-Object { ($_[1] -split '\|')[-1] -eq $name }).Count -ne 1) {
            throw "MSI must contain exactly one $name."
        }
    }
    foreach ($name in @('TinyWallInstall','TinyWallUninstall','TinyWallRollback','TinyWallProtectedDirectory')) {
        if (@($actions | Where-Object { $_[0] -eq $name }).Count -ne 1 -or
            @($sequence | Where-Object { $_[0] -eq $name }).Count -ne 1) {
            throw "Missing or duplicate MSI lifecycle action: $name."
        }
    }
    $serviceInstall = @($sequence | Where-Object { $_[0] -eq 'TinyWallInstall' })[0]
    $finalise = @($sequence | Where-Object { $_[0] -eq 'InstallFinalize' })[0]
    $serviceUninstall = @($sequence | Where-Object { $_[0] -eq 'TinyWallUninstall' })[0]
    $removeFiles = @($sequence | Where-Object { $_[0] -eq 'RemoveFiles' })[0]
    if ([int] $serviceInstall[2] -ge [int] $finalise[2]) {
        throw 'Service installation must occur before MSI finalisation.'
    }
    if ([int] $serviceUninstall[2] -ge [int] $removeFiles[2]) {
        throw 'Service shutdown and WFP cleanup must occur before MSI file removal.'
    }
    Write-Output "Verified $($files.Count) MSI payload files and service lifecycle actions without installing the MSI."
}
finally {
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) | Out-Null
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) | Out-Null
}
