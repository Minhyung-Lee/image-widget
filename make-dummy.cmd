@echo off
setlocal
set "compiler=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%compiler%" set "compiler=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%compiler%" exit /b 1
set "dummyGenerator=%TEMP%\ImageWidgetDummy-%RANDOM%-%RANDOM%.exe"
"%compiler%" /nologo /target:exe /reference:System.Drawing.dll /out:"%dummyGenerator%" "%~dp0MakeDummy.cs"
if errorlevel 1 exit /b 1
"%dummyGenerator%" "%~dp0image.png"
set "dummyResult=%errorlevel%"
del /q "%dummyGenerator%"
exit /b %dummyResult%
