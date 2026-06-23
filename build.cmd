@echo off
setlocal
cd /d "C:\Users\qdsyq\Desktop\godot-floating-objects"
echo BUILD_OUTPUT_START
dotnet build 2>&1
echo BUILD_EXIT_CODE:%ERRORLEVEL%
endlocal
