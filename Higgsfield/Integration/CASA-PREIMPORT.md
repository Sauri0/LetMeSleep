# Casa del Patio — descriptor pendiente

`casa.final-input.PENDING.json` prepara el ID `hf-casa-del-patio-v1`, capa Default, 5 humanos/16 mosquitos y cero mallas water/foam. **sourceFinal permanece false.** Apunta al nombre esperado de export limpio `HF_MAP_02_casa_UNITY.fbx`, al GLB y al futuro audit final. Ese nombre es una dependencia prevista, no una afirmación de existencia, integridad o importación correcta.

Sólo se leyeron los reportes `HF_MAP_02_casa_report.json/.txt` y el exportador central como código. No se leyó ni ejecutó el export activo, no se abrió Blender ni se llamó a su bridge y no se generó receta. Esperar confirmación del Encargado de GLB, scene-audit y FBX limpio final, con Scene Builder inactivo, antes de cambiar la confirmación del descriptor.

## Contrato comprobado en el reporte

El reporte declara escena `HF_MAP_02_casa`, 3751 objetos, 3664 mallas, 589 datablocks de malla únicos y 118152 triángulos. Los conteos están dentro de `checks`; el generador admite ese formato además del resumen plano anterior. Son declaraciones de autoría pendientes de cruzar con exports, no cifras impuestas en el generador.

La propiedad es seca, 45×40 m, casa de 14×10 m, suelos estructurales a Z Blender 0.25/3.15 m. Los tablones decorativos añaden hasta 0.024 m. El descriptor exige `expectedScene=HF_MAP_02_casa` y `expectedWaterCount=0`; falla si el audit corresponde a otra escena o aparecen reglas de agua/espuma. No oculta mallas para simular que la escena es seca.

Los aliases de reporte son:

- `Spawn_Human_01.001` a `Spawn_Human_05.001`, con logical_name sin `.001`.
- `Spawn_Mosquito_01.001` a `Spawn_Mosquito_16.001`, con logical_name sin `.001`.

Los prefijos actuales ya seleccionan esos nombres. El generador conserva el nombre físico y el path completos; **no elimina `.001`, no renombra objetos y no sustituye el nombre por logical_name**. Los aliases del reporte se guardan en `casa.report-contract.PENDING.json` para comparación futura. Las coordenadas definitivas vendrán de matrix_world del audit; las posiciones reportadas son referencia, no datos de receta. Los humanos tienen origen en pies/suelo; no sumar una altura de actor sin comprobar el contrato de spawn central.

## Materiales compartidos por instancia

El código central `maps_export_unity.py` copia temporalmente cada datablock de malla y asigna los materiales efectivos de cada objeto a slots DATA, exporta con Blender estándar y restaura los originales. Eso permite preservar diferencias por instancia sin alterar las fuentes. **No se ejecutó ese exportador desde esta tarea.** El generador examina conexiones Material→Model por objeto y primitivas de cada nodo GLB, no asume que una geometría compartida tenga los mismos colores en todas sus instancias.

La condición central de 8ee0229 se incorpora aquí: se aceptan slots idénticos a los autorados o la misma lista consolidando nombres repetidos. No se aceptan colores ausentes ni un color de otra instancia como equivalentes. Nunca se parchea el FBX binario para restaurar slots. Si el export comparte una malla y pierde materiales de una instancia, el cruce debe fallar y corresponde revisar el export limpio.

## Pendientes para importar

Verificar que GLB/FBX/audit contengan sólo Casa, pese a que el .blend conserva escenas protegidas anteriores. El contraste de nombres/paths, escena esperada y conteos del reporte detectará diferencias; no se recortará el export silenciosamente. Revisar los aliases físicos reales después de la exportación y la preservación de materiales por instancia.

Root conserva navegación/SpatialData, luces/cámaras, puerta interactiva y prueba de cápsulas. El reporte sólo ofrece líneas centrales libres para seis puertas; eso no prueba clearance de actor. Tejados removibles y puertas abiertas siguen siendo geometría estática hasta su integración. Los cristales son planos opacos estilizados y la iluminación requiere su tratamiento propio. El volumen de suelo/colisión y los miles de objetos necesitan validación nativa y medición; esta preparación no certifica rendimiento ni cupo real de partida.
