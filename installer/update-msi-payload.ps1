[CmdletBinding()]
param(
    [ValidateSet('win-x86', 'win-x64')]
    [string] $Architecture = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$payload = Join-Path $root "artifacts\publish\TinyWallDual\$Architecture"
$projectPath = Join-Path $root 'TinyWallSetup\TinyWallSetup.vdproj'
$project = Get-Content -LiteralPath $projectPath -Raw
$manifest = Get-Content -LiteralPath (Join-Path $payload 'package-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.runtime -ne $Architecture -or
    $project -notmatch ('"ProductVersion" = "8:' + [regex]::Escape($manifest.version) + '"')) {
    throw "Publish a matching $Architecture application archive before preparing the MSI payload."
}

function Get-Key([string] $Path) {
    return '_' + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($Path.ToLowerInvariant()))).Substring(0, 32)
}

function Set-Section([string] $Text, [string] $Name, [string] $Content) {
    $match = [regex]::Match($Text, '(?m)^([ ]*)"' + [regex]::Escape($Name) + '"\r?\n[ ]*\{')
    if (-not $match.Success) { throw "Missing setup project section: $Name" }
    $start = $match.Index + $match.Length
    $depth = 1
    $end = $start
    while ($depth -gt 0 -and $end -lt $Text.Length) {
        if ($Text[$end] -eq '{') { $depth++ }
        if ($Text[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw "Unbalanced setup project section: $Name" }
    return $Text.Substring(0, $start) + "`r`n" + $Content + "`r`n" +
        $match.Groups[1].Value + '}' + $Text.Substring($end)
}

$appFolder = '_64AD8F8B898B4B8BAB23CAB916A74340'
$folders = @{}
$fileKeys = @{}
$fileEntries = [Collections.Generic.List[string]]::new()
$hierarchy = [Collections.Generic.List[string]]::new()
$files = @(Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName)
if ($files.Count -ne $manifest.files.Count + 1) { throw 'Unexpected files in the staged MSI payload.' }
foreach ($entry in $manifest.files) {
    if ($entry.path -match '(^[\\/]|(^|[\\/])\.\.([\\/]|$)|:)') { throw 'Unsafe staged payload path.' }
    $file = Get-Item -LiteralPath (Join-Path $payload $entry.path.Replace('/', '\'))
    if ($file.Length -ne $entry.size -or
        (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $entry.sha256) {
        throw "Staged MSI payload hash or size mismatch: $($entry.path)"
    }
}
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($payload, $file.FullName)
    if ($relative -eq 'TinyWall.exe') { $key = '_B8E0172398E9477D924345B603244624' }
    elseif ($relative -eq 'ModernTinyWall.exe') { $key = '_5F1F70204337480FA68AEDD0C00101BE' }
    else { $key = Get-Key "file\$relative" }
    $fileKeys[$relative] = $key
    $directory = [IO.Path]::GetDirectoryName($relative)
    while ($directory) {
        $folders[$directory] = Get-Key "folder\$directory"
        $directory = [IO.Path]::GetDirectoryName($directory)
    }
    $directory = [IO.Path]::GetDirectoryName($relative)
    $folder = if ($directory) { $folders[$directory] } else { $appFolder }
    $source = $file.FullName.Replace('\', '\\')
    $fileEntries.Add(@"
            "{1FB2D0AE-D3B9-43D4-B9DD-F88EC61E35DE}:$key"
            {
            "SourcePath" = "8:$source"
            "TargetName" = "8:$($file.Name)"
            "Tag" = "8:"
            "Folder" = "8:$folder"
            "Condition" = "8:"
            "Transitive" = "11:FALSE"
            "Vital" = "11:TRUE"
            "ReadOnly" = "11:FALSE"
            "Hidden" = "11:FALSE"
            "System" = "11:FALSE"
            "Permanent" = "11:FALSE"
            "SharedLegacy" = "11:FALSE"
            "PackageAs" = "3:1"
            "Register" = "3:1"
            "Exclude" = "11:FALSE"
            "IsDependency" = "11:FALSE"
            "IsolateTo" = "8:"
            }
"@)
    $hierarchy.Add(@"
        "Entry"
        {
        "MsmKey" = "8:$key"
        "OwnerKey" = "8:_UNDEFINED"
        "MsmSig" = "8:_UNDEFINED"
        }
"@)
}

function Get-FolderEntries([string] $Parent) {
    $entries = foreach ($path in @($folders.Keys | Sort-Object)) {
        if ([IO.Path]::GetDirectoryName($path) -ne $Parent) { continue }
        $children = Get-FolderEntries $path
        @"
                    "{9EF0B969-E518-4E46-987F-47570745A589}:$($folders[$path])"
                    {
                    "Name" = "8:$([IO.Path]::GetFileName($path))"
                    "AlwaysCreate" = "11:FALSE"
                    "Condition" = "8:"
                    "Transitive" = "11:FALSE"
                    "Property" = "8:$($folders[$path])"
                        "Folders"
                        {
$children
                        }
                    }
"@
    }
    return $entries -join "`r`n"
}

$project = Set-Section $project 'Hierarchy' ($hierarchy -join "`r`n")
$project = Set-Section $project 'File' ($fileEntries -join "`r`n")
$project = Set-Section $project 'ProjectOutput' ''
$programFiles = if ($Architecture -eq 'win-x86') { 'ProgramFilesFolder' } else { 'ProgramFiles64Folder' }
$folderSection = @"
            "{1525181F-901A-416C-8A58-119130FE478E}:_080FEA96B8D944CCABDA982BC749BD65"
            {
            "Name" = "8:#1919"
            "AlwaysCreate" = "11:TRUE"
            "Condition" = "8:"
            "Transitive" = "11:FALSE"
            "Property" = "8:ProgramMenuFolder"
                "Folders"
                {
                }
            }
            "{3C67513D-01DD-4637-8A68-80971EB9504F}:$appFolder"
            {
            "DefaultLocation" = "8:[$programFiles]\\TinyWall"
            "Name" = "8:#1925"
            "AlwaysCreate" = "11:TRUE"
            "Condition" = "8:"
            "Transitive" = "11:FALSE"
            "Property" = "8:TARGETDIR"
                "Folders"
                {
$(Get-FolderEntries '')
                }
            }
            "{1525181F-901A-416C-8A58-119130FE478E}:_F42667624E2C4E59AA175030EFCDE35D"
            {
            "Name" = "8:#1916"
            "AlwaysCreate" = "11:TRUE"
            "Condition" = "8:"
            "Transitive" = "11:FALSE"
            "Property" = "8:DesktopFolder"
                "Folders"
                {
                }
            }
"@
$project = Set-Section $project 'Folder' $folderSection
$project = $project.Replace('"ARPPRODUCTICON" = "8:_BD68CFAA43D54F0EA07E7E5EFFD83FBA"',
    '"ARPPRODUCTICON" = "8:' + $fileKeys['Assets\firewall.ico'] + '"')
$platform = if ($Architecture -eq 'win-x86') { 0 } else { 1 }
$project = [regex]::Replace($project, '"TargetPlatform" = "3:\d+"', '"TargetPlatform" = "3:' + $platform + '"')
$project = [regex]::Replace($project, '"ProductCode" = "8:\{[^}]+\}"',
    '"ProductCode" = "8:{' + [Guid]::NewGuid().ToString().ToUpperInvariant() + '}"')
$project = [regex]::Replace($project, '"PackageCode" = "8:\{[^}]+\}"',
    '"PackageCode" = "8:{' + [Guid]::NewGuid().ToString().ToUpperInvariant() + '}"')
$postBuild = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"' +
    (Join-Path $PSScriptRoot 'complete-msi.ps1').Replace('\', '\\') +
    '\" -Path \"$(BuiltOuputPath)\" -Architecture ' + $Architecture
$project = [regex]::Replace($project, '(?m)^([ ]*)"PostBuildEvent" = .*$', {
    param($match)
    return $match.Groups[1].Value + '"PostBuildEvent" = "8:' + $postBuild + '"'
})
$outputDirectory = Join-Path $root "artifacts\installer\$Architecture"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $outputDirectory 'TinyWallSetup.vdproj'), $project, [Text.UTF8Encoding]::new($true))
Write-Output "Prepared $($files.Count) staged $Architecture files in an isolated Visual Studio MSI project."
