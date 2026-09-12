# Suite EditMode — MessageFraming

Estado: **pruebas preparadas; corrección de liberación global y Unity Test Runner pendientes**.

La suite cubre el framing puro de Online sin conectar a EOS:

- rechazo de payload nulo, vacío y mayor a 16 KiB;
- roundtrip en 1, 1160, 1161 y 16384 bytes, siempre dentro del MTU EOS de 1170 bytes;
- reensamblado fuera de orden, aislamiento por miembro, fragmento duplicado y metadatos en conflicto;
- `ArraySegment` con offset real y rechazo de magia, conteo, índice, total y tamaño inconsistentes;
- liberación del presupuesto global de miembros tras completar o expirar ensamblados.

Los dos últimos casos de presupuesto son regresiones del hallazgo QA que dejaba claves vacías/expiradas en el diccionario global. La suite debe ejecutarse después de integrar la corrección en `MessageFraming`.
