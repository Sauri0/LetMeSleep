# Entrega de muestra — 12 septiembre 2026

Estado: fuente y exportaciones comprobadas en Blender, listas para importación por Director. No se editó Unity, no se abrió editor ni se renderizó. El lote se realizó en N: con turno Blender headless autorizado por Director, procesos secuenciales de dos hilos acotados a 55 segundos; ambos terminaron en menos de 3 segundos en la ejecución final.

## Archivos

En `art_source/unity/environments/room_sample/`:

- `room_sample.blend`: fuente editable, objetos nombrados, materiales, puerta en reposo cerrado, sockets y shell soldado.
- `room_sample.fbx`: muestra completa, para revisar el conjunto.
- `room_furnished_without_door.fbx` + `door_01.fbx`: pareja destinada a integración con prefab de puerta separado. No instanciar además el FBX completo.
- `room_contract.json`: dimensiones reales, jerarquía/pivote de puerta, cajas de colisión locales, superficies y reservas. Las cajas se crean bajo el nodo indicado; un collider no debe heredar la traslación del mesh dos veces.
- `build_room.py` y `validate_room.py`: reproducción sin dependencias externas ni render.
- `validation.json`: resultados, tamaños y SHA-256 de cada binario.

`SPATIAL-CONTRACT.md` define casa/patio de dos pisos y lobby separado; `ROOM-PLAN.svg` permite revisar la habitación y sus reservas a escala. La casa/lobby completos todavía no están modelados en este lote. Los archivos de fuente están fuera de `unity/Assets`; al importar, Director genera/versiona los .meta correspondientes y materiales URP.

## Resultado comprobado

Blender 5.2.1 LTS, 55 meshes / 2178 triángulos para toda la muestra. 74 comprobaciones aprobadas, 0 fallidas. Shell manifold, volumen positivo y caras con área; escalas unitarias; pivote conservado; ausencia de penetración contra cajas estáticas al probar hoja/paneles/tiradores en 101 ángulos de 0 a −100°. Los pequeños cilindros representados por cajas de bisagra se excluyen de esa prueba porque encuentran intencionalmente el marco. No se excluyen hoja ni tiradores. Prueba geométrica de reservas de pie y zona principal de 1.80 × 1.80 m libre de muebles.

Los tres FBX se importaron de nuevo en Blender: conjuntos de objetos, jerarquías, orígenes y límites conservados. Error máximo observado 0.000000954 m, por debajo del umbral 0.0001 m. Esto comprueba ida y vuelta Blender/FBX; no certifica la interpretación del importador Unity.

Una primera ejecución del generador detectó un nombre de API Blender incorrecto al seleccionar descendientes para el FBX separado; se corrigió a `children_recursive`, se regeneraron las tres exportaciones y se repitió la validación completa. Ejecuciones finales usan `--python-exit-code 2` para que una excepción Python no parezca éxito del proceso Blender. Advertencia no bloqueante: `Material.use_nodes` está deprecado para Blender 6.0; versión utilizada 5.2.1.

## Integración que queda

Importar FBX en Unity 6000.3.24f1 comprobando metros, +Y arriba, frente y yaw negativo de apertura. Mantener puerta fuera de Static y collider móvil coherente con el visual; shell y mobiliario estáticos según contrato de física. Crear materiales URP por nombres de paleta; color/textura y normales requieren revisión visual. No hornear una caja alrededor del shell porque taparía el vano. Los materiales fuente no llevan imágenes externas.

Director/Gameplay deben probar cápsula humana 1.72/r0.25, ojos1.53 y mosquito r0.055; puerta abierta/cerrada y obstruida; posado en pared/techo; cámara, alcance de pickup y transición del umbral. El barrido numérico contra muebles no acredita interacción física con actores. No afirmar rendimiento, calidad artística en motor, WAN, mapa completo ni aprobación de Branko a partir de estas pruebas.

Reproducir en serie, con slot coordinado y límite externo de 55 segundos por proceso:

```powershell
N:/Blender/blender.exe --background --factory-startup --threads 2 --python-exit-code 2 --python N:/LetMeSleep/Worktrees/environment/art_source/unity/environments/room_sample/build_room.py
N:/Blender/blender.exe --background --factory-startup --threads 2 --python-exit-code 2 --python N:/LetMeSleep/Worktrees/environment/art_source/unity/environments/room_sample/validate_room.py
```
