[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Install', 'Uninstall')]
    [string] $Action,
    [Parameter(Mandatory)]
    [switch] $IsolatedHyperVTestVm
)

$ErrorActionPreference = 'Stop'
if (-not $IsolatedHyperVTestVm) {
    throw 'This experimental package may only be used in an isolated Hyper-V test VM.'
}
$computer = Get-CimInstance Win32_ComputerSystem
if ($computer.Manufacturer -notmatch 'Microsoft Corporation' -or $computer.Model -ne 'Virtual Machine') {
    throw 'This computer does not identify itself as a Hyper-V virtual machine.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated 64-bit PowerShell session.'
}
if (-not [Environment]::Is64BitProcess) {
    throw 'Run this script from a 64-bit PowerShell session.'
}

$inf = Join-Path $PSScriptRoot 'TinyWallPending.inf'
if (-not (Test-Path -LiteralPath $inf -PathType Leaf)) {
    throw "The driver INF is missing: $inf"
}
if ($Action -eq 'Install' -and -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'TinyWallPending.cat') -PathType Leaf)) {
    throw 'The signed driver catalog is missing. Generate and sign the package before installation.'
}
if ($Action -eq 'Uninstall') {
    $service = Get-Service -Name 'TinyWall' -ErrorAction SilentlyContinue
    if ($null -ne $service -and $service.Status -ne 'Stopped') {
        throw 'Disable pending prompts in Modern TinyWall and stop the TinyWall service before uninstalling the driver.'
    }
}

if ($PSCmdlet.ShouldProcess($inf, "$Action the experimental TinyWall driver package")) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class TinyWallDriverSetup
{
    [DllImport("newdev.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DiInstallDriverW(IntPtr parent, string path, uint flags, out bool reboot);
    [DllImport("newdev.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DiUninstallDriverW(IntPtr parent, string path, uint flags, out bool reboot);
}
'@
    $reboot = $false
    if ($Action -eq 'Install') {
        $success = [TinyWallDriverSetup]::DiInstallDriverW([IntPtr]::Zero, $inf, 0, [ref] $reboot)
    } else {
        $success = [TinyWallDriverSetup]::DiUninstallDriverW([IntPtr]::Zero, $inf, 0, [ref] $reboot)
    }
    if (-not $success) {
        throw [System.ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error())
    }
    if ($reboot) {
        Write-Warning 'Windows requires a restart to complete the driver operation.'
    } else {
        Write-Output "Driver package $Action completed. Restart the test VM before enabling pending prompts."
    }
}
