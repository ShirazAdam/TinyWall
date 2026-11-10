[CmdletBinding()]
param(
    [ValidateSet('All', 'win-x86', 'win-x64', 'win-arm64')]
    [string] $Architecture = 'All',
    [Alias('CheckOnly')]
    [switch] $ValidateOnly,
    [switch] $SelfTest,
    [switch] $VerifyArchive
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Get-ProjectVersion([string] $ProjectPath) {
    if (-not (Test-Path -LiteralPath $ProjectPath -PathType Leaf)) {
        throw "Project file not found: $ProjectPath"
    }

    [xml] $project = Get-Content -LiteralPath $ProjectPath -Raw
    $versions = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })
    if ($versions.Count -ne 1 -or $versions[0] -notmatch '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$') {
        throw "$ProjectPath must declare exactly one valid release version."
    }
    return [string] $versions[0]
}

function Get-FileDigest([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-LegacyPackageVersion([string] $PublishPath, [string] $PackageName) {
    $deps = Get-Content -LiteralPath (Join-Path $PublishPath 'TinyWall.deps.json') -Raw | ConvertFrom-Json -AsHashtable
    $keys = @($deps.libraries.Keys | Where-Object { $_.StartsWith("$PackageName/", [StringComparison]::OrdinalIgnoreCase) })
    if ($keys.Count -ne 1 -or $deps.libraries[$keys[0]].type -ne 'package') {
        throw "Expected exactly one resolved $PackageName package in TinyWall's dependency manifest."
    }
    return $keys[0].Substring($PackageName.Length + 1)
}

function Assert-CatalogueHeader([byte[]] $Header, [long] $Length) {
    if ($Length -lt 512 -or $Header.Length -lt 100 -or
        [Text.Encoding]::ASCII.GetString($Header, 0, 16) -ne "SQLite format 3`0" -or
        [Text.Encoding]::ASCII.GetString($Header, 68, 4) -ne 'TWCA' -or
        [BitConverter]::ToString($Header, 60, 4) -ne '00-00-00-01') {
        throw 'The catalogue seed is not a nonempty TinyWall SQLite catalogue with schema version 1.'
    }
}

function Assert-CatalogueSeed([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing catalogue seed: $Path"
    }
    $stream = [IO.File]::OpenRead($Path)
    try {
        $header = [byte[]]::new(100)
        if ($stream.Read($header, 0, $header.Length) -ne $header.Length) {
            throw 'The catalogue seed is not a complete SQLite file.'
        }
        Assert-CatalogueHeader $header $stream.Length
    }
    finally { $stream.Dispose() }
}

function Write-PackageManifest([string] $Path, [string] $Version, [string] $Runtime, [int] $ApplicationCount) {
    Assert-CatalogueSeed (Join-Path $Path 'catalogue\profiles.db')
    if ($ApplicationCount -le 0) {
        throw 'The packaged catalogue must contain at least one known application.'
    }
    $files = @(Get-ChildItem -LiteralPath $Path -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{
            path = [IO.Path]::GetRelativePath($Path, $_.FullName).Replace('\', '/')
            size = $_.Length
            sha256 = Get-FileDigest $_.FullName
        }
    })
    $manifest = [ordered]@{
        version = $Version
        runtime = $Runtime
        applications = @('TinyWall.exe', 'ModernTinyWall.exe')
        catalogue = [ordered]@{
            path = 'catalogue/profiles.db'
            knownApplications = $ApplicationCount
            schemaVersion = 1
        }
        files = $files
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Path 'package-manifest.json') -Encoding utf8
}

function Assert-PublishRuntime([string] $Path, [string] $Application, [string] $Runtime, [string] $Version) {
    foreach ($name in @("$Application.dll", "$Application.runtimeconfig.json", 'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Path $name) -PathType Leaf)) {
            throw "Incomplete self-contained $Application output: missing $name."
        }
    }
    if ($Application -eq 'ModernTinyWall') {
        foreach ($name in @('Microsoft.ui.xaml.dll', 'Microsoft.WindowsAppRuntime.dll',
            'ModernTinyWall.pri', 'App.xbf', 'MainWindow.xbf', 'Views\OverviewPage.xbf')) {
            if (-not (Test-Path -LiteralPath (Join-Path $Path $name) -PathType Leaf)) {
                throw "Incomplete self-contained Windows App SDK output: missing $name."
            }
        }
    }
    $deps = Get-Content -LiteralPath (Join-Path $Path "$Application.deps.json") -Raw | ConvertFrom-Json -AsHashtable
    if (-not $deps.runtimeTarget.name.EndsWith("/$Runtime") -or
        -not $deps.libraries.ContainsKey("$Application/$Version")) {
        throw "$Application dependency manifest has the wrong runtime or project version."
    }
    $config = Get-Content -LiteralPath (Join-Path $Path "$Application.runtimeconfig.json") -Raw | ConvertFrom-Json
    if (-not $config.runtimeOptions.includedFrameworks) {
        throw "$Application is not a self-contained publish."
    }
    $binary = [IO.File]::ReadAllBytes((Join-Path $Path "$Application.exe"))
    $peOffset = [BitConverter]::ToInt32($binary, 0x3c)
    $machine = [BitConverter]::ToUInt16($binary, $peOffset + 4)
    $expectedMachine = @{ 'win-x86' = 0x014c; 'win-x64' = 0x8664; 'win-arm64' = 0xaa64 }[$Runtime]
    if ($machine -ne $expectedMachine) {
        throw "$Application.exe architecture does not match $Runtime."
    }
}

function Test-PackageArchive([string] $Archive, [string] $Version, [string] $Runtime) {
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        $entries = @{}
        foreach ($entry in $zip.Entries) {
            $key = $entry.FullName.Replace('\', '/')
            if ($key.EndsWith('/')) { continue }
            if ($entries.ContainsKey($key) -or $key -match '(^/|(^|/)\.\.(/|$)|:)') {
                throw "Unsafe or duplicate archive entry: $key"
            }
            $entries[$key] = $entry
        }
        if (-not $entries.ContainsKey('package-manifest.json')) {
            throw 'Archive has no package manifest.'
        }
        $reader = [IO.StreamReader]::new($entries['package-manifest.json'].Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if ($manifest.version -ne $Version -or $manifest.runtime -ne $Runtime) {
            throw 'Archive manifest version or architecture does not match the requested package.'
        }
        if ($manifest.catalogue.path -ne 'catalogue/profiles.db' -or
            $manifest.catalogue.knownApplications -le 0 -or $manifest.catalogue.schemaVersion -ne 1) {
            throw 'Archive manifest has no valid nonempty catalogue seed declaration.'
        }
        $seen = @{}
        foreach ($file in $manifest.files) {
            if ($seen.ContainsKey($file.path) -or -not $entries.ContainsKey($file.path)) {
                throw "Missing or duplicate manifest file: $($file.path)"
            }
            $seen[$file.path] = $true
            $entry = $entries[$file.path]
            $stream = $entry.Open()
            $hasher = [Security.Cryptography.SHA256]::Create()
            try { $hash = [Convert]::ToHexString($hasher.ComputeHash($stream)) }
            finally { $hasher.Dispose(); $stream.Dispose() }
            if ($entry.Length -ne $file.size -or $hash -ne $file.sha256) {
                throw "Archive hash or size mismatch: $($file.path)"
            }
        }
        if ($entries.Count -ne $seen.Count + 1 -or
            -not $seen.ContainsKey('TinyWall.exe') -or -not $seen.ContainsKey('ModernTinyWall.exe') -or
            -not $seen.ContainsKey('catalogue/profiles.db')) {
            throw 'Archive contains unexpected files or is missing an application or catalogue seed.'
        }
        $seedEntry = $entries['catalogue/profiles.db']
        $seedStream = $seedEntry.Open()
        try {
            $header = [byte[]]::new(100)
            $seedStream.ReadExactly($header, 0, $header.Length)
            Assert-CatalogueHeader $header $seedEntry.Length
        }
        finally { $seedStream.Dispose() }
        Write-Output "Verified $($seen.Count) payload files in $Archive"
    }
    finally { $zip.Dispose() }
}

function Assert-PublishInput([string] $Path, [string] $Application) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "Missing $Application publish output: $Path"
    }
    $executable = Join-Path $Path "$Application.exe"
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Missing required executable: $executable"
    }
    $deps = Join-Path $Path "$Application.deps.json"
    if (-not (Test-Path -LiteralPath $deps -PathType Leaf)) {
        throw "Missing required dependency manifest: $deps"
    }
}

function Assert-CompatibleOutputs([string] $LegacyPath, [string] $ModernPath) {
    Assert-PublishInput $LegacyPath 'TinyWall'
    Assert-PublishInput $ModernPath 'ModernTinyWall'
    $legacyFiles = @(Get-ChildItem -LiteralPath $LegacyPath -File -Recurse)
    $modernFiles = @(Get-ChildItem -LiteralPath $ModernPath -File -Recurse)
    if ($legacyFiles.Count -eq 0 -or $modernFiles.Count -eq 0) {
        throw 'Both application publish outputs must contain files.'
    }

    $legacyByName = @{}
    foreach ($file in $legacyFiles) {
        $relative = [IO.Path]::GetRelativePath($LegacyPath, $file.FullName)
        $legacyByName[$relative.ToLowerInvariant()] = $file
    }
    foreach ($file in $modernFiles) {
        $relative = [IO.Path]::GetRelativePath($ModernPath, $file.FullName)
        $key = $relative.ToLowerInvariant()
        if ($legacyByName.ContainsKey($key) -and
            (Get-FileDigest $legacyByName[$key].FullName) -ne (Get-FileDigest $file.FullName)) {
            throw "Incompatible shared publish file '$relative' differs between the two applications."
        }
    }
}

function Join-PublishOutputs([string] $LegacyPath, [string] $ModernPath, [string] $Destination) {
    Assert-CompatibleOutputs $LegacyPath $ModernPath
    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null

    foreach ($source in @($LegacyPath, $ModernPath)) {
        foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
            $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
            $target = Join-Path $Destination $relative
            if (Test-Path -LiteralPath $target -PathType Leaf) {
                if ((Get-FileDigest $target) -ne (Get-FileDigest $file.FullName)) {
                    throw "Incompatible shared publish file '$relative' encountered while combining outputs."
                }
                continue
            }
            $parent = Split-Path -Parent $target
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target
        }
    }

    Assert-PublishInput $Destination 'TinyWall'
    Assert-PublishInput $Destination 'ModernTinyWall'
    if (Get-ChildItem -LiteralPath $Destination -File -Recurse |
        Where-Object { $_.Name -match 'TinyWall\.Driver|\.sys$' }) {
        throw 'Driver files are not part of this installer and must not be included.'
    }
}

function Get-MSBuildPath {
    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $installPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
        if ($LASTEXITCODE -eq 0 -and $installPath) {
            $candidate = Join-Path $installPath 'MSBuild\Current\Bin\MSBuild.exe'
            if (Test-Path -LiteralPath $candidate) {
                return $candidate
            }
        }
    }
    throw 'Visual Studio MSBuild.exe is required to resolve TinyWall COM references. Install no tools as part of this script.'
}

function Assert-InstallerProject([string] $Version) {
    [xml] $solution = Get-Content -LiteralPath (Join-Path $repositoryRoot 'TinyWall.slnx') -Raw
    $installers = @($solution.Solution.Project | Where-Object { $_.Type -eq 'Installer' })
    if ($installers.Count -ne 1 -or
        $installers[0].Path.Replace('/', '\') -ne 'TinyWallSetup\TinyWallSetup.vdproj') {
        throw 'TinyWallSetup\TinyWallSetup.vdproj must be the sole installer project in the solution.'
    }
    $project = Get-Content -LiteralPath (Join-Path $repositoryRoot 'TinyWallSetup\TinyWallSetup.vdproj') -Raw
    if ($project -notmatch ('"ProductVersion"\s*=\s*"8:' + [regex]::Escape($Version) + '"')) {
        throw "The Visual Studio installer project must use release version '$Version'."
    }
    if (@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.iss' -File -Recurse).Count -ne 0) {
        throw 'Only the Visual Studio Installer Projects MSI route is supported.'
    }
}

function Invoke-SelfTest {
    $version = Get-ProjectVersion (Join-Path $repositoryRoot 'TinyWall\TinyWall.csproj')
    if ($version -ne
        (Get-ProjectVersion (Join-Path $repositoryRoot 'ModernTinyWall\ModernTinyWall.csproj'))) {
        throw 'TinyWall and Modern TinyWall project versions do not match.'
    }
    Assert-InstallerProject $version
    try {
        Assert-InstallerProject "$version-mismatch"
        throw 'Self-test failed: an out-of-date MSI project was accepted.'
    }
    catch {
        if ($_.Exception.Message -notlike 'The Visual Studio installer project must use release version*') { throw }
    }

    $testRoot = Join-Path $PSScriptRoot ('.packaging-selftest-' + [Guid]::NewGuid().ToString('N'))
    try {
        $legacy = Join-Path $testRoot 'TinyWall'
        $modern = Join-Path $testRoot 'ModernTinyWall'
        $combined = Join-Path $testRoot 'combined'
        foreach ($path in @($legacy, $modern)) {
            New-Item -ItemType Directory -Path $path -Force | Out-Null
        }
        $unsupportedInstaller = Join-Path $testRoot 'unsupported.iss'
        Set-Content -LiteralPath $unsupportedInstaller -Value '; Unsupported installer test fixture.'
        try {
            Assert-InstallerProject $version
            throw 'Self-test failed: an alternative installer route was accepted.'
        }
        catch {
            if ($_.Exception.Message -ne 'Only the Visual Studio Installer Projects MSI route is supported.') { throw }
        }
        finally {
            Remove-Item -LiteralPath $unsupportedInstaller
        }
        foreach ($name in @('TinyWall.exe', 'TinyWall.deps.json', 'shared.dll')) {
            Set-Content -LiteralPath (Join-Path $legacy $name) -Value 'same'
        }
        foreach ($name in @('ModernTinyWall.exe', 'ModernTinyWall.deps.json', 'shared.dll')) {
            Set-Content -LiteralPath (Join-Path $modern $name) -Value 'same'
        }

        Assert-CompatibleOutputs $legacy $modern
        Join-PublishOutputs $legacy $modern $combined
        if ((Get-Content -LiteralPath (Join-Path $combined 'shared.dll') -Raw).Trim() -ne 'same') {
            throw 'Self-test failed: identical shared dependency was not preserved.'
        }
        $seedDirectory = Join-Path $combined 'catalogue'
        New-Item -ItemType Directory -Path $seedDirectory | Out-Null
        $seed = Join-Path $seedDirectory 'profiles.db'
        [IO.File]::WriteAllBytes($seed, [byte[]]::new(512))
        try {
            Assert-CatalogueSeed $seed
            throw 'Self-test failed: an invalid catalogue seed was accepted.'
        }
        catch {
            if ($_.Exception.Message -notlike 'The catalogue seed is not*') { throw }
        }
        $header = [byte[]]::new(512)
        [Text.Encoding]::ASCII.GetBytes("SQLite format 3`0").CopyTo($header, 0)
        [Text.Encoding]::ASCII.GetBytes('TWCA').CopyTo($header, 68)
        $header[63] = 1
        [IO.File]::WriteAllBytes($seed, $header)
        Write-PackageManifest $combined '1.2.3' 'win-x64' 1
        $testArchive = Join-Path $testRoot 'fixture.zip'
        Compress-Archive -Path (Join-Path $combined '*') -DestinationPath $testArchive
        Test-PackageArchive $testArchive '1.2.3' 'win-x64'
        Set-Content -LiteralPath (Join-Path $combined 'shared.dll') -Value 'tampered'
        Compress-Archive -Path (Join-Path $combined '*') -DestinationPath $testArchive -Force
        try {
            Test-PackageArchive $testArchive '1.2.3' 'win-x64'
            throw 'Self-test failed: archive corruption was accepted.'
        }
        catch {
            if ($_.Exception.Message -notlike 'Archive hash or size mismatch:*') { throw }
        }

        Set-Content -LiteralPath (Join-Path $modern 'shared.dll') -Value 'different'
        try {
            Assert-CompatibleOutputs $legacy $modern
            throw 'Self-test failed: an incompatible shared dependency was accepted.'
        }
        catch {
            if ($_.Exception.Message -notlike 'Incompatible shared publish file*') { throw }
        }

        Remove-Item -LiteralPath (Join-Path $modern 'ModernTinyWall.exe')
        try {
            Assert-CompatibleOutputs $legacy $modern
            throw 'Self-test failed: an incomplete application output was accepted.'
        }
        catch {
            if ($_.Exception.Message -notlike 'Missing required executable:*') { throw }
        }
    }
    finally {
        if (Test-Path -LiteralPath $testRoot) {
            Remove-Item -LiteralPath $testRoot -Recurse -Force
        }
    }
    Write-Output 'Packaging self-test passed: MSI project/version, input completeness, shared-file collisions, and archive hashes.'
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

$legacyProject = Join-Path $repositoryRoot 'TinyWall\TinyWall.csproj'
$modernProject = Join-Path $repositoryRoot 'ModernTinyWall\ModernTinyWall.csproj'
$version = Get-ProjectVersion $legacyProject
$modernVersion = Get-ProjectVersion $modernProject
if ($version -ne $modernVersion) {
    throw "TinyWall project version '$version' does not match Modern TinyWall project version '$modernVersion'."
}
Assert-InstallerProject $version

$architectures = if ($Architecture -eq 'All') { @('win-x86', 'win-x64', 'win-arm64') } else { @($Architecture) }
$artifactRoot = Join-Path $repositoryRoot 'artifacts'
$publishRoot = Join-Path $artifactRoot 'publish'
$releaseRoot = Join-Path $artifactRoot 'release'
$catalogueSource = Join-Path $repositoryRoot 'TinyWallSetup\Sources\CommonAppData\TinyWall\profiles.json'
$catalogueOutput = Join-Path $artifactRoot 'catalogue\profiles.db'
$catalogueReady = $false
$catalogueApplicationCount = 0
$msbuild = $null

if (-not $ValidateOnly -and -not $VerifyArchive) {
    $msbuild = Get-MSBuildPath
    if (-not (Get-Command dotnet.exe -ErrorAction SilentlyContinue)) {
        throw '.NET SDK (dotnet.exe) is required to publish Modern TinyWall.'
    }
}

foreach ($rid in $architectures) {
    $legacyPath = Join-Path $publishRoot "TinyWall\$rid"
    $modernPath = Join-Path $publishRoot "ModernTinyWall\$rid"
    $combinedPath = Join-Path $publishRoot "TinyWallDual\$rid"
    $archive = Join-Path $releaseRoot "TinyWall-$version-$rid.zip"
    if ($VerifyArchive) {
        Test-PackageArchive $archive $version $rid
        $checksumPath = "$archive.sha256"
        if (-not (Test-Path -LiteralPath $checksumPath)) {
            throw "Missing archive checksum: $checksumPath"
        }
        $expectedHash = ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -split '\s+')[0]
        if ($expectedHash -ne (Get-FileDigest $archive)) {
            throw 'Archive SHA-256 does not match the checksum sidecar.'
        }
        continue
    }

    if (-not $ValidateOnly) {
        foreach ($path in @($legacyPath, $modernPath, $combinedPath)) {
            if (Test-Path -LiteralPath $path) {
                Remove-Item -LiteralPath $path -Recurse -Force
            }
        }

        $legacyPublish = $legacyPath + [IO.Path]::DirectorySeparatorChar
        $buildRoot = Join-Path $artifactRoot "build\$rid"
        & $msbuild $legacyProject /restore /t:Publish /p:Configuration=Release '/p:Platform=Any CPU' `
            "/p:RuntimeIdentifier=$rid" /p:SelfContained=true "/p:PublishDir=$legacyPublish" `
            /p:UseArtifactsOutput=true "/p:ArtifactsPath=$buildRoot" /nologo /verbosity:minimal
        if ($LASTEXITCODE -ne 0) {
            throw "Publishing TinyWall for $rid with MSBuild failed."
        }

        $codeDomVersion = Get-LegacyPackageVersion $legacyPath 'System.CodeDom'
        $immutableVersion = Get-LegacyPackageVersion $legacyPath 'System.Collections.Immutable'
        $metadataVersion = Get-LegacyPackageVersion $legacyPath 'System.Reflection.Metadata'
        & dotnet publish $modernProject --configuration Release --runtime $rid --self-contained true `
            -p:WindowsAppSDKSelfContained=true --output $modernPath --artifacts-path $buildRoot `
            "-p:DirectoryBuildTargetsPath=$(Join-Path $PSScriptRoot 'Packaging.Dependencies.targets')" `
            "-p:PackagingCodeDomVersion=$codeDomVersion" "-p:PackagingImmutableVersion=$immutableVersion" `
            "-p:PackagingMetadataVersion=$metadataVersion" --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw "Publishing Modern TinyWall for $rid failed."
        }
    }

    if ($ValidateOnly) {
        Assert-CompatibleOutputs $legacyPath $modernPath
        Assert-PublishRuntime $legacyPath 'TinyWall' $rid $version
        Assert-PublishRuntime $modernPath 'ModernTinyWall' $rid $version
        Assert-CatalogueSeed (Join-Path $combinedPath 'catalogue\profiles.db')
        Write-Output "Validated $rid inputs, compatible shared files, and staged SQLite catalogue seed."
        continue
    }

    Assert-PublishRuntime $legacyPath 'TinyWall' $rid $version
    Assert-PublishRuntime $modernPath 'ModernTinyWall' $rid $version
    Join-PublishOutputs $legacyPath $modernPath $combinedPath
    if (-not $catalogueReady) {
        $sourceCatalogue = Get-Content -LiteralPath $catalogueSource -Raw | ConvertFrom-Json
        $catalogueApplicationCount = $sourceCatalogue.KnownApplications.Count
        if ($catalogueApplicationCount -le 0) {
            throw 'The source catalogue must contain at least one known application.'
        }
        $hostRid = switch ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) {
            'X64' { 'win-x64' }
            'X86' { 'win-x86' }
            'Arm64' { 'win-arm64' }
            default { throw 'Catalogue compilation requires an x86, x64, or ARM64 Windows host.' }
        }
        $compilerPath = $legacyPath
        if ($rid -ne $hostRid) {
            $compilerPath = Join-Path $publishRoot "CatalogueCompiler\$hostRid"
            if (Test-Path -LiteralPath $compilerPath) {
                Remove-Item -LiteralPath $compilerPath -Recurse -Force
            }
            $compilerBuild = Join-Path $artifactRoot "build\catalogue\$hostRid"
            & $msbuild $legacyProject /restore /t:Publish /p:Configuration=Release '/p:Platform=Any CPU' `
                "/p:RuntimeIdentifier=$hostRid" /p:SelfContained=true "/p:PublishDir=$compilerPath\" `
                /p:UseArtifactsOutput=true "/p:ArtifactsPath=$compilerBuild" /nologo /verbosity:minimal
            if ($LASTEXITCODE -ne 0) { throw 'Publishing the native-host offline catalogue compiler failed.' }
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $catalogueOutput) -Force | Out-Null
        if (Test-Path -LiteralPath $catalogueOutput) {
            Remove-Item -LiteralPath $catalogueOutput -Force
        }
        $compiler = Join-Path $compilerPath 'TinyWall.exe'
        $compilerProcess = Start-Process -FilePath $compiler -ArgumentList @(
            '/buildcatalogue', "`"$catalogueSource`"", "`"$catalogueOutput`""
        ) -Wait -PassThru -NoNewWindow
        if ($compilerProcess.ExitCode -ne 0) {
            throw "Offline catalogue compilation failed with exit code $($compilerProcess.ExitCode)."
        }
        Assert-CatalogueSeed $catalogueOutput
        $catalogueReady = $true
    }
    $seedDirectory = Join-Path $combinedPath 'catalogue'
    New-Item -ItemType Directory -Path $seedDirectory -Force | Out-Null
    Copy-Item -LiteralPath $catalogueOutput -Destination (Join-Path $seedDirectory 'profiles.db')
    Write-PackageManifest $combinedPath $version $rid $catalogueApplicationCount
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
    if (Test-Path -LiteralPath $archive) {
        Remove-Item -LiteralPath $archive -Force
    }
    Compress-Archive -Path (Join-Path $combinedPath '*') -DestinationPath $archive -CompressionLevel Optimal
    Test-PackageArchive $archive $version $rid
    "$(Get-FileDigest $archive)  $([IO.Path]::GetFileName($archive))" |
        Set-Content -LiteralPath "$archive.sha256" -Encoding ascii

    Write-Output "Packaged ${rid}: $archive"
}

if (-not $ValidateOnly -and -not $VerifyArchive) {
    Write-Output 'Archives are staging outputs, not installers. Build TinyWallSetup\TinyWallSetup.vdproj in Visual Studio to create the MSI.'
}
