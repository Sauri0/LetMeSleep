# Voz: consumidor visual local

`ActorView.set_voice_level(level: float)` recibe un valor finito entre 0 y 1 del audio de ese actor que **realmente se reproduce para el oyente local**. No toma datos del micrófono y no añade campos a los snapshots.

El proveedor de reproducción debe alimentar el nivel regularmente, incluido cero en silencio. La respuesta predeterminada abre en 45 ms y cierra en 140 ms; `configure_voice_response(attack, release, stale_after)` permite configurarla en segundos. Si dejan de llegar valores durante 160 ms, el watchdog inicia el cierre. La envolvente funciona independientemente de que lleguen nuevos snapshots del actor.

`clear_voice_level()` elimina de inmediato nivel y objetivo, restablece la expresión base y detiene el procesamiento de la envolvente. Root debe llamarlo al silenciar al participante, detener/limpiar la reproducción y vaciar actores. `ActorView` también limpia al recibir `alive=false` y al salir del árbol.

`get_voice_level()` devuelve el nivel local suavizado; es una API del render, no un dato público de Simulation.

CharacterSkin combina únicamente `MouthOpen = clamp(max(apertura_base, nivel_local * 0.72), 0, 1)`. Mantiene el diccionario facial base intacto y no cambia Blink, ojos, cejas ni sus correctivos. Un bostezo con apertura mayor se conserva. Al nivel cero, la ruta de facialpreview y las capturas sin voz siguen usando exactamente sus valores originales.

Los materiales, geometría, rig, colisiones, reglas, transporte y Opus no se modificaron por este consumidor.

## Gate específico

`game/tests/voice09_visual_checks.gd` usa niveles sintéticos, sin abrir el micrófono ni reproducir sonido. Comprueba envolvente, límites, silencio, watchdog, limpieza, seis opciones de boca y desplazamiento de sus vértices importados mediante bake del blendshape real.

```text
Godot --path game --script res://tests/voice09_visual_checks.gd -- --output=<JSON absoluto>
```

Este gate valida el consumidor visual; **no demuestra todavía sincronización de voz real, latencia de Opus, atenuación espacial ni integración online**. Esa validación requiere el proveedor y la reproducción integrados por Root.

Resultado ejecutado: **552/552 PASS**, Godot 4.5.2 Compatibility nativo, salida 0 y stderr vacío. Reporte `work/voice09-visual.json`; logs `work/voice09-visual.log` y `.err`. Incluye bake real de las seis bocas, equivalencia silenciosa, conservación de otros canales, máximo con gesto, limpieza por mute/eliminación y rechazo de callback tardío después de sacar el actor del árbol.
