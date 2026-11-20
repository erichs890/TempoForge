# Compila rubberband.dll (x64) a partir do código-fonte oficial do Rubber Band,
# usando o arquivo único single/RubberBandSingle.cpp (FFT e reamostrador embutidos).
#
# Uso:  .\native\build-rubberband.ps1 -Source <pasta do rubberband> -Zig <zig.exe>
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$Zig = 'zig',
    [string]$Output = ''
)
$ErrorActionPreference = 'Stop'
if (-not $Output) {
    $here = Split-Path -Parent $MyInvocation.MyCommand.Path
    $Output = Join-Path $here '..\src\TempoForge.Native\runtimes\win-x64\native'
}

$single = Join-Path $Source 'single\RubberBandSingle.cpp'
if (-not (Test-Path $single)) { throw "Não achei $single" }

New-Item -ItemType Directory -Force $Output | Out-Null
$dll = Join-Path $Output 'rubberband.dll'

& $Zig c++ -target x86_64-windows-gnu -shared -O3 -DNDEBUG -std=c++14 `
    -fno-sanitize=undefined -s `
    "-I$Source" $single -o $dll
if ($LASTEXITCODE -ne 0) { throw "Falha na compilação ($LASTEXITCODE)" }

# O linker deixa uma biblioteca de importação e símbolos de depuração que não distribuímos.
Get-ChildItem $Output -Include *.lib, *.pdb -Recurse | Remove-Item -Force
Get-Item $dll | Select-Object Name, Length
