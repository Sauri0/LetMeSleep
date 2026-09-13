# Preparación offline de catálogo y escena Higgsfield

Estado: código y configuraciones provisionales preparados; **ningún helper fue ejecutado en Unity**. Requiere el catálogo central con `Entry.DisplayName`, `Entries` y la integración `AlfaApplication.HiggsfieldMaps` (commits de Root 5c1306f/8b419cb). No modifica AlfaApplication, gameplay, UI, escenas, prefabs ni ajustes del proyecto.

## Datos de los mapas

Los archivos `*.catalog-provisional.json` son solicitudes individuales para `HiggsfieldMapCatalogBuilder`. Sus `entries` pueden incorporarse al catálogo final junto con los otros tres mapas realmente importados y aprobados. No constituyen un catálogo completo ni una instalación. Los nombres visibles propuestos son “Isla del laguito” y “Casa del patio”.

| Dato | Isla v2 | Casa v1 |
|---|---|---|
| Revisión fuente | 01-isla/unity-final-review-config.json | 02-casa/unity-review-config.json |
| Hora de referencia | Día | Noche |
| Intensidad Unity direccional | 1.05 | 0.8 |
| Color direccional | (1, .94, .82) | (.5, .65, 1) |
| Rotación Euler | (48, -35, 0) | (48, -35, 0) |
| Luces locales | 0 | 21 Point: 9 interiores a 3.5/rango 7; 12 prácticas a 2.2/rango 5 |
| Sombras | Direccional Soft | Direccional Soft; 9 interiores Soft; 12 prácticas None |
| SpatialData observado al cerrar esta entrega | isla-navigation-schema1.json | casa-navigation-schema1.json |

Casa recibió schema1 en central durante esta preparación. La evidencia fue actualizada leyendo el GUID real de `SpatialData` del prefab, sin editarlo ni inventar navegación. El archivo `offline-evidence.json` registra hashes, identidad, posiciones y estado del encabezado; esto no demuestra transitabilidad.

Las 21 anclas de Casa son nodos Null reales del FBX importado, bajo `Environment/`. Las variantes numéricas se llaman `CASA_Path_Lantern_Light_Anchor.001` a `.003`. La posición de revisión coincide con (FBX x, y, -z). El importador tiene `globalScale=1`, `bakeAxisConversion=1`, `preserveHierarchy=1`, `importLights=0`. Por ello `suppressLightPaths` queda vacío. El builder vuelve a resolver las rutas en el prefab Unity y exige posición local dentro de 1 cm: si la importación cambia, debe fallar, no inventar anclas ni moverlas.

Se copian los colores Trilight y valores globales de las escenas de revisión: ambient/reflection intensity 1, fog desactivado, modo Exp2, densidad .01, rango 0–300. No se instala un VolumeProfile antiguo. Los valores son intensidades Unity de NativeReview, no energía Blender.

Las configuraciones no cambian el pipeline URP. La revisión restaura shadowDistance 40; Casa capturó con 85 y NativeReview puede usar 250 por defecto cuando no se especifica. El pipeline temporal de captura también usa MSAA 4. Estas condiciones de captura no equivalen al presupuesto del juego; el soporte y coste de las nueve sombras Point quedan para el turno nativo de Root.

## Materiales de cielo nuevos

API: `LetMeSleep.Editor.HiggsfieldSkyboxMaterialBuilder.Build(absoluteConfigPath)`.

CLI (sólo dentro del turno autorizado por Root): `-executeMethod LetMeSleep.Editor.HiggsfieldSkyboxMaterialBuilder.BuildFromCommandLine -higgsfieldSkyboxConfig N:/ruta/absoluta/config.json`.

Usar primero los dos archivos `*.skybox-provisional.json`. Crean exclusivamente:

- `Assets/LetMeSleep/Presentation/Generated/Materials/hf-isla-del-laguito-v2-Skybox.mat`
- `Assets/LetMeSleep/Presentation/Generated/Materials/hf-casa-del-patio-v1-Skybox.mat`

El helper exige el recurso incorporado `Default-Skybox.mat`, GUID `0000000000000000f000000000000000`, fileID `10304`, shader `Skybox/Procedural`. Esa identidad está serializada en ambas escenas de revisión. La resolución del nombre de recurso se verificará en Unity y falla explícitamente si difiere; no busca otro material como reemplazo. Hace `new Material(builtin)`, cambia sólo `_SkyTint` y `_GroundColor`, crea asset/GUID nuevos y guarda un recibo externo nuevo. Nunca modifica el material incorporado ni RenderSettings.

Los tintes se derivan provisionalmente de `AmbientSky` y `AmbientGround` de la revisión de cada mapa; exposición, atmósfera y demás propiedades conservan los valores del builtin. No son colores de cielo medidos. **NativeReview usa CameraClearFlags.SolidColor**, con fondos día (.19, .35, .46) y noche (.025, .055, .09). Un juego que use Skybox mostrará el cielo procedural y requerirá revisión visual; una cámara que mantenga SolidColor no mostrará ese cielo. Esta entrega no cambia cámaras.

Las rutas Material de los configs de catálogo son destinos previstos, no assets ya creados. El builder de catálogo debe fallar si esos materiales aún no existen. Las carpetas destino y de recibos deben existir. Cada ejecución exige rutas nuevas y rechaza cualquier archivo, meta o recibo preexistente.

## Instalación en una copia nueva

API: `LetMeSleep.Editor.HiggsfieldBootstrapSceneInstaller.Install(absoluteConfigPath)`.

CLI: `-executeMethod LetMeSleep.Editor.HiggsfieldBootstrapSceneInstaller.InstallFromCommandLine -higgsfieldSceneConfig N:/ruta/absoluta/scene-config.json`.

1. Crear los materiales y el catálogo final por APIs Unity, con cinco entradas reales, `requireFiveMaps=true` y `allowMissingSpatialDataForAuthoring=false`. Las solicitudes individuales de esta carpeta no satisfacen este requisito.
2. Completar `scene-install.template.json` con la ruta, GUID y SHA256 del catálogo final guardado, los cinco `approvedMapIds`, una ruta nueva `Assets/.../*.unity` y un recibo absoluto nuevo fuera de Assets. No hay valores ficticios para los tres mapas restantes.
3. Seleccionar una **escena bootstrap alfa verificada y guardada en disco**. La plantilla registra el candidato central `Assets/Scenes/LetMeSleepBoot.unity`, GUID `ae104b9ec9548da42bb7e3b058320e3b`, SHA256 `6c9ae9b72f0049e6c4a1d579a34d755f2cbc2f8bc807f8726b14e9719f40f64c`. Es una identificación offline, no una certificación de ejecución. Si cambia, Root debe verificar la nueva fuente y actualizar esos campos; el helper no los refresca silenciosamente.
4. Ejecutar en Edit Mode inactivo. El helper valida el catálogo completo, IDs aprobados, una entrada de cada categoría, prefabs y encabezados SpatialData schema1/map_id. Rechaza catálogo/prefabs/datos sucios.
5. Usa `AssetDatabase.CopyAsset` hacia una escena nueva y exige un GUID distinto. Abre sólo la copia en modo aditivo, encuentra exactamente un AlfaApplication, asigna su propiedad serializada `HiggsfieldMaps`, registra override si corresponde y guarda únicamente esa escena. Cierra y reabre la copia para comprobar la referencia persistida. No aplica cambios al prefab fuente.
6. Verifica nuevamente SHA/GUID de fuente y catálogo, además de sus metas. Cierra la copia y restaura la escena activa previa. El recibo sólo marca éxito tras guardar y comprobar. La escena fuente abierta, incluso con cambios sin guardar, no se usa como fuente de datos ni se guarda: se copia la versión verificada del disco.

No cambia Build Settings, la escena de arranque efectiva, ni produce un player/build. Root selecciona después la nueva copia dentro de su integración. No usa guardados globales ni restaura el setup recargando otras escenas. Ante fallo conserva el nuevo artefacto parcial y un recibo no exitoso para inspección; no borra ni sobrescribe resultados existentes. El llamador controla `-quit`; los helpers no cierran una sesión residente.

## Verificación efectuada

Compilación offline con dotnet 10.0.202, referencias Unity 6000.3.24f1 y catálogo fuente central: 0 errores, 0 advertencias. Comparación Python de las 21 anclas FBX contra posiciones de revisión: coincidencia menor de 1e-6 antes de la futura comprobación Unity de 1 cm. JSON parseado y hashes registrados.

Pendiente: ejecutar los helpers en el turno de Root, verificar importación/identidad builtin, catálogo de cinco mapas, referencia serializada en escena, navegación real, cámara/cielo, sombras, audio, gameplay, FPS y WAN. No se reclama calidad visual ni rendimiento a partir de esta preparación.
