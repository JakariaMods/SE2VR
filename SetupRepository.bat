@echo off
set /p gameDllsPath="Enter the *directory* where SpaceEngineers2.exe is stored (do not includes quotes): "

if exist "%gameDllsPath%" (
    mklink /J GameLibs "%gameDllsPath%"
) else (
    echo The path does not exist
)

pause