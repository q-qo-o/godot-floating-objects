@echo off
cd /d "C:\Users\qdsyq\Desktop\godot-floating-objects"
echo === Starting dotnet build ===
dotnet build 2>&1
echo === Build exit code: %ERRORLEVEL% ===
