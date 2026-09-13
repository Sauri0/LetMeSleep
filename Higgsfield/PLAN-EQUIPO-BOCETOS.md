# Plan de equipo: Higgsfield → Blender → Unity

**Corrección posterior del usuario, de máxima prioridad:** crear los recursos nuevos
desde cero directamente desde los bocetos, sin usar la estética, mallas o modelos
actuales como base. Las menciones inferiores a adaptar/reutilizar fuentes del alfa
quedan sustituidas por creación nueva; la reutilización de variantes aplica a la
NUEVA base aprobada. Los contratos de integración Unity se estudian por separado y
pueden migrarse explícitamente. El modelo anterior no es referencia de Higgsfield.
Ver HUM-DES-001 y la corrección en HUMANOS-INICIO.md.

**Estado: plan aceptado como base por el usuario. Coordinación de la próxima actualización asignada a Encargado de Higgfield. Entrega del Director recibida. Preparación secuencial: Humanos primero; ninguna producción iniciada.**
Se revisaron 22 archivos de bocetos, con 21 imágenes únicas. El lobby transitable está duplicado exactamente entre las carpetas de mapas y UI. Los originales se mantienen intactos. Esta planificación no encargó generaciones ni consumió créditos.

El plan se basa en los bocetos y la distribución de tareas suministrada. No es una auditoría nueva de las fuentes de cada modelador ni una confirmación de que el alfa ya esté cerrado. El inventario de esas fuentes es el primer paso de ejecución.

## 1. Dirección visual y alcance

La referencia principal propuesta es **UI-06**, la lámina “Let Me Sleep” de las 18:01:41: dormitorio nocturno, humano con pijama, ojos expresivos, mosquito facetado, luz cálida interior y azules de noche; menú, salas, entrenamiento, personalización, HUD, pausa, resultados y conexión.

Las láminas “Bite & Build” sirven de biblioteca de formas, componentes, objetos y ambientes. No se toma su título como cambio de nombre. Sus armas, construcción, pesca, clases, misiones, progresión, supervivencia y cantidades de jugadores no se convierten automáticamente en requisitos. Lo mismo aplica al modo “Sangre” y a los números de la lámina final: deben corresponder al diseño funcional vigente.

### Reglas comunes propuestas

- Geometría low-poly facetada, siluetas claras, ojos blancos grandes y pupilas legibles; evitar realismo de piel, ruido de textura y detalles microscópicos.
- Madera cálida, metales simples, vegetación por masas y colores por bloques. La iluminación aporta profundidad; las texturas base no deben traer sombras direccionales pintadas que contradigan la escena.
- Identidad doméstica nocturna como primer objetivo. Bosque, lago, muelle y pueblo quedan disponibles para ampliar el escenario cuando corresponda.
- UI: paneles azul oscuro, bordes y selección celestes, rojo para mosquito/alerta y verde para confirmar. Acompañar colores con texto/iconos y estados reconocibles.
- Personalización por piezas y materiales compatibles. Una variación de color no justifica otro personaje completo.
- Mantener los contratos funcionales útiles del alfa —rig, sockets, escala de gameplay, identificadores— mientras se adapta el aspecto a los bocetos. Si un contrato impide lograr el diseño, documentar la migración antes de cambiarlo.

### Contradicciones que deben resolverse antes de gastar en esas piezas

| Evidencia | Tratamiento propuesto |
|---|---|
| UI-06 dice Let Me Sleep; muchas otras láminas dicen Bite & Build | Usar Let Me Sleep; conservar de las anteriores su vocabulario visual. |
| Humanos de exploración con botas frente al pijama/pantuflas de UI-06 | Pijama como primer conjunto; otras prendas como catálogo posterior, reutilizando cuerpo/rig cuando sea viable. |
| Mosquitos de tamaños muy distintos entre láminas | Fijar escala junto al humano y cámara en una escena de prueba; no importar automáticamente el tamaño gigante de una ilustración. |
| Patas/alas inconsistentes y un rostro humano erróneo en PER-03 | Normalizar una anatomía de producción. Propuesta: seis patas y un par de alas; cualquier estilización distinta debe quedar explícita en la ficha maestra. Descartar el rostro humano. |
| Colas “venenosas”, nidos y accesorios de otras criaturas | Referencias fantásticas opcionales; no implican habilidades, biología ni sistemas nuevos. |
| HUD con corazones, otro con barras; salas 3v3, 5 por lado, 6v6 o 6 totales | UI-06 orienta la composición; Gameplay/Online suministran el significado y los valores reales. |
| Primera persona en gameplay y tercera en lobby/preview | Cada cámara requiere validación propia. No se cambia la cámara del juego por copiar una composición. |

Estas son decisiones propuestas para la próxima actualización, no cambios aplicados.

## 2. Responsabilidad de las 14 tareas

Cada recurso tiene **un solo propietario de producción**. Una lámina puede ser consultada por varios departamentos; eso no les da propiedad compartida de la misma malla.

| Tarea del equipo | Responsabilidad y referencias | Uso de Higgsfield | Entrega / límite |
|---|---|---|---|
| Director | Completar el cierre actual y entregar estado, fuentes y versión recuperable al Encargado. | Recibe el plan y la evidencia técnica. | Cede la coordinación durante esta actualización; retoma el mando después de su finalización verificada y del informe de devolución. |
| Worker Online | Salas, jugadores, equipos, ready, conexión y sincronización de personalización. UI-04, UI-06, ENV-05. | Sin generación por defecto; consume previews y recursos aprobados. | Contratos de datos y estados para UI; sincroniza IDs de cosméticos, no crea arte ni mallas. |
| Worker Código / Gameplay | Interacciones, equipamiento, cámaras, colisiones funcionales y reglas existentes. UI-06, ENV-03/04. | Sin generación por defecto. | Define agarres, puntos de interacción y requisitos; no rediseña personajes u objetos. |
| Worker Presentación y Audio | Sonidos, mezcla, transiciones, ambientación audiovisual y capturas. UI-06, ENV-03/05. | Sólo un recurso audiovisual concreto que falte y tenga costo validado. Video para presentación cuando corresponda. | Audio editable y ajustes de mezcla/VFX; captura el juego real para promoción. No crea nuevos personajes, mapa ni controles UI. |
| Worker UI | Menús, HUD, personalización, lobby visual, ajustes, resultados y errores. UI-06 principal; UI-01/05 y PER-08 auxiliares. | Ilustraciones o iconos especiales faltantes; se priorizan renders de assets reales y componentes hechos localmente. | Interfaz funcional Unity con textos/controles independientes. No convierte un screenshot generado en toda la interfaz. |
| Modelador Humanos | Cuerpo, cabeza, ojos, pelo, pijama, ropa, calzado y accesorios que se visten; rig y animaciones humanas de su fuente. PER-01/02/04/06 y UI-06. | Referencia aislada → posible base 3D; después reparación, modularidad y adaptación local. | Fuente Blender, mallas/piezas, materiales, rig compatible, sockets y pruebas de deformación. No produce herramientas ajenas. |
| Modelador Mosquitos | Cuerpo, ojos, probóscide, patas, alas y cosméticos montados en la criatura; rig y movimiento. PER-01/03/05/07 y UI-06. | Candidato para volumen principal si aporta valor; patas/alas/ojos y variantes preferentemente locales. | Anatomía consistente, partes separadas y animables, materiales de alas y poses. No usa autorig humano como solución al insecto. |
| Modelador Terreno y Mapas | Suelo, casa, arquitectura modular, exteriores, vegetación de entorno, roca natural y composición del nivel/lobby. ENV-01 a 05 y UI-06. | Sólo módulos o formas distintivas justificadas; diseño de conjunto desde referencias. | Escena armada con módulos reutilizables, dimensiones, colisiones arquitectónicas y rutas comprobables. No genera una imagen del mapa esperando obtener un nivel jugable. |
| Modelador Elementos | Muebles, objetos de mano, utilería, contenedores, defensas y objetos interactivos de ambas facciones. PRP-01/02, ENV-01/03, UI-06. | Mejor ámbito para pruebas de objeto aislado a 3D; formas simples y variantes se hacen localmente. | Un prefab por objeto lógico, partes móviles separadas, pivotes/sockets y estados visuales. No construye cuerpo ni arquitectura completa. |
| Revisor Estabilidad | Rendimiento, memoria, carga, shaders, errores e importaciones. | Sin presupuesto generativo. | Métricas antes/después y fallos reproducibles; devuelve la corrección al propietario. |
| Revisar diseño visual | Fidelidad a bocetos, estilo, escala visual, paleta y consistencia entre áreas. | Compara las referencias y renders existentes; no genera reemplazos por su cuenta. | Aceptación o observaciones por ID de recurso; no altera fuentes ajenas. |
| Revisar animaciones | Deformación, contacto, agarres, alas/patas, transiciones y comportamiento de accesorios. | Sin generación por defecto; propone una necesidad al modelador responsable. | Clips/poses de prueba y defectos concretos. Humanos/Mosquitos corrigen; Gameplay conecta estados. |
| Revisor Funcional | Flujo menú→sala→partida→resultados, controles, uso de objetos y persistencia. | Sin presupuesto generativo. | Pruebas contra las funciones aprobadas; distingue arte correcto de interacción correcta. |
| Encargado de Higgfield | Dirección temporal de esta actualización: prioridades, asignaciones, dependencias, integración, revisiones y cierre; además referencias, costos, cuotas y turnos. | Verifica modelo/parámetros/costo y organiza ejecución; no absorbe la autoría de cada departamento. | Presupuesto único y trazabilidad. Coordina directamente a las tareas existentes y devuelve el mando al Director con resultados verificados. |

## 3. Resolver cruces antes de crear duplicados

| Recurso compartido | Dueño de la fuente | Cómo lo consumen las otras áreas |
|---|---|---|
| Linterna, spray, matamoscas, botiquín, herramienta | Elementos | Humanos adapta mano/pose y socket; Gameplay implementa uso; UI renderiza ese mismo recurso. |
| Mochila, gorro, guantes, gafas y ropa humana | Humanos | Elementos instancia la versión acordada si aparece apoyada en el mundo; no genera otra mochila. Si requiere una variante rígida, se registra como derivada. |
| Alas, ojos, bandas y armadura del mosquito | Mosquitos | UI usa renders/previews del recurso; Elementos no duplica estas piezas aunque figuren en PRP-01. |
| Cama, armario, lámpara, mesa, taza y cuadro | Elementos | Mapas coloca instancias; Gameplay conecta abrir/usar cuando exista esa función. |
| Pared, suelo, techo, puerta, ventana, escalera, puente y valla | Terreno y Mapas | Gameplay conecta interacción y verifica paso; Elementos no vuelve a crear el kit arquitectónico. |
| Árbol, roca, césped, arbusto y vegetación ambiental | Terreno y Mapas | Elementos sólo crea variantes registradas si pasan a ser objetos de inventario/interacción distintos. |
| Flor de néctar interactiva, nido, trampa o refugio pequeño de facción | Elementos | Mapas coloca y Mosquitos valida escala de uso. Su presencia en el catálogo no activa su mecánica. |
| Fogata | Elementos: base de piedras/leña | Presentación y Audio: llama/sonido; Mapas: posición; Gameplay: efectos funcionales si existen. |
| Lobby transitable | Terreno y Mapas: escena; UI: paneles | Online: jugadores/ready; Humanos/Mosquitos: avatares. Ninguno es dueño de todo el lobby. |
| Retratos, miniaturas de accesorios y mapas | UI: captura final con encuadre común | Renderiza versiones publicadas por sus propietarios; no regenera una interpretación parecida. |
| Fondo del menú | UI: integración gráfica; Presentación: composición/captura | Reutilizan el dormitorio o escenario de Mapas y modelos existentes. Una ilustración independiente lleva ticket propio y acuerdo de uso. |

## 4. Cómo trabajar con Higgsfield sin perder coherencia

### Preparación común, sin generar

1. Definir cada recurso nuevo desde los bocetos, con una referencia principal y límites de interpretación. Las fuentes viejas permanecen como antecedente técnico separado, no como entrada artística.
2. Por recurso nuevo decidir qué generar con Higgsfield y qué construir desde cero localmente. Los bocetos ya resuelven buena parte del concepto; no pagar por recrear cada lámina. Reutilizar sólo piezas de la nueva biblioteca una vez aprobadas.
3. Preparar una ficha con referencia principal, vista seleccionada, proporciones, partes móviles, destino, tamaño y presupuesto. Las láminas enteras con decenas de objetos no son entradas adecuadas para reconstruir un único asset.
4. Cuando haga falta una referencia aislada, extraer la pieza sin alterar el original, o producir vistas consistentes. No combinar frente de un diseño con espalda de otro.
5. Validar el orden y requisitos del modelo con el catálogo. En las configuraciones auditadas, Hunyuan usa frente/espalda/izquierda/derecha y Tripo frente/izquierda/espalda/derecha: no intercambiar el orden.
6. Preferir imagen de referencia para fidelidad al boceto. Text-to-3D queda para una forma exploratoria que no tenga referencia suficiente.

### Candidatos, no promesas de calidad

Cotizaciones de la auditoría previa; deben repetirse con los parámetros reales antes de cada trabajo. Todavía no hay una comparación de calidad obtenida mediante generaciones del proyecto.

| Necesidad | Candidato auditado | Estimación previa |
|---|---|---:|
| Objeto aislado / varias vistas coherentes | Tripo H3.1 Image / Multiview, estándar texturizado | 9 créditos |
| Alternativa de objeto desde imagen | Hunyuan3D v3 Image, configuración auditada | 11 créditos |
| Forma exploratoria desde texto | Tripo estándar / Hunyuan v3.1 standard sin PBR | 5 / 7 créditos |
| Alternativa cuando el primer método falle por una causa identificada | Meshy 7, geometría / textura sin rig | 25 / 38 créditos |
| Personaje humano con prueba de autorig justificada | Meshy 7 textura+rig+animación, configuración auditada | 47,5 créditos |
| Retopología o textura generativa excepcional | Meshy Remesh / Retexture | 6 / 9,5 créditos |

No usar automáticamente calidad máxima, PBR complejo, remesh pago o autorig. El low-poly del juego puede requerir reconstrucción deliberada aunque la salida parezca atractiva. Hunyuan se cotizó con 500.000 caras: no es un presupuesto aceptado para Unity.

Kimodo y 3D Body no reemplazan un personaje completo. El precio base de Kimodo no cotiza un clip final; un video de Seedance/Kling tampoco entrega huesos animados. Se estudian sólo si existe un problema de animación específico.

### Después de generar

El propietario inspecciona silueta y correspondencia antes de añadir gastos. Corrige escala, topología, UV cuando proceda, materiales, pivotes, partes, pesos y animación en Blender. Las variaciones salen de esa base aceptada.

Un resultado se acepta por cómo funciona y se ve en Unity, no por la miniatura del generador. Si dos intentos fallan para el mismo recurso, se detiene la repetición y se decide reparación local, referencia mejor o alternativa justificada. Cambiar de modelo no reinicia ese contador sin diagnóstico.

## 5. Aislamiento entre chats y turnos de herramientas

Separar carpetas o nombres no crea saldos independientes ni conexiones independientes.

- Cada ticket pertenece a una tarea. Sólo su propietario modifica la fuente y prepara prompts de ese recurso.
- Prefijos propuestos: HUM, MOS, ENV, PRP, UI y AV. Ejemplo: PRP_LINTERNA_001_v001. Cada trabajo guarda referencia, parámetros, costo, job ID, resultado y decisión.
- Estructura propuesta por departamento: inputs, source, jobs, exports y review. El Encargado la concreta tras recibir del Director las rutas y el estado del alfa; esta planificación no cambia las carpetas del alfa.
- Las dependencias usan una versión entregada. No editar directamente la fuente abierta por otro chat ni copiar su archivo .meta para un asset nuevo.
- El MCP local actualmente apunta a una sesión en 127.0.0.1:9876. Antes de una modificación, comprobar propietario del turno, proceso, archivo Blender abierto y colección objetivo. Una herramienta conectada no demuestra que tenga abierta la escena correcta.
- Empezar con **una generación pendiente y un turno de Blender** para toda la cuenta/equipo. Preparación de fichas y revisión de resultados sí pueden avanzar por separado.
- No cambiar el .blend durante una importación asíncrona: el turno se conserva hasta terminar/importar/guardar, o se descarga el resultado y se programa su importación después.
- Sólo se libera el turno después de guardar la entrega y registrar el estado. También se respeta la coordinación existente de Unity/exportadores para evitar interferencias.
- No reconfigurar cuentas, puertos ni sesiones globales desde cada departamento. Si más adelante se crean procesos aislados, el aislamiento se comprueba antes de habilitar concurrencia.
- Los revisores devuelven observaciones al propietario. Una falla visual no autoriza otra generación desde un chat distinto.

## 6. Un presupuesto para todo el equipo

Saldo de la última auditoría: Ultra, 3.000 créditos. Esta planificación gastó 0. Antes de activar producción se relee el saldo: podría haber cambiado por uso externo.

Los siguientes son **techos propuestos, no órdenes de gasto**, y reemplazarían la distribución provisional anterior; no se suman a ella.

| Área | Techo de créditos |
|---|---:|
| Humanos y prendas especiales | 650 |
| Mosquitos y piezas especiales | 350 |
| Terreno / arquitectura / formas distintivas | 450 |
| Elementos y muebles | 550 |
| UI e ilustraciones puntuales | 200 |
| Presentación y Audio, sólo necesidad esencial | 100 |
| Piloto compartido de toda la actualización | 100 |
| Reserva no comprometida | 600 |
| **Total** | **3.000** |

Director, Online, Gameplay y revisores no reciben bolsas generativas por defecto. La reserva sólo se reasigna con una prioridad documentada; un sobrante de un área tampoco se convierte automáticamente en permiso de otra. Si el saldo disponible es menor, se recorta alcance antes de reservar trabajos.

El piloto tiene un techo global de 100, incluido en los 3.000; no son 100 por chat, modelo ni prueba. Si no alcanza, se revisa el método antes de ampliar.

### Registro central y prevención de doble gasto

El Encargado es el único escritor del registro global de reservas. Los propietarios aportan sus fichas; no editan simultáneamente un CSV compartido para autorizarse.

Flujo propuesto: borrador → cotizado → reservado → enviado con job ID → completado/fallido/desconocido → conciliado → aceptado o corrección.

- Disponible para nuevas reservas = saldo confirmado menos compromisos que todavía no estén reflejados en ese saldo. Separar “reservado sin enviar”, “enviado no debitado” y “ya debitado” para no descontar dos veces.
- Registrar costo máximo aceptado, estimación vigente y costo real; comparar saldo antes/después del lote.
- Una respuesta de red perdida se marca “desconocida”. Consultar job ID e historial antes de reenviar; no asumir que un timeout canceló el cargo.
- No liberar una reserva por un fallo hasta verificar el cargo o reembolso. No asumir reembolsos.
- Mantener recarga automática desactivada. No comprar extras ni activar otro plan desde un ticket.

### Beneficios gratuitos e ilimitados

Los beneficios web auditados no se trasladan automáticamente a Blender/MCP/CLI/Supercomputer. Sólo tratar como cero una operación que el servicio confirme elegible y gratuita por esa vía.

Las promociones web manuales pueden servir para preparar referencias con sus parámetros/vigencias admitidos. No planificar automatización web como mecanismo de obtener generaciones ilimitadas. Nano Banana Pro tenía beneficio hasta el 20/09/2026 y Nano Banana 2 hasta el 13/10/2026 según la auditoría; comprobar vigencia al usarlos.

Modelado, cambios de materiales, renders y scripts locales no gastan créditos generativos de Higgsfield, aunque requieren trabajo y recursos del equipo. Planificación en Codex y modelado local evitan gastar créditos en conversaciones de Supercomputer.

**No se puede garantizar convertir cada variación de todas las láminas en un asset final con un número fijo de créditos.** Para que alcance la siguiente actualización: backlog cerrado, piezas compartidas, adaptación local, prioridades y reserva. El número de combinaciones de personalización puede crecer sin multiplicar las generaciones.

## 7. Secuencia de la actualización

El usuario indicó organizar por departamentos completos, sin pasar de Humanos a Mosquitos hasta aprobar y finalizar Humanos. Esta secuencia reemplaza el piloto simultáneo y la producción por lotes cruzados de la primera propuesta.

### Preparación — base recibida, sin reabrir el alfa

Entrega recibida en ENTREGA-DIRECTOR-ALFA.md. El alfa quedó congelado, no aprobado. Proyecto real: N:/LetMeSleep/Repository/unity, Unity 6000.3.24f1, URP 17.3.0. Fuente funcional referida 5c82b12; cierre documental 67bfd94; entrega fb7fb57. Binario local alfa.3 de e9d15e7 y prerelease remota alfa.2 son bases distintas.

Registrar las fuentes y el alcance concreto de cada departamento. El STOP histórico permanece sobre las asignaciones antiguas. La planificación de la nueva actualización no reabre esas órdenes. Las pruebas de Humanos requerirán resolver, dentro de su nuevo ticket de integración, el bloqueo conocido de referencias del asmdef PlayMode; esto no se registra como prueba pasada ni se corrige durante esta entrega documental.

La raíz operativa de esta etapa es N:/LetMeSleep/Repository/Higgsfield/. Los documentos previos de C: quedan como antecedentes; toda nueva producción, trabajo y evidencia va en N:.

### Departamento 1 — Humanos

1. Crear ficha autoral exclusivamente desde los bocetos. Separar la investigación técnica de integración para que no introduzca modelos, renders, proporciones ni materiales anteriores.
2. Definir un catálogo finito de base/prendas/variantes y movimientos necesarios.
3. Crear desde cero la nueva base de pijama/pantuflas/gorro fiel a UI-06; evaluar su referencia Higgsfield y después comprobarla en Unity antes de multiplicar variantes.
4. Preparar rig, huesos y sockets adecuados al nuevo modelo. Migrar/enlazar IDs de runtime de forma explícita. Los 65 huesos y 17 movimientos informados del alfa son antecedentes técnicos, no una obligación autoral para la nueva malla.
5. Validar deformaciones, locomoción completa, crossfades, mirada, acciones y agarres con objetos existentes/patrones.
6. Producir personalización acordada reutilizando cuerpo, rig y materiales.
7. Revisar diseño visual, animación, función y estabilidad; corregir; cerrar una versión identificada y con fuentes/exportaciones.
8. Registrar limitaciones externas todavía no comprobables. No llamar cierre total a una entrega con un defecto bloqueante propio.

Gameplay, Presentación, Online y revisores pueden colaborar exclusivamente para validar/corregir el humano. No abren simultáneamente el catálogo de su departamento. Las dependencias compartidas requieren tickets acotados del Encargado.

El techo de 100 créditos del piloto sigue siendo único dentro de los 3.000 y se aplica primero a la evaluación del método en Humanos. No se reinicia por especie ni modelo. La cuota de Humanos de 650 es otro apartado del mismo presupuesto total, no una duplicación del piloto.

### Departamento 2 — Mosquitos

Sólo tras cerrar Humanos: base, escala, anatomía, ojos, probóscide, patas, alas, rig, movimiento y personalización. Revisores y contratos de gameplay acompañan la entrega completa. El WIP de ragdoll fallido no se activa automáticamente ni se presenta como aprobado.

### Departamento 3 — Elementos

Herramientas, muebles y objetos del alcance acordado. Usar dimensiones y sockets cerrados de los personajes. Transferir la fuente histórica del matamoscas desde personajes a su propiedad lógica con trazabilidad y sin duplicación ciega. Verificar partes móviles y uso real.

### Departamento 4 — Terreno y Mapas

Arquitectura, terreno, distribución y recorridos con personajes/elementos de dimensiones conocidas. Cerrar el mapa o conjunto acotado de la actualización; el requisito general de cinco mapas no convierte un único render en cinco niveles terminados. La ampliación se desglosa y presupuesta.

### Departamento 5 — UI

Dirección visual definida desde preparación; producción completa después de disponer de los assets finales. Menús, personalización, HUD, salas, ajustes, resultados y estados de error con controles reales y alcance funcional vigente. Retratos y miniaturas desde assets aprobados.

### Departamento 6 — Presentación y Audio

Completar ambiente, mezcla, transiciones, feedback y efectos sobre escenas/acciones estables. Los ajustes mínimos necesarios para validar personajes se hacen como apoyo a sus tickets, sin adelantar este catálogo completo.

### Integración y devolución

Prueba completa sobre la misma versión: flujos, personalización, interacción, online, animación y rendimiento. La prueba integrada puede detectar regresiones que vuelvan al dueño aunque su departamento haya cerrado; no ocultarlas para mantener el orden. Evidencia local no sustituye WAN ni aceptación visual del usuario.

Encargado de Higgfield coordina, concilia gasto, informa al usuario y devuelve el mando al Director con versión, fuentes, resultados y pendientes explícitos. Finalizar generaciones no finaliza la actualización.

## 8. Contrato de entrega para Unity

Cada ticket entrega:

- ID, propietario, referencia principal y referencias auxiliares; lista de desviaciones justificadas.
- Fuente .blend editable y exportación compatible con el pipeline confirmado —por ejemplo FBX, o GLB sólo si el importador real lo admite— junto con texturas/materiales.
- Unidades/escala documentadas, orientación, origen y pivotes comprobados con humano, puerta y objeto patrón.
- Rig, nombres de huesos, sockets, clips y slots compatibles o instrucciones explícitas de migración.
- Materiales probados en Unity: transparencia de alas, caras visibles, normales, sombras y emisión de linterna.
- Medidas reales: triángulos, materiales, tamaño de texturas y, si aplica, huesos/LODs. El límite se fija después de medir plataforma/cámara/cantidad simultánea, no copiando un valor arbitrario del generador.
- Preview bajo iluminación común; personajes también en movimiento, objetos equipados y mapas recorridos con la cámara real.
- Prefab preparado en el área de integración asignada, o instrucciones de importación si aún no está autorizado editar el proyecto. Preservar GUID de assets existentes durante reemplazos controlados.
- Créditos estimados/reales, job ID, parámetros y procedencia de recursos externos cuando existan.

UI entrega además estados normal/foco/seleccionado/deshabilitado/error, texto editable y adaptación a las resoluciones objetivo. Las listas de salas vacías, nombres largos, desconexión y reintento necesitan estados propios.

Mapas comprueba acceso por puertas, colisiones y rutas de humanos y mosquitos. Animación comprueba pies, manos, penetraciones de ropa, alas/patas, bucles y cambios de estado. Online comprueba catálogo de cosméticos coincidente y comportamiento al entrar/reconectar.

## 9. Ficha que utilizará cada propietario

```text
ID de recurso y versión:
Tarea propietaria:
Objetivo de la actualización:
Referencia principal (ID del inventario + zona):
Fuente actual y decisión (reusar/adaptar/local/generar):
Forma, proporciones y paleta que se conservan:
Partes separadas / rig / sockets / variantes:
Consumidores y contratos:
Entrada exacta / modelo / parámetros / número de resultados:
Costo cotizado / techo / reserva:
Turno Blender, archivo y colección:
Job ID / estado / costo real:
Archivos fuente / exportación / preview:
Resultado de revisión / siguiente acción:
```

Ejemplo de dirección para Humanos: “Adaptar el humano al pijama de UI-06; conservar cara facetada y ojos de PER-02/PER-06; validar manos y pantuflas; separar prendas del cuerpo según los slots acordados”. No incluye fabricar su linterna.

Ejemplo para Elementos: “Una linterna amarilla de PRP-01, aislada, proporciones de juguete low-poly, cuerpo opaco y lente separada; origen y agarre para socket de mano; sin mano ni personaje en la geometría”.

Ejemplo para Mapas: “Construir el dormitorio de ENV-03 con iluminación de UI-06; instanciar cama y lámpara entregadas por Elementos; puertas y techo en módulos accesibles para la cámara real”.

Ejemplo para UI: “Recrear la jerarquía y estados del flujo UI-06 con controles reales; previews desde los modelos aprobados; nombres, cantidades y reglas desde Gameplay/Online”.

Estos ejemplos son fichas de planificación; no se enviaron como prompts a Higgsfield ni a las tareas.

## 10. Evidencia y documentos relacionados

- [Inventario de los 22 archivos, IDs y SHA256](<N:/LetMeSleep/Repository/Higgsfield/BOCETOS-INVENTARIO.csv>).
- [Lámina principal propuesta: Let Me Sleep](<C:/Users/brank/Desktop/bocetos/ui/ChatGPT Image 12 sept 2026, 06_01_41 p.m..png>).
- [Auditoría de instalación, cuenta y beneficios](<N:/LetMeSleep/Repository/Higgsfield/ESTADO-Y-PRESUPUESTO.md>).
- [Cotizaciones de referencia](<N:/LetMeSleep/Repository/Higgsfield/COTIZACIONES-3D.json>).
- [Catálogo técnico de modelos](<N:/LetMeSleep/Repository/Higgsfield/CATALOGO-3D.json>).
- [Registro de créditos existente](<N:/LetMeSleep/Repository/Higgsfield/REGISTRO-CREDITOS.csv>).

Fuentes oficiales de la auditoría previa: [complemento Blender](https://higgsfield.ai/plugins/blender), [créditos](https://higgsfield.ai/creator-hub/help-center/credits/how-credits-work), [MCP](https://higgsfield.ai/creator-hub/help-center/integrations/what-is-higgsfield-mcp), [planes y FAQ](https://higgsfield.ai/pricing), [formatos 3D de Unity](https://docs.unity3d.com/6000.0/Documentation/Manual/3D-formats.html).

**Historial:** la primera entrega fue sólo planificación, sin mensajes ni generaciones. El usuario posteriormente autorizó compartir el plan con Director y asignó al Encargado la coordinación completa de esta actualización. La comunicación del relevo no equivale a confirmar que el alfa esté cerrado ni a haber iniciado generaciones.
