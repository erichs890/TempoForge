# Gera a versão Release em publish\ e, se o Inno Setup 6 estiver instalado, o instalador
# em installer\Output\.
#
# Uso:  .\tools\build-release.ps1 [-SkipTests] [-SkipInstaller]
param([switch]$SkipTests, [switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

if (-not $SkipTests) {
    dotnet test TempoForge.sln -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Os testes falharam.' }
}

# Compila em artifacts\, separado de bin\, para funcionar mesmo com o app aberto.
$publish = Join-Path $root 'publish'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish src\TempoForge.App\TempoForge.App.csproj -c Release -r win-x64 --self-contained true `
    -p:DebugType=none -o $publish --artifacts-path (Join-Path $root 'artifacts') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falha no publish.' }

if ($SkipInstaller) { return }

$iscc = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 não encontrado (winget install JRSoftware.InnoSetup).' }

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = $props.Project.PropertyGroup.Version
& $iscc "/DAppVersion=$version" installer\TempoForge.iss
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar o instalador.' }
Get-ChildItem installer\Output\*.exe | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
