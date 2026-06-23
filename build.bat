@echo off
cd /d "%~dp0"
echo [PixCake Helper] Compiling...

set CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    set CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC%" (
    echo ERROR: Cannot find csc.exe. Please ensure .NET Framework 4.x is installed.
    pause
    exit /b 1
)

"%CSC%" /nologo /target:winexe /out:PixCakeHelper.exe /win32icon:app.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll src\PixCakeHelper.cs

if %errorlevel% neq 0 (
    echo.
    echo Compilation FAILED. See errors above.
    pause
    exit /b 1
)

echo Compilation successful: PixCakeHelper.exe
echo.
