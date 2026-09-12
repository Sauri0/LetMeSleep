# Let me sleep — migración a Unity y ciclo 0.9.4
Estado: PROPUESTA PARA REVISIÓN DE BRANKO. No autoriza ejecutar la migración ni reactivar tareas.
Orden obligatorio: alfa → beta → omega → delta → gamma.
Cada entrega tiene alcance cerrado, pruebas, descarga verificable y aprobación de Branko antes de iniciar la siguiente.
Fecha: 2026-09-12.

## 1. Visión y reglas que gobiernan el plan
Juego multijugador cómico de humanos contra mosquitos, descargable para Windows. Humanos en primera persona durante las rondas; mosquitos en tercera. Sala de espera 3D independiente; propuesta de tercera persona para humanos en la sala. Entrenamiento con bots para probar ambos roles.

Nombre: Let me sleep. Tres modos: Sangre, Supervivencia y Tareas. Las imágenes son referencias de dirección artística y composición; sus textos no crean requisitos. No incorporar por las imágenes Bite & Build, crafting, construcción, clases con estadísticas, armas de fuego, misiones diarias, niveles, tienda, recompensas, minimapa, inventario de seis espacios ni modos adicionales.

- Crear sala dentro del juego inicia el anfitrión; amigo pega un código. Sin IP, puertos, LAN ni programas externos en el flujo del jugador.
- Mantener EOS como primera opción para identidad, descubrimiento y transporte P2P/relay; comprobar integración y compatibilidad con Unity antes de expandir contenido.
- El código resuelve una sala en el servicio: no sustituye la conectividad. Si el anfitrión se va, cierre claro para todos; migración automática de anfitrión fuera del alcance.
- Roles sorteados cada ronda. Cantidad humana configurable de 1 a 5; opción automática también sortea cantidad válida, siempre con ambos bandos. Sin requisito de dos mosquitos por humano.
- Capacidad inicial propuesta: hasta 16 participantes totales, condicionada a validación de rendimiento/red. No prometer capacidad ilimitada.
- Eliminar las marcas y la rotación de zonas de picadura en todos los modos. Se pica por proximidad/contacto válido con el cuerpo, sin atravesar obstáculos.
- Mirada y alcance manual para defenderse. Cámara debe mostrar el cuerpo alcanzable y seguir girando libremente.
- Mosquito vuela hacia donde apunta con W y frena al soltar; puede posarse y caminar en piso, pared y techo.
- Llenar completamente la extracción sin que el humano se lo quite provoca desmayo del humano. Duración vinculada al tiempo de caída del mosquito; valores concretos deben probarse, no heredar 35 segundos por inercia.
- Un humano solo debe poder defender todas las superficies expuestas a picaduras. Con compañeros se permite defensa cooperativa de espalda; el respaldo geométrico no implica volver a dibujar marcas.
- Personalización en menú principal, con giro, zoom, vista frontal/perfil/espalda y guardado.
- Predeterminado humano: pijama, pantuflas y gorro de noche.
- Mapas fijos y selector del anfitrión. Se elimina la generación procedural de distribución.
- Inventario de tres espacios y estamina incluidos en el ciclo.
- Voz cercana entre ambos equipos; mosquito agudo, más bajo y de menor alcance para humanos; entre mosquitos mejor inteligibilidad.
- FPS sin límite por defecto, con límites y sincronización configurables. Objetivo de referencia: 1080p/60 FPS en GTX 1660 Ti con resto del equipo documentado. 1440p/4K requieren perfiles y medición en hardware apropiado.

## 2. Dirección artística aplicable
Low-poly cómico y expresivo, siluetas claras, volúmenes simples, facetas controladas y superficies limpias. Personajes graciosos para todas las edades. Mantener superficies grandes lisas cuando favorezca lectura; no convertir cada pared en ruido triangular.

Humanos: cuerpo simplificado y compacto, extremidades coherentes, ojos expresivos, cejas y bocas fáciles de distinguir. Eliminar bultos en cachetes, dedos que doblan hacia afuera y piezas superpuestas.
Mosquitos: abdomen alargado, alas finas, patas articuladas y escala pequeña respecto al humano. Variantes cosméticas sin alterar la hitbox ni el balance. Alas visibles con transparencias controladas.
Entorno: casa y exteriores habitables, proporciones funcionales, mobiliario colocado por uso y circulación. Luces cálidas interiores, iluminación ambiental exterior coherente, sombras de contacto y reflejos contenidos.
UI: jerarquía, botones grandes y paleta nocturna con acentos inspiradas en las referencias; iconos propios, texto breve y solo funciones existentes.
Audio: travesura nocturna, jazz juguetón, pizzicatos y percusión suave. Señales de peligro e interacción por encima de música decorativa.

Producción: archivos Blender editables + exportaciones comprobadas en Unity. Escala, ejes, origen, nombres, rig, materiales, sockets de agarre y colisiones documentados. Una muestra real aprobada antes de producir variantes. Las imágenes conceptuales no acreditan calidad del juego.

## 3. Migración y organización técnica
Preparación ya instalada: Unity 6000.3.24f1 LTS, Hub y licencia Personal. Propuesta de proyecto Unity URP/C#, con versiones fijadas.
Nueva copia del repositorio en N:, propuesta N:/LetMeSleep/Repository; proyecto Unity dentro de unity/. Conservar historial y versión publicada Godot como referencia recuperable. No mover ni eliminar la copia previa durante el arranque.
Inventario inicial de funcionalidades: conservar comportamiento, reescribir, sustituir o retirar. Portar pruebas por intención, no copiar scripts Godot como si fueran código Unity.
Conservar/revisar: reglas confirmadas, EOS ya provisionado, launcher, audio y fuentes de arte aprovechables. Rehacer: escenas, controladores, cámaras, física, interfaz, integración de red y rig que dependen del motor.
Prever importación de ajustes/cosméticos antiguos con respaldo y equivalencias; no reutilizar sin validar IDs, identidad de dispositivo ni formato de guardado.
Módulos: Core, Gameplay, Online, Presentation, UI, Audio, Content y Tests. Regla autoritativa en anfitrión; presentación suave en clientes. Separar definición de objetos/mapas/cosméticos de la lógica.
Escenas: inicio, menú, sala independiente y mapas. Catálogos por datos. Carpetas y ensamblados separados.
Director fija paquetes y API entre áreas. Evaluar transporte EOS y biblioteca de sincronización con una conexión funcional antes de comprometerse a una combinación; EOS por sí solo no resuelve simulación ni interpolación.
Unity text serialization y archivos .meta versionados. Fuentes pesadas de arte con Git LFS cuando corresponda, revisando disponibilidad/límites; nunca credenciales privadas en Git.

## 4. Equipo y propiedad propuesta
No se han enviado tareas de ejecución. Reasignación pendiente de aprobación del plan.
Las tareas existentes son Director, Modelador 1, Modelador 2, Worker 1, Worker 2, Revisar interfaz visual y Revisión funcional.

| Tarea | Responsabilidad y archivos propios |
|---|---|
| Director | Arquitectura, Core/Online, EOS, contratos, integración, versiones, launcher, compilación, GitHub y coordinación. Único integrador/publicador. |
| Modelador 1 | Personajes humanos/mosquitos, rig, manos, ropa, caras, accesorios, animaciones fuente y prefabs de personaje. |
| Modelador 2 | Mapas, arquitectura, mobiliario y vegetación; fuentes Blender, prefabs de entorno y escenas de mapas. |
| Worker 1 | Gameplay: movimiento, cámara, interacción, picaduras, defensa, modos, bots, inventario y estamina. |
| Worker 2 | Presentation/Audio: conexión del rig con gameplay, iluminación URP, materiales, VFX, sonido/música, reproducción de voz y optimización de render. |
| Revisar interfaz visual | UI: menú, lobby, HUD, personalización, ajustes, accesibilidad, guías y estados de conexión. Revisión visual junto al autor del área. |
| Revisión funcional | Tests e informes independientes de jugabilidad, red, instalación y regresiones. Devuelve defectos reproducibles; no modifica runtime ajeno. |

No cambiar los modelos de IA configurados desde este plan. Distribuir por responsabilidad, no activar todas las tareas sin dependencias listas.
Cada tarea recibe: versión, objetivo, archivos, dependencias, criterios de aceptación y formato de entrega.
Trabajo aislado en ramas/worktrees bajo N:/LetMeSleep/Worktrees. No compartir la misma escena, prefab, .blend o archivo simultáneamente. Worker 2 entrega presets/perfiles para que Modelador 2 integre en escenas; los cambios necesarios se coordinan por Director.
Un solo uso intenso de GPU o editor interactivo por vez. Unity batchmode y Blender por turnos de carga. No dos editores sobre la misma copia.
Checkpoint breve por entrega: commit, cambios, pruebas, pendientes y próximo paso; se consulta ese estado antes de reanudar tras reinicios. Director no reescribe por su cuenta el trabajo activo de otro.
QA reproduce el fallo, el propietario lo corrige y QA verifica. Repetir pruebas solo sobre cambios o riesgos relevantes; mediciones y artefactos asociados al commit exacto.

## 5. 0.9.4/alfa — nueva base Unity, arte real y primera partida online
Objetivo: una entrega pequeña en variedad pero completa en funcionamiento: Sangre jugable en la nueva casa con patio, con el aspecto nuevo desde el inicio.

1. Respaldo e inventario de migración; proyecto Unity y organización por módulos en N:.
2. Conexión EOS inicial: dos identidades, creación, código, unión y transporte de estado. Validar el riesgo online antes de construir el mapa completo.
3. Muestra en motor: humano en pijama, mosquito, habitación, puerta y objetos. Mostrar frente/perfil/espalda y movimiento; Branko aprueba dirección antes de ampliar.
4. Modelos base de producción, manos correctas, rig y animaciones de caminar, correr, agacharse, saltar, girar, palmada, vuelo, posado, picadura y caída.
5. Cámaras: humano primera persona con cuerpo visible y giro sin bloqueo; mosquito tercera persona sin atravesar paredes; lobby tercera persona propuesta.
6. Nueva picadura sin marcas, desprendimiento explícito, defensa manual, impacto visible y desmayo. Resolver el ciclo de caída/vidas antes de fijar esta lógica (decisión al final).
7. Casa fija de dos pisos con patio, recorridos lógicos, escalera cómoda, habitaciones con función, puertas y colocación real de mobiliario.
8. Sala de espera 3D separada, admin, ajustes esenciales, listo, sorteo, inicio, fin y regreso entre rondas.
9. Modo Sangre completo con cuota compartida, tiempo y resultado; entrenamiento de ambos roles con bots. Otros modos todavía no aparecen como jugables.
10. Menú nuevo: Jugar online, Entrenamiento, Personalizar, Ajustes, Salir. Crear/Unirse con código, estados comprensibles y Escape/Volver coherentes.
11. Personalización base funcional: color de piel/pijama y mosquito, visor con zoom/giro. El catálogo amplio corresponde a beta.
12. Manos + una herramienta de prueba de producción con agarre, tamaño, alcance e impacto distintos. El catálogo completo corresponde a omega.
13. Audio de acciones esenciales, ambiente y pieza de menú; iluminación y materiales completos para el contenido alfa.
14. Compatibilidad de versiones y adaptación del launcher al paquete Unity. Instalación limpia en carpeta elegida, actualización comprobada, código y artefactos descargables.

Reparto: Director migración/red/build; M1 personajes base; M2 casa/patio/lobby; W1 control/combate/Sangre/bots; W2 animación en juego/luces/audio; UI flujo completo alfa; QA ronda, superficies, WAN y launcher.

Cierre: ambos roles útiles, ronda y regreso funcionales, arte real revisado en movimiento, sin bloqueos conocidos del alcance, instalación desde GitHub verificada. Dos equipos en redes independientes juegan varias rondas. Si falta amigo, puede existir candidata descargable pero no se declara online aprobado ni se pasa automáticamente a beta.
Objetivo copiable: Completar 0.9.4/alfa en Unity según este alcance, con nuevo arte, casa/patio, lobby, Sangre, entrenamiento, online integrado y distribución verificada. Detenerse para prueba y aprobación de Branko antes de beta.

## 6. 0.9.4/beta — tres modos y personajes personalizables
Objetivo: cerrar la identidad de los personajes y toda la estructura de modos sobre la alfa aprobada.

1. Catálogo humano propuesto y finito: 8 peinados contando calvo, 6 opciones de vello contando ninguno, 6 ojos, 6 cejas, 6 bocas; pijama más 4 conjuntos combinables, 6 accesorios de cabeza contando gorro nocturno, 4 gafas contando ninguna, 4 calzados contando pantuflas. Colores por pieza y tonos de piel. Cantidades propuestas para aprobar, no obligación de reproducir todas las láminas.
2. Catálogo mosquito propuesto: 3 cuerpos de silueta equivalente en juego, 4 alas, 6 ojos, 6 cejas/expresiones, 5 patrones y 6 accesorios contando ninguno; paletas por partes. Probóscide/patas alternativas solo si caben sin interferencias ni ventaja.
3. Cejas, bigotes y barbas vinculados al pelo; opción explícita para separar color si se decide incluirla. Ocultar pelo bajo accesorios cuando evita clipping.
4. Parpadeo, mirada, boca y expresiones de reposo/esfuerzo/golpe/desmayo; animación estable desde cualquier ángulo.
5. Personalizador en menú con categorías, miniaturas reales, giro, zoom, restablecer, aleatorio y guardar presets.
6. Emotes humanos: propuesta inicial saludar, señalar, reír y celebrar, sincronizados y cancelables por acciones de juego.
7. Supervivencia: basta que sobreviva un mosquito; bots adecuados, eliminación y espectador, resultado y revancha.
8. Tareas: asignaciones personales legibles, objeto y acción identificables, progreso e interrupción. Meta compartida. Fallar acorta solo el plazo del responsable, con mínimo razonable y recuperación de margen propuesta al acertar. No recorta la ronda.
9. Picaduras y desmayos interrumpen tareas: la interrupción debe ser comprensible y dejar margen para defenderse. Asignación según distancias y rutas disponibles.
10. Resolver y aplicar vidas personales de Tareas, ayuda y eliminación definitiva; referencia del usuario: 3 vidas y victoria humana si todas se agotan.
11. HUD y tutorial contextual por modo; selección de modo en sala; bots y balance inicial para los tres.
12. Integrar, probar online, empaquetar y verificar actualización desde alfa.

Reparto: M1 variantes/rostros/emotes; M2 objetos y ubicación de tareas; W1 modos/bots/interrupciones; W2 integración facial y efectos; UI personalizador/HUD; Director sincronización/persistencia; QA combinaciones y modos.
Cierre: ningún botón cosmético sin función; combinaciones compatibles revisadas con matrices automatizadas y muestras visuales de perfil/en movimiento, además de todas las piezas individualmente; tres modos explicables y terminables.
Objetivo copiable: Completar 0.9.4/beta con personalización modular, rostros y emotes, Supervivencia y Tareas, balance e interfaz de los tres modos, publicación y actualización verificadas. Esperar aprobación de Branko antes de omega.

## 7. 0.9.4/omega — herramientas, estamina, voz y audio completo
Objetivo: enriquecer la interacción sin perder controles claros ni romper modos ya terminados.

1. Inventario de tres espacios: recoger, cambiar, soltar, objeto activo y manos siempre disponibles sin consumir un espacio; propuesta de funcionamiento a aprobar.
2. Catálogo inicial propuesto: manos, pantufla, diario enrollado, matamoscas, raqueta eléctrica y aerosol. Cada objeto con utilidad, alcance, área, cadencia y recursos propios cuando correspondan.
3. Clic izquierdo para golpe/uso; mantener derecho y soltar para lanzar los arrojables. Señal de potencia, cancelación, trayectoria e impacto comprensibles; no todos los objetos deben poder lanzarse.
4. Agarre y orientación por objeto en primera/tercera persona; manos que envuelven el mango; clips y sonido por material. Objetos en mesas, perchero, cocina o mueble lógico.
5. Estamina con esfuerzo y recuperación legibles. Propuesta: correr y acciones exigentes la consumen, caminar y defensa básica siguen disponibles; definir costes por rol sin limitar el vuelo normal de forma sorpresiva.
6. Balance de extracción, defensa, cadena de desmayos, herramientas y ayuda entre compañeros. Ventanas de recuperación para evitar bloqueo permanente.
7. Voz espacial online: dispositivos, pulsar para hablar, volumen y silencio propio/ajeno. Ambos bandos cercanos; filtro agudo mosquito sin acelerar el habla, atenuación distinta según oyente, puertas/pisos afectan audición.
8. Boca reacciona al audio de voz real; no presentar análisis de volumen como reconocimiento exacto de palabras. Pausa al silenciar/desconectar.
9. Música original o debidamente licenciada de menú/sala/ronda/tensión/final, con transiciones; pasos, alas, superficies, puertas, herramientas, picadura, carga y caída distinguibles.
10. Parámetros de mezcla para que zumbidos, golpes y voz sean audibles; presets y accesibilidad sonora.
11. Pruebas de abuso/duplicación de objetos, proyectiles en red, latencia, pérdidas, voz y balance humano. Publicar y actualizar desde beta.

Reparto: W1 inventario/objetos/estamina; M1 agarres/animaciones; M2 objetos y colocación; W2 audio/voz/filtros/VFX; UI inventario/controles/audio; Director transporte y sincronización de objetos/voz; QA combate y comunicación.
Cierre: cada herramienta se entiende al usarla, no hay objetos invertidos ni ventajas por cosmético, conversación y juego funcionan entre casas, tres modos siguen completos.
Objetivo copiable: Completar 0.9.4/omega con tres espacios, herramientas diferenciadas y lanzamientos cargados, estamina, voz espacial con filtro mosquito y audio/música, validación y publicación. Esperar aprobación antes de delta.

## 8. 0.9.4/delta — catálogo de mapas fijos
Objetivo: variedad de lugares completos usando la biblioteca y sistemas ya aprobados.

1. Casa con patio continúa mantenida.
2. Isla pequeña en medio del mar: refugio/construcción apropiada, costa, recorridos y límites claros; sin añadir navegación en barco o supervivencia ambiental por un dibujo.
3. Campamento en pantano de noche: tiendas/refugios, pasarelas y suelo transitable, visibilidad jugable y ambiente propio.
4. Dos mapas adicionales que Branko aceptó como “ambos”: sus nombres no se recuperaron con certeza del contexto disponible. Confirmar cuáles eran antes de cerrar delta; no reemplazarlos por nombres inventados tomados de las láminas.
5. Selector con miniaturas reales, nombre, descripción breve y ajustes de admin; todos cargan la misma versión del mapa. Sin opciones vacías.
6. En cada mapa: rutas humanas y de vuelo, posado, puertas donde corresponda, spawns separados, tareas temáticas accesibles, soportes de objetos, bots, límites, cámaras y acústica.
7. Tres modos completos en cada mapa, incluso tareas de preparar el lugar para descansar en exteriores.
8. Iluminación y audio propios, materiales compartidos donde sirve, optimización por distancias y oclusión. Preservar lectura del mosquito y cuerpos.
9. Revisión en movimiento de uniones, marcos, techo, colisiones, pendientes, escalones y luz a distintas distancias.
10. Descarga/publicación/actualización desde omega comprobadas.

Reparto: M2 mapas/arquitectura; W2 iluminación/agua/ambiente/perfiles; W1 tareas/bots/rutas; UI selector; M1 accesorios temáticos y revisión de poses solo si caben en catálogo aprobado; Director carga/red/build; QA matriz mapa × modo × rol.
Cierre: todos los mapas acordados terminados y jugables con los tres modos; no se elimina un mapa para declarar cerrada la entrega sin aprobación de alcance.
Objetivo copiable: Completar 0.9.4/delta con el catálogo fijo acordado, isla marítima y pantano nocturno más los dos mapas por confirmar, selector y todos los modos/bots/online verificados. Esperar aprobación antes de gamma.

## 9. 0.9.4/gamma — cierre del ciclo y distribución definitiva
Objetivo: cerrar integración, rendimiento y calidad de todo el alcance acordado.

1. Auditoría completa por mapa, modo, rol, cosmético, herramienta y flujo; defectos asignados al dueño y verificados de forma independiente.
2. Perfilar builds Windows: CPU/GPU, tiempos de cuadro, picos, memoria/cargas y coste de red/voz; medir cargas reales de participantes y cosméticos.
3. Ajustar LOD, batching, sombras, luces, materiales/transparencias, oclusión, física, animación a distancia y audio según perfiles. Evitar optimizaciones que cambien reglas físicas.
4. Objetivo 1080p/60 en GTX 1660 Ti y equipo documentado; 1440p/4K con GPU apropiada. Sin límite predeterminado; opciones de límite, VSync, resolución y calidad. No certificar equipos no medidos.
5. Afinar cámara, transiciones, rostros, manos, herramientas, legibilidad nocturna, guías y accesibilidad.
6. Online prolongado entre redes distintas, varias rondas, caída del anfitrión, entrada tardía según regla de sala, versión incompatible, voz, latencia y errores de servicio.
7. Launcher con identidad visual propia, instalación inicial en carpeta elegida, actualización por orden explícito alfa<beta<omega<delta<gamma, comprobación de integridad, recuperación y mensajes claros.
8. Validar transición desde 0.9.3 y etapas Unity, conservación/importación de ajustes, controles y cosméticos compatibles; avisar de opciones sin equivalencia sin borrar el original.
9. Revisión de licencias de assets y dependencias, secretos, instrucciones para amigos y changelog real.
10. Código, fuentes de arte relevantes, build, checksum y manifiesto relacionados al mismo commit; descargar y probar lo publicado. Sin trabajo anunciado como hecho cuando solo existe en una rama.

Reparto: Director integra/publica; QA verifica; W1 funcionalidad; W2 rendimiento/audio; M1 personajes; M2 entorno; UI navegación/launcher con Director. Corregir por propietario.
Cierre: todas las entregas y observaciones de Branko cerradas, defectos bloqueantes resueltos, límites conocidos declarados y artefactos descargables verificados.
Objetivo copiable: Cerrar 0.9.4/gamma con regresión integral, pulido y rendimiento medidos, online real y launcher/actualización verificados, código y release publicados. Entregar informe final de alcance y evidencia.

## 10. Secuencia operativa y control del alcance
Antes de empezar alfa: Branko revisa este plan y se resuelven decisiones que afectan al núcleo.
Dentro de alfa: contrato de arquitectura → prueba EOS y muestra visual en paralelo por equipos distintos → aprobación de muestra → producción y gameplay con contratos listos → integración frecuente → QA → candidata → amigos → correcciones → release y aprobación.
En cada etapa: no iniciar automáticamente la siguiente ni activar trabajos de una etapa futura. Tener una funcionalidad programada para omega no es omitirla de alfa; omitir algo comprometido en alfa sí impide cerrarla.
No prometer plazos/tokens antes de medir un lote representativo. Reutilizar bibliotecas, plantillas de rig/objetos y contratos. Presupuesto de esfuerzo por bloque y punto de control antes de ampliarlo.
No usar documentación, pruebas o render de Godot como evidencia de la versión Unity. Sirven para conocer errores y comportamientos que probar.

## 11. Decisiones pendientes para aprobar
1. Ciclo mosquito: las instrucciones históricas piden vidas personales (Tareas: 3) y la versión actual usa caídas con ayuda. Propuesta: en Sangre golpe = caída recuperable; en Tareas golpe = caída con oportunidad de ayuda y descuento de una vida al completar la recuperación/respawn sin rescate; Supervivencia eliminación sin respawn. Esta propuesta NO es una regla confirmada: acordar cuándo consume vida y cómo participa el rescate antes de alfa. Mantener duración del desmayo humano vinculada a la caída, con balance nuevo.
2. Nombres de los dos mapas adicionales ya aceptados. No bloquear preparación de alfa por esto, pero sí cerrar catálogo antes de comenzar delta.
3. Aprobar o ajustar cantidades propuestas de cosméticos y catálogo de herramientas, límite inicial de participantes y reparto del equipo. No reproducir todas las variantes de los bocetos por defecto.

## Fuentes técnicas revisadas
- URP en Unity 6.3: https://docs.unity3d.com/6000.3/Documentation/Manual/universal-render-pipeline.html
- EOS Plugin for Unity: https://github.com/EOS-Contrib/eos_plugin_for_unity
- Instalación local comprobada: N:/Unity/Setup/SETUP.md
Las fuentes respaldan disponibilidad de herramientas; no acreditan todavía compatibilidad ni funcionamiento del juego migrado.

