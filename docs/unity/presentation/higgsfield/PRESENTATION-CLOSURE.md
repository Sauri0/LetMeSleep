# Cierre de Presentación — cinco mapas Higgsfield

## Cierre definitivo después de la corrección nocturna

Root aplicó el ajuste nocturno y aprobó visualmente las cuatro capturas Human/Mosquito de Casa y Campamento: cielo nocturno, rutas legibles y luces cálidas. Esta aprobación proviene de la revisión de imágenes por Root, no de igualdad de parámetros. Las imágenes finales están en `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/UnityPackage/GameLoadingFinal/`.

Se verificaron los cuatro recibos actuales `night-adjustment-receipt.json`, `catalog-receipt-night-final.json`, `scene-receipt-night-final.json` y `lighting-receipt-night-final.json` en UnityPackage: **success=true**. La revalidación de iluminación terminó con los cinco mapas PASS en bind/rebind/unbind/destrucción, `assetsUnchanged=true` y `originalSceneStateRestored=true`.

El catálogo conserva GUID `5a2287ce1168159439a0d2aa8486f5d8`; su **SHA256 final** es `bd686d181fdf16356bc87b92146bd7e9283856beb528354eb50226ccbd54d3ea`. La escena conserva SHA256 `c4fb082660d80211238ad7e7a649cc9fef17a1172972d76a17c8584cb5798e51`, con `savedBindingVerified=true` y `sceneUnchanged=true`. Los recibos anteriores, descritos abajo, documentan la fase previa y no sustituyen estos hashes actuales.

**Diez cargas Play Mode finales PASS**: cinco mapas × Human/Mosquito. `N:/LetMeSleep/Validation/Higgsfield/FiveMapGameLoading-20260913/run-night-final/results.xml` registra 1 caso agregado ejecutado, 1 Passed, 0 Failed; no son diez casos NUnit independientes. El reporte `UnityPackage/GameLoadingFinal/five-map-game-loading.txt` enumera las diez sesiones locales, verificando identidad/runtime/navegación, tres actores con valores finitos y regreso al menú. No acredita WAN, recorrido completo, capacidad de roles ni rendimiento.

El primer preflight de NightCorrection rechazó el wrapper EditorJsonUtility antes de mutar o generar recibos. Root corrigió el acceso a los cuerpos MonoBehaviour/Material, mantuvo las invariantes y obtuvo el PASS posterior. Ese intento inicial no es un fallo del resultado final y la versión vieja del worker no debe reemplazar la corrección central. No se editaron helpers ni se ejecutó Unity/GPU durante este cierre documental.

**Departamento cerrado, sin cambios adicionales pendientes.**

## Evidencia histórica anterior al ajuste nocturno

**PASS nativo de iluminación para los cinco mapas en Edit Mode**, ejecutado por Root con Unity **6000.3.24f1**. Los recibos finales de catálogo y copia de escena también registran PASS. Este cierre documenta esos resultados leídos del disco; el worker no ejecutó nuevas sesiones Unity/GPU.

## Resultado final

| ID instalado | Luces locales | Bind | Rebind | Unbind | Destrucción sin residuos |
|---|---:|---|---|---|---|
| hf-isla-del-laguito-v2 | 0 | PASS | PASS | PASS | PASS |
| hf-casa-del-patio-v1 | 21 | PASS | PASS | PASS | PASS |
| hf-campamento-pinar-v2 | 23 | PASS | PASS | PASS | PASS |
| hf-yate-a-la-deriva-v3 | 6 | PASS | PASS | PASS | PASS |
| hf-puerto-del-faro-v1 | 21 | PASS | PASS | PASS | PASS |

El fixture confirmó parámetros de iluminación y anclas reales, skybox y estado ambiental, rebind sin duplicados y restauración tras Unbind. El recibo registra `assetsUnchanged=true`, `originalSceneStateRestored=true` y `error=null`. Son 71 luces locales distribuidas entre mapas probados individualmente, no 71 luces concurrentes en una partida.

Catálogo final: `Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset`, GUID `5a2287ce1168159439a0d2aa8486f5d8`, SHA256 `21ec0558ce3218d707412881791e2b05084e224c734270e75700d293402b39cd`. El recibo de autoría declara cinco entradas, `fiveMapStructuralCheck=true` y los cinco encabezados SpatialData validados. Su `sceneInstalled=false` describe el alcance de ese builder; la instalación posterior está acreditada por el recibo separado de escena.

Escena nueva: `Assets/Scenes/LetMeSleepHiggsfield.unity`, GUID `60df67d16705fc64d9244c13fd7979e4`, SHA256 `c4fb082660d80211238ad7e7a649cc9fef17a1172972d76a17c8584cb5798e51`. El instalador registra `savedBindingVerified=true`, `originalPreserved=true` y `catalogPreserved=true`. La fuente `Assets/Scenes/LetMeSleepBoot.unity` conserva GUID `ae104b9ec9548da42bb7e3b058320e3b` y SHA256 `6c9ae9b72f0049e6c4a1d579a34d755f2cbc2f8bc807f8726b14e9719f40f64c`. Ese paso no modifica Build Settings ni acredita un build/player.

## Evidencia trazable

Directorio de recibos: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/UnityPackage/`.

| Recibo | SHA256 leído para este cierre |
|---|---|
| lighting-receipt.json | 9c5c0886c6a5e87ab54a98caa03715e4ba31c90a6673ec6aecd0c2d928c83b21 |
| catalog-receipt.json | 38222ea226e8f71907161c91c49b7f73bb04851ef4260d53b4c4d7d8c9d22e91 |
| scene-receipt.json | b701e5bd64f79ff3eb2e6f3cf022324c7d5f3c17f5761578ccb150e3154a60cc |

La ejecución de iluminación registra DLL SHA256 `da77ceba1677a6babfd347218e69285bbd9bbb070283b8acfd217c737cdaafe3` y config SHA256 `3d6189cd82524714d812fd6653898213fa65ab8358fda827fcce13ace4d3563b`. `lighting-input.json` selecciona los cinco IDs de la tabla y `baselineScenePath=Assets/Scenes/LetMeSleepHiggsfield.unity`.

Root corrigió e integró el fixture antes del PASS: evita llamar SetActiveScene cuando la escena deseada ya está activa y comprueba después la identidad efectiva. Para un batch dedicado que arranca con una única escena Untitled, permite abrir la baseline guardada explícita antes de crear la escena aditiva. Esas correcciones están en central y en la DLL del recibo; no se editaron ni recompilaron desde esta entrega de documentación. Las DLL anteriores y configs históricos del worker no sustituyen esa versión final.

Yate instalado es **v3**, no los candidatos v1/v2. Los configs v2 del departamento permanecen como antecedentes; Root adaptó la integración final a v3. El fixture nativo final usa los IDs recibidos, sin depender de una revisión hardcodeada.

## Alcance del cierre

Quedan acreditados autoría estructural del catálogo, binding serializado en copia de escena y ciclo de iluminación **Edit Mode** con rig real. El PASS no es una aprobación estética de las imágenes: igualdad de color/rango/skybox no demuestra por sí sola aspecto visual, sombras renderizadas o composición.

Las **diez cargas Play Mode estaban en curso por Root** al solicitar este cierre; no se les asigna resultado aquí. Tampoco se amplía este recibo a Update/LateUpdate, Destroy diferido, agua GPU/CPU, compilación/render de shaders, transitabilidad, UI, audio, FPS o WAN. Las validaciones específicas de esos sistemas requieren sus propios recibos. El departamento queda sin trabajo activo adicional hasta nueva indicación de Root.
