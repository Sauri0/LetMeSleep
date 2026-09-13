# Yate — copia ajustada entregada

La operación conjunta terminó con **PASS_EXCLUSIVE_YATE_COPY_STORAGE_AND_BULKHEAD_READBACK_LIVE_RESTORED**. Archivo: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/04-yate/UnityAdjustedSource/HF_MAP_04_yate_UNITY_ADJUSTED.blend`, 4.881.135 bytes, SHA256 `de6304396d5c95aab52003be5118058b15a7819e734694fb14f5d5fecaf8ae50`. El recibo vecino `adjustment-receipt.json` se conserva aquí como `delivery.json`.

Se comprobó el recibo nativo aplicado y leído nuevamente en prefab/escena: `applied-01/yate-semantic-apply.json`, SHA256 `a712d24a7f3a5e07848f5e0c9ac94edea09dd8819be369ecbd50feb67c5fef04`, ContentHash `dee1fea0f5f21429ed434668ee408a6ec3efd67984ad24e0640faa1177ca20d6`. `final-adjustment.json` fija ambos registros; el JSON pending anterior queda sólo como historial.

Se usó el MCP visible ya abierto, en el turno concedido por el coordinador, con Puerto activo, OBJECT, sin render y Scene Builder idle antes y después. El archivo escrito contiene únicamente la escena Yate y sus dependencias. La lectura independiente de los tres objetos guardados confirmó los desplazamientos de baúl/tapa y la geometría del mamparo. Todas las matrices, mallas y miembros de las escenas vivas se restauraron; los catorce archivos preservados conservan sus hashes. No hubo nuevos exports FBX/GLB ni importación Unity.

El mamparo ajustado tiene 16 vértices, diez polígonos y 28 triángulos; se verificaron dos caras por arista y volumen positivo igual al original menos la muesca. Los tres objetos fuente carecían de capas UV; la copia conserva esa ausencia. La rama genérica de interpolación UV del script no fue necesaria ni ejecutada. El SHA de malla Blender documenta su propia serialización; no se presenta como idéntico al hash de buffers Unity.

Los exports NormalsV3 siguen con el baúl y mamparo originales. La copia editable sincroniza las dos reparaciones, sin afirmar equivalencia portable completa. El índice queda pendiente de la regeneración final ordenada por el coordinador.

## Preparación anterior

Gameplay confirmó como candidato validado el desplazamiento de `YATE_DeckStorage_01` y `YATE_DeckStorage_Lid_01`: ΔUnity mundo [−1.4,0,+2] m, equivalente a ΔBlender [−1.4,+2,0] m. Ambos objetos tienen transformación original identidad y geometría baked. El recibo `storage-native-01/map-checks-20260913-082944-913.json`, bajo `N:/LetMeSleep/Validation/Higgsfield/YateV3-20260913`, conserva los mesh GUID/local IDs, materiales, cinco apoyos sobre teak, penetración conservadora cero y separación visual máxima 0.0002384 mm.

**El ajuste conjunto aún espera aplicación nativa final y sus hashes.** El coordinador también autorizó rebajar `YATE_Lower_EndBulkhead_-11.6` sólo dentro de Blender X [1.625,3.075] y Z > 2.70, manteniendo el espesor Y [−11.67,−11.53], resto de pared, UV exteriores y material. La copia debe incluir ambas correcciones. El coordinador prohibió abrir Blender hasta confirmar un turno. `pending-adjustment.json` permanece con `ready:false`; no se ejecutó el script ni se creó una copia Blender ajustada.

`save_adjusted_copy.py` prepara una operación limitada al MCP visible existente, cuando lleguen ambos requisitos: recibo final de aplicación nativa con hash y rutas exactas a los registros de ambos ajustes, y turno confirmado por el coordinador. No lanza procesos. Comprueba el SHA de la fuente NormalsV3, el audit de la escena viva, los 32.000 triángulos del océano hacia arriba y los objetos exactos. Mueve temporalmente baúl y tapa y asigna una malla nueva al mamparo, conservando su malla original intacta. La muesca utiliza un perfil de ocho puntos extruido: 16 vértices, diez polígonos, 28 triángulos, aristas compartidas por dos caras y volumen original menos el recorte. Interpola los UV sobre las caras exteriores originales; sólo las caras nuevas del corte reciben proyección XZ. Mantiene YATE_Cream.

Escribe sólo la escena Yate y dependencias mediante libraries.write, restaura matrices y malla vivas y lee los tres objetos guardados sin abrir ni vincular otra escena. Los hashes de geometría incluyen los UV. El script aborta si falta la evidencia final del mamparo; no permite producir una copia ajustada únicamente al baúl.

La salida prevista es `04-yate/UnityAdjustedSource/HF_MAP_04_yate_UNITY_ADJUSTED.blend`; aborta si la carpeta ya existe. Conserva NormalsV3, sus exports y el archivo activo Puerto por hash. No reexporta FBX/GLB ni reimporta Unity. Los exports seguirían con el baúl y mamparo originales hasta una acción posterior autorizada; la sincronización de estos dos ajustes no certifica equivalencia portable completa.

Antes de ejecutar debe completarse el JSON con el recibo realmente persistido y su estructura, actualizarse el alcance si Gameplay confirma otros deltas y revisarse nuevamente el estado vivo. La preparación se verificó mediante compilación sintáctica offline, sin importar bpy ni iniciar Blender; el comportamiento dependiente del MCP todavía no fue ejecutado.
