# Índice local de entrega de mapas

Entrada: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/ENTREGA.html`.
Inventario: `ENTREGA.manifest.json`; verificación: `ENTREGA.verification.json`, en la misma carpeta.

El índice contiene cinco mapas, cinco fuentes Blender, cinco GLB y cinco FBX limpios, diez renders de arte (general y detalle por mapa), y los veinte bocetos aprobados. Todos los enlaces son relativos; se debe trasladar la carpeta Mapas completa. Los cuatro bocetos por mapa se muestran en un desplegable. El duplicado `Bocetos/05-detail.png` está identificado y excluido del total.

Selección técnica: Campamento utiliza exports NormalsV2; Yate utiliza fuente y exports NormalsV3 con océano corregido y receta GPU v3. Casa utiliza la copia exclusiva UnityAdjustedSource; sus GLB/FBX originales conservan el marcador anterior y la página lo advierte expresamente. Los candidatos reemplazados aparecen sólo en el historial. Las imágenes son vistas de arte previas a los ajustes técnicos y no capturas de la integración Unity.

Estado visible: arte Higgsfield terminado; integración Unity en verificación. No se afirma equivalencia portable completa, rendimiento ni partida certificada.

`build_maps_delivery_index.py` genera exclusivamente HTML y JSON, comprueba los veinte hashes únicos de los bocetos contra su manifiesto original y verifica rutas contenidas en Mapas, existencia de todos los enlaces, dimensiones PNG, texto alternativo y SHA256 de todos los archivos registrados. También cruza el comprobante de Casa con la fuente ajustada y vuelve a verificar los hashes preservados. No modifica fuentes ni abre aplicaciones nativas.

Última validación: **PASS**, 61 archivos registrados, 93 enlaces href/src locales, 30 imágenes. SHA256 HTML `7c8f21d4945a346416f939395b4aaf43b244ca153d2da54298d7245d585f34ff`; manifiesto `2a569b05ad08a68c6c0a93beede0663acc0b58c2b8e34a5935c0323fa5132d33`. La comprobación es de contenido/enlaces; no se efectuó una nueva prueba visual en navegador. Al regenerar cambian las marcas de tiempo y los hashes HTML/JSON.
