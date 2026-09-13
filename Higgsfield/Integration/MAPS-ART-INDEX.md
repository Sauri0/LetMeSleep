# Índice local de entrega de mapas

## Ampliación preparada, pendiente de regeneración coordinada

El generador ahora incorpora cinco capturas Unity adicionales: Isla/UnityFinal, Casa/UnityFinal, Campamento/UnityFinalV2, Yate/UnityFinalV3 y Puerto/UnityPresentationProvisional. Cada captura se etiqueta como Unity; Puerto mantiene su condición provisional. Quedan separados los diez renders de Blender y los veinte bocetos. La futura salida espera 35 imágenes.

Se registra el alcance de la revisión visual: ninguna evidencia P0/P1 identificada en esas cinco imágenes y dos P2 pendientes (agua radial del Yate y fondo de acantilado de Puerto), sin rediseño nuevo. La captura no certifica navegación, interiores ocultos o rendimiento.

Se añadieron argumentos opcionales `--catalog-receipt`, `--scene-receipt`, `--loading-receipt`. Sin argumentos, busca `UnityPackage/catalog-receipt.json`, `UnityPackage/scene-receipt.json` y `UnityPackage/GameLoading/five-map-game-loading.txt` dentro de Mapas. `integration_receipts.py` valida los esquemas reales de los builders: literal `success: true`, error ausente/nulo/vacío y guardas propias. El catálogo conserva `sceneInstalled: false` porque su alcance es sólo autoría; la escena exige originalPreserved, catalogPreserved y savedBindingVerified verdaderos. Ambos necesitan los cinco IDs finales únicos y hashes espaciales/contenido. El texto exige cabecera `PASS Unity <versión>`, diez líneas exactas (cinco IDs × Human/Mosquito) y la declaración original de alcance al final.

Sólo con los tres recibos válidos, la misma versión Unity, la referencia de catálogo coincidente (ruta/GUID/SHA) y hashes por mapa coincidentes entre catálogo/escena, el intro y cada `unityStatus` dicen **Integración local verificada**. De otro modo continúan en verificación. No se renombra ni reescribe el contenido crudo de los recibos. Su alcance nativo se conserva en el manifiesto; no se extiende a WAN, FPS o partida completa.

Los recibos externos exitosos se copian a UnityEvidence con hash en el nombre durante la regeneración, para conservar todos los enlaces dentro de Mapas. La selección de fuente Yate cambia a UnityAdjustedSource sólo cuando exista el recibo de copia con status exacto, los tres objetos (baúl, tapa y mamparo) y SHA del archivo verificados. Aclara que los exports NormalsV3 anteriores no incluyen esos dos ajustes.

La ampliación está verificada por sintaxis y seis pruebas con múltiples casos negativos de formatos nativos, guardas booleanas, JSON ambiguo, IDs finales, diez roles y cruces entre recibos (`python -B -m unittest discover -s Higgsfield/Integration -p test_integration_receipts.py -v`), sin escribir ni regenerar los artifacts de entrega. **Esperar la orden final del coordinador** para generar y actualizar los hashes y recuentos de abajo.

## Entrega emitida anteriormente

Entrada: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/ENTREGA.html`.
Inventario: `ENTREGA.manifest.json`; verificación: `ENTREGA.verification.json`, en la misma carpeta.

El índice contiene cinco mapas, cinco fuentes Blender, cinco GLB y cinco FBX limpios, diez renders de arte (general y detalle por mapa), y los veinte bocetos aprobados. Todos los enlaces son relativos; se debe trasladar la carpeta Mapas completa. Los cuatro bocetos por mapa se muestran en un desplegable. El duplicado `Bocetos/05-detail.png` está identificado y excluido del total.

Selección técnica: Campamento utiliza exports NormalsV2; Yate utiliza fuente y exports NormalsV3 con océano corregido y receta GPU v3. Casa utiliza la copia exclusiva UnityAdjustedSource; sus GLB/FBX originales conservan el marcador anterior y la página lo advierte expresamente. Los candidatos reemplazados aparecen sólo en el historial. Las imágenes son vistas de arte previas a los ajustes técnicos y no capturas de la integración Unity.

Estado visible: arte Higgsfield terminado; integración Unity en verificación. No se afirma equivalencia portable completa, rendimiento ni partida certificada.

`build_maps_delivery_index.py` genera exclusivamente HTML y JSON, comprueba los veinte hashes únicos de los bocetos contra su manifiesto original y verifica rutas contenidas en Mapas, existencia de todos los enlaces, dimensiones PNG, texto alternativo y SHA256 de todos los archivos registrados. También cruza el comprobante de Casa con la fuente ajustada y vuelve a verificar los hashes preservados. No modifica fuentes ni abre aplicaciones nativas.

Última validación: **PASS**, 61 archivos registrados, 93 enlaces href/src locales, 30 imágenes. SHA256 HTML `7c8f21d4945a346416f939395b4aaf43b244ca153d2da54298d7245d585f34ff`; manifiesto `2a569b05ad08a68c6c0a93beede0663acc0b58c2b8e34a5935c0323fa5132d33`. La comprobación es de contenido/enlaces; no se efectuó una nueva prueba visual en navegador. Al regenerar cambian las marcas de tiempo y los hashes HTML/JSON.
