@echo off
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Compilador nao encontrado: %CSC%
  exit /b 1
)
if not exist dist mkdir dist
if not exist dist\desenhos mkdir dist\desenhos
"%CSC%" /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ /out:dist\Pintinho.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll src\*.cs
if errorlevel 1 (
  echo FALHOU
  exit /b 1
)
echo OK: dist\Pintinho.exe
