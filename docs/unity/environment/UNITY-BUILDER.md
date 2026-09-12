# Builder de la muestra — ejecución por Director

Bootstrap `0d6c3fa` incorporado a environment como `559a3f8`. Se entrega código Editor en ownership nuevo `unity/Assets/LetMeSleep/Content/Editor/Environment/`; no se ejecutó Unity ni Blender en este lote. Compilación offline contra APIs instaladas de Unity 6000.3.24f1 aprobada, con recibo/hash en `UNITY-BUILDER-OFFLINE-CHECK.json`. Esto no acredita importación ni ejecución nativa.

## Entrada estática

Después de integrar el commit, esperar compilación del editor residente e invocar:

```csharp
LetMeSleep.Content.Editor.EnvironmentSampleBuilder.Build();
```

También expuesto en menú `Let Me Sleep > Environment > Build Room Sample`. El método exige Edit mode inactivo, no inicia editor/Play mode/bake/render ni modifica paquetes, settings, build scenes o capas. Rechaza reconstruir si la escena generada está abierta para conservar posibles cambios. Usa una escena aditiva temporal, restaura la escena activa y cierra solamente la escena temporal al acabar.

## Resultado esperado al ejecutar

En `unity/Assets/LetMeSleep/Content/Environment/RoomSample/` crea/importa Models, Materials, Prefabs, Data y Scenes a través de Unity. Copia las dos exportaciones de integración y los manifests desde `art_source/unity/environments/room_sample/` de esa misma copia del repo. Unity genera los .meta de esos assets. No importar `.blend` directamente ni instanciar además el FBX completo de revisión.

- `Prefabs/Door_01.prefab`: puerta independiente, 11 meshes, pivote real y único Rigidbody cinemático sin gravedad, collider hijo del pivote.
- `Prefabs/RoomSample.prefab`: 55 meshes en total con prefab de puerta anidado, 45 colliders hijos separados del mesh, 13 anchors vacíos de Presentation. Prefab guardado con puerta cerrada, sin luces/audio/volumes.
- `Scenes/RoomSample.unity`: habitación con puerta abierta −100°, cámara humana a 1.53 m y cámaras alternativas desactivadas para puerta/mosquito. Dos luces provisionales de revisión están en un root de escena separado, no en el prefab fuente; Worker 2 las sustituye al integrar su receta.
- Diez materiales URP/Lit compartidos, remapeados por nombre. Si ya existen se conservan, para respetar ajustes posteriores de Worker 2. Material existente no URP provoca fallo explícito.
- `docs/unity/environment/UNITY-IMPORT-RECEIPT.json`: se escribe solo si acaba la ejecución nativa. Incluye hashes del modelo/manifest, conteos, comprobaciones y limitaciones. No existe como prueba hasta que Director ejecute el método.

El builder comprueba límites de shell en metros, origen/ejes del pivote, centro de hoja cerrada y giro a −100°, escala unitaria, conteos, materiales y presencia de UV2 en estáticos. Configura generación UV2 con margen calculado para 16 texels/m y escala mínima1. La existencia del canal no prueba ausencia de solapamientos o margen suficiente tras bake: queda ese gate para revisión nativa.

Si `WorldStatic`/`WorldDynamic` no existen, las colisiones de muestra usan Default con advertencia en el recibo. No crea capas fuera de ownership. Director debe ajustar capas/máscaras antes de gameplay. `CameraCollision` es anchor semántico; la cámara debe consultar colliders estáticos y hoja móvil en su ángulo real, nunca una pared invisible en el vano abierto. No duplicar física al crear proxies de consulta.

La cama lleva LODGroup con solo LOD0, transición None y sin desaparición a distancia. No se declara optimización ni variantes LOD inexistentes. UV2, sombras/reflexiones/probes y GI se configuran según el manifest; no se hornean luces ni oclusión automáticamente.

## Manifest de Presentation

`presentation_manifest.json`, reproducible con `build_presentation_manifest.ps1`, amplía el contrato original según Worker 2 `190fad6`: flags para 55 renderers, 45 cajas, anchors de luz/reflexión/audio/cámara, bounds de planta/habitación de muestra y dos portales. Ventana es cerrada, con paso humano y acústico deshabilitados. Mapeo acústico por material del shell: piso Wood, pared/techo Stone provisional. Tile/Grass y zonas pasillo/escalera/patio/lobby corresponden al lote de casa futuro, no se inventan volúmenes presentes.

Los anchors están descritos en el manifest y se instancian al ejecutar este builder; los FBX originales permanecen sin cambios. Límite del lote: muestra revisable y datos del contrato, no casa completa ni funcionalidad online.

Referencias de API: [margen mínimo por texel del importador](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ModelImporter-secondaryUVMinLightmapResolution.html) y [conversión de ejes](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ModelImporter-bakeAxisConversion.html). Firmas verificadas además compilando contra ensamblados instalados; la interpretación real del FBX se comprueba al ejecutar en Unity.
