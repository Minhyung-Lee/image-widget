@echo off
setlocal
set "compiler=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%compiler%" set "compiler=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%compiler%" (
  echo .NET Framework C# compiler not found.
  exit /b 1
)
set "widgetOutput=%~dp0bin"
if not "%~1"=="" set "widgetOutput=%~f1"
if not exist "%widgetOutput%" mkdir "%widgetOutput%"
if errorlevel 1 exit /b 1
"%compiler%" /nologo /codepage:65001 /target:winexe /optimize+ /win32manifest:"%~dp0widget.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:"%widgetOutput%\ImageWidget.exe" "%~dp0ImageWidget.cs"
if errorlevel 1 exit /b 1
copy /y "%~dp0image.png" "%widgetOutput%\image.png" >nul
exit /b %errorlevel%
