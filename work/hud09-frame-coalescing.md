# HUD: aplicar el estado pendiente desde el cuadro de presentación

Sustituye la implementación deferred documentada en hud09-coalescing.md. El perfil instrumentado perf09-view-attribution.json encontró 327 llamadas a flush en 108 cuadros: la cola diferida puede drenar entre ticks de física de recuperación. Es diagnóstico con sobrecoste, no un benchmark FPS.

Client conserva recepción, estado, cámara y sonido inmediatos. La cola sólo marca pendiente; el proceso idle consume el último par público/privado una vez antes de presentar el cuadro. Inicio, resultados y lobby siguen inmediatos. Salida y desconexión limpian el pendiente; los guards impiden reabrir una pantalla anterior. Sin paquetes nuevos no se aplica de nuevo UI.show_game. El contador del helper llamado desde _process no equivale al número de aplicaciones UI: medir dentro de la rama dirty.

## Validación del 8 de septiembre de 2026, 14:30 UTC

- Fixture headless: 29/29, exit 0, stderr vacío. Usa callbacks y _process reales, HUD/world/audio inertes. Cuatro drenajes de cola con presentación suspendida no redibujan; al retomarla se aplica sólo el último par. Incluye audio inmediato, transiciones, revancha, pausa, salida y desconexión. Este caso discrimina la implementación deferred anterior.
- Práctica nativa humano/Sangre: 29/29, exit 0, stderr vacío.
- Práctica nativa mosquito/Tareas: 25/25, exit 0, pero cierre con ObjectDB instances leaked y 16 resources still in use. La navegación pasó; el cierre NO queda certificado limpio. Causa pendiente, sin atribuir a este parche.
- Prácticas con Main real, bots, cámara, mapas y revancha; resultado de ronda inyectado sólo para navegación. Preferencias idénticas byte por byte; micrófono explícitamente deshabilitado, audio Dummy, Compatibility. Sin validación WAN, hardware de voz, EXE exportado ni mejora FPS cuantificada.
- Revisión estática independiente sin regresión confirmada; git diff --check limpio.

Evidencia: director09-hud-frame-coalescing.json/.run.json/.stdout.log/.stderr.log y director09-hud-frame-practice-{human,mosquito} con los mismos sufijos, dentro de work.

SHA256 Client: 783A412E2255943391E157AAEB9440FD317C8B3852FFEB5C609970EAED9EFCFB.

SHA256 fixture: 0FACC59CD88198AE90F1C3E4A78224E2752C7E3032CDE34F610BF530B3CF4A87.

SHA256 Godot 4.5.2: 446E08F71624052572F96DE9031850BA96382CE6752ADDE38BB955B0A49BED01.
