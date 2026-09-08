# DoorState: equivalencia y coste CPU

Se modificó únicamente `game/scripts/door_state.gd` en producción. Los candidatos de cierre se descartan por una envolvente conservadora de todo el giro alrededor de la bisagra. Las cajas de actores se calculan una vez por llamada a `step`; las cajas de extremidades se crean sólo cuando una hoja próxima las necesita y se reutilizan dentro de esa misma llamada. No se conserva estado de ocupantes entre ticks ni entre salas.

La hoja de ancho `w` y espesor `t` cabe para cualquier ángulo en un radio horizontal `sqrt(w*w + (t/2)*(t/2))` desde la bisagra. El descarte usa la caja de ese radio y la altura real de la hoja, con 0,01 mm adicionales sólo en la fase de descarte. Los candidatos restantes pasan por el SAT orientado original, sin alterar su tolerancia. Para humanos el descarte abarca tanto la caja de viaje como la envolvente que ya usaba el algoritmo anterior para decidir si revisar extremidades. No se usa la envolvente como colisión final.

Se mantienen orden de actores y puertas, número y posición de subpasos, velocidad, cooldown, ángulos, apertura detenida, reapertura al cerrar sobre un ocupante y separación de estados por sala. No se editaron catálogo, generador, rutas, geometría, pieles ni reglas de juego.

## Comprobación

`game/tests/door09_reference.gd` conserva la implementación anterior, quitando únicamente `class_name` para que sea cargable junto con la actual. El SHA256 del archivo de producción antes de modificarlo es:

`26A1C4A8DF4535C7BEFCF35647522986159FE324BC5DE8ED6F619CA18E73FE7C`.

`game/tests/door09_equivalence_test.gd` pasó **2.578/2.578**, exit 0, stderr vacío, en 7,47 s. Incluye:

- 1.176 consultas de ocupación a cuatro orientaciones de bisagra y siete ángulos, con muestras a ambos lados del límite físico, alturas diferentes y ambos roles: 420 bloqueadas y 756 despejadas. Añade 120 muestras de extremidad extendida; cinco sólo bloquean por extremidades fuera de la caja de viaje.
- Estado de puerta y cooldown idénticos en cada uno de 180 pasos para `house`, `house-v1-1` y `house-v1-2` (10, 23 y 17 hojas). Incluye cambios de pose/posición/vida, apertura/cierre simultáneos, pulsaciones duplicadas, dt de 60 Hz/20 Hz/100 ms y valores de dt inválidos. Ningún paso modifica actores. Registra 155/124/124 muestras bloqueadas.
- Reapertura ante un cuerpo, retirada del ocupante en el tick siguiente, mosquito aturdido bajo la luz de 14 cm, independencia entre salas y reset de ronda.
- Dos Simulation completas, mapa generado 1 y 16 actores, durante 120 ticks de 60 Hz: snapshots públicos idénticos en cada tick y privados idénticos para los 16 actores cada 30 ticks. Una usa la referencia y otra la implementación nueva.
- Repetición CPU A/B/B/A con estado final idéntico en los cuatro pases.

La suite existente `doors07_test.gd`, sin cambiarla, pasó **342/342**, exit 0 y stderr vacío, en 1,92 s. Conserva pruebas reales de autoridad, cierre seguro, hueco, rayos, privacidad, bots y tareas a ambos lados de una hoja.

## Repetición CPU acotada

Actor-pose replay determinista de 360 ticks por pase sobre las 23 puertas de `house-v1-1`, 16 ocupantes y calentamiento simétrico. Las órdenes y construcción de casos quedan fuera del span. Se mide sólo `DoorState.step`; no es el benchmark FPS de Main/Client.

| Orden | Implementación | Media ms | p90 ms | p99 ms | Máximo ms |
|---|---|---:|---:|---:|---:|
| A1 | Anterior | 2,430 | 3,280 | 3,713 | 3,973 |
| B1 | Optimizada | 0,404 | 0,672 | 0,877 | 1,000 |
| B2 | Optimizada | 0,408 | 0,681 | 0,873 | 1,114 |
| A2 | Anterior | 2,478 | 3,419 | 3,979 | 4,667 |

La media conjunta pasa de 2,454 a 0,406 ms: **83,5% menos tiempo** de este método en este replay. Había exclusividad de Godot y Visual realizaba lectura ligera de JSON por CPU. El orden simétrico y los casos idénticos permiten atribuir esta diferencia al método; no eliminan ruido de sistema ni prueban 60 FPS del juego completo. El caso difiere del perfil nativo inicial, por lo que no se comparan directamente sus medias.

Hash del replay de 360 poses: `0d2fb26b3fec15a1f3172e791f6633bbcbbb888d1df0841cb3f2057db3bbcf16`.

## Evidencia conservada

- `work/perf09-door-equivalence-fixed.{json,log,err,run.json}`: gate final, hashes de módulos, estados y tiempos ABBA.
- `work/perf09-doors07-regression.{log,err,run.json}`: suite existente.
- `work/perf09-door-parse.log`: parse inicial, exit 0.
- `work/perf09-door-equivalence.{log,err,run.json}`: primera corrida fallida por dos errores de la fixture, no se borra ni se presenta como PASS. La altura Vector3 de 4 cm requería comparación aproximada por precisión float32/float64. El reporte borraba `final_state` de la primera fila antes de compararlo con las siguientes; se guarda ahora antes de borrarlo. Se detuvo exclusivamente ese proceso tras la excepción de reporte. No había divergencia de estados de puerta en esa corrida.

Fuente final DoorState SHA256: `c9771faabf4fa3d068937afbd2a4b73ea0a20d2b7d498363be52cb29257f0606`. Motor liberado; sin procesos propios pendientes. No hubo nueva exportación ni cambios de preferencias.
