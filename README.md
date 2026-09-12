# Let me sleep — candidata Unity 0.9.4-alfa.2

[Descargar launcher 1.1.1](https://github.com/Sauri0/LetMeSleep/releases/download/v0.9.4-alfa.2/Let-me-sleep-Launcher.exe) · [ZIP y notas de la candidata](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.9.4-alfa.2)

Juego cómico de humanos contra mosquitos para Windows. Esta es la primera candidata de la migración a Unity: Sangre, entrenamiento con bots para ambos roles, casa fija de dos pisos con patio y sala de espera 3D independiente.

## Instalar

1. Descargá **Let-me-sleep-Launcher.exe** del enlace anterior.
2. Elegí dónde instalar y pulsá **Instalar y jugar**. No necesitás Unity ni una cuenta de GitHub.
3. Usá ese launcher para abrir el juego y recibir las próximas etapas.

Hace falta el launcher **1.1.1** para reconocer revisiones como alfa.1 además de alfa/beta/omega/delta/gamma. Si ya instalaste alfa, descargá este launcher una vez y elegí la misma carpeta del juego. El cambio de motor conserva la versión Godot publicada como descarga histórica; la importación completa de sus ajustes y cosméticos corresponde a gamma.

## Jugar con amigos

1. Todos instalan **0.9.4-alfa.2**.
2. Una persona elige **Jugar online → Crear sala**, escribe su nombre y comparte el código.
3. Los amigos eligen **Unirme con código**, escriben su nombre y pegan el código.
4. Todos marcan **Listo**. El anfitrión configura los ajustes y pulsa **Iniciar ronda**.

El anfitrión ejecuta la partida dentro del juego y debe permanecer conectado. No hay consola de servidor, IP, puertos ni opciones LAN en este flujo. Al salir el anfitrión se cierra la sala.

**Online pendiente de aprobación:** crear/cerrar una sala EOS fue probado en Windows. Todavía falta verificar unión/transporte de dos identidades independientes, varias rondas y conexión entre dos casas. Publicar esta candidata permite esas pruebas; no certifica que el online completo ya esté aprobado.

## Probar solo y controles

Elegí **Entrenamiento**, humano o mosquito, y **Empezar entrenamiento**. Alfa ofrece únicamente Sangre; otros modos no aparecen como terminados.

- Humano: WASD y ratón; Espacio para saltar, Ctrl para agacharse, Shift para correr, clic izquierdo para golpear, F para interactuar/recoger y G para soltar herramienta.
- Mosquito: W avanza hacia donde mirás y al soltar frena; F alterna posado; mantené E cerca del cuerpo para picar y volvé a pulsar E para desprenderte. Rueda ajusta distancia de cámara.
- Escape abre o cierra el menú contextual. Ajustes permite sensibilidad, calidad, límite de FPS y sincronización; FPS sin límite por defecto.
- Personalizar está en el menú principal: colores base de humano/mosquito, giro, zoom y vistas. El humano inicial lleva pijama, pantuflas y gorro nocturno.

## Estado y código

Compilación Unity **6000.3.24f1**, Windows x64, commit fuente `0a82314da866f94ae71e3807cdaea875b3d111a9`. El ZIP incluye `BUILD.json` con hashes individuales y un SHA-256 externo. Es un **Development Build**: conserva símbolos de diagnóstico managed/Burst.

Alfa.2 corrige la cancelación del posado al acercarse a una superficie, la cámara demasiado retraída al posarse y la distribución de sombras puntuales dentro del atlas PC. Pasaron 76 comprobaciones de lógica Gameplay, dos PlayMode de cámara y una ruta física por escalera/patio. El ejecutable Windows pasó entrenamiento de ambos roles, retorno al menú y creación/cierre de sala EOS. Conserva la recuperación de ajustes y protección de perfiles futuros de alfa.1.

El launcher 1.1.1 reconoce esta revisión. [Informe de alcance y comprobaciones alfa.2](docs/unity/qa/FINAL-REVIEW-0.9.4-ALFA2-CANDIDATE.md). Las pruebas e informes anteriores conservan su versión de origen: no se presentan como una repetición integral sobre alfa.2.
Pendientes: conexión entre jugadores/redes independientes, recorrido manual completo, aprobación artística y balance por Branko, medición en GTX 1660 Ti. La revisión de interfaz del equipo pasó; eso no sustituye la aprobación artística del usuario. Los datos cortos obtenidos en RTX 3060 Ti no certifican otros equipos ni una partida completa.

- Proyecto activo: [unity](unity/).
- [Plan por entregas](docs/unity/PLAN-UNITY-0.9.4.md): alfa → beta → omega → delta → gamma. Beta espera prueba y aprobación de alfa.
- [Validaciones pendientes](docs/unity/PENDING-EXTERNAL.md) y [evidencia QA](docs/unity/qa/README.md).
- [Versión Godot 0.9.3 anterior](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.9.3), conservada junto a su historial y fuentes en `game/`.
