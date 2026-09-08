# Cierre de audio — candidato fuente 0.9

El cierre de la ventana podía dejar reproducciones sincronizadas del menú
referenciadas al terminar Godot. El caso de hosting completaba sus 13 acciones,
pero el log verbose denunciaba recursos de audio vivos. Cerrar el informe y
usar el cierre normal de la ventana no resolvieron por sí solos esa fuga.

Main ahora detiene MusicDirector antes de los 250 ms que ya concedía al cierre
de red. El estado final de MusicDirector también ignora señales tardías de
pantallas, acentos y controles, para que no reinicien el mezclador. `clear()`
sigue siendo reutilizable durante el juego; sólo `shutdown()` es definitivo.

## Verificación

- `release07-director09-hosting-audio-shutdown.run.json`: ejecución nativa
  Compatibility/Dummy, cierre real de ventana, 13/13, exit 0, stderr vacío.
  Log verbose sin las fugas observadas antes. Micrófono deshabilitado.
- `release07-music09-shutdown.run.json`: música con AudioDriverDummy,
  49/49, exit 0, stderr vacío. Incluye continuidad, cambios de contexto,
  mezcla, recuperación del aturdimiento, silencio independiente, reutilización
  de clear y bloqueo de las tres entradas tardías tras shutdown.
- El PCM grabado proviene del mezclador de Godot, no de un dispositivo de
  entrada: pico 5184/32767, audio no nulo y margen de saturación conservado.

Los intentos anteriores de hosting y sus diagnósticos se conservan. Esta
verificación corresponde a fuente; no se atribuye al EXE 0.7 anterior ni
certifica todavía el futuro paquete 0.9.
