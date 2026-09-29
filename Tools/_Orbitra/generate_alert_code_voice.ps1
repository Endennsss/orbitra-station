# Генерирует голосовые объявления кодов через edge-tts; нужны Python с edge-tts, ffmpeg и интернет.
param(
    [string] $Python = 'python',
    [string] $Voice = 'ru-RU-SvetlanaNeural',
    [string] $Rate = '-5%'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputDir = Join-Path $repoRoot 'Resources/Audio/_Orbitra/AlertLevels'
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$alarmPaths = @{
    green = Join-Path $repoRoot 'Resources/Audio/Announcements/announce.ogg'
    blue = Join-Path $repoRoot 'Resources/Audio/Misc/bluealert.ogg'
    yellow = Join-Path $repoRoot 'Resources/Audio/Misc/notice1.ogg'
    red = Join-Path $repoRoot 'Resources/Audio/Misc/redalert.ogg'
    violet = Join-Path $repoRoot 'Resources/Audio/Announcements/outbreak7.ogg'
    delta = Join-Path $repoRoot 'Resources/Audio/Misc/delta.ogg'
    epsilon = Join-Path $repoRoot 'Resources/Audio/Misc/epsilon.ogg'
    gamma = Join-Path $repoRoot 'Resources/Audio/Misc/gamma.ogg'
    isolation = Join-Path $repoRoot 'Resources/Audio/Announcements/attention.ogg'
}
$codes = [ordered]@{
    green = 'Внимание. Код зелёный.'
    blue = 'Внимание. Код синий.'
    yellow = 'Внимание. Код жёлтый.'
    red = 'Внимание. Код красный.'
    violet = 'Внимание. Код фиолетовый.'
    delta = 'Внимание. Код дельта.'
    epsilon = 'Внимание. Код эпсилон.'
    gamma = 'Внимание. Код гамма.'
    isolation = 'Внимание. Код изоляции.'
}

& $Python -m edge_tts --version
if ($LASTEXITCODE -ne 0) { throw 'Установите edge-tts в выбранное окружение Python.' }
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$temporaryDir = Join-Path ([IO.Path]::GetTempPath()) ('orbitra-alert-voice-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryDir | Out-Null

try {
    foreach ($code in $codes.Keys) {
        $textPath = Join-Path $temporaryDir "$code.txt"
        $speechPath = Join-Path $temporaryDir "$code.mp3"
        $outputPath = Join-Path $outputDir "code_$code.ogg"
        [IO.File]::WriteAllText($textPath, $codes[$code], [Text.UTF8Encoding]::new($false))
        & $Python -m edge_tts --voice $Voice "--rate=$Rate" --file $textPath --write-media $speechPath
        if ($LASTEXITCODE -ne 0) { throw "Не удалось синтезировать код $code" }
        & $ffmpeg -hide_banner -loglevel error -y `
            -i $alarmPaths[$code] `
            -f lavfi -i 'anullsrc=r=44100:cl=mono:d=0.35' `
            -i $speechPath `
            -filter_complex '[0:a]loudnorm=I=-18:TP=-2:LRA=7,aresample=44100,aformat=channel_layouts=mono[alarm];[2:a]highpass=f=100,lowpass=f=9000,aecho=0.8:0.88:120|260:0.22|0.10,loudnorm=I=-18:TP=-2:LRA=7,aresample=44100[speech];[alarm][1:a][speech]concat=n=3:v=0:a=1[out]' `
            -map '[out]' -ac 1 -ar 44100 -c:a libvorbis -q:a 5 $outputPath
        if ($LASTEXITCODE -ne 0) { throw "ffmpeg завершился с кодом $LASTEXITCODE для $code" }
    }
}
finally {
    if (Test-Path $temporaryDir) { Remove-Item -LiteralPath $temporaryDir -Recurse -Force }
}
