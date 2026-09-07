@echo off
title Build e Deploy - TsOverlay
echo Iniciando processo de automatizacao...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File %~dp0build.ps1
echo.
pause