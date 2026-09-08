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
