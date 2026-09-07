@echo off
title Let me sleep - servidor UDP
echo Let me sleep 0.3.0 - servidor en esta PC
echo Deja esta ventana abierta mientras juegan.
echo Puerto por defecto: UDP 27840. Ver servidor.cfg y LEEME.html.
"%~dp0Let-me-sleep.exe" --headless --log-file "%~dp0servidor.log" -- --server --config="%~dp0servidor.cfg"
echo.
echo El servidor se cerro. Si hubo un error, revisa servidor.log.
pause
