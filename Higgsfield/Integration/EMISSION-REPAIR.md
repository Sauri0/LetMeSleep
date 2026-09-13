# Emisión plana y reparación de mapas existentes

Extensión acotada para conservar la emisión autorada de materiales, sin convertir intensidades de objetos Light ni modificar iluminación/cámaras. Se abrió la captura existente `02-casa/UnityFinal/unity-overview.png` como referencia del problema; no se produjo un render nuevo ni se ejecutó Unity/Blender desde esta tarea.

## Datos y compatibilidad

HiggsfieldSwatch añade `emissionRgb` (tres canales **lineales**) y `emissionStrength`. Recetas anteriores sin ambos campos equivalen a negro con fuerza cero. Se rechazan componentes negativos/no finitos, fuerza inválida, fuerza positiva sin RGB y productos que desborden float. No se aplica un multiplicador de exposición, un color base como emisión ni un valor obtenido de luces.

El generador lee `emissiveFactor` y `KHR_materials_emissive_strength.emissiveStrength` del GLB. Compara **el producto lineal** con `emission_color × emission_strength` del audit, con tolerancia de representación numérica. Esto conserva casos donde el exportador absorbió una fuerza menor a uno dentro del RGB, como CASA_WindowAmber. Si el GLB emite y el audit no contiene los nuevos campos, falla. La emisión texturada se rechaza en este contrato plano. Las formas factor×strength siguen la [especificación Khronos](https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_materials_emissive_strength/README.md).

En URP/Lit, ApplyEmission escribe el producto lineal en `_EmissionColor` usando SetVector, habilita/deshabilita `_EMISSION` y actualiza sus flags emisivos. Se usa SetVector para evitar la conversión de SetColor en propiedades HDR; [Unity documenta la diferencia](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Material.SetVector.html). La declaración `[HDR] _EmissionColor` y el keyword se comprobaron también en el paquete URP instalado. No ejecuta un bake: el flag BakedEmissive no crea iluminación horneada por sí solo. Bloom, exposición y la contribución de luces siguen bajo control de Presentation/root.

## Reparar exclusivamente materiales existentes

Entrada nueva:

```csharp
HiggsfieldEmissionRepair.Repair(
    mapId, sourceFbxSha256, newRecipePath,
    expectedContentHash, receiptPath);
```

Namespace `LetMeSleep.Content.Editor.Higgsfield`. El Encargado debe invocarla sólo durante su turno nativo, en Edit mode inactivo. `expectedContentHash` es el hash actual que verifique en el prefab, no una constante de esta documentación. `receiptPath` debe ser un archivo nuevo; el helper no sobrescribe una evidencia anterior.

Antes de cambiar assets, exige mapa/FBX/receta coincidentes, fuente externa y FBX ya importado con el SHA indicado, mapa completo, prefab y materiales sin cambios sin guardar. El contrato C# compara recursivamente todos los campos excepto emisión: una modificación de BaseRGB, paleta/orden, rutas, spawns, reglas o bounds se rechaza. Verifica además las rutas y materiales reales de cada Renderer del prefab. La receta original `Data/import-recipe.json` se conserva como baseline de geometría; la receta nueva queda externa y se identifica por ruta y SHA en el recibo.

El helper modifica **sólo `_EmissionColor`, `_EMISSION` y flags emisivos de los materiales que difieran**, conservando objetos de material, rutas y GUID. Guarda **sólo ContentHash** como metadato del prefab existente mediante SavePrefabAsset: no lo instancia, reconstruye ni sustituye por otro asset. No llama ModelImporter/SaveAndReimport, no abre escenas, no crea geometría, no cambia luces y no sobrescribe SpatialData de navegación. Una escena cargada con ContentHash override o mapa desligado del prefab se rechaza; no se guarda automáticamente.

Compara una huella de componentes/transformaciones/referencias del prefab antes/después, incluyendo todos los campos actuales de EnvironmentMapDefinition salvo ContentHash. También comprueba hashes de los archivos existentes ajenos a las modificaciones permitidas. Un error intenta restaurar valores de materiales y ContentHash; el recibo distingue ROLLED_BACK de FAILED_ROLLBACK_REQUIRES_REVIEW. La ejecución y esa compensación **todavía requieren prueba nativa**, no están certificadas por compilar.

Si hay cambios, el nuevo hash es:

```text
SHA256(UTF8("higgsfield-emission-repair-1\n" +
            previousContentHash + "\n" + lower(sourceFbxSha256) + "\n" +
            SHA256(UTF8(newRecipeText))))
```

Si los materiales ya coinciden, devuelve NO_CHANGE y conserva el hash actual. El recibo contiene antes/después de emisión, keyword, flags, SHA de cada .mat, GUID, identidad del prefab, hash anterior/nuevo y SpatialData preservado. Root debe revisar herencia del hash en las escenas guardadas y refrescar cualquier catálogo/caché central; el helper no toca Bootstrap/NativeReview/Online. No se presenta este hash derivado como una aprobación de gameplay.

## Candidato Casa y evidencia offline

Receta nueva ya preparada por CPU, separada de UnityRecipe original:

`N:/LetMeSleep/Worktrees/maps/Higgsfield/Integration/.verification/CasaEmission/hf-casa-del-patio-v1.recipe.json`

Su recibo de cruce está al lado. `.verification` queda fuera de Git; puede reproducirse con el descriptor final y el generador actualizado en otra carpeta nueva. `casa.emission-candidate-check.json` registra los hashes y la comparación real C# con la receta entregada previamente.

| Material | Emisión lineal efectiva |
|---|---|
| CASA_LampGlass | (2.3, 0.92, 0.1725) |
| CASA_WindowAmber | (0.48, 0.2736, 0.0864) |

Los otros 27 materiales permanecen sin emisión. El nuevo archivo conserva las 3664 reglas y todos los campos ajenos a emisión. La compilación offline incluye runtime, contrato, importador y helper separado; 37 comprobaciones C# y 9 pruebas Python pasan. Se comprobaron compatibilidad antigua, producto HDR, rechazos de datos inválidos y rechazo de cambios ajenos a emisión. **No se ha ejecutado Repair ni demostrado corrección visual nativa.**
