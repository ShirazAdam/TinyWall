[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Path
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

$files = @(Get-Rows 'SELECT `File`, `FileName`, `FileSize` FROM `File`' 3)
$service = @($files | Where-Object { ($_[1] -split '\|')[-1] -eq 'TinyWall.exe' })
if ($service.Count -ne 1) { throw 'MSI must contain exactly one TinyWall.exe service executable.' }
$serviceFile = $service[0][0]
$root = Split-Path -Parent $PSScriptRoot
$payload = Join-Path $root 'artifacts\publish\TinyWallDual\win-x64'
$expected = @(Get-ChildItem -LiteralPath $payload -File -Recurse)
if ($files.Count -ne $expected.Count) { throw 'MSI file count differs from the verified application staging payload.' }

# The Release service deliberately rejects SCM stop requests. Its own authenticated
# uninstall command must stop it and remove persistent WFP filters before file removal.
$actions = @(
    @('TinyWallInstall', 3090, $serviceFile, '/install'),
    @('TinyWallUninstall', 3090, $serviceFile, '/uninstall /quiet'),
    @('TinyWallRollback', 3346, $serviceFile, '/uninstall /quiet'),
    @('TinyWallProtectedDirectory', 51, 'TARGETDIR', '[ProgramFiles64Folder]TinyWall\')
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
Invoke-Sql "INSERT INTO ``LaunchCondition`` (``Condition``, ``Description``) VALUES ('Installed OR VersionNT64', 'TinyWall requires 64-bit Windows.')"
Invoke-Sql "UPDATE ``Property`` SET ``Value`` = '1' WHERE ``Property`` = 'ALLUSERS'"
$database.Commit()
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) | Out-Null
Write-Output "Completed MSI service lifecycle actions and protected x64 installation directory: $Path"
