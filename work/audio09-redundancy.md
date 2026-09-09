# Foley: evitar trabajo sin evento y consultas duplicadas

AudioFX prepara transformaciones, posición del emisor y listener de una puerta sólo después de reconocer movimiento, bloqueo o cierre. Mantiene su prioridad y el historial de snapshots. La admisión de un efecto ya descarta fuentes obstruidas; se elimina la segunda consulta idéntica dentro de la misma llamada síncrona.

Prueba del 9 de septiembre de 2026: audio09_redundancy_checks ejecuta producción con paredes físicas y observadores de conteo. Baseline19/19 y after18/18 (una comprobación por consulta observada, por eso cambia el total), exit0 y stderr vacío. Consulta de emisión admitida:2→1; consultas al listener en100 snapshots de10 puertas estacionarias:1000→0. Los cuatro registros de sonidos de puertas coinciden exactamente antes/después: tipo, posición, ganancia y pitch. Conserva rechazo por pared/distancia, exclusión de hoja propia sin ignorar otra pared, silencio suspendido y fallback sin listener.

Regresión listener espacial9/9 y objetos36/36, exit0 y stderr vacío. Motor Godot4.5.2, headless, audio Dummy; no micrófono ni afirmación de calidad audible o FPS. Esta reducción de operaciones no acredita por sí sola 60FPS.

SHA256 anterior: c8d5289719d417144607ddf79b203deca3ec62c9f3f79be3951f497721adae41.

SHA256 nuevo: 0d347b80b63e75c0218ffac186acf1b3b7a8c934966bb8ac1e46a23c472ebd0b.

Evidencia en work/audio09-redundancy-before.json, after.json, after.run.json, listener.run.json y tools.run.json, más stdout/stderr locales. Fixture: game/tests/audio09_redundancy_checks.gd; --baseline observa el comportamiento anterior, ejecución predeterminada exige la optimización.
