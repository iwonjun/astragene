@echo off
echo 전용 서버를 시작합니다. 창을 닫으면 서버가 종료됩니다.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\launch.ps1" -Mode server
