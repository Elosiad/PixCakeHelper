@echo off
cd /d "%~dp0"
if not exist PixCakeHelper.exe (
    echo First run - compiling...
    call build.bat
)
start "" "%~dp0PixCakeHelper.exe"
