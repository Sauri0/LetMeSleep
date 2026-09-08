# Práctica completa en fuente 0.9.0

8 de septiembre de 2026. Director corrigió únicamente game/tests/practice_ui_checks.gd, con ownership concedido por Worker y devuelto al terminar. No cambió runtime ni reglas.

El fixture anterior exigía el mapa autoral house aunque Client ya genera casas. Ahora verifica identidad y fingerprint entre autoridad, snapshot y mundo renderizado, registra el mapa inicial y revancha, mide salto respecto del piso real y compensa sensibilidad e inversión de mouse guardadas para el vuelo. Conserva bots, acciones y navegación reales; permite dividir la matriz mediante check-role y check-mode. Revisa preferencias byte por byte y salida sin sim ni brains. La revisión independiente detectó un contador de informe prematuro, corregido antes de ejecutar.

Parse limpio y seis procesos nativos: **157 comprobaciones, cero fallos**, salida cero y stderr vacío en todos.

| Rol | Modo interno | Checks |
|---|---|---:|
| human | blood | 29 |
| human | survival | 25 |
| human | sleep | 25 |
| mosquito | blood | 28 |
| mosquito | survival | 25 |
| mosquito | sleep | 25 |

Los seis casos recorren menú de práctica, inicio con bots, pausa Escape, bloqueo de movimiento mientras el menú está abierto, continuación del reloj, resultados, revancha con una casa generada y salida al menú/lobby. Blood añade movimiento real: humano corre, salta, se agacha con cambio de cámara y ataca; mosquito asciende con W apuntado y frena al soltar. Esos controles adicionales no se triplican artificialmente en los otros modos.

El resultado de ronda se inyecta exclusivamente para probar navegación: esto no demuestra condiciones naturales de victoria. Las seis revanchas observadas cargaron identificadores distintos, con identidad autoritativa/render consistente. Los seeds iniciales fueron 1 y 2.

Dummy y no-microphone evitan salida de audio y captura/enumeración de entrada. Preferencias intactas en seis procesos. Sin capturas PNG nuevas ni aprobación estética inferida. Algunos procesos coincidieron con el consumidor offline de malla fit4: ninguna cifra se presenta como FPS. Esta evidencia pertenece a fuente, pendiente de repetir contra el nuevo ejecutable.

Hashes de fixture, JSON, logs, stderr y registros de ejecución en practice09-native-matrix.json. Worker actualizó por separado verify-release07.ps1 para ejecutar seis casos en el EXE y pasar no-microphone tanto a práctica como hosting.
