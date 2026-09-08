# Propuesta focal: apoyo de anteojos humanos A2

Estado: propuesta offline; no se modificaron modelos, fuentes de producción, metadatos ni runtime. No se ejecutó motor. GLB de referencia: `90cd6a695b32c3b98395df8f8da496ee56ffebd6026db757c03f9006eb777a06`.

El diagnóstico externo confirma un aro completo enterrado en la nariz (testigo37 mm) y patillas que terminan al menos14,61 mm antes de la superficie de la cabeza. Son problemas del accesorio compartido; la revisión de láminas012–023 a escala reducida no constituye prueba de ese apoyo y no los da por resueltos.

## Forma mínima propuesta

1. Conservar el contorno frontal de ambos aros, separación entre ojos, color, grosor actual y unión al hueso head. Corregir sólo la profundidad del arco medial inferior que atraviesa la nariz: recorrido continuo alrededor de la superficie nasal real, con transición suave hacia las porciones lateral/superior actuales. No abrir el aro ni trasladar todo el accesorio hacia delante. El testigo requiere aproximadamente38–39 mm de avance local del vértice para pasar de37 mm de enterramiento a1–2 mm de margen; el desplazamiento final se resolverá contra triángulos reales, no con ese valor uniforme. Validar puente existente antes de conservarlo: si toca la nariz, conformar sólo su tramo afectado.
2. Completar cada patilla con una curva lateral y un pequeño gancho descendente detrás de la parte superior de la oreja. La anatomía fuente sitúa el techo aproximado de la oreja A cerca de(x=±0,183; y=1,596; z=−0,004); no usarlo como punto de contacto definitivo. Trazar el soporte contra los triángulos actuales, reducir el extremo alto/adelantado y llevar la sección exterior del tubo a una separación0–1 mm de una zona explícita de oreja. Mantener fuera de la cabeza el resto de la patilla y comprobar los tres peinados. Los anclajes deben ser simétricos por intención; aceptar pequeñas diferencias si la geometría real lo exige.
3. Usar subdivisión local suficiente para conservar la curva en los interiores de triángulo, conservando sección aproximadamente6 mm de aro y4 mm de patilla. El objetivo es encaje legible y continuo, sin agregar adornos ni cambiar el estilo. Todos los nuevos vértices mantienen peso1 en head, sin nuevos huesos o controles.
4. Implementación eventual: helper humano separado llamado por el pipeline seleccionado, más wrapper incremental desde el blend final preservado. Sustituir únicamente human_accessory_2 y sincronizar blend/GLB/contadores. Resumen+SHA en model/manifest, detalle geométrico en informe externo; evitar repetir el bloque grande que dejó la reparación del ribete.

## Comprobación necesaria antes de aceptar

- Guardar before real del GLB/blend/metadata y testigos H-H0162/H-H0199; mismas cámaras y luz para después, ocho vistas y acercamiento de nariz/oreja.
- Gate específico de aro cerrado: continuidad de sus segmentos y cero penetración nasal sobre vértices, aristas e interiores de triángulos. Distinguir un borde realmente oculto dentro de la nariz de la oclusión normal por perspectiva. Mirar frontal y ambas oblicuas, no sólo distancia de soporte.
- Gate de patillas: distancia firmada y continuidad hasta un apoyo regional de cada oreja. No permitir separación14,61 mm ni declarar apoyo por simple proximidad del centro. Margen propuesto0–1 mm en el pequeño soporte, sin cruces del tubo fuera de él; umbrales y región registrados antes del pase.
- Corregir el clasificador del gate para A2: contacto con head no puede ser automáticamente admisible, y A2 no es una raíz de gorro. Sólo el apoyo de oreja/puente explícitamente definido puede tener tolerancia de unión. A1/A3 conservarían su política de gorro.
- Comprobar A2 contra cabeza, tres ojos/cejas/bocas, tres peinados compatibles sin gorro, ambos bigotes/barbas y estados faciales reales (neutral, cierre/mitad, cejas, gaze y apertura/sonrisa extremos mediante helper común). El accesorio es rígido al mismo head: yaw/pitch no cambian esos pares internos; un control nativo debe demostrar igualdad de transformaciones antes de reutilizar esa independencia.
- Invariancia completa de las otras mallas: posiciones, normales, UV, índices, morphs, pesos, materiales, jerarquía, binds y driver. Los285 casos de prenda/antenas no incluyen anteojos: pueden mantenerse por identidad, con hashes de antes/después y sin presentarlos como validación del accesorio reparado.
- Control nativo de LOD/import y renders: los aros deben dibujarse completos y los extremos llegar a las orejas; no basta certificar la malla base si el render usa otro índice.

## Evidencia afectada

Cambiar A2 afecta729 de2916 cabezas humanas (5832 vistas). Con el layout actual, toca41 páginas y hasta164PNG; las páginas están enumeradas en el JSON compañero. No implica recapturar automáticamente las2187 cabezas sin A2 ni mosquito: su reutilización requiere prueba de identidad y un registro explícito de dependencia, porque el SHA global del GLB cambia. Las láminas actuales permanecen históricas con su SHA.

La evidencia focal del ribete/ropa y las285 poses conserva alcance si se prueba invariancia de todas sus dependencias. Las tablas faciales de autoría y registros de galería que referencian el GLB completo deben añadir el nuevo SHA y distinguir datos reutilizados de ejecución nueva. Los testigos A2 sí necesitan después real. La galería neutral no sustituye ese gate de apoyo ni el de morphs.

No se propone reabrir pelo, mejillas, ojos, barba, rig, cámara, físicas o personalización. No hay parche aplicado pendiente de aprobación: ésta es la propuesta concreta para coordinar la siguiente ventana.
