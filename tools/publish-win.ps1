# Публикация self-contained версии для Windows (папка с exe + нативные DLL OCR).
# Запускать из корня репозитория:  .\tools\publish-win.ps1
$ErrorActionPreference = "Stop"

$rid = "win-x64"
$out = "publish/$rid"

Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $out
dotnet publish -c Release -r $rid --self-contained true -o $out

Remove-Item -Force -ErrorAction SilentlyContinue "$out/PDF-shar.pdb"

# Tesseract (tesseract50.dll) требует Microsoft Visual C++ Runtime.
# Вкладываем его DLL в корень и в x64/ рядом с нативными библиотеками OCR.
New-Item -ItemType Directory -Force -Path "$out/x64" | Out-Null
Copy-Item runtime/vcrt/*.dll -Destination $out -Force
Copy-Item runtime/vcrt/*.dll -Destination "$out/x64" -Force

Remove-Item -Force -ErrorAction SilentlyContinue "publish/PDF-shar-$rid.zip"
Compress-Archive -Path "$out/*" -DestinationPath "publish/PDF-shar-$rid.zip" -Force

Write-Host "Готово: publish/PDF-shar-$rid.zip"