# Let me sleep 0.9.2 — pruebas de esta descarga

Windows x86_64, Godot4.5.2, protocolo11, casasv3.
Fuente: 642b8831aa0cb3714fa9be97690fffed141f131f
SHA256 EXE: 348372B149FA7FCB4FB488A34A1941ABE55574F3F0249DAB785C859206F0715F

Las verificaciones director092-full-final-r5 y su continuación r6 pasaron las suites
headless y nativas. R6 reutilizó evidencia R5 con hash inmutable y código del juego sin cambios, tras actualizar sólo la prueba de cámara. Incluye física,
combate, cosméticos, movimiento, voz, sincronización, herramientas y menús.
Once semillas pasaron mobiliario y recorridos físicos hacia tareas/objetos:
1,2,7,31,97,257,997,2026,65537,1234567,2147483646. La caché pasó40rondas.
El estrés adicional de1000semillas no se ejecutó en esta entrega.
Manos:64848comprobaciones de movimiento y36058de malla sin fallos.
Marcas:972comprobaciones,540registros con herramientas y atuendos.
No es una revisión visual exhaustiva de todas las combinaciones.

El EXE pasó9escenarios: anfitrión EOS, pareja local, red, sesión, menús,
puertas, cámara/cuerpo y práctica de humano/mosquito. Crear un anfitrión EOS
real también pasó en build. La pareja local usa ENet: no certifica relay WAN.
Se generaron capturas con el EXE y tres mediciones a1080p, FPS sin límite,
RTX3060Ti/Ryzen5600X. Datos: work/director092-perf-final/summary.json.
No se certifican60FPS sostenidos ni GTX1660Ti/1440p/4K.

La prueba entre dos casas y el micrófono físico siguen pendientes.
Posarse sobre algunos muebles todavía usa cajas envolventes. Inventario de
3slots, estamina y nuevo balance quedan pendientes. LEEME explica cómo crear
una sala dentro del juego y compartir la invitación LMS1-.