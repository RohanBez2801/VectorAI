$baseDir = $PSScriptRoot
$piperDir = Join-Path $baseDir "piper"

# Create directory
if (!(Test-Path $piperDir)) {
    New-Item -ItemType Directory -Path $piperDir | Out-Null
    Write-Host "Created $piperDir"
}

# 1. Download Piper Binary
$zipUrl = "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip"
$zipPath = Join-Path $piperDir "piper.zip"

if (!(Test-Path "$piperDir\piper.exe")) {
    Write-Host "Downloading Piper..."
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath
    
    Write-Host "Extracting Piper..."
    Expand-Archive -Path $zipPath -DestinationPath $baseDir -Force
    
    # Clean up zip
    Remove-Item $zipPath
} else {
    Write-Host "Piper already installed."
}

# 2. Download Voice Model (Ryan Medium)
$voiceUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_US/ryan/medium/en_US-ryan-medium.onnx?download=true"
$jsonUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_US/ryan/medium/en_US-ryan-medium.onnx.json?download=true"

$voicePath = Join-Path $piperDir "en_US-ryan-medium.onnx"
$jsonPath = Join-Path $piperDir "en_US-ryan-medium.onnx.json"

if (!(Test-Path $voicePath)) {
    Write-Host "Downloading Voice Model (onnx)..."
    Invoke-WebRequest -Uri $voiceUrl -OutFile $voicePath
}

if (!(Test-Path $jsonPath)) {
    Write-Host "Downloading Voice Config (json)..."
    Invoke-WebRequest -Uri $jsonUrl -OutFile $jsonPath
}

Write-Host "Piper Setup Complete."
