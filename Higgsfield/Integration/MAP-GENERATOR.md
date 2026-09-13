# Generador reutilizable de recetas

`prepare_map_recipe.py` sustituye la preparación específica de Isla. `Prepare-IslaReview.py` queda como entrada compatible que requiere los mismos argumentos; ya no contiene rutas, cupos ni escrituras automáticas de Isla. Los JSON históricos entregados no se modificaron.

En este cambio se preparó **sólo la herramienta**. No se leyó ninguna fuente 3D activa ni se generaron recetas de los mapas en construcción.

## Uso al confirmar export final

Copiar `map-input.EXAMPLE.json` a un descriptor propio. Completar `fbx`, `glb`, `audit`, `report` (rutas absolutas o relativas al descriptor) y `mapId`. Sólo después de la confirmación final del Encargado, establecer `sourceFinal:true`. La plantilla se mantiene en false y el generador la rechaza antes de leer exports. Puede añadirse `sourceSha256` si el Encargado ya fijó el hash esperado; en todos los casos se calcula y registra el SHA real.

Invocación futura:

```powershell
python N:/LetMeSleep/Worktrees/maps/Higgsfield/Integration/prepare_map_recipe.py --config N:/ruta/map-final-input.json --output-dir N:/ruta/entrega
```

Escribe `<mapId>.recipe.json` y `<mapId>.validation.json` únicamente después de pasar las comprobaciones. Si ya existe cualquiera de esos archivos, rechaza sobrescribirlos. No abre Unity/Blender, no modifica FBX/GLB/audit/report y verifica otra vez sus hashes antes de escribir. La confirmación de finalización procede del descriptor autorizado; los conteos de un report no permiten inferirla.

IDs acordados:

- `hf-isla-del-laguito-v1`
- `hf-casa-del-patio-v1`
- `hf-campamento-pinar-v1`
- `hf-yate-a-la-deriva-v1`
- `hf-puerto-del-faro-v1`

Valores por defecto: **5 humanos, 16 mosquitos, capa Default**, `firstSurfaceId=1000000`. Se aceptan cambios explícitos con `humanCount`, `mosquitoCount`, `collisionLayer`, `firstSurfaceId`. Los cupos son mínimos de marcadores, no aprobación de partida; se conservan todos los EMPTYs que coincidan. El rango de SurfaceId necesita coordinación central si conviven mapas; no asigna rangos de red automáticamente.

## Contrato de audit y export

- Cada malla debe tener `properties.collision_role` igual a `static_solid` o `non_solid`. Si falta, falla. El rol del GLB, cuando existe, debe coincidir. Nunca convierte una copa en sólida por pertenecer a una colección de árboles. Troncos y copas separados se clasifican por sus propiedades propias.
- `static_solid` se vuelve solid; `non_solid` se vuelve decoration por defecto y sigue sin collider, con sombras. Para especializar visuales, admite `properties.environment_kind` o `kinds` en el descriptor, mapa de **ruta exacta** a `water`, `foam`, `foliage`, `decoration` o `solid`. Una categoría no puede contradecir la colisión declarada. La ruta inexistente falla.
- Para agua non_solid, `wave_loop_seconds` o un nombre `Water_*` identifica el componente visual; un nombre que contenga `foam` identifica espuma. Son convenciones visuales, no heurísticas de colisión. `wave_amplitude`, `wave_length` y `wave_loop_seconds` se leen de properties, con defaults 0.025 m, 4 m y 8 s. Para nombres distintos usar `environment_kind` o `kinds`.
- Cada malla y spawn debe incluir `matrix_world` finita 4×4. Los spawns usan su traslación mundial, **no** `location` local. Se seleccionan EMPTYs por prefijos `Spawn_Human_` y `Spawn_Mosquito_`; `humanPrefix`/`mosquitoPrefix` permiten ajustar el contrato. Se rechazan cupos insuficientes, geometría con prefijo de spawn, origen repetido a 10 cm o menos, y puntos fuera de bounds.
- Los nombres y paths se cruzan entre audit/GLB/FBX. Materiales, ranuras y colores finales se validan como antes. Los conteos provienen de los archivos y se contrastan con los resúmenes disponibles: no hay cifras de Isla impuestas.
- `expectedScene` permite exigir la escena indicada tanto en audit como en reporte; los conteos del reporte pueden estar en raíz o en `checks`. `expectedWaterCount` exige una cantidad exacta de reglas water/foam (Casa usa 0). No oculta ni recorta objetos que no cumplan.
- Los sufijos de nombre como `.001` se conservan en las rutas de spawns; el prefijo sigue coincidiendo. Los materiales se cotejan por instancia, admitiendo slots originales o nombres repetidos consolidados según la corrección central 8ee0229, sin modificar bytes del FBX.
- Bounds se derivan de `bounds_min/max` **mundiales** de sólidos, con margen y conversión Blender XYZ → Unity XZY usada en este pipeline; el agua non_solid queda excluida. Opcionalmente proporcionar `playBoundsMin`/`playBoundsMax` Unity XYZ para otro volumen jugable. Un formato de export distinto necesita revisión de ejes nativa, no un supuesto nuevo. La escala del importador sigue en 1 con unidades del archivo.

El generador no produce navegación, luces, cámaras, RoomRules ni registro UI/Online. SpatialData del importador continúa siendo receta de importación: el esquema de navegación y la habilitación para BeginRound son responsabilidad central. La comprobación de marcadores no prueba cápsulas, rutas, cupo de sala ni aprobación artística.

## Comprobación de este cambio

Sólo seis pruebas pequeñas de datos sintéticos: fuente no final rechazada antes de leer archivos; IDs/cupos configurables; roles ausentes/conflictivos; copa separada sin collider; agua; spawns anidados usando matrices mundiales. No crean exports ni recetas y no ejecutan el parser contra fuentes activas. Además se comprueba sintaxis de las entradas y `--help`. Evidencia: `map-generator-tool-check.json`.

Corrección de compatibilidad FBX: contrastado con `N:/Blender/5.2/scripts/addons_core/io_scene_fbx/parse_fbx.py:118`, los escalares son `Z` byte con signo, `B` booleano y `C` carácter; no son arrays. Una séptima prueba comprueba esos tipos, el desplazamiento del siguiente escalar y ambas cabeceras 7400/7500 con un bloque sintético. No se cambia el FBX. Tras el aviso de que Scene Builder aún estaba activo, el intento de preparación encontró `sourceFinal:false` y se detuvo antes de leer exports o escribir receta; se espera confirmación nueva de fuente quieta.
