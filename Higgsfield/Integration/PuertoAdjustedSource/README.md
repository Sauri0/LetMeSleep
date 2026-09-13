# Puerto: copia Blender ajustada final

Archivo: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/05-pueblo/UnityAdjustedSource/HF_MAP_05_pueblo_UNITY_ADJUSTED.blend`.
SHA256: `18466cf28d52587f5b246e95b95093557e5e310b6bfd6ee69df960760390fce7`.
Recibo vecino `adjustment-receipt.json`; copia exacta en `delivery.json`.

Guardada mediante la sesión visible Blender MCP autorizada por Encargado. Sólo contiene HF_MAP_05_pueblo y dependencias (811 objetos). Bisel de 30 mm horizontal/vertical en el canto superior frontal del segundo peldaño de Lighthouse_Approach_StoneStairway. Recorte por el plano del recibo nativo final APPLIED_READBACK_PASS, ContentHash `3be1751178d5df73b75b089c315e617b8935466bf7814866c502824db9787fa9`.

Se conservaron los 234 polígonos/468 triángulos fuera del peldaño, sus coordenadas y materiales. Sólo se recortaron seis quads y añadió cierre con PDF05_StoneLight. Malla resultante: 322 vértices, 241 polígonos, 484 triángulos. Unity tiene 492 triángulos por recorte de las diagonales originales; la superficie sigue el mismo plano final, sin afirmar triangulación idéntica ni equivalencia portable completa. Bordes con dos caras, volumen positivo, volumen retirado 0,001125007 m³ frente a 0,001125 m³ esperado y cierre a menos de 2 micrómetros del plano. No existían UVs; no se añadieron.

Readback del objeto guardado: hash de geometría y matriz coincidentes. Inventario de 811 objetos comprobado; archivo con una única escena Puerto. Todos los objetos, mallas y pertenencias de escenas de la sesión volvieron al estado original. Se verificaron SHA de los 34 archivos originales de Puerto. Blender quedó en HF_MAP_05_pueblo, archivo original, modo OBJECT, sin render, Scene Builder idle.

Los BLEND, FBX y GLB originales permanecen intactos. Los exports conservan el peldaño sin bisel; no se reexportó ni reimportó. El generador de entrega recomienda la copia ajustada cuando verifica su recibo y hash, conserva la fuente original en historial y explica la diferencia. No se regeneró ENTREGA: se espera la orden coordinada final.

`save_adjusted_copy.py` conserva el procedimiento ejecutado. Rechaza una carpeta de salida existente; no volver a ejecutar ni abrir Blender por CLI/headless. La regresión Unity (49/49, 57/57 portales, 16/16 runtime, pico 0,857829931 mm) pertenece al recibo de Gameplay, no a esta comprobación Blender.
