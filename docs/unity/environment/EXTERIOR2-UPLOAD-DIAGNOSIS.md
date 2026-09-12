# EX2: diagnóstico de procedencia y prueba controlada pendiente

Evidencia nativa: central `6e712a4`, rebuild2026-09-12T21:19:11Z, PNG `N:/LetMeSleep/Validation/Alfa-VisualRecovery/exterior2-*`. Se abrieron window-front, patio-forward, patio-reverse y patio-mosquito-eye. El árbol de patio mezcla formas marrones/verdes de la versión anterior; plantas bajas se leen como líneas. La familia Wild de creación nueva sí muestra la copa continua. Es un patrón observado, no una causa demostrada.

## Lectura de archivos, sin modificar nativos

Se decodificaron los buffers de vértices e índices de los assets YAML centrales y se compararon con `generated_exterior.json`, SHA256 `b90b3d406aa02518cd219e8404e97072a5ddf5494dc3cdde669b49a76c190bcd`.

| Malla | Vértices | Índices | Error máximo de posición |
|---|---:|---|---:|
| Pine_00 | 1014 | Iguales al JSON | 1.1561e−7m |
| Pine_Wild_00 | 1014 | Iguales al JSON | 1.1561e−7m |
| Herb_00 | 372 | Iguales al JSON | 1.4599e−8m |
| Grass_00 | 210 | Iguales al JSON | 8.8788e−9m |

El prefab guardado contiene ocho renderers heredados de pinos desactivados: tronco y tres copas por cada árbol. Los dos reemplazos de QualityExterior están activos. No hay evidencia para retirar geometría duplicada. Las referencias de los reemplazos son:

- Oeste → `Exterior_Pine_00.asset`, GUID83f9555ae049199438d6cae14c7c393c.
- Este → `Exterior_Pine_01.asset`, GUID7825707585004ea43a5cb4d4176f6f75.
- Ambos tienen materiales en orden Bark, NeedleDeep, Needle, NeedleTip, BarkLight, coincidente con la receta actual.

Esto confirma la procedencia serializada. No prueba el contenido de la instancia residente ni el estado de sus buffers gráficos.

## Prueba externa para Director

`N:/LetMeSleep/Worktrees/maps/art_source/unity/environments/quality_exterior/ProbeExteriorUpload.cs` es un cuerpo de `eval_file`, fuera de Assets. Compilación offline del cuerpo envuelto contra Unity6000.3.24f1, Newtonsoft y la referencia real de AlfaReviewCapture: PASS. No se ejecutó nativamente desde Mapas.

Ejecutar en la misma instancia de la casa EX2 que Director mantiene después de su rebuild de sofá60754dc. El script exige exactamente una raíz activa QualityExterior y una cámara residente. Conserva sus IDs, coloca una sola vez la cámara patio-forward `(6.06,1.53,12.1)` → `(2,1.2,16.5)`, FOV65, y:

1. Calcula hashes CPU de vértices, normales, UV/UV2, tangentes, colores, topología/índices y bounds; hashes de archivos de malla.
2. Captura `before.png` a1920×1080.
3. Llama únicamente `UploadMeshData(false)` en las mallas legibles existentes bajo QualityExterior.
4. Comprueba mismos datos CPU/disco y cámara, captura `after.png` y escribe receipt con IDs, hashes y rutas.
5. Restaura la cámara en finally. No usa Clear, setters de malla, reconstrucción, guardado de escena/assets ni cambios de luz.

Salida única con timestamp: `N:/LetMeSleep/Validation/Alfa-VisualRecovery/exterior-upload-YYYYMMDD-HHmmss-fff/`. Incluye recibo JSON y error/estado parcial si falla. PNG con bytes diferentes no demuestra por sí solo la causa; comparar píxeles y lectura. Si no hay cambio, no atribuir el fallo a GPU: un reinicio o lectura de instancia posterior podrá aportar otra evidencia.

Se retiró el borrador no comprometido que proponía Clear/setters/Upload en MakeMesh al recibir esta instrucción del Director. El importador, geometría y editable permanecen sin cambios. No se modificó ni retiró ensamblado de Elementos, luces, colliders o assets nativos.

## Resultado controlado: Upload no cambia la imagen

Director ejecutó la prueba el2026-09-12T21:34:36Z. Evidencia preservada en `N:/LetMeSleep/Validation/Alfa-VisualRecovery/exterior-upload-20260912-213435-760/`. Receipt leído y hashes de ambos PNG comprobados desde Mapas:

- Misma raíz de escena ID−261054 y cámara ID−263848.
- 38 mallas legibles utilizadas; hashes CPU y disco sin cambios en las38.
- `before.png` y `after.png` tienen bytes idénticos, SHA256 `c792bbb29ac88ad2ef45b857a887ee1d5c7c3e9ce6a9a147c8c6448b7aec362f`.

**UploadMeshData(false) no corrigió la lectura rota.** Esta prueba no respalda aplicar el arreglo de buffers propuesto. No se atribuye la causa a GPU ni se aplica Clear/setters. Director prepara reinicio del Editor y captura de la misma receta para obtener la siguiente comparación. Geometría, importador y editable quedan intactos mientras tanto.

## Evidencia del reinicio y corrección autorizada posterior

Director reinició a PID32520, HEAD3dce610, sin nuevo rebuild/JSON. `exterior2-fresh-patio-forward.png` fue abierta desde Mapas: los diamantes marrones desaparecen y el pino de patio muestra la copa completa. Las hierbas siguen finas. Esto acredita una discrepancia de visualización entre sesiones con los assets persistidos, sin demostrar un mecanismo GPU específico ni eficacia de Upload por sí solo.

Con esta nueva evidencia, Director autorizó actualizar MakeMesh mediante API. La implementación conserva el asset/GUID y usa `UpdateMeshData`: Clear(false), asignación de vértices/índices/UV, normales y bounds recalculados, MarkModified y UploadMeshData(false). Verifica lectura de vuelta de todos los vértices e índices. La creación y SetDirty permanecen en MakeMesh; UpdateMeshData no guarda assets. No cambian JSON, formas de copas, materiales, colliders ni editable.

### Prueba de actualización repetida para Director

`art_source/unity/environments/quality_exterior/ProbeExteriorRepeatedUpdate.cs`, cuerpo eval_file compilado offline, utiliza el mismo método de producción sobre el mesh existente del pino oeste. Requiere el importador nuevo integrado. Cámara patio-forward fija; alterna EX1→EX2→EX1→EX2 en la misma instancia y GUID, captura baseline y cuatro imágenes, registra hashes CPU/PNG y comprueba que las repeticiones coincidan y estados distintos se vean diferentes. No guarda assets/escenas. Restaura EX2 y la cámara aun si falla.

`Pine_EX1_Diagnostic.json` es sólo un fixture histórico extraído de2f1631a, con submeshes ordenados según los materiales actuales para no cambiar el renderer durante la prueba. No entra en la receta de producción. Salida con timestamp `Validation/Alfa-VisualRecovery/exterior-repeat-*`. Verificación nativa de actualización repetida: pendiente; no se declara corregido el pipeline sólo por compilar.

### Hierbas: análisis separado de fuente

Sin reautoría: Herb_00 tiene hojas de180×94mm, espesor cero y caras opuestas con vértices independientes. Los triángulos no son degenerados y el asset serializado coincide con la receta. En las caras de hojas NeedleTip, la normal vertical absoluta media ponderada por área es0.7407; su superficie proyectada desde tres direcciones horizontales es aproximadamente33.7–35.5% de su área. Esa poca masa visible, el tamaño y la distancia pueden contribuir a la apariencia de alambre; no demuestran un fallo de winding ni explican por sí solos la captura. Próximo paso tras validar el pipeline: vista nativa cercana del mismo grupo desde frente y arriba, para decidir entre orientación, grosor y escala sin modificar copas o luz.
