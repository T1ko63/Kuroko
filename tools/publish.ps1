# Builds a release version of Kuroko into publish\Kuroko (framework-dependent) or publish\Kuroko-selfcontained,
# and zips it as publish\Kuroko-<version>-win-x64.zip (or ...-win-x64-selfcontained.zip) for the GitHub release.
# The version comes from Directory.Build.props.
#   powershell -File tools\publish.ps1                 # needs the .NET 10 Desktop Runtime on the target PC (small)
#   powershell -File tools\publish.ps1 -SelfContained  # runs anywhere, ~150 MB
# ReadyToRun pre-compiles the code, which is what makes the first hotkey fast after a cold start.
param([switch]$SelfContained, [switch]$NoReadyToRun)

$root = Split-Path -Parent $PSScriptRoot
$name = if ($SelfContained) { 'Kuroko-selfcontained' } else { 'Kuroko' }
$out = Join-Path $root "publish\$name"
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
if (-not $version) { throw "no <Version> in Directory.Build.props" }
$suffix = if ($SelfContained) { '-selfcontained' } else { '' }
$zip = Join-Path $root "publish\Kuroko-$version-win-x64$suffix.zip"

# Start clean so files from an older build do not end up in the zip.
if (Test-Path $out) {
    try { Remove-Item $out -Recurse -Force -ErrorAction Stop }
    catch { throw "Cannot clear $out. Is Kuroko still running from there? Quit it from the tray and try again." }
}

dotnet publish (Join-Path $root 'src\Kuroko.App\Kuroko.App.csproj') `
    -c Release -r win-x64 `
    --self-contained:$SelfContained `
    -p:PublishReadyToRun=$(-not $NoReadyToRun) `
    -p:DebugType=none -p:DebugSymbols=false `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

$size = (Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
"Published Kuroko $version to $out ({0:F1} MB)" -f $size
if (-not $SelfContained) {
    'The target PC needs the .NET 10 Desktop Runtime (x64): https://dotnet.microsoft.com/download/dotnet/10.0'
}

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
"Zipped to $zip"
