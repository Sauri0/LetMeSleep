# Atribución inicial 16 actores / 1080p

Las dos corridas usan el contexto real Main/Client y verificaron que el mapa renderizado coincide con la autoridad. Es una medición instrumentada de fuente con un consumer facial externo autorizado a correr en paralelo. **No sirve como benchmark limpio ni prueba de que una diferencia entre mapas sea causada sólo por su geometría.** No se optimizó el runtime.

El `performance07_live` anterior dejaba posible un desajuste: Client preparaba una casa generada, la fixture reiniciaba autoridad con `house`, y el cliente seguía en `playing`, evitando recargar World. Root corrigió esa fixture. `perf09-current16-1080.json` queda retirado como comparación geométrica exacta; estos perfiles no se presentan como una mejora respecto de él.

## Costes iniciales observados

| Componente | House: ms por tick medido | Seed 1: ms por tick medido |
|---|---:|---:|
| Practice.advance inclusivo | 6,80 | 15,55 |
| Simulation.step inclusivo | 3,50 | 9,55 |
| Todas las consultas privadas | 0,96 | 1,50 |
| Callbacks y residuo de publicación | 0,83 | 1,70 |
| Decisiones de bots, con ruta incluida | 0,60 | 1,15 |
| Snapshots públicos | 0,46 | 0,88 |
| Residuo del scheduler y registro | 0,36 | 0,63 |

Los componentes hijos están normalizados por 719 ticks en house y 566 en seed 1; una publicación ocurre cada dos ticks. `Practice._publish` tarda en promedio 2,29 / 4,74 ms por llamada, de los que 1,65 / 3,39 ms son residuo después de snapshots y privados medidos. Ese residuo conserva callbacks síncronos reales de Client/UI/World/audio.

`Simulation.step` representa aproximadamente 52% / 61% del tiempo inclusivo de `advance`. El siguiente pase solicitado por Root subdivide ese tick mediante overrides; no presupone que todo sea navegación o render.

El seguimiento de rutas llamó a `_path_direction` 2.043 / 1.210 veces, con 166 / 82 reconstrucciones reales. Su tiempo total fue 280 / 441 ms; las llamadas que reconstruyeron acumularon 237 / 382 ms. Sus máximos fueron 5,72 / 7,39 ms. Es el método completo de Brain, no una medición aislada del método estático `MapNavigation.path`.

House registró 525 frames y seed 1, 81. En seed 1 hubo ocho ticks de física en 57 de los 81 frames observados: el retraso acumulado de física forma parte del problema de esa corrida. Los tiempos de render también están en los JSON, pero no se separó el efecto del consumer concurrente ni del propio perfilador.

## Preparación y cierre

El coste previo a la ventana medida se registra explícitamente. House: Main/Client 812 ms, preámbulo de práctica seed 1 603 ms, preparación adicional de navegación house 34 ms, inicio de simulación 7,7 ms y publicación/carga final de World 340 ms. La variante seed 1 reutiliza su navegación y mundo del preámbulo; no se afirma que esos costes sean cero en un arranque frío.

Ambos procesos finalizaron dentro del límite de 50 s y devolvieron exit 0. House terminó con stderr vacío. Seed 1 emitió un aviso ObjectDB y doce recursos retenidos durante cierre; se preserva su `.err`. El fixture se ajustó para diferir `quit` y dejar terminar la coroutine que conserva referencias locales. El segundo pase detallado terminó en 18,46 s, exit 0 y stderr vacío, confirmando la limpieza de la fixture. No se atribuyó el aviso original al runtime.

Archivos:

- `game/tests/performance09_profile.gd` y `work/perf09-profile-contract.md`.
- `work/perf09-profile-house.{json,log,err,run.json}`.
- `work/perf09-profile-house-v1-1.{json,log,err,run.json}`.
- `work/perf09-profile-parse.log`: check-only inicial, exit 0.

Los JSON contienen hashes de fuente, jerarquía de llamadas, filas por frame/tick, testigos costosos, estadísticas de bots, mapa/fingerprint y configuración gráfica. Los tiempos inclusivos se solapan; no deben sumarse entre niveles.
