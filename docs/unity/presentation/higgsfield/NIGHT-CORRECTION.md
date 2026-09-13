# Corrección nocturna final: Casa y Campamento

**Resultado definitivo:** ajuste aplicado por Root, cuatro capturas nocturnas Human/Mosquito aprobadas por Root, diez sesiones locales finales Play Mode PASS y revalidación de iluminación cinco mapas PASS. Los recibos `*-night-final.json` y `night-adjustment-receipt.json` registran éxito. Catálogo actual SHA256 `bd686d181fdf16356bc87b92146bd7e9283856beb528354eb50226ccbd54d3ea`; escena y GUIDs preservados. Detalles e interpretación del caso agregado 1/1 en PRESENTATION-CLOSURE.md. La preparación y condiciones previas se conservan abajo como historial.

Las diez cargas Play Mode previas pasaron según Root, pero las capturas Human de Casa/Campamento muestran horizonte y superficies demasiado claros para noche. Se inspeccionaron `UnityPackage/GameLoading/hf-casa-del-patio-v1-Human.png` y `hf-campamento-pinar-v2-Human.png`. El PASS anterior de bindings acredita parámetros/restauración, no aprobación nocturna visual.

| Parámetro | Casa v1 | Campamento v2 |
|---|---:|---:|
| SunUnityIntensity | .10 | .08 |
| Skybox _Exposure | .10 | .08 |
| AmbientSky / _SkyTint | (.05,.08,.14) | (.05,.08,.14) |
| AmbientEquator | (.03,.045,.075) | (.03,.045,.075) |
| AmbientGround / _GroundColor | (.015,.02,.035) | (.015,.02,.035) |

Se conservan color/rotación de luna, luces locales, emisiones, fog, Volume y restantes parámetros. Isla, Yate v3 y Puerto conservan sus entradas y cielos. No se crea arte, geometría, mapa o escena.

## Aplicación autorizada por Root

Config: `docs/unity/presentation/higgsfield/night-correction-input.json`, con GUID/SHA anteriores reales de catálogo, dos materiales y escena. Helper: `LetMeSleep.Editor.HiggsfieldNightCorrection.ApplyFromCommandLine`, argumento `-higgsfieldNightConfig <ruta absoluta>`. También admite `Apply(path)`. Compilado offline sin errores/advertencias; requiere ejecución nativa de Root.

El helper sólo admite los dos IDs/materiales propios y el catálogo instalado `HiggsfieldFiveMaps.asset`. Compara la serialización completa excluyendo únicamente los cuatro campos Lighting permitidos por entry y las tres propiedades Material permitidas. Comprueba identidades de los cinco prefabs, dependencias y navegación, así como hashes de los cielos de los otros tres mapas. Guarda sólo catálogo y dos materiales mediante SaveAssetIfDirty. No usa guardado global.

Realiza readback mediante SerializedObject de campos y propiedades. Inspecciona el AlfaApplication de la escena existente y su referencia `HiggsfieldMaps`, verificando GUID/SHA/meta y estado guardado. No guarda ni crea escena. Un batch dedicado que arranca en una única Untitled puede abrir la escena final como baseline; en los demás casos reutiliza una escena guardada o abre/cierra de modo aditivo. Un fallo intenta rollback de los tres assets mediante APIs Unity y deja recibos no exitosos; nunca se considera aprobado por ese intento de recuperación.

Recibos **nuevos**, bajo `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/UnityPackage/`:

- `night-adjustment-receipt.json`: SHA config, pre/post del catálogo y materiales, readback y comprobaciones de invariantes.
- `catalog-receipt-night-final.json`: inspección del catálogo existente con GUID, SHA actual y cinco mapas. No reafirma creación.
- `scene-receipt-night-final.json`: inspección de escena existente, SHA/identidad intactos y referencia guardada al catálogo con su nuevo SHA. No reafirma creación ni modifica Build Settings.

Los recibos previos permanecen como evidencia histórica del catálogo con SHA `21ec0558...`. Root debe usar los SHA nuevos al regenerar inputs de fixtures y el índice cruzado; el GUID de catálogo y la referencia de escena permanecen iguales.

## Reproducción de configuraciones

Se actualizaron los configs individuales de catálogo y cielo de Casa/Campamento. `HiggsfieldSkyboxMaterialBuilder` ahora admite `overrideExposure` y `exposure`: false/ausente conserva el builtin; true exige propiedad _Exposure y número finito0..8, lo aplica y lo registra en el recibo. Los dos configs tienen true y .10/.08; no contienen una exposición ignorada por el builder.

`catalog-input-night-final.json` conserva el input de cinco mapas con sólo los cuatro campos permitidos de las dos entradas actualizados. Es fuente para reproducción desde cero; **no ejecutar el builder de creación sobre el asset instalado existente**. Para éste usar NightCorrection. Los `*.unity-review-night-final.json` mantienen las luces locales originales y los nuevos valores ambientales, sin guardar escena; NativeReview continúa capturando SolidColor, por lo que la exposición Skybox se evalúa con gameplay real, no con esos fondos planos.

La comparación offline de JSON confirmó entradas de los otros tres mapas exactamente iguales y locales intactas. No se ejecutó Unity/GPU desde el worker. Root realizará nueva evidencia de diez cargas tras aplicar y decidirá si Casa/Campamento se ven nocturnos. Igualdad de parámetros, compilación o recibos exitosos no bastan para esa aprobación visual.
