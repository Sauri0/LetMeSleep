# v0.3.0 — UI etapa 1 (reestilo según UI-06)

Autoridad visual: `docs/v030/GUIA-ESTILO-BOCETOS.md` y el boceto UI-06. La UI sigue siendo uGUI + TMP
construida por código; no se agregaron prefabs ni escenas. Esta versión incluye las diez correcciones de la
dirección de arte (sección "Correcciones de dirección de arte").

## Piezas nuevas

- **Paleta** (`AlfaUiTheme`): valores de la guía con los nombres de token del alfa (fondo `#0E1A30`, panel
  `#15264A`, cabecera `#1C3160`, borde `#3B5E9C`, inset `#0F1D38`, secundario `#1E3358`/hover `#274473`, primario
  `#1F6FE0→#3A8DFF` con borde `#7CC0FF`, CTA `#2E9E48→#46C45F`, peligro `#C62E36→#E5484F`, texto `#F2F6FF`/`#A8B8D8`,
  acentos `#FFC93C`/`#49B2FF`, estados `#57D26B`/`#FF6B5E`). Scrim de modales `#0E1A30` al 80 %.
- **Estilos de botón** (`AlfaButtonStyle`): `Primary`, `Success`, `Danger`, `Secondary`, `Quiet`, `Tab` y `Menu`.
  `AlfaUiFactory.Button(..., AlfaButtonStyle, ...)` y `AlfaUiFactory.ApplyStyle(button, style)`. `Menu` pasa a azul
  cuando está seleccionado y selecciona al pasar el mouse (un único ítem resaltado, UI-06 pantalla 1).
- **Selección** (`AlfaUiFactory.SetSelected` / `MarkSelectedFrame`): fondo primario más marco `#49B2FF` de 3 unidades
  (aro `ringthick` del skin). La usan pestañas, cantidad de humanos, paletas, categorías/opciones modulares, pestañas
  de rol de personalización y el cinturón de equipo del HUD. Ningún texto lleva el prefijo `> `.
- **Deshabilitado**: todo botón no interactuable se dibuja `#1E3358` plano con su contenido al 50 %, sea cual sea su
  intención (`AlfaUiSurface.Disabled`, `AlfaUiFocusMotion.CaptureContent`). Un CTA verde deshabilitado ya no se ve
  verde oscuro.
- **Skin procedural** (`AlfaUiSkin`): atlas en runtime de 1024 (2× densidad) con rellenos redondeados 14/12/8/6, aros
  de 2 y 3 unidades, sombras suaves, círculos y un círculo grande de 256 px (sin desenfoque al agrandarlo).
  `AlfaUiSurface` dibuja sombra, degradado vertical y marco en la misma malla.

## Tipografía (corrección 1)

- **Display**: `LMS Barlow Narrow Bold` (`UI/Fonts/LMSBarlowNarrow-Bold.ttf`, SIL OFL 1.1) para pestañas, botones, CTAs,
  rótulos y títulos, con tracking de 2 % (`AlfaUiTheme.DisplayTracking`). Se carga desde
  `Resources/AlfaUiFonts/LMSBarlowNarrow-Bold SDF.asset`, así no necesita referencia en la escena ni tocar Bootstrap.
  Tamaños a 1080p: riel del menú 30, títulos de panel 34, CTAs 34, cabecera de sala 40.
- Barlow Condensed y Lilita One no estaban disponibles sin red (descargarlas requiere autorización del usuario).
  Se derivó la cara estrecha de **Barlow Bold 1.408** con `docs/unity/ui/tools/build_ui_fonts.py` (contornos, avances y
  kerning al 86 %, renombrada; Barlow no tiene nombre reservado). La fuente original está en
  `docs/unity/ui/tools/fonts/Barlow-Bold.ttf` con su licencia; se copió tal cual de una instalación local de Steam
  (`N:/SteamLibrary/steamapps/common/Counter-Strike Global Offensive/game/core/tools/fonts/barlow-bold.ttf`, SHA-256
  `984a0f81f4b34352fdf463d201091f9be8e5f6be66277779ddec6d3644d77ecf`, OFL). Conviene reemplazarla por la versión
  oficial de https://github.com/jpt/barlow (o usar Barlow Semi Condensed/Condensed Bold) cuando se autorice la
  descarga: basta con cambiar el TTF y correr `AlfaUiFontAssetBuilder`.
- **Cuerpo**: Atkinson Hyperlegible sin cambios. Bangers ya no se usa en la UI (queda en la escena como
  `HeadingFont` por compatibilidad).
- **Legibilidad 720p** (corrección 10): la fábrica no crea texto por debajo de 21 unidades (14 px a 1280×720,
  `AlfaUiTheme.MinTextSize`), tampoco los autoajustes. El arnés falla si algún rótulo activo queda por debajo de 14 px.
- **Asset de fuente**: `LetMeSleep.Editor.AlfaUiFontAssetBuilder.Build` (menú "Let me sleep/UI") crea el asset SDF
  dinámico; si ya existe lo limpia conservando el GUID. Las corridas en modo juego del editor escriben glifos en el
  atlas dinámico (igual que con Atkinson): antes de commitear, correrlo para dejarlo limpio.

## Logo y menú (correcciones 2 y 7)

- `docs/unity/ui/tools/build_ui_brand.py` dibuja con Pillow el logotipo (`Resources/AlfaUiBrand/LogoWordmark.png`, 2× el
  tamaño a 1080p): letras pesadas y redondeadas (Barlow Bold engrosada con trazo de unión redonda), "LET ME" en crema
  `#FFF1CC→#F2C27A` con "ME" al 75 %, "SLEEP" en `#B8E8FF→#6CC3F7`, contorno `#0B1426` de 9 px y extrusión sólida de
  6 px hacia abajo; y el mosquito caricaturesco (`LogoMosquito.png`, ojos blancos grandes, cuerpo rojo facetado, alas
  lavanda) a 124 px junto a "SLEEP". El subtítulo "HUMANOS CONTRA MOSQUITOS" es texto TMP crema `#EACEAB` con tracking
  de 6 %. Sin los PNG, el logo cae a texto en la cara display.
- Fondo del menú: degradado `#0E1A30` del 85 % en x=0 al 0 % en el 45 % del ancho, un lavado azul frío sobre toda la
  escena y un velo inferior. El lema va dentro de la columna del riel: siempre 24 unidades debajo de SALIR.
- **Pendiente de otro frente**: la escena del menú (hombre sentado con raqueta y pared gris) sigue siendo la mayor
  distancia con UI-06. Pedido para ese frente: la escena del hombre durmiendo en la cama con un mosquito visible.

## Pantallas

- **Jugar online** (corrección 6): pestañas CREAR SALA / UNIRSE A SALA. En crear, la columna izquierda tiene nombre,
  carrusel de mapa (miniatura 300×110 con flechas y nombre), MODO y HUMANOS (AUTO, 1–5). Son valores iniciales de la
  sala: al crearla se aplican con las acciones de reglas del anfitrión que ya existen (`IRoomMapActions`,
  `IRoomModeActions`, `SetHumanCount`), una por instantánea de la sala y cada una una sola vez; siguen editables
  adentro. No hay selector de "jugadores máximos": la capacidad es fija (`RoomRules.Capacity`, 16), así que el
  contador editable es el de humanos por ronda. Las filas informativas de la derecha ya no tienen barra verde.
  Unirse muestra nombre, código con PEGAR y una ayuda; la tarjeta se acorta (690) para no dejar hueco.
- **Miniaturas de mapa**: `Resources/AlfaUiMapThumbs/<mapId>.png` (600×220). `docs/unity/ui/tools/build_ui_map_thumbs.py`
  las recorta de las capturas de mundo por mapa (`Validation/V030/Maps/before/<mapId>-human-1920x1080-world.png`);
  hay que regenerarlas cuando cambien los mapas. `house-patio-v1` (mapa por defecto de la sala) no tiene captura y
  muestra el pictograma.
- **Conexión y errores** (corrección 3): scrim `#0E1A30` al 80 % en toda la pantalla, tarjeta opaca de 720 de ancho,
  título único "CONECTANDO…" con el mensaje de la fase ("Buscando la sala…"), spinner y CANCELAR (260×56) dentro de
  la tarjeta. La tarjeta de error tiene marco neutro `#3B5E9C`; el rojo `#FF6B5E` queda sólo en el icono y el título.
  Mientras hay tarjeta, el estado en línea del formulario se oculta.
- **Sala de espera** (corrección 4): la lista de jugadores se ajusta a su contenido (filas de 74, lista de hasta 520)
  y muestra barra de desplazamiento cuando hay más; nunca pisa el panel de voz aunque cambie la relación de aspecto.
  El panel de reglas mide 460 de ancho y arranca plegado (resumen de modo/tiempo/mapa/humanos, motivo de bloqueo e
  INICIAR RONDA); el anfitrión lo despliega con el chevrón para editar. Abajo a la izquierda, un panel de voz de
  440×190 (no hay chat de texto): quién habla, quién está silenciado y la tecla de hablar. Nombres flotantes sobre
  cada personaje de la sala mediante `ILobbyPresenceSource`. La segunda línea de la cabecera muestra "¡TODOS
  LISTOS!" cuando todos los conectados están listos, o "LA PARTIDA COMIENZA EN 00:28" si la sala publica
  `LobbyUiState.StartCountdownSeconds` (opcional; hoy ninguna sala lo publica porque la ronda la inicia el anfitrión).
  LISTO es el CTA verde; ya listo, "CANCELAR LISTO" usa el estilo secundario con icono de cruz (corrección 9).
- **Entrenamiento** (corrección 5): la zona de imagen de cada tarjeta (546×296) se llena de borde a borde. Orden: una
  ilustración en `Resources/AlfaUiPortraits/Human|Mosquito` (dibujada "cover"), si no una instantánea del modelo del
  juego (`AlfaRolePortrait`: instancia el prefab en el rig de vista previa de personalización, capa 30, lejos del
  pedestal, con luz azul para el humano y roja para el mosquito, lo renderiza una vez a 1092×592 y lo destruye), si no
  el pictograma grande sobre un círculo de alta resolución.
- **Personalización, ajustes, pausa, resultados y confirmación**: retema global (cara display, piso de 21, selección
  y deshabilitado nuevos). El HUD cambia la selección del cinturón (placa azul con marco, número arriba a la
  izquierda) y el estado de voz va en un chip `#A8B8D8` sobre tinta, legible contra el cielo.

## Correcciones de dirección de arte (resumen)

1. Tipografía display condensada sin Bangers, tamaños 30/34/34. 2. Logo pesado redondeado con degradados, contorno,
extrusión y mosquito dibujado. 3. Scrim, tarjeta opaca, CANCELAR adentro, sin estado duplicado, error neutro.
4. Lista ajustada, reglas de 460 plegables, voz, nombres flotantes, banner de listos y cuenta regresiva opcional.
5. Retratos que llenan la tarjeta y círculo nítido. 6. Columna izquierda con carrusel, modo y humanos; sin barras
verdes. 7. Degradado y luz fría del menú, lema a 24 de SALIR (la escena nueva es pedido para otro frente). 8. Selección sin `> `.
9. Deshabilitado `#1E3358` al 50 % y CANCELAR LISTO secundario. 10. Piso de 14 px a 720p y chip de PTT.

## Correcciones anteriores

- **ui-presentation-audio-1**: el campo de código ya no desordena caracteres al escribir o pegar
  (`AlfaRoomCode.FormatForEditing`). Prueba: `Tests/PlayMode/RoomCodeInputPlayModeTests`.
- **ui-presentation-audio-9**: Tareas para humanos dice "Mantené E" (`AlfaModeText`), igual que el HUD.
- Sala: un jugador sin conexión ya no aparece como listo aunque su último estado lo fuera.

## Nombres y contratos

Se conservaron los nombres usados por foco, pruebas y arneses (`MainPlayButton`, `PlayerNameInput`,
`RoomCodeInput`, `LobbyReadyButton`, `LobbyCopyButton`, `LobbyExploreButton`, `LobbyCustomizeButton`,
`TrainingHumanButton`, `TrainingMosquitoButton`, `RoleBadge`, `ClockBadge`, `ContextHintPanel`,
`GameplayHudView/PrivateEquipment`, `EquipmentSlot{n}`, `CustomizationView`, `OptionsPanel`, `ModularFields`,
`Actions`, …). `OnlineCancelButton` ahora es hijo de `OnlineConnectingCard`. Nuevos: `OnlineMapPrevious/Next`,
`OnlineModePrevious/Next`, `OnlineHumansPrevious/Next`, `LobbyRulesToggle`, `LobbyVoicePanel`, `LobbyNametags`,
`VoiceChip`.

Contratos (aditivos): `ILobbyPresenceSource` (opcional, junto a `IMenuActions`), parámetro opcional
`startCountdownSeconds` de `LobbyUiState`, icono `AlfaUiIconKind.Close`.

**Bootstrap**: un único archivo nuevo, `Bootstrap/AlfaApplication.LobbyPresence.cs`, que implementa
`ILobbyPresenceSource` leyendo `lobbyMovement.TryGetVisual` (sólo lectura). Sin cambios en protocolos de red ni
gameplay.

## Verificación

Arnés `N:/LetMeSleep/Validation/V030/UI/harness` (`run-v030-review.ps1` + `UiV030Capture.cs`): escena real con sus
fuentes, pantallas a 1920×1080 y 1280×720 (más el menú a 2560×1080, 1920×1200 y 1280×1024) con estados sintéticos,
incluidas la sala con todos listos, la cuenta regresiva con reglas desplegadas y la lista de 12 con desplazamiento.
Compuertas: UI presente, sin desbordes TMP, glifos visibles y ningún rótulo activo bajo 14 px a 720p. Los nombres
flotantes se muestran anclando al jugador "Branko" al personaje del menú mediante el gancho de presencia.
`Tests/PlayMode/UiArtDirectionPlayModeTests` cubre la aplicación de los valores iniciales de la sala, que unirse no
toca reglas, la tarjeta de conexión, CANCELAR LISTO, la ausencia de `> ` y el piso de 21 unidades.
`TrainingBootstrapPlayModeTests` falla en este worktree por la configuración de Build Settings ("LetMeSleepBoot must be
present"), ajena a la UI; se sacó del filtro del arnés.
