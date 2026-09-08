# Atribución de presentación, 2026-09-08

Corrida `perf09-view-natural-house-v1-1`: Compatibility/OpenGL3, RTX 3060 Ti, 1920×1080, semilla 1, 16 actores/15 bots, dos segundos de calentamiento y doce de medición. No hubo órdenes globales ni frames con puertas moviendo. Mapa/fingerprint coinciden entre World y Simulation. Proceso 22,67 s, exit 0, stderr vacío, cero fallos de instrumentación/huellas. No se modificó producción.

Hubo 655 ticks de autoridad y 108 callbacks/frame de presentación. Tiempos inclusivos, solapados:

| Ámbito | Media por ejecución | Suma por frame de presentación |
|---|---:|---:|
| Practice.advance | 11,926 ms/tick | 72,328 ms |
| Simulation.step, dentro de advance | 5,072 ms/tick | 30,763 ms |
| Client._process | 15,556 ms/frame | 15,556 ms |
| World.sync_actors, dentro de Client | 14,880 ms/frame | 14,880 ms |
| Cuatro humanos update_state | 1,841 ms/actor | 7,363 ms |
| Humano pose, dentro de update_state | 1,561 ms/actor | 6,246 ms |
| Humano colliders, dentro de pose | 0,362 ms/actor | 1,446 ms |
| Doce mosquitos update_state | 0,332 ms/actor | 3,984 ms |
| World._process | 0,278 ms/frame | 0,278 ms |
| Client._snapshot | 2,650 ms/publicación | 8,025 ms |
| Client._private | 0,062 ms/publicación | 0,189 ms |
| HUD diferido | 0,567 ms/flush | 1,716 ms |
| Client._physics_process | 0,046 ms/tick | 0,280 ms |
| Doors.step, sin movimiento | 0,0315 ms/tick | 0,191 ms |

No sumar la tabla: snapshot forma parte de publicación/advance; pose y colliders forman parte del humano y de Client. `perf09-view-attribution.json` contiene normalización por tick/frame y datos originales completos por ámbito en el JSON de corrida.

El registro costó al menos 1.115,116 ms en los doce segundos: 77.914 pares enter/leave, aproximadamente 9,2% del intervalo. La calibración sintética fue 9,61 microsegundos/par; el registro real midió 14,31 microsegundos/par con diccionarios/jerarquía/frames reales. No incluye todo el dispatch de wrappers y no se resta de las cifras. Los residuos contienen bookkeeping de hijos.

La corrida instrumentada cayó en recuperación de física: 66 de 108 frames ejecutaron ocho ticks, con frame p50/p90/p99 139,30/164,61/174,42 ms. No es una comparación válida de FPS contra el benchmark limpio 33/65/80 ms. El coste agregado del perfil y la acumulación de ticks cambian la cadencia efectiva y el muestreo de poses visibles. La resolución, calidad, población, frecuencia física configurada y algoritmos se mantuvieron.

Hallazgos concretos para la siguiente intervención:

1. **Scaffold humano oculto:** ActorView actualiza `CapsuleMesh.height` de torso y ocho segmentos en cada nueva pose aunque `_hide_legacy_geometry` ocultó esas mallas. Conserva transformaciones para sockets/colliders, pero escribir geometría oculta es evitable. El residuo humano pose es 4,80 ms/frame, que también contiene skin: todavía no se puede atribuir todo ese residuo al scaffold. Root autorizó un A/B focal y guardia de escritura, sin cambiar transformaciones ni colliders.
2. **Visibilidad repetida:** cada snapshot llama `World.set_local_role` sobre los 16 actores; `ActorView.set_local` vuelve a llamar `CharacterSkin.set_first_person`, que recorre todas sus mallas con `_update_visibility`, incluso sin cambiar el rol/localidad. Es candidato dentro del residuo snapshot de 2,373 ms/publicación; no está aislado todavía.
3. **HUD diferido entre ticks:** hubo 327 flushes para 108 frames. `call_deferred` agrupa público/privado del mismo tick, pero no todos los ticks de catch-up antes de dibujar. Se podría actualizar sólo al dibujar conservando estado más reciente; su coste medido es secundario frente a actores y autoridad.
4. **Resto de sync_actors:** 3,533 ms/frame residuales, incluido `AudioFX.sync`, bookkeeping de hijos y bucles. Requiere un ámbito corto adicional antes de adjudicarlo al audio; World._process/etiquetas sólo consume 0,278 ms/frame.

Fuente de evidencia: `perf09-view-natural-house-v1-1.json/.log/.err/.run.json`; compilación final `perf09-view-parse2.*`. El intento inicial `perf09-view-parse.*` falló por una ruta externa no normalizada del helper y se preserva. Se corrigió con `simplify_path`, únicamente en la fixture. Main ahora usa su cierre real y 0,25 s de drenaje; el exit code del diagnóstico se conserva mediante sustitución exacta en su copia de prueba.
