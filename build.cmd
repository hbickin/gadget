@echo off
setlocal

if /i "%1"=="publish" goto publish

echo === MyGadget - Release derlemesi ===
dotnet build -c Release
if errorlevel 1 (
    echo.
    echo Derleme BASARISIZ oldu. Yukaridaki hatalari inceleyin.
    pause
    exit /b 1
)
echo.
echo Derleme tamamlandi. Cikti: bin\Release\net8.0-windows\MyGadget.exe
pause
exit /b 0

:publish
echo === MyGadget - Dagitim paketi (tek dosya exe) ===
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
if errorlevel 1 (
    echo.
    echo Yayinlama BASARISIZ oldu. Yukaridaki hatalari inceleyin.
    pause
    exit /b 1
)
echo.
echo Yayinlama tamamlandi. Cikti: publish\MyGadget.exe
pause
exit /b 0
