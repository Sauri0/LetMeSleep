# Partida después de optimizar navegación — fuente 0.9

Dos ejecuciones nativas seriales con la misma casa `house-v1-1`, textura de
render 1920×1080, Compatibility/OpenGL, sombras altas, VSync y límite de FPS
desactivados. Hardware: Ryzen 5 5600X y RTX 3060 Ti. Se midieron 12 segundos
después de dos de calentamiento, con bots autoritativos y aperturas/cierres
periódicos de todas las puertas disponibles. No equivale a una sesión WAN.

| Actores | Muestras | p50 | p90 | p99 | Cuadros >16,67 ms |
|---|---:|---:|---:|---:|---:|
| 2 | 1899 | 4,611 ms | 12,696 ms | 20,451 ms | 2,84 % |
| 16 | 499 | 21,198 ms | 45,506 ms | 65,591 ms | 61,72 % |

Ambos procesos terminaron con exit 0 y stderr vacío. La textura real fue
1920×1080 aunque la ventana presentada midió 999×562. Los informes registran
la misma identidad de mapa solicitada, simulada y dibujada, y 138/137 órdenes
de puertas respectivamente. La medición incluye Main/Client/Practice reales.

El caso de dos actores se mantiene bajo 16,67 ms durante la mayor parte de la
muestra; no demuestra 60 FPS sostenidos sin picos. El estrés de 16 actores aún
queda fuera de ese objetivo. La diferencia frente a mediciones anteriores
no se atribuye por completo a una sola optimización: los bots y la carga de
dibujo siguen trayectorias adaptativas según el tiempo transcurrido.

Evidencia: `perf09-final1-{2,16}-1080.json` y
`release07-perf09-final1-{2,16}-1080.run.json`. Corresponden a fit4 antes de los
ajustes nuevos del ribete y la presentación al levantarse; no al futuro EXE.

## Medición posterior con ribete y HUD actuales

Las dos ejecuciones siguientes incluyen el ribete trim2, la presentación de
crouch y la coalescencia del HUD. Ambas terminaron con exit 0 y stderr vacío.
Siguen siendo fuente 0.9, 16 actores, 1080p real y 12 segundos de medición.

| Renderizador y puertas | Muestras | p50 | p90 | p99 | Cuadros >16,67 ms |
|---|---:|---:|---:|---:|---:|
| Forward+, estrés de puertas | 380 | 30,489 ms | 49,834 ms | 70,354 ms | 83,68 % |
| Compatibility, interacciones ordinarias | 338 | 33,066 ms | 65,611 ms | 79,973 ms | 78,70 % |

El segundo caso no envió órdenes globales de puertas y no registró cuadros
con puertas moviéndose. El problema de fluidez con 16 actores también existe
sin el estrés artificial de puertas. Esta evidencia no permite atribuir una
regresión al HUD, al ribete ni comparar causalmente ambos renderizadores:
las trayectorias adaptativas y la carga dibujada difieren. No acredita 60 FPS.
Los percentiles de CPU, GPU y física se solapan y no deben sumarse.

Evidencia: `perf09-hud-forward-close2-16-1080.json` y
`perf09-hud-compat-natural-16-1080.json`, con sus respectivos registros
`release07-*.run.json`. Compatibility continúa como configuración del juego.

El fixture conserva el estrés anterior por defecto y agrega `--natural-doors`
para distinguirlo de una partida ordinaria. Ahora usa el cierre real de Main,
que drena la música durante el plazo existente de desconexión. Los primeros
intentos con cierre directo tuvieron recursos de audio pendientes; se
conservaron como intentos fallidos. Un intento posterior falló por una
constante sin calificar en SceneTree, corregida a `Node.NOTIFICATION_WM_CLOSE_REQUEST`.
Las ejecuciones Vulkan con `--verbose` también mostraron rutas obsoletas de
capas externas instaladas en el equipo; no se modificó el sistema. Las dos
mediciones aceptadas mantienen la comprobación estricta de stderr y salida.


## Guardia de geometría oculta, visibilidad y HUD por frame

El 8 de septiembre, la corrida limpia `perf09-guards-natural-16-1080` usa
Compatibility a 1920 × 1080, 16 actores y puertas ordinarias. La escena incluye
A2 candidato 1, aún rechazado estéticamente; no es evidencia de un EXE final.
Las diez fuentes registradas coinciden antes y después. Proceso exit 0 y stderr
vacío, dos segundos de calentamiento y doce de medición.

369 muestras: p50 **27,533 ms**, p90 **61,344 ms**, p99 **79,018 ms**;
**75,34 %** de cuadros superan 16,67 ms. Física p50/p90 14,084/25,755 ms;
render CPU 3,215/23,073 ms y GPU 4,973/22,296 ms. Los monitores se solapan.
No hubo órdenes globales ni cuadros con puertas moviéndose. Draw calls
p50/p90 789/7007, máximo 8956; máximo 2,396 millones de primitivas.

La mediana es menor que en la medición ordinaria previa, pero las trayectorias
adaptativas y la carga visible difieren. No se atribuye causalmente ese cambio
a una optimización individual ni se declara 60 FPS. El A/B aislado de
`actor09-legacy-optimization.md` sí conserva el replay y acredita ahorro del
método de pose, no de FPS globales. Falta resolver la fluidez con 16 actores.

Evidencia: `work/perf09-guards-natural-16-1080.json`,
`work/perf09-guards-natural-16-1080.source.json` y
`work/release07-perf09-guards-natural-16-1080.run.json`.
