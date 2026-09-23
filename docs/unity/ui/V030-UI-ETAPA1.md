# v0.3.0 — UI etapa 1 (reestilo según UI-06)

Autoridad visual: `docs/v030/GUIA-ESTILO-BOCETOS.md` y el boceto UI-06. La UI sigue siendo uGUI + TMP
construida por código; no se agregaron prefabs, escenas ni campos serializados.

## Piezas nuevas

- **Paleta** (`AlfaUiTheme`): se conservaron los nombres de token del alfa con valores azul marino calibrados
  contra UI-06 (panel `#0E2545`, borde `#2F5A96`, primario `#2F86F0→#0C5FC9`, CTA `#3CC45A→#219A3E`, peligro
  `#E04550→#A92A34`, texto `#F2F6FF`/`#A8B8D8`, acentos `#FFC93C`/`#49B2FF`). Tokens nuevos para cabecera,
  inset, equipos, estados y logo.
- **Estilos de botón** (`AlfaButtonStyle`): `Primary`, `Success`, `Danger`, `Secondary`, `Quiet`, `Tab` y `Menu`.
  `AlfaUiFactory.Button(..., AlfaButtonStyle, ...)` y `AlfaUiFactory.ApplyStyle(button, style)`. La firma vieja
  `(primary, destructive)` sigue funcionando. `Menu` pasa a azul cuando está seleccionado y selecciona al pasar
  el mouse, así hay un único ítem resaltado (UI-06 pantalla 1).
- **Skin procedural** (`AlfaUiSkin`): un único atlas en runtime (2× densidad) con rellenos redondeados de radio
  14/12/8/6, aros de 2 unidades, sombras suaves y círculos. `AlfaUiSurface` (BaseMeshEffect) dibuja en la misma
  malla y textura la sombra, el degradado vertical y el marco, y el foco interpola marco/brillo/relleno. Reemplaza
  los componentes `Outline`/`Shadow` del alfa: para cambiar un borde usar `AlfaUiFactory.SetFrame`/`SetSurface`.
- **Tipografía**: Bangers (`factory.Title`, `factory.ComicLabel`) para logo, títulos de pantalla, rieles del menú y
  CTAs grandes; Atkinson Hyperlegible para cuerpo y botones secundarios. Los glifos `▼` `✓` `→` no existen en las
  fuentes: el desplegable usa los iconos `ChevronDown` y `Ready`, y los ciclos `ChevronLeft/Right`.
- **Iconos**: 18 pictogramas nuevos en `Resources/AlfaUiIcons` generados por `docs/unity/ui/tools/build_ui_icons.py`
  (se puede regenerar sólo una lista: `python build_ui_icons.py Wifi Warning`). Los 13 del alfa no cambian.

## Pantallas

- **Menú**: logo "LET ME / SLEEP" grande (amarillo/azul, contorno y extrusión de tinta) con subtítulo, riel de
  5 botones con icono (JUGAR, ENTRENAMIENTO, PERSONALIZACIÓN, AJUSTES, SALIR) anclado debajo del logo, lema
  "LA NOCHE NUNCA ES TAN TRANQUILA". JUGAR abre directamente el panel online.
- **Jugar online**: un panel con pestañas CREAR SALA / UNIRSE A SALA (`OnlineCreateTab`, `OnlineJoinTab`).
  `ShowOnlineChoice()` ahora muestra la pestaña de crear; el enum `OnlineChoice` se mantiene por contrato.
  Sólo se muestran opciones que existen: nombre, código de sala (en unirse) y filas informativas (sala privada por
  código, hasta 16 jugadores, mapa y modo se eligen dentro de la sala, roles al azar). No hay navegador de salas.
- **Conexión y errores**: tarjeta "CONECTANDO…/CREANDO SALA…/BUSCANDO SALA…/ENTRANDO…" con icono wifi y spinner
  (respeta movimiento reducido) mientras `OnlineUiState.IsBusy`, con CANCELAR; tarjeta de error con triángulo rojo,
  título según fase (`RecoverableError`, `IncompatibleVersion`, `RoomClosed`), mensaje, REINTENTAR (si `CanRetry`) y
  VOLVER. Esc cancela o cierra la tarjeta.
- **Sala de espera**: cabecera "ESPERANDO JUGADORES" con `X/16` y "mapa · MODO", lista de jugadores con barra de
  estado (Listo verde / No listo rojo / sin conexión gris) y voz (Hablando/Silenciado), panel de reglas del
  anfitrión (se conservan modo, tiempo, mapa y cantidad de humanos), chip con el código, INVITAR AMIGOS (copia el
  código con `IMenuActions.CopyRoomCode`) y LISTO verde grande. No hay chat de texto (no existe protocolo).
- **Entrenamiento**: dos tarjetas (humano azul / mosquito rojo) con retrato, "ENTRENAR COMO …", descripción e
  INICIAR propio (`TrainingHumanButton` / `TrainingMosquitoButton` son ahora esos INICIAR). Modo y mapa quedan en
  una fila inferior. Ya no existe `TrainingStartButton`.
- **Retratos de entrenamiento**: `AlfaUiController.LoadRolePortrait` carga `Resources/AlfaUiPortraits/Human` y
  `Resources/AlfaUiPortraits/Mosquito` (PNG importado como Sprite, o como textura común). Para instalarlos basta con
  dejar `unity/Assets/LetMeSleep/UI/Resources/AlfaUiPortraits/Human.png` y `Mosquito.png` con sus `.meta`; si no
  existen, la tarjeta muestra el pictograma grande del rol.
- Personalización, ajustes, HUD, pausa, resultados y confirmación sólo reciben el retema global en esta etapa
  (títulos en Bangers, etiquetas en tinta de rótulo). Ajustes agranda sus paneles: en el alfa el contenido de
  Audio y Controles se salía de su caja. Confirmación y resultados centran su contenido.
- **Menú a otras relaciones de aspecto** (ui-presentation-audio-7, parte menú): el riel queda anclado arriba, bajo el
  logo, así que ya no se superpone a 21:9, 16:10 ni 5:4 (capturas `01-menu-ultrawide`, `-16x10`, `-5x4`). El HUD no
  se tocó en esta etapa.

## Correcciones

- **ui-presentation-audio-1**: el campo de código ya no desordena caracteres al escribir o pegar. El formateo
  usa `AlfaRoomCode.FormatForEditing`, que ubica el cursor por cantidad de caracteres de código delante de él.
  Prueba: `Tests/PlayMode/RoomCodeInputPlayModeTests` (tecleo real con `ProcessEvent`, pegado con `Append` y Ctrl+V).
- **ui-presentation-audio-9**: la instrucción de Tareas para humanos decía "Mantené R"; ahora dice "Mantené E"
  (`AlfaModeText`), igual que el HUD.
- Sala: un jugador sin conexión ya no aparece como listo aunque su último estado lo fuera.

## Nombres y contratos

Se conservaron los nombres usados por foco, pruebas y arneses (`MainPlayButton`, `PlayerNameInput`,
`RoomCodeInput`, `LobbyReadyButton`, `LobbyCopyButton`, `LobbyExploreButton`, `LobbyCustomizeButton`,
`TrainingHumanButton`, `TrainingMosquitoButton`, `RoleBadge`, `ClockBadge`, `ContextHintPanel`,
`GameplayHudView/PrivateEquipment`, `CustomizationView`, `OptionsPanel`, `ModularFields`, `Actions`, …).
Desaparecen `OnlineChoiceView`, `CreateChoiceButton`, `JoinChoiceButton` y `TrainingStartButton`.
No hubo cambios en Bootstrap, protocolos de red ni gameplay.

## Verificación

Arnés `N:/LetMeSleep/Validation/V030/UI/harness` (`run-v030-review.ps1` + `UiV030Capture.cs`, adaptado de
V020 UiRedesignReview01): escena real con fuentes serializadas, 14 pantallas a 1920×1080 y 1280×720 (más el menú
a 2560×1080, 1920×1200 y 1280×1024), sin desbordes TMP y con glifos visibles. El runner toma un turno del semáforo
global de Unity, copia el arnés a `Tests/PlayMode` sólo durante la corrida y lo quita del csproj al terminar.
En un worktree recién creado, la primera apertura puede dejar scripts sin clase (los prefabs de mapas fallan con
"Missing prefab/content identity"); `run-reimport-scripts.ps1` fuerza la reimportación y la siguiente corrida sale
bien. Las pruebas de evidencia `ModularCustomizationUiPlayModeTests` y
`CustomizationPersistencePlayModeTests` aceptan ahora salidas bajo `Validation/V020` o `Validation/V030`.
