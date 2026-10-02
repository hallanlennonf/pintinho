@echo off
setlocal
cd /d "%~dp0"
call "%~dp0build.bat" || exit /b 1
set /p VER=<VERSION

set ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe
if not exist "%ISCC%" set ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe
if not exist "%ISCC%" (
  echo Inno Setup nao encontrado. Instale com: winget install JRSoftware.InnoSetup
  exit /b 1
)
"%ISCC%" /Q /DAppVersion=%VER% installer\Pintinho.iss
if errorlevel 1 (
  echo FALHOU o instalador
  exit /b 1
)
echo OK: release\Pintinho-Setup-%VER%.exe
