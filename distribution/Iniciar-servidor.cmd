@echo off
title Dejame dormir - servidor UDP
echo Dejame dormir 0.1.0 - servidor en esta PC
echo Deja esta ventana abierta mientras juegan.
echo Puerto por defecto: UDP 27840. Ver servidor.cfg y LEEME.html.
"%~dp0Dejame-dormir.exe" --headless --log-file "%~dp0servidor.log" -- --server --config="%~dp0servidor.cfg"
echo.
echo El servidor se cerro. Si hubo un error, revisa servidor.log.
pause
