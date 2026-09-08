# Perfil integrado después de las dos mejoras de puertas

Corrida de fuente con Main/Client/Practice reales, casa `house-v1-1`, 16 actores, 15 bots, dos segundos de calentamiento y doce de medición. Renderer verificado por el motor: **gl_compatibility / opengl3**; 1920×1080 internos, física de60Hz, VSync desactivado, sin límite de FPS y audio Dummy. No se redujo población, calidad, frecuencia ni validación física.

Proceso30276:18,78s, exit0, failures0, sin timeout y stderr vacío. Hubo exclusividad de motor/CPU, con lectura ligera de Visual. World y autoridad conservaron el mismo mapa/fingerprint. Los hashes del perfil incluyen ahora DoorState y DoorCatalog además de los demás módulos.

## Coste actual medido

| Componente | Media normalizada por los718ticks, ms |
|---|---:|
| Practice.advance inclusivo | 7,789 |
| Simulation.step inclusivo | 3,234 |
| Decisiones de bots, con rutas incluidas | 1,193 |
| Todas las consultas privadas | 1,164 |
| Residuo de publicación y callbacks síncronos | 1,080 |
| Todos los snapshots públicos | 0,621 |
| Residuo de advance | 0,401 |
| Entradas dentro de advance | 0,098 |

La tabla usa una partición sin sumar dos veces las llamadas anidadas. El residuo conserva trabajo no instrumentado y parte del coste del perfilador. `Practice._publish` tuvo359llamadas y promedió3,078ms por llamada, de las cuales2,159ms son residuo después del snapshot/privado medidos.

Dentro de Simulation.step:

| Hijo directo o residuo | ms/tick |
|---|---:|
| Concentración de los12mosquitos | 1,007 |
| Movimiento libre de los12mosquitos | 0,892 |
| Residuo _tick, incluido Arena humano estático y bucles | 0,800 |
| DoorState.step | 0,280 |
| Ayuda y sus consultas | 0,147 |
| Otros hijos y envoltura | 0,108 |

Simulation.step tuvo p90=4,36ms, p99=5,30ms y máximo7,61ms. DoorState.step tuvo p99=1,14ms y máximo1,52ms. Los replays ABBA previos, conservados en sus informes, prueban el ahorro de cada método; esta corrida integrada no es una comparación simétrica de FPS con el perfil antiguo.

## Cola restante identificada

Los picos de Practice.advance contienen decisiones de ruta agrupadas en un mismo tick. El mayor, tick470, tarda38,326ms: Brain.decide acumula29,926ms, de los cuales22,209ms están en `_path_direction`; Simulation.step tarda4,881ms y no hubo publicación ese tick. Otros testigos:

| Tick | advance ms | Brain ms | path incluido ms | Sim.step ms | Publicación ms |
|---|---:|---:|---:|---:|---:|
| 475 | 25,995 | 5,127 | 4,456 | 7,609 | 7,268 |
| 579 | 24,562 | 16,104 | 11,869 | 3,684 | 3,020 |
| 364 | 21,382 | 16,777 | 12,561 | 2,581 | 0 |
| 454 | 21,206 | 13,771 | 13,158 | 4,647 | 0 |
| 817 | 20,463 | 8,650 | 8,259 | 4,242 | 4,829 |

Se observaron172reconstrucciones en2.142llamadas de `_path_direction`. Las llamadas que reconstruyeron acumulan580,814ms de los659,546ms totales de ese método. Es el método completo de Brain, incluida comprobación/seguimiento; no se midió aisladamente el método estático de navegación. El próximo diagnóstico puede concentrarse allí sin asumir de antemano que reducir la frecuencia de decisión sea necesario. No se implementó otra optimización.

## Límites

No hubo ataques, picaduras, rescates o proyectiles activos, y el modo Sangre no ejecutó tareas. La fixture ejercita vuelo real, movimiento humano y órdenes periódicas de puertas; conserva136órdenes. Los costes bajos de las ramas ociosas no representan un combate activo.

Se observaron422frames, con entre0y7ticks de física por frame. Frame p50/p90/p99=22,259/53,688/96,514ms; monitor de física=11,990/24,802/38,511ms. Son datos de una corrida instrumentada, no certificación de60FPS. Los monitores del motor no son idénticos a los spans del perfilador; quedan incluidos para evitar atribuir su diferencia enteramente a Simulation.step.

Archivos: `work/perf09-profile-postdoors-house-v1-1.{json,log,err,run.json}`. El JSON conserva filas por tick/frame, jerarquía, máximos, estadísticas de bots, hashes de código, posición inicial/final y renderer real. Motor/CPU liberados a Root y Visual inmediatamente al finalizar.
