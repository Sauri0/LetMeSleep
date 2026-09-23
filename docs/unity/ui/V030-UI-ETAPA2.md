# v0.3.0 — UI etapa 2 (personalización, HUD, pausa, resultados y ajustes según UI-06)

Autoridad visual: `docs/v030/GUIA-ESTILO-BOCETOS.md`, UI-06 (pantallas 5–10), UI-01 (HUD), UI-05 (ajustes) y PER-08
(personalización dual). Continúa la etapa 1 (`V030-UI-ETAPA1.md`): misma paleta, cara display LMS Barlow Narrow
Bold (sin Bangers en la UI, según la corrección de dirección de arte 1), Atkinson para el cuerpo, selección azul con
marco de 3 unidades y piso de 21 unidades (14 px a 720p). Sigue siendo uGUI + TMP por código, sin prefabs ni escenas.
Este documento describe el estado después de la pasada de correcciones del director de arte (sección final).

## Estructura del código

`AlfaUiController` es `partial`: las pantallas de esta etapa viven en `AlfaUiController.Customization.cs`, `.Hud.cs`,
`.Settings.cs` y `.PauseResults.cs`. Se conservaron los nombres de GameObject, campos y métodos privados que usan el
foco, las pruebas y los arneses. `LateUpdate` llama a `UpdateStageTwoLayout` (reacomodo de resultados si cambia el
tamaño del canvas y degradados de las listas).

## Personalización (UI-06 5–6, PER-08)

- Cabecera: título PERSONALIZACIÓN y pestañas de rol HUMANO (azul) / MOSQUITO (rojo) con icono y subtítulo.
- Carril izquierdo `CategoryRail`. Modo básico, humano: PERSONAJE (tono de piel), COLORES (pijama y gorro) y
  ACCESORIOS (gorro de dormir y pantuflas, que siempre lleva: se muestran puestos, no como elección, porque no hay
  alternativas). Mosquito: CUERPO, ALAS, OJOS y PROBÓSCIDE atenuados con candado (esta versión no los cambia) y
  COLORES editable. Modo modular: `CategoryScroll` con `ModularCategory_<wire>` (icono deducido del slot). VOLVER abajo.
- Centro `PreviewPanel`: el visor renderiza en un objetivo propio con la proporción del panel (nada se estira y todo
  el panel acepta el arrastre). Detrás del personaje, el dormitorio cálido pintado de UI-06
  (`Resources/AlfaUiBackdrops/PreviewBedroom.png`: pared crema, armario, lámpara #FFB347 con halo, piso #8B5A2B,
  alfombra azul; `docs/unity/ui/tools/build_ui_customization_art.py`), fijo a la cámara; pedestal facetado de
  madera clara #A86F3A con canto #8B5A2B; luz de contorno cálida y un relleno frontal (luces puntuales de la capa
  del visor con intensidad proporcional a la distancia, así humano y mosquito reciben la misma luz). Chip
  "ARRASTRÁ PARA GIRAR" y botón CENTRAR (icono) en la esquina.
- Derecha `OptionsPanel`: en básico, una página por categoría con muestras de 80 unidades en grilla de 4 columnas y
  el nombre elegido; en modular, todas las categorías del rol como secciones de una sola lista (el carril salta a la
  sección), de modo que ESTILO DE ALAS y OJOS se ven juntos como en el boceto; cada sección dice la opción elegida.
  Cada opción visual tiene imagen propia: la miniatura del catálogo si existe (`Thumbnail`) o el arte a color
  (`OptionArt`, `Resources/AlfaUiOptionArt`: alas facetadas/redondas/largas/cortas, ojos grandes/chicos/enojados/
  dormidos, probóscide estándar/curva/corta/larga, cuerpo estándar/robusto/delgado) elegido por palabras del id y el
  nombre de la opción. Degradado de 24 unidades al pie de la lista cuando sigue.
- VISTA PREVIA: tres renders del personaje (FRENTE, ESPALDA, LADO) sobre el gris azulado de las miniaturas del
  boceto, hechos por el visor y refrescados un cuadro después de cada cambio; cada uno es también el botón que gira
  el visor a ese ángulo (reemplazan la fila FRENTE/PERFIL/ESPALDA). En básico ocupan el alto libre del panel.
- Vista aproximada: si el modo es modular y el juego todavía no puede ensamblar las piezas
  (`ModularPreviewAvailable` falso), el visor del mosquito aproxima la elección (escala/forma de huesos `Wing.*`,
  `Pupil.*`, `LidUpper.*`, `Head`, `Proboscis`, `Abdomen01`, `Thorax`, y tinte del cuerpo) y lo dice con un chip
  discreto "VISTA APROXIMADA". Ya no aparece el aviso "La vista de estas piezas llegará…".
- Un solo llamado a la acción, siempre APLICAR (como en Ajustes): verde, al 50 % sin cambios pendientes;
  ALEATORIO y DESHACER encima. VOLVER cierra.
- **ui-presentation-audio-8**: las presentaciones no reconstruyen botones que no cambiaron (firmas de paleta y de
  catálogo); scroll y foco sobreviven a cada selección. El aviso del visor se actualiza con `BindingChanged`.

## HUD (UI-06 7 y 7b, UI-01)

- Arriba a la izquierda, la tarjeta de objetivo (`RoleBadge`): la cara del personaje propio (render de cabeza por
  rol, una vez) sobre una insignia #FFC93C; sin visor, el pictograma del rol en tinta sobre el mismo disco.
  Sangre: el humano ve "Evitá que te piquen" con corazón y barra #E0393E de la sangre que le queda (18/18 al inicio,
  baja cuando pican, como el 100/100 del boceto); el mosquito ve la gota y la sangre juntada. Siempre en enteros.
- Reloj y contadores con cifras tabulares de la cara display (sin cero tachado).
- Inventario: cuatro ranuras de 80 unidades sobre bandeja #15264A al 92 %, cada una #0F1D38 opaca; la elegida conserva
  el relleno #1E3358 con marco #49B2FF de 3 unidades. Números de las teclas reales: 1, 2, 3 para los objetos y 0 para
  las manos, que cierran la fila (no hay tecla 4 en `GameplayRuntime`).
- Un solo aviso por situación: la oferta de reemplazo es un chip #0F1D38 con borde #E0393E y texto #FF6B5E en una
  línea ("E · REEMPLAZAR MATAMOSCAS POR PANTUFLA") y se oculta su duplicado "E · Confirmar reemplazo"; "¡Te están
  picando!…" pasa al cartel central "TE ESTÁN PICANDO · MIRÁ Y GOLPEÁ" y nunca como aviso de esquina.
- Mosquito: leyenda compacta de 4 filas (W Volar, ESPACIO/CTRL Subir · bajar, E Picar, F Posarte) sobre #15264A al
  92 %, 340 × ~172 unidades (las teclas reales al piso de 14 px no entran en 240); la situación ("Interrumpiendo al
  humano…") en blanco en su cabecera #1C3160. VIDAS con un corazón por vida.
- **ui-presentation-audio-7**: `LayoutHud` recalcula con el ancho del canvas; sin solapes a 1280×720, 1920×1080,
  1920×1200, 1280×1024, 1600×1200, 2560×1080 y 3440×1440.

## Pausa (UI-06 8)

- Escena atenuada con #0E1A30 (alfa 0,7: la UI mezcla en espacio lineal, así se lee como el 50–60 % pedido).
  "PARTIDA EN PAUSA" con contorno #0B1426 y sombra. CONTINUAR (azul), AJUSTES, SALIR (rojo). No se ofrece "VOLVER A LA
  SALA" a mitad de ronda: sólo el anfitrión puede terminarla.
- Chat de voz más bajo que el menú: silenciar mi micrófono y una lista que muestra tres filas enteras, con degradado
  de 24 unidades al pie mientras sigue (**ui-presentation-audio-5/-6**: filas por jugador estables, scroll).

## Resultados (UI-06 9)

- Sin tarjeta: #0E1A30 (alfa 0,7) sobre la escena; título de 116 unidades con contorno #0B1426 y sombra.
- Orden fijo: humanos a la izquierda, mosquitos a la derecha (figura y ficha). Figuras de cuerpo entero sin marco,
  en transparencia (render del modelo del juego sobre negro y blanco para recuperar el alfa): el ganador festeja
  (humano con los brazos arriba, mosquito encabritado) a escala 1,15 con halo de su color; el perdedor atrás, a 0,8 y
  atenuado. Fichas con trofeo y borde #FFC93C de 3 unidades en la ganadora y el número rotulado ("3 JUGADORES").
- Línea de marcador en la cara display ("MODO SANGRE · SANGRE 20 / 20 · TIEMPO 02:22"); en una ronda interrumpida no
  se muestra marcador ni tiempo, sólo el motivo. El marco se reacomoda si cambia el tamaño del canvas.
- Acciones reales: entrenamiento → VOLVER AL MENÚ y JUGAR DE NUEVO (verde); anfitrión → SALIR DE LA SALA y VOLVER A LA
  SALA; invitado → SALIR DE LA SALA y "ESPERANDO AL ANFITRIÓN…".

## Ajustes (UI-06 10, UI-05)

- GENERAL: Idioma (Español, único disponible, sin flechas), Sensibilidad del mouse (humano y mosquito) y Chat de voz
  (tecla para hablar). AUDIO: volúmenes y micrófono. VIDEO: pantalla completa, resolución (la actual; en batch el
  arnés presenta la resolución de captura), calidad, VSync, FPS. CONTROLES: sensibilidades, invertir eje y teclas
  reales por rol. ACCESIBILIDAD: Reducir movimiento y la nota de que los estados no dependen sólo del color.
- Valores en la cara display. APLICAR siempre verde, al 50 % sin cambios; RESTAURAR vuelve a lo guardado.
- **ui-presentation-audio-4**: reasignar la tecla conserva el borrador; un solo texto de espera en la fila
  ("PULSÁ UNA TECLA…", "Esc cancela"), el pie queda vacío; el Esc sólo cancela.
- No se muestran Mostrar subtítulos, Modo daltonismo ni Tamaño de texto: no existen en el juego y agregarlos a
  `AlfaSettingsDraft` rompe la lectura exacta de las preferencias schema 2 (MAPA-SISTEMAS, ui); queda para el dueño de
  Bootstrap/persistencia.

## Contratos y Bootstrap

- `BloodHudUiState` suma opcionales `mosquitoesTotal`, `humansActive`, `humansTotal`; `ResultsUiState` suma
  `humansCount`, `mosquitoesCount` (-1 = desconocido; la UI oculta el dato). Bootstrap los pasa desde `state.Actors`
  (`AlfaApplication.PresentGame`, 6 líneas, commit de etapa 2). La pasada de dirección de arte no tocó Bootstrap.
- Iconos (`build_ui_icons.py`): los de etapa 2 y Heart. Arte a color de opciones y dormitorio:
  `build_ui_customization_art.py`.
- Nombres que buscan los arneses: `GameplayHudView/RoleBadge`, `/ClockBadge`, `/ContextHintPanel`, `/PrivateEquipment`
  siguen siendo hijos directos; nuevo `SwapOfferChip`. `CustomizationSaveButton`, `PreviewFrontButton`,
  `PreviewBackButton`, `PreviewSideButton`, `PreviewResetButton` y `ResultsCard` se conservan (este último ahora es
  un marco sin fondo). `AudioMasterVolumeSlider` pasó a llamarse `MasterVolumeSlider` (el volumen general existe
  sólo en AUDIO).

## Correcciones del director de arte (en orden)

1. HUD humano: bandeja #15264A al 92 % y ranuras opacas; un solo chip de reemplazo; sin aviso de esquina con el cartel
   central; sangre en enteros.
2. Resultados: orden fijo con ficha bajo su retrato; ganador 1,15 con trofeo y borde #FFC93C; overlay; figuras de cuerpo
   entero festejando sin marco; perdedor a 0,8; título 116 con contorno y sombra; números rotulados; sin marcador en la
   ronda interrumpida.
3. Visor: dormitorio cálido del boceto, viñeta, contorno cálido; pedestal #A86F3A / #8B5A2B.
4. Panel derecho: VISTA PREVIA con tres renders y los botones de vista; muestras de 80 en 4 columnas; carril humano de
   3 categorías; carril del mosquito con candados y "COLORES"; CTA único APLICAR.
5. Modular: imagen propia por opción, visor que cambia (vista aproximada), alas y ojos a la vez, sin aviso de maqueta.
6. Ajustes: APLICAR verde siempre (50 % sin cambios); reparto por pestañas; resolución actual; un solo texto de espera.
7. HUD mosquito: leyenda de 4 filas con fondo #15264A 0,92, cabecera blanca, VIDAS con corazones. La cámara del
   mosquito (60 px detrás de la baranda) es de Gameplay/Presentation (`GameplayRuntime.MosquitoCameraDistance`,
   `HiggsfieldCameraDistanceOverride`): fuera del alcance de la UI, queda reportado.
8. HUD humano: ranuras de 80, elegida #1E3358 con marco #49B2FF 3 u, números de las teclas reales (1, 2, 3 y 0),
   insignia #FFC93C con la cara, barra #E0393E con corazón.
9. Pausa y resultados con overlay; lista de voz sin filas cortadas, con degradado y más baja que el menú.
10. Cifras tabulares sin cero tachado en tiempos y contadores; títulos de pausa y resultados con contorno y sombra.

## Verificación

- Arnés `N:/LetMeSleep/Validation/V030/UI/harness` (`run-v030-stage2.ps1` + `UiV030Capture.cs`): escena real con sus
  fuentes; todas las pantallas a 1920×1080 y 1280×720; menú, HUD y personalización también a 1920×1200, 1280×1024 y
  2560×1080. Nuevas capturas: 09e (accesorios), 09f (modular aproximado: alas redondas, ojos enojados, color bosque),
  12c (HUD humano en modo sangre con cartel), ajustes con la resolución de captura. Compuertas: UI presente, sin
  desbordes TMP, glifos visibles, ningún rótulo bajo 14 px a 720p.
- `Tests/PlayMode/UiStageTwoPlayModeTests`: además de lo de etapa 2, un aviso por situación y sangre entera; leyenda
  compacta y corazones; carriles, vistas y CTA único; alas y ojos juntos con imagen propia; ajustes según el boceto y
  APLICAR verde; lista de voz de filas enteras con degradado; resultados en orden fijo con escalas y números rotulados.
- `ModularCustomizationUiPlayModeTests`: el estado ya no muestra el aviso "llegará…".
- La captura modular usa un catálogo sintético: no existe catálogo modular de producción.
