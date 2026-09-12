# Let me sleep 0.9.4/alfa — contrato de equipo

Estado: AUTORIZADO por objetivo activo de Branko. Solo alfa. Beta requiere aprobación posterior.
Director repo: C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir.
No trabajar en el cwd OneDrive ni reutilizar ramas antiguas 091/092.

## Requisitos de aceptación

| ID | Entrega requerida | Evidencia de cierre |
|---|---|---|
| A01 | Casa fija deliberadamente diseñada, sin generación aleatoria en flujo jugable | mapa canónico, partidas sucesivas y entrenamiento cargan la misma geometría |
| A02 | Casa coherente, circulaciones, escaleras, descansos y puertas accesibles | medidas reales, recorridos físicos y revisión visual |
| A03 | Exteriores y patio jugable con accesos, caminos, cercas, vegetación y mobiliario | datos, colisiones y recorrido interior/exterior por humanos y mosquitos |
| A04 | Muebles/objetos apoyados, tareas alcanzables, spawns separados | comprobaciones geométricas, interacción real y bots |
| A05 | Sin parpadeos ni cortes de superficies; materiales y sombras coherentes con referencias | revisión visual en movimiento, no solo tests de cajas |
| A06 | Selector solo administrador; un mapa disponible; carga sincronizada | peticiones autorizadas/rechazadas y pares de clientes |
| A07 | Sala independiente, listo, ajustes, roles aleatorios y regreso entre rondas | sesión completa con distintas composiciones de equipo |
| A08 | Online entre dos conexiones independientes, varias rondas y cierres | prueba WAN con dos participantes; host EOS aislado NO suficiente |
| A09 | Entrenamiento con bots en casa y patio | ambos roles y tareas accesibles |
| A10 | Versionado y launcher orden alfa/beta/omega/delta/gamma, incompatibilidad explícita | selección de release, instalación, actualización y rechazo de versión distinta |
| A11 | Rendimiento del alcance medido y corregido | datos de equipo/resolución/escenario y tiempos de cuadro reales |
| A12 | Código, ZIP y checksum publicados y descargados/verificados; launcher los obtiene | recibos de build, release y descarga |

## Referencias y límites

Referencias locales: work/references094/characters.png y environment.png.
Low-poly con facetas controladas, volúmenes simples, colores definidos, estética cómica. Entorno según segunda lámina. Personajes actuales, solo arreglos indispensables; NO rediseño beta. No inventario, estamina, nuevas picaduras ni mapas delta en alfa.
Mapas fijos significa autoría real, NO fijar una semilla del generador existente y declararlo terminado.
No abrir juego, editor Blender visible ni benchmarks gráficos hasta que Director confirme la respuesta de Branko. Se permiten lecturas, código y pruebas headless sin ventanas. No usar proveedores remotos de pago.
Actualización de autorización: Branko respondió «Podés abrir el juego para las pruebas». Director coordina turnos únicos. WAN con amigos: «cuando pueda en otro momento con amigos», por lo tanto queda sin evidencia hasta esa prueba real.
No exponer LAN/IP/puertos. Usar EOS integrado y conservar entrenamiento.

## Contrato de mapa

- ID nuevo canónico: `house-patio-v1`, etiqueta `Casa con patio`; sala conserva `lobby`.
- MapCatalog debe ofrecer catálogo jugable (id/label) y datos fijos; catálogo no incluye lobby ni mapas futuros.
- Preservar forma de datos usada por Arena/World: bounds, obstáculos, niveles, habitaciones, puertas, spawns, tareas, pickups y grafo de navegación.
- Distinguir metadatos `authored_version`/exterior de `generator_version`; no fingir una generación procedural.
- Propuesta inicial: dos plantas, patio conectado mediante puertas y rutas continuas, escaleras cómodas; dimensiones finales las justifica Modelador 1 con geometría real.
- Modelador 1 publicará esquema y layout antes de que Worker 1/2 integren sus consumidores. Publicará API exacta del catálogo antes del selector.
- Director posee validación de configuración, arranque de ronda y huella/identidad de mapa en red.

## Propietarios

| Chat | Copia y rama | Archivos propios |
|---|---|---|
| Modelador 1 | lms094-house / codex/094-house | map_catalog.gd, fixed_house.gd y auxiliares fixed_house*, house_validation.gd solo si imprescindible; tests house094_* |
| Modelador 2 | lms094-assets / codex/094-assets | art_source/environments/alfa/**, game/assets/art/house/alfa/**, alfa_library.gd; tests assets094_* |
| Worker 1 | lms094-motion / codex/094-motion | arena.gd, map_navigation.gd, navigation_geometry.gd, client.gd, puertas/interacciones si se acuerda archivo; tests motion094_* |
| Worker 2 | lms094-environment / codex/094-environment | world.gd, house_details.gd, frame_joinery.gd, house_occlusion.gd, materiales/shaders de entorno; tests environment094_* |
| Revisar interfaz visual | lms094-ui / codex/094-ui | ui.gd y recursos UI exclusivos; tests ui094_* |
| Revisión funcional | lms094-qa / codex/094-qa | tests review094_* y work/review094*; no modifica runtime |
| Director | dejame-dormir / codex/094-alfa | network.gd, simulation.gd, lobby_rules.gd, EOS, versión/protocolo, launcher/**, build/release/docs y tests de red/versionado |

Raíz de copias: C:/Users/brank/Documents/Codex/2026-09-06/.
Nadie está solo. No revertir cambios ajenos, no git add ., no reset/clean, no publicar ni exportar por separado. Commits selectivos. Cambios fuera de propiedad se piden a Director. No iniciar nuevos subagentes.

## Tandas

1. Modelador 1 diseña/implementa mapa; Modelador 2 produce recursos reutilizables; Revisión funcional prepara auditoría independiente.
2. Con contratos concretos: Worker 1 integra funcionamiento, Worker 2 acabados y UI selector/sala. Director avanza versionado y red en paralelo.
3. Integración, pruebas focalizadas y correcciones; GPU por turnos autorizados. WAN requiere participante y segunda red reales.
4. Publicación de alfa únicamente con evidencia de todos los requisitos. Si falta prueba, mantener objetivo activo y explicar el límite. Branko prueba y decide si habilita beta.

## Integración del 12 de septiembre

- Casa a637f07, assets d7d3d1e, movimiento a470200, UI adc9e3a y entorno 15b4e83 incorporados por Director mediante commits selectivos.
- QA tiene autorización temporal sobre fixed_house.gd para validar/corregir la geometría tras el checkpoint M1; su ajuste 3d34628 agrega cercas y alinea colisiones con los modelos. M1 congeló sus cambios para evitar trabajo simultáneo.
- Worker 1 amplió propiedad a practice_session.gd y bot_brain.gd. Entrega funcional integrada; no modifica red ni simulación.
- Interfaz tiene autorización adicional sobre ui_navigation_test.gd para conservar su cobertura con el inicio online actual.
- Revisión visual de assets: 383 comprobaciones, sin errores, tres láminas nativas en work/assets094-native. Es revisión de modelos aislados, no aprobación del mapa integrado.
- Red Director: par local con transporte ENet de prueba, 39 comprobaciones incluyendo dos rondas, ajustes de dueño, rechazo de mapa/huella y desconexión. Host EOS real: 15 comprobaciones. Ninguno acredita conexión WAN.
- Rama remota de integración: codex/094-alfa. No es la release ni actualiza el juego de los jugadores.
- Pendiente: cerrar mapa renderizado, navegación UI nativa, partidas largas/rendimiento, paquete/launcher y prueba con amigo desde otra red. Sin aprobación para beta.
