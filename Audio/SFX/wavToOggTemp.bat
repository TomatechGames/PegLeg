@echo off
cd %1
for /f "tokens=1 delims=." %%a in ('dir /B *.wav') do ffmpeg -i "%%a.wav" "%%a.ogg"