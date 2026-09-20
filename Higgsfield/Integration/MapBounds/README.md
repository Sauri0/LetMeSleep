# Límites de cinco mapas — herramienta propuesta y medición

Propiedad concedida por Encargado: este directorio y `unity/Assets/Editor/ProjectBootstrap/HiggsfieldMapBoundaryInstaller.cs` + meta. No modifica UnityGameplayWorld, arte, imports ni el bootstrap. No se abrió Unity/Blender ni se aplicó en central. La recuperación pertenece al Director; el installer añade su opt-in al root por el esquema aprobado.

## Dimensiones aprobadas por Encargado para preflight

Las caras interiores laterales/techo y SafetyBounds/PlayBounds usan estas cotas locales Unity. Los cuatro laterales se prolongan hasta wallBottomY=-10 independientemente del mínimo de seguridad. Espesor 0,5 m hacia fuera y solape en esquinas/techo. **No hay piso.**

| Mapa | X | Y seguridad | Z |
|---|---|---|---|
| Isla | -54..54 | -4..16 | -49..49 |
| Casa | -25..25 | -1..12 | -22..22 |
| Camp | -50..55 | -.5..10 | -40..40 |
| Yate | -7..7 | .05..12 | -18..18 |
| Puerto | -58..58 | -2..22 | -46..46 |

Puerto: plataforma/escalera del faro hasta16.16, cubierta19.5; techo22 permite altura humana1.72 más margen. Yate: casco X±4/Z±15, plataforma1.1, cubierta superior6.1, radar8.79; límites±7/±18 dan vuelo alrededor. Camp Terrain_Grass llega X49.323, Hill_NE47.394, lago48.423 y Jetty41.043: el límite antiguo X46 cortaba terreno.

Se preservan los 327/3313/488/96/418 sólidos seleccionados por nombre en la configuración. Permanecen fuera del movimiento13 elementos periféricos de Camp y40 de Puerto, sin borrar malla ni quitar fondo. La medición inicial halló33/68 sólidos fuera de límites declarados anteriores. No todos son POIs; los35BackdropPine son escenario de fondo. Las listas exactas, AABB y colecciones están en `outside-declared-solids.csv` y `external-scenery-outside-approved-play.json` externos.

## Agua: huellas y límites por rol

Evidencia vigente: `N:/LetMeSleep/Validation/Higgsfield/CompleteScope/MapBounds/water-footprints-v3.json`. **No usar v1 como polígonos de producción**: su contorno Water_Lake_Deep se autointersecta. No es redundante con Water_Lake principal (ambas superficies son bandas cóncavas). V3 deriva esa región por unión planar de todas las caras GLB, produciendo un exterior y hueco válidos. Los otros contornos válidos se conservan; las diferencias de tessellation/cuantización contra unión de triángulos no contienen un disco de20 micrómetros y su área queda documentada. No se usó convex hull ni se rellenó un hueco real.

Once regiones por rol: Isla2, Casa0, Camp6, Yate1, Puerto2. Dos huecos reales (Lake_Deep y casco Yate). Deep/Shallow de Camp no se eliminan porque algunos sobresalen de la capa principal. El pozo usa el disco superior Well_DeepWater de20v, conserva borde seco y cubre la adquisición de Plaza_ContinuousPaving SurfaceId1000246 medida por Gameplay bajo el agua.

El Director ejecutó el validador real RecoveryFallPrism offline: PASS11 regiones/2huecos, sin cambiar tolerancias; suite887aserciones, con el contorno antiguo rechazado como regresión. Su recibo es `N:/LetMeSleep/Validation/Higgsfield/CompleteScope/Recovery/checks-result-v3.txt`; esas pruebas usaron alturas sintéticas.

Política aprobada por Encargado: pies humanos baseY-.02; centro mosquito máximo de cresta+.055; minY=-10. Es explícita, sin natación/daño ni colisión artificial de agua. GPU: Yate.14→centro.195, Puerto.09→.145; pozo3.36→3.415. CPU: no asumir Amplitude=.025: el código mezcla dos morphs con pesos complementarios0..100; la cota conservadora es máximo de ambos targets GLB. Isla lago1.035/mar.070; Camp según capa, crestas aproximadamente-.060..-.100. `water-policy-proof.json` registra cada valor y SHA de fuente. El installer mide también los blendshapes IMPORTADOS y sus bindings antes de aplicar; aborta si difieren>1mm. No se afirma que datos GLB por sí solos prueben el comportamiento Unity.

## Contrato del installer

Entrada estricta `bounds-approved-preflight.json`, acción inicial `inspect`. Requiere integrar primero GameplayRecoveryVolume del Director y su fuente exacta por SHA. Lookup por tipo evita compilar contra un ensamblado central aún inexistente. Añade SafetyBounds, arrays de prismas con Outer/Holes.Vertices, arrays box vacíos y fallbacks vacíos para usar roster; RetryTicks30, InteriorMargin.02 y SupportProbe.08. Compila la misma clase interna Settings de BeginRound para validar polígonos y parámetros.

- Preflight de los cinco mapas antes de guardar: SHA de archivos/meta, GUID, ContentHash, PlayBounds anterior, dependencia completa de prefab, referencia espacial y catálogo. Rechaza escenas objetivo ya abiertas, prefabs sucios, contenedor/componente previo y raíces perchables. Cualquier guard cambiado requiere nueva captura; no se sobreescribe WIP.
- Comprueba nativamente cada MeshCollider protegido por nombre, dimensiones map-local y margen corporal, incluidos faro/tejados/puentes/botes. Audits son propuesta; si la envolvente importada excede la fuente o toca límite, aborta para revisar cotas. Valida los spawns por radio/altura de cada rol.
- Crea sólo `_LMS_ArtificialBoundaries_v1` con West/East/South/North/Ceiling BoxColliders estáticos, no trigger, Default, sin Renderer, Rigidbody ni GameplaySurface/ancestro. El componente de recuperación va directamente en MapRoot. La consulta TrySurface/motor aún exige pruebas nativas.
- Actualiza ContentHash derivado de hash anterior+revisión+dimensiones+política de recuperación, PlayBounds y prefab/instancia de su escena. Guarda sólo esos assets mediante Unity API; nunca SaveAssets global. Las referencias de catálogo se vuelven a comprobar y su archivo/GUID quedan intactos; cambia su dependencia de prefabs, no se finge reconstrucción del catálogo.
- Readback recarga prefab y escena, coteja cinco colliders y configuración Recovery. Fingerprint de propiedades serializadas de componentes y jerarquía protegida excluye sólo límites/ContentHash/PlayBounds/Recovery propios; referencias ajenas se conservan. Tipos serializados desconocidos abortan conservadoramente.
- Recibo externo nuevo incluye configuración completa, SHA, resultados y alcance. Repetición con el mismo recibo PASS/config/hash de activos verifica estado y devuelve sin guardar ni encadenar hashes. Recibo distinto/incompleto se rechaza.
- Ante error revierte modificaciones propias en orden inverso mediante Unity APIs y restaura overrides previos de escena. Compara SHA actual con la última escritura conocida antes del rollback: conflicto externo o save parcial de hash desconocido queda `FAILED_ROLLBACK_CONFLICT_REQUIRES_REVIEW`, no pisa archivos. Rollback es semántico, no promesa de bytes idénticos tras serialización Unity. Limpia sólo sus escenas/prefab previews y restaura escena activa. Se necesita comprobar rollback con fallos inyectados en slot autorizado antes de certificarlo.

## Estado y uso

Compilación offline contra Unity6000.3.24f1 y assemblies centrales PASS, cero errores/avisos (`Compile-Installer.ps1`). `verify_configuration.py` pasa22 casos/agrupaciones: topología, límites, casco, mar/cresta, pozo/borde, muelles/puentes, fondos sumergidos y105 posiciones spawn authored. Son pruebas de puntos y geometría, no recorridos físicos ni prueba de recuperación de estados autoritativos.

Los guardas de archivo/content de Camp ya corresponden al arreglo3mm: ContentHash3551537b2702090fb67e41e10ed8dccd9a3703dc33a6cfd8c01fa4fa8ce90f36. Gameplay confirmó la regresión persistida49/49 y dependencyHash final de Camp `f9ffa9495f47961bac47a4a3037ce383`; queda registrado con reporte/SHA en native-dependency-overrides.json. **Recapturar después cualquier guard que cambie por extensión visual Puerto o integración de scripts**. Los otros valores parten del recibo night-final y cualquier diferencia falla cerrada.

Regeneración offline de evidencia/config: `measure_envelopes.py`, `measure_water.py`, `build_approved_config.py`, `verify_configuration.py`. numpy existente; shapely2.1.2 aislado en N:/.../MapBounds/python-libs, sin instalar en el intérprete global. Ejecutar measure_envelopes recaptura archivos y aplica overrides nativos sólo si coinciden contentHash y hash del reporte de evidencia; el resto conserva dependencia base night-final. Actualizar con evidencia nativa antes de apply si cambia.

Invocación futura sólo en lease de Encargado: `LetMeSleep.Editor.HiggsfieldMapBoundaryInstaller.Run(configPath)` o -executeMethod RunFromCommandLine -higgsfieldBoundsConfig <config>. Primero inspect con recibo nuevo; luego configuración apply con recibo nuevo y guardas definitivas. Root no autorizó todavía aplicar central; esta entrega es herramienta+config+medición para revisión. No se abre otro Unity/Blender.
