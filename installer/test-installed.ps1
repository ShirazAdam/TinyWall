[CmdletBinding()]
param(
    [string] $InstallDirectory = "$env:ProgramFiles\TinyWall",
    [string] $OutputDirectory = "$env:USERPROFILE\Downloads\TinyWallVMTest"
)

$ErrorActionPreference = 'Stop'
$computer = Get-CimInstance Win32_ComputerSystem
if ($computer.Manufacturer -ne 'Microsoft Corporation' -or $computer.Model -ne 'Virtual Machine') {
    throw 'This validation script is restricted to a Hyper-V guest. Do not run it on the development host.'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$results = [Collections.Generic.List[object]]::new()
function Test-Check([string] $Name, [scriptblock] $Check) {
    try {
        & $Check
        $results.Add([pscustomobject]@{ name = $Name; passed = $true; error = $null })
        Write-Host "PASS: $Name"
    }
    catch {
        $results.Add([pscustomobject]@{ name = $Name; passed = $false; error = $_.Exception.Message })
        Write-Warning "FAIL: ${Name}: $($_.Exception.Message)"
    }
}
$manifest = Get-Content -LiteralPath (Join-Path $InstallDirectory 'package-manifest.json') -Raw | ConvertFrom-Json
Test-Check 'Installed payload hashes' {
    foreach ($entry in $manifest.files) {
        if ($entry.path -match '(^[\\/]|(^|[\\/])\.\.([\\/]|$)|:)') { throw 'Unsafe manifest path.' }
        $path = Join-Path $InstallDirectory $entry.path.Replace('/', '\')
        $file = Get-Item -LiteralPath $path
        if ($file.Length -ne $entry.size -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) {
            throw "Payload mismatch: $($entry.path)"
        }
    }
}
Test-Check 'Automatic running service and quoted executable path' {
    $service = Get-CimInstance Win32_Service -Filter "Name='TinyWall'"
    if (-not $service -or $service.State -ne 'Running' -or $service.StartMode -ne 'Auto' -or
        $service.PathName -ne ('"' + (Join-Path $InstallDirectory 'TinyWall.exe') + '" /service')) {
        throw 'TinyWall service state, startup mode or quoted executable path is incorrect.'
    }
}
Test-Check 'Administrator-protected application files' {
    $unprivileged = @('S-1-1-0', 'S-1-5-11', 'S-1-5-32-545')
    $write = [Security.AccessControl.FileSystemRights]::Write -bor
        [Security.AccessControl.FileSystemRights]::Delete -bor
        [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor
        [Security.AccessControl.FileSystemRights]::ChangePermissions -bor
        [Security.AccessControl.FileSystemRights]::TakeOwnership
    foreach ($path in @($InstallDirectory, (Join-Path $InstallDirectory 'TinyWall.exe'),
        (Join-Path $InstallDirectory 'ModernTinyWall.exe'), (Join-Path $InstallDirectory 'catalogue\profiles.db'))) {
        $rules = (Get-Acl -LiteralPath $path).GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])
        foreach ($rule in $rules) {
            $sid = $rule.IdentityReference.Value
            if ($sid -in $unprivileged -and $rule.AccessControlType -eq 'Allow' -and
                ($rule.FileSystemRights -band $write) -ne 0) {
                throw "Unprivileged write access to $path."
            }
        }
    }
}
Test-Check 'Service-owned SQLite catalogue initialised' {
    $database = Join-Path $env:ProgramData 'TinyWall\profiles.db'
    $stream = [IO.File]::Open($database, 'Open', 'Read', 'ReadWrite')
    try {
        $header = New-Object byte[] 100
        if ($stream.Read($header, 0, 100) -ne 100 -or
            [Text.Encoding]::ASCII.GetString($header, 0, 16) -ne "SQLite format 3`0" -or
            [Text.Encoding]::ASCII.GetString($header, 68, 4) -ne 'TWCA') {
            throw 'Service catalogue is missing or has an invalid SQLite header.'
        }
    }
    finally { $stream.Dispose() }
}
$serviceLog = [string[]] @()
if (Test-Path -LiteralPath "$env:ProgramData\TinyWall\logs\service.log") {
    $serviceLog = [string[]] @(Get-Content -LiteralPath "$env:ProgramData\TinyWall\logs\service.log" -Tail 60)
}
$report = [ordered]@{
    version = $manifest.version
    timestamp = [DateTime]::UtcNow.ToString('o')
    checks = @($results.ToArray())
    serviceLog = $serviceLog
    serviceDetails = Get-CimInstance Win32_Service -Filter "Name='TinyWall'" |
        Select-Object State, StartMode, PathName, ExitCode, ProcessId
    serviceEvents = @(Get-WinEvent -LogName Application -MaxEvents 50 |
        Where-Object Message -Match 'TinyWall' | Select-Object -First 4 TimeCreated, Id, Message)
    manualChecksRequired = @('Both clients and service IPC', 'ETW and firewall decisions',
        'Blocked-today persistence', 'Legacy catalogue migration', 'Light/dark/contrast/DPI/reduced motion',
        'Reboot persistence', 'Upgrade/uninstall and rollback')
}
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'installed-validation.json') -Encoding UTF8
if (@($results | Where-Object { -not $_.passed }).Count -ne 0) { exit 1 }
