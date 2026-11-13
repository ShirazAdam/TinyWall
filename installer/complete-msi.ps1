[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Path,
    [ValidateSet('win-x86', 'win-x64')]
    [string] $Architecture = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase((Resolve-Path -LiteralPath $Path).Path, 1)

function Invoke-Sql([string] $Sql) {
    $view = $database.OpenView($Sql)
    try { [void] $view.Execute() } finally { [void] $view.Close() }
}

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

$files = @(Get-Rows 'SELECT `File`, `FileName`, `FileSize`, `Version` FROM `File`' 4)
$service = @($files | Where-Object { ($_[1] -split '\|')[-1] -eq 'TinyWall.exe' })
if ($service.Count -ne 1) { throw 'MSI must contain exactly one TinyWall.exe service executable.' }
$serviceFile = $service[0][0]
$root = Split-Path -Parent $PSScriptRoot
$payload = (Resolve-Path -LiteralPath (Join-Path $root "artifacts\publish\TinyWallDual\$Architecture")).Path
$expected = @(Get-ChildItem -LiteralPath $payload -File -Recurse)
if ($files.Count -ne $expected.Count) { throw 'MSI file count differs from the verified application staging payload.' }

# Visual Studio can omit version metadata for versioned files, causing Windows
# Installer to skip them before removing an older product during an upgrade.
$sourceFiles = @{}
foreach ($file in $expected) {
    $relative = $file.FullName.Substring($payload.Length + 1)
    if ($relative -eq 'TinyWall.exe') { $key = '_B8E0172398E9477D924345B603244624' }
    elseif ($relative -eq 'ModernTinyWall.exe') { $key = '_5F1F70204337480FA68AEDD0C00101BE' }
    else {
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            $hash = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes("file\$relative".ToLowerInvariant()))
            $key = '_' + [BitConverter]::ToString($hash).Replace('-', '').Substring(0, 32)
        }
        finally { $sha.Dispose() }
    }
    if ($sourceFiles.ContainsKey($key)) { throw "Duplicate MSI file key: $key" }
    $sourceFiles[$key] = $file.FullName
}
foreach ($file in $files) {
    if (-not $sourceFiles.ContainsKey($file[0])) { throw "Unknown MSI file key: $($file[0])" }
    $version = $installer.FileVersion($sourceFiles[$file[0]], 0)
    if ($version -and -not $file[3]) {
        if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Invalid file version: $version" }
        Invoke-Sql "UPDATE ``File`` SET ``Version`` = '$version' WHERE ``File`` = '$($file[0])'"
    }
}

# The Release service deliberately rejects SCM stop requests. Its own authenticated
# uninstall command must stop it and remove persistent WFP filters before file removal.
$programFiles = if ($Architecture -eq 'win-x86') { 'ProgramFilesFolder' } else { 'ProgramFiles64Folder' }
$actions = @(
    @('TinyWallInstall', 3090, $serviceFile, '/install'),
    @('TinyWallUninstall', 3090, $serviceFile, '/uninstall /quiet'),
    @('TinyWallRollback', 3346, $serviceFile, '/uninstall /quiet'),
    @('TinyWallProtectedDirectory', 51, 'TARGETDIR', "[$programFiles]TinyWall\")
)
foreach ($action in $actions) {
    Invoke-Sql ("INSERT INTO ``CustomAction`` (``Action``, ``Type``, ``Source``, ``Target``) VALUES " +
        "('$($action[0])', $($action[1]), '$($action[2])', '$($action[3])')")
}
$sequences = @(
    @('TinyWallProtectedDirectory', 'NOT Installed', 950),
    @('TinyWallUninstall', 'REMOVE~="ALL"', 3450),
    @('TinyWallRollback', 'NOT Installed', 5998),
    @('TinyWallInstall', 'NOT Installed', 5999)
)
foreach ($sequence in $sequences) {
    Invoke-Sql ("INSERT INTO ``InstallExecuteSequence`` (``Action``, ``Condition``, ``Sequence``) VALUES " +
        "('$($sequence[0])', '$($sequence[1])', $($sequence[2]))")
}
Invoke-Sql "UPDATE ``InstallExecuteSequence`` SET ``Sequence`` = 1450 WHERE ``Action`` = 'RemoveExistingProducts'"
Invoke-Sql "INSERT INTO ``InstallUISequence`` (``Action``, ``Condition``, ``Sequence``) VALUES ('TinyWallProtectedDirectory', 'NOT Installed', 950)"
$platformCondition = if ($Architecture -eq 'win-x86') { 'Installed OR NOT VersionNT64' } else { 'Installed OR VersionNT64' }
$platformDescription = if ($Architecture -eq 'win-x86') { 'This TinyWall installer requires 32-bit Windows. Use the x64 installer on 64-bit Windows.' } else { 'This TinyWall installer requires 64-bit Windows.' }
Invoke-Sql "INSERT INTO ``LaunchCondition`` (``Condition``, ``Description``) VALUES ('$platformCondition', '$platformDescription')"
Invoke-Sql "UPDATE ``Property`` SET ``Value`` = '1' WHERE ``Property`` = 'ALLUSERS'"
$database.Commit()
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) | Out-Null
Write-Output "Completed MSI service lifecycle actions and protected $Architecture installation directory: $Path"
