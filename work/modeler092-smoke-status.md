# Primer diagnóstico 0.9.2

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
