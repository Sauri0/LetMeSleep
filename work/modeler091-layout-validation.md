# Validación inicial del layout v2

Godot 4.5.2, worktree `lms091-house`, base de código `b1d9fa4` más corrección
de reserva en descansos. Turno exclusivo concedido por Director y liberado
explícitamente al finalizar; no quedaron procesos Godot/Blender.

## Resultado

`modeler091_layout_test.gd`: **204 comprobaciones, 0 fallos**, exit 0,
sin timeout, stderr vacío. Once semillas: 1, 2, 7, 31, 97, 257, 997, 2026,
65537, 1234567 y 2147483646. Incluye dos/tres plantas y 16–22 habitaciones.
Todas tienen cero aristas rechazadas, firma determinista al regenerar y
ancho lateral mínimo declarado de aproximadamente 1.5825 m.

La prueba comprueba rechazo de v1/IDs no canónicos, dimensiones de circulación,
soporte por unión de losas, cuatro laterales por planta, descansos, ubicación
de emisores fuera de sólidos y presupuesto de 32 luces. Sus casos negativos
detectan una intrusión que deja transitable la línea central, una ruta lateral
omitida y una franja sin soporte entre losas.

Se conserva además el resto de HouseValidation: grafo humano/mosquito,
spawns, ocho tareas, herramientas y equipamiento mínimo existente.

## Hallazgo corregido

El volumen inicial de algunos descansos llegaba exactamente a la cara de
pared norte. Diferencias numéricas producían intersecciones de espesor Z
redondeado a cero en semillas 31, 997, 2026 y 1234567. La corrección reserva
6 cm para acabados en ambos extremos del descanso, como ya se hace en los
laterales. Profundidad libre mínima: 2.13 m; ancho: 2.8 m. Se mantiene la
validación estricta de volumen; no se ignoran obstáculos ni se rebaja el mínimo.
Los errores ahora incluyen tamaño de intersección para poder diagnosticarlos.

## Ejecuciones conservadas

| Ejecución | Resultado | stderr |
|---|---|---|
| import-01 | fallo nativo 0xC0000005 al iniciar Bangers-Regular.ttf | vacío |
| import-02 | exit 0 con `--single-threaded-scene` | vacío |
| layout-01 | exit 1, cuatro semillas con contacto de pared | vacío |
| layout-02 | exit 1, diagnóstico de semilla 31 | vacío |
| layout-03 | exit 0, 204/0 después de corregir descansos | vacío |

Todos los procesos tenían timeout de 55 s; ninguno alcanzó ese límite.
Importación: `--headless --editor --import --quit --frame-delay 800`, con
el flag de escena de un hilo añadido únicamente al segundo intento.
No se copiaron cachés, clases ni configuraciones EOS. Los `.import` que el
motor sólo convirtió de CRLF a LF fueron restaurados después de comprobar
que no había diferencias de contenido; no se incluyen en el commit.

Logs locales: `work/modeler091-import-01.*.log`, `import-02.*.log` y
`work/modeler091-layout-01.*.log` hasta `layout-03.*.log` (los nombres completos
de import incluyen el prefijo `modeler091-`). Resultados portables:
`modeler091-layout-results.json` y `modeler091-engine-evidence.json`.

## Pendiente

Corpus de 1000 semillas y regresiones de integración; capturas y recorridos
reales de ambos POV con acabados y luces de Worker 2; rendimiento comparable.
Zonificación/mobiliario son el tramo 0.9.2: no se mezclaron cambios v3 aquí.
No hay validación WAN ni nueva publicación/exportación en esta entrega.
