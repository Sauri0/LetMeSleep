# v0.3.0 — UI etapa 2 (personalización, HUD, pausa, resultados y ajustes según UI-06)

Autoridad visual: `docs/v030/GUIA-ESTILO-BOCETOS.md`, UI-06 (pantallas 5–10), UI-01 (HUD), UI-05 (ajustes) y PER-08
(personalización dual). Continúa la etapa 1 (`V030-UI-ETAPA1.md`): misma paleta, cara display LMS Barlow Narrow
Bold (sin Bangers en la UI, según la corrección de dirección de arte 1), Atkinson para el cuerpo, selección azul con
marco de 3 unidades y piso de 21 unidades (14 px a 720p). Sigue siendo uGUI + TMP por código, sin prefabs ni escenas.

## Estructura del código

`AlfaUiController` pasó a ser `partial` (lo recomendaba el mapa de sistemas): las pantallas de esta etapa viven en
`AlfaUiController.Customization.cs`, `.Hud.cs`, `.Settings.cs` y `.PauseResults.cs`. Se conservaron los nombres de
GameObject, campos y métodos privados que usan el foco, las pruebas y los arneses.

## Personalización (UI-06 5–6, PER-08)

- Cabecera: título PERSONALIZACIÓN y pestañas de rol HUMANO (azul) / MOSQUITO (rojo) con icono y subtítulo.
- Columna izquierda `CategoryRail` con categorías con icono. Modo básico: TONO DE PIEL, PIJAMA Y GORRO (humano) y
  COLOR DEL CUERPO (mosquito). Modo modular: `CategoryScroll` con `ModularCategory_<wire>`; el icono se deduce del
  slot (alas, ojos, probóscide, gorro, piel, ropa, colores…). VOLVER abajo.
- Centro `PreviewPanel`: visor 3D grande sobre fondo azul suave (`CharacterPreviewOrbit.StageBackground`) con viñeta,
  **pedestal** facetado de madera en dos niveles (creado en runtime por `CharacterPreviewOrbit`, capa del visor, sin
  tocar la escena), chip "ARRASTRÁ PARA GIRAR" con mouse y flechas, y FRENTE / PERFIL / ESPALDA / CENTRAR. La imagen
  se muestra "contain" en todo el panel (el UV se extiende y los bordes repiten el fondo), así todo el panel acepta el
  arrastre sin deformar.
- Derecha `OptionsPanel`: grilla de muestras cuadradas (paletas y slots de color) o de tarjetas con miniatura (slots
  visuales; sin miniatura, el pictograma de la categoría), nombre de la opción elegida con ✓ (icono), estado,
  ALEATORIO y DESHACER, y el CTA verde que dice APLICAR si hay cambios o LISTO (cierra) si no.
- **ui-presentation-audio-8**: las presentaciones ya no reconstruyen botones que no cambiaron. Paletas por firma
  (id, nombre, color); lista modular por firma de categorías y de opciones. El scroll se reinicia sólo al cambiar de
  rol o categoría y el foco se restaura por nombre si hubo reconstrucción.
- El aviso "El visor 3D se conecta al personaje del juego." se actualiza con el evento `BindingChanged` del visor
  (antes quedaba fijo si el visor se enlazaba después de construir la UI).

## HUD (UI-06 7 y 7b, UI-01)

- Arriba a la izquierda, la tarjeta de objetivo (se llama `RoleBadge` para el arnés GameplayVisualCapture01): retrato
  circular del rol, "HUMANO · OBJETIVO" / "MOSQUITO · OBJETIVO", texto del objetivo según modo y rol, y barra con
  valor (sangre x/y, tareas x/y, mosquitos vivos x/total). Las vidas y ESPECTADOR van en un chip debajo.
- Arriba al centro `ClockBadge`: reloj grande y "MODO SANGRE/TAREAS/SUPERVIVENCIA"; el estado de red debajo.
- Arriba a la derecha `TeamCounter`: MOSQUITOS vivos/total (humano) o HUMANOS activos/total (mosquito), con icono.
  Debajo, el chip de voz y la tarea privada.
- Abajo al centro `PrivateEquipment`: cuatro ranuras cuadradas (0 manos, 1–3 objetos, como las teclas reales) con
  número, pictograma grande (PNG nuevos de matamoscas, pantufla, raqueta eléctrica, aerosol y manos), recurso corto
  en la esquina (`EquipmentSlot<n>`), la seleccionada en azul con marco; nombre completo del objeto elegido encima y
  la estamina integrada abajo (se pone roja bajo 25 %). Carga de pantufla y oferta de reemplazo crecen hacia arriba.
- Mosquito: la leyenda de teclas reales (GameplayRuntime) abajo a la derecha dentro de `ContextHintPanel`: Mouse
  Apuntar, W Volar, ESPACIO/CTRL Subir·bajar, F Posarte, E Picar y, en Tareas, R Ayudar. No hay "Shift Acelerar": el
  mosquito no usa Shift. La pista genérica de vuelo ("W · volar…") no se repite; las situacionales sí.
- **ui-presentation-audio-7**: `LayoutHud` recalcula el ancho del canvas: el aviso y el estado del actor se apilan
  sobre el cinturón (o abajo al centro sin él) y la pista/leyenda usa la esquina sin llegar al centro. Probado sin
  solapes a 1280×720, 1920×1080, 1920×1200, 1280×1024, 1600×1200, 2560×1080 y 3440×1440.

## Pausa (UI-06 8)

- Columna izquierda "PARTIDA EN PAUSA": CONTINUAR (azul), AJUSTES, SALIR DE LA SALA / SALIR DEL ENTRENAMIENTO (rojo),
  con la escena visible a la derecha. Se quitó CONTROLES (duplicaba AJUSTES). No se ofrece "VOLVER A LA SALA" a mitad
  de ronda: sólo el anfitrión puede terminarla.
- Panel "CHAT DE VOZ" debajo (sólo en sala): silenciar mi micrófono y la lista de jugadores en un scroll.
  **ui-presentation-audio-5**: la lista ya no empuja SALIR fuera de pantalla. **ui-presentation-audio-6**: filas por
  jugador que se actualizan en su lugar; se reconstruyen sólo si cambia el conjunto y nunca con la pausa oculta, y el
  foco sigue al mismo jugador.

## Resultados (UI-06 9)

- Banner "¡HUMANOS GANAN!" / "¡MOSQUITOS GANAN!" / "RONDA INTERRUMPIDA" con degradado del color del equipo, retrato
  del ganador grande y del otro equipo atenuado (ilustraciones de `Resources/AlfaUiPortraits` si existen; si no, los
  modelos del juego como en las tarjetas de entrenamiento), fichas HUMANOS n / MOSQUITOS n con trofeo en la ganadora.
- Acciones reales: entrenamiento → VOLVER AL MENÚ y JUGAR DE NUEVO (verde); anfitrión online → SALIR DE LA SALA y
  VOLVER A LA SALA (azul); invitado → SALIR DE LA SALA y "ESPERANDO AL ANFITRIÓN…".

## Ajustes (UI-06 10, UI-05)

- Pestañas GENERAL / AUDIO / VIDEO / CONTROLES / ACCESIBILIDAD con icono a la izquierda; filas con etiqueta y el
  control con su valor a la derecha (sliders con %, "‹ Activado ›" para on/off, ciclos de resolución, calidad y FPS,
  desplegable de micrófono). Sólo opciones reales. GENERAL agrupa volúmenes, sensibilidades y pantalla completa;
  los controles gemelos editan el mismo borrador y se refrescan juntos. CONTROLES suma la referencia de teclas reales
  por rol. ACCESIBILIDAD: reducir movimiento del menú (cuando la escena lo soporta).
- RESTAURAR vuelve a lo guardado; APLICAR (verde) guarda. VOLVER abajo a la izquierda.
- **ui-presentation-audio-4**: reasignar la tecla para hablar conserva los cambios sin aplicar (aunque Bootstrap
  vuelva a presentar los ajustes guardados al completar), la fila dice "PULSÁ UNA TECLA…" y bloquea el resto, y el Esc
  que cancela la reasignación ya no cierra Ajustes. Fuera de una sala el botón queda deshabilitado con la explicación.
  Pendiente para el dueño de Bootstrap/voz: excluir `<Mouse>/leftButton` en `VoiceRuntimeCoordinator.BeginRebind` y
  limpiar su `notice` al cancelar/completar.

## Contratos y Bootstrap

- `BloodHudUiState` suma opcionales `mosquitoesTotal`, `humansActive`, `humansTotal`; `ResultsUiState` suma
  `humansCount`, `mosquitoesCount` (-1 = desconocido; la UI oculta el dato).
- Único cambio de cableado en Bootstrap: `AlfaApplication.PresentGame` pasa esos conteos desde `state.Actors`
  (6 líneas). Sin cambios de red ni de gameplay.
- Iconos nuevos (`build_ui_icons.py`, mismos criterios): Hands, Flyswatter, Slipper, ElectricRacket, Aerosol y Blood
  (antes eran barras procedurales) y Palette, Dice, Undo, Mouse, Eye, Wings, Proboscis, Hat, Face, Accessibility,
  Trophy, Bolt.
- Nombres que buscan los arneses: `GameplayHudView/RoleBadge`, `/ClockBadge`, `/ContextHintPanel`,
  `/PrivateEquipment` siguen siendo hijos directos. `BloodBadge` ya no existe (su dato está en la tarjeta de
  objetivo). `PauseControlsButton` se quitó.

## Verificación

- Arnés `N:/LetMeSleep/Validation/V030/UI/harness` (`run-v030-stage2.ps1` + `UiV030Capture.cs` de etapa 2): escena real
  con sus fuentes, todas las pantallas tocadas a 1920×1080 y 1280×720; menú y HUD (humano y mosquito, vivo y
  sintético) también a 1920×1200, 1280×1024 y 2560×1080. Compuertas: UI presente, sin desbordes TMP, glifos visibles,
  ningún rótulo bajo 14 px a 720p.
- `Tests/PlayMode/UiStageTwoPlayModeTests`: selección sin reconstrucción ni pérdida de scroll/foco (básico y
  modular), aviso del visor tras enlazar, pedestal, PTT que conserva el borrador y Esc que sólo cancela, PTT fuera de
  sala, pestañas de ajustes y controles gemelos, lista de voz con 15 jugadores en scroll y filas estables, resultados,
  y HUD / menú / tarjetas sin solapes en siete relaciones de aspecto.
- `ModularCustomizationUiPlayModeTests` se adaptó al carril de categorías (busca `CategoryScroll` en `CategoryRail`,
  revisa los rótulos activos de toda la vista y hace el scroll después de fijar la resolución de captura).
- La captura modular usa un catálogo sintético: no existe catálogo modular de producción.
