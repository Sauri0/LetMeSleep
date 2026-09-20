# Decisiones consolidadas — Let Me Sleep v0.2.0

Fuente: exportación del usuario del20/09/2026 a06:54:40Z. Original preservado sin cambios.

**Archivo original:147 respuestas (129 elecciones,5 alternativas propias,13 delegaciones). Seguimiento: tres ausentes delegadas y resueltas; O08 reemplazada por ningún bot online. Las150 preguntas tienen respuesta, con detalles abiertos abajo.**

Las elecciones claras se incorporan al plan. Las contradicciones y ambigüedades se conservan abiertas. Ninguna entrada de este documento certifica implementación, arte final, rendimiento o pruebas online.

## Puntos por cerrar

- **J25, O08** (resolved_by_user_followup): Ningún bot online. Reservar al jugador30s sin control por IA; sustituye O08=A del archivo original.
- **A01, A02** (resolved_by_visual_direction): Respuesta posterior: mirar los bocetos y reproducirlos idénticos. El criterio es fidelidad a proporciones, silueta y facetas de originales PER-06/PER-07/PER-08, no aumentar o reducir polígonos por una interpretación verbal.
- **C08** (references_located_count_pending): PER-08 personalización dual y PER-06/PER-07 ampliados localizados y vistos. Dirección visual: fidelidad a bocetos originales. Cantidad final de cuerpos mosquito y alcance de categorías extra no fijados; cantidades explícitas C01-C12 conservadas.
- **A08** (interpretation_pending): Todo expresa amplitud, pero las alternativas de exageración y contención no son simultáneamente un único estilo. Conservar nota literal; definir intensidad por acción en boceto/clip.
- **P26, P27** (implementation_interpretation): Guardar cada cambio localmente con deshacer; mantener Aplicar/publicar como acción explícita hacia el lobby. No transmitir cada prueba intermedia.
- **D08, O11** (scope_question_pending): Teclado/ratón completos y mando fuera de entrega frente a opción PTT que también menciona control. Confirmar si PTT requiere excepción; no prometer soporte completo de mando.
- **O16, O17, O18** (implementation_interpretation): Base clara4m/corte12m; al hablar mosquito, cortes8m para humanos y16m para mosquitos sustituyen el corte base. No acumular dos cortes.
- **O03, O19** (detail_pending): Falta decidir con quién conversa quien espera y si canal eliminado es global o de proximidad; preservar separación de actores activos.
- **O07, O08** (detail_pending): Definir consecuencia exacta por modo al vencer30s; no recrear actor ni recuperar vidas automáticamente.
- **T01** (reference_hardware_pending): 1080p60 es objetivo, no evidencia. Falta fijar CPU/GPU/RAM del equipo de referencia.
- **D04** (balance_pending): Pérdida gradual tras gracia aprobada; valores concretos no incluidos en esta respuesta.
- **C11** (design_pending): Seis opciones mosquito incluida ninguna; los diseños concretos no están enumerados.

## Decisiones delegadas

| ID | Decisión CEO | Motivo |
|---|---|---|
| P06 | A · Entrar y repetir sin fricción | Primero verificar ingreso y repetición de rondas para que la sesión con amigos pueda completarse. |
| P13 | B · Media: 22 px | 22 px como referencia a 1080p; verificar lectura y redistribución a 720p. |
| P14 | A · Icono y texto en todos los estados | Icono y texto evitan depender sólo del color y ayudan a reconocer estados. |
| P15 | C · Controles por efecto | Controles por efecto permiten regular balanceo y sacudidas sin confundirlos con la orientación funcional de A20. |
| P16 | A · Dos tarjetas por rol | Tarjetas por rol consultables desde pausa; no se muestran como introducción obligatoria, respetando P04. |
| P17 | B · Tres presets y ajuste fino | Presets iniciales más ajuste fino y valor visible para cada rol. |
| P18 | B · Cinco miniaturas visibles | Los cinco mapas visibles facilitan comparar sin recorrer un carrusel. |
| P19 | B · Fila resaltada hasta estar listo | La regla cambiada sigue visible hasta confirmar listo; no depende de leer un aviso fugaz. |
| P20 | A · Causa y revancha | Causa de victoria y regreso a sala claros; conservar la privacidad de tareas. |
| P21 | A · HUD mínimo | HUD mínimo con aliado observado, marcador público y control para cambiar. |
| P22 | B · Mensaje con diagnóstico desplegable | Acción directa más diagnóstico opcional copiable, sin exponer credenciales ni identificadores privados. |
| J04 | A · Ágil con freno fuerte | Delegación posterior explícita: vuelo ágil actual, aceleración13m/s², frenado28m/s² y velocidad3,8m/s. |
| J27 | A · Memoria de 3 s | Delegación posterior explícita: recordar última información observada3s, sin actualizar información oculta. |
| D05 | A · Cinco segundos | Delegación posterior explícita: restar5s por fallo, conservando mínimo15s. |
| S07 | A · Capas selectivas | Capas selectivas conservan identidad del mapa y espacio para voz y señales. |
| S08 | A · Motivo corto de 3 segundos | Motivo de tres segundos de caja musical/jazz, coherente con entradas musicales puntuales y revancha rápida. |

## Identidad y experiencia

### P01 · ¿Qué sensación debería dominar una buena partida entre amigos?

Estado: **user_choice**.

- **A · Comedia de situación** — Errores y escapes divertidos; celebraciones breves y expresivas.
### P02 · ¿Qué tono deberían tener los textos del juego?

Estado: **user_choice**.

- **A · Cercano con humor discreto** — Frases cálidas fuera del combate; instrucciones literales cuando hay que actuar.
### P03 · ¿Qué debería destacar la primera pantalla para un amigo nuevo?

Estado: **user_choice**.

- **C · Elegir sin preferencia** — Crear, unirse y entrenar aparecen con igual tamaño; hay más decisiones de entrada.
### P04 · ¿Cómo debería aprender alguien su rol la primera vez?

Estado: **user_choice**.

- **C · Ayuda contextual** — Sin tarjeta inicial; una pista aparece al acercarse por primera vez a cada interacción.
### P05 · ¿Cuándo deberían retirarse las ayudas para principiantes?

Estado: **user_choice**.

- **A · Después del primer éxito** — La pista se retira al completar esa acción una vez; se recupera desde ayuda.
### P06 · ¿Qué prioridad debe guiar la primera sesión de prueba con amigos?

Estado: **ceo_delegated_decision**.

- **A · Entrar y repetir sin fricción** — Medir desde código de sala hasta revancha; registrar cada espera o salida inesperada.
### P07 · ¿Cómo se debería ofrecer ayuda después de una dificultad repetida?

Estado: **user_choice**.

- **A · Consejo opcional discreto** — Mostrar una sola sugerencia breve tras tres intentos; nunca pausar.
### P08 · ¿Qué información debería acompañar el cambio de rol entre rondas?

Estado: **user_choice**.

- **C · Tarjeta sólo al cambiar de rol** — Mostrar 3 segundos si el rol difiere del anterior; si se repite, aviso de 1 segundo.

## Interfaz y accesibilidad

### P09 · ¿Cuánta información debería permanecer visible en el HUD?

Estado: **user_choice**.

- **B · Información contextual** — Tiempo y marcador fijos; controles y detalles de tarea aparecen cuando son relevantes.
### P10 · ¿Cómo preferís leer el avance de tu tarea privada?

Estado: **user_choice**.

- **A · Barra y porcentaje** — Título, barra horizontal y 40 %; preciso y fácil de comparar.
### P11 · ¿Cómo debería destacarse un plazo de tarea que está por vencer?

Estado: **user_choice**.

- **A · Umbral fijo de 10 segundos** — Borde destacado y texto «Poco tiempo», sin parpadeo.
### P12 · ¿Cómo debería explicar el HUD una acción que no se puede hacer?

Estado: **user_choice**.

- **A · Razón inmediata** — Mostrar «Acercate a tu tarea» durante 2 segundos al intentar.
### P13 · ¿Qué escala de interfaz querés como referencia para una pantalla de 1080p?

Estado: **ceo_delegated_decision**.

- **B · Media: 22 px** — Equilibra lectura y espacio; algunos textos ocuparán dos líneas.
### P14 · ¿Cómo deberían distinguirse estados sin depender sólo del color?

Estado: **ceo_delegated_decision**.

- **A · Icono y texto en todos los estados** — Redundancia permanente; filas un poco más anchas.
### P15 · ¿Qué control de movimiento visual debería ofrecerse en ajustes?

Estado: **ceo_delegated_decision**.

- **C · Controles por efecto** — Balanceo, transiciones y pulsos por separado; mayor flexibilidad y más ajustes.
### P16 · ¿Qué formato debería tener la guía de controles?

Estado: **ceo_delegated_decision**.

- **A · Dos tarjetas por rol** — Un esquema de teclado para cada rol, accesible desde pausa.
### P17 · ¿Cómo te gustaría ajustar la sensibilidad de cada rol?

Estado: **ceo_delegated_decision**.

- **B · Tres presets y ajuste fino** — Suave, media y rápida, más deslizador opcional; mejor punto de partida.
### P18 · ¿Cómo debería organizarse la selección de los cinco mapas en el lobby?

Estado: **ceo_delegated_decision**.

- **B · Cinco miniaturas visibles** — Comparación inmediata; cada imagen tendrá menos detalle.
### P19 · ¿Cómo debería comunicarse un cambio de reglas hecho por el anfitrión?

Estado: **ceo_delegated_decision**.

- **B · Fila resaltada hasta estar listo** — Destacar las reglas modificadas hasta tu nueva confirmación; menos avisos flotantes.
### P20 · ¿Qué debería priorizar la pantalla de resultados?

Estado: **ceo_delegated_decision**.

- **A · Causa y revancha** — Ganador, objetivo alcanzado y volver a la sala; lectura en pocos segundos.
### P21 · ¿Cómo debería presentarse la observación después de quedar eliminado?

Estado: **ceo_delegated_decision**.

- **A · HUD mínimo** — Nombre del aliado, marcador público y control para cambiar; más espacio para mirar.
### P22 · ¿Cómo deberían explicarse los errores al entrar a una sala?

Estado: **ceo_delegated_decision**.

- **B · Mensaje con diagnóstico desplegable** — Misma ayuda simple y un detalle copiable para reportar problemas.

## Personalización

### P23 · ¿Cómo debería recorrerse el catálogo de personalización previsto?

Estado: **user_choice**.

- **A · Por rol primero** — Elegir Humano o Mosquito y después cuerpo, piezas y colores; pocas opciones simultáneas.
### P24 · ¿Cómo deberían identificarse los colores de una paleta?

Estado: **user_choice**.

- **B · Muestra con nombre al enfocar** — Más compacta; el nombre aparece con cursor o selección por teclado.
### P25 · ¿Qué interacción debería ofrecer el visor de apariencia?

Estado: **user_choice**.

- **A · Giro libre con arrastre** — Rotar con cursor y botón de restablecer vista; control continuo.
### P26 · ¿Cómo debería guardarse una nueva combinación de piezas y colores?

Estado: **user_choice**.

- **B · Guardar cada cambio con deshacer** — Resultado inmediato y botón para volver a la combinación inicial de esa sesión.

Revisar: appearance_save.
### P27 · ¿Qué debería ocurrir al cambiar apariencia mientras estás en el lobby?

Estado: **user_choice**.

- **A · Publicar al aplicar** — Los demás ven la combinación cuando la guardás; evita ver pruebas intermedias.

Revisar: appearance_save.
### P28 · ¿Cómo te gustaría reutilizar combinaciones del catálogo acordado?

Estado: **user_choice**.

- **A · Una combinación guardada** — Mantener sólo la última; interfaz y persistencia simples.

## Jugabilidad

### J01 · ¿Cómo debe gastar estamina el humano al correr?

Estado: **user_choice**.

- **A · Consumo continuo** — Propuesta: 100 puntos, coste 16/s; permite unos 6,25 s de carrera completa y caminar siempre.
### J02 · ¿Cuándo y a qué ritmo se recupera la estamina humana?

Estado: **user_choice**.

- **C · Recuperación por postura** — Propuesta: 12/s caminando y 28/s quieto o agachado; premia detenerse de forma visible.
### J03 · ¿Qué acciones humanas, además de correr, consumen estamina?

Estado: **user_choice**.

- **C · Saltos y acciones cargadas** — Propuesta: salto 10 y lanzamiento cargado 8–15; manos y defensa básica siempre gratuitas, sin reservar una única palmada.
### J04 · ¿Qué sensación debe tener la aceleración y el freno del mosquito?

Estado: **ceo_delegated_followup**.

- **A · Ágil con freno fuerte** — Conservar propuesta actual: aceleración 13 m/s², frenado 28 m/s² y velocidad 3,8 m/s.

Aclaración posterior del usuario:

> Respuesta directa posterior: Delegártelas: vuelo ágil actual, memoria de 3 s y penalidad de 5 s.
### J05 · ¿Cómo se activa y cancela el posado del mosquito?

Estado: **user_choice**.

- **A · Alternar con tecla** — Una pulsación busca superficie; otra o saltar desprende. El roce normal no cambia de estado.
### J06 · ¿Qué compromiso temporal debe exigir iniciar una picadura?

Estado: **user_choice**.

- **A · Preparación de 0,6 s** — Conservar el valor provisional; soltar, perder contacto o recibir golpe cancela y obliga a preparar otra vez.
### J07 · ¿Cómo debe recuperarse un humano después del desmayo?

Estado: **user_choice**.

- **A · 12 s más protección** — Conservar 12 s desde el aterrizaje, transición de 0,4 s y protección de 1,5 s al volver.
### J08 · ¿Qué interacción gana cuando varios objetos están bajo la mirada?

Estado: **user_choice**.

- **B · Centro de mirada** — Gana el collider más centrado y cercano, sin prioridad por categoría; máxima consistencia espacial.

## Modos

### J09 · ¿Qué duración base debe tener cada modo?

Estado: **user_choice**.

- **A · Duración por modo** — Propuesta: Sangre 180 s, Supervivencia 150 s y Tareas 240 s; el host elige variantes dentro de límites.
### J10 · ¿Cómo escala la cuota de sangre con la población?

Estado: **user_choice**.

- **A · Escala por humanos** — Propuesta: 12 unidades base + 6 por humano; 18 con uno y 42 con cinco.
### J11 · ¿Cómo contribuyen varios mosquitos que pican al mismo humano?

Estado: **user_choice**.

- **A · Tasa total compartida** — Conservar el reparto actual: juntos aseguran continuidad, pero la víctima recibe como máximo una tasa completa.
### J12 · ¿Cómo se distribuyen las herramientas en Supervivencia?

Estado: **user_choice**.

- **A · Pickups finitos por ronda** — Los objetos quedan donde se sueltan y no reaparecen; el equipo debe administrarlos y recuperarlos.
### J13 · ¿Qué cadencia y plazo inicial deben usar las tareas personales?

Estado: **user_choice**.

- **A · 40/30/15 segundos** — Conservar cadencia 40 s, plazo 30 s y mínimo 15 s; trabajo y ruta deben caber dentro.
### J14 · ¿Qué porcentaje de oportunidades debe exigir la meta compartida de Tareas?

Estado: **user_choice**.

- **A · Dos tercios** — Conservar ceil(2/3): 8 de 12. Tolera fallos sin volver irrelevantes a los mosquitos.
### J15 · ¿Puede recuperarse el plazo personal después de completar tareas?

Estado: **user_choice**.

- **A · Recupera un escalón** — Propuesta: cada acierto devuelve 3 s, hasta el plazo inicial; premia recomponerse.
### J16 · ¿Cuánta protección recibe un mosquito al reaparecer en Tareas?

Estado: **user_choice**.

- **A · 1,5 s sin atacar** — Conservar 1,5 s de protección y bloquear picadura/ayuda hasta que termine o el mosquito actúe.

## Inventario y herramientas

### J17 · ¿Qué ocurre al recoger un objeto con los tres slots ocupados?

Estado: **user_choice**.

- **A · Intercambiar slot activo** — Suelta de forma segura el objeto activo y ocupa ese slot; muestra una confirmación breve antes de ejecutar.
### J18 · ¿Cómo se seleccionan slots y manos durante una acción?

Estado: **user_choice**.

- **A · Cambio cancela acción** — 1–3 seleccionan slots y una tecla vuelve a manos; cambiar cancela carga sin lanzar ni gastar recurso.
### J19 · ¿Cómo se suelta una herramienta sin convertir soltar en lanzamiento?

Estado: **user_choice**.

- **A · Depositar delante** — Coloca a 0,6–1 m sobre el primer soporte libre; si no hay espacio, conserva el objeto.
### J20 · ¿Qué ventaja concreta ofrece el matamoscas frente a las manos?

Estado: **user_choice**.

- **A · Más alcance, más lento** — Propuesta: +35% alcance, ventana/cooldown 25% más largos y área moderada.
### J21 · ¿Qué objetos pueden lanzarse con carga?

Estado: **user_choice**.

- **A · Pantufla** — Arrojable recuperable, arco medio y golpe de precisión; propuesta principal.
### J22 · ¿Cómo se traduce la carga en potencia de lanzamiento?

Estado: **user_choice**.

- **A · Carga de 0,9 s** — Propuesta: mínimo 35%, máximo a 0,9 s, curva suave y autoestabilización hasta 1,5 s.
### J23 · ¿Qué recurso y efecto usa la raqueta eléctrica?

Estado: **user_choice**.

- **A · Pulsos con batería** — Propuesta: 5 cargas, un pulso de 0,35 s y cooldown 1,2 s; recarga sólo al reaparecer el pickup.
### J24 · ¿Cómo funciona el aerosol y dónde se repone?

Estado: **user_choice**.

- **A · Nube corta consumible** — Propuesta: 4 s totales por envase, cono 2 m, nube 1,2 s; no se repone hasta nueva ronda o pickup.

## Bots

### J25 · Además del entrenamiento obligatorio, ¿dónde permitimos bots?

Estado: **user_choice**.

- **A · Sólo entrenamiento** — Entrenamiento completo con bots; sala privada sólo con personas.

Revisar: bots_online.
### J26 · ¿Qué niveles de dificultad de bots deben existir en v0.2.0?

Estado: **user_choice**.

- **A · Normal único** — Un perfil verificable para publicar; reacción propuesta 250–400 ms y mismos permisos que jugadores.
### J27 · ¿Cuánto dura la memoria de un objetivo que salió de vista?

Estado: **ceo_delegated_followup**.

- **A · Memoria de 3 s** — Recuerda última posición y dirección durante 3 s; después vuelve a patrulla sin actualizar datos ocultos.

Aclaración posterior del usuario:

> Respuesta directa posterior: Delegártelas: vuelo ágil actual, memoria de 3 s y penalidad de 5 s.
### J28 · ¿Cómo prioriza un bot humano su tarea frente a un mosquito visible?

Estado: **user_choice**.

- **A · Defensa por amenaza** — Interrumpe si el mosquito está a menos de 2 m, se acerca o ya pica; luego retoma progreso conservado.
### J29 · ¿Cómo elige víctima un bot mosquito cuando ve varios humanos?

Estado: **user_choice**.

- **A · Puntuación de oportunidad** — Propuesta: distancia, línea libre, tarea activa y ataques recientes; penaliza repetir víctima durante 8 s.
### J30 · ¿Hasta qué punto usan puertas y herramientas los bots humanos?

Estado: **user_choice**.

- **A · Puertas y herramienta cercana** — Abre rutas necesarias y recoge una herramienta si el desvío es menor a 4 m y tiene slot libre.
### J31 · ¿Cuándo debe un bot mosquito rescatar a un aliado caído?

Estado: **user_choice**.

- **A · Riesgo y tiempo** — Rescata si puede llegar con 1,5 s de margen y no ve un humano a menos de 2,5 m del aliado.
### J32 · ¿Qué hace un bot cuando no progresa por una ruta durante varios segundos?

Estado: **user_choice**.

- **A · Replanificar y esperar** — A los 5 s marca el pasaje 6 s, busca otra ruta; si no existe, espera/patrulla la región y registra el atasco.

## Arte y personajes

### A01 · ¿Qué tratamiento de formas querés para el humano nuevo?

Estado: **user_custom**.

Respuesta literal:

> mas poly todavia que el A pero por ahi va la cosa

Aclaración posterior del usuario:

> mira los bocetos, los quiero identicos

Revisar: poly.
### A02 · ¿Qué tratamiento de formas querés para el mosquito?

Estado: **user_custom**.

Respuesta literal:

> mas poly todavia que el A pero por ahi va la cosa

Aclaración posterior del usuario:

> mira los bocetos, los quiero identicos

Revisar: poly.
### A03 · ¿Cuánto exageramos las proporciones humanas?

Estado: **user_choice**.

- **A · Exageración moderada** — Cabeza y manos algo grandes, piernas adultas, sin parecer un niño.
### A04 · ¿Qué expresión base tendrá el mosquito?

Estado: **user_choice**.

- **A · Travieso** — Cejas y ojos atentos, picardía sin gesto maligno.
### A05 · ¿Cuánta textura pintada deben tener ropa y objetos?

Estado: **user_choice**.

- **A · Sutil** — Colores amplios, costuras y vetas seleccionadas.
### A06 · ¿Qué contraste de color tendrá la personalización?

Estado: **user_choice**.

- **A · Paleta curada** — Colores variados con luminosidad controlada y lectura parecida.
### A07 · ¿Cómo distinguimos visualmente a jugadores con el mismo modelo?

Estado: **user_choice**.

- **A · Color secundario y nombre contextual** — Detalles cosméticos y nombre al mirarlos cerca.
### A08 · ¿Cuánta expresividad facial querés en juego?

Estado: **user_custom**.

Respuesta literal:

> todo

Revisar: expressions.
### A09 · ¿Qué lectura visual tendrán las alas del mosquito?

Estado: **user_choice**.

- **A · Translúcidas discretas** — Venas/facetas visibles al reposo y vibración suave al volar.
### A10 · ¿Cómo representamos la sangre sin marcas de picadura?

Estado: **user_choice**.

- **A · Abdomen y HUD** — Cambio sutil de volumen/color y progreso; sin gotas en pantalla.
### A11 · ¿Qué acabado visual unifica los cinco mapas?

Estado: **user_choice**.

- **A · Materiales limpios** — Facetas legibles, desgaste sólo donde cuenta una historia.
### A12 · ¿Cómo identificamos objetos utilizables sin añadir balizas?

Estado: **user_choice**.

- **A · Contorno al apuntar** — Sólo el objeto cercano bajo la mira, más texto de acción.

## Animación y cámaras

### A13 · ¿Qué peso debe tener el caminar humano?

Estado: **user_choice**.

- **B · Cómico pesado** — Más balanceo y arrastre visual, sin añadir retraso al control.
### A14 · ¿Cómo debe verse una defensa manual fallida?

Estado: **user_choice**.

- **A · Recuperación breve** — Trayectoria clara, retorno rápido y mueca corta.
### A15 · ¿Qué tratamiento damos al desmayo y caída?

Estado: **user_choice**.

- **A · Caída dirigida** — Poses diseñadas con ajuste físico acotado al entorno.
### A16 · ¿Qué protagonismo tienen los gestos de ayuda/rescate?

Estado: **user_choice**.

- **A · Breves y legibles** — Gesto claro y progreso discreto, cancelación inmediata.
### A17 · ¿Cuánto movimiento secundario tienen gorro y pijama?

Estado: **user_choice**.

- **A · Moderado** — Gorro y tela reaccionan poco; silueta estable.
### A18 · ¿Qué personalidad tiene el mosquito al estar quieto?

Estado: **user_choice**.

- **A · Atento** — Antenas y patas ajustan postura con movimientos pequeños.
### A19 · ¿Cuánto cuerpo humano querés ver al mirar hacia abajo?

Estado: **user_choice**.

- **A · Cuerpo completo coherente** — Torso, brazos y piernas visibles donde el encuadre lo permita.
### A20 · ¿Cómo se adapta la cámara mosquito al pasar de suelo a pared y techo?

Estado: **user_custom**.

Respuesta literal:

> sigue al mosquito en terminos de su horizonte, y lo que solo queda libre es cuando no te moves ahi si la camara podes moverla sin mover al mosquito pero si te moves vuelve a su horizonte
### A21 · ¿Qué distancia de cámara mosquito preferís por defecto?

Estado: **user_choice**.

- **A · Media adaptable** — Distancia media; se acerca al detectar obstáculos.
### A22 · ¿Qué intensidad de sacudida y balanceo visual tendrá el preset inicial?

Estado: **user_choice**.

- **A · Suave y regulable** — Feedback perceptible; slider hasta cero.

## Mapas y ambiente

### A23 · ¿Qué tratamiento de luz preferís para Casa y Campamento nocturnos?

Estado: **user_choice**.

- **C · Mezcla por zona** — Interiores A y exteriores B; C queda sólo como referencia de crepúsculo para Puerto.
### A24 · ¿Qué tono de crepúsculo querés en Puerto del Faro?

Estado: **user_choice**.

- **A · Lavanda con luces cálidas** — Referencia C del tablero, lectura amplia del horizonte.
### A25 · ¿Qué sensación debe dominar Isla del Laguito diurna?

Estado: **user_choice**.

- **A · Vacaciones tranquilas** — Luz clara, agua suave y rincones cálidos.
### A26 · ¿Qué carácter doméstico debe tener Casa del Patio?

Estado: **user_choice**.

- **A · Casa vivida ordenada** — Objetos cotidianos y rutas despejadas.
### A27 · ¿Qué contraste organiza Campamento Pinar?

Estado: **user_choice**.

- **A · Fogón central cálido** — El fogón orienta, bosque azul y senderos claros.
### A28 · ¿Qué personalidad material tendrá Yate a la Deriva?

Estado: **user_choice**.

- **A · Vacacional sencillo** — Madera y tapizados claros, detalles náuticos moderados.
### A29 · ¿Qué punto de referencia debe dominar Puerto además del faro?

Estado: **user_choice**.

- **A · Taller reconocible** — Color/accesorios propios y acceso claramente visible.
### A30 · ¿Cuánto detalle ambiental pequeño querés cerca de rutas?

Estado: **user_choice**.

- **A · Detalle concentrado** — Zonas decorativas ricas; suelo/ruta y objetos usables limpios.
### A31 · ¿Cómo comunicamos el límite jugable del mapa?

Estado: **user_choice**.

- **A · Entorno y aviso breve** — Obstáculo/lectura ambiental y texto sólo al insistir.
### A32 · ¿Qué explicación visual acompaña una recuperación por agua o caída fuera del mapa?

Estado: **user_choice**.

- **A · Transición corta y motivo** — Fundido breve y «Volviste a una zona segura». Sin penalidad nueva.
### A33 · ¿Qué comportamiento visual del agua preferís?

Estado: **user_choice**.

- **A · Suave por entorno** — Lago calmo, arroyo direccional y mar algo más activo.
### A34 · ¿Qué evidencia visual querés usar para aprobar el arte integrado?

Estado: **user_choice**.

- **A · Comparativa y clip** — Frente/perfil/espalda más video corto de acciones y recorrido real.

## Catálogo y alcance exacto

### C01 · ¿Cuántas siluetas humanas distintas incluimos?

Estado: **user_choice**.

- **C · Tres bases compatibles** — Más variedad corporal; todas requieren rig, combinaciones y validación.
### C02 · ¿Cuántos peinados compondrán el catálogo humano?

Estado: **user_choice**.

- **A · Ocho en total** — Cantidad propuesta en el plan histórico, con siete peinados más calvo.
### C03 · ¿Cuánta variedad modular de ojos, cejas y bocas querés?

Estado: **user_choice**.

- **A · Seis por categoría** — 18 variantes de piezas faciales, propuesta histórica.
### C04 · ¿Qué catálogo de vello facial humano incluimos?

Estado: **user_choice**.

- **A · Seis opciones** — Incluye ninguna; color vinculado al pelo con separación opcional.
### C05 · ¿Qué catálogo de ropa acompaña al pijama base obligatorio?

Estado: **user_choice**.

- **A · Pijama más cuatro conjuntos** — Separables en superior/inferior cuando las combinaciones sean compatibles.
### C06 · ¿Cuántos accesorios de cabeza humanos se incluyen?

Estado: **user_choice**.

- **A · Seis opciones totales** — Contar el gorro nocturno y una opción sin accesorio.
### C07 · ¿Cuántas gafas y variantes de calzado humano incluimos?

Estado: **user_choice**.

- **A · Cuatro y cuatro** — Cuatro gafas contando ninguna; cuatro calzados contando pantuflas.
### C08 · ¿Cuántos cuerpos cosméticos de mosquito incluimos?

Estado: **user_custom**.

Respuesta literal:

> personalizacion total como enn uno de mis bocetos principales sobre personalizacion asi me gustaria que se vieran losn pesojanes tantio mosquitos como humanos asi identicos

Revisar: reference.
### C09 · ¿Qué variedad de alas y expresión mosquito querés?

Estado: **user_choice**.

- **A · Cuatro alas y seis expresiones** — Cantidad histórica; expresiones mediante ojos/cejas compatibles.
### C10 · ¿Cuántos patrones de abdomen mosquito incluimos?

Estado: **user_choice**.

- **A · Cinco patrones** — Cantidad propuesta histórica, con paletas por partes.
### C11 · ¿Qué variedad de accesorios mosquito incluimos?

Estado: **user_choice**.

- **A · Seis opciones totales** — Incluye ninguna; seleccionar diseños concretos en notas.

Revisar: accessory_designs.
### C12 · ¿Qué conjunto de emotes entra en esta versión?

Estado: **user_choice**.

- **A · Cuatro emotes comunes** — Saludar, señalar, reír y celebrar, adaptados a ambos roles.

## Reglas por cerrar

### D01 · ¿Cómo elige el anfitrión la composición humana/mosquito?

Estado: **user_choice**.

- **B · Presets editables** — Composiciones sugeridas por tamaño más ajuste numérico.
### D02 · ¿Qué variedad de acciones tendrán las tareas?

Estado: **user_choice**.

- **A · Mantener una tecla** — Diferencia por objeto, duración y animación; control consistente.
### D03 · ¿Cuántos puntos de tarea distintos queremos por mapa?

Estado: **user_choice**.

- **A · Mínimo diez puntos** — Dos alternativas por humano a cinco humanos, distribuido entre zonas.
### D04 · ¿Qué progreso conserva una tarea si la interrumpen?

Estado: **user_choice**.

- **A · Conserva con pérdida gradual** — Breve gracia y luego baja lentamente; valores concretos se prueban.

Revisar: task_decay.
### D05 · ¿Cuánto se acorta el plazo personal al fallar una tarea?

Estado: **ceo_delegated_followup**.

- **A · Cinco segundos** — Penalidad lineal fácil de explicar, hasta el mínimo.

Aclaración posterior del usuario:

> Respuesta directa posterior: Delegártelas: vuelo ágil actual, memoria de 3 s y penalidad de 5 s.
### D06 · ¿Cuánto tarda el rescate de un mosquito aliado?

Estado: **user_choice**.

- **A · Dos segundos continuos** — Tiempo corto con cancelación al alejarse o recibir impacto.
### D07 · ¿Qué esquema inicial de teclas preferís para inventario e interacción?

Estado: **user_choice**.

- **B · Rueda más números** — Rueda recorre manos y slots, números directos; E interactuar.
### D08 · ¿Qué equipo de entrada debe quedar listo para v0.2.0?

Estado: **user_choice**.

- **A · Teclado y ratón completos** — Reasignación y sensibilidad por rol; mando queda fuera de esta entrega.

Revisar: ptt_controller.

## Online y sesiones

### O01 · Forma de escribir y compartir el código privado

Estado: **user_choice**.

- **A · Copiar y pegar explícito** — Tarjeta grande con botón Copiar y campo único con acción Pegar iniciada por la persona. Normaliza guiones, espacios y mayúsculas y muestra el código completo antes de conectar.
### O02 · Tamaño de grupo objetivo para la primera validación

Estado: **user_choice**.

- **A · Dos a cuatro** — Diseñar la primera aceptación para 2–4 personas reales. Facilita sesiones repetibles y conversación clara; luego se escala con evidencia.
### O03 · Ingreso cuando una ronda ya empezó

Estado: **user_choice**.

- **A · Esperar próxima ronda** — Permitir entrar a la sala, conversar con límites definidos y ver un resumen no sensible, pero asignar rol y actor recién al comienzo siguiente. Conserva el balance y evita filtrar tareas privadas.

Revisar: waiting_voice.
### O04 · Confirmación de listo antes de iniciar

Estado: **user_choice**.

- **A · Todos listos** — Iniciar sólo cuando cada miembro conectado cargó contenido y marcó Listo. Es la regla más clara para grupos de amigos.
### O05 · Cómo explicar el sorteo aleatorio de roles

Estado: **user_choice**.

- **A · Resultado y composición** — Mostrar quién quedó humano o mosquito y la cantidad total de cada rol, con una animación corta. No expone semilla ni promete compensación.
### O06 · Momento para elegir mapa y modo

Estado: **user_choice**.

- **A · Anfitrión elige antes** — El anfitrión selecciona mapa y modo en la sala; todos ven la elección y sus reglas antes de marcar Listo. Al iniciar queda bloqueada hasta la ronda siguiente.
### O07 · Ventana de reingreso tras una desconexión

Estado: **user_choice**.

- **A · Reserva de 30 segundos** — La misma identidad recupera su actor conservado durante 30 s si continúa válido; un bot puede mantenerlo inmóvil o seguro. Después aplica ingreso tardío.

Revisar: disconnect_expiry.
### O08 · Continuidad de ronda ante una desconexión

Estado: **user_followup_supersedes_choice**.

- **CUSTOM · Ningún bot online** — Reservar al jugador30s sin control por IA.

Aclaración posterior del usuario:

> Ningún bot online: reservar al jugador 30 s sin que una IA lo controle.

Revisar: bots_online, disconnect_expiry.
### O09 · Cómo reunir al grupo después de perder al anfitrión

Estado: **user_choice**.

- **A · Crear sala nueva** — Mostrar a todos Volver al menú y, a una persona, Crear nueva sala; el nuevo anfitrión comparte un código nuevo por el canal habitual.
### O10 · Nivel de ayuda ante errores de conexión

Estado: **user_choice**.

- **A · Mensaje y acción directa** — Una frase específica y botones Reintentar, Cambiar código o Actualizar según corresponda. Mantiene la pantalla breve.

## Voz y sonido

### O11 · Tecla y personalización de pulsar para hablar

Estado: **user_choice**.

- **A · Reasignable por dispositivo** — Permitir cambiar la acción mantenida de teclado, mouse y control, detectar conflictos y ofrecer Restaurar. Da control sin cambiar la regla de captura.

Revisar: ptt_controller.
### O12 · Presentación de dispositivos y fallos de entrada

Estado: **user_choice**.

- **A · Lista con estado en línea** — Mostrar el elegido primero, marcar Disponible/No disponible y dejar Seleccionar como acción explícita. Un fallo permanece junto al nombre.
### O13 · Ayuda al pulsar PTT sin micrófono disponible

Estado: **user_choice**.

- **A · Aviso breve no modal** — Mostrar «Elegí un micrófono en Ajustes» durante unos segundos con acceso directo. No tapa la partida.
### O14 · Ubicación y confirmación del mute propio

Estado: **user_choice**.

- **A · Menú rápido más icono** — Mute propio está en el menú rápido y deja un icono persistente en HUD/sala. Equilibra acceso y prevención de cambios accidentales.
### O15 · Alcance temporal del mute de otra persona

Estado: **user_choice**.

- **A · Hasta salir de la sala** — Mantenerlo entre rondas de la misma sala y borrarlo al salir. Evita repetir la acción y no crea una lista persistente.
### O16 · Distancia base de voz por proximidad

Estado: **user_choice**.

- **A · Clara 4 m, corte 12 m** — Favorece encuentros cercanos y reduce ruido simultáneo. Puede ser demasiado corta en exteriores amplios.

Revisar: voice_distance.
### O17 · Audición entre roles dentro de proximidad

Estado: **user_choice**.

- **A · Curva base compartida** — Humanos y mosquitos se oyen con la misma curva base; luego la regla mosquito modifica timbre y alcance por oyente. Es fácil de aprender.

Revisar: voice_distance.
### O18 · Diferencia de alcance de la voz mosquito

Estado: **user_choice**.

- **A · Ocho y dieciséis metros** — Humanos dejan de oír a 8 m y mosquitos a 16 m, con caída gradual. Relación 1:2 fácil de comunicar y medir.

Revisar: voice_distance.
### O19 · Voz de participantes eliminados

Estado: **user_choice**.

- **A · Canal de eliminados** — Cortar envío y recepción con actores activos y permitir proximidad o chat entre eliminados en una ruta separada. Evita ventaja y mantiene compañía, pero requiere reglas claras de transición.

Revisar: waiting_voice.
### O20 · Intensidad de oclusión por geometría

Estado: **user_choice**.

- **A · Filtro leve uniforme** — Una obstrucción reduce 6 dB y recorta agudos, sin cortar por completo. Es estable ante geometría imperfecta.
### O21 · Mezcla de voz con ambiente, música y señales

Estado: **user_choice**.

- **A · Música y ambiente menos 4 dB** — Aplicar ducking suave sólo mientras hay voz audible; efectos críticos quedan intactos. Favorece comprensión en mapas ruidosos.
### O22 · Ubicación de indicadores de voces audibles

Estado: **user_choice**.

- **A · Roster de sesión** — Marcar nombre e icono sólo mientras su audio es audible. Funciona aunque el actor esté fuera de cámara y no revela voces fuera de regla.

## Entrega y validación

### O23 · Matriz mínima de hardware para aceptar voz

Estado: **user_choice**.

- **A · USB más integrado en dos equipos** — Validar headset USB e integrado en dos PCs, incluyendo desconexión y dispositivo ausente. Es el mínimo recomendado.
### O24 · Momento de exigir redes independientes

Estado: **user_choice**.

- **A · Antes de cualquier release pública** — Exigir al menos dos rondas con dos identidades reales en redes independientes antes de publicar v0.2.0. Mantiene el criterio completo de entrega.
### O25 · Regla para certificar capacidad simultánea

Estado: **user_choice**.

- **A · Escalar hasta primer fallo** — Probar 2→4→8→16 y certificar el último escalón que cumpla criterios. Produce el máximo basado en evidencia.
### O26 · Formato de aceptación auditiva humana

Estado: **user_choice**.

- **A · Dos personas con guion** — Dos testers recorren frases, distancias, ambos roles, pared/puerta y solapamiento; califican comprensión y audio residual. Es el gate mínimo repetible.
### T01 · ¿Qué objetivo de rendimiento debe orientar la configuración inicial?

Estado: **user_choice**.

- **A · 1080p a 60 FPS** — Equilibrio visual y fluidez en el equipo de referencia que indiqués.

Revisar: performance_machine.
### T02 · ¿Qué idiomas deben estar completos en v0.2.0?

Estado: **user_choice**.

- **A · Español completo** — Redacción elegida en P02; estructura preparada para ampliar.

## Música y efectos

### S01 · ¿Qué identidad musical tendrá Let Me Sleep?

Estado: **user_choice**.

- **C · Caja musical y jazz ligero** — Motivo de sueño reconocible con variaciones ágiles para resultados.
### S02 · ¿Cuánta música habrá durante la ronda?

Estado: **user_choice**.

- **C · Entradas puntuales** — Fragmentos durante momentos públicos del modo; nunca revela un rival oculto.
### S03 · ¿Qué estilo tendrán golpes y herramientas?

Estado: **user_choice**.

- **B · Caricatura sonora** — Más elasticidad y golpes expresivos, sin chirridos invasivos.
### S04 · ¿Qué intensidad subjetiva tendrá el zumbido mosquito?

Estado: **user_choice**.

- **A · Suave pero localizable** — Timbre menos agudo; aumenta sólo al acercarse mucho.
### S05 · ¿Qué señal auditiva anuncia que te están picando?

Estado: **user_choice**.

- **C · Combinación muy sutil** — Foley corto y reacción casi susurrada; revisar accesibilidad auditiva.
### S06 · ¿Cuánto protagonismo tienen los pasos humanos?

Estado: **user_choice**.

- **B · Más discretos** — Pantuflas suaves; correr y metal se distinguen más.
### S07 · ¿Qué densidad de ambiente debe tener cada mapa?

Estado: **ceo_delegated_decision**.

- **A · Capas selectivas** — Un fondo suave más fuentes locales concretas.
### S08 · ¿Qué presentación sonora acompaña la victoria y derrota?

Estado: **ceo_delegated_decision**.

- **A · Motivo corto de 3 segundos** — Variación musical ganadora/perdedora, después voz clara.

## Trazabilidad

SHA-256 original: `3d77c07e9f7d45a45086d6e1866e72b29fb6a1e11fd3d412d4b4fd2ff9ceb63e`.
SHA-256 cuestionario: `f322ee5064c963070c0c029b58f5974eb62e68fe4dc4577d8b2a055a24ee6e3a`.
