# Primer diagnóstico 0.9.2

**Estado actual:** semilla 1 aprobada en ejecución `20260912-010236`:
mobiliario **1324/0**, contratos **48/0**, estructura **139/0**; tres exit 0,
stderr vacío, 6.5 segundos total. Motor liberado. El historial siguiente conserva
los fallos anteriores, no describe el estado de esa semilla después del fix.

La primera representativa posterior, semilla 2 (`010337`), todavía falla en
cuartos 06/07/12 y soporte slipper. Se detuvo el corpus al primer fallo. No se
afirma aprobación de las once semillas ni del gate físico QA.

Fix coherente aprobado por seed1: esenciales/complementos separados según Director;
anclajes alejados de la hoja antes de ramificar; uso principal de biblioteca
central en la mitad despejada; cabecera real -X y acceso al pie +X; mesita ligada
a la cama; separación física de muebles y ancho de rutas conservados. La ruta
diagonal ahora comprueba el cuerpo barrido por segmento, sin bloquear esquinas
vacías de su AABB envolvente. Búsqueda acotada a 64 estados para no descartar
prematuramente combinaciones de esenciales. `--report` opcional conserva su
default y permite informes independientes por semilla.

Turno autorizado de 120 segundos; detenido y liberado después de 17.2 segundos.
No quedaron procesos Godot/Blender. Ejecución `20260912-002241`, Godot 4.5.2.

- Importación: exit 0, stderr vacío, 15.5 segundos.
- Contratos: **resultado inválido**, aunque el script imprimió `42/0` y exit 0.
  Hubo dos errores de ejecución `Array` → `Array[String]` en
  `ProceduralHouse._floor_rooms`, línea 213 anterior a la corrección.
  El ejecutor detectó `SCRIPT ERROR`, marcó fallo y detuvo la secuencia.
- Estructura y mobiliario de semilla 1: **no ejecutados**.

Corrección mínima preparada: inicializar el array tipado vacío y convertir
los elementos con `assign`, igual que el bloque contiguo del segundo piso.
**La corrección todavía no fue ejecutada**; requiere nueva señal de Director.

Evidencia: `modeler092-smoke-20260912-002241.json`, logs con ese mismo prefijo
y `modeler092-contract-results-attempt01.json` (salida nominal del test, inválida
por los errores indicados). `modeler092-smoke.ps1` conserva logs separados y
presupuesto global; corta ante timeout, exit no cero o errores de script,
aunque el test termine con exit 0.

Se restauraron únicamente los cambios de finales de línea de archivos `.import`
tras verificar que no había diferencias de contenido; se retiraron UIDs ajenos
que la importación acababa de generar. Se conservan los tres UIDs de tests propios.

## Segundo diagnóstico

Ejecución `20260912-003149`, sin import; liberada después de 4.6 segundos.
Contratos **44/0**, estructura **139/0**, ambos exit 0 y stderr vacío.
Mobiliario de semilla 1: exit 1, **1324 comprobaciones, 2 fallos**. Catorce de
22 habitaciones sin solución completa y cuatro nodos en la hoja abierta;
los faltantes de camas/herramientas/tareas son consecuencias. Sin SCRIPT ERROR.
Los JSON de los tres tests se conservaron con sufijo `-attempt02`.

Fix posterior preparado, **todavía sin ejecutar**: anclaje único alineado con
centro libre, corrección de anclajes compuestos sobre hoja abierta, candidatos
a 5 cm de la cara interior (la ducha no cabía con el margen anterior de 20 cm)
y distinción de esenciales/complementos autorizada por Director. Se mantienen
los filtros de colisión, barrido, acceso y mínimo de tres muebles reales.
Los informes de habitaciones ahora incluyen bounds, portal, anclajes y reservas
para diagnosticar cualquier fallo residual sin inferir posiciones.

## Tercer diagnóstico y reanudación

Ejecución `20260912-004030`, liberada en 2.3 s: mobiliario **1324/1**, exit 1.
Quedan siete habitaciones sin solución: 00 (cocina), 08/15 (familiares),
13/20 (dormitorios centrales), 14/21 (biblioteca/estudio centrales).
Servicios completos; desaparecieron los cuatro nodos en hojas y los faltantes
de herramientas/tareas. No se ejecutaron contratos/estructura después del fallo.
JSON preservado como `modeler092-furnishing-results-attempt03.json`.

Preparado tras reanudación, aún sin ejecutar: aproximación al pie real de cama
(+X rotado) para que la reserva de acceso no ocupe la franja de la mesita;
superficies primarias a 2 cm de pared útil para superar el contacto de pocos
milímetros en cuartos centrales. Continúan todas las comprobaciones de cuerpos,
barrido y bandas. El diagnóstico de fallo ahora incluye estado parcial y muestra
de candidatos para identificar cualquier conflicto residual de cocina/centrales.
