#!/usr/bin/env bash
# Публикация self-contained версии для Windows (папка с exe + нативные DLL OCR).
# Запускать из корня репозитория:  bash tools/publish-win.sh
set -e
RID=win-x64
OUT="publish/$RID"
rm -rf "$OUT"
dotnet publish -c Release -r "$RID" --self-contained true -o "$OUT"
rm -f "$OUT/PDF-shar.pdb"
(cd "$OUT" && zip -q -r "../PDF-shar-$RID.zip" .)
echo "Готово: publish/PDF-shar-$RID.zip"