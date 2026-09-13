# Casa del Patio — receta final entregada

## Copia editable ajustada al marcador documentado en Unity

Se entregó `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/02-casa/UnityAdjustedSource/HF_MAP_02_casa_UNITY_ADJUSTED.blend`, SHA256 `4745f076b6df86722f5ad4777dae833a9b108e787b926b3429e9029cc9e9338f`. Contiene solamente la escena Casa y sus dependencias. El único ajuste autorizado es `Spawn_Human_03.001`: Blender Z pasa de `0.25` a `0.28411149978637695` m, un incremento de `0.03411149978637695` m, con XY `[-2.5, 2.0999999046325684]` preservados.

El comprobante vecino `marker-adjustment.json` registra PASS, hashes de los originales y lectura independiente del EMPTY guardado. Se usó el MCP de Blender ya abierto, sin iniciar otro proceso. La copia se escribió con `bpy.data.libraries.write`; el marcador vivo se restauró a Z `0.25`. Se comprobó que todos los objetos, mallas y miembros de las escenas vivas conservaran su estado y que Puerto siguiera activo, con su archivo intacto. La lectura usa `matrix_basis` porque el EMPTY guardado, sin padre, restricciones ni animación, se carga temporalmente sin vincularlo a una escena y no tiene matriz mundial evaluada.

Los GLB, FBX, fuente original y auditoría anteriores permanecen intactos. **Los exports conservan el marcador original a 0.25 m; no hubo reexportación ni reimportación.** Esta copia sincroniza el único marcador documentado y no certifica equivalencia portable completa con Unity. El índice de entrega hace visible esa diferencia. El script acotado `casa_adjusted_source.py` impide sobrescribir una entrega completada; no se debe ejecutar como tarea periódica.

## Receta y verificación anteriores

Receta: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/02-casa/UnityRecipe/hf-casa-del-patio-v1.recipe.json`.
Cruces de fuente: archivo vecino `hf-casa-del-patio-v1.validation.json`.
Descriptor reproducible: `Higgsfield/Integration/casa.final-input.json` en este worktree; el descriptor PENDING anterior permanece bloqueado como histórico.

Fuente final confirmada por el Encargado: `HF_MAP_02_casa_UNITY.fbx` y `HF_MAP_02_casa_UNITY.glb`, audit efectivo renovado y `HF_MAP_02_casa_report.json`. SHA256 FBX `f0586b7087395c0f97e7b1eaf548aabcb62ab937ca9f93a98d2ac0ae79688bd7`. La generación comprueba nuevamente los hashes antes de escribir. No hubo llamadas a Unity, Blender, bridge ni medios pagos desde esta tarea.

Resultado: **3664 mallas, 29 materiales, 3313 solid, 351 decoration no sólidas, cero agua/espuma, 5 humanos y 16 mosquitos**. Capa Default; PlayBounds mínimo [-25,-1,-22], máximo [25,12,22], Unity XYZ en metros. Spawns físicos con `.001` preservados, con matrices mundiales y paths reales. El contrato C# actual Validate/Resolve pasa las 3664 reglas; evidencia compacta y hashes en `casa.delivery.json`.

El primer cruce detectó 182 materiales de instancia mal representados en el audit anterior, que tomaba o.data.materials. Se detuvo sin generar receta ni cambiar las comprobaciones. El Encargado renovó el audit con o.material_slots: ahora incluye los 29 colores efectivos, incluido CASA_RoofDark. FBX y GLB ya coincidían por instancia. La receta final pasó el generador estricto sin cambios de código ni parches a los exports. `casa.material-audit-diagnosis.json` conserva el diagnóstico y su resolución.

Pendientes nativos: tiempo de importación y coste de 3313 MeshColliders; preservación visual de materiales por instancia; rutas, escaleras y cápsulas en dos pisos; origen de spawns en pies; navegación SpatialData, reglas/registro de mapa e iluminación. Los 21 marcadores y las comprobaciones de archivos no certifican una partida completa, aprobación artística ni rendimiento. Root ejecuta la importación y esas verificaciones.
