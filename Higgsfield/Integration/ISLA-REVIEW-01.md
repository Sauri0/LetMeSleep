# Isla del Laguito — receta final para revisión nativa

Preparada el 2026-09-13 a pedido del Encargado. Archivo listo: `N:/LetMeSleep/Worktrees/maps/Higgsfield/Integration/isla.review-01.recipe.json`. Identidad `hf-isla-del-laguito-review-01`, fuente marcada final por autorización del Encargado. No es un mapa habilitado para partidas ni aprobación visual.

FBX: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/01-isla/HF_MAP_01_isla.fbx`.
SHA256: `a04362dcee8551fed1321ed8e353a921c633fe7663ad0f906f04096b68f727f8`.

Se leyeron únicamente archivos: FBX binario 7400, GLB, audit final actualizado con matrices/propiedades y `HF_MAP_01_isla_build_report.json` (éste es el nombre real del informe). Hashes de los cuatro en `isla.review-01.validation.json`. Se comprobó que no cambiaron durante la preparación. No se abrió Unity/Blender, no se alteró geometría ni importador y no hubo generaciones.

## Contenido y clasificación

Los 488 nombres de objetos y las rutas completas de jerarquía coinciden en audit/GLB/FBX; 471 son mallas. GLB proporciona `extras.collision_role` explícito para todas. Cada malla tiene una regla exacta, sin comodines ni inferencia de colecciones como nodos FBX.

**Capa corregida a `Default`.** El Encargado informó que el primer intento nativo rechazó la capa `WorldStatic` antes de escribir assets: esa capa no existe en el proyecto actual. Su segunda prueba usa `Default`, igual que los mapas existentes, con GeometryMask=~0. La receta y su generador en este worktree incorporan esa corrección; no se cambia ProjectSettings. La copia activa que el Encargado puso en Artifacts permanece bajo su propiedad y no se tocó. Futuras recetas deben usar una capa existente y verificable, sin heredar `WorldStatic` del ejemplo inicial.

| Categoría | Mallas | Resultado del importador actual |
|---|---:|---|
| solid | 321 | MeshCollider completo y GameplaySurface, IDs ordenados desde 1000000 |
| foliage | 144 | Plantas/hierba sin colisión; sombras conservadas |
| decoration | 3 | Alfombra, farol de porche y farol/tazas de picnic sin colisión |
| water | 2 | Lago y océano sin colisión, animación visual |
| foam | 1 | Espuma de costa sin colisión |

La cuenta de triángulos del audit/report final es 126177 con instancias. Las 38 muestras de color son los `base_color` finales del audit, en linear, coincidentes con los factores del GLB. El océano tiene **cuatro** materiales: ISLA_Ocean, ISLA_OceanLight, ISLA_OceanDeep, ISLA_OceanShallow. El lago conserva ISLA_Lake, ISLA_LakeLight, ISLA_LakeDeep. El recibo offline enumera slots y primitivas por malla.

Las cuatro paredes `Cabin_Wall_Front_OpenDoor_COLLIDABLE`, `Cabin_Wall_Back_Window_COLLIDABLE`, `Cabin_Wall_East_Windows_COLLIDABLE` y `Cabin_Wall_West_Window_COLLIDABLE` tenían slots Wood/WoodLight/Wood en el audit; el exportador FBX deduplicó a Wood/WoodLight. No se perdió un color distinto. Las demás listas coinciden exactamente; el importador conserva los slots que efectivamente entregue Unity.

PlayBounds de revisión: mínimo **[-54,-4,-49]**, máximo **[54,19,49]**, Unity XYZ en metros. Se obtuvieron de los límites mundiales de sólidos autorados, incluyendo muelles/árboles, más 2 m horizontales/superiores y 0.5 m inferiores, redondeados hacia afuera. X/Z simétricos toleran un cambio de orientación horizontal mientras se verifica la conversión real. Excluyen el plano oceánico de 600 m y la espuma. El volumen rectangular permite espacio de vuelo alrededor de la isla; no es una máscara exacta de costa ni impide salir del mapa por sí mismo.

## Riesgos concretos antes de jugar

1. **Navegación no conectada.** El importador actual pone `import-recipe.json` en `EnvironmentMapDefinition.SpatialData`. GameplayBotNavigation requiere `schema_version=1`, `map_id`, `zones`, `portals` y, cuando corresponda, `stair`. La receta no cumple ese esquema. El Encargado resolverá esos datos tras importar: no invocar BeginRound suponiendo navegación válida.
2. **Sólo 2 humanos y 1 mosquito.** Son EMPTYs reales, válidos únicamente para revisión. No hay cupo 16, selección de roles, separación con cápsulas ni comprobación de superficies de apoyo certificadas. Los marcadores pueden traer rotación de conversión FBX; comprobar el eje vertical de los actores al usarlos, no sólo su posición.
3. **81 colliders de árbol incluyen copa.** Cada árbol fusiona ISLA_Trunk con materiales de copa en una malla `static_solid`. Esta receta respeta el rol de origen y conserva colisión de troncos; el importador actual colisionará también la copa, lo que puede bloquear vuelo entre hojas. Se enumeran los 81 paths en el recibo. Colisión exclusiva de tronco requiere un ajuste posterior de importador por submesh/material o un proxy separado; este lote no modifica esa geometría.
4. **Agua y presupuesto CPU.** El FBX conserva cuatro BlendShapeChannels: Wave_A/Wave_B para cada agua, aunque el informe describe export de agua en reposo. Esto prueba que existen canales, no qué Renderer entregará Unity. El GLB suma 38400 entradas POSITION del océano, 8866 del lago y 3360 de espuma, incluyendo splits por normales/materiales. El océano podría superar el límite de 20000 de la alternativa MeshFilter CPU; si Unity entrega SkinnedMeshRenderer sin huesos, usa las formas existentes. No aumentar el límite automáticamente sin observar el import y medir.
5. **Movimiento de espuma distinto.** Velocidad fijada a 2π/8 para un ciclo principal de 8 s. La ruta blendshape respeta las formas autoradas; CPU usa amplitud 1.5 cm en lago y 2.5 cm en océano/espuma. La espuma sin morphs y las aguas con morphs no garantizan unión exacta. Revisar separación de costa, normales, culling y rendimiento en movimiento; no se certifican mediante lectura de archivos.
6. **Ejes/unidades pendientes.** FBX declara UpAxis Y+, UnitScaleFactor 1 (centímetros), exporta traslaciones multiplicadas por 100 y escalas de nodo 100. La receta mantiene importScale=1 y el importador useFileScale/bakeAxisConversion. No agregar otro factor 100 o giro por intuición. Verificar tamaño terrestre ≈101.62×80.01 m, niveles de agua 0/1 m y dimensiones de cabaña 8×6 m. Unity puede cambiar handedness respecto del GLB; posiciones de referencia abajo no son un resultado nativo.
7. **Materiales y visualización.** Los materiales GLB son double-sided; el importador URP actual usa su culling predeterminado, por lo que una lámina vista desde el reverso podría desaparecer. Revisar hojas, espumas y superficies delgadas. Colores se conservan como datos linear con conversión en el importador; sin iluminación equivalente no se garantiza apariencia igual. No se importan cámaras/luces del Blender ni emisión especial del farol.
8. **Interacciones aún estáticas.** Puerta abierta, techo removible, bote y muebles quedan según geometría/rol exportados: no reciben interacción, flotación ni lógica de retirada. Falta comprobar tránsito real, escalones, cocina, muelles, agua/caídas y límites. No registrar este ID provisional en RoomRules/UI/Online como mapa definitivo.

## Referencias de posiciones

| EMPTY | Blender mundial XYZ | GLB mundial XYZ, metros |
|---|---|---|
| Spawn_Human_01 | (0,-23,2.304505) | (0,2.304505,23) |
| Spawn_Human_02 | (24,-3,2.639775) | (24,2.639775,3) |
| Spawn_Mosquito_01 | (-4,-5,6) | (-4,6,5) |

La receta usa estos nombres exactos en root; los demás objetos anidados usan paths completos, por ejemplo `Cabin_Root/Cabin_Door_Hinge/Cabin_Door_Open_COLLIDABLE`. `presentationRoot` queda vacío porque no existe un grupo único de EMPTYs de presentación separado del arte. No se reasigna la raíz de la cabaña para fingir ese contrato.

## Validación y consumo

- `Prepare-IslaReview.py` reproduce la receta desde los cuatro archivos finales y escribe evidencia offline. No ejecuta importadores ni modifica fuentes.
- `isla.review-01.validation.json` contiene procedencia, jerarquía comprobada, clasificación, slots, posiciones y pendientes. Es evidencia de lectura de export, no de importación nativa.
- El contrato C# real `HiggsfieldImportContract.Validate()` pasó mediante .NET, y `Resolve()` devuelve la categoría esperada para las 471 rutas. Resultado y hashes en `isla.review-01.contract-check.json`.

El Encargado puede consumir el archivo ya escrito con `HiggsfieldEnvironmentImporter.Import(ruta)` o la entrada `ImportFromCommandLine -higgsfieldRecipe <ruta>`. La carpeta destino debe estar ausente; si una prueba previa falló y dejó `import-incomplete.txt`, inspeccionar sus resultados y acordar la limpieza/revisión de ID antes de repetir. No se borró ninguna carpeta de Unity desde esta tarea.
