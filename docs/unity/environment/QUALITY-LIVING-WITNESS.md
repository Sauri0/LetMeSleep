# Estar testigo — reconstrucción artística de entorno

## Brecha observada

Se revisaron las seis imágenes `N:/LetMeSleep/References/EnvironmentQuality-20260912/01.png`…`06.png`, y las capturas reales living-round3 y living-round5 de `N:/LetMeSleep/Validation/Alfa-VisualRecovery`. Round5 es el antes de esta reconstrucción. Sus cojines todavía son prismas, la cortina es una sucesión de listones, sofá y mesa tienen grandes caras planas sin ensambles, y la pared apenas compone una habitación. La luz mejorada de W2 permite ver mejor esos límites; no los resuelve.

Las referencias piden siluetas intencionales y domésticas: madera con cantos y uniones legibles, tapicería con masa y costuras, tela que pliega y cuelga, herrajes pequeños, lámparas con cuerpo y luz compacta, plantas con hojas diferenciadas y profundidad entre habitación/exterior. No se copian las herramientas, crafting, modos, carteles o marcas de los conceptos.

## Primer conjunto en fuente

AlfaQualityMeshes.cs genera mallas en metros con chamfers de radio elegido, lofts acolchados de cinco anillos, superficies de tela plegada con espesor, cuerpos torneados y hojas con nervio. AlfaQualityLiving.cs monta el testigo. Los nuevos elementos no proceden de escalar el mesh de una mesa. Los materiales Quality_* son propios de este conjunto y siguen el contrato W2.

- Sofá con bastidor visto, cuatro patas, faldón, apoyabrazos acolchados, dos almohadones de asiento y dos de respaldo. Cojines pequeños con contorno cosido y volumen abombado; manta con ondulación y caída sobre el borde delantero. El asiento conserva cota de apoyo0.575.
- Mesa de café con tres tablas y dos remates transversales, patas afinadas, faldones entre patas y pequeños tarugos. Tapa0.480 y pickup1005 a0.485; las patas apoyan en el campo de alfombra a0.008. El comedor permanece igual.
- Estante con montantes, remates de pie/coronamiento, canto delantero de baldas y fondo de tablas. Libros con bloque de papel, tapas y lomo redondeado; cesta tejida en balda inferior y planta en maceta torneada sobre la tapa. Mantiene la pared posterior y despeja ventana/puerta.
- Alfombra con contorno recortado, borde de lino y trama albedo real repetible de bajo contraste. El mismo tejido se aplica a tapicería y cortinas con UV métricas: repetición8.33cm, paso de hilo2.6mm, contraste inferior6%; sin normal de ruido.
- Cortinas como paños con pliegues que se abren hacia abajo, dobladillo y presillas; barra cilíndrica y remates. Marco de ventana con capas y montantes, sin modificar los ocho panes transparentes ni sus colliders.
- Puerta del estar conserva pivote y hoja móvil, con cantos propios en sus piezas, metal diferenciado y tarugos. Molduras bajo techo y cuadro original de colinas componen la pared del sofá.
- Lámpara de pie en(0.65,0,3.85), base/asta torneadas y pantalla cónica hueca de pared fina, dobladillos y bulbo interior. Plafón del estar con roseta, cuenco esmerilado y aro. No se añade Light ni se cambia LightAnchor_Living.

La pantalla sigue el contrato W2: opaca con emisión estilizada baja; Base(0.72,0.58,0.42), Emission(0.14,0.07,0.02), Smoothness0.12; interior Base(0.88,0.72,0.50), Emission(0.20,0.10,0.03). Sin sombras/especular/reflejos de entorno. Window_Glass permanece intacto. Ajuste separado solicitado por W2 tras round4: Lobby_LanternGlow sube sólo su emisión a(0.55,0.25,0.05), conserva base/anchors y desactiva ShadowCaster; el difusor de casa conserva su receta.

## Fuente editable y evidencia

BuildAlfaMaps genera mallas/materiales Unity mediante sus APIs y exporta las mismas piezas a `art_source/unity/environments/quality_living/generated_meshes.json`, con triángulos, UV, materiales, origen y jerarquía, más Quality_LinenWeave.png. `import_generated_meshes.py` reconstruye ese conjunto en LivingWitnessKit.blend editable, con trama empaquetada, cuando Director ceda turno Blender CPU. El export no contiene una imagen conceptual sustitutiva.

Estado de esta entrega: **fuente y compilación offline; todavía sin render nuevo ni .blend generado**. La producción no queda cerrada hasta reconstruir/revisar el .blend y mirar capturas reales. No se abrió Unity, Blender, Play ni audio desde M2. Hash de contenido ampliado a21 entradas para incluir ambos nuevos generadores C#.

## Revisión visual siguiente

Director dispone del Editor23792 para regenerar. Capturar el mismo encuadre de living-round5, más una esquina contraria que incluya la estantería y la lámpara, un detalle bajo de mesa/asiento/manta, ventana a contraluz y puerta entreabierta. Mantener la iluminación W2 para comparar formas. Las vistas neutras del kit y despiece deben salir de los mismos assets, según ASSET-CATALOG-VIEWS.md.

Juzgar si sofá y cojines se leen blandos, si la mesa muestra espesor/uniones sin parecer maqueta, si la tela cae y los apoyos convencen, si las piezas de madera/metal/tela se separan y si pared/lámpara/ventana forman una habitación. Revisar contacto y orden de transparencia; ninguna cantidad de objetos, triángulos o checks constituye aprobación artística. El exterior de la ventana sigue pendiente de profundidad real: no se oculta con una ilustración detrás del vidrio.

Después de revisar el estar completo, propagar el lenguaje aprobado a dormitorios y restantes muebles, lobby, puertas/marcos/luminarias y fachada/patio con vegetación, rocas y suelo apropiados. **Esa propagación y el exterior todavía no están implementados en este lote**. Se mantiene el alcance de mapas alfa existentes y los contratos de circulación/pickups; sin nuevas mecánicas.

## Checkpoint transferible

Reanudado con TEAM-RECOVERY-20260912: responsable **Modelador Elementos**. Conserva temporalmente montaje/puntos comunes de este delta. **Modelador Terreno y Mapas** trabaja exterior en worktree maps y módulo separado; no se le transfieren estos archivos hasta integrar y acordar la división. Presentación/Audio tiene nuevo responsable; los valores W2 históricos aquí recogidos constituyen el contrato recibido, no propiedad actual de Gameplay.

- Worktree `N:/LetMeSleep/Worktrees/environment`, rama `codex/unity-environment`, base anterior4605ea0 (integrado por Director como7d669ef). SHA de esta entrega comunicado al Director; producción gráfica todavía abierta.
- Elementos: AlfaQualityMeshes.cs concentra geometría/materiales reutilizables; AlfaQualityLiving.cs todavía mezcla montaje del estar, muebles, carpintería, luminarias y exportación. Si se separan Modelador Elementos/Mapas, el primero debe tomar geometría/materiales; el segundo, poses y composición. Separar métodos antes de editar ambos el mismo archivo.
- Archivos compartidos: AlfaHouseDressing.cs invoca el testigo y contiene gates; AlfaMapBuilder.cs mantiene el hash, importación y mapa; AlfaLobbyDressing.cs contiene sólo el ajuste de núcleo de farol en este delta. compute_content_hash.ps1 ahora exige21 inputs. Coordinar con Director antes de dividir ownership de estos puntos.
- Blender: import_generated_meshes.py requiere el JSON/PNG que produce el siguiente BuildAlfaMaps. No ejecutar antes ni atribuirle un .blend todavía inexistente. El turno CPU debe producir LivingWitnessKit.blend y receipt; después hacen falta vistas neutras/despiece.
- Evidencia real disponible: living-round3 y round5 son el antes. De esta reconstrucción sólo se verificó compilación offline y sintaxis Python. Pendientes import/build nativo, captura comparable, corrección artística, .blend y pruebas de puerta/pickup/recorrido.
- Sigue después: completar habitación según capturas, profundidad exterior y propagación a alfa. No duplicar ese trabajo con nuevas tareas sin asignación explícita del Director.

### Receta de integración y captura del Director

1. Integrar el commit selectivo de esta entrega sobre el AlfaMapBuilder existente. Recalcular ContentHash con compute_content_hash.ps1 (21 entradas) en la rama central.
2. En el Editor autorizado, Edit mode y escenas generadas cerradas, ejecutar `LetMeSleep.Content.Editor.AlfaMapBuilder.BuildAlfaMaps();`. No iniciar otro Editor desde el worktree de Elementos.
3. Conservar receipt nativo y archivos generados; capturar con Presentation actual el encuadre de living-round5 y los detalles arriba indicados. Revisar apoyos y errores; no aprobar por compilación.
4. Entregar a Elementos generated_meshes.json y Quality_LinenWeave.png, y conceder turno Blender CPU para import_generated_meshes.py. El script crea LivingWitnessKit.blend y receipt, con puntos coincidentes soldados para edición y UV conservadas por esquina. Su sintaxis está revisada, pero su ejecución todavía no se acredita.
5. Revisar el .blend y vistas neutras contra los assets Unity antes de cerrar exportación. La incorporación del exterior y propagación se coordina con Mapas después de revisar el testigo.

## Evidencia crafted1 y corrección siguiente

ec36a946 integrado como b7d9ca6: BuildAlfaMaps nativo PASS el12/09/2026 a19:53:20UTC, Unity6000.3.24f1. Captura real `N:/LetMeSleep/Validation/Alfa-VisualRecovery/living-crafted1.png`, comparada con round5 y referencias. Se ven ensambles de mesa/bastidor, manta colgante y presillas/montantes; **no aprobado**: cojines y respaldo tienen frente plano y borde duro, manta gira de forma abrupta, pantalla apagada, pared dominante y cuadro demasiado elemental. Informe independiente: `N:/LetMeSleep/Validation/TeamRecovery/visual/LIVING-CRAFTED1-REVIEW-20260912.md`.

Export crafted1 recibido de Director: generated_meshes.json SHA256 `3d9b7e9d5492752d52dd87ce5105e8f7e1ddc89acdf86d914fe416ef722d4c05` y Quality_LinenWeave.png. Se verificaron estructura JSON, datos finitos, UV e índices válidos (172 partes/17422 triángulos); no se ejecutó Blender y esos números no valoran calidad. Este export corresponde a ec36a946, no a la corrección siguiente; regenerar/copy antes de construir el .blend actualizado.

Corrección LC1/LC2 en fuente:

- Cojines decorativos y de respaldo: superelipse de contorno continuo y nueve anillos que convergen en el centro convexo, sin tapa frontal plana. Los de asiento conservan zona amplia de apoyo y borde blando; no se les aplica la forma de almohada decorativa. Cojines pequeños inclinados12° y girados±5°, base exacta0.575 tras compensación de bounds.
- Manta desplazada al asiento derecho localx0.46, fuera de la unión de almohadones. Su perfil se calcula sobre los triángulos reales de tapicería, pasa gradualmente por el canto y tiene dobladillo colgante ondulado. El gate compara vértices con esa superficie, no con el plano superior de un BoxCollider: mínimo8 contactos, tolerancia1mm y espesor/ondulación hasta12mm. Se conserva collider funcional del sofá y pickup1005.

Corrección LC3 coordinada con Presentación (perfil abfd63d): nuevo `PresentationAnchors/LightAnchor_Living_StandingLamp` en(0.65,1.37,3.85), inmediatamente después de LightAnchor_Living. Content sólo crea el Transform. Quality_LampShade emisión(0.38,0.18,0.045); nuevo Quality_LampBulb exclusivo emisión(0.70,0.34,0.09). Quality_LampShadeInner y plafón conservan emisión(0.20,0.10,0.03). Pantalla/bulbo no proyectan sombras; base/asta conservan sombras. No se eleva ambiente global.

Próxima evidencia: regenerar, capturar luz de pie ON/OFF sobre **la misma geometría nueva**, vista equivalente a crafted1 y detalle lateral/bajo de sofá–manta–mesa. Comparar contra crafted1 sólo muestra el efecto combinado de geometría/material/luz. Cuadro/composiciónLC4 y exteriorLC5 siguen abiertos; Mapas prepara módulo exterior separado. .blend sigue pendiente de turno autorizado posterior a Humanos y de export actualizado.

Actualización de export editable: tras el turno concedido por Director se produjo y reabrió LivingWitnessKit.blend de **crafted1/ec36a946**, con Blender5.2.1 LTS, PIDs34148/34260 terminados exit0,2 hilos/2 CPU lógicos. La verificación de bounds/caras/materiales/UV/trama está en el .verify.json del kit; no hubo render. Turno liberado. La geometría posterior4f850a5 todavía requiere un export nuevo; README.md del kit identifica sin ambigüedad la versión almacenada.

## Diagnóstico crafted2 y corrección de sombreado

Director regeneró central61956fa a20:16:38UTC, después del arreglo de índices d1d5af8. Revisadas las cinco capturas living-crafted2: ON/OFF, sofá lateral, mesa baja y opposite (esta última obstruida por la pantalla, no sirve para evaluar composición). El export central confirma la nueva geometría: los cojines ahora tienen182 posiciones únicas, frente a72 en crafted1. Aun así, la tapicería conserva apariencia rígida. Las fuentes duplican vértices por triángulo y RecalculateNormals dejaba todas las caras planas.

Corrección aislada: normales textiles promediadas por posición y ángulo de esquina, separadas por material/costura, en cojines, asientos, manta y cortinas. No se alteran medidas, índices de contacto ni colocación; carpintería conserva normales facetadas. El export incluye normales transformadas con matriz inversa transpuesta y el importador Blender las asigna por esquina después de soldar posiciones. El verificador del editable comprueba también su conservación al reabrir. La API utilizada está documentada en https://docs.blender.org/api/5.3/bpy.types.Mesh.html; **la ejecución de esta versión de los scripts sigue pendiente del próximo turno Blender**. JSON y .blend archivados todavía son crafted1.

Separadamente, fa41fd5 corrige emisión persistente de Quality_LampShade/Inner/Bulb, House_Diffuser y Lobby_LanternGlow con BakedEmissive/FixupEmissiveFlag, sin variar colores ni disparar bake. Director encontró keyword_EMISSION desactivada y EmissiveIsBlack tras recarga en standing-lamp-materials.json. Presentación confirmó la causa en el código URP. Comprobar keyword y AnyEmissive tras reimportar; URP puede restaurar ShadowCaster del material opaco, por eso la garantía de lámparas es shadowCastingModeOff en sus renderers.

Compilación offline de fuentes PASS; quedan pendientes build/captura nuevos y comparación lateral idéntica para separar forma de sombreado. LC1/LC2/LC4 siguen abiertos; no se declara aprobación visual. El banco del patio EX4 está aceptado como siguiente delta independiente por Elementos, con pose/colliders/IDs conservados y montaje exterior de Mapas.

## Banco del patio EX4 — fuente para próxima captura

Revisado exterior1-patio-reverse.png real: respaldo continuo y dos apoyos macizos. BuildQualityPatioBench reemplaza únicamente las mallas de Patio_Bench por cuatro tablas de asiento con separación12mm, tres tablas de respaldo con corona15mm y perfil levemente reclinado, dos bastidores de extremos con patines/postes/travesaños y tapones de unión. Se conservan posición(10,0,15.8), rotación, ancho1.8m, fondo0.48m, altura superior del asiento0.54m, respaldo hasta1.12m y los cuatro BoxColliders originales. Los huecos de tablas y bastidores siguen cubiertos por esos proxies simplificados; no se certifica paso de mosquitos por huecos decorativos.

El gate compara las mismas instancias/bounds de collider antes/después y exige que todos los vértices nuevos queden dentro de su envolvente (tolerancia0.1mm por lado). No crea colliders ni altera su orden/IDs. Se añade el banco al JSON del kit editable; el .blend archivado sigue siendo crafted1 y no contiene esta pieza. El método permanece temporalmente en AlfaQualityLiving.cs bajo la propiedad de integración acordada, separado de colocación/arquitectura Mapas.

Offline C# PASS, hash21 actualizado, diff-check sin errores. Pendientes build nativo/gate y captura patio-reverse comparable, más banco aislado frontal y tres cuartos con asiento y apoyo al suelo visibles. Esta fuente todavía no tiene evidencia artística nueva.

## Crafted3 — seguimiento del sombreado real

Director integró fa41fd5 como6759939 y2b43c85 como2ee6e54; BuildMaps PASS20:36:01UTC. standing-lamp-materials3.json confirma keyword_EMISSION y BakedEmissive persistentes. Capturas ON y sofá lateral revisadas: pantalla cálida ahora visible, pero los textiles siguen mostrando caras duras. Banco4a62d6b todavía no integrado en estas capturas.

La lectura numérica del JSON recién exportado descarta pérdida del suavizado antes de exportar: por submesh de cuerpo, BackPad299 vértices/280 triángulos, Cushion325/280, Seat269/200 y Throw345/216; ninguno de esos triángulos tiene las tres normales iguales (tolerancia angular0.1°). Diferencia máxima entre normales de un mismo triángulo63.10°/45.97°/58.35°/24.73°, respectivamente. Los vértices coincidentes mantienen continuidad dentro de0.023° de precisión float. No se encontró recálculo posterior en Presentation/Gameplay.Unity. Esto no demuestra qué mesh usa la instancia capturada.

`diagnostics/DumpQualityTextileMeshes.cs` preparado para eval_file de Director, sólo lectura: identifica escena/jerarquía, instancia activa, static batching, sharedMesh/asset candidato con IDs y huella de vértices/normales/índices, estadísticas de normales y shaders/keywords. Compila envuelto como método contra API Unity6000.3.24f1; todavía falta ejecución nativa. No modificar perfiles hasta comparar instancia capturada con asset/export.

Presentación informó faroles magenta en UI3, pero UI4 fresco muestra faroles cálidos y los dumps house/lobby indican shader soportado/sin errores, compilación inactiva y emisión correcta. El incidente no se reprodujo; causa transitoria exacta desconocida. No se revirtieron flags ni materiales, y IsPassCompiledfalse aislado no se trató como prueba de fallo.

Director ejecutó el diagnóstico en Humantraining fresca: `N:/LetMeSleep/Validation/Alfa-VisualRecovery/textile-meshes3.json`. Las siete piezas coinciden con su asset por instancia y huella; staticBatch=false, URP/Lit, estadísticas de normales iguales al export. Esto descarta pérdida de normales en la copia CPU inspeccionada. La última captura de esa misma instancia quedó pendiente porque Unity20212 cerró antes del pedido; no atribuir automáticamente el dump nuevo a las imágenes anteriores.

Próximo turno Unity del Director: capturar sofá lateral sobre la misma instancia del dump. Si persiste el aspecto anterior, comparar antes/después de UploadMeshData(false) de esos meshes, con cámara y luz idénticas. Hipótesis todavía sin probar: CopySerialized actualiza datos CPU pero deja un buffer gráfico antiguo. La [documentación UploadMeshData](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Mesh.UploadMeshData.html) explica subida de cambios y conservación de lectura con false; [CopySerialized](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorUtility.CopySerialized.html) documenta copia de propiedades, sin garantizar aquí invalidación de buffers. No se introdujo arreglo de GPU ni cambio de geometría sin esta comprobación. Los assets PC/Mobile RP guardados tienen GPUResidentDrawerMode=0; tampoco se atribuye el problema a ese sistema por conjetura.

Turno nativo siguiente concedido a Humanos (BlenderCPU2), luego cola del Director. Elementos conserva sólo fuentes/revisión hasta nueva concesión; export editable y banco siguen pendientes de su integración/captura correspondiente.

## Probe nativo y corrección constructiva TP1–TP3

Director ejecutó ProbeTextileGpuUpload en Unity PID37448, instancia nueva antes del rebuild del banco/exterior. Evidencia real `N:/LetMeSleep/Validation/Alfa-VisualRecovery/textile-gpu-probe-20260912-211224-230`: before/after idénticos SHA256 `2f803ff95511051ed7dcdc0be24e4cc32aa48609f91c1645dafacb40dc483d4f`, cero píxeles distintos, siete mallas, frame61603 en ambos, controles/restauración/bindings verdaderos. El before revisado **ya muestra normales suaves**. UploadMeshData no cambió esta pareja; no se confirmó la causa histórica de las capturas anteriores ni se añadió un arreglo GPU especulativo.

La imagen nueva separa defectos de construcción: falta plataforma bajo los asientos, respaldos altos sin asiento convincente, contacto mínimo de los cojines pequeños y manta con caras/hem fragmentados y madera visible a través. Revisión independiente TEXTILE-GPU-PROBE-REVIEW-20260912.md: TP1/TP2/TP3 abiertos, forma primero y oscuridad TP4 después.

Delta fuente siguiente:

- Plataforma continua Seat_Support_Deck de1.94×0.035×0.76m, centro(0,0.3775,-0.04) en sofá. Top0.395 coincide con el fondo de los asientos. Se une a bastidores existentes y queda dentro del collider de base original; no se añaden colliders/IDs.
- Respaldos0.92×0.58×0.18m, centro local(x±0.46,0.865,0.205), delante de la madera. Base comprimida25mm y adaptada a la superficie triangulada del asiento, conservando parte superior rellena. Evita el anterior hueco vertical de90mm y que casi todo el almohadón quede embebido en el respaldo de madera.
- Cojines decorativos con compresión basal20mm y centroZ0.035; se conservan inclinación12°, giro±5° y base sobre asiento. El pie deja de depender de un único vértice.
- Manta con quince filas y alturas válidas al abandonar el contorno del asiento: desaparece el fallbackY0 que arrastraba columnas externas a través del bastidor. La cara interior conserva la superficie muestreada y los4mm de espesor se construyen hacia fuera por la normal, también al colgar. La caída pasa por delante del Front_Apron(z frontal−0.42); borde azul lateral reducido a14mm y ondulación final6mm. No se cambia el material ni el suavizado.

Gates añadidos: top de plataforma/fondo de asiento, al menos tres contactos distintos por cojín contra la tapicería real sin penetración mayor1mm, y separación del travesaño frontal para vértices y centroides de triángulos de manta. Se mantiene el gate anterior de contacto de la manta y los contratos de circulación/pickup.

Validación offline: C# compila contra Unity6000.3.24f1; hash actualizado y diff-check sin errores. Un cálculo numérico de las deformaciones propuestas sobre los asientos del JSON nativo encontró11 contactos por respaldo y18 por cojín, sin penetración significativa; para la nueva grilla de manta25 contactos, separación vertical0..9.91mm, cero puntos en el travesaño y mínimoY0.28884. Ese cálculo sirve para contrastar las medidas y preparar los gates; **no ejecuta el generador dentro de Unity ni sustituye sus verificaciones o una revisión visual**.

Pendiente próximo Director: regenerar, verificar gates y capturar lateral equivalente al probe, plano bajo despejado y manta oblicua. Banco ya integrado comoed18ab2 y MapasEX2 como3ee30e9 requieren su rebuild/capturas propios; no se juzgan por esta pareja anterior. Editable Blender todavía archivado como crafted1 hasta recibir export vigente/slot.

### Banco EX4 — primera evidencia colocada

Revisión visual EXTERIOR2-REVIEW-20260912 identifica central6e712a4 y rebuild21:19:12. Abierto exterior2-patio-reverse.png: ya se distinguen tablas separadas en asiento/respaldo y bastidores abiertos. La descripción anterior de una placa y dos bloques queda superada en esa vista. EX4 sigue parcial, P2: la distancia no permite certificar cantos, uniones, comodidad de sección ni contacto físico; no se cambia el banco sin esa evidencia.

Receta próxima para Director, misma colocación/luz: tres cuartos desde(11.65,1.20,14.20) hacia(10,0.58,15.8), FOV50; perfil desde(11.45,0.70,15.8) hacia(10,0.58,15.8), FOV55. Son encuadres propuestos, todavía no ejecutados ni asociados a un actor. Incluir suelo y patas, conservar pose del banco y restaurar cámara después. Hallazgos EX2-A/B/C, fachada y vidrio quedan con Mapas/Presentación; no se atribuyen a geometría del banco.

## Actualización residente de mallas y cierre de manta

ee572af integrado como60754dc, buildPASS21:26:31. Abiertas las cinco living-crafted4: plataforma/respaldo mejorados, manta todavía fragmentada en lateral/oblicua; low completamente ocluida no sirve para apoyos. Banco tres cuartos muestra cuatro tablas/ tres respaldos/biseles/bastidores; perfil corta arriba/pies. Se auditó el export SHA1feacc7fca51e792f18a721c92c3aab1cf3763945dc953141838abeb56163c53: manta465v/270 posiciones/536tris, un componente cerrado sin aristas abiertas, clearance frontal de vértices17.507mm mínimo a altura de travesaño. Trece aristas con orientación incompatible se localizaron exclusivamente en el cierre final del hem, Y0.289..0.300; no explicaban todas las roturas visibles más arriba. Auditoría en Validation/TeamRecovery/elements/CRAFTED4-THROW-TOPOLOGY.json.

Director reinició **sin nuevo rebuild**, PID32520, HEAD3dce610/assets persistidos69e8394. living-crafted4-fresh-sofa-side.png revisada: manta continua y cojines más asentados con los mismos assets. Se acredita discrepancia entre sesión residente y sesión fresca; no se demuestra su causa interna exacta ni un efecto de UploadMeshData (la prueba anterior dio imágenes idénticas).

Director autorizó entonces corrección acotada de persistencia y cap, coordinada con Mapas:

- PersistQualityMesh conserva el asset existente y su GUID/instanceID. Reemplaza datos con Clear(false), indexFormat, SetVertices/SetNormals, UV0/UV1, tangentes/colores presentes, submeshes e índices/topologías, bounds, MarkModified y UploadMeshData(false). No usa CopySerialized para meshes. Conserva normales/UV2 producidas por el unwrap; no recalcula normales después. Gates de identidad y canales después de actualización. Se aplica al kit Quality y a House_Plate bajo Elementos; Mapas modifica su propio builder sin compartir archivos.
- El cierre de la manta se orienta con la tangente de salida de su última fila; la punta final apunta hacia abajo, no al Vector3.back fijo anterior. No se modifica ninguna posición ni espesor. Se exige malla cerrada con pares de aristas en sentidos opuestos después de soldadura posicional a1µm.

Validación offline: compilación C# contra Unity6000.3.24f1; prueba numérica del cap sobre el export nativo pasa de13 a0 conflictos orientando nueve triángulos terminales, sin cambiar vértices. **No se ejecutó Unity ni se verificó la actualización residente desde Elementos.**

Próximo Director, misma sesión de Editor: registrar GUID/IDs y captura de la instancia que usa mallas existentes; ejecutar BuildAlfaMaps en Edit mode con escenas generadas cerradas conforme al contrato; volver a la instancia fresca de juego dentro de esa misma sesión, capturar lateral/oblicua sin reiniciar Editor y guardar dump/JSON. Verificar gate de orientación, mismos GUID de assets, datos exportados y apariencia. Repetir build en la misma sesión permite comprobar la rama de actualización del asset existente; la coincidencia de datos CPU no sustituye los PNG. La vista low debe reubicarse fuera de la superficie oclusora; no desplazar muebles para despejarla.

## Cierre acotado de prueba residente — 2026-09-12

Central 2ce1fb2, BuildMaps PASS comunicado por Director a las 21:52:21, incluido el gate de cierre/orientación de manta. Auditoría de Elementos sobre el export central vigente, soldando posiciones a 1µm: cero aristas sin pareja opuesta o cierre. Esta comprobación es independiente de la prueba de actualización del cojín; el informe CRAFTED4-THROW-TOPOLOGY.json conserva el estado histórico anterior de trece conflictos.

Director ejecutó ProbeQualityResidentUpdates revisado en 60c3e87, PID 37000. Elementos revisó recibo y cuatro PNG en `N:/LetMeSleep/Validation/Alfa-VisualRecovery/quality-resident-updates-20260912-215607-255`, verificando también sus hashes: original → 55% → 80% → original produce dos tamaños claramente distintos y restauración exacta. Todas las etapas conservan frame 480523, meshID 43304, filterID -78978, cameraID -83528 y GUID c5134a576eb744f44bc7690a69990771. Datos CPU, píxeles, cámara y dirty state restaurados; disco intacto y controles estables. Resultado completo y límites en [QUALITY-RESIDENT-UPDATES.md](diagnostics/QUALITY-RESIDENT-UPDATES.md).

La ruta de producción acredita dos cambios visibles de vértices en la misma malla residente. No cambia topología en esta prueba y no demuestra la causa interna histórica de la discrepancia gráfica.

Abiertas living-crafted5-api-sofa-side.png y living-crafted5-api-throw-oblique.png: manta continua y ribete legible. Coincide con revisión independiente `N:/LetMeSleep/Validation/TeamRecovery/visual/MESH-UPDATE-REPEAT-CRAFTED5-REVIEW-20260912.md`: TP3 cerrado sólo para fragmentación gruesa en esas vistas. El efecto perceptible del cap no puede aislarse; persisten línea transversal oscura y caída rectangular. Vista low obstruida, apoyos/contacto de cojines todavía sin cierre. No hay aprobación artística global. No se ejecutaron nuevos nativos desde Elementos.
