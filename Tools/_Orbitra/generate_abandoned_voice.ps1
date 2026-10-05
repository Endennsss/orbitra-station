# Генерирует объявления через edge-tts; нужны Python с edge-tts, ffmpeg и интернет.
# Сервису передаются только тексты объявлений из локализации. В игре интернет для озвучки не нужен.
param(
    [string] $Python = 'python',
    [string] $Voice = 'ru-RU-SvetlanaNeural',
    [string] $Rate = '-5%'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputDir = Join-Path $repoRoot 'Resources/Audio/_Orbitra/Announcements'
$localePath = Join-Path $repoRoot 'Resources/Locale/ru-RU/_Orbitra/abandoned-station.ftl'
$signalPaths = @{
    warning = Join-Path $repoRoot 'Resources/Audio/Announcements/announce.ogg'
    isolated = Join-Path $repoRoot 'Resources/Audio/Misc/notice1.ogg'
}
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
& $Python -m edge_tts --version
if ($LASTEXITCODE -ne 0) { throw 'Установите edge-tts в выбранное окружение Python.' }
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$temporaryDir = Join-Path ([IO.Path]::GetTempPath()) ('orbitra-voice-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryDir | Out-Null

try {
  foreach ($phase in @('warning', 'isolated')) {
    $key = "orbitra-abandoned-station-$phase = "
    $line = Get-Content -LiteralPath $localePath -Encoding UTF8 | Where-Object { $_.StartsWith($key) }
    if (-not $line) { throw "Не найдена строка $key" }
    $speechPath = Join-Path $temporaryDir "abandoned_$phase.mp3"
    $textPath = Join-Path $temporaryDir "abandoned_$phase.txt"
    $oggPath = Join-Path $temporaryDir "abandoned_$phase.ogg"
    [IO.File]::WriteAllText($textPath, $line.Substring($key.Length), [Text.UTF8Encoding]::new($false))
    & $Python -m edge_tts --voice $Voice "--rate=$Rate" --file $textPath --write-media $speechPath
    if ($LASTEXITCODE -ne 0) { throw "Не удалось синтезировать объявление $phase" }

    # Первое сообщение предваряет обычное уведомление, второе — тревога.
    & $ffmpeg -hide_banner -loglevel error -y `
        -i $signalPaths[$phase] `
        -f lavfi -i 'anullsrc=r=44100:cl=mono:d=0.35' -i $speechPath `
        -filter_complex '[0:a]loudnorm=I=-18:TP=-2:LRA=7,aresample=44100,aformat=channel_layouts=mono[alarm];[2:a]highpass=f=100,lowpass=f=9000,aecho=0.8:0.88:120|260:0.22|0.10,loudnorm=I=-18:TP=-2:LRA=7,aresample=44100[speech];[alarm][1:a][speech]concat=n=3:v=0:a=1[out]' `
        -map '[out]' -ac 1 -ar 44100 -c:a libvorbis -q:a 5 $oggPath
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg завершился с кодом $LASTEXITCODE" }
  }

  # Заменяем игровые файлы только после успешной генерации обоих объявлений.
  foreach ($phase in @('warning', 'isolated')) {
    Copy-Item -LiteralPath (Join-Path $temporaryDir "abandoned_$phase.ogg") -Destination $outputDir -Force
  }
}
finally {
    Get-ChildItem -LiteralPath $temporaryDir -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
    Remove-Item -LiteralPath $temporaryDir
}
