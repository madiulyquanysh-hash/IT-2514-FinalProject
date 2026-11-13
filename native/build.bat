@echo off
setlocal
cd /d "%~dp0"
if not exist bin mkdir bin

where gcc >nul 2>nul
if %errorlevel%==0 (
    gcc -shared -O2 -Wall -Wextra -std=c99 -o bin\ticketfees.dll ticketfees.c
    if errorlevel 1 exit /b 1
    goto built
)

where cl >nul 2>nul
if %errorlevel%==0 (
    cl /nologo /O2 /W4 /LD ticketfees.c /Fo:bin\ /Fe:bin\ticketfees.dll
    if errorlevel 1 exit /b 1
    goto built
)

echo No C compiler found. Install MinGW-w64 (64-bit gcc) or run this script from the x64 Native Tools Command Prompt for Visual Studio.
exit /b 1

:built
echo Built native\bin\ticketfees.dll
exit /b 0