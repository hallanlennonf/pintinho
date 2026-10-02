@echo off
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Compilador nao encontrado: %CSC%
  exit /b 1
)
set /p VER=<VERSION

rem Versao vem do arquivo VERSION (fonte unica para o app e o instalador)
echo // Gerado pelo build.bat a partir do arquivo VERSION. Nao edite.> src\AppInfo.g.cs
echo [assembly: System.Reflection.AssemblyVersion("%VER%.0")]>> src\AppInfo.g.cs
echo [assembly: System.Reflection.AssemblyFileVersion("%VER%.0")]>> src\AppInfo.g.cs
echo [assembly: System.Reflection.AssemblyTitle("Pintinho")]>> src\AppInfo.g.cs
echo [assembly: System.Reflection.AssemblyProduct("Pintinho")]>> src\AppInfo.g.cs
echo namespace Pintinho { static class AppInfo { public const string Version = "%VER%"; } }>> src\AppInfo.g.cs

set ICON=
if exist assets\pintinho.ico set ICON=/win32icon:assets\pintinho.ico

if not exist dist mkdir dist
if not exist dist\desenhos mkdir dist\desenhos
if not exist dist\desenhos-aconchego mkdir dist\desenhos-aconchego
"%CSC%" /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ %ICON% /out:dist\Pintinho.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll src\*.cs
if errorlevel 1 (
  echo FALHOU
  exit /b 1
)
echo OK: dist\Pintinho.exe v%VER%
