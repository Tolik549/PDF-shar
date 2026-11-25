# Публикация self-contained версии для Windows (папка с exe + нативные DLL OCR).
# Запускать из корня репозитория:  .\tools\publish-win.ps1
$ErrorActionPreference = "Stop"

$rid = "win-x64"
$out = "publish/$rid"

Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $out
dotnet publish -c Release -r $rid --self-contained true -o $out

Remove-Item -Force -ErrorAction SilentlyContinue "$out/PDF-shar.pdb"
Compress-Archive -Path "$out/*" -DestinationPath "publish/PDF-shar-$rid.zip" -Force

Write-Host "Готово: publish/PDF-shar-$rid.zip"