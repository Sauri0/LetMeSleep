# Inicio con actualización automática · 1.0.0

Descargar una sola vez **Let-me-sleep-Launcher.exe** y abrir siempre ese archivo (o un acceso directo a él). El launcher instala y actualiza el juego; el juego continúa en la versión 0.9.3 hasta que se publique otra. El ejecutable antiguo del juego no adquiere actualización automática por sí solo.

El launcher consulta la [API pública de releases de GitHub](https://docs.github.com/en/rest/releases/releases). Entre las últimas 100 publicaciones elige la versión numérica más alta con ZIP Windows y checksum completos. Incluye las versiones de prueba públicas de Let me sleep; excluye borradores, tags ajenos, publicaciones incompletas y paquetes para otros sistemas. No pide credenciales ni accede a repositorios privados.

Instala en `%LOCALAPPDATA%\LetMeSleep\versions`, con una carpeta independiente por versión. Descarga por HTTPS, verifica tamaño, SHA-256 y el manifiesto BUILD.json del paquete. Rechaza rutas inseguras y colisiones de nombres del ZIP. Solo activa una instalación validada mediante reemplazo atómico de `current.txt`. Conserva las instalaciones anteriores; por ahora no elimina automáticamente versiones viejas. No toca los ajustes Godot en `user://` ni la carpeta desde la que se descargó el launcher.

Si GitHub no responde o una descarga falla, permite reintentar o abrir la versión instalada, cuando sigue íntegra. No intenta actualizar durante una partida. Para jugar online todos necesitan la misma versión. Un mutex evita dos actualizaciones o partidas simultáneas iniciadas por el launcher dentro de la sesión de Windows.

El launcher 1.0.0 actualiza **el juego**, no su propio ejecutable. Requiere Windows de 64 bits con .NET Framework 4.5 o posterior (incluido en Windows 10/11). No requiere .NET SDK en la computadora de los jugadores. Los futuros paquetes generados por `work/build.ps1` incluyen el launcher y `Jugar.cmd` lo usa preferentemente. Los ZIP históricos permanecen intactos.

Compilar: `powershell -File work/build-launcher.ps1`. Compila con el C# de .NET Framework y ejecuta 31 comprobaciones de selección de versiones, integridad, rutas, extracción, cancelación y activación. La prueba `work/updater-tests.exe --install-latest-no-launch <carpeta-aislada>` instala realmente desde GitHub y comprueba que un segundo arranque no reinstala. Nunca abre el juego.

Validación 2026-09-12: 31 comprobaciones locales y 3 de descarga/instalación real de 0.9.3, todas correctas. No se abrió el launcher gráfico ni el juego, respetando la indicación del usuario. La interacción visual y el lanzamiento de una partida quedan sin prueba manual en esta entrega. La verificación online entre dos casas no cambia.
