# Índice local de entrega de mapas — final emitido

El coordinador confirmó las capturas nocturnas finales y autorizó la regeneración. Entrada: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/ENTREGA.html`. Manifiesto y verificación vecinos `ENTREGA.manifest.json`, `ENTREGA.verification.json`.

Estado en introducción y cinco mapas: **Integración local verificada**. Alcance: catálogo final y enlace guardado de escena comprobados tras el ajuste nocturno, más diez sesiones locales por la API de la aplicación (cinco IDs × Human/Mosquito) con regreso al menú. No se afirma WAN, FPS, partida completa ni equivalencia portable completa.

Incluye cinco fuentes Blender (Casa, Yate y Puerto usan UnityAdjustedSource), cinco GLB, cinco FBX limpios, diez vistas Blender, cinco capturas Unity y veinte bocetos únicos. Casa/Campamento muestran Human de GameLoadingFinal, aprobadas por el coordinador junto con sus versiones Mosquito; Isla/Yate/Puerto conservan las vistas anteriores identificadas. Puerto mantiene la etiqueta de presentación provisional de su captura. Las notas de las fuentes ajustadas explican que GLB/FBX conservan los elementos originales.

Recibos finales enlazados sin reescribir bytes:

- `UnityPackage/catalog-receipt-night-final.json`, SHA256 `34f8cdf203dbe052a2278fb0060370ba17f50236cdf095f6101db139f8a78abe`.
- `UnityPackage/scene-receipt-night-final.json`, SHA256 `901c97e44543e93a3b59537f833209cdc930a31aa39b84a775f68840ab9f99b8`.
- `UnityPackage/GameLoadingFinal/five-map-game-loading.txt`, SHA256 `f457a736d60c8f9a71ef2d7f104bb667bca923399ebb13bbeed4a54c894f08c6`.

Los dos JSON se etiquetan **validación postajuste**. `integration_receipts.py` reconoce por separado los esquemas reales de creación y de inspección (`HiggsfieldNightCorrection.cs`). En inspección exige inspectionOnly literal true, scope nativo exacto, cinco IDs finales, guardas estructurales del catálogo y sceneUnchanged/savedBindingVerified de escena. Verifica hashes de prefab, dependencias, skybox, contenido y datos espaciales; los cruza entre ambos recibos junto con configSha256 y la referencia de catálogo. No inventa guardas propias de creación que el esquema de inspección no emite. Rechaza errores, booleanos fingidos, IDs duplicados/anteriores y JSON ambiguo.

La carga exige `PASS Unity <versión>`, diez líneas exactas mapa/rol PASS y el scope original final. Sólo con los tres recibos válidos y concordantes publica el estado verificado. Nueve pruebas con casos negativos PASS: `python -B -m unittest discover -s Higgsfield/Integration -p test_integration_receipts.py -v`.

Comando final ejecutado:

```powershell
python -B Higgsfield/Integration/build_maps_delivery_index.py --catalog-receipt N:/LetMeSleep/Artifacts/Higgsfield/Mapas/UnityPackage/catalog-receipt-night-final.json --scene-receipt N:/LetMeSleep/Artifacts/Higgsfield/Mapas/UnityPackage/scene-receipt-night-final.json --loading-receipt N:/LetMeSleep/Artifacts/Higgsfield/Mapas/UnityPackage/GameLoadingFinal/five-map-game-loading.txt
```

Validación final **PASS_LOCAL_RELATIVE_LINKS_AND_FILE_HASHES**: **73 archivos, 110 href/src locales, 35 imágenes**. Todos los enlaces están dentro de Mapas; veinte hashes de bocetos únicos, fuentes ajustadas recomendadas, tres recibos finales sin rechazos y seis apariciones del estado verificado comprobadas. Los bytes de los recibos originales permanecieron intactos. Sin nuevas imágenes, render, importación ni cambios a fuentes. No se ejecutó una nueva inspección del HTML en navegador; se verificaron contenido, rutas y hashes.

HTML SHA256: `1189557e99069c0e7864c5f17475d37f2883577af16f0e778124fa8e5a5f08f5`.
Manifiesto SHA256: `83d1735fbbbd855f4c6edf1fc3d870aacce631c9742cc6d2909182cda3e12347`.
Copia exacta de la verificación emitida: `MAPS-ART-INDEX.verification.json`.

## Entrega emitida anteriormente

Entrada: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/ENTREGA.html`.
Inventario: `ENTREGA.manifest.json`; verificación: `ENTREGA.verification.json`, en la misma carpeta.

El índice contiene cinco mapas, cinco fuentes Blender, cinco GLB y cinco FBX limpios, diez renders de arte (general y detalle por mapa), y los veinte bocetos aprobados. Todos los enlaces son relativos; se debe trasladar la carpeta Mapas completa. Los cuatro bocetos por mapa se muestran en un desplegable. El duplicado `Bocetos/05-detail.png` está identificado y excluido del total.

Selección técnica: Campamento utiliza exports NormalsV2; Yate utiliza fuente y exports NormalsV3 con océano corregido y receta GPU v3. Casa utiliza la copia exclusiva UnityAdjustedSource; sus GLB/FBX originales conservan el marcador anterior y la página lo advierte expresamente. Los candidatos reemplazados aparecen sólo en el historial. Las imágenes son vistas de arte previas a los ajustes técnicos y no capturas de la integración Unity.

Estado visible: arte Higgsfield terminado; integración Unity en verificación. No se afirma equivalencia portable completa, rendimiento ni partida certificada.

`build_maps_delivery_index.py` genera exclusivamente HTML y JSON, comprueba los veinte hashes únicos de los bocetos contra su manifiesto original y verifica rutas contenidas en Mapas, existencia de todos los enlaces, dimensiones PNG, texto alternativo y SHA256 de todos los archivos registrados. También cruza el comprobante de Casa con la fuente ajustada y vuelve a verificar los hashes preservados. No modifica fuentes ni abre aplicaciones nativas.

Última validación: **PASS**, 61 archivos registrados, 93 enlaces href/src locales, 30 imágenes. SHA256 HTML `7c8f21d4945a346416f939395b4aaf43b244ca153d2da54298d7245d585f34ff`; manifiesto `2a569b05ad08a68c6c0a93beede0663acc0b58c2b8e34a5935c0323fa5132d33`. La comprobación es de contenido/enlaces; no se efectuó una nueva prueba visual en navegador. Al regenerar cambian las marcas de tiempo y los hashes HTML/JSON.
