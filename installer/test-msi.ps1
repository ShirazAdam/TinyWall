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
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class TinyWallMsiIconReader
{
    [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiOpenDatabase(string path, IntPtr mode, out uint database);
    [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiDatabaseOpenView(uint database, string query, out uint view);
    [DllImport("msi.dll")] private static extern uint MsiCreateRecord(uint fields);
    [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiRecordSetString(uint record, uint field, string value);
    [DllImport("msi.dll")] private static extern uint MsiViewExecute(uint view, uint record);
    [DllImport("msi.dll")] private static extern uint MsiViewFetch(uint view, out uint record);
    [DllImport("msi.dll")] private static extern uint MsiRecordDataSize(uint record, uint field);
    [DllImport("msi.dll")] private static extern uint MsiRecordReadStream(uint record, uint field, byte[] buffer, ref uint size);
    [DllImport("msi.dll")] private static extern uint MsiCloseHandle(uint handle);

    private static void Check(uint result) { if (result != 0) throw new Win32Exception((int)result); }

    public static byte[] Read(string path, string name)
    {
        uint database = 0, view = 0, parameter = 0, record = 0;
        try
        {
            Check(MsiOpenDatabase(path, IntPtr.Zero, out database));
            Check(MsiDatabaseOpenView(database, "SELECT `Data` FROM `Icon` WHERE `Name` = ?", out view));
            parameter = MsiCreateRecord(1);
            if (parameter == 0) throw new InvalidOperationException("Could not create an MSI query record.");
            Check(MsiRecordSetString(parameter, 1, name));
            Check(MsiViewExecute(view, parameter));
            Check(MsiViewFetch(view, out record));
            uint size = MsiRecordDataSize(record, 1);
            var bytes = new byte[size];
            Check(MsiRecordReadStream(record, 1, bytes, ref size));
            if (size != bytes.Length) throw new InvalidOperationException("Incomplete MSI icon stream.");
            return bytes;
        }
        finally
        {
            if (record != 0) MsiCloseHandle(record);
            if (parameter != 0) MsiCloseHandle(parameter);
            if (view != 0) MsiCloseHandle(view);
            if (database != 0) MsiCloseHandle(database);
        }
    }
}
'@
function Get-ComparableIconHash([byte[]] $Bytes) {
    if ($Bytes.Length -lt 6 -or [BitConverter]::ToUInt16($Bytes, 0) -ne 0 -or
        [BitConverter]::ToUInt16($Bytes, 2) -ne 1) { throw 'Invalid ICO header.' }
    $count = [BitConverter]::ToUInt16($Bytes, 4)
    if ($count -eq 0 -or $Bytes.Length -lt 6 + 16 * $count) { throw 'Invalid ICO directory.' }
    for ($index = 0; $index -lt $count; $index++) {
        $entry = 6 + 16 * $index
        $size = [BitConverter]::ToUInt32($Bytes, $entry + 8)
        $offset = [BitConverter]::ToUInt32($Bytes, $entry + 12)
        if ($size -lt 40 -or [long] $offset + $size -gt $Bytes.Length -or
            [BitConverter]::ToUInt32($Bytes, $offset) -ne 40) {
            throw 'Invalid bitmap-backed ICO frame.'
        }
        # Visual Studio normalises the directory planes and uncompressed bitmap size.
        $Bytes[$entry + 4] = 0
        $Bytes[$entry + 5] = 0
        [Array]::Clear($Bytes, [int] $offset + 20, 4)
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-', '') }
    finally { $sha.Dispose() }
}
try {
    $files = @(Get-Rows 'SELECT `File`, `FileName`, `FileSize`, `Component_`, `Version` FROM `File`' 5)
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
    $modernIconHash = Get-ComparableIconHash ([IO.File]::ReadAllBytes((Join-Path $payload 'Assets\ModernTinyWall.ico')))
    $modernShortcuts = @(Get-Rows 'SELECT `Name`, `Icon_` FROM `Shortcut`' 2 | Where-Object {
        (($_[0] -split '\|')[-1]) -eq 'Modern TinyWall'
    })
    if ($modernShortcuts.Count -ne 2) { throw 'MSI must contain two Modern TinyWall shortcuts.' }
    foreach ($shortcut in $modernShortcuts) {
        if (-not $shortcut[1]) { throw 'A Modern TinyWall shortcut has no icon.' }
        $shortcutIconHash = Get-ComparableIconHash (
            [TinyWallMsiIconReader]::Read((Resolve-Path -LiteralPath $Path).Path, $shortcut[1]))
        if ($shortcutIconHash -ne $modernIconHash) {
            throw "A Modern TinyWall shortcut has the wrong icon: $shortcutIconHash, expected $modernIconHash."
        }
    }
    $seen = @{}
    foreach ($row in $files) {
        $directory = Get-RelativeDirectory $components[$row[3]]
        $name = ($row[1] -split '\|')[-1]
        $relative = if ($directory) { "$directory\$name" } else { $name }
        if ($seen.ContainsKey($relative)) { throw "Duplicate MSI destination: $relative" }
        $seen[$relative] = $true
        $source = Get-Item -LiteralPath (Join-Path $payload $relative)
        if ($source.Length -ne [long] $row[2]) { throw "Wrong MSI file size: $relative" }
        $version = $installer.FileVersion($source.FullName, 0)
        if ($version -and $row[4] -ne $version) { throw "Wrong MSI file version: $relative" }
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
