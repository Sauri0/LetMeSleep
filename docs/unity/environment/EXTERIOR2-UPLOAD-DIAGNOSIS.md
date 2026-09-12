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
