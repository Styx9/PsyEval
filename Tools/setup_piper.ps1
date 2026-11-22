# Descarca Piper (TTS offline) + vocea romaneasca "mihai" in Tools/piper.
# Folosit de meniul Unity: PsyEval > Genereaza vocea pentru calibrare.
#
#   powershell -ExecutionPolicy Bypass -File Tools/setup_piper.ps1

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$dir = Join-Path $PSScriptRoot 'piper'
New-Item -ItemType Directory -Force $dir | Out-Null

if (-not (Test-Path (Join-Path $dir 'piper\piper.exe'))) {
    Write-Host 'Descarc Piper...'
    $zip = Join-Path $dir 'piper.zip'
    Invoke-WebRequest 'https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip' -OutFile $zip
    Expand-Archive $zip -DestinationPath $dir -Force
    Remove-Item $zip
}

# Revizie fixata: versiunile mai noi ale vocii cer Piper >= 1.5 (incompatibile cu acest executabil).
$rev = 'e7229f7d33a10e145e109b886d661984201ac3c4'
$base = "https://huggingface.co/rhasspy/piper-voices/resolve/$rev/ro/ro_RO/mihai/medium"
foreach ($f in 'ro_RO-mihai-medium.onnx', 'ro_RO-mihai-medium.onnx.json') {
    if (-not (Test-Path (Join-Path $dir $f))) {
        Write-Host "Descarc $f..."
        Invoke-WebRequest "$base/$f" -OutFile (Join-Path $dir $f)
    }
}
Write-Host "Gata. Piper este in $dir"
