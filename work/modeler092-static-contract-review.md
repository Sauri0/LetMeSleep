# Revisión estática de contratos 0.9.2

Estado: código preparado, **sin importación, ejecución ni pruebas aprobadas**.
Separado de la corrección validada de 0.9.1 (`9eb6f64` en `lms091-house`).

## Hallazgos corroborados y cambios

- Dormitorio familiar: el solver coloca camas antes de los otros esenciales y
  exige un pasillo recto de al menos 1.30 m entre sus caras, con solapamiento
  longitudinal de al menos la mitad de la menor huella. La distancia diagonal
  entre esquinas no cuenta como pasillo. Otros muebles no pueden ocuparlo;
  se reserva también antes de colocar soportes de herramientas.
- Mesa de luz: candidatos relativos a las camas colocadas, a 18 cm de su costado.
  El filtro acepta una distancia lateral entre 10 y 35 cm y solapamiento con
  el costado; considera la rotación de la cama. Conserva las comprobaciones
  generales de colisión, ventana, barrido y acceso.
- Ampliación tras revisión independiente: cabecera local -X del asset real hacia
  pared, separación máxima 20 cm; laterales de cama locales ±Z. Se contrastó
  `build_house.py:171-176` y la ausencia de giro adicional en `house_library.gd`.
  Director exige al menos una mesita por dormitorio/grupo, no una por cama.
  Se añade la pieza existente cuando el blueprint del dormitorio no la incluye;
  el grupo completo no puede aprobarse sin ella. Camas y mesita se colocan antes
  del resto de esenciales. Los estados parciales pueden esperar esa pieza;
  el resultado final debe satisfacer la relación completa.
- Tareas: el validador v3 comprueba ocho habitaciones distintas, al menos dos
  plantas y exterior para ventana/mosquitero. Revalida los ocho identificadores,
  camas reales y aproximación de la superficie asignada. Las cuatro tareas
  sobre mesa sólo consideran habitaciones con una superficie publicada.
- Pose de tareas: presencia, finitud e igualdad de `display_p/display_yaw` con
  la fuente única; posición sobre huella/top y referencia a mueble de apoyo real.
  Los fixtures positivos incluyen esos datos. Negativos cambian posición, yaw,
  finitud, soporte referenciado y una pose coincidente pero fuera del apoyo.

`modeler092_contract_test.gd` prepara fixtures positivos/negativos independientes
del éxito del solver: camas a 20 cm, separación diagonal, mueble en pasillo,
mesita lejana, rotaciones cardinales, habitaciones repetidas, una sola planta,
ventana interior y ausencia de cama/apoyo. Su comprobación de cocina-comedor
documenta el filtro de deduplicación que ya existía: siete piezas, una mesa
auxiliar, una superficie principal. No se cambió ese filtro.

QA retiró el hallazgo inicial de mesas duplicadas y acotó el de tareas: el
vínculo mantas/sábanas con cama real ya estaba validado. No se tratan como bugs
corregidos en esta entrega.

## Gate físico pendiente con Revisión funcional

QA preparará `review092_*` cuando Director integre v3 en su árbol. Para cada
aproximación verificará la metadata y caminará centro → anclaje → aproximación
y el camino inverso usando `Arena.step_human` a 30 Hz, con puertas abiertas.
Exigirá consumir todos los puntos, distancia final <=0.30 m, piso constante,
sin 90 ticks estancado y dentro de presupuesto derivado de longitud/velocidad.
El rayo inflado será diagnóstico, no un rechazo definitivo.

Negativos acordados: destino/intermedio dentro de mueble, anclaje corrupto,
zona inexistente y presupuesto insuficiente. No cubre red, predicción, cámara
ni la transición de apertura. Hasta ejecutar ese gate, los accesos funcionales
no tienen aceptación física. La prueba del generador con Geometry tampoco
sustituye esa aceptación.

Siguiente turno solicitado: importación del worktree v3, test de contratos,
estructura y primera semilla de mobiliario. El solver puede requerir ajustes
tras ese diagnóstico; no se afirma que esta revisión estática cierre 0.9.2.
