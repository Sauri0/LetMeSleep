# DoorCatalog.body_blocked: atribución, equivalencia y CPU

Se aisló el coste antes de modificar el método. Una copia en memoria del Arena real sustituyó únicamente su dependencia `Doors` por un observador de prueba. Cada movimiento se comparó con el Arena de producción. Se capturaron 15.318 consultas reales de colisión de 12 mosquitos durante 360 ticks, con geometría generada seed1, movimiento de vuelo a 60 Hz y ángulos de puertas que cambian durante el recorrido.

Antes de optimizar, `body_blocked` acumuló 652,711 ms dentro de 778,110 ms de movimiento instrumentado: 83,9% en este escenario. El tiempo del método excluye almacenar la traza, mientras el tiempo de movimiento incluye ese registro; no es una extrapolación del perfil nativo anterior. La repetición aislada de sus consultas tardó 1,75–1,88 ms por tick antes del cambio.

## Cambio de producción

Sólo `game/scripts/door_catalog.gd`: el bucle mantiene el orden de estados y el SAT orientado existente, pero descarta antes cajas que quedan fuera del barrido completo de una hoja. Esa caja conservadora depende únicamente de ancho, espesor, altura y bisagra. El radio horizontal es `sqrt(width² + (thickness/2)²)`; se añaden 0,01 mm a la caja de descarte, sin ampliar la colisión final.

Las cajas se calculan una vez por mapa inmutable. Las de mapas generados se expulsan junto con `_generated` mediante el mismo límite de 24; las diez de la casa autoral tienen almacenamiento separado. No se cachean ángulos, bloqueos, ocupantes ni resultados por sala. Los estados dinámicos siguen entrando en cada llamada. Las cajas no finitas o con tamaño negativo recorren el SAT original sin aplicar descarte.

No cambian geometría, posiciones, velocidad, alcance, frecuencia de física, reglas de cierre, mapas, generador o rutas. `DoorGeometry` permanece idéntico.

## Gate final

**17.117/17.117 PASS**, exit0 y stderr vacío, proceso27256 en8,46s. Incluye:

- 4.320 movimientos de Arena comparados con producción, con igualdad del diccionario del actor tras cada paso.
- Repetición de las 15.318 consultas sin divergencias en todos los pases.
- 12.600 consultas de borde en tres mapas y sus 50 puertas (10/23/17), siete ángulos, humanos agachados e insectos, distintas alturas y lados de la hoja. Contiene 4.650 resultados bloqueados y 7.950 libres. IDs desconocidos permanecen ignorados.
- Dos Simulation completas de 16 actores durante120ticks de60Hz: una usa la referencia antigua de consultas y otra producción. Snapshot público idéntico cada tick y privados idénticos de los16actores cada30ticks. Todas las demás funciones de Simulation/Arena conservan su fuente original.

La suite existente `doors07_test.gd`, sin modificarla, pasó **342/342**, exit0 y stderr vacío en1,85s. No se interpreta este gate como prueba de rendimiento del EXE.

## Replay A/B/B/A después del cambio

Mismas15.318cajas, estados y respuestas esperadas; calentamiento de ambas implementaciones. Se mide sólo la suma de `body_blocked` por tick capturado, excluyendo preparación y render.

| Orden | Implementación | Media ms/tick | p90 | p99 | Máximo |
|---|---|---:|---:|---:|---:|
| A1 | Anterior | 1,799 | 3,007 | 3,334 | 4,442 |
| B1 | Optimizada | 0,327 | 0,536 | 0,772 | 0,840 |
| B2 | Optimizada | 0,333 | 0,543 | 0,667 | 1,947 |
| A2 | Anterior | 1,869 | 3,083 | 3,783 | 4,342 |

La media conjunta baja de1,834a0,330ms por tick: **82,0% menos tiempo del método en este replay**. Hubo exclusividad de motor/CPU; Root sólo hacía revisión documental ligera. La mejora no equivale a un porcentaje de FPS ni se suma directamente a porcentajes de otros componentes.

El hash de la traza antes y después es idéntico: `0a179163b17dfd811da7ec0f4f246d58d3903e0adf8dda58d648a026123f8f2c`. El fingerprint de la casa es `1b34a7206283c854a1fae77ef36c6b8e8d06af30e31044edd35d7d1c842621d9`.

## Archivos y portabilidad

Producción para integrar: `game/scripts/door_catalog.gd`, SHA256 `fc6da39cb7d11a7f6c43f271e68212bae0f90238a685d3c719fd4a3ebcf3aafc`.

Nuevos archivos de prueba:

- `game/tests/door09_catalog_reference.gd`: copia anterior del catálogo, sólo sin `class_name`. Fuente original SHA256 `995aee9689debc47357bf984f010b178fec2256ca50a30e5a734ce542c5b4ff7`.
- `game/tests/door09_query_trace.gd`: observador de consultas y delegación a referencia/actual.
- `game/tests/door09_body_query_test.gd`: gate y replay **sólo para checkout de fuente**. Puede incluirse en el build headless de fuente; no incluirlo como gate del EXE.

La fixture comprueba primero la existencia de fuentes Arena/Simulation. Para comparar la simulación completa escribe `work/perf09-traced-arena.gd`, derivado exactamente de Arena con sustitución de dependencia y sin `class_name`, y lo preloads sólo desde una Simulation creada dinámicamente. No hay un preload externo global ni una ruta absoluta escrita en scripts exportados. En un pack sin fuente, la fixture se detiene explícitamente con exit2; no declara PASS ni exige ese archivo fuera del export normal.

Evidencia:

- `work/perf09-body-baseline.{json,log,err,run.json}` y `perf09-body-parse.log`: anterior a optimización, 4.329checks PASS.
- `work/perf09-body-optimized-fixed.{json,log,err,run.json}`: gate final y ABBA.
- `work/perf09-body-doors07.{log,err,run.json}`: regresión existente.
- `work/perf09-body-optimized.{json,log,err,run.json}`: intento fallido conservado. La técnica de clase Arena anidada en GDScript compilado en memoria produjo llamadas estáticas nulas; sus184comparaciones posteriores no son evidencia de equivalencia. El método de precarga de archivo de prueba resolvió ese problema sin cambiar el runtime. No se ocultó el stderr ni se reutilizó esa corrida como gate final.

Motor/CPU liberados explícitamente a Visual. Sin nuevas exportaciones, cambios de preferencias o commits propios.
