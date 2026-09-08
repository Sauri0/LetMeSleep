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
