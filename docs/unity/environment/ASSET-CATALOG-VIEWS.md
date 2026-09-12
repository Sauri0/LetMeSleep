# Receta de catálogo visual real — entorno alfa

Entrega M2 para el índice HTML del Director y el inventario de QA. Estado: receta y medidas de fuente; **capturas de catálogo pendientes de slot nativo**. No representa renders terminados ni aprobación artística. Cada ficha debe enlazar el asset real y distinguir fuente Blender, prefab Unity generado y contexto de juego. El lote doméstico actual requiere regenerar ambos mapas antes de capturarlo.

## Protocolo de captura

Usar copias de revisión de los assets entregados, materiales reales y escala 1 m; no modificar originales para producir vistas. Fondo gris neutro mate, luz blanca suave, exposición fija, bloom desactivado en vistas neutras. Guardar PNG 1600×1600 para piezas, 1920×1080 para contexto, sin recorte de silueta y con margen del 10%. Orientaciones en coordenadas Unity: frente observado desde −Z, dorso desde +Z, laterales ±X y planta desde +Y. Marcar +Z, escala y pivote en una lámina técnica separada.

Por pieza: frente, dorso, lateral y tres cuartos a 30° de elevación; añadir planta cuando revele apoyo, hueco o unión. Cámara ortográfica en láminas de medidas; perspectiva en tres cuartos. Ajustar encuadre al bounds medido y registrar tamaño ortográfico o FOV. No presentar el color de la iluminación neutra como prueba de la luz final del juego.

Cada imagen debe registrar ID estable, ruta de prefab/nodo, revisión Git integrada, ContentHash del mapa si aplica, motor/versión, material/variante real, cámara, resolución y fecha. Nombre sugerido: `env_<id>_<variante>_<vista>.png`. Un estado sin PNG se publica como «pendiente», nunca con concept art sustituyendo la pieza. Las variantes son únicamente las existentes; una recoloración propuesta no es un asset entregado.

## Cobertura por familia

| Familia / fuente real | Vistas adicionales y contexto | Estado de esta entrega |
|---|---|---|
| Kit alfa: Table, Sofa, Counter, Stove, Fridge, Vanity, Toilet, Washer, Shelf, PatioBench, Pine; `art_source/unity/environments/alfa_maps/furniture_kit_alfa.fbx` | Cuatro vistas neutras por pieza. Planta de mesa/encimera; detalle de asiento, baldas, tiradores y tronco. Contexto dentro de HousePatio o PrivateLobby | Capturas individuales pendientes |
| Cama, mesita, escritorio y silla de la muestra; `art_source/unity/environments/room_sample` | Vistas neutras y conjunto del dormitorio; señalar superficie de apoyo y reserva de acceso | Capturas de catálogo pendientes |
| Puerta de la muestra, reutilizada nueve veces en HousePatio | Frente cerrado, lateral de grosor, planta 0°/−100°, detalle de bisagra y manilla; vano armado y despiece ilustrativo | Capturas y cotas nativas pendientes |
| Shell casa/patio, shell lobby, escalera y hastiales | Exterior cuatro esquinas; plantas por piso; corte de revisión ocultando techo en una copia; detalle muro/suelo, vano/jamba, forjado/escalera y hastial/cubierta | Las capturas anteriores no cubren este protocolo completo |
| HouseDressing generado por AlfaHouseDressing.cs | Alfombras, cortinas/alféizar, libros, tabla, paño, platos y plafón: neutras y contexto de sus apoyos reales | Nueva evidencia del material y paño pendientes |
| Lobby_Domestic y ArchitecturalTrim, generados por AlfaLobbyDressing.cs | Sofá base y vestido; estante vacío y lleno como comparación identificada; libros/cajas/perchero; panelado y lámpara. Cámara MainMenuCamera con y sin UI, más frontal de pared posterior | Nuevo lote: sin captura todavía |

Los despieces se hacen en duplicados de revisión: separar componentes existentes a distancias conocidas y trazar líneas de correspondencia; conservar una vista ensamblada al lado. No inventar tapas internas, herrajes, uniones constructivas ni mecanismos que el asset no contiene. Para el shell continuo, el corte es una vista del mesh existente, no un kit modular entregado.

## Medidas y detalles a verificar al capturar

Metros, ejes X×Y×Z. Las cifras siguientes proceden de generadores C#/Python; confirmar bounds del prefab importado antes de rotularlas como medidas nativas.

| Elemento | Cota de fuente / unión que debe verse |
|---|---|
| Puerta | Paso libre 1.10×2.20; vano estructural 1.24×2.26; jambas 0.07×2.20×0.20; dintel 1.24×0.06×0.20 |
| Hoja y giro | Hoja 1.07×2.185×0.04; borde inferior 0.010 sobre suelo. Pivote de muestra (0.585,0,0.045), centro de hoja relativo (0.535,1.1025,0). Eje +Y, abierto −100° respecto al cierre; alfa guarda inicialmente las nueve abiertas. Mostrar pivote real y barrido, no solo cambiar rotación de toda la puerta |
| Holguras de puerta cerrada | En muestra: jambas interiores x0.570/1.670; hoja x0.585…1.655: 0.015 por lado. Hoja llega a y2.195: 0.005 bajo dintel. Confirmar manillas y bisagras en detalle de unión durante barrido |
| Encuentros de arquitectura | Muros 0.18; niveles de piso 0/3.00; altura libre 2.80. Escalera: ancho libre 1.60, 18 contrahuellas de 1/6, huella 0.28, rellano 1.60. Capturar continuidad, hueco de forjado y contacto hastial/cubierta |
| Sofá | Asiento 1.82×0.17×0.72, centro y0.49: apoyo superior y0.575. Textil lobby base y0.575; cojín 0.36×0.30×0.14; paño 0.40×0.012×0.60. Mostrar contacto, respaldo y brazos |
| Estante | Laterales 0.08×1.70×0.35 en x±0.56; baldas 1.04×0.06×0.35 en y0.08/0.58/1.08/1.65. Superficies y0.11/0.61/1.11/1.68; fotografiar bases de libros y cajas sobre ellas |
| Cajas lobby | Cuerpo 0.42×0.32×0.27, base y0.11; tapa 0.44×0.02×0.29 con base y0.43. Mostrar tirador, tapa y apoyo en balda |
| Encimera y paño | Encimera superior y0.880; paño altura0.025, base0.880; banda altura0.002, base0.905. Primer plano rasante que permita detectar flotación |
| Alféizar y estante del estar | Alféizar z0.155…0.255 y estante mínimo z0.275: separación0.020. Vista lateral y planta con ambos objetos visibles |
| Plafón | Vista inferior del difusor y despiece de soporte/marco/difusor; comparar luz de escena encendida con vista neutra. Registrar emisión actual(0.18,0.10,0.035) de House_Diffuser y Lobby_LanternGlow |
| Suelo de roble | Tabla0.32×1.28, repetición1.28×2.56, junta nominal/ráster0.005. Planta cercana sin perspectiva y vista humana para valorar escala y repetición |

Para el resto del kit, QA debe extraer dimensiones finales del bounds de renderers y collider por separado, incluidos tiradores y biseles. No confundir tamaño del cuerpo principal con envolvente total ni follaje visual con colisión del pino.

## Evidencia actual y siguiente captura

`N:/LetMeSleep/Validation/Alfa-VisualRecovery/menu-1080.png` es evidencia real del **estado anterior** al nuevo dressing: paneles azules provisionales y estante vacío. Sirve como antes, no como miniatura actual del lote. Compilación offline Unity 6000.3.24f1 registrada en UNITY-BUILDER-OFFLINE-CHECK.json; no demuestra importación, apoyos ejecutados ni apariencia.

Prioridad de la próxima sesión autorizada: regenerar mapas, ejecutar comprobaciones de builder, capturar nuevo menú con ajustes W2 y detalle de sofá/estante; después puerta ensamblada/despiece y cuatro vistas por pieza. Director administra Editor/GPU y publicación del índice; M2 entrega fuentes y receta, QA vincula inventario/evidencia. No se abrió Editor, Blender, Play ni audio para esta entrega.
