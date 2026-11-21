function Get-ProjectVersion([string] $ProjectPath) {
    if (-not (Test-Path -LiteralPath $ProjectPath -PathType Leaf)) {
        throw "Project file not found: $ProjectPath"
    }

    [xml] $project = Get-Content -LiteralPath $ProjectPath -Raw
    $versions = @($project.SelectNodes('/Project/PropertyGroup/Version') |
        ForEach-Object { $_.InnerText.Trim() })
    if ($versions.Count -ne 1 -or $versions[0] -notmatch '^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$') {
        throw "$ProjectPath must declare exactly one valid release version."
    }
    return [string] $versions[0]
}

function Assert-ReleaseProjectVersions([string] $RepositoryRoot) {
    foreach ($projectFile in Get-ChildItem -LiteralPath $RepositoryRoot -Filter '*.csproj' -File -Recurse) {
        [xml] $project = Get-Content -LiteralPath $projectFile.FullName -Raw
        if ($project.Project.PropertyGroup.IsPackable -contains 'false') {
            continue
        }
        [void] (Get-ProjectVersion $projectFile.FullName)
    }
}

function Assert-MsiProductVersion([string] $Version) {
    $match = [regex]::Match($Version, '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')
    if (-not $match.Success -or $match.Groups[1].Length -gt 3 -or
        $match.Groups[2].Length -gt 3 -or $match.Groups[3].Length -gt 5 -or
        [int] $match.Groups[1].Value -gt 255 -or [int] $match.Groups[2].Value -gt 255 -or
        [int] $match.Groups[3].Value -gt 65535) {
        throw "MSI ProductVersion '$Version' must be numeric major.minor.patch, with major/minor at most 255 and patch at most 65535."
    }
}

function Set-InstallerProductVersion([string] $Project, [string] $Version) {
    Assert-MsiProductVersion $Version
    $matches = [regex]::Matches($Project, '(?m)^[ \t]*"ProductVersion"[ \t]*=[ \t]*"8:([^"\r\n]*)"')
    if ($matches.Count -ne 1) {
        throw 'The Visual Studio installer project must declare exactly one ProductVersion.'
    }

    $value = $matches[0].Groups[1]
    return $Project.Substring(0, $value.Index) + $Version + $Project.Substring($value.Index + $value.Length)
}
