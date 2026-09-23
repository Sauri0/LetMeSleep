# v0.3.0 — Mapa de sistemas (2026-09-23)

## ui

La UI de Let me sleep es 100 % uGUI + TextMeshPro construida por código en runtime. No usa prefabs, escenas UI, UXML/USS ni UI Toolkit. `AlfaUiRuntime.Create` crea un único Canvas Screen Space Overlay con CanvasScaler de referencia 1920×1080 y match 0.5, más un EventSystem con InputSystemUIInputModule. `AlfaUiController` (2516 líneas) arma todas las vistas en `BuildViews` y las alterna con `SetScreen` según el enum `AlfaUiScreen`, que tiene 11 valores. El modal de confirmación es un overlay aparte. Los primitivos visuales viven en `AlfaUiFactory`: Panel, Button, FeatureButton, Input, Slider, Toggle, Dropdown y ScrollView. Los tokens de color y tamaño están en `AlfaUiTheme`. El sprite redondeado de 9-slice ya es procedural (48 px, radio 12, borde 14). Los iconos son 13 PNG blancos de 128 px en `Resources/AlfaUiIcons`, generados con Pillow; para los demás tipos hay trazos procedurales de respaldo. Las fuentes son TMP SDF dinámicas: Bangers solo para el logo y Atkinson Hyperlegible Regular con negrita sintética para títulos y cuerpo. `AlfaApplication.Start` (Bootstrap) conecta la UI e implementa IMenuActions y las interfaces opcionales. El commit e2d7d80 (primera pasada de rediseño, 20/09) alejó la paleta del azul marino hacia gris oscuro neutro con acento ámbar. UI-06 pide lo contrario: azul marino, primario azul brillante, CTA verdes y rojos, texto blanco. Por eso el punto único de retema es `AlfaUiTheme` (tokens y `ButtonColors`) junto con los primitivos de `AlfaUiFactory` (`RoundedSprite`, `Panel`, `Button`). Casi todo el color pasa por tokens; solo `NightPrimaryButton`, que no se usa, tiene literales. Estructuralmente faltan o difieren respecto de UI-06: pestañas Crear/Unirse en un solo panel, sala de espera con chat, contador, X/N y nombres flotantes, tarjetas de entrenamiento con ilustración, personalización en tres columnas con rejilla de muestras y miniaturas, HUD con objetivo y contadores por equipo y barra de objetos centrada, leyenda de teclas del mosquito, pausa a la izquierda, resultados «¡HUMANOS GANAN!» con marcador por equipo, ajustes con pestañas General/Audio/Video/Controles/Accesibilidad y tarjetas de conexión y error. Hay un arnés de captura nativo, UiRedesignCapture: pruebas PlayMode en batch con cámara overlay URP, a 1920×1080 y 1280×720, que verifican desbordes y glifos. Su última corrida pasó 2/2 el 20/09 contra el código actual (el contenido coincide salvo finales de línea CRLF).

### Cómo funciona

CONSTRUCCIÓN
- Todo es uGUI + TMP (com.unity.ugui 2.0.0; TMP viene dentro de ugui en Unity 6) creado por código. No hay prefabs UI en UI/, ni .uxml/.uss, ni UIDocument. La decisión está documentada en docs/unity/ui/ALFA-UI-SPEC.md §2.
- AlfaApplication.Start (AlfaApplication.cs L91-92) llama a AlfaUiRuntime.Create(this, new AlfaUiDependencies(HeadingFont, BodyFont, preview: new CharacterPreviewSetup(PreviewCamera, PreviewStage, PreviewTexture, HumanPrefab, MosquitoPrefab, ConfigurePreviewAttention))).
- Create arma el GameObject 'AlfaUI' con Canvas Overlay (sortingOrder 100) y CanvasScaler ScaleWithScreenSize 1920×1080, MatchWidthOrHeight 0.5. Si falta, agrega un EventSystem con InputSystemUIInputModule y acciones por defecto.
- AlfaUiController.Initialize → BuildViews (L782-795) construye las 11 vistas una sola vez como hijos del mismo Canvas. SetScreen (L2235) activa una sola vista (CreateRoom y JoinRoom comparten OnlineFormView), resetea el CanvasGroup, muestra u oculta el visor 3D, bloquea el cursor solo en Gameplay y enfoca por NOMBRE de GameObject (Focus → FindActive). Después emite ScreenChanged.
- Update (L214): Esc o botón Este del gamepad → HandleEscape (L2190). Tab en espectador → ISpectatorActions.SpectateNext.
- Cada vista lleva AlfaUiEntranceMotion (alfa de 0,55 a 1 en 190 ms, tiempo no escalado). Cada botón lleva AlfaUiFocusMotion (brillo, escala de la placa del icono y sombra en 160 ms). Ambos se desactivan con AlfaUiMotionPreferences.ReducedMotion, que viene de AlfaSettingsDraft.ReduceMenuMotion.
- Los latches de intención (online, ready, start, reglas, entrenamiento, guardado, aplicar) evitan el doble envío. Cada Present* posterior es la instantánea autoritativa que los libera.

COLORES Y TAMAÑOS
- Todo sale de AlfaUiTheme (L8-22): Ink900 #080B12, Night800 #121824, Night700 #1C2534, Night600 #303B4D, Moon200 #CAD3DC, Sheet100 #FFF1D2, Lamp400 #F0B84E, Pajama500 #DF6559, Mint400 #74C89C, Sky400 #70ACD5, Disabled #77818E, Border #756D62, Scrim #070A10 al 80 %. WarmSurface y ChalkShadow no se usan.
- ButtonColors (L45-58): el primario es ámbar Lamp400 con texto Ink900, el secundario Night600 y el destructivo Pajama500.
- Tras e2d7d80 la paleta pasó de azul marino a gris neutro. Antes era Night800 #10233D, primario Sky400 #4FA9F5 y Border #4D83BD.
- Hay 180 referencias AlfaUiTheme.* en el controller. Las ~36 `new Color(AlfaUiTheme.X.r, …, alfa)` derivan de tokens. El único literal fijo está en NightPrimaryButton (Factory L173-187), que no se usa.
- Estilos que el controller aplica encima del factory: ApplyPositiveStyle (verde Mint400 con texto Ink900; lo usan LISTO, INICIAR RONDA, APLICAR personalización y APLICAR ajustes), SetRoleButtonSelection (Sky400 para humano, Pajama500 para mosquito), ApplyModularButtonStyle y BuildPalette (Night600 con contorno Lamp400 al seleccionar) y SetSceneScrim (Ink900 con alfa).
- Tamaños: botón por defecto de 58 de alto, FeatureButton de 76 a 94, CTA de 66 a 74, inputs y dropdowns de 58, sliders de 44 y placa de icono de 34 a 50. Las posiciones son offsets absolutos (helper Anchor, L2507) combinados con Vertical/HorizontalLayoutGroup y LayoutElement.

TIPOGRAFÍA
- Factory.Text con heading=true usa AlfaUiTheme.Display, que devuelve BodyFont: Atkinson Regular con FontStyles.Bold (negrita sintética TMP) y characterSpacing 1,5. LogoText usa Bangers.
- BrandLockup (L141-171) compone 'LET ME' a 84 y 'SLEEP' a 112 con VertexGradient Sheet100→Lamp400 y Sheet100→Sky400, contorno 0,14 Ink900, más el icono Mosquito rotado -14°.
- Los SDF son Dynamic con TTF incluido. No tienen FallbackFontAssetTable y TMP Settings tampoco tiene fallbacks (default LiberationSans SDF). Los glifos ▼ y ✓ del Dropdown no existen en Atkinson; ✓ tampoco existe en LiberationSans.

SPRITES E ICONOS
- RoundedSprite es procedural: Texture2D de 48×48 RGBA con alfa antialiasado por distancia (radio 12) y Sprite.Create PPU 100 FullRect con borde 9-slice (14,14,14,14), cacheado estático con HideAndDontSave.
- Panel, Button, Input y la raíz del Dropdown usan dependencies.PanelSprite o ButtonSprite si se pasan (Bootstrap no los pasa). Si no, usan RoundedSprite. Slider, Toggle, ScrollView y la plantilla del Dropdown usan siempre RoundedSprite.
- HorizontalFadeSprite (256×4, desvanecido horizontal) es el lavado del menú.
- Los bordes son el componente UnityEngine.UI.Outline, que copia la malla cuatro veces desplazada. Las sombras son UnityEngine.UI.Shadow, un desplazamiento duro. El brillo superior es el hijo 'Shine' y el bisel es el hijo 'LowerBevel'. No hay degradados en Image; solo el VertexGradient TMP del logo.
- Iconos: 13 PNG blancos (build_ui_icons.py) cargados como Texture2D y convertidos a Sprite con PPU 128. El resto son barras procedurales.

PANTALLAS (AlfaUiScreen) Y FLUJO
1) MainMenu (BuildMain L797): vista transparente sobre la escena 3D viva del menú (MainMenuLivingScene: humano sentado con matamoscas y mosquito en vuelo; AlfaApplication.MenuScene.cs).
- Capa NightWash de 900×1080 con desvanecido, WarmSeam, Brand arriba a la izquierda y subtítulo 'HUMANOS CONTRA MOSQUITOS'.
- MenuRail con MainPlayButton 'JUGAR ONLINE' (→ OnlineChoice), MainTrainingButton (→ Training), MainCustomizeButton (→ Customization), MainSettingsButton (→ Settings, volviendo a MainMenu) y MainQuitButton (→ ConfirmQuit → QuitGame).
- También un texto de navegación, la versión y el CastRibbon HUMANOS/MOSQUITOS abajo a la derecha. Esc → ConfirmQuit.
2) OnlineChoice (L850): tarjeta de 860×620 con CreateChoiceButton → ShowCreateRoom, JoinChoiceButton → ShowJoinRoom y VOLVER.
3/4) CreateRoom/JoinRoom (L868, misma vista OnlineFormView de 800×690):
- PlayerNameInput (24 caracteres); RoomCodeRow solo en Join, con RoomCodeInput formateado XXXXX-XXXXX y PasteRoomCodeButton.
- OnlineStatus. OnlinePrimaryButton → IMenuActions.CreateRoom o JoinRoom. OnlineCancelButton → CancelOnline, OnlineRetryButton y OnlineBackButton.
- Estados por PresentOnline(OnlineUiState): Connecting, Creating, Searching, Entering, Cancelled, RecoverableError, IncompatibleVersion y RoomClosed. Los errores se muestran como texto Pajama500 dentro de la misma tarjeta, sin pantalla propia de conexión o error.
- Si la sala se cierra, Bootstrap llama ShowJoinRoom + PresentOnline(RoomClosed o error).
5) Lobby (L908): vista transparente sobre el lobby 3D recorrible.
- Header de 92 de alto: icono, 'SALA ONLINE', código y LobbyCopyButton 'COPIAR PARA INVITAR'.
- RosterPanel izquierdo de 430 de ancho con ScrollView y 16 filas MemberPanel. Cada fila tiene icono y texto nombre + 'LISTO/NO LISTO/SIN CONEXIÓN · SE SORTEA AL EMPEZAR' + marcas de voz.
- RulesPanel derecho de 520×780 con 'PRÓXIMA RONDA' y ciclos MODO, TIEMPO y MAPA. HumanCount0..5 ('AUTO', 1-5; selección marcada '[x]'). LobbyReadyButton en verde. LobbyStartButton solo para el anfitrión. StartReason. LobbyExploreButton → exploración 3D (Esc vuelve). LobbyCustomizeButton → Customization, que vuelve al Lobby. LobbyStatus.
- Esc → modal 'PAUSA' con VOLVER A SALA o SALIR. Late join: PresentLobby con IsWaiting=false.
6) Training (L1049): tarjeta de 760×800 a la izquierda sobre la escena. Botones de rol HUMANO y MOSQUITO (FeatureButton), ciclos MODO y MAPA (mapas de HiggsfieldMapCatalog), estado, TrainingStartButton → StartTraining(role, modeId, mapId) y VOLVER/CANCELAR.
7) Customization (L1082):
- Columnas PreviewPanel de 900×900 (RawImage + CharacterPreviewOrbit, StageHeader 'VISTA EN VIVO · HUMANO', botones FRENTE, PERFIL, ESPALDA y CENTRAR) y OptionsPanel de 720×900.
- Pestañas de rol CustomizationHumanButton y CustomizationMosquitoButton.
- Modo básico: GridLayout de 3 columnas con celdas de 216×66 (texto + muestra de 28 px).
- Modo modular: ModularFields con CategoryScroll y OptionsScroll de botones de texto, más ColorSwatch o Thumbnail de 36 px.
- Footer 'Actions' con APLICAR, DESHACER CAMBIOS y VOLVER.
- Mientras está abierta, Bootstrap oculta los personajes del menú (OnUiScreenChanged L554).
8) Settings (L1167): tarjeta de 1280×840 sin pestañas.
- Columna izquierda: AudioPanel (VOLUMEN GENERAL, MÚSICA, EFECTOS, VOCES y el dropdown de micrófono) y ControlsPanel (sensibilidad del humano y del mosquito, invertir eje Y y el botón de reasignar PTT).
- VideoPanel: pantalla completa, RESOLUCIÓN y CALIDAD como ciclos, VSync, dropdown de límite de FPS y REDUCIR MOVIMIENTO (oculto si no está soportado).
- Status. SettingsApplyButton en verde, SettingsResetButton y SettingsBackButton. Si hay cambios sin aplicar, confirma '¿DESCARTAR CAMBIOS?'.
9) Gameplay/HUD (BuildHud L1250, PresentHud L667):
- RoleBadge arriba a la izquierda (178×50). ClockBadge (150×58) y BloodBadge (248×58, SANGRE x/y, TAREAS o MOSQUITOS VIVOS con barra) arriba al centro.
- NetworkState y VoiceState arriba a la derecha. PrivateTask arriba a la derecha (400×112 con barra). Lives.
- ActorStatePanel (430×62, estado + progreso) e InteractionPrompt (440×50) abajo al centro. ContextHintPanel abajo a la izquierda (540×64).
- PrivateEquipment abajo a la derecha (700×176+): cuatro placas de 164×100 MANOS/1/2/3 con icono de 22 px y texto, estamina, carga de pantufla y oferta de reemplazo. Reticle al centro.
- El mosquito usa la misma vista, sin equipo y con pista por defecto.
10) Pause (L1399): tarjeta centrada de 720×760 con PAUSA, CONTINUAR, AJUSTES, CONTROLES (abre los mismos ajustes), estado de voz, silenciar micrófono, lista de peers y SALIR DE LA SALA o VOLVER AL MENÚ (destructivo, con confirmación).
11) Results (L1423): tarjeta de 780×580 con título 'GANARON LOS HUMANOS', 'GANARON LOS MOSQUITOS' o 'RONDA INTERRUMPIDA'. Stats en texto: modo, puntaje, tiempo y motivo.
- ResultsPrimaryButton: VOLVER AL LOBBY (solo el anfitrión → ReturnToLobby) o REPETIR ENTRENAMIENTO.
- ResultsLeaveButton: SALIR DE LA SALA o VOLVER AL MENÚ. El invitado ve 'ESPERANDO AL ANFITRIÓN…'.
+ ConfirmModal (L1439): 640×360 con título, cuerpo, ConfirmSafeButton (primario) y ConfirmDangerButton.

VISOR 3D
- PreviewCamera renderiza solo la capa 30 sobre la RT Customization.renderTexture (1024²) con fondo sólido #0B1421. El Stage está en (500,0,0).
- CharacterPreviewOrbit instancia HumanPrefab o MosquitoPrefab bajo Stage y rota el Stage (no la cámara). El encuadre sale de los bounds del clon.
- Bootstrap pone la capa 30 al clon y le aplica colores o piezas modulares (ApplyPreviewColors, TryApplyModularPreview). OnPreviewCreated instala la atención facial.

EVIDENCIA ACTUAL
- UiRedesignReview01/Run-20260920-180449-325: menu-1920x1080.png muestra riel izquierdo gris oscuro con JUGAR ONLINE en ámbar y el logo Bangers crema/ámbar/celeste. hud-human-1920x1080.png muestra cápsulas gris oscuro y bandeja de equipo abajo a la derecha.
- ModularVisual-20260920-180849-655 muestra el personalizador sintético.
- Muestreo de colores de UI-06 (cuantizado a 8): fondo #001028; paneles #081830–#082040; botones secundarios y pestañas #082048/#103050; primario #0050B0 (menú) y #0870E0 (INICIAR y pestaña); pestaña seleccionada #0058C0–#0068D8; CTA verde #20A040–#28B040; rojo #A03038; relleno de slider #3098F0; realce del título LET ME #F8E8B8 y de SLEEP #98D8F8/#78C8F8; etiquetas #8BA2C3; título de error #DF474A.

### Archivos clave

- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/AlfaUiRuntime.cs` — Crea la raíz `AlfaUI`: Canvas ScreenSpaceOverlay con sortingOrder 100 (L22-24) y CanvasScaler ScaleWithScreenSize 1920×1080 con match 0.5 (L25-29). Usa GraphicRaycaster con blockingObjects None y DontDestroyOnLoad si `PersistentAcrossScenes` (L32). `EnsureEventSystem` (L38-43) crea EventSystem + InputSystemUIInputModule.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/AlfaUiTheme.cs` — Tokens de color (L8-22), duraciones de foco y entrada (L24-25), escala tipográfica (L27-33: Logo 88 sin uso, H1 48, H2 32, Button 24, Body 21, Label 18, Note 16). Selectores de fuente `Display`/`Logo`/`Body` (L35-43): Display devuelve BodyFont, así que los títulos usan Atkinson en negrita sintética y Bangers queda solo para el logo. `ButtonColors(primary, destructive)` (L45-58) devuelve el ColorBlock. Es el punto único de retema.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/AlfaUiFactory.cs` — Primitivos uGUI y sus métodos: View (L22), SafeArea (L36), Vertical/Horizontal (L44/57), Panel (L77-105: Image Sliced + Outline + Shadow + TopEdge), Text (L107), LogoText (L131), BrandLockup (L141-171: logo con VertexGradient y contorno TMP). También NightPrimaryButton (L173, sin uso), QuietButton (L189), Button (L207-292: placa de icono, Shine, LowerBevel, AlfaUiFocusMotion) y FeatureButton (L294). Sprites procedurales en RoundedSprite (L321-350) y HorizontalFadeSprite (L352). Resto: Icon (L379), SectionHeader (L389), Divider (L401), Input (L413), Slider (L454), Toggle (L497), Dropdown (L521, con '▼' en L538 y '✓' en L581), ScrollView (L617). Motion: AlfaUiEntranceMotion (L687), AlfaUiFocusMotion (L716) y AlfaUiMotionPreferences (L799).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/AlfaUiController.cs` — Todas las pantallas. Build*: BuildMain L797, BuildOnlineChoice L850, BuildOnlineForm L868, BuildLobby L908, BuildTraining L1049, BuildCustomization L1082, BuildSettings L1167, BuildHud L1250, BuildPause L1399, BuildResults L1423, BuildConfirm L1439. API pública Present*/Show* (L224-780). Navegación: SetScreen L2235, HandleEscape L2190, Focus por nombre L2282, DefaultFocusName L2338. Estilos locales: ApplyPositiveStyle L2399, SetRoleButtonSelection L2369, ApplyModularButtonStyle L1783, BuildPalette L1854, SetSceneScrim L2415.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/AlfaUiContracts.cs` — Contratos. Enums AlfaUiScreen (L11-24), OnlineOperationPhase (L26-37), AlfaRole, MatchOutcome y HudActorState. AlfaUiDependencies (L52-76) inyecta fuentes, PanelSprite y ButtonSprite. CharacterPreviewSetup (L78-104). Interfaces IMenuActions (L106-126), IModularCustomizationActions, IRoomMapActions, ISpectatorActions, IRoomModeActions e IVoiceActions. Estados OnlineUiState (L199), LobbyUiState (L255), TrainingUiState (L337), CustomizationUiState (L389), AlfaSettingsDraft (L465), SettingsUiState (L499), EquipmentHudUiState (L543), BloodHudUiState (L567) y ResultsUiState (L609).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/AlfaUiIcon.cs` — Enum AlfaUiIconKind con 23 tipos (L7-33). GetSprite (L77-87) carga `Resources/AlfaUiIcons/<Kind>` como Texture2D y hace Sprite.Create con PPU 128, sin borde. Los tipos sin PNG (Clock, Blood, Copy, Explore, Crosshair, Hands, Flyswatter, Slipper, ElectricRacket, Aerosol) se dibujan con barras Image rotadas (L119-206).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/CharacterPreviewOrbit.cs` — Visor 3D del personalizador sobre RawImage + RenderTexture. Show(role) instancia el prefab en PreviewStage (L61). El arrastre gira 0,25°/px (L127). La rueda hace zoom (L133). SetAngle Frente/Perfil/Espalda (L100) y ResetView (L106). Encuadre por bounds, incluido BakeMesh de skinned (L153-232). El yaw por defecto del mosquito es 35° (L138).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/AlfaModeText.cs` — Textos de modo en castellano rioplatense (Name, Instructions, Score, ResultScore, Clock) compartidos por lobby, entrenamiento, HUD y resultados.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Fonts/` — Bangers-Regular.ttf y AtkinsonHyperlegible-Regular.ttf (licencia OFL) con sus SDF TMP: Dynamic, atlas 1024², sampling 64, padding 8, ClearDynamicDataOnBuild 1, sin FallbackFontAssetTable. GUIDs: Bangers 125662b3…, Atkinson f36455aa…. Ninguna de las dos tiene U+25BC '▼', U+2713 '✓' ni U+2192 '→'.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Resources/AlfaUiIcons/` — 13 PNG RGBA de 128×128 (Audio, Back, Controls, Customize, Exit, Human, Mosquito, Online, Play, Ready, Settings, Training, Video): siluetas blancas que se tiñen con Image.color. Import: textureType 0 (Default, no Sprite), sin mipmaps, sin compresión, maxTextureSize 128.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/unity/ui/tools/build_ui_icons.py` — Script Pillow (supersampling ×4) que regenera los 13 iconos y escribe sus .meta de TextureImporter. Es el patrón a reutilizar para iconos nuevos, o para sprites 9-slice si se opta por PNG offline.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.cs` — Adaptador: implementa IMenuActions, IRoomMapActions, IRoomModeActions, ISpectatorActions e IVoiceActions (L21). Campos serializados HeadingFont y BodyFont (L27), preview (L24-26). En Start (L91-92) crea la UI con AlfaUiDependencies(HeadingFont, BodyFont, preview: CharacterPreviewSetup(...ConfigurePreviewAttention)), sin PanelSprite ni ButtonSprite. SetTrainingMaps y SetRoomMaps (L93-97), FeedbackRequested y ScreenChanged (L99-100). PresentRoom → PresentLobby (L305-317). BeginGame → ShowGameplay (L391). PresentGame cada 0,1 s → PresentHud o PresentResults (L404-447). InterruptGame (L484). ShowOnlineError (L580).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.Preferences.cs` — PresentPreferences (L183-192) emite SettingsUiState y CustomizationUiState (básico o modular). ApplySettings (L193), paletas básicas Skins/Pajamas/MosquitoColors (L29-38) y ApplyPreviewColors, que pone la capa 30 al clon del visor (L291).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/ModularCustomizationPersistence.cs` — PreferencesV1 y V2 serializan AlfaSettingsDraft completo. PreferenceSchemaCodec.TryReadV2 exige una ida y vuelta JSON exacta (L57-72): agregar campos a AlfaSettingsDraft rompe la lectura de los archivos schema 2 existentes.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/AlfaBootstrapBuilder.cs` — Genera LetMeSleepBoot.unity. Asigna fuentes (L30) y las crea con TMP_FontAsset.CreateFontAsset(64, 8, SDFAA, 1024, 1024, Dynamic) + TryAddCharacters (L53-64). Arma PreviewStage en (500,0,0), PreviewCamera (capa 30, fondo sólido (.045,.08,.13), FOV 35), RT Customization.renderTexture de 1024² y luz CustomizationKey (L35-47). La escena que va al build es Assets/Scenes/LetMeSleepHiggsfield.unity (copia con HiggsfieldMaps; fuentes serializadas en L584-585).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/AlfaReviewCapture.cs` — Utilidad de editor Capture(path, w, h, includeUi): pasa el canvas a ScreenSpaceCamera temporalmente y renderiza Camera.main con SingleCameraRequest a PNG.
- `N:/LetMeSleep/Validation/V020/UiRedesignReview01/UiRedesignCapture.cs` — Arnés nativo externo (PlayMode). Carga LetMeSleepHiggsfield.unity, captura el menú real y el HUD humano en Yate (hf-yate-a-la-deriva-v3) a 1920×1080 y 1280×720. Usa cámara overlay URP en el cameraStack sobre la capa 31 y genera una imagen de control sin UI. Verifica píxeles cambiados > 0,1 %, ausencia de isTextOverflowing y glifos visibles. Escribe manifest.json.
- `N:/LetMeSleep/Validation/V020/UiRedesignReview01/run-review.ps1` — Runner: copia el arnés y su .meta temporalmente a Tests/PlayMode, lanza Unity.exe en batch con -runTests y -testFilter doble, espera exit 0, resultado Passed, 2 pasadas y 0 omitidas, y luego borra la copia verificando el SHA-256.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Tests/PlayMode/EquipmentHudVisualEvidenceTests.cs` — Captura sintética del HUD de equipo en estados de carga y reemplazo a 720p y 1080p con el flag -equipmentHudReview <dir>. Crea la UI sin fuentes, así que renderiza con LiberationSans.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Tests/PlayMode/ModularCustomizationUiPlayModeTests.cs` — Cinco pruebas funcionales del personalizador modular. Invocan por reflexión SelectModularCategory, SetCustomizationRole, SetModularCustomizationOption, SaveCustomization, ResetCustomization y CloseCustomization. La captura SyntheticModularCanvasShowsLongLabelsAndScrollAt720And1080 usa -modularCustomizationUiEvidence con ruta bajo N:/LetMeSleep/Validation/V020 y exige que ModularFields no se superponga con Actions.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Tests/PlayMode/CustomizationPersistencePlayModeTests.cs` — Persistencia con la escena real. Opcionalmente captura la fila LobbyExploreButton/LobbyCustomizeButton con -customizationSaveEvidence (ruta bajo Validation/V020) y requiere --lms-validation-data.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/v030/GUIA-ESTILO-BOCETOS.md` — Guía de estilo v0.3 con tokens de paleta UI, forma, tipografía y las 11 pantallas de UI-06. Está SIN COMMIT (git status '??'); es trabajo en curso de otro agente y no hay que pisarla.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/unity/ui/ALFA-UI-SPEC.md` — Especificación alfa: la decisión uGUI está en la §2. Los tokens azul marino originales (Ink900 #081526, Night800 #10233D, Night700 #18365A, Night600 #244B78, Border #4D83BD, Sky400 #4FA9F5) están más cerca de UI-06 que los actuales.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/ceo/UI-REDESIGN-FIRST-PASS-20260920.md` — Registro de la primera pasada de rediseño con cuatro corridas nativas (tres rechazadas por desborde o superposición) y lo que queda pendiente: sala, ajustes, selección y resultados.

### Puntos de extensión

- PUNTO ÚNICO DE RETEMA: AlfaUiTheme.cs L8-22 + ButtonColors L45-58. Si se conservan los nombres de token y se cambian solo los valores, se recolorean ~180 usos sin tocar el controller. Mapeo propuesto (valores de docs/v030/GUIA-ESTILO-BOCETOS.md, contrastados con el muestreo de UI-06): Ink900 #0B1426 (o #001028), Night800 #0E1A30, Night700 #15264A, Night600 #1E3358, Border #3B5E9C, Moon200 #A8B8D8, Sheet100 #F2F6FF, Lamp400 #FFC93C, Sky400 #49B2FF, Mint400 #46C45F, Pajama500 #E0393E, Disabled #6E8299, Scrim #06111F al 82 %. Tokens nuevos: Primary #1F6FE0, PrimaryHi #3A8DFF, PrimaryBorder #7CC0FF, SuccessDark #2E9E48, DangerDark #C62E36, PanelHeader #1C3160, PanelInset #0F1D38, TeamHuman #2F7BFF, TeamMosquito #E0393E, StatusOk #57D26B, StatusWarn #FF6B5E; constantes de radio PanelRadius=14, ButtonRadius=12, SlotRadius=12 y ShadowOffset=4. Borrar WarmSurface y ChalkShadow, que no se usan.
- Texto blanco en botones rellenos (UI-06): cambiar Ink900 → Sheet100 en AlfaUiFactory.Button L238-240 (label), L250-255 (placa), L265-266 (icono) y FeatureButton L302-304; en el controller, ApplyPositiveStyle L2408-2410 y SetRoleButtonSelection L2372 (selectedContent). Pasar el FocusRail de FeatureButton (L309, Lamp400) a PrimaryBorder o blanco.
- Enum de estilos: agregar `enum AlfaButtonStyle { Primary, Success, Danger, Secondary, Quiet, Tab }` en AlfaUiTheme o AlfaUiFactory. Sobrecargar `Button(..., AlfaButtonStyle style)` y dejar la firma bool (primary, destructive) como envoltorio. Absorber ApplyPositiveStyle (Controller L2399), NightPrimaryButton (Factory L173, hoy sin uso), QuietButton (L189), SetRoleButtonSelection (L2369), ApplyModularButtonStyle (L1783) y los ColorBlock inline de BuildPalette (L1862-1869). Así cada pantalla declara la intención (verde para Listo/Crear sala/Aplicar/Jugar de nuevo, rojo para Salir/Iniciar mosquito, azul para el primario).
- SPRITES REDONDEADOS 9-SLICE (runtime, recomendado porque evita .meta e importación):
- Generalizar RoundedSprite (Factory L321-350) a `internal static Sprite RoundedSprite(int radius, int stroke = 0, int feather = 0)` con caché Dictionary por clave.
- Por píxel: d = length(max(|p−c| − (half−r), 0)) − r. Relleno: alfa = clamp01(0,5 − d). Solo trazo: clamp01(0,5 − d) · clamp01(0,5 + d + stroke). Sombra suave: alfa = 1 − smoothstep(−feather, feather, d).
- Tamaño = 2·b + 4, con borde b = radius + feather + 2.
- Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(.5f,.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b,b,b,b)); Image.type = Sliced.
- Para radios distintos sin regenerar: Image.pixelsPerUnitMultiplier (1 = radio 1:1 con Canvas.referencePixelsPerUnit 100). Para nitidez en 4K, generar a 2× y usar multiplicador 2.
- SPRITES 9-SLICE OFFLINE (alternativa): extender el patrón de docs/unity/ui/tools/build_ui_icons.py para escribir PNG en UI/Resources/AlfaUiSkin/ (PanelRounded, ButtonRounded, Frame2px, SoftShadow, Slot). Import como Sprite: textureType 8, spriteMode 1, spriteBorder {x,y,z,w}, spritePixelsToUnits 100, sin mipmaps, clamp, bilinear, sin compresión, alphaIsTransparency 1. Preferir configurarlo con TextureImporter desde un script de editor en Assets/Editor/ProjectBootstrap (AGENTS pide usar las APIs de Unity para la configuración). Cargar con Resources.Load<Sprite>("AlfaUiSkin/…") dentro de AlfaUiFactory y caer en el procedural si devuelve null. Así no se toca la escena LetMeSleepHiggsfield.unity (del Director) ni se agregan campos serializados a AlfaApplication. Existe además el punto de inyección AlfaUiDependencies.PanelSprite/ButtonSprite (Contracts L52-76), que Bootstrap hoy no usa (AlfaApplication.cs L91).
- DEGRADADOS: agregar `internal sealed class AlfaUiVerticalGradient : UnityEngine.UI.BaseMeshEffect` que, en ModifyMesh(VertexHelper), multiplique el color de cada vértice por Lerp(bottom, top, (y − yMin)/(yMax − yMin)), por ejemplo top blanco y bottom (0,78; 0,82; 0,9). Como la transición ColorTint del Button multiplica el color del CanvasRenderer, el tinte de estado sigue funcionando y el degradado aporta la luminancia. Funciona con Image Sliced porque hay filas de vértices en los cortes. Añadirlo en Panel y Button. Para texto, VertexGradient TMP como ya hace BrandLockup.
- SOMBRAS Y BORDES: reemplazar UnityEngine.UI.Outline (Panel L85-87, Button L228-230, Input, Dropdown, placas del HUD) por un hijo 'Frame' con RoundedSprite(radius, stroke:2) teñido Border/PrimaryBorder. Sustituir la Shadow dura (Panel L88-90, Button L231-233) por un hijo 'DropShadow' detrás (SetAsFirstSibling, desplazado 0/−4, RoundedSprite con feather 6, negro al 35 %). Mantener LowerBevel y Shine: dan el aspecto 'botón 3D' de UI-06. OJO: AlfaUiFocusMotion.Bind recibe la Shadow para animar effectDistance (Factory L290, L795); adaptarlo al nuevo nodo.
- LOGO (BrandLockup L141-171): gradiente de 'LET ME' #FFE27A→#FFC93C/#FF9F1C y de 'SLEEP' #9AD8FF→#2F8BFF. outlineWidth 0,2–0,25 con outlineColor #0B1426. Underlay para la extrusión (fontMaterial: EnableKeyword("UNDERLAY_ON"), _UnderlayColor, _UnderlayOffsetY −1, _UnderlaySoftness 0), idealmente como preset de material creado en editor. Tamaños ~112/128. Subtítulo 'HUMANOS VS MOSQUITOS' amarillo (hoy 'HUMANOS CONTRA MOSQUITOS', Controller L819) y lema 'LA NOCHE NUNCA ES TAN TRANQUILA' abajo a la izquierda. No usar nunca el nombre 'Bite & Build'.
- Tipografía opcional: para títulos y botones en MAYÚSCULAS condensadas como UI-06, agregar Atkinson Hyperlegible Bold (OFL) como fontWeightTable[7] del SDF Atkinson para tener negrita real, o una display condensada OFL. Crear el SDF con el mismo patrón que AlfaBootstrapBuilder.Font() (L53-64). Si se agrega una tercera fuente, sumar la propiedad a AlfaUiDependencies y resolver Display() en AlfaUiTheme L35. Cargar por Resources evita tocar la escena.
- Iconos nuevos que pide UI-06: Wifi/Conectando, Warning, Heart/Objetivo, Chat, Invite, Refresh, Accessibility, General (engranaje), Lock, Map, Trophy, KeyCap, Send, Eye y Spinner. Agregar a AlfaUiIconKind (Icon.cs L7-33) y dibujarlos en build_ui_icons.py; GetSprite los carga por nombre. Reemplazar los caracteres '▼' (Factory L538) y '✓' (L581) por iconos (Ready y un triángulo).
- Por pantalla (hooks existentes):
- Menú: BuildMain L797-848.
- Online: fusionar BuildOnlineChoice (L850) y BuildOnlineForm (L868) en una tarjeta con dos pestañas (Tab = Button con estilo Tab que llama ShowCreateRoom o ShowJoinRoom).
- Sala: BuildLobby L908-1047, con contador X/N = Members.Count/RoomRules.Capacity (16) y 'INVITAR AMIGOS' mapeado a CopyRoomCode.
- Entrenamiento: BuildTraining L1049; dos tarjetas humano/mosquito, cada INICIAR llama StartTrainingIntent(role, false) (L1614).
- Personalización: BuildCustomization L1082 + BuildModularCustomization L1710; carril vertical de categorías con icono, rejilla de muestras de 44 px para opciones con HasSwatch y rejilla de miniaturas de 64 px para las que ThumbnailResolver resuelve.
- HUD: BuildHud L1250 + PresentHud L667.
- Pausa: BuildPause L1399.
- Resultados: BuildResults L1423 + PresentResults L751.
- Ajustes: BuildSettings L1167; pestañas que activan y desactivan AudioPanel, ControlsPanel y VideoPanel más un panel General.
- Conexión y error: nueva vista modal dentro de OnlineFormView, alimentada por PresentOnline L260 (IsBusy → spinner; IsOnlineError → tarjeta roja + REINTENTAR).
- Visor con pedestal y fondo (personalización de UI-06): crear en runtime desde Bootstrap, por ejemplo en AlfaApplication.Start después de L91, un pedestal hijo de PreviewStage en la capa 30. El encuadre de CharacterPreviewOrbit.RecalculateFraming solo mide el clon, así que el pedestal no altera el zoom. Alternativa: fondo transparente (PreviewCamera.backgroundColor alfa 0) sobre un panel con degradado, a verificar con post-procesado URP. Si no, cambiar el color de fondo a azul marino en AlfaBootstrapBuilder L38 (requiere regenerar la escena, del Director).
- Contratos a ampliar para datos que pide UI-06 (tocan Bootstrap):
- BloodHudUiState: HumansAlive, HumansTotal, MosquitoesTotal (desde state.Actors en AlfaApplication.PresentGame L440-446).
- ResultsUiState: HumansCount y MosquitoesCount (L410-411).
- Opcional: ILobbyChatActions/LobbyChatUiState si Online implementa chat (hoy no hay canal de chat; los canales de transporte en uso son 2 = apariencia y 3 = movimiento de lobby).
- Refactor previo recomendado: partir AlfaUiController (2516 líneas) en archivos partial por pantalla (AlfaUiController.Lobby.cs, .Hud.cs, etc.) manteniendo nombres de GameObject, métodos privados y campos. Así se pueden paralelizar las pantallas sin conflictos.
- Arnés de captura v0.3: clonar UiRedesignCapture.cs como UiV030Capture y recorrer las 11 pantallas por API pública a 1920×1080 y 1280×720:
- ShowMainMenu; ShowOnlineChoice; ShowCreateRoom("QA"); PresentOnline(new OnlineUiState(OnlineOperationPhase.Connecting, canCancel:true)); PresentOnline(new OnlineUiState(OnlineOperationPhase.RecoverableError, "…", canRetry:true)).
- PresentLobby(new LobbyUiState(true, "ABCDE12345", miembros, false, false, null, false, "…")).
- ShowTraining; ShowCustomization; OpenSettings(AlfaUiScreen.MainMenu).
- StartTraining + ShowGameplay(true) para el HUD humano y el mosquito; ShowPause; PresentResults(new ResultsUiState(MatchOutcome.Humans, false, true, 18, 18, 175)).
- Validar que no haya desbordes, que los glifos sean visibles y que los paneles con nombre no se superpongan (patrón GetWorldCorners de ModularCustomizationUiPlayModeTests L277-280). Escribir bajo una carpeta nueva, p. ej. N:/LetMeSleep/Validation/V030/UiReview01.

### Contratos y restricciones

- Nombres de GameObject acoplados a lógica y pruebas: Focus y DefaultFocusName buscan por nombre (MainPlayButton, CreateChoiceButton, PlayerNameInput, LobbyReadyButton, LobbyCopyButton, TrainingHumanButton, TrainingMosquitoButton, CustomizationHumanButton, CustomizationMosquitoButton, MasterVolumeSlider, PauseContinueButton, ResultsPrimaryButton, ResultsLeaveButton, MainSettingsButton). Las pruebas buscan GameplayHudView/PrivateEquipment, CustomizationView, OptionsPanel, ModularFields, Actions, CategoryScroll, OptionsScroll, ModularCategory_<wire>, ColorSwatch, CategoryTitle, Status, LobbyExploreButton, LobbyCustomizeButton, CustomizationSaveButton, CustomizationResetButton y CustomizationBackButton. ModularCustomizationUiPlayModeTests exige que CategoryScroll esté DENTRO de OptionsPanel y que ModularFields quede por encima de Actions: mover las categorías a un carril izquierdo obliga a actualizar esas pruebas.
- Métodos y campos privados invocados por reflexión en pruebas: SelectModularCategory, SetCustomizationRole, SetModularCustomizationOption, SaveCustomization, ResetCustomization y CloseCustomization en AlfaUiController; el campo 'customizationState'; y en AlfaApplication 'ui', 'game', 'presentation', 'map', 'settings', 'appearance', 'localAppearanceDraft', 'previewAppearance', 'publishedModularAppearance', 'localModularAppearanceDraft', 'previewModularAppearance', 'appearanceAt', 'loadedPreferenceSchema' y 'modularCustomizationRuntime'. No renombrarlos.
- IMenuActions y las interfaces opcionales (IModularCustomizationActions, IRoomMapActions, IRoomModeActions, ISpectatorActions, IVoiceActions) son el contrato con Bootstrap. La UI emite intenciones y nunca decide autoridad. Los latches se liberan solo con el próximo Present* autoritativo; conservar esa semántica en cualquier botón nuevo (tarjetas de entrenamiento, pestañas online).
- AlfaSettingsDraft se serializa entero en PreferencesV1 y V2. PreferenceSchemaCodec.TryReadV2 compara el JSON compactado con su propia reserialización (ModularCustomizationPersistence.cs L57-72). Agregar campos (Idioma, Subtítulos, Daltonismo, ChatDeVoz) hace que los preferences.json schema 2 existentes se clasifiquen como UnsupportedVersion y queden bloqueados para escritura. Requiere schema 3 o relajar la comparación, en coordinación con el dueño de Bootstrap/persistencia.
- Reglas de producto (AGENTS.md y GUIA-ESTILO): nombre 'Let me sleep', nunca 'Bite & Build'. Sin crafting, armas de fuego, clases ni recompensas. Sin marcas de picadura. Roles aleatorios en cada ronda: no mostrar listas por equipo en la sala antes de empezar. Salas privadas por código: no hay navegador de salas ni contraseña; capacidad fija de 16 (RoomSession.cs L31). Humano por defecto con pijama, pantuflas y gorro de dormir.
- Propiedad: la escena LetMeSleepHiggsfield.unity (la que va al build; fuentes serializadas en L584-585), los manifiestos y los asmdef son del Director. LetMeSleep.UI solo referencia Core, ugui, TMP e InputSystem (LetMeSleep.UI.asmdef); no puede referenciar Gameplay, Online ni Presentation. Los datos nuevos llegan por los estados de Contracts.
- Resolución mínima de 1280×720 con canvas de referencia 1920×1080 y match 0.5. Los gates vigentes exigen isTextOverflowing == false en todas las etiquetas activas, glifos visibles y píxeles de UI > 0,1 %. Los estados no pueden depender solo del color (spec §3). El foco por teclado y mando (navegación Automatic) debe seguir funcionando.
- Los gates de evidencia existentes validan rutas: UiRedesignCapture exige salida bajo N:\LetMeSleep\Validation\V020\UiRedesignReview01\ y que la carpeta no exista; ModularCustomizationUiPlayModeTests y CustomizationPersistencePlayModeTests exigen N:/LetMeSleep/Validation/V020. Para v0.3 hay que actualizar esas aserciones o usar V020.
- Movimiento reducido: todo efecto nuevo (degradados animados, spinner, pulsos) debe respetar AlfaUiMotionPreferences.ReducedMotion y usar Time.unscaledDeltaTime (la UI funciona con la pausa activa).
- Solo lectura en esta fase. Además, run-review.ps1 y ModesUI/Compile.ps1 apuntan a N:/LetMeSleep/Repository, no a este worktree. El Repository está en el mismo commit c178bba; la UI es idéntica salvo CRLF, pero hay un cambio sin commit en 'AtkinsonHyperlegible-Regular SDF.asset' (el WIP de fuente que el reporte de rediseño explícitamente no incorporó).

### Brechas frente a bocetos

- 1 MENÚ PRINCIPAL:
- La estructura coincide: logo arriba a la izquierda, columna de botones y escena 3D a la derecha.
- Difiere: el primario es ámbar con texto oscuro (UI-06: azul #0050B0 con texto blanco) y los secundarios son gris neutro (UI-06: azul marino #082048).
- El logo va en crema/ámbar/celeste con contorno fino; UI-06 lo quiere grande, amarillo/azul, con contorno grueso y extrusión.
- El subtítulo dice 'HUMANOS CONTRA MOSQUITOS'. Falta el lema 'LA NOCHE NUNCA ES TAN TRANQUILA'. 'JUGAR ONLINE' lleva subtítulo; UI-06 dice solo 'JUGAR'.
- La escena muestra al humano sentado con matamoscas en lugar del humano durmiendo en la cama con el mosquito (es contenido 3D de Presentation/Mapas, no de UI).
- 2 CREAR/UNIRSE:
- Hoy son dos pasos: OnlineChoice con dos tarjetas y luego el formulario con nombre y código. UI-06 los muestra en un solo panel 'JUGAR ONLINE' con pestañas CREAR SALA / UNIRSE A SALA.
- Faltan: nombre de sala, selector de mapa con miniatura y flechas, dropdown de modo, jugadores máximos, contraseña opcional, lista 'PARTIDAS DISPONIBLES' con señal, botón ACTUALIZAR y CTA verde 'CREAR SALA'.
- El navegador de salas, la contraseña y los máximos no existen en Online (salas privadas por código). Mapa y modo hoy los elige el anfitrión dentro de la sala. IMenuActions.CreateRoom solo recibe el nombre del jugador.
- 3 SALA DE ESPERA:
- Hoy: cabecera con código y COPIAR, roster en panel izquierdo y reglas a la derecha.
- Faltan: cabecera 'ESPERANDO JUGADORES' con cuenta regresiva '00:28' (no existe en RoomRules/RoomView; el inicio es manual del anfitrión) y 'X/N' (derivable de Members.Count y Capacity).
- Faltan también el mapa y el modo en la cabecera ('Casa Principal · Modo Sangre'; hoy están en el panel de reglas) y los nombres flotantes sobre personajes 3D (no hay nameplates en LobbyMovementRuntime/LobbyVisualPresenter).
- Chat con historial, campo 'Escribir mensaje…' y enviar: no hay canal de chat en Online.
- INVITAR AMIGOS: hoy se llama 'COPIAR PARA INVITAR'.
- LISTO verde grande existe, pero en el panel de reglas.
- La selección de cantidad de humanos AUTO/1-5 y los ciclos de modo, tiempo y mapa no están en UI-06 pero deben conservarse para el anfitrión.
- 4 ENTRENAMIENTO:
- Hoy: una tarjeta izquierda con botones de rol tipo toggle, ciclos MODO y MAPA, y un único EMPEZAR.
- UI-06: dos tarjetas grandes lado a lado, 'ENTRENAR COMO HUMANO' (azul, con ilustración o render del humano con linterna) y 'ENTRENAR COMO MOSQUITO' (roja), cada una con descripción y su propio INICIAR (azul y rojo).
- Faltan ilustración o render por rol y un INICIAR por tarjeta. Modo y mapa pueden quedar como fila inferior.
- 5-6 PERSONALIZACIÓN:
- Hoy: dos paneles (visor de 900 a la izquierda y opciones de 720 a la derecha) con pestañas de rol, listas de TEXTO para categorías y opciones (el modular muestra una muestra o miniatura de 36 px) y paletas básicas como botones de 216×66 con nombre.
- UI-06: tres columnas. Carril vertical de categorías con iconos: PERSONAJE/COLORES/ACCESORIOS para el humano y CUERPO/ALAS/OJOS/PROBÓSCIDE/COLORES para el mosquito. Visor 3D central grande sobre pedestal y fondo de escena. Panel derecho con rejilla de muestras cuadradas con check ('COLORES DE ROPA', 'COLOR DE CUERPO'), rejillas de miniaturas ('ESTILO DE ALAS', 'OJOS') y 'VISTA PREVIA' con tres miniaturas FRENTE/ESPALDA/LADO (hoy son botones de texto).
- Falta 'VISTA PREVIA EN VUELO' (necesita una animación en el clon: Presentation).
- El fondo del visor es un color sólido #0B1421, sin pedestal.
- 7 HUD HUMANO:
- Hoy: insignia de rol arriba a la izquierda, reloj y SANGRE arriba al centro, tarea privada arriba a la derecha, bandeja de equipo de 700 px abajo a la derecha con placas de texto e icono de 22 px, pista abajo a la izquierda y prompt abajo al centro.
- UI-06: tarjeta 'OBJETIVO' arriba a la izquierda con icono, texto ('Evitá que te piquen') y barra roja con corazón '100/100'; reloj grande al centro; 'Mosquitos 2/4' con icono arriba a la derecha; barra de objetos centrada abajo con cuatro ranuras CUADRADAS de icono grande y número arriba a la izquierda.
- Faltan: contadores por equipo (BloodHudUiState solo tiene MosquitoesAlive) y barra de salud (no existe como mecánica; puede representar la sangre o el objetivo). Las ranuras deben pasar a icono primero, con PNG reales para Flyswatter, Slipper, ElectricRacket y Aerosol (hoy son barras procedurales).
- 7b HUD MOSQUITO:
- Hoy usa la misma vista: insignia MOSQUITO, reloj y SANGRE, y una pista de texto en una línea abajo a la izquierda.
- UI-06: barra 'MOLESTÁ AL HUMANO' con icono arriba a la izquierda, 'Humanos 1/4' y reloj arriba a la derecha, y leyenda de controles abajo a la derecha con teclas dibujadas (ratón Volar, Shift Acelerar, E Interactuar).
- Faltan el widget de teclas, el contador de humanos y la disposición propia del rol.
- 8 PAUSA:
- Hoy: tarjeta centrada con fondo oscurecido: PAUSA, CONTINUAR, AJUSTES, CONTROLES (duplica AJUSTES), bloque de voz y SALIR.
- UI-06: panel izquierdo 'PARTIDA EN PAUSA' con la escena visible a la derecha, CONTINUAR (azul seleccionado), AJUSTES, VOLVER A LA SALA y SALIR DE LA PARTIDA.
- 'VOLVER A LA SALA' a mitad de ronda no existe (solo el anfitrión puede terminar la ronda con room.FinishRound). Hay que decidir si se omite o se mapea a SALIR.
- Los controles de voz no están en el boceto pero deben conservarse.
- 9 RESULTADOS:
- Hoy: tarjeta centrada con título H1 'GANARON LOS HUMANOS' en texto crema, stats en texto plano y botones VOLVER AL LOBBY/REPETIR y SALIR.
- UI-06: banner ilustrado grande '¡HUMANOS GANAN!' (o '¡MOSQUITOS GANAN!') en amarillo, con personajes celebrando sobre la escena nocturna; fichas de marcador 'HUMANOS 4' (azul) y 'MOSQUITOS 2' (roja); botones VOLVER A LA SALA (azul) y JUGAR DE NUEVO (verde).
- Faltan: el copy '¡X GANAN!', los recuentos por equipo (ResultsUiState no los trae), la ilustración o render de celebración y el estilo verde del CTA.
- 10 AJUSTES:
- Hoy: una tarjeta de 1280×840 con tres paneles simultáneos (Audio, Controles, Video) y los botones APLICAR, DESHACER CAMBIOS y VOLVER.
- UI-06: pestañas verticales GENERAL/AUDIO/VIDEO/CONTROLES/ACCESIBILIDAD, filas etiqueta + control con valor a la derecha (slider azul #3098F0 con %, ciclos ‹ Activado ›), y RESTAURAR + APLICAR (verde).
- Faltan: Idioma (hoy solo español), Mostrar subtítulos, Modo daltonismo y Chat de voz activado/desactivado (hoy solo PTT y micrófono), y la pestaña Accesibilidad (ahí puede ir REDUCIR MOVIMIENTO).
- Opciones nuevas implican cambio de esquema de preferencias (ver restricciones). Los dropdowns usan '▼' y '✓', que no existen en Atkinson.
- 11 CONEXIÓN Y ERRORES:
- Hoy solo hay una línea de texto de estado dentro del formulario online (en Pajama500 si es error) más botones CANCELAR/REINTENTAR.
- UI-06: tarjeta 'CONECTANDO…' con icono wifi, subtítulo y spinner, y tarjeta de error con triángulo rojo, título 'NO SE PUDO CONECTAR' en rojo, texto explicativo y botón REINTENTAR.
- Faltan: la vista o tarjeta dedicada, los iconos Wifi y Warning, el spinner animado y la presentación de errores fuera del formulario (p. ej. un corte a mitad de ronda hoy va a Resultados como 'RONDA INTERRUMPIDA').
- GLOBAL:
- Paleta azul marino; primario azul, CTA verde y peligro rojo con degradado vertical; texto blanco sobre botones.
- Bordes de 2 px #3B5E9C con esquinas de 10-14 px (hoy 12 px con borde por componente Outline) y sombra suave inferior.
- Encabezados en sans MAYÚSCULAS condensada y gruesa (hoy Atkinson con negrita sintética).
- Iconos más ricos y a color (hoy siluetas blancas teñidas).

### Riesgos

- Rediseñar sin respetar los nombres de GameObject rompe el foco por nombre y varias pruebas PlayMode (ModularCustomizationUiPlayModeTests exige CategoryScroll dentro de OptionsPanel y ModularFields por encima de Actions; CustomizationPersistence exige que LobbyExploreButton y LobbyCustomizeButton no desborden). También rompe la reflexión sobre métodos privados.
- Agregar opciones de Ajustes (idioma, subtítulos, daltonismo, chat de voz) a AlfaSettingsDraft invalida la ida y vuelta exacta de PreferenceSchemaCodec.TryReadV2. Los usuarios con preferences.json schema 2 quedarían bloqueados para escritura ('Los ajustes son de otra versión'). Hace falta schema 3 o un lector tolerante.
- Glifos: '▼' (Dropdown, Factory L538) y '✓' (ítem del dropdown, L581) no existen en Atkinson ni en Bangers. No hay fallback configurado; '✓' tampoco está en LiberationSans. Probablemente '▼' se dibuja con otra fuente y '✓' como cuadrado de glifo faltante al abrir el dropdown de FPS o de micrófono. '→' tampoco existe. Usar iconos.
- Las capturas sintéticas (EquipmentHudVisualEvidenceTests y la captura modular) crean la UI SIN fuentes y renderizan con LiberationSans: no representan la tipografía real. Solo UiRedesignCapture usa la escena real y las fuentes serializadas.
- Rendimiento: un único Canvas para todas las vistas y el HUD. PresentHud cada 0,1 s cambia textos y fuerza reconstrucción del lote. Cada botón tiene AlfaUiFocusMotion con Update por frame. Los componentes Outline y Shadow multiplican vértices (Outline ×5). Añadir degradados y sombras en capas aumenta el overdraw. Conviene un Canvas propio para el HUD y sustituir Outline por sprites de trazo.
- El sprite redondeado procedural de 48 px (radio 12) se ve blando si se agranda el radio o la escala sube por encima de 1 (1440p/4K). Generarlo a 2× con pixelsPerUnitMultiplier 2, o por radio.
- Datos que UI-06 muestra y no existen: cuenta regresiva de sala, chat, navegador de salas, contraseña, jugadores máximos, conteo por equipo en HUD y resultados, salud del humano, 'VOLVER A LA SALA' desde pausa a mitad de ronda y 'VISTA PREVIA EN VUELO'. Necesitan cambios en Core, Online, Bootstrap o Presentation (otros dueños) o hay que omitirlos. No inventar mecánicas desde el boceto (la GUIA lo prohíbe).
- La escena del build (LetMeSleepHiggsfield.unity) serializa fuentes y referencias de AlfaApplication y pertenece al Director. Nuevas dependencias (sprites, tercera fuente, pedestal) conviene cargarlas por Resources o crearlas en runtime para no regenerar la escena.
- Evidencia y worktree: los scripts de gate apuntan a N:/LetMeSleep/Repository/unity y a rutas V020. Correrlos sin adaptar valida otro árbol, que además tiene el SDF de Atkinson modificado sin commit.
- AlfaUiController es un monolito de 2516 líneas: si varios agentes editan pantallas a la vez habrá conflictos. Conviene dividirlo en archivos partial antes del rediseño.
- El fondo transparente del visor 3D para componer sobre un panel con degradado puede no conservar el alfa con post-procesado URP. Verificarlo, o usar pedestal y fondo 3D en la capa 30.
- docs/v030/GUIA-ESTILO-BOCETOS.md está sin commit en este worktree (trabajo de otro agente). Sus tokens difieren levemente del muestreo real de UI-06: paneles medidos ~#081830–#082040 frente a #15264A en la guía, verde medido #20A040–#28B040. Fijar los valores definitivos con captura comparativa, no a ojo.

### Verificación

Lectura estática completa de UI/Runtime/*.cs (AlfaUiController, AlfaUiFactory, AlfaUiContracts, AlfaUiIcon, CharacterPreviewOrbit, AlfaUiTheme, AlfaUiRuntime, AlfaModeText), del asmdef, de las fuentes y .meta de iconos, de los parciales de Bootstrap que tocan la UI (AlfaApplication.cs, .Preferences, .ModularCustomization, .MenuScene, .Audio, .Voice, .Probe, .Appearance) y de ModularCustomizationPersistence.cs. También de las herramientas de editor (AlfaBootstrapBuilder, AlfaReviewCapture, V020ValidationRunner), las pruebas PlayMode relacionadas y la documentación (ALFA-UI-SPEC, ALFA-UI-RUNTIME, UI-REDESIGN-FIRST-PASS, GUIA-ESTILO-BOCETOS sin commit).
Git solo en lectura: e2d7d80 cambió la paleta azul marino a neutra y el worktree está limpio. El contenido de la UI es igual al de N:/LetMeSleep/Repository salvo CRLF, confirmado con diff ignorando CR.
Se revisaron visualmente UI-06, UI-01, UI-05, PER-08 y ENV-05, más las capturas de UiRedesignReview01. Los colores de UI-06 se muestrearon con PIL (histograma cuantizado por región). La cobertura de glifos de los TTF se comprobó con fontTools.
No se ejecutó Unity, ninguna build ni prueba.

CÓMO SE EJECUTARON LOS GATES DE UI (según scripts y logs):
- Nativo: el runner es N:/LetMeSleep/Validation/V020/UiRedesignReview01/run-review.ps1. Copia UiRedesignCapture.cs y su .meta a <proyecto>/Assets/LetMeSleep/Tests/PlayMode/ y ejecuta:
  N:/Unity/Editors/6000.3.24f1/Editor/Unity.exe -batchmode -projectPath N:/LetMeSleep/Repository/unity -runTests -testPlatform PlayMode -testFilter "LetMeSleep.Tests.PlayMode.UiRedesignCapture.MenuAndHudAtTwoResolutions;LetMeSleep.Tests.PlayMode.EquipmentHudVisualEvidenceTests.RealCanvasRendersEquipmentHudAt720And1080WithoutOverflow" -testResults <run>/results.xml -logFile <run>/unity.log -force-d3d11 --lms-validation-data <run>/data -uiReviewOutput <run>/captures -equipmentHudReview <run>/equipment
  El gate exige exit 0, result=Passed, passed=2 y skipped=0. Al terminar borra la copia verificando el SHA-256. IncludeHarness.targets incluye el archivo externo en el csproj de PlayMode para compilarlo fuera de Unity.
- Historial: Run-174454 falló 1/2 (EquipmentSlot1 desborda a 1280x720). Run-175045 falló 1/2 (StaminaLabel). Run-175446 pasó 2/2 pero se rechazó visualmente por superposición. Run-180449 pasó 2/2 y fue aceptado tras inspeccionar los ocho PNG.
- Funcional: -testFilter LetMeSleep.Tests.PlayMode.ModularCustomizationUiPlayModeTests (Functional-…: 5 pasadas, 1 omitida por falta de flag). La visual se corrió con -testFilter …SyntheticModularCanvasShowsLongLabelsAndScrollAt720And1080 -modularCustomizationUiEvidence <dir> (ModularVisual-…: 1/1).
- Compilación offline por CPU: N:/LetMeSleep/Validation/V020/ModesUI/Compile.ps1 genera csproj netstandard2.1 contra los DLL gestionados de Unity y Library/ScriptAssemblies y ejecuta `dotnet build`. Sus logs (cpu-ui-*.log, cpu-contract-*.log) dan 0 errores y 0 advertencias. Tiene fijado $repository='N:/LetMeSleep/Repository'; para el worktree hay que parametrizarlo.
- Otras vías:
  - Sonda de player de desarrollo: --lms-probe-output <dir> (más --lms-probe-menu-seconds, --lms-probe-mode y --lms-probe-map) genera menu.png, human.png, mosquito.png y player-probe.json (AlfaApplication.Probe.cs).
  - AlfaReviewCapture.Capture en editor.
  - Fixtures batch antiguos en docs/unity/ui/fixtures (Run-TrainingMapBatch.ps1, HiggsfieldTrainingUIBatch.cs), que capturan por RenderTexture a 1280x720 y 1920x1080.
- Para v0.3: clonar el arnés como UiV030Capture recorriendo las 11 pantallas de UI-06 por la API pública, cambiar la aserción de ruta a V030 y el projectPath al worktree, y conservar los gates de desborde, glifo visible, píxeles cambiados y no superposición. Cada corrida necesita un turno de GPU/Unity del Director.

## customization

La personalización modular ya existe como código probado con catálogos sintéticos, pero está apagada en producción. No hay ningún asset `CharacterCustomizationCatalog` en `Assets`. `AlfaApplication.ModularCustomizationProvider` no está serializado en la escena de build `LetMeSleepHiggsfield.unity` y queda en null. Ningún prefab (LMS_Human, LMS_Human_FirstPerson, LMS_HumanMenu, LMS_Mosquito) tiene `CharacterCustomizationHost` ni `CharacterModularVisualAssembler`: la búsqueda por GUID en .asset/.prefab/.unity no devolvió resultados. Por eso el juego real sigue en modo básico, con 3 paletas: piel ×4, pijama ×5 y mosquito ×4. Ese modo usa el canal 2 v1 y `preferences.json` en schema 1. En ese modo el visor 3D sí está conectado: la escena asigna PreviewCamera, PreviewStage y PreviewTexture.

La captura con "El visor 3D se conecta al personaje del juego." y "BODY-A" no es del juego. Es la evidencia sintética del test `ModularCustomizationUiPlayModeTests.SyntheticModularCanvasShowsLongLabelsAndScrollAt720And1080`, guardada en `N:/LetMeSleep/Validation/V020/ModularCustomizationUiNative02/modular-customization-1920x1080.png`. Ese test:
- crea la UI sin `CharacterPreviewSetup` (línea 72), así que el orbit queda sin enlazar y aparece el texto (AlfaUiController.cs:1117-1120);
- usa `EvidenceSnapshot()`, cuya opción visual tiene `Label = optionId.ToUpperInvariant()` → "BODY-A" (línea 389);
- no pasa `thumbnailResolver`, así que no hay miniatura;
- deja `modularPreviewAvailable=false`, de ahí el mensaje "La vista de estas piezas llegará…".

Para llegar a los bocetos PER-04..08 hacen falta cuatro cosas:
1. Arte modular real: prefabs de pieza con `CharacterCustomizationPart` y huesos/sockets del rig nuevo.
2. Un catálogo de producción con slots, opciones, 13 mapeos legacy y miniaturas.
3. Hosts en los prefabs de rol y el proveedor en la escena, instalados por herramientas de editor.
4. Ajustes de código: etiqueta del visor, re-encuadre tras ensamblar, zoom y pitch, vista de miniaturas en grilla, resolución de incompatibilidades, colores sin consumidor, límite de 24 slots, aplicación inmediata en gameplay y gate facial para ojos y expresiones.

### Cómo funciona

Rutas relativas a `unity/Assets/LetMeSleep` salvo que se indique otra cosa. Revisé el worktree en HEAD c178bba (rama claude/v0.3.0).

## 1. Modelo de datos (Core, sin Unity)

- **Selección.** `AppearanceSelection{Schema=1, Human:AppearanceLoadout, Mosquito:AppearanceLoadout}` (`Core/Customization/AppearanceSelection.cs:70-110`). Cada loadout es un array de `{SlotId, OptionId}`. La carga de ambos roles se guarda y se envía junta. El rol que se edita en la pantalla (`modularEditedRole`) es sólo estado de UI.
- **Categoría = slot.** `CustomizationSlotRecord`/`SlotDefinition` tiene estos campos:
  - `Role`, `SlotId`: regex `^[a-z][a-z0-9_.]{0,31}$`.
  - `Label`: texto de UI. Si está vacío, la categoría se oculta.
  - `WireSlotId`: byte 1..24, único en todo el catálogo.
  - `Required`, `AllowsNone`, `IsBaseSlot`: máximo uno por rol, y sus opciones deben ser SkinnedPart o Composite.
  - `DefaultOptionId`, `CompatibilityFamily`.
- **Opción.** `CustomizationOptionRecord`/`OptionDefinition` tiene estos campos:
  - `OptionId`: regex `^[a-z][a-z0-9_.-]{0,47}$`. El id `none` está reservado: Kind None, WireOptionId 0, y sólo vale si el slot tiene AllowsNone.
  - `WireOptionId`: ushort 1..65534, único por slot.
  - `Kind`: None, Color, SkinnedPart, SocketPart o Composite.
  - `Label`, `HasSwatch`/`Swatch` (RGBA empaquetado).
  - `AssetId`: GUID. `RuntimeAsset`: prefab. `Thumbnail`: Sprite.
  - `CompatibleBaseOptionIds`, `CompatibilityTags`, `IncompatibleSlotIds`.
- **Validación.** `CustomizationCatalogSnapshot.TryCreate` valida todo, incluidas estas reglas: una opción Color debe tener swatch, y una opción visual debe tener AssetId y RuntimeAsset (l.131-237).
- **RuntimeReady.** Es true sólo si ambos roles tienen un slot base Required cuyo default tiene asset (l.230-234).
- **Normalización.** `TryNormalize` rechaza slots u opciones desconocidos, `none` en un slot requerido, bases incompatibles, `IncompatibleSlotIds` activos y familias sin tags comunes. Los slots faltantes se completan con su default (l.279-317). Consecuencia: **agregar** slots nuevos es compatible con preferencias guardadas; **borrar o renombrar** un SlotId u OptionId invalida los archivos guardados.
- **Fingerprint.** SHA-256 de catalogId, revision y todos los campos estructurales. Excluye Label y Thumbnail, así que se pueden renombrar etiquetas o cambiar miniaturas sin romper la red (l.319-337). `NetworkFingerprint` son los primeros 8 bytes.

## 2. Catálogo Unity y gate de activación

- **Asset.** `CharacterCustomizationCatalog` (ScriptableObject). `TryCreateSnapshot` lo proyecta a Core. `TryGetAssets(slot, option, out RuntimeAsset, out Thumbnail)`.
- **Proveedor.** `ModularCustomizationRuntimeProvider` (MonoBehaviour con `Catalog` y `LegacyMappings[]`) se asigna a `AlfaApplication.ModularCustomizationProvider` (`Bootstrap/AlfaApplication.ModularCustomization.cs:12`). `LoadPreferences` llama a `ResolveModularCustomizationRuntime` (`Preferences.cs:56`).
- **Condiciones de `TryResolve`.**
  - snapshot RuntimeReady;
  - `LegacyAppearanceMapper` con los 13 ids exactos: piel light/warm/tan/dark, pijama blue/red/green/purple/yellow, mosquito red/blue/green/purple. Cada grupo apunta a opciones Color de un slot propio, y piel y pijama deben ir a slots distintos;
  - `HumanPrefab` y `MosquitoPrefab` con `CharacterView`, `CharacterCustomizationHost` y `CharacterModularVisualAssembler` que pasen `CanApply` con la selección default.
- **Estado hoy.** Faltan las tres cosas: no hay asset de catálogo, no hay proveedor en `LetMeSleepHiggsfield.unity` y no hay hosts en los prefabs. Resultado: `modularCustomizationRuntime == null` y el modo básico queda activo.

## 3. Persistencia

- **Archivo.** `DataPath/preferences.json`, escrito por `PreferenceFileStore` con copia de respaldo:
  - Editor: `N:/LetMeSleep/UserData/Unity`.
  - Override en editor o dev build: `--lms-validation-data <abs>`.
  - Build: `Application.persistentDataPath` (`AlfaApplication.cs:58-84`).
- **Schema 1** (`PreferencesV1`): `appearance` y `localAppearanceDraft` como `BasicCustomizationDraft{Role, SkinColorId, PajamaColorId, MosquitoColorId}`.
- **Schema 2** (`PreferencesV2`): `publishedAppearance` y `localAppearanceDraft` como `AppearanceSelection`. JSON: `{"schema":2,"playerName":..,"settings":{..},"publishedAppearance":{"Schema":1,"Human":{"Selections":[{"SlotId":..,"OptionId":..}]},"Mosquito":{..}},"localAppearanceDraft":{..},"resolutionWidth":..,"resolutionHeight":..}`. `TryReadV2` exige round-trip exacto de JsonUtility (`ModularCustomizationPersistence.cs:60-73`), así que cualquier campo nuevo en el DTO requiere un schema nuevo.
- **Flujo de carga.**
  - Con schema 2 y runtime, `TryActivateModularPreferences` normaliza y valida con `CanApply`.
  - Con schema 2 sin runtime, bloquea escrituras y conserva el archivo.
  - Con schema 1 y runtime, `TryMigrateLegacyPreferences` migra y guarda V2 (`AlfaApplication.ModularCustomization.cs:86-115`, `Preferences.cs:121`).
- **Semántica de guardado.**
  - Cambiar una opción: autoguarda el borrador privado (`PreviewModularCustomization`, l.164-201) sin publicar.
  - APLICAR: `SaveModularCustomization` (l.203-239) escribe publicado = borrador, pone `appearanceAt=0` para forzar el envío y, si el disco falla, restaura memoria y preview.

## 4. Sincronización online (canal 2, fiable)

- **Envío.** `TickAppearance` corre cada 2 s. Construye el paquete con `TryBuildAppearancePacket`:
  - modo modular: `AppearanceWireCodec.TryEncode` v2 = [0x02][roomCode: ushort len + UTF-8 ≤16][fingerprint u64][count byte][count × (WireSlotId byte, WireOptionId ushort)], en orden de WireSlotId e incluyendo todos los slots de ambos roles; máximo 128 B;
  - modo básico: v1 = [0x01][room][skin][pajama][mosquito].
  
  Luego lo envía a cada miembro conectado (`Appearance.cs:15-35`).
- **Recepción.** `AppearancePeerState.TryReceive` valida sala, membresía, rate limit de 0,25 s, fingerprint, `count == Slots.Count` y normalización canónica. El receptor aplica `canApply` antes de reemplazar el último valor válido.
- **Aplicación.** `ApplyLive` elige la carga por `host.Role` autoritativo (`Appearance.cs:56-87`). Si falta la v2 de un par, cae a la migración v1 o al default.
- **Enviado vs. local.** Sólo viajan códigos numéricos. Labels, miniaturas y el borrador nunca se envían.

## 5. Ensamblado visual (`CharacterModularVisualAssembler`)

- **Host.**
  - `CharacterView`, host y ensamblador deben estar en el mismo root.
  - `host.RigId` no vacío.
  - `PartsRoot`: hijo dedicado de `VisualRoot`, fuera del árbol del Animator.
  - `VisualRoot.localScale == ExpectedVisualScale`: humano 1, mosquito 0,5 (`CharacterContentBuilder.cs:336`).
  - `OwnedBaseRenderers` no vacío y fuera de PartsRoot.
  - `ColorChannels` válidos (l.167-191).
- **Pieza** (prefab con `CharacterCustomizationPart` en la raíz, l.193-275):
  - transform identidad;
  - Role, SlotId y Kind iguales al catálogo;
  - TargetRigId y TargetVisualScale iguales al host;
  - sólo componentes Transform, Renderer, MeshFilter y Part. Un FBX importado con Animator no sirve directo;
  - según Kind: SkinnedPart = sólo skinned; SocketPart = sólo sockets; Composite = ambos;
  - cada SkinnedMeshRenderer declara `RootBonePath` y `BonePaths[]`, uno por bindpose, relativos a `view.Animator.transform` y únicos por nombre;
  - cada socket declara `PartRoot` (la raíz o un hijo directo) y un `AnchorName` que exista en `CharacterView.Anchors`;
  - todo renderer debe estar declarado;
  - `FacialImpact.ReplacesTrackedFace` se rechaza (l.206-207);
  - `OccludesExistingFace` exige `FirstPersonHeadRenderers`.
- **Colores.**
  - Cada slot Color seleccionado debe tener al menos un canal declarado por el host o por una pieza activa (l.277-290).
  - Cada canal debe referir a un slot Color seleccionado del mismo rol.
  - El color se aplica con `CharacterView.ApplyColor(slotId, color)` → MPB `_BaseColor` (`CharacterView.cs:153-164`).
- **Aplicación** (`Prepare`/`Commit`, l.316-424):
  1. Instancia las piezas en un `CustomizationStaging` inactivo bajo PartsRoot.
  2. Remapea `rootBone` y `bones` a los huesos del host por path.
  3. Reparenta los roots de socket bajo el anchor.
  4. Registra cabezas de primera persona y canales en `CharacterView.SetCustomizationBindings`.
  5. Aplica colores, deshabilita `OwnedBaseRenderers` y activa todo.
  
  Siempre debe haber exactamente una base seleccionada (l.158-159). El cuerpo autoral del host siempre queda oculto y lo reemplaza la pieza base del catálogo. Si algo falla, se restaura el estado anterior. La misma selección es idempotente gracias a `appliedKey`.

## 6. UI y visor

- **Construcción.** `AlfaApplication.Start` crea la UI con `new CharacterPreviewSetup(PreviewCamera, PreviewStage, PreviewTexture, HumanPrefab, MosquitoPrefab, ConfigurePreviewAttention)` (`AlfaApplication.cs:91-92`).
- **Visor.** `CharacterPreviewOrbit` vive en el RawImage `CharacterPreview`:
  - `Show(role)` instancia el prefab bajo `CustomizationStage` (500,0,0);
  - la cámara `CustomizationCamera` sólo renderiza la capa 30 al RT de 1024²;
  - `ApplyPreviewColors` corre en cada Update: pone la capa 30, apaga colliders y aplica la apariencia modular o básica;
  - `SetVisible` sólo en la pantalla Customization (`AlfaUiController.cs:2252`).
- **Modo modular** (`PresentPreferences` → `TryCreateModularUiState`, con `modularPreviewAvailable:true` y `thumbnailResolver = Catalog.TryGetAssets`):
  - `VisibleModularSlots` = slots del rol con Label y al menos una opción con Label;
  - categorías = botones `ModularCategory_<WireSlotId>` con texto `slot.Label` (y "> " si está seleccionada);
  - opciones = botones `ModularOption_<WireSlotId>_<WireOptionId>` con `option.Label` y, a la izquierda, un cuadro de 36×36: swatch si `HasSwatch`, o Sprite del resolver si existe (l.1757-1775);
  - clic → `SetModularCustomizationOption`: normaliza y, si falla, sólo muestra "No se pudo seleccionar esa opción"; si no, `SendModularPreview` → `AlfaApplication.PreviewModularCustomization`.
- **Modo básico:** tres paletas fijas (`BuildPalette`).

## 7. Por qué la captura muestra "El visor 3D se conecta al personaje del juego." y "BODY-A"

- **La captura es sintética.** Es `N:/LetMeSleep/Validation/V020/ModularCustomizationUiNative02/modular-customization-1920x1080.png` (también existe `UiRedesignReview01/ModularVisual-*/captures`), generada por `ModularCustomizationUiPlayModeTests.SyntheticModularCanvasShowsLongLabelsAndScrollAt720And1080`. El recibo dice "Synthetic catalog and local canvas only".
- **Texto del visor.**
  - La UI se crea con `new AlfaUiDependencies(persistentAcrossScenes:false)`, sin Preview (test l.72). Entonces `previewOrbit.IsBound == false` y se activa `PreviewUnavailable` (`AlfaUiController.cs:1117-1120`).
  - Además, ese `SetActive` se evalúa una sola vez al construir la UI. Si luego se llama a `orbit.Bind(...)`, el texto no se oculta: es un bug latente.
  - En el juego real (`LetMeSleepHiggsfield.unity:581-583`) el visor sí está enlazado.
- **"BODY-A".**
  - `EvidenceSnapshot()` crea `mosquito.base` con label "CUERPO" y una única opción construida por `Visual()`, cuya etiqueta es `optionId.ToUpperInvariant()` = "BODY-A" (test l.389).
  - No se pasa `thumbnailResolver` ni `modularPreviewAvailable`, así que no hay miniatura y el estado dice "La vista de estas piezas llegará cuando estén listas en el juego." (`AlfaUiController.cs:1921-1924`).
  - No hay sprites ni labels reales porque no existe catálogo de producción.

## 8. Lista exacta de categorías y opciones existentes hoy

**Producción** (único modo vivo, básico; `Preferences.cs:29-38`; defaults warm/blue/red):
- Humano — TONO DE PIEL:
  - light "Claro" (0.91, 0.70, 0.50)
  - warm "Cálido" (0.72, 0.40, 0.25)
  - tan "Bronce" (0.54, 0.29, 0.16)
  - dark "Oscuro" (0.27, 0.12, 0.07)
- Humano — COLOR DE PIJAMA:
  - blue "Azul" (0.12, 0.32, 0.51)
  - red "Rojo" (0.65, 0.17, 0.16)
  - green "Verde" (0.16, 0.40, 0.27)
  - purple "Violeta" (0.40, 0.22, 0.56)
  - yellow "Mostaza" (0.72, 0.54, 0.18)
- Mosquito — COLOR:
  - red "Rojo" (0.55, 0.14, 0.11)
  - blue "Azul" (0.17, 0.30, 0.52)
  - green "Oliva" (0.31, 0.36, 0.18)
  - purple "Violeta" (0.37, 0.20, 0.43)

Se aplican sobre los canales `Skin` (material Human_Skin), `Pajamas` (Human_Pajamas, que también tiñe el gorro HumanNightcap) y `Mosquito` (Mosquito_Shell y Mosquito_Abdomen) (`CharacterContentBuilder.cs:367-372`). La ropa es fija: pijama, pantuflas y gorro de noche (`AlfaUiController.cs:1145`).

**Modular de producción:** 0 slots y 0 opciones.

**Sólo existen catálogos sintéticos de test:**
- `lms.ui.synthetic`:
  - human.base "BASE": base-a "BASE-A"
  - human.hair "CABELLO" (Color): short "CORTO" #C87952, long "LARGO" #513624
  - mosquito.base "CUERPO": body-a "BODY-A", body-b "BODY-B"
  - human.hidden sin label, oculta
- `lms.ui.evidence` (el de la captura):
  - human.base
  - human.style_2 a human.style_10, "CATEGORÍA n CON NOMBRE MUY LARGO", con option-a/option-b
  - mosquito.base (wire 11) "CUERPO": body-a "BODY-A"
- `lms.v020.synthetic.persistence` (plantilla del mapeo legacy):
  - human.base: human-a
  - human.skin: skin-light, skin-warm, skin-tan, skin-dark
  - human.pajama: pajama-blue, pajama-red, pajama-green, pajama-purple, pajama-yellow
  - mosquito.base: mosquito-a
  - mosquito.color: mosquito-red, mosquito-blue, mosquito-green, mosquito-purple
- `lms.v020.synthetic.visual`:
  - human.base
  - human.hat: none, hat-a (SocketPart en "HatSocket")
  - human.tint: red
  - mosquito.base

**Anchors reales disponibles hoy para SocketPart:**
- Humano: CameraEye, AimChest, HandGrip_L, HandGrip_R, ToolSocket_R, Foot_L, Foot_R.
- Mosquito: CameraTarget, AimForward, ProboscisTip, WingRoot_L, WingRoot_R, GroundContact.

No hay anchor de cabeza/sombrero, cara, espalda, tórax ni cola.

### Archivos clave

- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Core/Customization/AppearanceSelection.cs` — Modelo serializable sin Unity: CustomizationRole{Human=0,Mosquito=1}, AppearanceSlotSelection{SlotId,OptionId}, AppearanceLoadout{Selections[]} con OptionFor/SetOption, AppearanceSelection{Schema=1,Human,Mosquito} con For(role), Copy y CanonicalEquals.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Core/Customization/CustomizationCatalogSnapshot.cs` — Contrato y validación inmutable. CustomizationOptionKind{None,Color,SkinnedPart,SocketPart,Composite}, registros de slot y opción, regex de ids (l.105-108), MaximumSelectedSlots=24 (l.104), TryCreate (l.131-237), TryEncode/TryDecode wire (l.242-256), TryNormalize/DefaultSelection (l.258-317) y fingerprint SHA-256 (l.319-337), que no incluye Label ni Thumbnail.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterCustomizationCatalog.cs` — ScriptableObject (menú 'Let Me Sleep/Characters/Customization Catalog'): CatalogId, Revision, Slots[SlotDefinition], Options[OptionDefinition] con RuntimeAsset (GameObject), AssetId (GUID) y Thumbnail (Sprite). TryCreateSnapshot (l.52-103) y TryGetAssets(slot, option) → prefab + miniatura (l.105-111).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterCustomizationHost.cs` — Contrato del host por prefab de rol: Role, View, RigId, ExpectedVisualScale, PartsRoot, OwnedBaseRenderers y ColorChannels.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterCustomizationPart.cs` — Metadata en la raíz de cada prefab de pieza: Role, SlotId, Kind, TargetRigId, TargetVisualScale, FacialImpact, SkinnedRenderers{Renderer,RootBonePath,BonePaths[]}, SocketParts{PartRoot,AnchorName}, ColorChannels{Renderer,MaterialIndex,ColorSlotId} y FirstPersonHeadRenderers.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterModularVisualAssembler.cs` — Ensamblador con validación, staging y commit atómico con rollback. TryBuildPlan (l.116-165), TryValidateHost (l.167-191), TryValidatePart (l.193-275), TryValidateColorBindings (l.277-290), Prepare, que clona y remapea huesos por path y reparenta sockets (l.316-376), y Commit, que oculta OwnedBaseRenderers y aplica colores con MaterialPropertyBlock (l.378-424).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterView.cs` — Anchors (GetAnchor, RefreshAnchors en LateUpdate), Colors por categoría con _BaseColor por MPB (l.153-164), HeadRenderers para primera persona y SetCustomizationBindings/ClearCustomizationBindings, que el ensamblador usa para añadir cabezas y canales.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/ModularCustomizationRuntimeProvider.cs` — Gate de activación: exige Catalog RuntimeReady, 13 LegacyMappings válidos y hosts y ensamblador en HumanPrefab y MosquitoPrefab que pasen CanApply con los defaults (l.15-42). Expone ModularCustomizationRuntime (TryMigrate, CanApply, TryApply).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.ModularCustomization.cs` — Estado publicado, borrador y preview modular. Migración V1→V2 (l.86-115), escritura V2 (l.117-149), TryCreateModularUiState con thumbnailResolver (l.151-162), PreviewModularCustomization/SaveModularCustomization con rollback (l.164-239) y aplicación al clon del visor (l.254-269).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/ModularCustomizationPersistence.cs` — DTOs PreferencesV1/V2, PreferenceSchemaCodec (round-trip exacto de JSON, l.57-73) y LegacyAppearanceMapper, que exige exactamente los 13 ids legacy mapeados a opciones Color (l.128-185).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.Preferences.cs` — Paletas básicas vigentes (l.29-38), LoadPreferences con detección de schema y migración (l.39-133), SavePreferences (l.156-182), PresentPreferences, que elige UI modular o básica (l.183-192), y ApplyPreviewColors (Update): capa 30, colliders apagados y aplicación modular o básica al clon (l.291-310).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.Appearance.cs` — Canal 2 online: TryBuildAppearancePacket (v2 si modular, v1 si básico), TickAppearance con envío fiable cada 2 s, ReceiveAppearance y ApplyLive sobre personajes de menú, lobby y gameplay según host.Role (l.15-87).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AppearanceWireCodec.cs` — Formato v2: byte versión=2, código de sala (ushort len + ≤16 B), fingerprint u64, cantidad de slots y pares (byte WireSlotId, ushort WireOptionId) para TODOS los slots de ambos roles. Máximo 128 B; el decode exige count == Slots.Count y re-normaliza.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AppearancePeerState.cs` — Cache por par con rate limit de 0,25 s, validación de membresía, v1 legacy (3 textos) y v2 modular con fingerprint. Un v1 tardío no pisa un v2 aceptado.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/CharacterPreviewOrbit.cs` — Visor 3D: instancia el prefab de rol bajo PreviewStage y ubica la cámara. Yaw por arrastre, zoom por rueda, SetAngle Front/Side/Back (0/90/180°), ResetView y auto-framing por bounds horneados. No tiene pitch ni botones de zoom, y RecalculateFraming es privado.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/AlfaUiController.cs` — Pantalla PERSONALIZAR. BuildCustomization (l.1082-1165): panel de preview, texto 'El visor 3D…' fijado una sola vez (l.1117-1120), botones FRENTE/PERFIL/ESPALDA/CENTRAR y listas modulares. Lógica modular: VisibleModularSlots, BuildModularCustomization, AddModularOptionVisual, SetModularCustomizationOption, SendModularPreview y UpdateCustomizationView (l.1695-1937).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/AlfaUiContracts.cs` — CharacterPreviewSetup{Camera,Stage,Texture,HumanPrefab,MosquitoPrefab,OnPreviewCreated} con IsUsable (l.78-104), IModularCustomizationActions (l.130-134) y CustomizationUiState en modo básico y modular (l.389-462).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Editor/Characters/CharacterContentBuilder.cs` — Construye los prefabs de rol: anchors por hueso Socket.* (l.33-42), VisualRoot con escala 1 (humano) o 0,5 (mosquito) (l.336) y canales de color por nombre de material (l.359-375). Es el lugar natural para agregar host, PartsRoot y nuevos anchors.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Editor/Characters/CharacterCustomizationCatalogValidator.cs` — Inspector y validador de editor: además del contrato Core, exige AssetId == GUID del RuntimeAsset.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/AlfaBootstrapBuilder.cs` — Crea CustomizationStage (500,0,0), CustomizationCamera (capa 30, FOV 35, fondo .045/.08/.13), Customization.renderTexture 1024² y la luz CustomizationKey (l.35-48). No crea proveedor modular.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/CharacterRenderReview.cs` — Patrón reutilizable para generar miniaturas offline: escena aditiva, cámara y RenderTexture → PNG (Render, l.101-115).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Scenes/LetMeSleepHiggsfield.unity` — Única escena de build (EditorBuildSettings). El componente AlfaApplication (l.555-590) tiene Preview* asignados pero NO serializa ModularCustomizationProvider, así que el valor es null.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Tests/PlayMode/ModularCustomizationUiPlayModeTests.cs` — Origen de la captura: UI sin preview (l.72), EvidenceSnapshot (l.323), label BODY-A (l.389) y ruta de evidencia fija en V020 (l.211).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Tests/PlayMode/ModularCustomizationPersistenceIntegrationTests.cs` — Fixture de producción simulada: host y prefab, base part, catálogo con human.skin/human.pajama/mosquito.color y 13 LegacyMappings (l.219-345). Sirve de plantilla para el catálogo real y muestra cómo enlazar el visor con orbit.Bind (l.82).
- `N:/LetMeSleep/Validation/V020/ModularCustomizationUiNative02/modular-customization-1920x1080.png` — La captura en cuestión: catálogo sintético, sin visor ni arte; el recibo modular-customization-ui-layout.txt lo declara.
- `N:/LetMeSleep/Validation/V020/ModularVisualIntegrationContract-01.md` — Contrato de diseño del ensamblador: no reemplazar root, Animator ni anchors; formatos admisibles; orden de aplicación; riesgos de primera persona y facial.
- `N:/LetMeSleep/Validation/V020/ModularCustomizationImplementationPlan-01.md` — Plan original con los slots previstos (human.* y mosquito.*) y la partición CUST-FND, UI, NET y ART.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/Higgsfield/BOCETOS-INVENTARIO.csv` — PER-04..PER-08 (filas 12-16): láminas de personalización humana y mosquito básica y ampliada, y pantalla dual.

### Puntos de extensión

- **(a) Visor 3D — etiqueta.** Guardar el TMP 'PreviewUnavailable' en un campo (`previewUnavailableLabel`) de `AlfaUiController.BuildCustomization` (l.1117-1120). Actualizar su visibilidad (`SetActive(!previewOrbit.IsBound)`) en `UpdateCustomizationView` (l.1892) y exponer un `RebindPreview(CharacterPreviewSetup)` que llame a `previewOrbit.Bind` y refresque el label.
- **(a) Visor 3D — re-encuadre.** Agregar en `CharacterPreviewOrbit` un `public void Reframe()` que llame a `RecalculateFraming(); ApplyOrbit();` (hoy `RecalculateFraming` es privado, l.153). Llamarlo tras cada `TryApplyModularPreview` exitoso en `AlfaApplication.ApplyPreviewColors` (Preferences.cs:299-300) y en `TryPrepareModularChange` (ModularCustomization.cs:251). Hoy el framing se calcula en `Show()` antes de ensamblar, así que gorros y alas nuevos pueden quedar cortados.
- **(a) Visor 3D — zoom.** Agregar `ZoomIn()`/`ZoomOut()` públicos en `CharacterPreviewOrbit` (p. ej. `zoomFactor = Clamp(zoomFactor*0.85f|1.15f, minDistance/fitDistance, 2.2f); ApplyOrbit();`) y botones '+'/'−' en la fila `PreviewAngles` (AlfaUiController.cs:1121-1126), con el mismo estilo `QuietButton`.
- **(a) Visor 3D — pitch opcional.** Campo `pitch` limitado a −10..35. En `OnDrag` usar el delta en y. En `ApplyOrbit` (l.140-151) posicionar la cámara en `focusWorld + Quaternion.Euler(pitch,0,0)*Vector3.forward*distance`. `SetAngle`/`ResetView` deben poner `pitch=0` para conservar FRENTE/PERFIL/ESPALDA exactos (lo exige el comentario de l.146-147).
- **(a) Visor 3D — escenografía de PER-08.** Pedestal de madera: malla en capa 30, hija de `CustomizationStage`; gira con el yaw porque el stage rota. Fondo de bosque/lago: quad o skybox fuera del stage, o bien cambiar `PreviewCamera.backgroundColor`/clearFlags en `AlfaBootstrapBuilder.cs:35-48`, donde también se ajusta la luz `CustomizationKey`. Todo debe crearse por herramienta de editor, no a mano en la escena.
- **(a) Visor 3D — evidencia real.** Crear un test o captura que construya la UI con `CharacterPreviewSetup` real, cargando `LetMeSleepHiggsfield` como `ModularCustomizationPersistenceIntegrationTests.SetUp` (l.55-86, `orbit.Bind(...)` en l.82). Cambiar la raíz de evidencia fija 'N:/LetMeSleep/Validation/V020' (ModularCustomizationUiPlayModeTests.cs:211) a V030.
- **(b) Activar modular en producción — catálogo.** Crear el asset `CharacterCustomizationCatalog` (p. ej. `Assets/LetMeSleep/Content/Characters/Customization/LMS_CustomizationCatalog.asset`, CatalogId 'lms.v030.characters', Revision 1). Contenido mínimo: human.base y mosquito.base (IsBaseSlot, Required, default SkinnedPart con RuntimeAsset y AssetId = GUID) y slots Color para piel (≥ light/warm/tan/dark), pijama (≥ blue/red/green/purple/yellow) y mosquito (≥ red/blue/green/purple).
- **(b) Activar modular en producción — hosts.** Extender `CharacterContentBuilder.BuildCharacter` (l.325-384) para que añada `CharacterCustomizationHost` y `CharacterModularVisualAssembler` a LMS_Human, LMS_Human_FirstPerson, LMS_HumanMenu y LMS_Mosquito (o a los prefabs Higgsfield nuevos). Datos del host: `PartsRoot = VisualRoot/CustomizationParts` (fuera del Animator), `RigId` (p. ej. 'lms.human.v030' / 'lms.mosquito.v030'), `ExpectedVisualScale` = escala de VisualRoot, `OwnedBaseRenderers` = todos los renderers autorales y `ColorChannels` si el host conserva mallas coloreables.
- **(b) Activar modular en producción — escena.** Instalar por editor (extender `AlfaBootstrapBuilder` o `HiggsfieldBootstrapSceneInstaller`) un `ModularCustomizationRuntimeProvider` en el GameObject 'Let me sleep' de `LetMeSleepHiggsfield.unity`. Asignar `Catalog` y 13 `LegacyMappings{Kind, LegacyId, SlotId, OptionId}`, y asignarlo a `AlfaApplication.ModularCustomizationProvider`.
- **(b) Herramienta de piezas nueva** (p. ej. `Content/Editor/Characters/CharacterCustomizationPartBuilder.cs`). Desde cada FBX exportado con el mismo esqueleto: calcular `BonePaths` y `RootBonePath` relativos a `view.Animator.transform` a partir de `SkinnedMeshRenderer.bones`, quitar Animator y otros componentes prohibidos, dejar la raíz en identidad, poblar `CharacterCustomizationPart` y guardar en `.../Customization/Parts/<rol>/<slot>/<option>.prefab`. Luego agregar la `OptionDefinition` con AssetId = GUID y validar con `CharacterCustomizationCatalogValidator.Validate`.
- **(b) Añadir categoría.** Nueva `SlotDefinition` con WireSlotId nuevo (nunca reutilizado) y Label en español, más sus opciones. Si AllowsNone, agregar la opción `none` (Kind None, WireOptionId 0). La UI la muestra sin código gracias a `VisibleModularSlots` y `BuildModularCustomization`. Para retirar una opción, dejar su Label vacío (queda oculta pero cargable); no borrarla.
- **(b) Nuevos anchors para SocketPart.** Agregar a `CharacterContentBuilder.HumanAnchors`/`MosquitoAnchors` (l.33-42) y exportar los huesos Socket.* en Blender. Humano: p. ej. 'HeadTop'←Socket.HeadTop (gorros, capucha, gorro de noche), 'FaceFront'←Socket.Face (lentes), 'Back'←Socket.Back (mochila). Mosquito: 'HeadTop'/'Thorax'←Socket.Thorax (armadura, accesorios), 'AbdomenTip'←Socket.AbdomenTip (aguijón), 'MouthRoot' (base de probóscide; ProboscisTip es la punta). Las alas pueden usar WingRoot_L/R con dos SocketBinding en un mismo prefab.
- **(b) Colores sin consumidor.** Cambiar `CharacterModularVisualAssembler.TryValidateColorBindings` (l.285-287) para permitir un slot Color seleccionado sin canal cuando su pieza consumidora es `none` (p. ej. hair_color con 'calvo', top_color sin camiseta). Hoy ese caso rompe la aplicación completa.
- **(b) Incompatibilidades en UI.** Resolverlas en `AlfaUiController.SetModularCustomizationOption` (l.1814-1830): antes de `TryNormalize`, poner en 'none' (o default) los slots listados en `IncompatibleSlotIds` de la nueva opción y los que la declaran incompatible (p. ej. capucha vs gorro/pelo). Hoy sólo muestra un error.
- **(b) Límite de 24 slots.** Si hacen falta más, subir `CustomizationCatalogSnapshot.MaximumSelectedSlots` (l.104, usado en l.145/149, en `AppearanceWireCodec` l.32/74 y en `[Range]` de `CharacterCustomizationCatalog.SlotDefinition.WireSlotId`, l.19) a 32. El paquete queda en 1+2+16+8+1+3×32 = 124 B, bajo el máximo de 128.
- **(b) Aplicación inmediata en gameplay y lobby.** Exponer un evento en `GameplayVisualPresenter.EnsureVisual` (l.148-201) y en `LobbyVisualPresenter` para que `AlfaApplication` llame a `ApplyLive(view, owner)` antes de `VisualAttentionFactory.TryInstall`/`BindTool`/`BindLocalCamera`. Hoy se aplica hasta 2 s después, vía `TickAppearance` (Appearance.cs:26-35).
- **(b) Textos de rol.** Hacer dinámicos o actualizar los subtítulos fijos de las pestañas: 'PIJAMA Y GORRO' / 'COLOR DEL CUERPO' (AlfaUiController.cs:1133-1136).
- **(c) Miniaturas — datos.** El campo `OptionDefinition.Thumbnail` (Sprite) ya existe y no afecta al fingerprint. El resolver ya está cableado (ModularCustomization.cs:159-160). Crear la herramienta de editor `CustomizationThumbnailBaker`, basada en el patrón de `CharacterRenderReview.Capture/Render` (l.20-115):
  1. escena aditiva, luz cálida y cámara con fondo alfa 0 (RenderTextureFormat.ARGB32);
  2. por opción: instanciar el prefab host, aplicar `DefaultSelection()` + esa opción con `assembler.TryApply`, encuadrar por slot (cabeza para pelo, gorro, lentes y ojos; torso para ropa; cuerpo entero para bases; ala o probóscide para mosquito);
  3. escribir PNG de 256² en `.../Customization/Thumbnails/<slot>/<option>.png`, importar como Sprite (TextureImporterType.Sprite, alphaIsTransparency) y asignar `Thumbnail` en el catálogo.
- **(c) Miniaturas — UI.** Pasar de lista a grilla en `BuildModularCustomization`/`AddModularOptionVisual` (l.1710-1775): contenedor con `GridLayoutGroup` (celda ~112×128, miniatura 96² y label corto debajo), borde seleccionado Lamp400 y swatches circulares para opciones Color. Categorías en columna vertical con icono, como PER-08: añadir un resolver `Func<string,Sprite>` de iconos por slot a `CustomizationUiState` o un campo `Icon` en `SlotDefinition`, fuera del fingerprint. Conservar o actualizar los nombres de nodo usados por los tests ('ModularCategory_n', 'ModularOption_n_m', 'ColorSwatch', 'Thumbnail').

### Contratos y restricciones

- SlotId `^[a-z][a-z0-9_.]{0,31}$`, OptionId `^[a-z][a-z0-9_.-]{0,47}$`, CatalogId `^[a-z][a-z0-9_.-]{0,31}$` y tags `^[a-z][a-z0-9_.-]{0,31}$`, todos ≤64 bytes UTF-8 (CustomizationCatalogSnapshot.cs:105-108, 339).
- WireSlotId 1..24, único en todo el catálogo (ambos roles). WireOptionId 1..65534 único por slot; 0 sólo para `none` y 65535 reservado. No renumerar ni reutilizar códigos: cambian el fingerprint y la decodificación.
- Máximo 24 slots entre los dos roles. El paquete v2 incluye todos los slots y el receptor exige `count == snapshot.Slots.Count` (AppearanceWireCodec.cs:74). Dos builds con catálogos distintos se ignoran entre sí y el par cae a la selección por defecto (Appearance.cs:66-73).
- El fingerprint incluye Revision, ids, códigos, Kind, swatch RGBA, AssetId, HasRuntimeAsset y compatibilidades. Cambiar el color de una muestra o reimportar un prefab con otro GUID cambia la compatibilidad de red. Label y Thumbnail no la afectan.
- Borrar o renombrar SlotId/OptionId hace fallar `TryNormalize` en el preferences.json guardado: la personalización modular queda no disponible y se bloquean las escrituras (Preferences.cs:85-91). Para retirar algo, dejar el Label vacío.
- `LegacyAppearanceMapper` exige exactamente 13 mapeos a opciones Color: 4 de piel, 5 de pijama y 4 de mosquito; piel y pijama en slots distintos. Sin ellos `TryResolve` falla y el juego sigue en modo básico (ModularCustomizationPersistence.cs:144-185).
- La activación del runtime migra automáticamente cualquier preferences.json V1 (incluida una instalación nueva) a V2 en la primera carga (Preferences.cs:121). `TryReadV2` exige round-trip JSON exacto, así que agregar campos (p. ej. presets) requiere schema 3.
- Una opción Color sólo tiñe `_BaseColor` por MPB (CharacterView.cs:52, 153-164). Los patrones y texturas (alas moteadas, rayas, marcas) no se pueden expresar como Color: necesitan una pieza con su propio material o un Kind nuevo en Core, que cambiaría el contrato.
- Las piezas deben tener raíz en identidad, sólo componentes Transform, Renderer, MeshFilter y CharacterCustomizationPart (sin Animator, Collider ni scripts), y todos sus renderers declarados (CharacterModularVisualAssembler.cs:202-263).
- SkinnedPart: `BonePaths.Length == mesh.bindposes.Length` y todos los paths únicos bajo `view.Animator.transform`. El esqueleto de la pieza debe coincidir en nombres con el rig del host; no se admite remapear entre el rig alfa y un rig nuevo.
- `FacialImpact.ReplacesTrackedFace` se rechaza siempre (l.206-207). Cambiar ojos, expresiones, párpados o cabeza que alteren el rig facial de `VisualAttentionFactory` requiere una certificación facial nueva. Además, una base que oculte la cabeza autoral también desactiva las blendshapes de parpadeo ligadas a ella.
- Exactamente un slot base por rol, Required, con opciones SkinnedPart o Composite. El ensamblador siempre oculta `OwnedBaseRenderers` y usa la base del catálogo: el cuerpo tiene que existir como pieza separada.
- No reemplazar root, Animator, anchors, HitVolume ni escala de VisualRoot (ModularVisualIntegrationContract-01.md). Humano: VisualRoot 1 y CameraEye a 1,53 m. Mosquito: VisualRoot 0,5.
- Primera persona: toda pieza visible en la cabeza debe figurar en `FirstPersonHeadRenderers`, o se verá dentro de la cámara (LMS_Human_FirstPerson también necesita host).
- Reglas de AGENTS.md: no usar el nombre 'Bite & Build' (aparece en PER-08), ni armas (escopeta de PER-04), crafting ni clases. El humano por defecto usa pijama, pantuflas y gorro de noche. El arte nuevo se crea desde cero a partir de los bocetos, sin reciclar el modelo alfa. Los ítems de mano de PER-04/06 chocan con `ToolSocket_R` y las herramientas de gameplay (matamoscas, pantufla, raqueta, aerosol), así que conviene excluirlos o dejarlos sólo para el menú.
- Asignación de capas del visor: `ApplyPreviewColors` pone la capa 30 sólo una vez por instancia, y el ensamblador hereda la capa del root, así que las piezas quedan en la capa 30. Cualquier pedestal o fondo del visor debe estar en la capa 30 y la cámara del menú excluye esa capa (AlfaBootstrapBuilder.cs:32,37).
- Escenas, prefabs y settings se modifican sólo con herramientas de editor de Unity y cada asset nuevo necesita su .meta. El prefab y la escena son de un único dueño a la vez (AGENTS.md).

### Brechas frente a bocetos

- PER-04/PER-06, humano. Pide pelo (short, messy, side part, curly, long, ponytail, buzz, mullet, spiky, fringe, bald, man bun), barba (none, stubble, mustache, goatee, short/full beard), gorros y cascos (cap, beanie, helmet, hard hat, boonie, cowboy, headphones, trapper, bucket, straw, party), lentes (round, square, sunglasses, goggles, visor), camisetas y tops, chaquetas y chalecos, overoles, pantalones y shorts, calzado, guantes, mochilas, tonos de piel, expresiones faciales y paletas (camiseta, pantalón, chaqueta, accesorio). Hoy sólo hay piel ×4 y pijama ×5 como color, sin piezas.
- PER-05/PER-07, mosquito. Pide forma de cuerpo (slim, standard, round, hunched, long, bulky), forma de alas (classic, long, short, rounded, angled, lance, split, feathered), patrón de alas (clear, tipped, striped, veined, speckled…), ojos (normal, wide, small, squint, lazy, bugged, angry, sleepy) y cejas, probóscide (standard, short, long, curved, needle, barbed, split, trumpet), aguijón, patas (default, thick, thin, spiked, bended…), tórax, abdomen, marcas y rayas, armadura o caparazón, accesorios y paletas (natural, forest, desert, urban, fantasy, toxic, blood, ice). Hoy sólo hay mosquito ×4 como color.
- Propuesta que cabe en 24 slots (sin tocar el límite), en orden de WireSlotId.
  Humano (14):
  1. human.base 'CUERPO' (puede ir sin label si hay una sola base)
  2. human.skin 'TONO DE PIEL' (Color, destino legacy)
  3. human.hair 'PELO' (AllowsNone)
  4. human.hair_color 'COLOR DE PELO' (Color)
  5. human.facial_hair 'BARBA' (AllowsNone)
  6. human.headwear 'GORROS' (SocketPart en HeadTop; default gorro de noche; gorra roja)
  7. human.glasses 'LENTES' (AllowsNone)
  8. human.top 'CAMISETA'
  9. human.top_color (Color)
  10. human.bottom 'PANTALÓN' (default pijama)
  11. human.pajama 'COLOR DE PIJAMA' (Color, destino legacy)
  12. human.footwear 'CALZADO' (default pantuflas)
  13. human.gloves 'GUANTES'
  14. human.back 'MOCHILA'
  Mosquito (10):
  15. mosquito.base 'CUERPO'
  16. mosquito.color 'COLOR' (Color, destino legacy)
  17. mosquito.wings 'ALAS'
  18. mosquito.wing_color (Color)
  19. mosquito.eyes 'OJOS' (bloqueado por el gate facial)
  20. mosquito.proboscis 'PROBÓSCIDE'
  21. mosquito.legs 'PATAS'
  22. mosquito.markings 'MARCAS'
  23. mosquito.accent_color (Color)
  24. mosquito.accessory 'ACCESORIOS'
  Aguijón, armadura, cejas, chaquetas y expresiones exigen subir el límite a 32 o fusionar slots.
- PER-08, pantalla dual. Tiene pestañas de rol grandes con retrato, columna vertical de categorías con iconos, grilla de miniaturas y paleta de colores en rejilla, preview 3D sobre pedestal con fondo de bosque/lago, flechas 'DRAG TO ROTATE', presets (guardar/cargar), botón ALEATORIO y CTA verde 'LISTO'. Hoy hay dos paneles planos, listas de texto con '> ' como marca de selección, sin iconos de categoría, sin grilla, sin presets, sin aleatorio y con APLICAR como CTA; el visor tiene fondo sólido y no tiene pedestal.
- Miniaturas: el contrato las soporta (`Thumbnail`), pero no existe ningún sprite ni horneador. La UI actual las pinta a 36×36 dentro de un botón de texto, lejos de la grilla de los bocetos.
- Visor: no tiene zoom por botones, pitch, re-encuadre tras cambiar piezas, animación de pose de menú ni iluminación cálida de interior. La etiqueta de 'no disponible' no se refresca tras un Bind posterior.
- Anchors: faltan sockets de cabeza, cara, espalda y muñeca (humano) y de cabeza, tórax, base de boca y cola (mosquito). No se pueden montar gorros, lentes, mochila, aguijón ni armadura como SocketPart.
- Expresiones y ojos (PER-04/06 'eye/facial expressions'; PER-05/07 'eye expressions/shapes/eyebrows') están bloqueados por el gate facial del ensamblador. Hace falta un Kind o contrato nuevo (p. ej. preset de blendshapes o textura de ojos) con certificación facial, o limitarse a emotes de animación no persistidos.
- Patrones y texturas (alas moteadas, marcas, camisa a cuadros): el Kind Color sólo tiñe. Hay que modelarlos como piezas con material propio, lo que multiplica opciones, o añadir un Kind de material o textura en Core.

### Riesgos

- Activar el catálogo migra en la primera carga los preferences.json de todos los jugadores de V1 a V2, sin vuelta atrás. Una build anterior sin runtime deja el archivo en sólo lectura (Preferences.cs:60-68, 121). Hay que probar la migración con los 13 colores reales y con una instalación limpia.
- Cualquier cambio estructural del catálogo (Revision, códigos, swatch, GUID de prefab) cambia el fingerprint. Jugadores con builds o catálogos distintos se verían entre sí con la apariencia por defecto, sin error visible.
- El límite de 24 slots se queda corto frente al alcance de PER-06/07. Subirlo toca Core, el codec y el Range del inspector, y exige re-ejecutar los tests de codec (≤128 B).
- Regla de colores: un slot Color seleccionado sin ningún renderer consumidor hace fallar TODO el ensamblado (Assembler l.285-287). Con piezas opcionales (pelo 'none', sin camiseta) el personaje queda sin aplicar y se ve el anterior o el cuerpo autoral.
- Gate facial: ojos y expresiones intercambiables están prohibidos (l.206-207). Además, una base nueva oculta la cabeza autoral cuyas blendshapes o huesos usa `VisualAttentionFactory` (`TryInstall`/`TryInstallPreview`). El parpadeo y la mirada pueden romperse en menú, visor y gameplay si la base no replica ese contrato facial.
- Primera persona: gorros, capuchas, lentes o pelo no declarados en `FirstPersonHeadRenderers` taparían la cámara del humano local (LMS_Human_FirstPerson también necesita host).
- Orden de aplicación en gameplay y lobby: `ApplyLive` corre hasta 2 s después de `EnsureVisual`, cuando la atención facial y las herramientas ya están ligadas a la base autoral. Puede haber un parpadeo visual o atención ligada a renderers ocultos.
- Los FBX importados traen Animator y otros componentes prohibidos. Sin una herramienta que genere prefabs de pieza limpios y calcule `BonePaths`, poblar el catálogo a mano es propenso a errores y cada error invalida el gate completo (`TryResolve` falla y se vuelve al modo básico sin avisar al jugador más allá de un Debug.LogWarning).
- El arte nuevo (Higgsfield/Humanos) debe crearse desde cero con rig y sockets nuevos. Los anchors y huesos actuales (Socket.*) y el RigId deben definirse con el modelador antes de producir piezas, o habrá que rehacer los BonePaths.
- La UI actual muestra error en vez de resolver conflictos entre slots incompatibles, así que el jugador puede quedar 'atascado' sin entender por qué una opción no se aplica.
- Los tests existentes dependen de nombres de nodo de UI ('ModularCategory_n', 'ModularOption_n_m', 'ColorSwatch', 'Thumbnail', 'PreviewUnavailable'). Rediseñar a grilla o pestañas obliga a actualizarlos sin relajar sus aserciones.
- El texto de evidencia y la ruta de validación están fijos en V020 (test l.211). Las capturas nuevas de V030 fallarán la aserción si no se actualiza.

### Verificación

Revisión estática en modo solo lectura del worktree N:/LetMeSleep/Worktrees/v020-candidate-20260920 (HEAD c178bba, rama claude/v0.3.0). No ejecuté Unity, Blender, builds ni exes, y usé git sólo para lectura.

**Código leído completo:**
- Core/Customization/AppearanceSelection.cs y CustomizationCatalogSnapshot.cs
- Content/Characters/Runtime/CharacterCustomizationCatalog.cs, CharacterCustomizationHost.cs, CharacterCustomizationPart.cs, CharacterModularVisualAssembler.cs y CharacterView.cs
- Bootstrap/AlfaApplication.ModularCustomization.cs, .Preferences.cs y .Appearance.cs; ModularCustomizationPersistence.cs, ModularCustomizationRuntimeProvider.cs, AppearanceWireCodec.cs y AppearancePeerState.cs
- UI/Runtime/CharacterPreviewOrbit.cs y AlfaUiContracts.cs

**Código leído por secciones:**
- AlfaUiController.cs: 540-595, 1082-1165 y 1661-2015
- CharacterContentBuilder.cs, AlfaBootstrapBuilder.cs, CharacterRenderReview.cs y GameplayVisualPresenter.cs

**Comprobaciones de ausencia:**
- Busqué el GUID de cada script (CharacterCustomizationCatalog d7c145a4…, Host bb45704d…, Part 2463d062…, Assembler 927feed8…, Provider 771cc7ec…) en todos los .asset, .prefab y .unity de Assets: 0 resultados.
- `ModularCustomizationProvider` no aparece en LetMeSleepBoot.unity ni en LetMeSleepHiggsfield.unity (bloque de AlfaApplication, l.555-590), mientras que PreviewCamera, PreviewStage y PreviewTexture sí están asignados.
- EditorBuildSettings contiene sólo LetMeSleepHiggsfield.unity.
- Anchors y colores de los prefabs LMS_Human y LMS_Mosquito leídos del YAML.

**Captura:** abrí N:/LetMeSleep/Validation/V020/ModularCustomizationUiNative02/modular-customization-1920x1080.png y su recibo modular-customization-ui-layout.txt, y cotejé el texto con ModularCustomizationUiPlayModeTests.cs (l.72, 211-243, 323-389).

**Bocetos:** vi los PER-04..PER-08 en C:/Users/brank/Desktop/bocetos/personajes (rutas tomadas de Higgsfield/BOCETOS-INVENTARIO.csv).

**Contexto:** contrasté con Validation/V020/ModularCustomizationImplementationPlan-01.md, ModularVisualIntegrationContract-01.md, ModularUiPersistenceContract-01.md y docs/ceo/STATE.md (secciones del gate modular).

**Para validar los cambios (cuando haya turno de Unity asignado):** usar el mismo patrón que `N:/LetMeSleep/Validation/V020/run-modular-integration-play-02.ps1`, es decir `N:/Unity/Editors/6000.3.24f1/Editor/Unity.exe -batchmode -projectPath <worktree>/unity -runTests -testPlatform PlayMode -testFilter "LetMeSleep.Tests.PlayMode.CharacterCustomizationCatalogPlayModeTests;LetMeSleep.Tests.PlayMode.CharacterModularVisualAssemblerPlayModeTests;LetMeSleep.Tests.PlayMode.ModularCustomizationUiPlayModeTests;LetMeSleep.Tests.PlayMode.ModularPersistenceContractPlayModeTests;LetMeSleep.Tests.PlayMode.CustomizationPersistencePlayModeTests;LetMeSleep.Tests.PlayMode.ModularCustomizationPersistenceIntegrationTests" -testResults <V030>/x.xml -logFile <V030>/x.log -force-d3d11 --lms-validation-data <V030>/data -modularCustomizationUiEvidence <V030>/ModularCustomizationUi…`. Antes hay que cambiar la raíz de evidencia fija en V020 (test l.211) y añadir tests para el catálogo de producción:
- `ModularCustomizationRuntimeProvider.TryResolve` con los prefabs reales;
- `CharacterCustomizationCatalogValidator.Validate` en verde;
- captura con el visor enlazado y miniaturas reales.

## characters

Las mallas actuales del humano y del mosquito son geometría procedural escrita en Python de Blender (bpy): tubos, elipsoides y tiras en art_source/unity/characters/build_characters.py y author_*.py. Cada una se exporta como .blend + FBX con armature (LMS_HumanRig: 65 huesos, 9.652 triángulos, 5 renderers; LMS_MosquitoRig: 39 huesos, 3.588 triángulos, 3 renderers) y un audit.json. En Unity, el builder de editor CharacterContentBuilder copia esos FBX a Assets/LetMeSleep/Content/Characters/Models, los importa como Generic y crea materiales URP Lit de color plano a partir de la paleta del audit, sin texturas. También genera los controllers (estados con ID estable) y los prefabs LMS_Human, LMS_Human_FirstPerson y LMS_Mosquito, con CharacterView, anclas PresentationAnchors y SourceOrientation a 180°. El prefab LMS_Flyswatter usa ToolView en lugar de CharacterView. Después, LivingMenuContentBuilder monta LMS_HumanMenu (4 clips sentados) y FacialContentBuilder certifica ojos y párpados (VisualAttentionContract).

El contrato real es por nombres: huesos, sockets, renderers, materiales y clips "Especie_Estado". Hay además cotas fijas: CameraEye a 1,53 m, punta de probóscide en +Z 0,095 m, pies a ±0,08 m o más, VisualRoot del mosquito a escala 0,5 y exactamente 2 renderers de cabeza. Los colliders reales, las superficies de mordida y todo lo online no dependen de la malla.

Camino más corto: modelar desde cero en Blender según PER-01/PER-03, pero con un armature que respete los mismos nombres y jerarquía (es un contrato técnico, no estético). Hay que exportar con los mismos parámetros FBX y audit.json a las mismas rutas y volver a correr los builders. No hay que tocar nada en runtime. La personalización por piezas ya existe en código (Host/Part/Assembler/Catalog), pero no está instalada en ningún prefab ni escena. El ragdoll del mosquito está atado a la geometría R4 y hoy sólo lo usa una prueba PlayMode.

### Cómo funciona

1) ORIGEN DE LA MALLA
- Humano, mosquito y matamoscas son geometría procedural escrita en bpy. build_characters.py usa las primitivas tube/ellipsoid/strip (L42-80) más author_human_geometry.py, author_human_joints.py, author_human_facial.py, author_mosquito_geometry.py y author_mosquito_face.py, y crea el armature en código (Character.bone).
- Cada especie produce art_source/unity/characters/<especie>/LMS_<Especie>_alpha.blend, LMS_<Especie>_alpha.fbx y audit.json.
- El humano del menú es un segundo FBX (menu/LMS_HumanMenu.fbx) generado por export_human_menu.py: mismo rig y malla, con 4 tomas sentadas (8/4/1,4/1,8 s) y un asiento a 0,575 m.
- No hay texturas. Cada material es un color plano (Principled Base Color y Roughness) y el sombreado es facetado (mesh_smooth_type='FACE').
- Sistema de ejes de la fuente: Blender en metros, Z arriba, frente -Y, lado .L en +X.
- El piloto Higgsfield (textura 2K, nombres tipo Mixamo) existe sólo en N:/LetMeSleep/Repository sin trackear y no está conectado.

2) IMPORTACIÓN EN UNITY (CharacterContentBuilder.BuildAll, L111-160)
- Lee audit.json de las 3 especies. Exige passed=true, species correcta y material_palette.
- ImportModel (L186-238) hace WriteIfChanged del FBX a Assets/LetMeSleep/Content/Characters/Models/LMS_<Especie>_alpha.fbx, así que se conserva el GUID.
- Ajustes del importer: globalScale 1, useFileScale, bakeAxisConversion, preserveHierarchy, optimizeGameObjects=false, isReadable, sin compresión, normales Import, blendshapes activados, Generic con CreateFromThisModel, motionNodeName="Root".
- Los clips se crean desde la toma cuyo nombre termina en el nombre del clip (L222), con loop según el audit y todos los locks/keepOriginal activados.
- UpsertMaterial (L240-278) crea Materials/<nombre>.mat con "Universal Render Pipeline/Lit" y sólo _BaseColor/_Smoothness. Mosquito_Wing se trata aparte: transparente, alpha 0,42, sin sombras. Luego AddRemap por nombre de material.
- BuildController (L297-323): parámetro int Motion, un estado por nombre sin transiciones AnyState, estado por defecto Idle.
- BuildCharacter (L325-386) monta esta jerarquía:
  LMS_<X> (CharacterView + HitVolume desactivado)
  ├ VisualRoot (escala 1 en el humano, 0,5 en el mosquito)
  │  └ SourceOrientation (yaw medido, hoy 180°)
  │     └ Model (Animator, applyRootMotion=false, AlwaysAnimate)
  │        └ LMS_HumanRig|LMS_MosquitoRig/Root/…
  └ PresentationAnchors
     ├ CameraEye, AimChest, HandGrip_L, HandGrip_R, ToolSocket_R, Foot_L, Foot_R (humano)
     └ CameraTarget, AimForward, ProboscisTip, WingRoot_L, WingRoot_R, GroundContact (mosquito)
- Las anclas son hermanas del rig y cada LateUpdate copian la pose de su hueso fuente con RotationOffset = inversa(rotación del hueso) × rotación de la raíz en reposo. Por eso, en reposo, están alineadas con el actor (+Z frente, +Y arriba).
- HeadRenderers = renderers llamados HumanHead o HumanNightcap.
- Colors: Human_Skin → "Skin", Human_Pajamas → "Pajamas", Mosquito_Shell y Mosquito_Abdomen → "Mosquito".
- Motions: Id = índice del array, StateName = "Base Layer.<Estado>", ClipName = "<Especie>_<Estado>".
- El prefab LMS_Human_FirstPerson es idéntico con firstPerson=true.
- El matamoscas (BuildTool L403-417) coloca Grip e Impact en Socket.Grip y Socket.Impact.

3) CERTIFICACIÓN FACIAL Y MENÚ
- AlfaBootstrapBuilder.Build (L23-26) llama a LivingMenuContentBuilder.BuildAndBind. Éste instancia LMS_Human, cambia el Model por LMS_HumanMenu.fbx conservando SourceOrientation, reasigna las anclas por las mismas rutas relativas al Animator y guarda LMS_HumanMenu.prefab.
- Después llama a FacialContentBuilder.BuildAll, que restaura la pose de bind y mide ojos y párpados sobre el FBX importado. Escribe VisualAttentionContract (Schema lms.visual-attention.v1, RigRevision "human-joints2-facial" / "mosquito-r4-facial", SourceSha256 del FBX, Rig=Bindings) en los 4 prefabs.
- En runtime, VisualAttentionFactory.TryInstall sólo añade VisualAttentionRig (orden 1200, último escritor de cabeza, ojos y párpados) si el contrato está certificado. Si no, registra LMS_FACIAL_SKIPPED.
- Reimportar cualquiera de los FBX dispara FacialContractImportGuard, que invalida los certificados.

4) RUNTIME DE JUEGO
- GameplayVisualPresenter.EnsureVisual (L148-201) instancia el prefab como hijo del GameplayActorProxy (pose local cero) y añade ActorVisualBinding. Si el clip set está completo, añade HumanLocomotionPresenter (un mixer de las 4 marchas por nombre, pies desde los huesos fuente de Foot_L/Foot_R). También instala la atención facial y monta las herramientas en ToolSocket_R alineando ToolView.Grip.
- ActorVisualBinding (orden 1100) interpola los snapshots y elige el ID de estado:
  - Humano: 0 Idle, 1 Walk, 2 Run (>2,45 m/s), 3 Crouch, 4 Jump, 9 Fall, 10 Faint, 11 Recover, 12 Swat.
  - Mosquito: 1 Hover, 2 Fly, 4 PerchEnter, 5 PerchIdle, 6 SurfaceWalk, 7 BiteStart, 8 BiteLoop, 9 Detach, 10 Hit, 11 Fall, 12 Recover.
  - Llama a CharacterView.PlayMotion y ajusta Animator.speed según la zancada (humano 1,2 m, mosquito 0,3 m, SurfaceWalk 0,1 m).
  - Durante un golpe resuelve IK analítica de 2 huesos sobre UpperArm/LowerArm/Hand.<L|R>, usando las longitudes reales de los huesos, para llevar Impact del matamoscas a StrikeVisualTrajectory.
  - En la mordida traslada el actor visual para que el ancla ProboscisTip toque el punto autoritativo de la GameplayBodySurface y orienta el +Z del mosquito hacia la normal.
- Los colliders vivos, las superficies de mordida y golpe, y los radios (humano cápsula de 0,25 × 1,72; mosquito esfera de 0,055, apoyo a 0,057 y rayo de mordida de 0,095 + reach) vienen de GameplayActorProxy y GameplayAuthority, nunca de la malla. El HitVolume del prefab está desactivado y sólo sirve de referencia.

5) PRIMERA PERSONA
- No hay malla de brazos separada. El actor local humano usa LMS_Human_FirstPerson: el mismo cuerpo completo, con HumanHead y HumanNightcap en ShadowsOnly.
- HumanViewCamera (orden 1300) llama a RefreshAnchors, coloca el pivote en CameraEye (hueso Socket.Eye, 1,53 m de alto y 0,17 m por delante del eje de la cabeza) y aplica yaw/pitch autoritativos, no la rotación del hueso. FOV 75, near 0,03.
- Los brazos que se ven son los del cuerpo, animados por clips, locomoción e IK de golpe. La herramienta cuelga de ToolSocket_R (Socket.Grip.R).
- El mosquito local usa MosquitoFollowCamera con CameraTarget y AimForward. Calcula los límites del cuerpo con los vértices pesados a Thorax/Head/Abdomen01/Abdomen02.

6) PERSONALIZACIÓN Y ONLINE
- En vivo sólo hay colores legacy (skin light/warm/tan/dark, pijama blue/red/green/purple/yellow, mosquito red/blue/green/purple). Se aplican por MaterialPropertyBlock (_BaseColor) a las categorías Skin, Pajamas y Mosquito.
- Se envían por P2P (canal 2, cada 2 s) y se aplican en menú, lobby, juego y vista previa (CharacterPreviewOrbit, capa 30, órbita arrastrable).
- El sistema modular por piezas (Catalog/Host/Part/Assembler, AppearanceWireCodec con fingerprint) está completo en código y tests, pero no hay catálogo, host ni provider en los prefabs y escenas.
- SpawnActor.CosmeticProfileId viaja por la red (OnlineGameplaySession.cs L279) pero nadie lo consume.
- Cambiar el modelo no cambia el protocolo online, salvo que cambie el fingerprint del catálogo modular.

7) CAMINO TÉCNICO MÁS CORTO (recomendado)
Reemplazo directo: la autoría es nueva, pero se respeta el contrato de nombres de rig y FBX. Así no se toca ningún runtime y las escenas conservan los GUID de prefabs y clips.

(a) Decisión de escala del mosquito.
- PER-03 lo dibuja de 0,5 a 0,8 m junto a un humano de 1,8 m. Gameplay fija un radio de 0,055, la punta a 0,095 m y el apoyo a 0,057 m.
- Para v0.3.0 recomiendo mantener la envolvente de juego (unos 0,22 m de envergadura en Unity) y exagerar cabeza y ojos dentro de ella.
- Agrandarlo implica cambiar constantes en GameplayAuthority, UnityGameplayWorld y GameplayActorProxy. Es trabajo del dueño de Gameplay.

(b) Humano en Blender (PER-01 Humano 01 + UI-06).
- Malla low-poly facetada con Shade Flat, del orden de 8 a 15 mil triángulos. Materiales planos con nombres de paleta; se recomienda prefijo por especie (Human_*, Character_*).
- Objetos que el export debe dejar como renderers:
  - HumanBody: cuerpo y ropa, sin cabeza.
  - HandSkin.L / HandSkin.R: opcionales.
  - HumanHead: cabeza, globos, pupilas y párpados, con 8 shape keys Blink25/50/75/Blink.{L,R}.
  - HumanNightcap: gorra o gorro. El nombre es obligatorio aunque sea una gorra roja.
- Armature "LMS_HumanRig" con los nombres y la jerarquía listados en contratos. Root en el origen y sin traslación; Socket.Eye a unos (0,-0,17,1,53); pies en x=±0,125.
- 17 acciones Human_<Estado> a 30 fps.
- Exportar con los parámetros de build_characters.py L220-225 y generar audit.json reutilizando el bloque export() (L172-218).
- Menú: exportar el mismo rig y malla con 4 acciones MenuSeatedIdle/MenuLook/MenuSwat/MenuReturn a menu/LMS_HumanMenu.fbx.

(c) Mosquito en Blender (PER-03 Mosquito 01 rojo, ojos enormes, 2 pares de alas facetadas, 6 patas largas, probóscide).
- Se autoriza al doble de la escala final (VisualRoot = 0,5).
- Armature "LMS_MosquitoRig" con los 39 nombres. Root en el origen, que es el centro de colisión.
- Socket.Mouth en (0,-0,19,0) de la fuente, con un vértice de la probóscide a menos de 4 mm en la fuente (menos de 2 mm en Unity). Socket.GroundContact en (0,0,-0,114).
- Renderers MosquitoSkin, MosquitoMembranes y MosquitoVeins; materiales Mosquito_*; 15 acciones Mosquito_<Estado>.

(d) Unity, en un turno de editor del Director.
1. CharacterContentBuilder.BuildAndVerifyIdempotence.
2. AlfaBootstrapBuilder.Build, o LivingMenuContentBuilder.BuildAndBind + FacialContentBuilder.BuildAll (en la escena v0.2, WindowsAlfaBuild.PrepareV020).
3. Actualizar las cadenas RigRevision en FacialContentBuilder L134.
4. Tests y capturas.
- Sólo hace falta tocar código si se cambia la carpeta fuente (SourceRoot L23-24), los nombres de material o categorías de color, o si se exponen nuevas anclas para accesorios.

Alternativa más cara: adaptar el runtime a otro esquema de huesos (p. ej. el piloto HF tipo Mixamo). Obliga a modificar CharacterContentBuilder (anclas y gates), ActorVisualBinding.FindArm, FacialContentBuilder, GameplayVisualPresenter (huesos del núcleo del mosquito), el ragdoll y la locomoción. No la recomiendo.

### Archivos clave

- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Editor/Characters/CharacterContentBuilder.cs` — Builder central (menú 'Let Me Sleep/Content/Build Characters[ and Check Idempotence]', L111/L162). SourceRoot=art_source/unity/characters (L23-24), estados (L25-32), mapa ancla→socket (L33-42), importador FBX (L186-238), materiales URP Lit de la paleta (L240-278), controller (L297-323), prefabs (L325-386) y gates de validación (L419-501, L586-624).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterView.cs` — Contrato de runtime: Animator, VisualRoot, HitVolume, HeadRenderers, Anchors (Name/Anchor/SourceBone/RotationOffset), Colors (Renderer/MaterialIndex/Category) y Motions (Id/StateName/ClipName/Loop). Métodos GetAnchor, RefreshAnchors (LateUpdate, orden 1000), PlayMotion y SetFirstPersonVisibility (ShadowsOnly en la cabeza); SetSkin/Pajama/MosquitoColor pone _BaseColor por MaterialPropertyBlock (L153-164).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/ToolView.cs` — Herramienta: ToolId, Grip, Impact, HeadRadius=0.085 y GripToImpact=0.365 (L10-13). Se monta alineando Grip con el ancla ToolSocket_R.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterModularVisualAssembler.cs` — Ensamblador modular: partes skinned por rutas de hueso, sockets en anclas y canales de color. Valida el host (L167-191) y la parte (L193-275, sólo se permiten Transform/Renderer/MeshFilter/CharacterCustomizationPart y la raíz debe tener transform identidad). Hoy no está instalado en ningún prefab.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterCustomizationHost.cs` — Metadatos del host modular: Role, View, RigId, ExpectedVisualScale, PartsRoot, OwnedBaseRenderers y ColorChannels. No está en ningún prefab.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterCustomizationPart.cs` — Metadatos de una pieza: SlotId, Kind, TargetRigId, TargetVisualScale, SkinnedRenderers (RootBonePath/BonePaths relativos al Animator), SocketParts (AnchorName), ColorChannels, FirstPersonHeadRenderers y FacialImpact.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterCustomizationCatalog.cs` — ScriptableObject de slots y opciones con WireSlotId/WireOptionId. No existe ningún .asset en el proyecto (sólo se usa en tests).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/FacialContentBuilder.cs` — Certifica la cara (VisualAttentionContract). Rutas FBX y prefabs fijas (L18-19). Busca los huesos Head/Neck/Eye.L/Eye.R (humano) y Head/Pupil.L/R/LidUpper/LidLower (mosquito) (L97-131). Exige pupila rígida al frente (L101-105), ojos con la misma orientación (L117-118) y Neck/Head alineados (L121-122). También exige 8 blendshapes en HumanHead (L123-125) y usa RigRevision fijo (L134).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/FacialContractImportGuard.cs` — Al reimportar cualquiera de los 3 FBX invalida los certificados faciales, que hay que regenerar con FacialContentBuilder.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/LivingMenuContentBuilder.cs` — Humano del menú: copia art_source/.../menu/LMS_HumanMenu.fbx (L25) con las tomas MenuSeatedIdle/MenuLook/MenuSwat/MenuReturn (L21). Reutiliza las rutas de ancla del prefab de juego (L89-108) y el clip Mosquito_Fly del FBX del mosquito (L71-72).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/AlfaBootstrapBuilder.cs` — Orden de ensamblado de la escena de arranque: asigna HumanPrefab/MosquitoPrefab y llama a LivingMenuContentBuilder.BuildAndBind y FacialContentBuilder.BuildAll (L23-26).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/WindowsAlfaBuild.cs` — PrepareV020 (L19) vuelve a ejecutar FacialContentBuilder.BuildAll (L31) antes del build de la escena LetMeSleepHiggsfield.unity.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/CharacterRenderReview.cs` — Captura de revisión visual de LMS_Human y LMS_Mosquito (requiere GPU). Salida por defecto: N:/LetMeSleep/Artifacts/review/alfa-characters.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Editor/Characters/HumanEyelidNormalsPostprocessor.cs` — AssetPostprocessor sobre Models/LMS_Human_alpha.fbx y LMS_HumanMenu.fbx (L30-45). Lanza excepción si falta HumanHead o alguno de los 8 blendshapes Blink (L85-86).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Gameplay/ActorVisualBinding.cs` — Enlace de juego: selección de estado por ID (L431-459) y eventos (L157-184). IK de brazo por nombres UpperArm/LowerArm/Hand.{L,R} (L556-628). Alinea ProboscisTip con la mordida (L461-487) y orienta al mosquito hacia la superficie (L643-690).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Gameplay/GameplayVisualPresenter.cs` — Instancia el prefab por rol (el local humano usa el prefab FirstPerson, L203-208) y monta herramientas en ToolSocket_R (L268-286). Enlaza la cámara a CameraEye (humano) o CameraTarget/AimForward más los huesos Thorax/Head/Abdomen01/Abdomen02 (mosquito) (L210-232).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Runtime/HumanViewCamera.cs` — Cámara en primera persona: sigue la posición de CameraEye y gira con yaw/pitch autoritativos, pitch de -110° a +75° (L9-10, L34-46). FOV 75 y near 0,03 según AlfaPresentationPreset.asset.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Gameplay/HumanLocomotionSetup.cs` — Marchas por nombre exacto de clip: Human_WalkSlow/Walk/Trot/Run, velocidades 1/1,55/3,1/5 y distancia por ciclo 0,8333/0,96875/1,55/2,1739 (L10-12). Pies tomados de las anclas Foot_L/Foot_R.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Physics/MosquitoRagdollBuilder.cs` — Ragdoll del mosquito R4: 18 cuerpos y 15 huesos auxiliares por nombre (L12-23). La geometría está codificada a mano (SourceFrame L40-61, LegPoints L371-382) y exige escala 0,5 exacta. Sólo lo usa Tests/PlayMode/MosquitoRagdollPlayModeProof.cs.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Editor/AlfaPresentationBuilder.cs` — Genera LMS_GameplayPresentation.prefab con referencias a los prefabs de personaje y herramientas por ruta (L468-534). Slipper, ElectricRacket y Aerosol no existen.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Gameplay.Unity/GameplayActorProxy.cs` — Colliders reales y superficies de mordida (independientes de la malla): cápsula de 0,25 × 1,72 y 10 cápsulas corporales (cabeza en 1,57 con r 0,13, torso en 1,19...) (L14-31); pose procedural de las extremidades (L60-90). El mosquito es una esfera de 0,055.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.Appearance.cs` — Apariencia online por P2P (canal 2, cada 2 s): IDs de color legacy o AppearanceSelection modular con NetworkFingerprint (L15-34). Se aplica a menú, lobby y juego (L45-86).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.Preferences.cs` — Opciones de color legacy: Skins, Pajamas y MosquitoColors (L29-39); ApplyAppearance (L311-317).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/ModularCustomizationRuntimeProvider.cs` — Resuelve el catálogo y los hosts modulares a partir de HumanPrefab/MosquitoPrefab (L15-42). No está en ninguna escena.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/art_source/unity/characters/build_characters.py` — Fuente procedural en bpy (Blender 5.2 LTS según la cabecera). Agrupa objetos en renderers por nombre (L145-160), genera audit.json (L172-218) y exporta el FBX con -Z forward, Y up, sin leaf bones y todas las acciones (L220-225). Rig humano: L229-262.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/art_source/unity/characters/human/audit.json` — Auditoría que lee el builder: species, passed, triangles=9652, mesh_count=5, bones=65, bone_names, bind_bones, 17 clips, material_palette (6), blend_shapes y facial_contract.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/art_source/unity/characters/mosquito/audit.json` — Auditoría del mosquito: 3588 triángulos, 3 renderers, 39 huesos, 15 clips y 7 materiales.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/art_source/unity/characters/integration.json` — Mapa ancla→socket, renderers visibles y ocultos en primera persona, escalas (humano 1, mosquito 0,5), punta (0,0,0,095), GroundContact (0,-0,057,0) y ajustes de material de las alas.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/art_source/unity/characters/human_locomotion_contract.py` — Contrato de marcha: 30 fps, frames 1-61, duración nominal = 2/contacts, distancia = velocidad × duración, duty/lift por perfil. Contacto izquierdo en fase 0 y derecho en 0,5 (coincide con HumanLocomotionClock).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/unity/characters/UNITY-INTEGRATION.md` — Documentación del contrato de prefabs, IDs de estados, API de Presentation y la corrección de orientación y BakeMesh.
- `N:/LetMeSleep/Repository/unity/Assets/Editor/ProjectBootstrap/HiggsfieldHumanPilot.cs` — No está en el worktree (sin trackear en Repository). Piloto HF_Human_Default.fbx con nombres tipo Mixamo (Head/Spine3/RightHand/LeftFoot), textura 2K, 28.164 triángulos, sólo Idle/Walk y anclas sin offsets (L80). No es compatible con el contrato actual.
- `N:/LetMeSleep/Repository/Higgsfield/DEFAULTS-UNITY-ESTADO.md` — Estado del piloto Higgsfield: no conectado a juego ni menú; el usuario lo juzgó lento y medio feo. El .blend editable está en N:/LetMeSleep/Worktrees/characters/Higgsfield/HUM-RIG-001/HUM-RIG-001_unity.blend.

### Puntos de extensión

- Carpeta fuente nueva sin pisar la del alfa: cambiar CharacterContentBuilder.SourceRoot (L23-24) a p. ej. art_source/unity/characters_v030. Mantener igual la ruta del asset Unity (ModelPath L183 = Models/LMS_<Especie>_alpha.fbx) para conservar el GUID del FBX y los fileID de los clips que referencian LetMeSleepBoot.unity y LetMeSleepHiggsfield.unity (MenuSeatedIdle..., Mosquito_Fly). Si se renombra el FBX hay que actualizar FacialContentBuilder.Models (L18), HumanEyelidNormalsPostprocessor (L30-33) y LivingMenuContentBuilder (L19, L71), y volver a instalar las escenas.
- Menú: LivingMenuContentBuilder.BuildAndBind lee art_source/unity/characters/menu/LMS_HumanMenu.fbx (L25). Para no mantener dos FBX se puede apuntar Model a las tomas Menu* dentro del FBX principal, pero hay que ajustar clipAnimations, porque hoy CharacterContentBuilder sólo importa los clips del audit.
- Nuevas categorías de color, p. ej. camiseta y gorra: añadir casos en CharacterContentBuilder L370-372 y LivingMenuContentBuilder L117-121 (p. ej. Human_Shirt→"Shirt", Human_Cap→"Cap"), métodos en CharacterView (L105-108, ApplyColor ya es genérico), opciones en AlfaApplication.Preferences.cs L29-39 y el codec legacy de AppearancePeerState (cambia el protocolo de apariencia).
- Accesorios por socket (gorra, gorro, mochila del Humano 03): el rig ya tiene Socket.Head (humano, 1,72 m) y Socket.Back (humano y mosquito), pero no se exponen como anclas. Añadir {"HeadTop","Socket.Head"} y {"Back","Socket.Back"} a HumanAnchors/MosquitoAnchors (L33-42). Las partes SocketPart se colocan con transform local identidad bajo el ancla, orientada como el actor.
- Personalización por piezas (PER-04..08): crear un CharacterCustomizationCatalog.asset. En los prefabs (desde CharacterContentBuilder.BuildCharacter) añadir CharacterCustomizationHost (Role, RigId p. ej. "lms-human-v030", ExpectedVisualScale 1 o 0,5, PartsRoot como hijo de VisualRoot fuera del Model, OwnedBaseRenderers) y CharacterModularVisualAssembler. Poner ModularCustomizationRuntimeProvider en la escena con Catalog y LegacyMappings.
- Prefabs de piezas: importar el FBX de la pieza con animationType None (el Assembler rechaza el componente Animator, L209-212), raíz con transform identidad, BonePaths relativos a CharacterView.Animator (p. ej. "LMS_HumanRig/Root/Hips/Spine/Chest") y un único CharacterCustomizationPart en la raíz.
- Párpados del humano por huesos en lugar de morphs: VisualAttentionRig ya soporta LeftLids/RightLids. Habría que cambiar la rama humana de FacialContentBuilder.Bind (L119-126, usar Lid() como el mosquito) y relajar HumanEyelidNormalsPostprocessor (L85-86), que hoy lanza excepción sin los 8 blendshapes.
- Expresiones de los bocetos (neutral/enojado/alerta): los huesos Brow.L/R y Jaw ya existen en el rig humano pero ningún runtime los mueve. Punto natural: VisualAttentionRig, que es el último escritor facial, o una capa de Animator con máscara.
- Tamaño del mosquito: la escala visual 0,5 está fijada en BuildCharacter L336, ValidateCharacter L433 y MosquitoRagdollBuilder.SourceFrame L54-57. Las constantes de juego están en GameplayActorProxy L31, GameplayAuthority L391/L424 y UnityGameplayWorld L217/L281/L353.
- Herramientas pendientes: GameplayVisualPresenter y AlfaPresentationBuilder ya buscan Prefabs/LMS_Slipper, LMS_ElectricRacket y LMS_Aerosol (L476-478) con ToolView (ToolId, Grip, Impact). Basta con generarlos con BuildTool o un builder similar.
- Ragdoll del mosquito nuevo: reescribir SourceFrame, LegPoints, AddWing y el contorno de alas de MosquitoRagdollBuilder (L40-61, L345-382) a partir de las posiciones bind del rig nuevo (hoy son constantes R4). Sólo lo usa MosquitoRagdollPlayModeProof.

### Contratos y restricciones

- Humano, jerarquía del armature 'LMS_HumanRig' (nombres únicos en todo el modelo, CharacterContentBuilder.Unique L626-631): Root > Hips > Spine > Chest > Neck > Head > {Jaw, Socket.Eye, Socket.Head, Eye.L, Eye.R, Brow.L, Brow.R}. Chest > {Socket.Back, Socket.AimChest, Shoulder.{L,R} > UpperArm > LowerArm > Hand > {Socket.Grip, Index01-03, Middle01-03, Ring01-03, Little01-03, Thumb01-03}}. Hips > UpperLeg.{L,R} > LowerLeg > Foot > {Toe, Socket.Foot}. Los imprescindibles para runtime son Root, Hips, Neck, Head, Eye.L/R, UpperArm/LowerArm/Hand.{L,R}, Foot.{L,R} y los sockets; los dedos son opcionales si no se requiere FingerCurl ni mano cerrada.
- Anclas humanas (L33-37): CameraEye→Socket.Eye, AimChest→Socket.AimChest, HandGrip_L→Socket.Grip.L, HandGrip_R→Socket.Grip.R, ToolSocket_R→Socket.Grip.R, Foot_L→Socket.Foot.L, Foot_R→Socket.Foot.R. En runtime se consumen CameraEye, ToolSocket_R y Foot_L/Foot_R; AimChest y HandGrip_* sólo se validan.
- Anclas del mosquito (L38-42): CameraTarget→Socket.CameraTarget, AimForward→Socket.AimForward, ProboscisTip→Socket.Mouth, WingRoot_L/R→Socket.WingRoot.L/R, GroundContact→Socket.GroundContact.
- Huesos del mosquito 'LMS_MosquitoRig': Root > {Thorax > {Head > {Proboscis > Socket.Mouth, Socket.AimForward, Pupil.L/R, LidUpper.L/R, LidLower.L/R}, Abdomen01 > Abdomen02, Socket.Back, Socket.CameraTarget, Socket.WingRoot.L/R, Wing.L/R, Leg{1,2,3}01 > Leg{1,2,3}02 > Leg{1,2,3}03 .{L,R}}, Socket.GroundContact}. La cámara usa Thorax/Head/Abdomen01/Abdomen02 (GameplayVisualPresenter L229) y el ragdoll exige el esquema R4 exacto.
- Ejes y unidades: fuente en Blender en metros, Z arriba, frente -Y, .L en +X. Export FBX con axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False, use_armature_deform_only=False (los Socket.* no deformantes deben exportarse), bake_anim_use_all_actions=True, bake_anim_simplify_factor=0, mesh_smooth_type='FACE' (build_characters.py L220-225). El builder deduce el frente con Head→Socket.Eye (humano) o Thorax→Socket.Mouth (mosquito) y aplica sólo yaw (L528-548).
- Gates de CharacterContentBuilder.ValidateCharacter que un modelo nuevo debe pasar: Avatar Generic válido y no Humanoid, sin root motion (L428-431). VisualRoot 1 en el humano y 0,5 en el mosquito (L433). Socket.Eye con z de actor > 0,10 y Socket.Mouth > 0,09 (L592). UpperArm.L o Wing.L con x < -0,001 y .R con x > +0,001 (L593). Foot.L x < -0,08 y Foot.R x > +0,08 (L596-597).
- Más gates humanos: CameraEye con y = 1,53 ± 0,005 m (L442) y exactamente 2 HeadRenderers llamados HumanHead y HumanNightcap (L358, L441). La cápsula 0,25/1,72 la genera el builder.
- Más gates del mosquito: ProboscisTip en (0,0,0,095) ± 1 mm local al actor en reposo (L447-448), es decir Socket.Mouth en (0,-0,19,0) de la fuente con Root en el origen. Un vértice de malla a menos de 2 mm de la punta (L617). Esfera de 0,055.
- Gates comunes: triángulos y número de SkinnedMeshRenderer iguales al audit (L453-455); bindposes = bones y sin huesos nulos (L458-460); todos los materiales URP/Lit (L461); duración de cada clip igual al audit ± 1,1/30 s (L471); curvas sólo sobre rutas existentes (L473); hueso Root inmóvil (< 1 mm) en todos los clips (L479); costura de pies en loops humanos ≤ 2 cm (L490); pose bind coherente entre renderers (RestoreBindPose L550-584). Usar como máximo 4 influencias por vértice (SkinQuality.Bone4, L365).
- Clips (nombre exacto '<Especie>_<Estado>'; la toma del FBX debe terminar con ese nombre, L222). Humano: IDs 0-14 = Idle, Walk, Run, Crouch, Jump, Land, Turn, Clap, Hit, Fall, Faint, Recover, Swat, Blink, FingerCurl; 15 WalkSlow y 16 Trot. Si existe WalkSlow o Trot, deben existir en bucle las 4 marchas Human_WalkSlow/Walk/Trot/Run (L285-295). Mosquito 0-14 = Idle, Hover, Fly, Brake, PerchEnter, PerchIdle, SurfaceWalk, BiteStart, BiteLoop, Detach, Hit, Fall, Recover, Land, Bite. Bucles: Idle, Walk, Run, WalkSlow, Trot, Fly, Hover, PerchIdle, SurfaceWalk, BiteLoop.
- Marchas humanas (HumanLocomotionSetup L10-12 y human_locomotion_contract.py): en el sitio, 30 fps. Velocidades 1 / 1,55 / 3,1 / 5 m/s con distancia por ciclo 0,8333 / 0,96875 / 1,55 / 2,1739 m. Un ciclo son 2 pasos: contacto izquierdo en fase 0 y derecho en 0,5.
- Menú: LMS_HumanMenu.fbx con el mismo armature (mismas rutas relativas al Animator, p. ej. 'LMS_HumanRig/Root/Hips/...') y tomas MenuSeatedIdle (bucle), MenuLook, MenuSwat y MenuReturn; asiento del mapa a 0,575 m. El menú reproduce Mosquito_Fly del FBX del mosquito (LivingMenuContentBuilder L71-72).
- Facial del humano (FacialContentBuilder L97-126): pupila e iris pesados al 100 % (≥ 0,999) a Eye.L/R, con centroide por delante del pivote (dot > 0,96). Si todo el globo se pesa a Eye, el centroide cae en el pivote y falla. Eye.L y Eye.R con la misma orientación bind, sin roll espejado (Symmetrize de Blender lo rompe). Neck y Head con la misma orientación bind. HumanHead con los 8 shape keys Blink25.L, Blink50.L, Blink75.L, Blink.L y los mismos .R; interpolación por tramos entre muestras vecinas (VisualAttentionRig L25-26). Además HumanEyelidNormalsPostprocessor lanza excepción si falta alguno (L85-86).
- Facial del mosquito (L129-130, Lid L185-196): Pupil.L/R, LidUpper y LidLower con vértices rígidos. El pivote va en el centro del ojo. En reposo, el párpado superior queda arriba y algo por detrás del pivote y el inferior abajo y detrás, de modo que girar ±90° sobre el eje derecho del actor lo cierre hacia adelante. Pupil.L y Pupil.R con la misma orientación.
- Nombres de renderers: humano HumanBody, HandSkin.L, HandSkin.R, HumanHead, HumanNightcap. Primera persona: sólo HumanHead y HumanNightcap pasan a ShadowsOnly; el resto sigue visible bajo una cámara con near 0,03. Mosquito: MosquitoSkin, MosquitoMembranes y MosquitoVeins (estos dos últimos sin sombras, L362-364).
- Materiales: nombres estables y con prefijo por especie, porque los .mat se comparten por nombre en Materials/. Human_Skin es la categoría Skin; Human_Pajamas es Pajamas (hoy el gorro también usa Human_Pajamas y se recolorea con el pijama); Mosquito_Shell y Mosquito_Abdomen son la categoría Mosquito (ambos reciben el mismo color, así que un mosquito bicolor pierde el segundo tono al personalizar). Mosquito_Wing recibe tratamiento transparente. El builder no asigna _BaseMap, así que las texturas se pierden: usar color plano por material.
- Primera persona: no existe malla de brazos aparte. El cuerpo completo debe verse bien desde Socket.Eye con pitch de -110° a +75°, y la cabeza debe estar separada en HumanHead/HumanNightcap. Para la IK de golpe, UpperArm, LowerArm y Hand deben ser cadena padre-hijo directa (ActorVisualBinding L41-45).
- Herramientas: ToolView con Grip e Impact hijos, GripToImpact entre 0,35 y 0,38 para el matamoscas (ValidateTool L508) e Impact en +Z (L510). Se montan en ToolSocket_R alineando la rotación de Grip con el ancla (GameplayVisualPresenter L282-284).
- Envolvente de juego que la malla debería cubrir (GameplayActorProxy L17-31): cabeza centrada en 1,57 m con r 0,13; torso en 1,19 m (r 0,18, alto 0,56); brazos ±0,25 a 1,26 m; muslos ±0,11 a 0,69 m; espinillas a 0,28 m. Mosquito: esfera de r 0,055 centrada en Root, punta en +Z 0,095, apoyo en superficie a 0,057 (GroundContact = -0,057). Si no coincide, la mordida (ApplyBiteAnchor) desplaza el cuerpo visual; con corrección > 15 cm registra LMS_BITE_VISUAL_OFFSET.
- Online: la malla no viaja por la red. Snapshots, IDs de superficie (ActorId×100+parte) y ataduras de mordida van contra los proxies. La apariencia legacy por IDs de color o modular por fingerprint no requiere cambios mientras se mantengan las categorías de color. Todos los clientes deben usar el mismo build de catálogo.

### Brechas frente a bocetos

- Humano (PER-01/UI-06): el alfa es geometría de tubos y elipsoides con pijama y gorro de dormir, 9.652 triángulos. Los bocetos piden cabeza facetada low-poly con ojos blancos enormes y pupila negra, gorra roja o gorro, camiseta clara, pantalón de pijama y pantuflas. Sólo existe un humano, sin las variantes Humano 02 (overol) ni Humano 03 (hoodie con mochila).
- Ojos humanos: pivotes en ±0,081 / 1,558 m con globos pequeños. Los bocetos exigen globos mucho mayores, a encajar dentro de la cápsula de cabeza (1,57, r 0,13) o aceptar un desajuste con la hitbox.
- Expresiones (neutral/enojado/alerta, PER-01 y PER-03): no hay sistema de expresión. Brow.L/R y Jaw existen sin driver y el mosquito no tiene cejas.
- Mosquito (PER-03): el alfa es rojo oscuro, 3.588 triángulos, un ala por lado y ojos moderados. El boceto pide ojos enormes, dos pares de alas facetadas translúcidas, patas muy largas y probóscide recta larga. Faltan las variantes 02 (oliva) y 03 (azul, más grande).
- Escala: PER-03 dibuja mosquitos de unos 0,5-0,8 m junto a un humano de 1,8 m. En el juego el mosquito mide unos 0,22 m de envergadura (VisualRoot 0,5, radio 0,055). El inventario ya indica 'fijar escala y anatomía'. La decisión está pendiente y afecta a Gameplay.
- Personalización por piezas (PER-04..08: pelo, gorros, ropa, accesorios; alas, ojos, abdomen, marcas): sólo funcionan 3 canales de color legacy. El sistema modular está en código, pero no hay catálogo, host, assembler ni provider instalados, ni anclas HeadTop/Back.
- Vista 3D giratoria: existe (CharacterPreviewOrbit, órbita y zoom en la capa 30 con cámara de 35°), pero muestra los modelos alfa y sólo cambia color.
- Estética facetada y colorida: el pipeline ya es de color plano y normales por cara. Encaja con los bocetos, pero la paleta actual es apagada (pijama 0,12/0,30/0,47, mosquito 0,36/0,045/0,027) frente a los rojos, azules y cremas saturados de los bocetos.
- Herramientas y props (PRP-01): sólo existe el matamoscas. Slipper, ElectricRacket y Aerosol están referenciados, pero sus prefabs no existen (LMS_TOOL_PREFAB_MISSING).
- Piloto Higgsfield (Repository, sin trackear): malla texturizada de 28k triángulos con nombres tipo Mixamo y sólo Idle/Walk. No está integrado y el usuario lo juzgó lento y medio feo; no es base para el reemplazo directo.

### Riesgos

- Gobernanza: el AGENTS.md del worktree contiene un STOP de alfa congelada, la orden de crear lo nuevo desde cero a partir de los bocetos sin reutilizar rig ni proporciones del alfa como base artística, y reparte la propiedad (Modelador 1 dueño de art_source/unity/characters; Director dueño de builders, proyecto y turnos Unity/Blender). Reutilizar los NOMBRES de huesos y sockets es un requisito técnico de integración (HUM-INT-001), pero hay que declararlo explícitamente y no copiar la malla ni el rig autoral.
- Reimportar los FBX invalida los certificados faciales (FacialContractImportGuard). Hasta volver a correr FacialContentBuilder no hay atención ni parpadeo (LMS_FACIAL_SKIPPED), y PrepareV020 no reconstruye el prefab del menú; sólo lo hace AlfaBootstrapBuilder.Build.
- HumanEyelidNormalsPostprocessor lanza excepción al importar Models/LMS_Human_alpha.fbx o LMS_HumanMenu.fbx si falta HumanHead o alguno de los 8 blendshapes Blink: el import falla antes de llegar al builder.
- Gates rígidos que un diseño cabezón puede romper: CameraEye a 1,53 ± 0,005 m, exactamente 2 renderers de cabeza, pies a más de ±0,08 m, punta del mosquito a (0,0,0,095) ± 1 mm con malla a menos de 2 mm, y ojos y cuello con la misma orientación bind (un Symmetrize de Blender espeja el roll y falla).
- Renombrar clips o FBX rompe referencias de escena a subassets (MenuSeatedIdle/MenuLook/MenuSwat/MenuReturn y Mosquito_Fly en LetMeSleepBoot.unity y LetMeSleepHiggsfield.unity). Hay que conservar nombres y rutas de assets, o volver a instalar las escenas.
- Escala de PER-03 incompatible con gameplay (radio 0,055, punta 0,095, apoyo 0,057). Agrandar sólo lo visual deja mordidas y golpes desalineados; cambiar gameplay afecta a la autoridad online y al balance.
- El ragdoll del mosquito (MosquitoRagdollBuilder) tiene la geometría R4 codificada a mano y exige escala 0,5 exacta. Con un mosquito nuevo lanza ArgumentException ('Expected the unmodified R4 skeleton...'). Hoy no afecta al juego porque sólo lo usa MosquitoRagdollPlayModeProof.
- Texturas: el builder remapea a materiales de paleta sin _BaseMap. Un modelo texturizado (Higgsfield, Meshy) se vería plano o incorrecto salvo que se extienda UpsertMaterial.
- Personalización: Mosquito_Shell y Mosquito_Abdomen comparten la categoría 'Mosquito' (se pierde el bicolor) y el gorro usa Human_Pajamas. Añadir categorías nuevas cambia el codec legacy de AppearancePeerState, y activar el sistema modular cambia el fingerprint: todos los peers deben tener el mismo build.
- Desajuste entre la malla y los proxies de GameplayActorProxy (cabeza en 1,57 r 0,13, hombros del proxy en 1,39 frente a unos 1,17-1,20 del rig alfa): la corrección visual de la mordida mueve al mosquito, con posible penetración o flotación visible.
- Rendimiento: el alfa tiene 9,6k y 3,6k triángulos, con 5 submallas en HumanBody. El piloto HF tiene 28k triángulos y textura 2K. No hay LOD. No se ha medido FPS con modelos nuevos.
- El piloto Higgsfield del Repository no está trackeado ni presente en el worktree, y usa nombres incompatibles. Adoptarlo exigiría adaptar el runtime en varios sistemas (camino largo).

### Verificación

Nada de esto se ejecutó: el análisis fue de sólo lectura. Todo lo que sigue requiere un turno Unity/Blender asignado.

1) Blender, por especie:
- Exportar el FBX y generar audit.json con el mismo esquema que build_characters.py export() (L172-218): passed, triangles, mesh_count, bones, bone_names, bind_bones, clips{name,duration_seconds,loop}, material_palette, blend_shapes, sockets.
- Reutilizar o adaptar las auditorías existentes: verify_fbx.py, audit_human_joints.py, audit_human_facial.py, audit_mosquito_candidate.py, check_human_locomotion_contract.py y audit_motion.py, en art_source/unity/characters.
- Patrón de ejecución: blender --background --python <script>.py -- <args>.

2) Unity, en el worktree N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity:
- Menú 'Let Me Sleep/Content/Build Characters and Check Idempotence', o en batch: Unity.exe -batchmode -projectPath <unity> -executeMethod LetMeSleep.Content.Characters.Editor.CharacterContentBuilder.BuildAndVerifyIdempotence -quit -logFile <log>.
- En el log deben aparecer LMS_CHARACTER_BUILD_PASSED y LMS_CHARACTER_IDEMPOTENCE_PASSED.
- En Assets/LetMeSleep/Content/Characters/BuildReceipt.json revisar success=true y, en validations: CameraEye con bonePath .../Head/Socket.Eye, orientation.frontPointActorLocal (humano y≈1,53, z>0,10; mosquito (0,0,0,095)), nearestTipVertexDistance < 0,002, maximumLoopFootDelta ≤ 0,02 y la lista de motions.

3) Menú y certificación facial:
- Ejecutar -executeMethod LetMeSleep.Editor.AlfaBootstrapBuilder.Build (reconstruye LMS_HumanMenu y certifica la cara). Para la escena v0.2, -executeMethod LetMeSleep.Editor.WindowsAlfaBuild.PrepareV020.
- En el log deben aparecer LMS_LIVING_MENU_CONTENT_BUILT y 4 líneas LMS_FACIAL_IMPORT_BOUND.
- Comprobar en los prefabs que VisualAttentionContract.SourceSha256 coincide con el SHA-256 del FBX nuevo.
- Si cambian rutas de prefabs: -executeMethod LetMeSleep.Presentation.Editor.AlfaPresentationBuilder.BuildFromCommandLine.

4) Tests PlayMode (Test Runner o -runTests -testPlatform PlayMode):
- Deben pasar: HumanLocomotionTransitionTests, GameplayToolVisualPresentationTests, CharacterModularVisualAssemblerPlayModeTests, ModularCustomizationPersistenceIntegrationTests, ModularPersistenceContractPlayModeTests y CharacterCustomizationCatalogPlayModeTests.
- MosquitoRagdollPlayModeProof fallará con un mosquito nuevo hasta adaptar MosquitoRagdollBuilder.

5) Evidencia visual:
- -executeMethod LetMeSleep.Editor.CharacterRenderReview.Capture (requiere GPU), con salida a N:/LetMeSleep/Artifacts/review/alfa-characters. Copiar la evidencia a N:/LetMeSleep/Validation/V030/Personajes/.

6) Recorrido en juego, ambos roles:
- El log no debe contener LMS_FACIAL_SKIPPED, LMS_MENU_FACIAL_PENDING, LMS_TOOL_SOCKET_MISSING, LMS_CHARACTER_PREFAB_MISSING, LMS_BITE_VISUAL_OFFSET ni LMS_BITE_VISUAL_RESIDUAL. LMS_BITE_CONTACT debe mostrar finalResidual ≤ 0,001.
- Revisar la primera persona con pitch -110 y +75 (sin ver el interior de la cabeza ni recortes del cuello), el golpe del matamoscas, pies sin deslizar en las 4 marchas, la mordida sobre la cabeza y los brazos del humano, la percha en suelo, pared y techo, la vista previa giratoria y el cambio de colores.
- Online: dos clientes con el mismo build, comprobando que la apariencia remota se aplica.

## animation

Estado actual (rama claude/v0.3.0, worktree N:/LetMeSleep/Worktrees/v020-candidate-20260920). Los personajes se animan con un sistema mixto, pero muy rígido:
(1) Un Animator Generic por personaje (LMS_Human.controller con 17 estados, LMS_Mosquito.controller con 15). No tienen transiciones, blend trees, capas ni IK: el código fuerza cada estado con CrossFadeInFixedTime o Play. El parámetro Int "Motion" se asigna, pero nada lo lee.
(2) Para la marcha humana, un PlayableGraph manual (HumanLocomotionPresenter) quita el controller del Animator y mezcla 4 clips (WalkSlow/Walk/Trot/Run) según la distancia renderizada.
(3) Escritores procedurales después de la animación:
- AlignArm: IK analítico de 2 huesos durante el golpe.
- ApplyBiteAnchor: traslada todo el mosquito hasta el punto de picadura.
- ResolveVisualRotation: alinea el mosquito a la superficie.
- VisualAttentionRig: cabeza, ojos, microsacadas y parpadeo.
Todos los clips se hornearon en Blender con scripts procedurales (art_source/unity/characters/author_motion.py y author_mosquito_motion.py). Hay una sola ruta de ragdoll probada (mosquito R4, 18 cuerpos), pero no está conectada al juego.

Parámetros usados: sólo el ActorSnapshot autoritativo a unos 30 Hz (LifeState, Velocity, Grounded, CrouchFraction, MotionPhase = distancia/1.2 m o /0.3 m, StrikeState, Surface/BiteAttachment, RecoveryEndTick) y 4 eventos (StrikeStarted, BiteStarted, BiteEnded, MosquitoKnockedDown).

Defectos concretos encontrados en código:
a) En la bajada de cada salto humano se reproduce el clip de desmayo Human_Fall (SelectMotion devuelve 9 cuando Vy<=0.1).
b) Golpear o agacharse en movimiento congela las piernas y el personaje se desliza: la marcha se desactiva y el clip Swat/Crouch es de cuerpo completo.
c) Entrar y salir de la marcha son cortes secos, porque se cambia runtimeAnimatorController=null.
d) El aleteo del mosquito se escala con la distancia recorrida (entre 2,6 y 18,75 Hz) y en remotos se resincroniza por la fase de distancia.
e) Recovering dura 0,4 s en la autoridad, pero los clips Recover duran 2,0 s (humano) y 1,2 s (mosquito), así que se cortan y el personaje "salta" a Idle.
f) El mosquito aturdido (Stunned) muestra la pose final de Hit, no la de Fall.
g) El vuelo es de un bloque: el cuerpo sólo gira en yaw, sin inclinación ni alabeo.
h) La marcha alfa tiene cadera baja, codo constante (~168°) y ninguna transferencia de peso.
i) El golpe estira el brazo a ~177,7° por AlignArm.
j) No existen expresiones faciales. Sólo hay parpadeo y mirada.

Para acercarse a los bocetos hacen falta seis cosas:
1. Un grafo unificado con mezclas, más una capa de torso con AvatarMask.
2. Estados nuevos: aire/caída de salto, aterrizaje, agachado caminando, giro y levantarse ajustado a RecoveryEndTick.
3. Un componente de movimiento secundario: squash & stretch con springs, inclinación y alabeo en vuelo, pelvis y hombros.
4. Un sistema de expresiones (neutral/happy/angry/alert/sleepy/surprised/focused/dizzy) dentro de VisualAttentionRig, con párpados del mosquito inclinables y blendshapes humanos nuevos (Smile, MouthO, párpado inferior).
5. Aleteo independiente de la distancia, con blur de alas.
6. IK de pies y de brazo con flexión mínima.
Todo debe ser sólo visual, sin tocar autoridad ni red.

### Cómo funciona

1) CADENA POR FRAME (orden de ejecución)
- MainMenuLivingScene: 900 (sólo menú).
- CharacterView.LateUpdate: 1000. RefreshAnchors copia los huesos a PresentationAnchors.
- ActorVisualBinding.LateUpdate: 1100. Interpola la pose, evalúa la marcha y hace el IK de brazo. Después: RefreshAnchors, PublishContacts y ApplyBiteAnchor.
- GameplayAttentionTarget y MosquitoRagdollSimulation: 1150.
- VisualAttentionRig: 1200. Cabeza, ojos y párpados. El evento AfterEvaluation lo usa la medición de picadura.
- MosquitoFollowCamera: 1250.
- HumanViewCamera: 1300. Refresca el anchor CameraEye (Socket.Eye) y coloca ahí la cámara de primera persona.
El Animator nativo evalúa antes de LateUpdate. Casi todos los huesos tienen curva en todos los clips (los clips se muestrean por frame en Blender), así que cualquier escritura procedural debe repetirse en cada LateUpdate.

2) HUMANO
Selección de estado en ActorVisualBinding.SelectMotion (433-445), en este orden:
- StrikeState≠None → 3 si CrouchFraction>.25, si no 12 (Swat).
- Fainted → 10; Recovering → 11; Falling → 9.
- !Grounded → 4 (Jump) si Velocity.Y>0.10, si no 9 (Fall).
- CrouchFraction>.25 → 3 (Crouch).
- Velocidad planar >2.45 → 2 (Run); >0.10 → 1 (Walk); si no → 0 (Idle).

Si el actor es elegible para la marcha (CanUseLocomotion 64-68: grounded, Active, crouch≤0.1, sin golpe, sin temporal y speed>0.1):
- ApplyMotion no toca el Animator.
- HumanLocomotionPresenter.EvaluateRenderedPose le quita el controller (runtimeAnimatorController=null, línea 123).
- Mezcla WalkSlow/Walk/Trot/Run por la velocidad medida con transform.position entre frames.
- La fase avanza con distancia/zancada. Los contactos izquierdo/derecho en fase 0 y 0,5 emiten pisadas de audio (GameplayAudioPresenter.HandleFootContact).
Al salir de la marcha:
- Si deja de ser elegible: ReleaseLocomotion y ApplyMotion con crossfade de 0,10 s. Pero reasignar el controller reinicia la máquina en Idle, así que el cambio es visualmente seco.
- Si el grafo se suspende (discontinuidad o dt>0,25): ApplyMotion(current, true, false), que es un Play inmediato sin fade (línea 249).

Velocidades reales (GameplayAuthority:366): 3,1 m/s por defecto, así que la marcha normal ES el perfil Trot (trote con fase de vuelo). Sprint hasta 5,0 (Run). Agachado 1,55, pero con crouch>0.1 no hay marcha: se desliza la pose estática Crouch (clip de 1 s sin loop). La velocidad cambia de golpe, sin aceleración.

Controller sin marcha (fallback): Walk y Run se escalan con 1,2 m/ciclo (ApplyAnimatorSpeed 380-388, limitado a [0.35, 2.5]).

Golpe:
- StrikeStarted → PlayTemporary(12) con fade de 0,06 s y duración igual al clip (1,0 s), aunque la autoridad dura 0,6 s.
- Cada frame, ApplyAuthoritativeHands → AlignArm resuelve UpperArm y LowerArm (y Hand si hay matamoscas) hacia StrikeVisualTrajectory.Contact. Es IK analítico de 2 huesos con alcance limitado a upper+lower−0,1 mm. No usa Shoulder ni Chest.
- Mientras dura el golpe la marcha no es elegible: si el humano golpea corriendo, las piernas quedan en la pose base del Swat y el cuerpo patina.

Salto: vy=4,6 y g=12 dan apex a 0,38 s y ~0,77 s en el aire. Jump (1,2 s) empieza ya en el aire, así que su agachada de anticipación ocurre en el aire. En la bajada (Vy≤0.1) SelectMotion devuelve 9 = Human_Fall, que es el clip de desmayo (la cadera rota −1,48 rad hacia atrás, author_motion.py:173-184). Land (5) no se usa nunca.

Desmayo:
- HumanFainted → Falling (1 tick) → Fall.
- Al tocar suelo → Fainted → Faint (1,8 s, repite la caída desde de pie y mantiene el último frame).
- Recovering dura 0,4 s y Recover dura 2,0 s: a los 0,4 s el personaje sigue casi tumbado y pasa a Idle en 0,10 s. Además la autoridad teletransporta al punto de recuperación (GameplayAuthority:537).

Giro: el cuerpo gira en yaw igual a la vista (Rotation.Yaw), interpolado. El clip Turn (6) no se usa.
IDs humanos nunca seleccionados en juego: 5 Land, 6 Turn, 7 Clap, 8 Hit, 13 Blink, 14 FingerCurl.

Actor local: fuera de la marcha escribe la pose del snapshot sin interpolar (ActorVisualBinding 211-214). Hay que verificar si produce escalones a 30 Hz en salto o agachado.

3) MOSQUITO
Selección (447-458):
- ApproachingSurface → 4 (PerchEnter).
- Surface → 6 (SurfaceWalk) si v>0.08, si no 5 (PerchIdle).
- PreparingBite → 7; Biting → 8; Falling → 11; Stunned → 10 (Hit); Recovering → 12.
- Flying → 2 (Fly) si v>0.12, si no 1 (Hover).
Eventos: BiteStarted → 7, BiteEnded → 9 (Detach), MosquitoKnockedDown → 10 (Hit) como temporal de 0,6 s.

Aleteo: un solo hueso por ala (Wing.L/R), rotación rígida horneada. Fly dura 0,4 s con 3 aleteos (7,5 Hz a speed 1). Pero UsesAuthoritativeDistancePhase (406-411) trata Fly (2) como paso por distancia:
- Animator.speed = 0,4·v/0,3, limitado a [0.35, 2.5]. Eso da 2,6 Hz justo sobre 0,12 m/s, 7,5 Hz a 0,75 m/s y 18,75 Hz desde 1,9 m/s hasta el máximo de 3,8.
- En remotos, SynchronizeLoopPhase fuerza la fase a MotionPhase (distancia/0,3) si el error pasa de 0,2, lo que produce saltos.
- Hover va a speed 1 (7,5 Hz): pasar de Hover a vuelo lento baja el aleteo a la tercera parte.

SurfaceWalk sí usa correctamente velocidad tangencial/0,1 m, con límite 8 y fase convertida.

Orientación: en vuelo, visualRotation = BodyRotation, que es sólo yaw (GameplayAuthority:402). No hay pitch hacia la velocidad ni alabeo en giros. En superficie y picadura se alinea a la normal con Slerp exp(−24·dt) (ResolveVisualRotation 643-690). En la picadura, ApplyBiteAnchor traslada el transform raíz para que Socket.Mouth toque el punto; registra LMS_BITE_VISUAL_RESIDUAL si el residuo pasa de 1 mm.

Caída: KnockDown → temporal Hit (0,6 s) → Falling → Fall (1,0 s) → al tocar suelo Stunned → estado 10 Hit. Si ya era el motion actual no se reinicia, así que queda la pose final de Hit (tórax .25/.40 rad), no la de espalda de Fall (1,15/1,25 rad). Recovering de 0,4 s corta Recover (1,2 s). R4 (física) no está conectado.
IDs de mosquito no usados en juego: 0 Idle (sólo es el estado por defecto, en preview y personalización), 3 Brake, 13 Land, 14 Bite.

4) CARA
VisualAttentionRig, instalado sólo con VisualAttentionContract certificado:
- Humano (RigRevision human-joints2-facial): Neck y Head (yaw 55/pitch 25), Eye.L/R (22/15), párpados por blendshapes Blink25/50/75/Blink por lado sobre el SkinnedMeshRenderer de la cabeza.
- Mosquito (mosquito-r4-facial): Head (25/15), Pupil.L/R (12/10), LidUpper y LidLower con ClosedAngleDegrees ±90 sobre su eje local.
Microsacadas y parpadeo aleatorio con semilla por instancia. El objetivo lo pone GameplayAttentionTarget. No existe ningún estado emocional:
- Cejas y mandíbula humanas (Brow.L/R, Jaw) sólo se mueven dentro de los clips Swat, Hit y Fall.
- El mosquito no tiene cejas.
- La boca humana es geometría con material Character_Expression, sin blendshapes de sonrisa ni de O.

5) IK Y BLEND
- No hay IK de Animator: rig Generic, m_IKPass 0, SetApplyFootIK(false), sin OnAnimatorIK ni paquete Animation Rigging en Packages/manifest.json.
- IK propio: AlignArm (runtime), Pose.chain (horneado en Blender para piernas, palmada y patas del mosquito) y BendJoint (colisionadores autoritativos).
- No hay BlendTree (ningún objeto !u!206 en los .controller). La única mezcla continua es el AnimationMixerPlayable de la marcha y el del menú.

6) ORIGEN DE LOS PARÁMETROS
ActorSnapshot (Gameplay/Contracts.cs 217-244):
- LifeState, Position, Velocity, BodyRotation, ViewForward/Yaw/Pitch, Grounded, CrouchFraction.
- MotionPhase (acumulada en GameplayAuthority:411).
- SurfaceAttachment, BiteAttachment.
- StrikeState (Phase, Progress, Hand, ToolId, Origin, Target).
- RecoveryEndTick (tick + ceil(Recovery·30)), EquippedToolId, LivesRemaining.
Eventos (Contracts.cs:93). Sólo se usan 4; están disponibles además StrikeImpact, RecoveryStarted, Recovered, HumanFainted, HelpStarted/Ended, LifeConsumed, ActorEliminated/Respawned, RoundEnded y Task*.
Locales: GameplayRuntime.LocalViewYaw/Pitch, LocalCameraRotation, MosquitoCameraDistance; estado privado (Stamina, SprintExhausted, ThrowCharge, PreparationProgress, ExtractionProgress).
Derivados en presentación: desplazamiento renderizado (HumanLocomotionClock) y landedFrame (usado sólo para el audio).

### Archivos clave

- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Gameplay/ActorVisualBinding.cs` — Núcleo de animación en juego ([DefaultExecutionOrder(1100)]). Interpola snapshots (LateUpdate 186-258: Lerp/Slerp 217-233, extrapolación máx. 0,10 s) y elige estado con SelectMotion (431-459: humano 433-445, mosquito 447-458). Arranca clips temporales por evento en ApplyEvent 157-184 / PlayTemporary 317-327, escala la velocidad del Animator según distancia (ApplyAnimatorSpeed 357-389, constantes HumanStrideMeters=1.2, MosquitoStrideMeters=0.3, MosquitoSurfaceStrideMeters=0.1 en 14-19) y resincroniza la fase en SynchronizeLoopPhase 300-315. IK del brazo en golpe: ApplyAuthoritativeHands 545-554 y AlignArm 563-628. Traslado a la picadura: ApplyBiteAnchor 461-487. Alineado a superficie: ResolveVisualRotation 643-690. Tiempos de crossfade en CrossFadeSeconds 731-738 (0,10 / 0,06 / 0,14 s).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Gameplay/HumanLocomotionPresenter.cs` — Marcha humana manual. AcquirePose (117-144) guarda el controller y pone animator.runtimeAnimatorController=null (línea 123). Crea el PlayableGraph 'LMS_HumanLocomotion' con un AnimationMixerPlayable de 4 entradas, sin foot IK (SetApplyFootIK(false)). EvaluateRenderedPose (73-98) muestrea cada clip en phase%1 * clip.length y mezcla lower/upper. ReleasePose (146-155) restaura el controller. PublishContacts (101-108) emite ContactReady (pisadas de audio).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Gameplay/HumanLocomotionClock.cs` — Reloj de fase a partir de la distancia horizontal renderizada. Advance 69-123: con speed<0,05 congela la fase (97-101); interpola perfiles por velocidad (103-112); genera contactos izquierdo/derecho en fases 0 y 0,5 (113-121). Se suspende si speed > 1,5 × el máximo del perfil.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Gameplay/HumanLocomotionSetup.cs` — Instalación todo-o-nada de la marcha. Nombres {Human_WalkSlow, Human_Walk, Human_Trot, Human_Run}, velocidades {1, 1.55, 3.1, 5} m/s y distancias por ciclo {0.8333, 0.96875, 1.55, 2.1739} m (líneas 10-12). Usa los sockets Foot_L/Foot_R y la revisión 'human-four-gaits-20260913' (línea 49).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Gameplay/GameplayVisualPresenter.cs` — Instancia los visuales. EnsureVisual (148-201) añade ActorVisualBinding, HumanLocomotionSetup.TryConfigure (178-182), VisualAttentionFactory.TryInstall más GameplayAttentionTarget (184-190) y las herramientas en ToolSocket_R (191-197). BindLocalCamera (210-232) conecta la cámara del mosquito a CameraTarget y a los huesos Thorax/Head/Abdomen01/Abdomen02. Punto natural para instalar componentes nuevos (movimiento secundario, humor/expresiones).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Runtime/CharacterView.cs` — Contrato visual ([DefaultExecutionOrder(1000)]). MotionBinding {Id, StateName, ClipName, Loop} (31-38). PlayMotion (92-103) hace SetInteger("Motion") y CrossFadeInFixedTime. Anchors → SourceBone se copian en RefreshAnchors (81-89) en cada LateUpdate. También gestiona colores y HeadRenderers de primera persona.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Runtime/VisualAttentionRig.cs` — Único escritor facial final ([DefaultExecutionOrder(1200)]). Cabeza y cuello con límites de yaw/pitch (Apply 209-228, ClampHeadCorrection 229-240). Microsacadas de ±1,1°/±0,65° cada 1,1-2,4 s (156-162). Parpadeo cada 3,2-5,8 s de 0,22 s (Blink 241-247): en humano con blendshapes Blink25/50/75/Blink por lado; en mosquito con huesos LidUpper/LidLower a ±90° (LidState.Apply 55-61). Restore (248-269) respeta a otros escritores. Aquí conviene añadir las expresiones.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Runtime/VisualAttentionFactory.cs` — Instala el rig facial sólo si el prefab lleva un VisualAttentionContract certificado: schema lms.visual-attention.v1, SourceSha256 de 64 hex y UnityAxesVerified (líneas 7-50). Si falla, el prefab queda sin cara y se registra LMS_FACIAL_SKIPPED.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Gameplay/GameplayAttentionTarget.cs` — Elige a quién mira cada actor ([DefaultExecutionOrder(1150)]): el actor más cercano dentro de un cono (NearestAttentionPolicy) o, si no hay, un punto a 8 m en la dirección de vista (37-89).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Physics/MosquitoRagdollSimulation.cs` — Ragdoll R4 del mosquito: 18 Rigidbody, ConfigurableJoint y cápsulas. Modos Prepared/LocalSimulation/RemotePose/Held/Disposed. Toma prestado el Animator (BorrowAnimation 325-333). ValidateAnimatedScale (398-402) lanza excepción si los cuerpos no tienen lossyScale 0,5, así que prohíbe squash sobre huesos durante la captura. Sólo se usa en Tests/PlayMode/MosquitoRagdollPlayModeProof.cs.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Physics/MosquitoRagdollBuilder.cs` — Fábrica R4 (Build, línea 63). BodyNames (12-17) y AuxiliaryNames (18-23). Exige el esqueleto R4 intacto a escala uniforme 0,5.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Runtime/MosquitoFollowCamera.cs` — Cámara del mosquito ([DefaultExecutionOrder(1250)]). El pivot sigue al anchor CameraTarget (Socket.CameraTarget, hijo de Thorax), así que cualquier movimiento procedural del tórax mueve la cámara. Mezcla a primera persona según la distancia (73-80) y oculta renderers.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Runtime/MainMenuLivingScene.cs` — Escena de menú viva: PlayableGraph manual con mixer de 4 clips del humano sentado (MenuSeatedIdle/Look/Swat/Return) y mosquito siguiendo una curva con alabeo de ±12° (Sample 230-284, bank 268-269). Es la única ruta con inclinación de vuelo y respeta ReducedMotion.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Gameplay/LobbyVisualPresenter.cs` — Sala: sólo Idle/Walk con HumanStrideMeters=1.2 (línea 12). ApplyPlaybackSpeed (122-135) limita a [0.35, 2.5]. SynchronizeLoop (153-172).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Controllers/LMS_Human.controller` — 17 estados en Base Layer: Idle 0, Walk 1, Run 2, Crouch 3, Jump 4, Land 5, Turn 6, Clap 7, Hit 8, Fall 9, Faint 10, Recover 11, Swat 12, Blink 13, FingerCurl 14, WalkSlow 15, Trot 16. m_Transitions vacías, m_AnyStateTransitions vacías (350), parámetro Motion Int (367-373), m_IKPass 0, sin máscaras, m_WriteDefaultValues 0.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Controllers/LMS_Mosquito.controller` — 15 estados: Idle 0, Hover 1, Fly 2, Brake 3, PerchEnter 4, PerchIdle 5, SurfaceWalk 6, BiteStart 7, BiteLoop 8, Detach 9, Hit 10, Fall 11, Recover 12, Land 13, Bite 14. Sin transiciones, blend trees ni capas.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Models/LMS_Human_alpha.fbx.meta` — animationType 2 (Generic), avatarSetup CreateFromThisModel. 17 clips a 30 fps: Idle/Walk/Run/WalkSlow/Trot de 0-60 en loop; Crouch 0-30; Jump 0-36; Land 0-18; Turn/Clap/Swat 0-30; Hit 0-18; Fall 0-36; Faint 0-54; Recover 0-60; Blink 0-30; FingerCurl 0-60. motionNodeName Root. Compresión apagada.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/Models/LMS_Mosquito_alpha.fbx.meta` — Generic. 15 clips: Fly/Hover 0-12 (0,4 s, loop), Idle/PerchIdle 0-60 loop, PerchEnter/Land/Brake 0-24, SurfaceWalk 0-30 loop, BiteStart/Detach/Hit 0-18, BiteLoop 0-30 loop, Bite 0-36, Fall 0-30, Recover 0-36.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Characters/BuildReceipt.json` — Recibo del builder 'alpha-characters-7-gait-clips'. Humano: 65 huesos (Root/Hips/Spine/Chest/Neck/Head{Jaw, Eye.L/R, Brow.L/R, Socket.Eye}, Shoulder/UpperArm/LowerArm/Hand más 15 falanges por mano, UpperLeg/LowerLeg/Foot/Toe). Mosquito: 39 huesos (Thorax, Head{Proboscis, Pupil.L/R, LidUpper.L/R, LidLower.L/R}, Abdomen01/02, Wing.L/R de un hueso cada ala, 3×3 segmentos de pata por lado). No hay antenas, huesos de ceja en el mosquito, gorro ni twist.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Editor/Characters/CharacterContentBuilder.cs` — Genera los controllers y prefabs desde art_source (menú 'Let Me Sleep/Content/Build Characters', BuildAll línea 111). Listas HumanStates (25-28) y MosquitoStates (29-32); HumanStatesFor sólo añade al final (285-295). ImportModel fija loopTime, lockRootRotation y lockRootHeightY (211-226). BuildController (297-323) crea el parámetro Motion, borra AnyState transitions y estados ajenos: cualquier edición manual del .controller se pierde.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/art_source/unity/characters/author_motion.py` — Autoría Blender de los clips humanos (función human 82-188): base/idle 84-96, gait antiguo 107-121, crouch 122, jump 124-134, land 135, turn 137-139, clap 140-153, swat 159-166 (sin Shoulder, cejas ±0,14), hit 167-172 (Jaw, Brow), fallen/Fall/Faint/Recover 173-187. IK Pose.chain en 33-47. La función mosquito 190-248 es legado: build_characters.py:345 usa author_mosquito_motion.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/art_source/unity/characters/author_mosquito_motion.py` — Clips del mosquito R4. flight_channels (22-38): 3 aleteos por loop de 13 frames = 7,5 Hz a speed 1, AIR_FLAP_RADIANS .58. Hover = Fly sin 'drive'. SurfaceWalk con contrato de zancada 0,1 m (9-16, 58-129). Además bite 241-253, detach 255-265, hit 282-290, fall_pose 292-301, recover 303-315.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/art_source/unity/characters/human_locomotion_contract.py` — Perfiles de la marcha actual (12-21): hip .685-.695, elbow .2 constante en caminar y .6 en correr, brazo ±.16/.28 rad. author_human_locomotion.py:43-67 los aplica (hips_height-.78, IK de pies, UpperArm senoidal, LowerArm constante, Shoulder sin animar).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Gameplay/GameplayAuthority.cs` — Fuente de los parámetros:
- Velocidad humana 1,55 agachado / 3,1 normal / 3,1+1,9·Sprint (366).
- Salto a 4,6 m/s y gravedad 12 (368).
- Mosquito siempre nivelado: MosquitoBody = Rotation.Yaw (402), máx. 3,8 m/s (404).
- MotionPhase += distancia/1,2 (humano) o /0,3 (mosquito) (411).
- Falling → Fainted/Stunned al tocar suelo (412-416).
- HumanFainted (505).
- Recovering de 0,4 s (537, 539-542).
- KnockDown (643).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Gameplay/StrikeVisualTrajectory.cs` — Línea de tiempo visual del golpe: Duration 0,6 s, SweepStart 0,08, SweepDuration 0,17, cierre a 8/30 s. PoseWeight y Contact (10-34) alimentan AlignArm.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Gameplay.Unity/GameplayActorProxy.cs` — Superficies de golpe autoritativas, posadas proceduralmente desde MotionPhase, Velocity y Strike (PoseLimbs 61-93, segmentos de brazo de 0,28 m). Son independientes del rig visual (brazo visual 0,27+0,23 m) y no se deben tocar.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/ceo/VISUAL-MOTION-CORRECTIONS-20260920.md` — Queja del usuario y criterios (50-74): rigidez, marcha antinatural, brazos estirados, caídas sin peso. Aceptación por clips completos frente/perfil/espalda.
- `N:/LetMeSleep/Validation/V020/HumanMotionSource01/REPORT.md` — Diagnóstico con números: cadera 71,6 mm bajo el bind, rodilla de apoyo a 52,5°, codo de 168,5° constante al caminar, 177,7° en el golpe por AlignArm, desajuste de 0,72 m de alcance de autoridad contra 0,50 m visual. Propone el A/B Human_Walk_R2_AB con criterios.
- `N:/LetMeSleep/Validation/V020/CharacterMotionAudit01/REPORT.md` — Auditoría: caída humana por clip sin física, R4 aislado, AlignArm como sospechoso del brazo recto; ruta de corrección.
- `N:/LetMeSleep/Validation/V020/MosquitoFallIntegrationDraft/CHECKPOINT-FROZEN.md` — Prototipo de R4 con tórax guiado por la autoridad (ThoraxAnchoredR4FallPrototype.cs). Congelado con 3 bloqueadores conocidos; no integrar sin resolverlos.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/ceo/definicion-v020/respuestas-20260920-065440/DECISIONES.md` — Decisiones del usuario sobre animación (465-500):
- A13: caminar cómico pesado sin retraso.
- A14: recuperación breve y mueca corta.
- A15: caída dirigida con ajuste físico acotado.
- A16: gestos de ayuda breves.
- A17: gorro y pijama con movimiento secundario moderado.
- A18: mosquito atento (antenas y patas).
- A19: cuerpo completo visible en primera persona.
- P15: controles por efecto para balanceo y sacudidas.

### Puntos de extensión

- ESTADOS NUEVOS (sólo añadir al final para no romper IDs). Sumar nombres después de 'Trot' en CharacterContentBuilder.HumanStatesFor (285-295) y después de 'Bite' en MosquitoStates (29-32). Crear las acciones en Blender en author_motion.human() y author_mosquito_motion.mosquito(). Regenerar con 'Blender --background --python art_source/unity/characters/build_characters.py -- --species Human|Mosquito' y luego el menú Unity 'Let Me Sleep/Content/Build Characters'. Mapear los IDs en ActorVisualBinding.SelectMotion y ApplyEvent. Estados sugeridos. Humano: 17 JumpAir (loop), 18 FallAir (loop, distinto del desmayo), 19 CrouchWalk (loop, 1,55 m/s), 20 TurnL90, 21 TurnR90, 22 GetUp (≤0,4 s), 23 Bitten (palmada al cuello), 24 Yawn o IdleSleepy, 25 Help, 26 Throw, 27 Victory. Mosquito: 15 StunnedLoop (mareado con patas temblando), 16 Dash/Lunge, 17 Celebrate, 18 IdleAlert (patas ajustándose, A18).
- CORRECCIÓN RÁPIDA DEL SALTO (ActorVisualBinding.cs:440). Cambiar `return state.Velocity.Y > 0.10f ? 4 : 9;` para no usar el 9 (desmayo). Mientras no exista FallAir: mantener Jump congelado cerca del apex con view.Animator.Play("Base Layer.Jump", 0, 0.5f) y speed 0. Al aterrizar (landedFrame == Time.frameCount en LateUpdate) lanzar PlayTemporary(5) con Land (0,6 s) o una versión recortada de 0,25 s.
- ALETEO INDEPENDIENTE DE LA DISTANCIA (ActorVisualBinding.cs:406-411 y 380-388). Para el mosquito, UsesAuthoritativeDistancePhase debe devolver true sólo en motion 6. En Fly usar Animator.speed = Mathf.Lerp(1.1f, 1.6f, v/3.8f) (8-12 Hz) y dar una fase inicial aleatoria por ActorId (Animator.Play(state, 0, hash01) al entrar). Así se elimina el salto de SynchronizeLoopPhase. Opcional: SkinnedMeshRenderer o quad translúcido 'WingBlur' anclado a WingRoot_L/R (anchors ya existentes), con alpha según la frecuencia.
- INCLINACIÓN Y ALABEO DE VUELO (ActorVisualBinding.ResolveVisualRotation, rama final 683-687 para LifeState.Flying). Sólo visual: visualRotation = bodyRotation * Quaternion.Euler(pitch, 0, roll), con pitch = clamp(avance/3.8, 0..1)·18° y roll = clamp(−yawRate·0.08, ±28°). Suavizar con 1−exp(−10·dt) y calcular yawRate con BodyRotation entre frames. En Hover añadir un bob de ±8 mm a 0,8 Hz sobre Thorax. Ojo: MosquitoFollowCamera toma el pivot de Socket.CameraTarget (hijo de Thorax).
- COMPONENTE DE MOVIMIENTO SECUNDARIO (nuevo, p. ej. Presentation/Gameplay/CharacterSecondaryMotion.cs, [DefaultExecutionOrder(1120)], instalado en GameplayVisualPresenter.EnsureVisual después de la línea 183). Springs amortiguados (ζ≈0,45, f≈3,5 Hz) aplicados cada LateUpdate sobre huesos ya evaluados. Squash & stretch sobre el hueso Root (LMS_HumanRig/Root o LMS_MosquitoRig/Root) conservando volumen: sy=1+s, sx=sz=1/sqrt(1+s). Disparadores: aterrizaje (s=−0,12·min(1,|Vy|/6)), despegue (+0,08), windup/barrido del golpe según StrikeVisualTrajectory (−0,05 y luego +0,05), aterrizaje en superficie del mosquito (−0,15), KnockedDown (−0,25 y luego +0,1), pulso de abdomen al picar (Abdomen01/02 de 1 a 1,25 según el tiempo en Biting). Humano: roll de pelvis ±4° y desplazamiento lateral 25% de la semiseparación de pies (31 mm) sincronizados con HumanLocomotionClock.Current.Phase; torsión contraria de Chest; flexión de LowerArm entre 18° y 30° según la fase; inclinación del torso por aceleración. Exponer en ActorVisualBinding accesores de sólo lectura (snapshot actual, landedFrame, localActor, LocomotionPresenter).
- CAPA DE TORSO (quitar el deslizamiento al golpear o agacharse). Opción Animator: en CharacterContentBuilder.BuildController crear un AvatarMask Generic con las rutas LMS_HumanRig/Root/Hips/Spine/Chest/** activas (asset nuevo, p. ej. Controllers/LMS_Human_UpperBody.mask). Añadir la capa 'UpperBody' (override, IKPass 0) con Swat y Clap, y controlar layerWeight desde ActorVisualBinding. Opción Playables (coherente con la marcha): en HumanLocomotionPresenter reemplazar la salida directa por un AnimationLayerMixerPlayable [0 = gait mixer, 1 = AnimatorControllerPlayable o clip de golpe] y llamar SetLayerMaskFromAvatarMask(1, mask). Después retirar `state.StrikeState.Phase == None` de CanUseLocomotion (línea 67).
- TRANSICIONES SIN CORTES. Dejar de alternar runtimeAnimatorController=null (HumanLocomotionPresenter.cs:123). Construir un único PlayableGraph por actor: AnimatorControllerPlayable.Create(graph, controller) más el mixer de marcha, conectados a un AnimationMixerPlayable raíz cuyos pesos se interpolan en 0,12-0,2 s al cambiar de dueño (Controller o Locomotion). Alternativa con Animator: un BlendTree 1D 'Locomotion' (umbrales 1 / 1,55 / 3,1 / 5, useAutomaticThresholds=false) con timeParameterActive=true y el parámetro float 'GaitPhase' alimentado por HumanLocomotionClock. Requiere generarlo en CharacterContentBuilder.BuildController.
- LEVANTARSE AJUSTADO A LA AUTORIDAD. Usar ActorSnapshot.RecoveryEndTick y el hostTick que llega a ApplySnapshot para empezar GetUp o Recover de modo que termine en RecoveryEndTick: normalizedTime = 1 − (RecoveryEndTick − tick)/30/clipLength. Otra opción: autorar Recover de ≤12 frames para los 0,4 s de Recovering (GameplayAuthority:537/577/589). Mosquito: Stunned debe mapear a StunnedLoop o a la pose final de Fall, no a 10 (ActorVisualBinding.cs:454), y el temporal por KnockedDown debería durar ~0,2 s.
- EXPRESIONES FACIALES dentro del único escritor facial (VisualAttentionRig). Añadir `public void SetMood(FacialMood mood, float weight, float blendSeconds = .12f)` con enum FacialMood {Neutral, Happy, Angry, Alert, Sleepy, Surprised, Focused, Dizzy, Confused, Excited}, en línea con PER-07 'Mood expressions' y PER-02. En EvaluateAfterAnimation calcular por lado upperBase, lowerBase, tiltDeg, pupilScale y browOffset; el cierre final es closure = max(base, blink). Mosquito: LidState.Apply (55-61) ya aplica AngleAxis(ClosedAngleDegrees·closure, LocalAxis); extender BlinkBone con `Vector3 TiltLocalAxis; float TiltDegrees` (por defecto 0, compatible) para párpados inclinados de enfado, cierre inferior ~0,35 para Happy y superior ~0,55 para Sleepy. Escala de Pupil.L/R entre 0,8 y 1,15 (Restore debe guardar y devolver la escala, como ya hace con scaleWritten). Humano: base de cierre con BlinkWeightPolicy.Fill sobre Blink25-Blink; Brow.L/R rotan y trasladan igual que Swat/Hit (±0,14 rad, +12 mm); Jaw −0,18 rad para Surprised. Happy y Surprised completos requieren blendshapes nuevos 'Smile' y 'MouthO', más 'LowerLid.L/R', creados en author_human_facial.py (finish_shapes 50-66) y declarados como nombres opcionales en VisualAttentionRig.Bindings.
- POLÍTICA DE HUMOR POR ESTADO (clase pura y testeable, p. ej. Presentation/Gameplay/GameplayMoodPolicy.cs, que alimenta SetMood desde GameplayAttentionTarget o un componente nuevo a 1150). Mosquito: Flying → Alert si hay un humano a <2 m en el cono; PreparingBite → Focused; Biting → Happy creciente; BiteEnded → Excited 1 s; MosquitoKnockedDown o Falling → Surprised; Stunned → Dizzy; Recovering → Sleepy hasta Neutral. Humano: Idle >6 s → Sleepy más bostezo (tema del juego); StrikeStarted → Angry 0,6 s (mueca corta, A14); StrikeImpact con acierto → Happy; BiteStarted con TargetActorId == self → Surprised y luego Angry; HumanFainted → ojos cerrados; Recovered → Sleepy 1 s. Todo local, sin cambios de red. Los emotes voluntarios necesitarían un comando nuevo.
- IK DE BRAZO CON FLEXIÓN MÍNIMA (ActorVisualBinding.AlignArm 563-628). Limitar `length` a ≈0,93·(upper+lower) para un ángulo interno ≤150°. Repartir el alcance restante rotando Shoulder.L/R (clavícula, hasta 20°) y Chest (torsión de hasta 10°) antes de resolver el brazo. Registrar el residuo entre el contacto autoritativo y el renderizado. Criterios en HumanArmPose01/REPORT.md y HumanMotionSource01 (89).
- IK DE PIES Y TERRENO (nuevo post-proceso a ~1110). Raycast desde Socket.Foot.L/R contra colisionadores del mundo (filtro UnityGameplayWorld.IsWorldCollider, como MosquitoFollowCamera.SetCollisionFilter). Resolver UpperLeg y LowerLeg con la misma matemática de AlignArm, extraída a un helper estático TwoBoneSolver, y compensar la pelvis. Sólo con Grounded y Active. No mover transform.position porque HumanLocomotionClock lo lee.
- GIRO Y PARTE SUPERIOR DEL CUERPO. En ActorVisualBinding.SetWorldPose (630-634) para humanos, retrasar el yaw de la cadera (umbral de 50-60° y paso con Turn, ID 6, si speed<0,1). Llevar Chest, Neck y Head hacia la vista con un reparto 30/30/40, además de lo que ya aporta VisualAttentionRig.
- SECUNDARIO DE ACCESORIOS (A17 moderado). Las partes socket se cuelgan de los anchors de CharacterView (CharacterModularVisualAssembler 347-364). Un spring o verlet ligero sobre un hijo pivote del gorro o pompón, ejecutado después de 1100, basta sin tocar el rig. Para piezas skinned haría falta un hueso nuevo (actualizar el assert `len(rig.data.bones) == 65` en author_human_locomotion.py:22 y recertificar el rig).
- RAGDOLL R4 DEL MOSQUITO. Punto de composición en GameplayVisualPresenter después de binding.ApplySnapshot, siguiendo el diseño de tórax guiado de Validation/V020/MosquitoFallIntegrationDraft. Primero hay que resolver sus 3 bloqueadores (CHECKPOINT-FROZEN.md) y el test de separación de articulaciones (docs/unity/RAGDOLL-POSE-PROTOCOL-STATUS.md:4-7).

### Contratos y restricciones

- Autoridad intocable. La animación es sólo presentación: no escribir GameplayActorProxy.BodySurfaces (PoseLimbs 61-93), el motor, LifeState ni la red. Los snapshots llegan a ~30 Hz (ActorVisualBinding usa tickDelta/30f, línea 144). El humor, el squash y la inclinación se derivan localmente del ActorSnapshot y los eventos.
- IDs estables de motion. CharacterView.MotionBinding.Id viene del índice de HumanStates/MosquitoStates, se serializa en prefabs y en BuildReceipt.json y se usa numéricamente en ActorVisualBinding.SelectMotion, ApplyEvent y UsesAuthoritativeDistancePhase. Sólo se permite añadir al final (comentario en CharacterContentBuilder.cs:293).
- Los .controller se regeneran. CharacterContentBuilder.BuildController (297-323) redefine los parámetros, borra AnyState transitions y estados que no estén en la lista, y fuerza writeDefaultValues=false. Capas, máscaras o blend trees deben implementarse en el builder, no a mano.
- Rig Generic con root estacionario. El builder exige que Root no se traslade (`rootStationary`, validación 474-479) e importa con lockRootRotation y lockRootHeightY. No hay root motion: el desplazamiento lo pone la autoridad.
- Contrato de marcha. Los 4 clips deben llamarse Human_WalkSlow, Human_Walk, Human_Trot y Human_Run, ser loop, no legacy, y tener contactos izquierdo/derecho en fase 0 y 0,5. Las velocidades y distancias por ciclo están fijadas en HumanLocomotionSetup.cs:10-12 y human_locomotion_contract.py. Las pisadas de audio dependen de ContactReady.
- Contrato facial. VisualAttentionFactory exige VisualAttentionContract con schema 'lms.visual-attention.v1', RigRevision, SourceSha256 (hash del FBX) y UnityAxesVerified. Cualquier cambio de FBX o rig obliga a recertificar el hash y los ejes; si no, se pierden ojos y parpadeo en silencio (LMS_FACIAL_SKIPPED). VisualAttentionRig debe seguir siendo el ÚNICO escritor facial (author_mosquito_face.py lo exige: 'one shared facial driver').
- R4. MosquitoRagdollSimulation.ValidateAnimatedScale (398-402) exige lossyScale 0,5 en los 18 cuerpos al capturar la pose. Cualquier squash sobre Root, Thorax o Abdomen debe anularse antes de CaptureAnimatedPose o BeginLocalSimulation. MosquitoRagdollBuilder exige los nombres y la escala uniforme de 0,5 (SourceFrame 43-58).
- Personalización modular. CharacterModularVisualAssembler exige VisualRoot.localScale == host.ExpectedVisualScale (línea 180; el VisualRoot del mosquito está a 0,5). Las partes skinned se enlazan por BonePaths (343-344): no escalar VisualRoot y no renombrar ni quitar huesos. Añadir huesos exige actualizar los asserts de conteo (65 humano en author_human_locomotion.py:22; 39 mosquito en author_mosquito_face.py, 'total_rig_bones').
- Picadura. ApplyBiteAnchor corrige la raíz después de la animación y la medición final ocurre tras VisualAttentionRig (AfterEvaluation). Un residuo >1 mm registra LMS_BITE_VISUAL_RESIDUAL. El squash o la inclinación deben ejecutarse antes de ApplyBiteAnchor (dentro de 1100 o a 1120 sólo si no hay BiteAttachment) o desactivarse en PreparingBite y Biting.
- Primera persona. HumanViewCamera coloca la cámara en Socket.Eye (hijo de Head). Bob, squash o inclinación del humano local mueven la cámara: desactivarlos en el actor local o compensarlos. Respetar P15 (controles por efecto) y ReducedMotion (settings.ReduceMenuMotion / AlfaUiMotionPreferences.ReducedMotion).
- La cámara del mosquito usa el pivot Socket.CameraTarget (hijo de Thorax) y los huesos Thorax, Head, Abdomen01 y Abdomen02 para ocultar el cuerpo (GameplayVisualPresenter 227-230). Mover Thorax proceduralmente desplaza el pivot.
- HumanLocomotionClock mide transform.position de la raíz visual. Los efectos secundarios no deben mover la raíz, sólo huesos por debajo de Root.
- Normas del proyecto (AGENTS.md del worktree): el arte alfa está congelado y el arte nuevo debe salir de los bocetos, sin reciclar la base alfa. No abrir Unity ni Blender, ni hacer builds, sin un turno asignado por Director. Todo asset nuevo con su .meta. Los clips nuevos sobre el rig alfa sirven para validar sistemas, no como arte final.
- La base humana nueva de Higgsfield (HF_Human_Default, controller piloto sólo con Idle y Walk, citada en HumanMotionSource01) NO está en esta rama. Cualquier rig nuevo debe cumplir el mismo contrato: MotionBinding completo, anchors CameraEye/AimChest/HandGrip_L/R/ToolSocket_R/Foot_L/R y VisualAttentionContract.

### Brechas frente a bocetos

- Expresiones del mosquito (PER-07: Neutral, Happy, Angry, Alert, Sleepy, Confused, Focused, Excited; cejas: None, Angry, Curious, Worried, Confident, Surprised, Tired, Sneaky; PER-03: Neutral, Angry, Alert, Surprised, Focused). Hoy sólo hay mirada y parpadeo. El rig tiene LidUpper/LidLower/Pupil por ojo, pero no cejas ni eje de inclinación de párpado.
- Expresiones humanas (PER-02: Neutral, Happy, Angry, Surprised). Brow y Jaw existen pero sólo se mueven en Swat, Hit y Fall. No hay blendshapes de sonrisa, boca en O ni párpado inferior.
- Pose de vuelo del boceto (PER-03 'In flight' y 'Attack lunge'): cuerpo inclinado hacia delante, patas colgando hacia atrás y alas con blur. En juego el cuerpo va siempre nivelado (yaw puro), sin alabeo, sin estocada al preparar la picadura (BiteStart mantiene el tórax fijo) y con alas rígidas de un hueso sin blur.
- El aleteo varía entre 2,6 y 18,75 Hz según la distancia recorrida, y en Hover pasa a 7,5 Hz: parece a cámara lenta al volar despacio.
- Humano relajado del boceto (brazos algo flexionados, peso cómico). La marcha tiene la cadera 71,6 mm baja, la rodilla de apoyo a 52°, codo fijo de ~168° sin Shoulder animado ni transferencia lateral (HumanMotionSource01). El golpe deja el brazo a 177,7°.
- Vida en reposo (A18 mosquito atento, tema de sueño humano). Idle humano: sólo respiración de 0,014 rad y mandíbula. Idle del mosquito: abdomen ±0,02 rad. No hay bostezos, variaciones ni ajustes de patas, y el mosquito no tiene antenas en el rig.
- Movimiento secundario de gorro, pijama y accesorios (A17 moderado; mochilas y gorras de PER-02). No existe: las partes socket son rígidas al anchor.
- Squash & stretch y anticipación (estilo cómico): inexistentes. Además la anticipación del Jump ocurre en el aire y el aterrizaje (Land) nunca se reproduce.
- Caídas con peso (A15 caída dirigida con ajuste físico): sólo hay clips fijos, sin ajuste al suelo inclinado ni escalón. La física R4 del mosquito no está integrada y el humano no tiene módulo físico. Recovering de 0,4 s corta los clips Recover.
- Vista 3D giratoria de personalización (PER-08, CharacterPreviewOrbit): el personaje queda en el estado por defecto (Idle). El mosquito aparece con las alas plegadas, no desplegadas hacia arriba como el 'Base Mosquito' del boceto, y no hay animación de 'probar' la pieza elegida.
- Sala (lobby): sólo Idle/Walk con zancada supuesta de 1,2 m. El clip Walk nuevo cubre 0,97 m/ciclo y el movimiento es de 3,1 m/s con límite de speed 2,5, así que hay un deslizamiento notable de pies (LobbyVisualPresenter 12, 122-135; LobbyMovementRuntime:217).
- Menú: MainMenuLivingScene (humano sentado que golpea al mosquito, con alabeo de vuelo) es la pieza más cercana al tono de los bocetos. Su lógica de inclinación y reacción es la referencia a reutilizar en el juego.

### Riesgos

- Pops y cortes nuevos si se añaden estados sin resolver antes el intercambio runtimeAnimatorController=null de HumanLocomotionPresenter: cada adquisición reinicia la máquina en Idle. Es un riesgo alto de 'marcha rígida' aunque los clips mejoren.
- Squash o escala sobre huesos: rompe CaptureAnimatedPose de R4 (excepción en ValidateAnimatedScale), puede mover ProboscisTip y disparar LMS_BITE_VISUAL_RESIDUAL, y mueve la cámara de primera persona (Socket.Eye) y el pivot del mosquito (Socket.CameraTarget). Escalar VisualRoot invalida la personalización modular (CharacterModularVisualAssembler:180).
- Cambiar rig o FBX (cejas y antenas del mosquito, blendshapes humanos, huesos de gorro) invalida VisualAttentionContract.SourceSha256 y los asserts de conteo (65/39). Si no se recertifica, la cara queda desinstalada en silencio (sólo un warning LMS_FACIAL_SKIPPED).
- Editar LMS_*.controller a mano se pierde al ejecutar 'Let Me Sleep/Content/Build Characters'. Capas, máscaras y blend trees deben ir en CharacterContentBuilder.
- Desfase visual contra autoridad: alargar la recuperación visual, o la inclinación y el retraso de yaw, no debe cambiar dónde se puede golpear. Las superficies de golpe (GameplayActorProxy) siguen el snapshot y los 0,72 m de alcance del golpe superan en ~0,22 m el brazo visual (HumanMotionSource01). Reducir ese alcance es una decisión de balance, no de animación.
- Mareo y accesibilidad: bob, alabeo y sacudidas deben respetar P15 (controles por efecto) y ReducedMotion. Hoy sólo existe ReduceMenuMotion.
- Rendimiento: un PlayableGraph por actor (hasta 16), más springs e IK de pies con raycasts. Aceptable, pero hay que medirlo; no se puede afirmar FPS sin captura.
- Posibles escalones a 30 Hz en actores locales fuera de la marcha (ActorVisualBinding 211-214 escribe el snapshot sin interpolar). Hay que verificarlo antes de añadir secundarios que lo hagan más visible.
- Invertir mucho en clips sobre el rig alfa congelado puede ser trabajo desechable (AGENTS.md: arte nuevo desde bocetos). Conviene priorizar sistemas portables (grafo, capas, humor, secundario, IK) con contrato semántico de huesos (Hips, UpperLeg, Shoulder, UpperArm, etc.), como propone HumanMotionSource01 (87).
- Integrar R4 (física del mosquito) sigue bloqueado por el test de separación de articulaciones y por los 3 bloqueadores del prototipo con tórax guiado. No activar réplica ni simulación por cliente sin gates.

### Verificación

Sólo lectura, sin ejecutar Unity ni Blender. Leí el código completo de:
- ActorVisualBinding, HumanLocomotionPresenter/Clock/Setup, GameplayVisualPresenter, CharacterView, VisualAttentionRig/Factory/Contract, GameplayAttentionTarget.
- MosquitoFollowCamera, MosquitoRagdollSimulation/Pose/Builder (parcial), MainMenuLivingScene, LobbyVisualPresenter (parcial), HumanViewCamera.
- GameplayAuthority (movimiento, estados y recuperación), GameplayActorProxy.PoseLimbs, StrikeVisualTrajectory, Contracts.cs (ActorSnapshot, LifeState, GameplayEventKind).
También inspeccioné los YAML de LMS_Human.controller y LMS_Mosquito.controller (sin objetos BlendTree ni transiciones, parámetro Int Motion, m_IKPass 0) y los .fbx.meta (clips, rangos de frames, loopTime, animationType Generic). Revisé los prefabs: overrides del Animator (CullingMode 0 = AlwaysAnimate), VisualAttentionContract (límites, párpados por huesos o blendshapes) y VisualRoot a escala 0,5. De BuildReceipt.json saqué huesos, jerarquía, motions y anchors. En Blender fuente leí author_motion.py, author_mosquito_motion.py, author_human_locomotion.py y human_locomotion_contract.py, y confirmé con build_characters.py:338-348 qué script genera cada especie. Cotejé con los documentos pedidos, las decisiones A13-A20, los informes de Validation/V020 (CharacterMotionAudit01, HumanMotionSource01, HumanArmPose01, MosquitoFallIntegrationDraft) y los bocetos PER-02, PER-03 y PER-07 (vistos como imagen).

Los defectos marcados (salto → clip de desmayo, patinaje al golpear o agacharse, aleteo por distancia, corte de Recover, Stunned → pose de Hit, cortes al entrar y salir de la marcha) se deducen del flujo de código con las líneas citadas. No hay captura que los confirme visualmente; el de escalones de 30 Hz en el actor local es sólo una sospecha.

Para verificar tras implementar (requiere un turno de Director):
- PlayMode: Unity.exe -batchmode -projectPath N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity -runTests -testPlatform PlayMode -testFilter "HumanLocomotionTransitionTests|MosquitoRagdollPlayModeProof|CharacterModularVisualAssemblerPlayModeTests" -testResults <ruta en N:/LetMeSleep/Validation/V030/...>
- EditMode nuevos para GameplayMoodPolicy y para la velocidad de aleteo.
- Capturas de 24 fases frente/perfil con los arneses existentes N:/LetMeSleep/Validation/V020/HumanMotionVisual01/run-capture.ps1 (HumanMotionVisualCapture.cs) y HumanArmPose01 (HumanArmPoseDiagnostic.cs, RUN-RECIPE.md).
- Aplicar los criterios de HumanMotionSource01 (rodilla de apoyo 10-25°, codo ≤165° y variación ≥8° al caminar, residuo del golpe) y de VISUAL-MOTION-CORRECTIONS (secuencias idle→marcha→carrera→giro→golpe→retorno; vuelo→posado→picadura→salida; caída en suelo, rampa y escalón).
- Cargar el recibo de LMS_BITE_CONTACT y exigir finalResidual ≤0,001.

## maps

Los cinco mapas (Isla, Casa, Campamento, Yate, Puerto) son prefabs que salen de FBX de Blender. Se importaron una vez con HiggsfieldEnvironmentImporter. En tiempo de ejecución, AlfaApplication.LoadMap los instancia dentro de una sola escena, LetMeSleepHiggsfield.unity. La iluminación la aplica HiggsfieldMapLighting.Bind a partir del catálogo HiggsfieldFiveMaps.asset: una luna/sol direccional, ambiente Trilight, un skybox Procedural por mapa y de 0 a 23 luces puntuales ancladas a nodos del FBX.

Qué falta hoy respecto de los bocetos ENV-03/ENV-04:
- Ningún mapa tiene niebla: FogEnabled=0 en los cinco.
- No hay post-proceso en juego. El VolumeProfile del catálogo es nulo en los cinco, así que el Volume global se apaga. Además, la PlayerCamera de LMS_GameplayPresentation.prefab no tiene UniversalAdditionalCameraData, y por defecto URP la renderiza con renderPostProcessing=false: sin tonemapping, sin bloom, sin gradación de color y sin AA.
- Hay un bug en AlfaPresentationBuilder: AlfaGlobalVolume.asset quedó con 5 componentes nulos.
- La emisión HDR de faroles y fuego (2 a 4,5) nunca produce halo.
- Las paletas vienen de muestras lineales poco saturadas.
- Los cielos nocturnos son casi negros (exposición 0,08 a 0,1).
- Los faroles son débiles (intensidad 2,2 a 2,5, alcance 5 a 6 m).
- En Casa, 9 luces con sombra fuerzan a URP a reducir el atlas ×4.

Mayor salto con riesgo cero para colisiones y navegación, en este orden:
1. Activar el post-proceso en la cámara de juego y asignar un VolumeProfile por mapa: Bloom, Tonemapping Neutral, ColorAdjustments con saturación +10 a +20, sombras azules y luces cálidas, Vignette y SMAA. La plomería ya existe en Configuration.VolumeProfile.
2. Niebla por mapa, junto con la corrección del stripping de variantes de fog (GraphicsSettings m_FogStripping=0 las elimina en build) y UseFog en el agua GPU.
3. Ajustar ambiente, luna, faroles, emisión de ventanas y lámparas, y reducir las luces con sombra.
4. Retocar _BaseColor de los materiales Color_NNN.mat.
5. Agregar props decorativos con un prefab Decor por mapa, sin colliders, instanciado como hijo del mapa en LoadMap.

Cualquier Collider bajo MapRoot pasa a ser geometría de juego (IsWorldCollider): bloquea el movimiento, la cámara, la recuperación y los objetivos. Por eso los props deben ser solo visuales. Reimportar geometría obliga a rehacer navegación, objetivos, límites, catálogo y escena.

### Cómo funciona

1) CARGA DE LOS CINCO MAPAS (prefabs instanciados, no escenas)
- Build Settings contiene solo Assets/Scenes/LetMeSleepHiggsfield.unity. Es una copia de LetMeSleepBoot.unity hecha por HiggsfieldBootstrapSceneInstaller, que asignó AlfaApplication.HiggsfieldMaps = Presentation/Generated/HiggsfieldFiveMaps.asset.
- AlfaApplication.Start (93-97) llena los selectores de entrenamiento y sala con catalog.Entries. HasTaskCatalog exige un GameplayObjectiveCatalog válido para el modo Tareas.
- Al entrar a jugar, PrepareGame (365-379) llama a LoadMap(true, mapId) (530-553), que hace en orden:
  - HiggsfieldMaps.Resolve(mapId) (534), que valida el catálogo completo;
  - LightingRig.UnbindHiggsfield() (535) y Destroy del mapa previo (536);
  - map = Instantiate(entry.Prefab.gameObject) (537), en la raíz de la escena, sin padre y con transform identidad;
  - LightingRig.BindHiggsfield(map.transform, HiggsfieldMaps.ResolveLighting(mapId, map)) (538).
- Después, en PrepareGame: game.World.MapRoot = map.transform (370), game.NavigationData = map.SpatialData (371), Instantiate(GameplayPresentationPrefab) (373) y BindCameraDistance (374). Este último usa Entry.CameraFarPlane, que hoy vale 0 en los cinco, así que queda el farPlane de 100 m del preset.
- BeginGame arma GameplayRoundConfig(map.MapId, map.ContentHash, doors, tools, objectives) (352). El ContentHash viaja en la red como identidad del mapa.
- Las escenas <id>/Scenes/<id>.unity son de autoría y revisión; en runtime no se cargan.
- Al volver al menú, LoadMap(false) instancia LobbyPrefab con AlfaLightingRig.BindMap (camino Alfa, anclas LightAnchor_).

2) ORIGEN (FBX → import)
El importador se invoca a mano:
-executeMethod LetMeSleep.Content.Editor.Higgsfield.HiggsfieldEnvironmentImporter.ImportFromCommandLine -higgsfieldRecipe <receta.json>
Pasos:
- Copia el FBX (verifica el SHA) a <Output>/<mapId>/Models/Environment.fbx.
- Configura el ModelImporter: escala 1, bakeAxisConversion, sin luces, cámaras ni collider automático, isReadable, normales importadas y materiales InPrefab.
- Instancia el FBX bajo un GameObject raíz con el nombre del mapId. El hijo del FBX se llama 'Environment', de ahí las rutas 'Environment/...'.
- Guarda Prefabs/<id>.prefab y Scenes/<id>.unity.
- Exige un mapId nuevo por revisión. Por eso existen isla-v2, camp-v2 y yate-v3.

Detalle por mapa:
| Mapa | ID / categoría | FBX fuente | Nodos | Materiales | Luces locales | Navegación | Objetivos |
|---|---|---|---|---|---|---|---|
| Isla | hf-isla-del-laguito-v2, Island(0), día | 01-isla/HF_MAP_01_isla_UNITY.fbx (sha 8ce74c4f…) | 327 solid, 228 decoration, 2 water + 1 foam (CPU) | 38, ninguno emisivo | 0 | isla-navigation-human-v020.json | 12 |
| Casa | hf-casa-del-patio-v1, House(1), noche | 02-casa/HF_MAP_02_casa_UNITY.fbx (sha f0586b70…) | 3313 solid, 351 decoration | 29; emisivos Color_010 LampGlass (2.3,0.92,0.17) y Color_027 WindowAmber (0.48,0.27,0.09) | 21 | casa-navigation-schema1.json | 10 |
| Camp | hf-campamento-pinar-v2, Camp(2), noche | 03-campamento/NormalsV2/HF_MAP_03_campamento_UNITY_V2.fbx (sha 3fd036eb…) | 501 solid, 920 decoration, 8 water + 7 foam (CPU) | 40; emisivos Color_006 Ember (4.5,1.44,0.27), Color_013 LanternGlass (2.2,1.34,0.48), Color_032-034 llamas | 23 | camp-navigation-schema1.json + revisiones camp-path-*.asset | 10 |
| Yate | hf-yate-a-la-deriva-v3, Yacht(3); PeriodFor=Sunset pero valores diurnos | 04-yate/NormalsV3/HF_MAP_04_yate_UNITY_V3.fbx (sha 21777458…) | 96 solid, 62 decoration, océano GPU + 1 foam | 23; YATE_Glass en BLEND | 6 | yate-navigation-human-v020.json + yate-swim-bulkhead-v1.asset | 10 |
| Puerto | hf-puerto-del-faro-v1, Port(4), crepúsculo | 05-pueblo/HF_MAP_05_pueblo_UNITY.fbx (sha aa4bb192…) | 458 solid, 270 decoration, océano GPU + foam | 36; emisivo Color_000 PDF05_Amber (2.1,0.97,0.18) | 21 | puerto-navigation-schema1.json + puerto-exterior-stair-bevel-v1.asset | 11 |

- Todas las recetas usan colorSpace 'linear': el importador escribe _BaseColor = color.gamma.
- El .blend editable de cada mapa está junto a su FBX (HF_MAP_0X_*.blend).

3) COLISIONES, NAVEGACIÓN, SPAWNS, OBJETIVOS Y LÍMITES
- Colisiones: solo los nodos 'solid' reciben MeshCollider no convexo con la malla de render, y además GameplaySurface (SurfaceId = firstSurfaceId 1000000 + i, CanPerch según la regla). La capa sale de recipe.collisionLayer, que es 'Default' en los cinco.
- Los nodos 'decoration' y 'foliage' no tienen collider.
- El agua no tiene collider: usa HiggsfieldLowPolyWater (CPU) o HiggsfieldGpuWaterBinding (GPU).
- UnityGameplayWorld trata como mundo cualquier collider bajo MapRoot (IsWorldCollider, 30-36; GeometryMask=~0). Lo usan el motor de personajes, los raycasts de herramientas y mordida, la recuperación (TrySafe), ApproachFree de objetivos y la colisión de la cámara del mosquito (SetCollisionFilter(IsWorldCollider) en GameplayVisualPresenter 225).
- Límites: HiggsfieldMapBoundaryInstaller agregó el hijo _LMS_ArtificialBoundaries_v1 con 5 BoxCollider invisibles, sin piso. También agregó GameplayRecoveryVolume en la raíz (SafetyBounds = PlayBounds, fall zones y puntos de recuperación).
- PlayBounds actuales (centro / extensión):
  - Isla: (0,6,0) / (54,10,49)
  - Casa: (0,5.5,0) / (25,6.5,22)
  - Camp: (2.5,4.75,0) / (52.5,5.25,40)
  - Yate: (0,6.025,0) / (7,5.975,18)
  - Puerto: (0,10,0) / (58,12,46)
- Navegación: no hay NavMesh de Unity; no aparece ninguna referencia en el código. SpatialData es un JSON schema 1 con zones (AABB), portals (centro, normal, ancho, alto, door) y stair. Los archivos v020 agregan human_zones, human_portals y human_routes (puntos). GameplayRuntime.cs:90 lo pasa a World.ConfigureModeMap, que crea GameplayBotNavigation.
- Spawns: EMPTYs del FBX listados en la receta. Cada mapa tiene 5 HumanSpawnPoints y 16 MosquitoSpawnPoints. AlfaApplication.SpawnPoint usa points[index % length].
- Tool pickups: no hay GameplayToolPickup ni GameplayDoor en los mapas Higgsfield.
- Objetivos: GameplayObjectiveCatalog en la raíz, instalado por V020GameplayObjectiveInstaller (ConfigureForEditor). Ejemplo: camp.clean.barrel, TargetPath Environment/CAMP_Barrel_01, RouteRegionId shelter_clearing.

4) ILUMINACIÓN
AlfaLightingRig.BindHiggsfield → HiggsfieldMapLighting.Bind hace lo siguiente:
- Guarda el estado previo y apaga lobbyFill, las luces de mapa previas y todas las luces direccionales dentro del mapa.
- Reutiliza Moon_MainDirectional como sol: SunColor, SunUnityIntensity, SunShadows=Soft, CullingMask ~(1<<30) y SunWorldRotation.
- Asigna RenderSettings.skybox, AmbientMode.Trilight con Sky/Equator/Ground, fog* y reflectionIntensity.
- Asigna Volume_Global.sharedProfile = VolumeProfile y lo habilita solo si no es null.
- Crea un GameObject 'Higgsfield_LocalLight' con un Light por cada LocalLight, en el ancla relativa, con bounceIntensity 0.
- Llama a DynamicGI.UpdateEnvironment().
- Todo es realtime: no hay lightmaps, light probes ni reflection probes.

Valores actuales del catálogo:
| Mapa | Sol/luna | Ambiente sky / equator / ground | Skybox (Procedural) |
|---|---|---|---|
| Isla | (1,0.94,0.82) × 1.05 | (0.6,0.72,0.85) / (0.45,0.5,0.55) / (0.28,0.3,0.26) | exp 1.3 |
| Casa | (0.5,0.65,1) × 0.1 | (0.05,0.08,0.14) / (0.03,0.045,0.075) / (0.015,0.02,0.035) | exp 0.1 |
| Camp | (0.55,0.7,1) × 0.08 | igual que Casa | exp 0.08 |
| Yate | (1,0.84,0.62) × 1.05 | igual que Isla | exp 1.3 |
| Puerto | (0.56,0.7,1) × 0.8 | (0.16,0.23,0.38) / (0.12,0.16,0.23) / (0.06,0.08,0.13) | exp 1.3, tint (0.16,0.23,0.38) |

- En los cinco: FogEnabled=0 (FogMode 3 = Exp2, densidad 0.01), VolumeProfile=null y CameraFarPlane=0.
- Rotación del sol: Isla, Casa y Camp comparten la misma, con unos 48° de elevación.

Luces locales (todas Point):
- Casa: 9 interiores de 3.5 / 7 m con sombra Soft; porche, trasera, galpón y 4 faroles de camino de 2.2 / 5 m sin sombra; chimenea (1,0.28,0.055) 2.2; 2 mesas de luz.
- Camp: LGT_Fire 5 / 8 m Soft y LGT_Fire_Outer 1.5 / 10; 12 faroles de 2.5 / 6 m; lookout azul; refugio y baño Soft; 6 carpas de 0.8 / 2 m.
- Yate: 6 prácticas interiores de 3.5 / 7 m Soft.
- Puerto: lente del faro 3.5 / 12 m; 4 rellenos interiores Soft; faroles de 2.2 / 5 m.

URP, sombras y atlas:
- Forward+, main shadow 2048 con 4 cascadas a 50 m, soft de calidad alta.
- Sombras de luces adicionales en un atlas de 2048. Las luces creadas con AddComponent quedan en el tier por defecto (High, 1024). Casa pide 9×6 = 54 caras y URP las reduce ×4 (unity.log:862).
- SSAO feature (0,4). HDR activo, MSAA apagado, espacio de color Linear.

5) POST-PROCESO Y NIEBLA HOY
- Cámara de juego: la PlayerCamera no tiene UniversalAdditionalCameraData serializado, y por defecto URP usa renderPostProcessing=false. En juego no hay tonemapping, bloom, gradación, vignette ni AA, aunque el Volume esté habilitado.
- Menú: solo el MenuCamera tiene post-proceso (AlfaBootstrapBuilder:34). Recibe los defaults del RP asset (SampleSceneProfile: Bloom 0.25, Vignette 0.2, Neutral), porque AlfaGlobalVolume.asset está vacío por el bug de GetOrAdd.
- Niebla: apagada en la escena y en el catálogo. Si se enciende, las variantes probablemente se eliminan en build (m_FogStripping 0 y la escena sin fog; URP usa multi_compile_fog). El agua GPU además exige UseFog=true.

6) MATERIALES Y SHADERS DEL ENTORNO
- URP/Lit sin texturas: _BaseColor plano, Metallic 0, Smoothness 0, opaco (salvo YATE_Glass en BLEND).
- Emisión plana HDR aplicada con SetVector en swatches concretos.
- Castshadows On, salvo agua y espuma.
- El aspecto facetado viene de las normales autoradas (NormalsV2/V3).
- Agua CPU (Isla, Camp, espumas): URP/Lit animado por HiggsfieldLowPolyWater.
- Agua GPU (Yate, Puerto): 'LetMeSleep/Higgsfield/FlatGpuWater', unlit, sin sombras y sin niebla por defecto.
- Skybox: Skybox/Procedural por mapa (Presentation/Generated/Materials/hf-<id>-Skybox.mat).
- El shader GradientSky existe pero ningún material lo usa.
- HiggsfieldStaticBatching existe y nadie lo invoca.

7) PLAN RECOMENDADO PARA ACERCARSE A ENV-03/ENV-04 (de mayor impacto y menor riesgo a menor)
Todos los valores numéricos son puntos de partida a ajustar con capturas.

P1. Post-proceso en juego (riesgo físico nulo)
- (a) Habilitar el post-proceso en la PlayerCamera desde Presentation, porque Bootstrap no referencia URP.
  - Opción 1: en HumanViewCamera.ApplyPreset (68-75) y MosquitoFollowCamera.ApplyPreset (~125-132), agregar:
    var d = controlledCamera.GetUniversalAdditionalCameraData(); d.renderPostProcessing = true; d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; d.dithering = true;
  - Opción 2: crear HiggsfieldCameraPostOverride, copiando el patrón de HiggsfieldCameraDistanceOverride, enlazarlo en PrepareGame después de la línea 374 y desenlazarlo en StopGame.
  - Opción 3: agregar esos datos en AlfaPresentationBuilder (499-503) y guardar solo ese prefab.
- (b) Crear un VolumeProfile por mapa en Presentation/Generated/Profiles/hf-<id>-Volume.asset. Cada componente se agrega con profile.Add<T>(true) seguido de AssetDatabase.AddObjectToAsset(c, profile). Asignarlo en Lighting.VolumeProfile con VolumeWeight 1; HiggsfieldMapLighting.Bind ya lo aplica.
- Base común:
  - Tonemapping Neutral.
  - Bloom con threshold 0.9-1.0, scatter 0.65 y HQ. Intensidad 0.25-0.35 de día y 0.5-0.7 de noche.
  - ColorAdjustments con saturación +15-20 de día y +10 de noche, contraste +8-10. PostExposure +0.2-0.4 de noche.
  - ShadowsMidtonesHighlights o SplitToning: sombras azuladas y altas luces ámbar (A23 C, guía §4).
  - Vignette 0.15-0.2. En Casa, WhiteBalance +5-8.
- (c) Corregir GetOrAdd en AlfaPresentationBuilder (602-607) para que el menú recupere su perfil.

P2. Niebla y cielo
- Activar FogEnabled por entrada y cambiar GraphicsSettings.m_FogStripping a 1 (Custom, conservando Linear, Exp y Exp2), o bien encender fog en los RenderSettings de la escena.
- Valores sugeridos:
  - Isla: Linear 45→140, color (0.66,0.80,0.92), CameraFarPlane 150.
  - Casa: Linear 20→85, color (0.07,0.10,0.20).
  - Camp: Linear 15→75 o Exp2 0.02, color (0.06,0.10,0.20), para el 'bosque azul' de A27.
  - Puerto: Linear 35→110, color lavanda (0.55,0.50,0.68) (A24 A).
  - Yate: Linear 60→220, CameraFarPlane 250.
- FogEnd debe quedar por debajo del far plane efectivo, 100 m si CameraFarPlane=0.
- Para el agua GPU, llamar en runtime a HiggsfieldGpuWater.SetParameters(p con UseFog=true) después de BindHiggsfield, para no tocar el prefab.
- Cielos:
  - Nocturnos y Puerto: materiales nuevos con GradientSky, con horizonte igual al color de niebla y cenit navy o índigo (Casa y Camp (0.03,0.05,0.13); Puerto (0.20,0.22,0.45)).
  - Diurnos: mantener Procedural y subir la saturación del tint.

P3. Calidez nocturna
- Subir el ambiente de noche a sky (0.10,0.14,0.26), equator (0.10,0.10,0.16) y ground (0.06,0.05,0.06), con la luna en 0.15-0.2. Esto excede el rango de HiggsfieldNightCorrection (0.08-0.12), así que hace falta una herramienta nueva.
- Faroles a 3.5-4.5 y 7-8 m, sin sombra. Fogón de Camp a 7 y 12 m con parpadeo.
- Dejar 2-3 luces con sombra en Casa (Living, Kitchen, Foyer) y las demás en None, o bajar el tier con UniversalAdditionalLightData en Bind (146-155).
- Emisión:
  - Casa Color_027 WindowAmber, de 0.48 a ~1.5.
  - Isla Color_010 ISLA_Lamp: agregar emisión.
  - Yate: dar emisión a sus lámparas.
  - Se editan los .mat directamente, porque EmissionRepair está bloqueado.

P4. Paleta
- Editar _BaseColor de Color_NNN.mat: verdes más saturados, por ejemplo Camp Color_008 CAMP_Mat_Grass, hoy (0.48,0.63,0.51) sRGB; también maderas más cálidas.
- No toca geometría ni ContentHash.

P5. Props decorativos (detalle abajo en extension_points)
- Un prefab Decor por mapa, sin colliders, hijo del mapa instanciado.

P6. Solo en una etapa posterior: geometría nueva o props sólidos
- Implica reimportar con un mapId nuevo y rehacer navegación, objetivos, límites y recuperación, catálogo, escena y validaciones de recorridos (48/48, etc.).

### Archivos clave

- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/HiggsfieldMapCatalog.cs` — ScriptableObject del catálogo. Entry (líneas 28-39): MapId, DisplayName, CameraFarPlane, Prefab, Lighting, LocalLights y SuppressLightPaths. Validate (46-65), Resolve (67-73), ResolveLighting (81-89) y ResolveEntryLighting (91-145), que copia por reflexión todos los campos públicos de Configuration y resuelve rutas relativas exactas (146-167). Es el punto natural para agregar un campo Decor.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.cs` — Orquesta la carga. Catálogo hacia la UI (93-97). PrepareGame (365-379): LoadMap, luego World.MapRoot (370), NavigationData=SpatialData (371), instancia GameplayPresentationPrefab (373) y BindCameraDistance (374). Round config con MapId y ContentHash (352). MakeRoster y SpawnPoint (396-403). BindCameraDistance (491-502). LoadMap (530-553): destruye el mapa previo (536), Instantiate(entry.Prefab) (537) y BindHiggsfield (538).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Runtime/HiggsfieldMapLighting.cs` — Aplica la iluminación de un mapa. Enum Map y PeriodFor (12-24), Configuration (37-57), Validate (86-110). Bind (112-159): suprime luces direccionales importadas (133-134), convierte la luna en el sol (135-138), ambiente Trilight (139-142), niebla (143-144), Volume con sharedProfile y enabled=VolumeProfile!=null (145) y crea las luces locales Higgsfield_LocalLight (146-155). Unbind restaura todo (167-193).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Runtime/AlfaLightingRig.cs` — Dueño de moon, lobbyFill y globalVolume. BindHiggsfield (30-37) delega en HiggsfieldMapLighting. BindMap/ApplyAmbientProfile (87-140, 251-270) es el camino legado Alfa/lobby, con anclas LightAnchor_.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset` — Datos del catálogo instalado (guid 5a2287ce1168159439a0d2aa8486f5d8). Las entradas empiezan en las líneas 16 (Isla), 44 (Casa), 282 (Camp), 540 (Yate) y 628 (Puerto). En las cinco: FogEnabled 0, VolumeProfile {fileID: 0} y CameraFarPlane ausente (vale 0).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Generated/Prefabs/LMS_GameplayPresentation.prefab` — PlayerCamera (línea 135): ClearFlags Skybox, far 1000 que el preset sobrescribe a 100, HDR 1, MSAA permitido. No tiene UniversalAdditionalCameraData, así que corre sin post-proceso ni AA.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Generated/Prefabs/LMS_AlfaLightingRoot.prefab` — Contiene Moon_MainDirectional, Lobby_CharacterFill, las plantillas MapPointLight_* y Volume_Global (global, prioridad 0, sharedProfile AlfaGlobalVolume, línea 346).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Generated/Profiles/AlfaGlobalVolume.asset` — Está roto: la lista components tiene 5 referencias {fileID: 0}. Los overrides nunca se guardaron como sub-assets.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Editor/AlfaPresentationBuilder.cs` — BuildVolumeProfile (70-98) define ACES, contraste +4, saturación -3, WhiteBalance -5, Bloom 0,025 y Vignette 0,1. GetOrAdd (602-607) usa profile.Add<T>(true) sin AssetDatabase.AddObjectToAsset, y ese es el bug. BuildLightingPrefab arma Volume_Global (213-218). BuildGameplayPresentationPrefab (468+) crea PlayerCamera (499-503) sin datos URP.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Editor/Environment/Higgsfield/HiggsfieldEnvironmentImporter.cs` — Importador manual desde FBX y receta JSON. Ajustes del ModelImporter (79-94). Crea materiales URP/Lit planos Color_NNN (116-128) y agua CPU/GPU (171-194). Pone MeshCollider y GameplaySurface solo en nodos 'solid' (161-170). Arma EnvironmentMapDefinition con spawns desde EMPTYs, PlayBounds y SpatialData (199-216), no admite luces ni cámaras (217) y guarda Prefab y Scene (218-226). Nunca sobrescribe: exige un mapId nuevo por revisión (68).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Editor/Environment/Higgsfield/HiggsfieldImportContract.cs` — Contrato de receta: tipos de nodo solid/water/foam/foliage/decoration (50), rgb en 0..1 con colorSpace linear o srgb (60-61), agua cpu o gpu con amplitud de 0 a 0,15 m, emisión lineal y alpha OPAQUE o BLEND.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Editor/Environment/Higgsfield/HiggsfieldEmissionRepair.cs` — Repara solo la emisión en los .mat y cambia el ContentHash. Hoy no se puede usar: exige prefab.transform.childCount==1 (línea 107) y los prefabs ya tienen el nodo _LMS_ArtificialBoundaries_v1.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/HiggsfieldMapCatalogBuilder.cs` — -executeMethod LetMeSleep.Editor.HiggsfieldMapCatalogBuilder.BuildFromCommandLine -higgsfieldCatalogConfig <json>. Solo crea un catálogo nuevo (RequireNew, 108). Acepta skyboxAssetPath, volumeProfileAssetPath y sunEulerDegrees (134-140).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/HiggsfieldBootstrapSceneInstaller.cs` — Copia la escena fuente a una escena nueva y asigna AlfaApplication.HiggsfieldMaps (122-137). Exige 5 entradas, una por categoría Map 0..4, con SpatialData schema 1 (86-107).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/HiggsfieldNightCorrection.cs` — Corrección nocturna con guardas de GUID y SHA. Solo toca Casa v1 y Camp v2 (33), con luna entre 0,08 y 0,12 y exposición entre 0,08 y 0,15 (68-69). Edita SunUnityIntensity, Ambient* y las propiedades _Exposure/_SkyTint/_GroundColor de Skybox/Procedural (116-123). Sirve de plantilla para una herramienta de atmósfera más amplia.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/HiggsfieldMapBoundaryInstaller.cs` — Instaló en los cinco prefabs y escenas el nodo _LMS_ArtificialBoundaries_v1 con 5 BoxCollider (West/East/South/North/Ceiling, sin piso; 266-281), un GameplayRecoveryVolume en la raíz (533-544), PlayBounds y un ContentHash nuevo. InvariantRows (446-470) vigila todo el resto del prefab.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/HiggsfieldSkyboxMaterialBuilder.cs` — Crea copias nuevas de Skybox/Procedural (tint, ground y exposure). No soporta el shader degradado.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Runtime/HiggsfieldGradientSky.shader` — Shader de cielo 'LetMeSleep/Higgsfield/GradientSky' (_HorizonColor/_ZenithColor), pensado para que el horizonte coincida con la niebla. Ningún material lo usa hoy.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Presentation/Runtime/HiggsfieldWater/HiggsfieldGpuWater.cs` — Agua GPU sin iluminación (unlit), sin sombras y con UseFog opcional (18, 170). SetParameters() es público. La instalan HiggsfieldGpuWaterBinding en Yate (Water_Ocean, amplitud 0,14, prefab línea 5105 UseFog: 0) y en Puerto (Water_Ocean_Pueblo, amplitud 0,09, línea 21422 UseFog: 0).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Environment/HiggsfieldMaps` — Contenido de los mapas: <id>/Models/Environment.fbx, Materials/Color_NNN.mat (23 a 40 por mapa), Prefabs/<id>.prefab, Scenes/<id>.unity (escena de autoría, no se carga en runtime), Data/import-recipe.json, *-navigation-*.json (SpatialData) y revisiones técnicas (*.asset). También HiggsfieldLowPolyWater.cs (agua CPU) y HiggsfieldStaticBatching.cs (no se usa).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Environment/EnvironmentMapDefinition.cs` — Componente raíz del mapa: MapId, ContentHash, SpatialData, Human/Mosquito/Lobby spawns, ToolPickupPoints, PresentationAnchors, PlayBounds y GeometryContract.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Content/Environment/GameplayObjectiveCatalog.cs` — Objetivos del modo Tareas, en la raíz del prefab (10 a 12 por mapa): LocalPosition, LocalApproachPoint, UseRadius, RouteRegionId de la navegación y TargetPath a un nodo del FBX. Los instala Editor/ProjectBootstrap/V020GameplayObjectiveInstaller.cs.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Gameplay.Unity/UnityGameplayWorld.cs` — GeometryMask=~0 y MapRoot (12-13). IsWorldCollider (30-36) acepta cualquier collider bajo MapRoot. RegisterGeometry (38-52) registra GameplayDoor y GameplaySurface.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Gameplay.Unity/UnityGameplayWorld.Bounds.cs` — Recuperación por caída o fuera de límites. Lee GameplayRecoveryVolume de la raíz (49-64). TrySafe usa raycasts y overlaps contra toda la geometría del mundo (213-255).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Gameplay.Unity/UnityGameplayWorld.ModeRules.cs` — ConfigureModeMap: el JSON de navegación alimenta GameplayBotNavigation y se enlazan los objetivos (32-42). ValidateObjective incluye el chequeo físico ApproachFree (52-58).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Settings/PC_RPAsset.asset` — URP activo en Standalone (QualitySettings índice 1 'PC'). HDR 1, MSAA 1 (apagado), depth y opaque texture. Main light shadows 2048 con 4 cascadas a 50 m y soft de calidad alta. Luces adicionales con sombra, atlas 2048 y tiers 256/512/1024. SRP Batcher y light layers activos. m_VolumeProfile=SampleSceneProfile (línea 95), que es el perfil base de volumen.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Settings/PC_Renderer.asset` — m_RenderingMode 2 (Forward+, sin límite de luces por objeto) y feature ScreenSpaceAmbientOcclusion (intensidad 0,4, radio 0,3).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Settings/SampleSceneProfile.asset` — Perfil base del RP asset: Bloom (threshold 1, intensidad 0,25, scatter 0,5), Vignette 0,2 y Tonemapping Neutral. Solo actúa en cámaras con post-proceso activo, es decir, hoy solo en el MenuCamera.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/ProjectSettings/GraphicsSettings.asset` — m_FogStripping: 0 (automático, línea 47). Como LetMeSleepHiggsfield.unity tiene m_Fog: 0, la build probablemente descarta las variantes FOG_* aunque m_FogKeep*=1 (56-58). URP 17.3 usa multi_compile_fog (USE_DYNAMIC_BRANCH_FOG_KEYWORD=0).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Scenes/LetMeSleepHiggsfield.unity` — Única escena en Build Settings. RenderSettings sin niebla (línea 17) y skybox por defecto (29). MenuCamera con m_RenderPostProcessing: 1 (443). AlfaApplication.HiggsfieldMaps apunta al catálogo (588).
- `N:/LetMeSleep/Artifacts/Higgsfield/Mapas` — Fuentes Blender y FBX: 01-isla/HF_MAP_01_isla_UNITY.fbx, 02-casa/HF_MAP_02_casa_UNITY.fbx, 03-campamento/NormalsV2/HF_MAP_03_campamento_UNITY_V2.fbx, 04-yate/NormalsV3/HF_MAP_04_yate_UNITY_V3.fbx y 05-pueblo/HF_MAP_05_pueblo_UNITY.fbx, con sus .blend y checkpoints. Casa, Yate y Puerto tienen además UnityAdjustedSource.
- `N:/LetMeSleep/Validation/V020/FiveMapGameReview01` — Capturas base de juego (captures/<id>-Human|Mosquito.png), input.json y unity.log con la línea de comandos exacta. En la línea 862 aparece el aviso del atlas de sombras de Casa: 54 caras, reducido ×4.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/ceo/definicion-v020/respuestas-20260920-065440/DECISIONES.md` — Decisiones del usuario sobre ambiente (líneas 518-570). A23 C: interiores cálidos, exteriores con luna fría. A24 A: Puerto lavanda con luces cálidas. A25 A: Isla de vacaciones. A26 A: casa vivida y con rutas despejadas. A27 A: fogón central cálido, bosque azul y senderos claros. A30 A: detalle concentrado con rutas limpias. A31 A: el límite se lee en el entorno.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/v030/GUIA-ESTILO-BOCETOS.md` — Guía v0.3.0 sin commitear. Sección 4, líneas 121-131: saturación +10-20%, contraste suave, sombras azuladas y luces cálidas, bloom solo en fuentes de luz, niebla suave, más la lista de props de ENV-03/ENV-04.

### Puntos de extensión

- Catálogo: agregar `public GameObject Decor;` en HiggsfieldMapCatalog.Entry (Bootstrap/HiggsfieldMapCatalog.cs:28-39). Validarlo en Validate() (46-65): debe ser un asset, no una instancia de escena, y no puede contener Collider, Rigidbody, CharacterController, GameplaySurface, GameplayDoor, GameplayToolPickup, GameplayObjectiveCatalog, EnvironmentMapDefinition, GameplayRecoveryVolume, HiggsfieldLowPolyWater, HiggsfieldGpuWater, Camera, AudioListener ni luces direccionales. Las luces permitidas son Point o Spot, con Shadows=None y cullingMask sin la capa 30. Conviene fijar tope de renderers y de luces. Bootstrap ya referencia Content.Environment y Gameplay.Unity, así que puede hacer esos chequeos.
- Instanciar la decoración en AlfaApplication.LoadMap (Bootstrap/AlfaApplication.cs:537-538), entre Instantiate(entry.Prefab) y BindHiggsfield: `if (entry != null && entry.Decor) { var d = Instantiate(entry.Decor, map.transform, false); d.name = "_LMS_Decor"; }`. Como hijo del mapa se destruye junto con él (536). Al no tener colliders, IsWorldCollider nunca la ve. No se puede anclar luces del catálogo a la decoración: Validate() resuelve las rutas contra entry.Prefab, donde la decoración no existe. Las luces de la decoración van como componentes Light dentro del propio prefab Decor (Point, sin sombra, bounceIntensity 0); HiggsfieldMapLighting.Bind solo suprime las direccionales.
- Autoría de decoración, sin tocar los prefabs protegidos. Carpeta nueva Assets/LetMeSleep/Content/Environment/HiggsfieldDecor/<mapId>/ con <mapId>-Decor.prefab (raíz en identidad; hijos por zona, por ejemplo Decor_Interior_Living, Decor_PathEdges, Decor_BoundaryCues), Models/ (FBX de Blender importados con addCollider=false e importLights=false) y una escena <mapId>-DecorAuthoring.unity con el prefab del mapa en el origen, sin overrides aplicados, y el Decor como raíz hermana. Se pueden reutilizar mallas del FBX del mapa (sub-assets de <id>/Models/Environment.fbx, por ejemplo faroles o cajones de CAMP) y los materiales Color_NNN.mat del mismo mapa para mantener la paleta.
- Validador de decoración: herramienta nueva de editor, por ejemplo Editor/ProjectBootstrap/HiggsfieldDecorValidator.cs. Rechazar renderers cuyos bounds caigan dentro de: human_routes (radio 0,6 m, altura 0-2 m) y portals (centro, ancho y alto) del JSON SpatialData; 1 m alrededor de HumanSpawnPoints, MosquitoSpawnPoints y los spawns de GameplayRecoveryVolume; LocalPosition y LocalApproachPoint de GameplayObjectiveCatalog (UseRadius + 0,3 m), sin tapar el TargetPath. También exigir que cada prop se apoye sobre una superficie, con un raycast hacia abajo contra los MeshCollider del mapa y tolerancia de 2 cm. Esto implementa las decisiones A26 A y A30 A (rutas limpias, detalle concentrado).
- Señales visuales de límite (A31 A): cercas, rocas y arbustos sin collider, dispuestos justo dentro de PlayBounds, al pie de los muros invisibles de _LMS_ArtificialBoundaries_v1. Coordenadas internas: Camp x -50..55 / z -40..40; Isla ±54 / ±49; Casa ±25 / ±22; Yate ±7 / ±18; Puerto ±58 / ±46.
- Post-proceso de la cámara de juego (Presentation, que referencia Unity.RenderPipelines.Universal.Runtime). Tres lugares posibles: HumanViewCamera.ApplyPreset (Presentation/Runtime/HumanViewCamera.cs:68-75) y MosquitoFollowCamera.ApplyPreset (~125-132); un componente nuevo HiggsfieldCameraPostOverride enlazado desde AlfaApplication.PrepareGame después de BindCameraDistance (línea 374) y desenlazado en StopGame; o AlfaPresentationBuilder.BuildGameplayPresentationPrefab (Presentation/Editor/AlfaPresentationBuilder.cs:499-503) agregando UniversalAdditionalCameraData con renderPostProcessing, SMAA y dithering. Opcionalmente, un campo nuevo en AlfaPresentationPreset.
- VolumeProfile por mapa: HiggsfieldMapLighting.Configuration.VolumeProfile/VolumeWeight (HiggsfieldMapLighting.cs:51-52) ya se aplica en Bind (145). Solo faltan los assets y su asignación en el catálogo. Todo campo público nuevo en Configuration se propaga solo, porque ResolveEntryLighting copia por reflexión (HiggsfieldMapCatalog.cs:138-141). Ejemplos: parpadeo por luz, tier de sombra, un flag de post-proceso.
- Herramienta nueva de atmósfera, recomendada, a partir de HiggsfieldNightCorrection (Editor/ProjectBootstrap/HiggsfieldNightCorrection.cs): `-executeMethod LetMeSleep.Editor.HiggsfieldAtmosphereCorrection.ApplyFromCommandLine -higgsfieldAtmosphereConfig N:/.../atmosphere.json`. Verifica GUID y SHA del catálogo (guid 5a2287ce1168159439a0d2aa8486f5d8) y de los skies y perfiles. Crea los VolumeProfile con sub-assets. Escribe por entrada Lighting.Fog*, VolumeProfile, VolumeWeight, Ambient*, SunColor, SunUnityIntensity, SunWorldRotation, CameraFarPlane y LocalLights[].Settings (intensidad, alcance, sombras). Llama a catalog.Validate(), hace comparación invariante y guarda recibos fuera de Assets. HiggsfieldMapCatalogBuilder no sirve para esto: solo crea catálogos nuevos (RequireNew). Para prototipar alcanza con editar el asset en el Inspector.
- Luces locales en HiggsfieldMapLighting.Bind (146-155): se puede agregar `light.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier = ...Low/Medium` para evitar la reducción del atlas, y un componente de parpadeo para LGT_Fire, CASA_Fireplace_Practical_Anchor y la lente del faro. Alternativa global: PC_RPAsset m_AdditionalLightsShadowmapResolution 4096.
- Agua GPU: HiggsfieldGpuWater.SetParameters (HiggsfieldGpuWater.cs:91-99) permite activar UseFog en runtime, después de BindHiggsfield en LoadMap. Así no se edita el prefab (UseFog: 0 en el prefab de Yate línea 5105 y en el de Puerto línea 21422).
- Cielo: crear materiales de GradientSky (Presentation/Runtime/HiggsfieldGradientSky.shader, _HorizonColor = color de la niebla y _ZenithColor) y asignarlos en Lighting.Skybox. Se puede extender HiggsfieldSkyboxMaterialBuilder, que hoy solo copia Skybox/Procedural. Ojo: HiggsfieldNightCorrection exige Skybox/Procedural para Casa y Camp.
- Paleta y emisión sin tocar geometría: editar _BaseColor, _EmissionColor, la keyword _EMISSION y globalIlluminationFlags en <mapId>/Materials/Color_NNN.mat. El índice NNN es el orden de recipe.materials; por ejemplo Casa Color_027 = CASA_WindowAmber, Camp Color_008 = CAMP_Mat_Grass, Isla Color_010 = ISLA_Lamp. HiggsfieldEnvironmentImporter.ApplyEmission (280-299) se puede reutilizar desde un script de editor.

### Contratos y restricciones

- HiggsfieldMapCatalog.Validate: MapId único, sin espacios y no vacío; DisplayName obligatorio; Prefab como asset con el mismo MapId y ContentHash no vacío; el ID de Isla requiere la categoría Island; CameraFarPlane 0 o entre 10 y 1000; Lighting.LocalLights y SuppressLights deben ser arrays vacíos (solo valen los bindings relativos); hasta 64 luces locales Point o Spot sin anclas duplicadas; rutas con nombres exactos, separador '/' y '.' para la raíz; FogEnd > FogStart; VolumeWeight entre 0 y 1; rotación del sol normalizada; colores e intensidades finitos y no negativos.
- HiggsfieldBootstrapSceneInstaller exige exactamente 5 entradas, una por categoría Map 0..4, cada una con SpatialData schema_version 1 y su map_id. HiggsfieldMapCatalogBuilder solo crea assets y recibos nuevos, nunca sobrescribe. El catálogo instalado es Presentation/Generated/HiggsfieldFiveMaps.asset.
- HiggsfieldMapLighting.Bind siempre fuerza AmbientMode.Trilight, suprime toda luz direccional bajo el mapa, reutiliza la luna del AlfaLightingRig como sol y deshabilita el Volume global cuando VolumeProfile es null. Unbind restaura RenderSettings, el Volume y las luces.
- Física de juego: GeometryMask=~0, así que las capas no aíslan nada. Cualquier Collider (incluso trigger, según la consulta) bajo MapRoot entra en IsWorldCollider y afecta el movimiento, la mordida, las herramientas, la recuperación (TrySafe), ApproachFree de objetivos y la cámara del mosquito. La decoración debe tener cero colliders o quedar fuera de MapRoot.
- Importador: un mapId nuevo por revisión (la salida existente no se sobrescribe); colliders solo en 'solid'; el FBX no puede traer luces, cámaras, Rigidbody ni colliders; normales autoradas; spawns como EMPTY dentro de PlayBounds y separados más de 0,1 m; agua con amplitud entre 0 y 0,15 m; materiales solo URP/Lit planos.
- BoundaryInstaller y NightCorrection trabajan con guardas de SHA, GUID y hash de dependencias sobre catálogo, prefabs, escenas y skies. Cualquier edición deja obsoletas las configs y recibos anteriores y exige configs y recibos nuevos. NightCorrection solo admite Casa v1 y Camp v2, luna entre 0,08 y 0,12, exposición entre 0,08 y 0,15 y skybox Skybox/Procedural. Los guardas de Higgsfield/Integration/MapBounds/bounds-approved-preflight.json ya no coinciden con el árbol actual.
- HiggsfieldEmissionRepair exige prefab.transform.childCount == 1 (línea 107) y hoy falla porque existe _LMS_ArtificialBoundaries_v1. Además cambia el ContentHash del prefab y exige que _BaseColor coincida con la receta. Si se editan los _BaseColor a mano, esta herramienta quedará bloqueada en el futuro.
- Online: GameplayRoundConfig incluye map.MapId y map.ContentHash (AlfaApplication.cs:352). La decoración y la iluminación fuera del prefab no alteran el ContentHash, lo cual es correcto mientras sean solo visuales y todos los pares usen la misma build.
- URP: Forward+ (sin límite de 4 luces por objeto), SRP Batcher, HDR, MSAA apagado, SSAO siempre activo; sombras de luces adicionales en atlas 2048 con tier High por defecto (1024 por cara); espacio Linear; m_StripUnusedPostProcessingVariants=1 y m_FogStripping=0 (automático). Standalone usa la calidad 'PC' (PC_RPAsset).
- Cámara: el farPlane del preset es 100 m (AlfaPresentationPreset.asset) salvo que Entry.CameraFarPlane lo sobrescriba (HiggsfieldCameraDistanceOverride acepta de 10 a 1000). La niebla debe cerrar antes del far plane para ocultar el recorte.
- GameplayObjectiveCatalog: hasta 24 entradas, WorkTicks + RouteBudgetTicks ≤ 450 y RouteRegionId existente en la navegación. ValidateObjective exige ApproachFree físico en el punto de aproximación.
- Decisiones del usuario vigentes: A23 C (interior cálido, exterior con luna fría), A24 A (Puerto lavanda con luces cálidas), A25 A (Isla de vacaciones), A26 A (casa vivida con rutas despejadas), A27 A (fogón central cálido, bosque azul, senderos claros), A28 A (Yate vacacional sencillo), A29 A (taller reconocible), A30 A (detalle concentrado, rutas limpias) y A31 A (límite comunicado por el entorno).

### Brechas frente a bocetos

- Sin bloom ni tonemapping en juego: los faroles, las llamas y el ámbar de las ventanas (emisión HDR de 2 a 4,5) se ven como manchas planas saturadas, sin el halo de ENV-03/ENV-04. La causa es que la PlayerCamera no tiene post-proceso y el VolumeProfile del catálogo es null.
- Sin gradación de color: las muestras están en espacio lineal y resultan apagadas. El pasto de Camp se ve verde salvia (0.48,0.63,0.51) sRGB, las paredes de Casa gris azulado y Puerto gris-azul lavado, frente a los verdes, azules de lago y maderas cálidas saturadas de los bocetos.
- Sin niebla ni perspectiva aérea: los bocetos exteriores muestran bosque y montañas de fondo con bruma azul. Hoy no hay fog, y con un far plane de 100 m sin niebla, lo lejano se recorta de golpe.
- Cielos: de noche el Procedural con exposición 0,08-0,1 queda casi negro, frente al azul marino de la guía. De día no hay nubes (ENV-04 las tiene). Puerto no se lee como crepúsculo lavanda (A24 A), sino como cielo azul diurno.
- Luz local débil: faroles de 2,2-2,5 con 5-6 m no forman los charcos de luz cálida sobre el suelo de ENV-04 ('Campsite', 'Lake & Dock'). La ventana de Casa (emisión 0,48) no llega al umbral de bloom. Isla y Yate no tienen ningún material emisivo.
- Contraste nocturno: la revisión de cinco mapas marca el mosquito poco contrastado frente a árboles y cielo oscuro en Casa y Camp.
- Aliasing: sin AA (MSAA apagado, SMAA inalcanzable sin post-proceso), los bordes low-poly no quedan tan limpios como en el boceto ('clean').
- Densidad de props, deducida por nombres de nodos de las recetas y sin auditoría visual completa: ningún mapa tiene flores. Faltan buzón, bicicleta, caja de herramientas roja, cascada, cuadros y plantas en maceta en exteriores. Isla tiene 1 alfombra y 2 faroles. Yate casi no tiene accesorios (2 'Lamp', 3 'Frame'). Casa tiene alfombras (5), cortinas (70), libros (40) y chimenea; su densidad frente a ENV-03 (cacerolas colgadas, frascos, barriles, cofres, farol en la mesa) requiere revisión con capturas.
- Interiores de ENV-03: la luz diagonal de ventana y los rayos de sol no existen. Casa es nocturna por diseño; se podrían simular haces con quads aditivos en la cabaña de Isla, que es de día.
- Fuego estático: las llamas de Camp y la chimenea de Casa no parpadean, ni en malla ni en luz.
- Solapamientos y parpadeo: confirmado en Puerto (terreno sobre piso, faro). Hay pendientes en Camp, Isla, Casa y Yate (MAP-RENDER-OVERLAPS-20260920.md). Hasta corregirlo, degrada el acabado 'clean'.

### Riesgos

- Niebla que no llega a la build: GraphicsSettings m_FogStripping=0 (automático) y la única escena tiene m_Fog 0. Con multi_compile_fog (USE_DYNAMIC_BRANCH_FOG_KEYWORD=0), las variantes FOG_* probablemente se eliminan: la niebla se vería en el Editor pero no en el Player. Pasar a Custom (m_FogStripping 1; los flags Keep* ya están en 1) y verificar en una build.
- Océano GPU sin niebla: si se activa fog pero no UseFog en Yate y Puerto, el océano unlit queda nítido y brillante frente a un entorno con bruma. Además, al ser unlit, no responde a luces ni ambiente de noche o crepúsculo.
- El tonemapping cambia la exposición y el tono de todo, incluidos los personajes y la UI 3D de previsualización si comparte cámara. ACES oscurece los medios tonos, y Neutral es más predecible. La emisión HDR de hasta 4,5 (Ember de Camp) puede generar halos excesivos: ajustar threshold e intensidad por mapa. SampleSceneProfile se aplicará también al mapa Alfa (casa legado) apenas se active el post-proceso en la cámara de juego.
- Una nube o bruma más clara puede empeorar la silueta del mosquito de noche (problema ya señalado en Casa y Camp). Revisar las capturas Mosquito de cada mapa.
- La decoración con cualquier Collider bajo MapRoot se vuelve geometría de juego (motor, cámara, recuperación, ApproachFree de objetivos, raycasts de mordida y herramientas). Ese es el mayor riesgo de romper colisiones y navegación sin tocar el prefab: hace falta validación automática de cero colliders.
- Decoración sin colisión sobre rutas o puertas: los jugadores y los bots la atraviesan visualmente (clipping) y rompe la lectura de rutas (A26/A30). Validar contra human_routes, portals, spawns y objetivos. Además, los mosquitos no pueden posarse en props sin GameplaySurface.
- Rendimiento: más luces sin sombra en Forward+ tienen costo moderado. Casa ya reduce su atlas de sombras ×4 (54 caras); agregar luces con sombra lo empeora. El post-proceso con SMAA suma aproximadamente 1-2 ms a 1080p (sin medir). La decoración sin static batching (HiggsfieldStaticBatching no se usa) suma draw calls, y el SRP Batcher mitiga.
- Procesos con guardas: editar HiggsfieldFiveMaps.asset, los skies, los materiales o los prefabs deja obsoletos los SHA de las configs y recibos de NightCorrection, BoundaryInstaller, V020MapPreflight y EmissionRepair. Hay que emitir recibos nuevos y no reusar configs viejas. No regenerar la escena con AlfaBootstrapBuilder, porque perdería el binding HiggsfieldMaps que dejó el instalador. Tampoco correr AlfaPresentationBuilder.Build completo sin revisar qué assets regenera.
- Editar _BaseColor a mano diverge de import-recipe.json: EmissionRepair y cualquier auditoría de 'base RGB = receta' fallarán. Documentar la nueva línea base de paleta.
- Reimportar geometría (props sólidos, fondo, cascada) exige un mapId nuevo y rehacer SpatialData, objetivos, límites y recuperación, catálogo, escena y recorridos, con el ContentHash cambiando en la red. Es un riesgo alto; dejarlo fuera del primer paso de v0.3.0.
- Algunas afirmaciones no están verificadas en nativo: el default de URP para una cámara sin datos adicionales, el perfil del RP asset como capa base, el tier de sombra por defecto (High, coherente con el log) y la eliminación de variantes. Confirmarlas en la primera corrida nativa.

### Verificación

Análisis solo de lectura, sin ejecutar Unity, Blender ni builds; git solo en modo lectura. Leí completos HiggsfieldMapCatalog.cs, HiggsfieldMapLighting.cs, AlfaLightingRig.cs, HiggsfieldEnvironmentImporter.cs, HiggsfieldImportContract.cs, los cuatro instaladores y correcciones Higgsfield*, UnityGameplayWorld.Bounds.cs y los tramos relevantes de AlfaApplication.cs, UnityGameplayWorld*.cs, AlfaPresentationBuilder.cs, HiggsfieldGpuWater*, GameplayObjectiveCatalog, EnvironmentMapDefinition y HiggsfieldEmissionRepair. Inspeccioné el YAML de HiggsfieldFiveMaps.asset (valores por mapa, extraídos con Python), los cinco prefabs (conteo de componentes por GUID de script, PlayBounds, SpatialData, UseFog), los materiales Color_NNN y Skybox, PC_RPAsset, PC_Renderer, SampleSceneProfile, DefaultVolumeProfile, AlfaGlobalVolume, GraphicsSettings, QualitySettings, EditorBuildSettings y LetMeSleepHiggsfield.unity. También las recetas import-recipe.json (FBX y SHA, tipos de nodo, paletas, emisión), los JSON de navegación, Fog.hlsl y ShaderConfig del paquete URP en PackageCache, los bocetos ENV-03/ENV-04 y cuatro capturas base de FiveMapGameReview01, además del unity.log de esa corrida (comando y aviso del atlas). Hay tres afirmaciones basadas en el comportamiento conocido de URP 17 que conviene confirmar en nativo: el default renderPostProcessing=false de la cámara sin datos URP, el perfil del RP asset como capa base de volumen y la eliminación automática de variantes de fog en build.

Verificación propuesta al implementar:
(1) Ejecutar la prueba de carga de cinco mapas y comparar las 10 capturas contra N:/LetMeSleep/Validation/V020/FiveMapGameReview01/captures: "N:/Unity/Editors/6000.3.24f1/Editor/Unity.exe" -batchmode -noaudio -force-d3d11 -projectPath N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity -runTests -testPlatform PlayMode -testFilter LetMeSleep.Tests.PlayMode.HiggsfieldMapLoadingPlayModeTests -higgsfieldGameReview <input.json con scene/output/expectedMapIds> --lms-validation-data <dir nuevo> -testResults <out>/results.xml -logFile <out>/unity.log. Camera.Render incluye el post-proceso si la cámara lo tiene activo.
(2) Extender esa prueba o una nueva con estas aserciones: RenderSettings.fog == entry.Lighting.FogEnabled; LightingRig.GlobalVolume.sharedProfile == entry.Lighting.VolumeProfile; la cámara con GetUniversalAdditionalCameraData().renderPostProcessing en true; el hijo _LMS_Decor con 0 Collider; y el número de Collider y GameplaySurface bajo MapRoot igual al del prefab.
(3) Prueba EditMode de HiggsfieldMapCatalog.Validate con Decor inválido (collider, luz direccional).
(4) Build de Windows (WindowsAlfaBuild) para confirmar que se ven la niebla y el bloom, que prueba que las variantes no se eliminaron.
(5) Como la física no cambia, no hace falta repetir los recorridos 48/48. Sí conviene un smoke de bots en el modo Tareas por mapa (ApproachFree) y revisar los avisos LMS_BOUNDS_RECOVERY.
(6) Medir el tiempo de frame antes y después (post-proceso con SMAA, luces más fuertes), y revisar en el log que desaparezca 'Reduced additional punctual light shadows'.

## toolchain

Cadena v0.2.0 reconstruida desde evidencia real (sólo lectura). (1) Tests: Unity en batchmode con `-runTests -testPlatform EditMode|PlayMode -testFilter <FQN;FQN> -testResults <xml> -logFile <log>`, SIN `-quit` (docs/ceo/TEAM.md:13-18). EditMode lleva `-nographics`; PlayMode lleva `-force-d3d11 --lms-validation-data <dir>`. Se lanza con Start-Process -WindowStyle Hidden y el gate es el XML: exit 0, result=Passed, failed=0, skipped=0. Cada invocación tarda unos 15-20 s con la Library caliente. (2) Capturas: se hacen como tests PlayMode externos (UiRedesignCapture.cs, GameplayVisualCaptureHarness.cs) que se copian temporalmente, con su .meta, a Assets/LetMeSleep/Tests/PlayMode y se borran en `finally`. Se activan con `-uiReviewOutput` o `-gameplayVisualCapture config.json`. El HUD se compone con una cámara Overlay en el stack URP más `ScriptableRenderContext.EmitGeometryForCamera`. También hay tests versionados con `-equipmentHudReview` y `-modularCustomizationUiEvidence`. No se usa `-executeMethod` para capturar. (3) Build: `Unity.exe -batchmode -quit -projectPath <wt>/unity -executeMethod LetMeSleep.Editor.WindowsAlfaBuild.BuildV020 -logFile ...`. Sale a `N:/LetMeSleep/Artifacts/0.2.0-<UTC yyyyMMdd-HHmmss>/Let-me-sleep.exe` con build-receipt.json. Es una build Development. Inyecta las credenciales EOS desde `N:/LetMeSleep/Private/eos.local.json` en `StreamingAssets/online.local.json`. Tarda unos 45-50 s. (4) Smoke: el propio exe con `--lms-probe-output/--lms-probe-mode/--lms-probe-map/--lms-validation-data`, que escribe player-probe.json y 3 PNG. (5) Empaquetado: `work/package-v020.ps1 -BuildDirectory <artefacto>` genera la carpeta, BUILD.json con releaseSeries=1, el ZIP, `.sha256.txt` y `Let-me-sleep-series-2.json`. (6) Publicación: prerelease `v0.2.0` en Sauri0/LetMeSleep, con tag ligero en 61f8b71 y 3 assets (antes 5; el launcher se retiró por Defender). No quedó script: se hizo con `gh` a mano, y `work/github05/Publish-Release.ps1` es del flujo 0.4.0 y no encaja. La versión 0.2.0 está en ProjectSettings.asset:150 más varios literales (WindowsAlfaBuild.cs:17,35,45,49-53; package-v020.ps1:3,9,18,30,41; docs/player/PRUEBA-V0.2.0.md; RoomSession.Protocol). La Library del worktree está lista y caliente: último uso en Build05 (2026-09-20 18:17Z), sin Temp ni lockfile, sin Unity.exe en ejecución y sin cambios de código desde entonces. No existe docs/ceo/V0.2.0-DELIVERY.md: la entrega consta en STATE.md:1-41 y en runs.jsonl:75.

### Cómo funciona

FLUJO REAL v0.2.0 (horas en UTC salvo que se diga otra cosa; los mtime de archivos están en hora local UTC-3):

A) Preparación de la fuente. El worktree N:/LetMeSleep/Worktrees/v020-candidate-20260920 (rama claude/v0.3.0, igual que codex/v0.2.0 en c178bba) se avanzó por fast-forward con BuildCandidateInputs03/advance.py. Ese script preserva y restaura dos reescrituras que Unity hace al abrir o ejecutar (unity/Assets/LetMeSleep/Bootstrap/Customization.renderTexture y unity/Assets/LetMeSleep/UI/Fonts/AtkinsonHyperlegible-Regular SDF.asset), hace `git merge --ff-only 61f8b71` y afirma que diff, diff --cached y untracked quedan vacíos. Hace falta porque WindowsAlfaBuild.SourceDirty() (WindowsAlfaBuild.cs:78-80) aborta si hay CUALQUIER modificación o untracked. La Library del worktree es una copia completa de la del aislado anterior (~2.44 GB, según BuildCandidateInputs02/REPORT.md).

B) Versión. PrepareV020 (WindowsAlfaBuild.cs:19-38) fija PlayerSettings.bundleVersion="0.2.0" y EditorBuildSettings, y ejecuta FacialContentBuilder.BuildAll. El resultado se commiteó a mano (57b7b21: ProjectSettings.asset:150 y EditorBuildSettings.asset). BuildCandidate rechaza compilar si bundleVersion no coincide (l.45-46).

C) Build (WindowsCandidateBuild04 y 05). Comando exacto (unity-build.log l.11-19):
N:\Unity\Editors\6000.3.24f1\Editor\Unity.exe -batchmode -quit -projectPath N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity -executeMethod LetMeSleep.Editor.WindowsAlfaBuild.BuildV020 -logFile N:/LetMeSleep/Validation/V020/WindowsCandidateBuild0X/unity-build.log
Se lanzó oculto, guardando pid.txt y context.json {visibleWindow:false, project, sourceCommit, purpose, utc}.
Build04: 17:19:43 a 17:20:31, unos 48 s, artefacto 0.2.0-20260920-172000.
Build05: 18:16:48 a 18:17:33, unos 45 s, artefacto 0.2.0-20260920-181701, sourceCommit 61f8b71, outputBytes 310579494.
Contenido del artefacto: Let-me-sleep.exe (667136 B), UnityPlayer.dll, Let-me-sleep_Data (con StreamingAssets/online.local.json y EOS/eos_windows_config.json), MonoBleedingEdge, D3D12, UnityCrashHandler64.exe, WinPixEventRuntime.dll, 'Let me sleep_BurstDebugInformation_DoNotShip', GUIA-DE-PRUEBA.md y build-receipt.json.
Las credenciales EOS (productId, sandboxId, deploymentId, clientId, clientSecret) salen de N:/LetMeSleep/Private/eos.local.json y quedan en el build. Para Build04 se escribió después verification.json (hashes del exe y de la guía, dependencias) sin abrir el exe.

D) Smoke del exe. WindowsCandidateSmoke04/run-smoke.ps1 ejecuta:
Start-Process <exe> '--lms-probe-output <case> --lms-probe-mode blood --lms-probe-map hf-casa-del-patio-v1 --lms-validation-data <case>/data -logFile <case>/player.log' -WindowStyle Hidden, con WaitForExit(180000).
El PASS exige exit 0, mapId y modeId correctos, y humanRuntime, mosquitoRuntime, humanStationary, returnedToMenu, onlineRoomCreated, onlineRoomLeft, humanGameplayVisible y mosquitoGameplayVisible en true, con runtimeErrorCount 0 y failure vacío. El script escribe execution.json (receipt_sha256; visual, performance, two_identity y wan en false).
Smoke04 (D3D12 por defecto): PNG reales de 1920x1080 (menu.png 644 KB), mediana 18,03 ms y p95 24,58 ms en 1138 frames.
Smoke05 (casa-blood y casa-blood-rendered) no dejó script. Su player.log muestra 'Forcing GfxDevice: Direct3D 11' y sus PNG son negros (27260 B cada uno), con frames de unos 0,34 ms: no renderizaba.
Smoke03 cubrió la matriz 15/15 con 5 mapas (hf-casa-del-patio-v1, hf-campamento-pinar-v2, hf-puerto-del-faro-v1, hf-isla-del-laguito-v2, hf-yate-a-la-deriva-v3) × blood, survival y tasks.

E) Tests en el editor. Plantilla (Validation/V020/run-appearance-channel-native-02.ps1):
-batchmode -projectPath <proj> -runTests -testPlatform EditMode|PlayMode -testFilter 'Ns.Clase[.Metodo];Ns.Clase2' -testResults <xml> -logFile <log>
Para EditMode se añade -nographics. Para PlayMode se añaden -force-d3d11 y --lms-validation-data <dir nuevo>. Nunca -quit.
El gate es exit 0, test-run result=Passed, failed=0 y skipped=0.
Duración por invocación: 13-19 s de arranque del editor más menos de 1 s a 6 s de tests (p. ej. appearance-channel-edit-02: Date 16:12:46Z, tests 16:12:59, fin del log 16:13:00).
Tamaño de la suite: 404 casos EditMode (suite raíz de appearance-channel-edit-02.xml) y unos 230 PlayMode. Varios PlayMode hacen Assert.Ignore si falta su argumento (-equipmentHudReview, -modularCustomizationUiEvidence, -higgsfieldGameReview, -higgsfieldReview, -customizationSaveEvidence, --lms-validation-data).
Casi toda la evidencia V020 se corrió contra N:/LetMeSleep/Repository/unity (checkout central codex/v0.2.0, hoy con WIP sucio), no contra el worktree. HumanMotionVisual01/run-capture.ps1 sí usa el worktree.

F) Capturas.
UI: UiRedesignReview01/run-review.ps1 comprueba que no haya Unity corriendo y que el destino esté libre. Copia UiRedesignCapture.cs y su .meta a <proj>/Assets/LetMeSleep/Tests/PlayMode/ y ejecuta:
-batchmode -projectPath <proj> -runTests -testPlatform PlayMode -testFilter 'LetMeSleep.Tests.PlayMode.UiRedesignCapture.MenuAndHudAtTwoResolutions;LetMeSleep.Tests.PlayMode.EquipmentHudVisualEvidenceTests.RealCanvasRendersEquipmentHudAt720And1080WithoutOverflow' -testResults <run>/results.xml -logFile <run>/unity.log -force-d3d11 --lms-validation-data <run>/data -uiReviewOutput <run>/captures -equipmentHudReview <run>/equipment
Borra el test en finally, tras comprobar el hash. Resultado: 2/2 en unos 5,5 s de test y alrededor de 1 min en total. Salen PNG menu-1920x1080.png, menu-1280x720.png, hud-human-* y *-control.png, más manifest.json.
Modular: -testFilter LetMeSleep.Tests.PlayMode.ModularCustomizationUiPlayModeTests.SyntheticModularCanvasShowsLongLabelsAndScrollAt720And1080 con -modularCustomizationUiEvidence <dir bajo N:/LetMeSleep/Validation/V020>.
Gameplay: GameplayVisualCapture01/run-gameplay-visual-capture.ps1 -AllowTemporaryProjectImport [-FullMatrix] (antes, -CompileOnly compila con dotnet msbuild sin Unity). El filtro es LetMeSleep.Tests.PlayMode.GameplayVisualCaptureHarness.CaptureFiveMapsFromHumanAndMosquitoWithRealHud (o CaptureHudSmokeOnYateHuman), con -gameplayVisualCapture <run>/config.json derivado de config.template.json (1920x1080, settleSeconds 1.5, readyTimeoutSeconds 10, expectedMapIds en orden Isla, Casa, Camp, Yate, Puerto). Full12 duró unos 23 s de test y produjo 10 PNG con sus *-hud-diagnostic.json y manifest.json.
Técnica común: la cámara Base renderiza con StandardRequest como control. Luego se añade una Camera Overlay temporal (renderType Overlay, cullingMask capa 31, la UI movida a la capa 31, Canvas en ScreenSpaceCamera apuntando a la Overlay) y se llama ScriptableRenderContext.EmitGeometryForCamera(overlay). La Base se renderiza de nuevo, se hace ReadPixels y se compara con el control (umbral: más de 1/1000 de píxeles cambiados). Por último se restaura todo.

G) Empaquetado.
pwsh -File work/package-v020.ps1 -BuildDirectory N:/LetMeSleep/Artifacts/0.2.0-20260920-181701
Salida en N:/LetMeSleep/Artifacts/v0.2.0/packages/20260920-181820-56e0c998/: Let-me-sleep-0.2.0-Windows/ (con BUILD.json de 330 archivos, releaseSeries 1), Let-me-sleep-0.2.0-Windows.zip (114868337 B, SHA256 3668a4cbe4142edea3d1bd7e79ba560412d4688e4b74de736c2ac4d864aed060), .zip.sha256.txt y Let-me-sleep-series-2.json (87 B). El script imprime el JSON que se guardó como AcceleratedDelivery01/package.json.

H) Publicación. El tag v0.2.0 es LIGERO y apunta a 61f8b71: git ls-remote no muestra ^{}, y localmente no está fetcheado.
Release: https://github.com/Sauri0/LetMeSleep/releases/tag/v0.2.0, prerelease, título 'Let me sleep v0.2.0 — versión de prueba', publicada a las 18:21:06Z.
Assets iniciales (release-before.json): ZIP, ZIP.sha256.txt, Let-me-sleep-Launcher.exe, Let-me-sleep-Launcher.exe.sha256.txt y Let-me-sleep-series-2.json. El launcher y su sha se retiraron después; hoy quedan 3 assets.
Notas: AcceleratedDelivery01/release-notes.md; luego se reemplazaron por LauncherDefender01/release-notes.md, con el aviso Defender al inicio.
No se versionó el comando gh. Coherente con los assets y flags: `gh release create v0.2.0 <assets> --repo Sauri0/LetMeSleep --verify-tag --prerelease --title ... --notes-file ...`, y después `gh release delete-asset` y `gh release edit --notes-file`.
Verificación pública: `work/updater-tests.exe --install-latest-no-launch N:/LetMeSleep/Validation/V020/AcceleratedDelivery01/downloaded-install` imprime LIVE_INSTALL checks=3 version=0.2.0 (download-verification.log). updater-tests.exe lo compila work/build-launcher.ps1.
Después se registró la entrega en docs/ceo/STATE.md y en docs/ceo/runs.jsonl (ticket V020-ACCELERATED-DELIVERY).

I) Estado de la Library del worktree: LISTA. unity/Library se escribió por última vez el 2026-09-20 a las 15:17 locales (18:17Z, Build05): ArtifactDB, SourceAssetDB, LastBuild.buildreport y PackageCache. ScriptAssemblies son de las 15:16 (LetMeSleep.Bootstrap.dll, Tests.PlayMode.dll). LastSceneManagerSetup.txt es de las 15:05 y contiene 'sceneSetups: []'. No existe unity/Temp (no hay lockfile) y tasklist no muestra Unity.exe. Entre 61f8b71 y HEAD c178bba sólo cambian docs/ceo/STATE.md y runs.jsonl, así que abrir el proyecto sólo refrescará sin reimportar. Ya existen los .csproj generados (LetMeSleep.Tests.PlayMode.csproj, LetMeSleep.Tests.EditMode.csproj y demás) y StreamingAssets/online.local.json con EOS/eos_windows_config.json (ignorados por git). dotnet 10.0.202 y gh 2.91.0 están instalados.

### Archivos clave

- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/WindowsAlfaBuild.cs` — Entrada -executeMethod del build. BuildV020() (l.16-17) llama a BuildCandidate("0.2.0","Assets/Scenes/LetMeSleepHiggsfield.unity"). PrepareV020() (l.19-38) ejecuta FacialContentBuilder.BuildAll, fija productName y bundleVersion="0.2.0" y deja una sola escena en EditorBuildSettings. BuildCandidate (l.40-75): lanza excepción si SourceDirty() (git diff, diff --cached o untracked no vacíos, l.78-80), exige bundleVersion==version y la guía docs/player/PRUEBA-V0.2.0.md, carga EOS desde N:/LetMeSleep/Private/eos.local.json y lo escribe en StreamingAssets/online.local.json (l.54-56), configura WindowsConfig de PlayEveryWare con DisableOverlay y NativePluginPolicy.Apply(). Salida N:/LetMeSleep/Artifacts/<version>-<UTC yyyyMMdd-HHmmss>/Let-me-sleep.exe con BuildOptions.Development, copia GUIA-DE-PRUEBA.md, escribe build-receipt.json {result,errors,unity,outputBytes,utc,sourceCommit,sourceDirty,version} y registra 'LMS_ALFA_BUILD <result> <dir>'.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/ProjectSettings/ProjectSettings.asset` — Fuente de Application.version: l.150 `bundleVersion: 0.2.0` (commit 57b7b21). productName 'Let me sleep' en l.16, companyName SaurioGames en l.15.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/ProjectSettings/EditorBuildSettings.asset` — Escena única del build: Assets/Scenes/LetMeSleepHiggsfield.unity (guid 60df67d16705fc64d9244c13fd7979e4).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.Probe.cs` — Smoke del player. Sólo corre si Debug.isDebugBuild, no está en el editor y existe --lms-probe-output (l.21-27). Fija 1920x1080 en ventana, espera --lms-probe-menu-seconds (2-30, por defecto 2) y guarda menu.png. Entrena Human y luego Mosquito en --lms-probe-mode (blood|survival|tasks) y --lms-probe-map durante 15 s cada rol, midiendo frames en los últimos 10 s, y guarda human.png y mosquito.png. Luego vuelve al menú, hace CreateRoom('Windows QA') por EOS (40 s de límite) y LeaveRoom. Escribe player-probe.json y termina con Quit(0|2) (l.43-169).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.cs` — DataPath (l.61-80): con --lms-validation-data <abs> en editor o DEVELOPMENT_BUILD aísla las preferencias. En el editor, sin ese argumento, usa N:/LetMeSleep/UserData/Unity. l.136 carga StreamingAssets/online.local.json.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/Core/RoomSession.cs` — l.93 `public const string Protocol = "lms-unity-020-4"`. Se usa como BucketId de lobby EOS (EosLobbySession.cs:59,179), en LobbyJoinPolicy.cs:25 y en el handshake (OnlineRoomCoordinator.cs:46). Hay que subirlo para que 0.2.0 y 0.3.0 no se mezclen. El test EditMode AlphaProtocolToolOwnershipTests.cs:16 fija el literal.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/LetMeSleep/UI/Runtime/AlfaUiController.cs` — l.834 muestra Application.version en la UI; se actualiza solo al cambiar bundleVersion. l.1254/1263/1314 crean los paneles 'RoleBadge', 'ClockBadge' y 'ContextHintPanel', de los que dependen por nombre los harness de captura.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/AlfaReviewCapture.cs` — Helper de editor Capture(path,w,h,includeUi), no apto para -executeMethod porque tiene parámetros. Pasa el Canvas a ScreenSpaceCamera con un único SingleCameraRequest. Es el enfoque que PERDÍA el HUD (GAMEPLAY-CAPTURE-20260920.md): sirve sólo para capturas de mundo con includeUi=false. Lo usan scripts de art_source/unity/environments/quality_exterior.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/CharacterRenderReview.cs` — `-executeMethod LetMeSleep.Editor.CharacterRenderReview.Capture` (sin argumentos; salida fija N:/LetMeSleep/Artifacts/review/alfa-characters). Renderiza LMS_Human y LMS_Mosquito desde Assets/LetMeSleep/Content/Characters/Prefabs/LMS_{species}.prefab en un estudio. Necesita dispositivo gráfico, así que no admite -nographics. Sirve para revisar personajes nuevos.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/V020ValidationRunner.cs` — Entradas -executeMethod para ensamblados de diagnóstico externos: RunExternalDiagnostic con -surfaceAssembly, -surfaceOutput, -surfaceMethod y -surfaceType; también RunSurfaceChecks y RunPuertoInitialOverlap.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/unity/Assets/Editor/ProjectBootstrap/V020MapPreflight.cs` — `-executeMethod LetMeSleep.Editor.V020MapPreflight.CaptureAndInspect -v020Evidence <dir nuevo absoluto>`: preflight de mapas guardado por hashes de bounds-approved-preflight.json.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/work/package-v020.ps1` — Empaquetado v0.2.0 (commit c751408). Valida build-receipt.json (Succeeded, errors 0, version '0.2.0', !sourceDirty, commit de 40 hex). Copia TODO el directorio del build a <OutputRoot>/<UTC yyyyMMdd-HHmmss>-<guid8>/Let-me-sleep-0.2.0-Windows/. Escribe BUILD.json {version, releaseSeries=1, executable, sourceCommit, unity, files{ruta:sha256}}, el ZIP (ZipFile.CreateFromDirectory, Optimal, includeBaseDirectory=true, límite 2 GiB), '<zip>.sha256.txt' ('<hash>  <nombre>') y Let-me-sleep-series-2.json {releaseSeries:1, firstVersion:'0.2.0', launcherMinimum:'1.2.0'}. Todo lleva 0.2.0 fijo en las l.3,9,18,30,41.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/launcher/Updater.cs` — Contrato del launcher 1.2.0. SeriesMarker='Let-me-sleep-series-2.json' (l.40). SelectRelease ordena primero por serie (marcador presente = 1) y luego por versión; incluye prereleases y excluye drafts (l.50-54). HasAssets exige exactamente un 'Let-me-sleep-<ver>-Windows.zip' (hasta 2 GiB) y su '.sha256.txt' (hasta 1 KiB); el marcador es opcional y debe ocupar entre 1 B y 1 KiB (l.64-72). ValidateInstallation exige que BUILD.json.releaseSeries coincida (l.153-166).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/launcher/UpdaterTests.cs` — Tests del updater. Modo vivo `updater-tests.exe --install-latest-no-launch <dir>` (l.29-37): descarga la última release real de GitHub, verifica checksum y manifiesto e imprime 'LIVE_INSTALL checks=3 game_launched=false version=X'.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/work/build-launcher.ps1` — Compila con csc de .NET Framework 4 el Let-me-sleep-Launcher.exe (en outputs/launcher-1.2.0 por defecto) y work/updater-tests.exe, y ejecuta los tests unitarios salvo con -SkipTests. Launcher.cs:62 contiene el pie 'INICIO 1.2.0'.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/work/github05/Publish-Release.ps1` — Script de publicación antiguo (0.4.0, por defecto MANIFIESTO-0.4.0.json). Exige 2 ZIP (Windows + fuentes) y el manifiesto con source_and_docs_commit; no crea tags. Hace gh release create --draft --verify-tag, descarga los assets y compara hashes, y termina con gh release edit --draft=false --latest. Es útil como plantilla, pero NO se usó para v0.2.0.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/ceo/TEAM.md` — l.13-18: regla de ejecutar Test Runner sin -quit, con XML de casos esperados y cero fallos u omitidos, logs y XML nuevos en cada intento, y no reabrir Unity si otro proceso sigue vivo.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/ceo/STATE.md` — l.1-41: registro de la publicación v0.2.0 (Build05, Smoke05, ZIP 114868337 B con SHA256 3668a4cb…, launcher 1.2.0 retirado por Defender). l.92-110 y l.327-370: historial de las candidatas Build01-04. No existe V0.2.0-DELIVERY.md.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/ceo/WINDOWS-CANDIDATE-04-20260920.md` — Resultado de Build04/Smoke04: fuente 4947f72, artefacto 0.2.0-20260920-172000, 310570306 B, Smoke04 casa-blood PASS; métricas y advertencias del log (atlas de sombras x4, Graphics Ring Buffer, faltan prefabs slipper/racket/spray).
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/ceo/GAMEPLAY-CAPTURE-20260920.md` — Historia de los intentos 01-12 del harness de captura de gameplay y de por qué hace falta la Overlay con EmitGeometryForCamera.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/ceo/UI-REDESIGN-FIRST-PASS-20260920.md` — Runs de UiRedesignReview01 (aceptado Run-20260920-180449-325, 2/2, 8 PNG) y del caso ModularVisual.
- `N:/LetMeSleep/Worktrees/v020-candidate-20260920/docs/player/PRUEBA-V0.2.0.md` — Guía de 97 líneas que el build copia como GUIA-DE-PRUEBA.md; BuildCandidate exige que exista.
- `N:/LetMeSleep/Validation/V020/WindowsCandidateSmoke04/run-smoke.ps1` — Script de smoke del exe: Start-Process con '--lms-probe-output <case> --lms-probe-mode blood --lms-probe-map hf-casa-del-patio-v1 --lms-validation-data <case>/data -logFile <case>/player.log', ventana oculta, 180 s de límite. El gate son los booleanos de player-probe.json; escribe execution.json con el SHA256 del recibo.
- `N:/LetMeSleep/Validation/V020/WindowsCandidateSmoke03/run-remaining-matrix.ps1` — Variante matriz 5 mapas × 3 modos (blood, survival, tasks) con los IDs canónicos de mapa. write-matrix.ps1 consolida verification-matrix.json.
- `N:/LetMeSleep/Validation/V020/GameplayVisualCapture01/run-gameplay-visual-capture.ps1` — Runner del harness de gameplay. -CompileOnly compila sin Unity vía dotnet msbuild del PlayMode csproj con /p:CustomAfterMicrosoftCommonTargets=IncludeHarness.targets y TargetFrameworkRootPath=MonoBleedingEdge/xbuild-frameworks. -AllowTemporaryProjectImport [-FullMatrix] copia el .cs y su .meta a Assets/LetMeSleep/Tests/PlayMode y ejecuta Unity -runTests con -testFilter CaptureHudSmokeOnYateHuman o CaptureFiveMapsFromHumanAndMosquitoWithRealHud, -force-d3d11 y -gameplayVisualCapture config.json. OJO: $project apunta a N:/LetMeSleep/Repository/unity (l.9).
- `N:/LetMeSleep/Validation/V020/GameplayVisualCapture01/GameplayVisualCaptureHarness.cs` — Harness (48600 B) que carga LetMeSleepHiggsfield y lee por reflexión los campos privados de AlfaApplication ui, game, presentation y map. Captura control y compuesto con Overlay + EmitGeometryForCamera (l.484) y verifica píxeles en RoleBadge, ClockBadge y ContextHintPanel (l.451-453). ValidationRoot fijo en N:/LetMeSleep/Validation/V020/GameplayVisualCapture01 (l.31, 867); mapas fijos en l.32-38.
- `N:/LetMeSleep/Validation/V020/UiRedesignReview01/run-review.ps1` — Runner de captura de menú y HUD a 1920x1080 y 1280x720: filtros UiRedesignCapture.MenuAndHudAtTwoResolutions y EquipmentHudVisualEvidenceTests.RealCanvasRendersEquipmentHudAt720And1080WithoutOverflow, argumentos -uiReviewOutput y -equipmentHudReview. Exige passed=2 y skipped=0. $project=N:/LetMeSleep/Repository/unity (l.3).
- `N:/LetMeSleep/Validation/V020/UiRedesignReview01/UiRedesignCapture.cs` — Test externo de captura de menú real (app.MenuCamera) y HUD humano en Yate. Overlay en stack + EmitGeometryForCamera (l.78-89); asserts de TMP sin overflow y con glifos visibles (l.96-101). La salida debe caer bajo N:\LetMeSleep\Validation\V020\UiRedesignReview01\ (l.33).
- `N:/LetMeSleep/Validation/V020/run-appearance-channel-native-02.ps1` — Plantilla canónica de tests nativos EditMode + PlayMode en serie (-nographics frente a -force-d3d11 --lms-validation-data), con gate XML y preservación de evidencia.
- `N:/LetMeSleep/Validation/V020/BuildCandidateInputs03/advance.py` — Receta para dejar limpio el worktree antes del build: preserva y restaura desde HEAD los reescritos de Unity (Bootstrap/Customization.renderTexture y UI/Fonts/AtkinsonHyperlegible-Regular SDF.asset), hace merge --ff-only y afirma que el árbol queda limpio.
- `N:/LetMeSleep/Validation/V020/WindowsCandidateBuild04/unity-build.log` — Log con la línea de comando exacta del build (l.11-19), 'Build Finished, Result: Success' (l.9795), PlayerBuildInfo 30,3 s (l.9796) y 'LMS_ALFA_BUILD Succeeded N:/LetMeSleep/Artifacts/0.2.0-20260920-172000' (l.9808). context.json y verification.json del mismo folder son el modelo de recibo y procedencia.
- `N:/LetMeSleep/Validation/V020/AcceleratedDelivery01` — package.json (ZIP y hash de la release), release-notes.md (texto usado en la release), download-verification.log (salida de LIVE_INSTALL 3/3) y downloaded-install/current.txt.
- `N:/LetMeSleep/Validation/V020/LauncherDefender01` — Incidente Defender Behavior:Win32/DefenseEvasion.A!ml sobre el launcher 1.2.0: detections.json, receipt.json, release-before.json (5 assets originales) y release-notes.md (notas editadas).
- `N:/LetMeSleep/Artifacts/v0.2.0/packages/20260920-181820-56e0c998` — Paquete publicado: Let-me-sleep-0.2.0-Windows.zip (114868337 B), .sha256.txt, Let-me-sleep-series-2.json y carpeta desempaquetada con BUILD.json (330 archivos).

### Puntos de extensión

- VERSIÓN 0.3.0, dónde subirla. 1) unity/ProjectSettings/ProjectSettings.asset:150 `bundleVersion: 0.3.0`: de ahí salen Application.version, la etiqueta de AlfaUiController.cs:834, EosConnection.cs:61 (ProductVersion) y el recibo del probe. 2) WindowsAlfaBuild.cs: añadir `public static void BuildV030() => BuildCandidate("0.3.0", "Assets/Scenes/LetMeSleepHiggsfield.unity");` y un PrepareV030 que escriba bundleVersion "0.3.0". Generalizar las l.45 (`version == "0.2.0"`) y l.49-53 para que la guía sea docs/player/PRUEBA-V0.3.0.md (por ejemplo, `$"docs/player/PRUEBA-V{version}.md"` y `PlayerSettings.bundleVersion != version` para cualquier versión 0.x). 3) Nuevo docs/player/PRUEBA-V0.3.0.md, que es obligatorio para el build. 4) work/package-v030.ps1, copia de package-v020.ps1 cambiando las l.3 (OutputRoot 'N:/LetMeSleep/Artifacts/v0.3.0/packages'), 9 (version -ne '0.3.0'), 18 ('Let-me-sleep-0.3.0-Windows') y 30 (version='0.3.0'). La l.41 se puede dejar igual: el launcher sólo valida nombre y tamaño del marcador; mantener releaseSeries=1. 5) RoomSession.cs:93 Protocol → 'lms-unity-030-1' y su test EditMode AlphaProtocolToolOwnershipTests.cs:16. Así los lobbies 0.2.0 y 0.3.0 no se cruzan, porque BucketId es igual al Protocol. 6) Las notas de release y la entrada en STATE.md y runs.jsonl.
- Para parametrizar la versión con un único punto: hacer que BuildCandidate lea PlayerSettings.bundleVersion y que package-vXXX.ps1 tome la versión de build-receipt.json (receipt.version) en lugar de literales.
- Excluir del ZIP en package-v030.ps1 lo que package-unity.ps1:113 ya excluía: build-receipt.json, '*_BurstDebugInformation_DoNotShip', '*_BackUpThisFolder_ButDontShipItWithYourGame' y *.pdb. Opcionalmente, conservar el chequeo de package-unity.ps1:105-107 (archivos requeridos, sin GfxPluginNativeRender-x64.dll).
- Capturas contra los bocetos. Copiar UiRedesignReview01 y GameplayVisualCapture01 a N:/LetMeSleep/Validation/V030/... y cambiar: $project al worktree (run-review.ps1:3; run-gameplay-visual-capture.ps1:9 y :61, que usa git -C del Repository); la constante ValidationRoot del harness (GameplayVisualCaptureHarness.cs:31) y el prefijo de UiRedesignCapture.cs:33; y las rutas <Compile Remove/Include> de IncludeHarness.targets. Añadir casos para pantallas que hoy no se capturan (sala/lobby, ajustes, selección de rol/mapa, personalización con vista 3D giratoria, resultados) usando los campos privados vía Field<T>("ui"|"game"|"presentation") y la misma técnica Overlay + EmitGeometryForCamera.
- Tests versionados ya disponibles para capturar UI sin harness externo: EquipmentHudVisualEvidenceTests.RealCanvasRendersEquipmentHudAt720And1080WithoutOverflow (-equipmentHudReview <dir>, sin restricción de ruta) y ModularCustomizationUiPlayModeTests.SyntheticModularCanvasShowsLongLabelsAndScrollAt720And1080 (-modularCustomizationUiEvidence <dir>, restringido a N:/LetMeSleep/Validation/V020 en ModularCustomizationUiPlayModeTests.cs:211-213; hay que ampliar la raíz si se quiere V030).
- Revisión de personajes nuevos: `-executeMethod LetMeSleep.Editor.CharacterRenderReview.Capture` renderiza LMS_Human y LMS_Mosquito desde Assets/LetMeSleep/Content/Characters/Prefabs/. Tiene la salida fija en N:/LetMeSleep/Artifacts/review/alfa-characters; conviene añadir una sobrecarga sin parámetros que lea un argumento -characterReviewOutput.
- Compilación rápida sin Unity (gate de CPU): `dotnet msbuild <wt>/unity/LetMeSleep.Tests.PlayMode.csproj /nologo /v:minimal /t:Build /consoleLoggerParameters:ErrorsOnly "/p:TargetFrameworkRootPath=N:/Unity/Editors/6000.3.24f1/Editor/Data/MonoBleedingEdge/lib/mono/xbuild-frameworks/"`, tomado de run-gameplay-visual-capture.ps1:26-28. Con /p:CustomAfterMicrosoftCommonTargets=<targets> incluye .cs externos.
- Smoke: parametrizar run-smoke.ps1 ($exe, $outputRoot, mapas y modos). Admite --lms-probe-menu-seconds N (2-30) para dar más tiempo al menú animado. El recibo del probe está en AlfaApplication.Probe.cs:28-42; se pueden añadir campos, por ejemplo una captura de la pantalla de personalización.
- Script de publicación nuevo (por ejemplo work/publish-v030.ps1) tomando de Publish-Release.ps1 el preflight con gh repo view y git ls-remote del tag, el create --draft --verify-tag, la verificación por gh release download más hash y el paso final --draft=false. Adaptarlo a 3 assets (ZIP, sha y marcador) y quitar la exigencia de 2 ZIP y de MANIFIESTO-0.4.0.json. Añadir --prerelease.
- Launcher: si algún día se vuelve a publicar, bumpear Launcher.cs:62 'INICIO 1.2.0' y work/build-launcher.ps1:3 (outputs/launcher-1.2.0). No hace falta para 0.3.0, porque el launcher 1.2.0 ya instalado selecciona 0.3.0 si la release lleva Let-me-sleep-series-2.json.

### Contratos y restricciones

- Tests: NUNCA pasar -quit con -runTests (TEAM.md:13-18). Con -executeMethod (build, preparación, diagnósticos) SÍ va -quit. Un gate exige XML con los casos esperados, failed=0 y skipped=0; exit 0 solo no vale. Usar log y XML nuevos por intento y no lanzar Unity si ya hay otro Unity.exe vivo (los runners hacen `Get-Process Unity`).
- EditMode con -nographics. PlayMode y capturas con -force-d3d11 (probado OK en el editor) y --lms-validation-data <dir absoluto nuevo>. Si falta, en el editor se escribe en N:/LetMeSleep/UserData/Unity (AlfaApplication.cs:68-77).
- El build falla si el árbol git del REPO completo (no sólo unity/) tiene cambios, staged o untracked (WindowsAlfaBuild.cs:43-44,78-80). Hoy el worktree tiene untracked art_source/unity/characters/render_sheet.py y docs/v030/, así que BuildV020 lanzaría 'Commit all candidate inputs before building Windows.' Commitearlos o moverlos antes.
- Unity reescribe al abrir o ejecutar Bootstrap/Customization.renderTexture y UI/Fonts/AtkinsonHyperlegible-Regular SDF.asset (a veces también otras fuentes TMP, eosPluginVersion y los assets URP, según BuildCandidateInputs02/REPORT.md). Hay que restaurarlos desde HEAD (preservando copia, como advance.py) antes del build. SourceDirty compara con git diff, así que diffs sólo de EOL o stat-cache se limpian con `git add --renormalize` o un restore.
- El build exige: bundleVersion igual a la versión pedida (l.45-46); la guía docs/player/PRUEBA-V<ver>.md (hoy fija 0.2.0); N:/LetMeSleep/Private/eos.local.json válido (los 5 campos no vacíos, EosConnection.cs:13-21); y la escena Assets/Scenes/LetMeSleepHiggsfield.unity. La salida cae siempre en N:/LetMeSleep/Artifacts/<ver>-<UTC>. El build es Development (BuildOptions.Development).
- El probe de smoke sólo funciona en builds Development (Debug.isDebugBuild, AlfaApplication.Probe.cs:25). Si se pasa a build de release, el smoke deja de funcionar.
- Harness externos: no pueden vivir fuera de Assets para el Test Runner, así que se copian temporalmente con su .meta y se borran en finally con verificación de hash. Nunca commitear GameplayVisualCaptureHarness.cs ni UiRedesignCapture.cs dentro de Assets. Dependen de nombres privados: los campos de AlfaApplication 'ui', 'game', 'presentation' y 'map'; el componente 'GameplayHudView'; los paneles 'RoleBadge', 'ClockBadge' y 'ContextHintPanel'; y la propiedad pública MenuCamera. Un rediseño de UI que los renombre rompe las capturas.
- Rutas de salida fijadas en el código: GameplayVisualCaptureHarness exige output bajo N:/LetMeSleep/Validation/V020/GameplayVisualCapture01 y un directorio inexistente; UiRedesignCapture exige N:\LetMeSleep\Validation\V020\UiRedesignReview01\; ModularCustomizationUiPlayModeTests exige N:/LetMeSleep/Validation/V020. El harness de gameplay exige exactamente 1920x1080 y los 5 IDs en orden canónico.
- Launcher y release: el nombre del ZIP debe ser exactamente 'Let-me-sleep-<ver>-Windows.zip' y el del sha 'Let-me-sleep-<ver>-Windows.zip.sha256.txt', con la línea '<sha256 64hex>  <nombre>'. Hace falta el asset 'Let-me-sleep-series-2.json' (1 B a 1 KiB) para que 0.3.0 quede en la serie 1: sin él, la release pasa a serie 0 y el launcher se queda en 0.2.0. BUILD.json.releaseSeries debe valer 1 y BUILD.json.version debe coincidir con el tag. El tag tiene formato vX.Y.Z; el launcher incluye prereleases y excluye drafts.
- Publicación: repositorio Sauri0/LetMeSleep. El tag debe existir en el remoto antes del create --verify-tag. v0.2.0 es un tag ligero en 61f8b71. No reemplazar assets con --clobber ni borrar releases; publicar en draft, verificar descargando y luego pasar a --draft=false. No adjuntar el launcher 1.2.0: Defender lo detecta y se retiró.
- Secretos: online.local.json, con clientId y clientSecret de EOS, viaja dentro del ZIP público (BUILD.json lista Let-me-sleep_Data/StreamingAssets/online.local.json). No leer, mostrar ni commitear N:/LetMeSleep/Private/eos.local.json ni unity/Assets/StreamingAssets/*.local.json (gitignorados en .gitignore:17-19).
- Evidencia: cada ejecución va a un directorio nuevo (los scripts hacen throw si ya existe). Las horas en los nombres de Run-* son UTC; los mtime de archivo son locales (UTC-3). Registrar cada ticket en docs/ceo/runs.jsonl con el esquema {ticket, agent, model, reasoning_effort, completed_at_utc, result, evidence[], first_review_accepted, rework, tokens:null, cost:null}.

### Brechas frente a bocetos

- La cadena de capturas no cubre las pantallas centrales de los bocetos. Hay menú principal y HUD humano en Yate (UiRedesignCapture), 10 vistas de gameplay (5 mapas × 2 roles), bandeja de equipo sintética y un canvas modular sintético con el visor vacío. Faltan sala/lobby con CTAs Listo/Crear sala, ajustes con Aplicar, selección de rol/mapa/modo, resultados, y personalización por piezas con vista 3D giratoria (cuerpo, alas, ojos, probóscide, colores, pelo, gorros, ropa, accesorios). Hay que añadir harness o casos para cada una.
- No hay comparación automática contra los bocetos (C:/Users/brank/Desktop/bocetos, Higgsfield/BOCETOS-INVENTARIO.csv, docs/v030/GUIA-ESTILO-BOCETOS.md, este último untracked). La aceptación visual sigue siendo inspección humana de PNG; los gates sólo miden presencia de HUD, píxeles cambiados y overflow de TMP.
- Los builds son Development (WindowsAlfaBuild.cs:66), así que el player muestra la marca 'Development Build' en pantalla, lo que choca con la UI pulida de los bocetos. Quitarla también desactiva el probe de smoke. Hay que decidir: una build Development para QA y otra de release para publicar, o bien mantener Development.
- Las capturas del exe oculto no son fiables. Smoke01 y Smoke05 dieron PNG negros o de 1024x768 (Smoke05 con D3D11 forzado y unos 0,34 ms por frame). Sólo Smoke04 (D3D12 por defecto) produjo PNG reales. No hay evidencia visual del player para la UI nueva: se necesita una ejecución visible o sin -force-d3d11, verificando tamaño y contenido de los PNG.
- Personajes: los PNG de v0.2.0 muestran personajes antiguos (WINDOWS-CANDIDATE-04 l.20-21). CharacterRenderReview renderiza sólo LMS_Human y LMS_Mosquito base, sin variantes de personalización (gorra roja o gorro de dormir, pijama, pantuflas; mosquito rojo con ojos grandes y alas facetadas). Falta un render matriz de variantes.
- El log de build avisa que faltan los prefabs de herramientas slipper, racket y spray (LMS_TOOL_PREFAB_MISSING en player.log de Smoke05; WINDOWS-CANDIDATE-04 l.26). Los objetos de los bocetos (faroles, alfombras, chimenea, muelle, carpa, fogón) no tienen gate de presencia en la cadena.
- El harness de gameplay fija los 5 IDs de mapa *-v1/-v2/-v3. Si v0.3.0 renueva interiores o exteriores con IDs nuevos, hay que actualizar RequiredMapIds (GameplayVisualCaptureHarness.cs:32-38), config.template.json y los scripts de smoke.
- Los gates estáticos son 16:9 (1920x1080 y 1280x720). No se validan otras relaciones de aspecto, transiciones ni animaciones de UI (UI-REDESIGN-FIRST-PASS l.147-151).

### Riesgos

- BuildV020 fallará de inmediato en el estado actual por los untracked art_source/unity/characters/render_sheet.py y docs/v030/ (SourceDirty incluye ls-files --others). También fallará si Unity reescribe Customization.renderTexture o la fuente Atkinson SDF después de correr tests o capturas en el mismo worktree.
- Correr tests o capturas en el worktree antes del build ensucia el árbol: fuentes TMP, renderTexture y quizá ProjectSettings. Conviene capturar y testear primero, luego restaurar o commitear, y compilar al final. Otra opción es separar QA y build en dos checkouts, como en v0.2.0, que testeó en Repository y compiló en el worktree.
- Los harness y runners V020 apuntan a N:/LetMeSleep/Repository/unity, que hoy está sucio con WIP ajeno (UnityGameplayWorld.cs, Atkinson SDF, Higgsfield). Si se ejecutan sin cambiar $project, se valida otra fuente distinta de la que se compila.
- Las rutas de salida fijadas en los harness (V020/GameplayVisualCapture01, V020/UiRedesignReview01, V020 para modular) hacen fallar la aserción si se redirige la evidencia a V030 sin editar las constantes.
- Rediseñar la UI según los bocetos puede renombrar los paneles RoleBadge, ClockBadge o ContextHintPanel, el componente GameplayHudView o los campos privados ui, game y presentation de AlfaApplication. Eso rompe GameplayVisualCaptureHarness y UiRedesignCapture, que los buscan por nombre y reflexión.
- Si no se sube RoomSession.Protocol, clientes 0.2.0 y 0.3.0 comparten BucketId y pueden unirse entre sí con codecs de apariencia o personalización incompatibles. Si se sube, hay que actualizar el test EditMode que fija el literal.
- Si la release v0.3.0 sale sin el asset Let-me-sleep-series-2.json, el launcher 1.2.0 la clasifica en serie 0 y sigue prefiriendo v0.2.0. Si BUILD.json.releaseSeries no vale 1, ValidateInstallation la rechaza.
- El ZIP público incluye online.local.json con clientSecret de EOS, además de build-receipt.json y la carpeta 'Let me sleep_BurstDebugInformation_DoNotShip': package-v020.ps1 no filtra nada. Un launcher nuevo o reempaquetado podría volver a disparar Defender, como pasó con Behavior:Win32/DefenseEvasion.A!ml en el 1.2.0.
- Capturas del exe con ventana oculta o D3D11 forzado dan PNG negros y tiempos de frame inválidos (Smoke01 y Smoke05), así que no sirven como evidencia visual ni de FPS. Sólo Smoke04 con D3D12 funcionó.
- Si el trabajo de arte v0.3.0 (modelos low-poly, texturas, prefabs nuevos) cambia muchos assets, la Library caliente deja de ahorrar tiempo: el build pasa de unos 45 s a una reimportación larga y los tiempos citados dejan de valer. También reaparecen los avisos de atlas de sombras y Graphics Ring Buffer si se añaden luces (faroles, chimenea).
- El tag v0.2.0 no está fetcheado localmente: hace falta `git fetch --tags` antes de comparar. Publish-Release.ps1 no crea tags y exige 2 ZIP, así que no sirve sin adaptarlo. El comando gh real de v0.2.0 no quedó versionado; es una reconstrucción a partir de los assets y flags observados.
- La duración de la suite completa EditMode (404 casos) y PlayMode (unos 230) no consta en la evidencia V020, que sólo tiene corridas filtradas de 15-20 s. Una corrida completa puede tardar bastante más, y los tests físicos de mapas pueden fallar por los defectos abiertos de Puerto (PuertoCoplanar01).

### Verificación

Receta en PowerShell 7. Todo es de ESCRITURA y NO se ejecutó en esta exploración de sólo lectura. Un solo Unity a la vez.

# 0) Variables y preflight
$wt='N:/LetMeSleep/Worktrees/v020-candidate-20260920'; $proj="$wt/unity"
$unity='N:/Unity/Editors/6000.3.24f1/Editor/Unity.exe'
$V='N:/LetMeSleep/Validation/V030'; New-Item -ItemType Directory -Force $V | Out-Null
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Unity slot occupied' }
git -C $wt status --short    # hoy: ?? art_source/unity/characters/render_sheet.py, ?? docs/v030/

# 1) Subir versión (editar y commitear)
- unity/ProjectSettings/ProjectSettings.asset:150 → `bundleVersion: 0.3.0`
- WindowsAlfaBuild.cs: añadir BuildV030() y PrepareV030(); generalizar las l.45 y 49-53 para la guía docs/player/PRUEBA-V0.3.0.md
- crear docs/player/PRUEBA-V0.3.0.md
- Core/RoomSession.cs:93 → "lms-unity-030-1", junto con Tests/EditMode/AlphaProtocolToolOwnershipTests.cs:16
- work/package-v030.ps1 = package-v020.ps1 con 0.3.0 en las l.3,9,18,30, más las exclusiones de package-unity.ps1:113
Después: git -C $wt add -A; git -C $wt commit -m "build: prepare v0.3.0"

# 2) Gate de compilación rápida sin Unity
dotnet msbuild "$proj/LetMeSleep.Tests.PlayMode.csproj" /nologo /v:minimal /t:Build /consoleLoggerParameters:ErrorsOnly "/p:TargetFrameworkRootPath=N:/Unity/Editors/6000.3.24f1/Editor/Data/MonoBleedingEdge/lib/mono/xbuild-frameworks/"
(Los .csproj listan archivos explícitos: los .cs nuevos no entran hasta que Unity regenere los proyectos.)

# 3) EditMode (unos 20 s de arranque; aproximadamente 404 casos; SIN -quit)
$run="$V/Tests-"+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'); New-Item -ItemType Directory $run | Out-Null
$a=@('-batchmode','-projectPath',$proj,'-runTests','-testPlatform','EditMode','-testResults',"$run/editmode.xml",'-logFile',"$run/editmode.log",'-nographics')
# acotado: $a+=@('-testFilter','LetMeSleep.Tests.EditMode.AlphaProtocolToolOwnershipTests;LetMeSleep.Tests.EditMode.AppearanceWireCodecTests')
$p=Start-Process $unity -ArgumentList $a -WindowStyle Hidden -PassThru; $p.WaitForExit()
[xml]$x=Get-Content "$run/editmode.xml" -Raw; $t=$x.'test-run'; "$($p.ExitCode) $($t.result) p=$($t.passed) f=$($t.failed) s=$($t.skipped)"
if ($p.ExitCode -ne 0 -or [int]$t.failed -ne 0) { throw 'EditMode gate' }

# 4) PlayMode (unos 230 casos; los que requieren argumentos salen Ignored, así que en suite completa se exige failed=0 y se revisan los skipped)
$a=@('-batchmode','-projectPath',$proj,'-runTests','-testPlatform','PlayMode','-testResults',"$run/playmode.xml",'-logFile',"$run/playmode.log",'-force-d3d11','--lms-validation-data',"$run/data")
$p=Start-Process $unity -ArgumentList $a -WindowStyle Hidden -PassThru; $p.WaitForExit()
Leer $run/playmode.xml igual que en el paso 3.

# 5) Capturas de UI y gameplay
5a) Con tests versionados:
$c="N:/LetMeSleep/Validation/V020/UiV030-"+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$a=@('-batchmode','-projectPath',$proj,'-runTests','-testPlatform','PlayMode','-testFilter','LetMeSleep.Tests.PlayMode.EquipmentHudVisualEvidenceTests.RealCanvasRendersEquipmentHudAt720And1080WithoutOverflow;LetMeSleep.Tests.PlayMode.ModularCustomizationUiPlayModeTests.SyntheticModularCanvasShowsLongLabelsAndScrollAt720And1080','-testResults',"$c/results.xml",'-logFile',"$c/unity.log",'-force-d3d11','--lms-validation-data',"$c/data",'-equipmentHudReview',"$c/equipment",'-modularCustomizationUiEvidence',"$c/modular")
(Modular exige una ruta bajo N:/LetMeSleep/Validation/V020.)

5b) Menú y HUD reales: copiar N:/LetMeSleep/Validation/V020/UiRedesignReview01/{run-review.ps1,UiRedesignCapture.cs,.meta}. Cambiar $project (l.3) a $proj y ejecutar `pwsh -File run-review.ps1`. Exige passed=2 y skipped=0. Salen PNG en <run>/captures: menu-1920x1080.png, menu-1280x720.png y hud-human-*.

5c) Gameplay 5 mapas × 2 roles: copiar GameplayVisualCapture01 y cambiar $project (l.9) y el git -C (l.61) al worktree. Luego:
pwsh -File run-gameplay-visual-capture.ps1 -CompileOnly
pwsh -File run-gameplay-visual-capture.ps1 -AllowTemporaryProjectImport              (smoke Yate humano)
pwsh -File run-gameplay-visual-capture.ps1 -AllowTemporaryProjectImport -FullMatrix  (10 PNG, unos 25 s de test)
(La salida debe caer bajo ValidationRoot, harness l.31.)

5d) Personajes: & $unity -batchmode -quit -projectPath $proj -executeMethod LetMeSleep.Editor.CharacterRenderReview.Capture -logFile "$V/char-review.log"   (sin -nographics; salida en N:/LetMeSleep/Artifacts/review/alfa-characters)

5e) Inspeccionar todos los PNG a mano. El PASS del XML no acredita el aspecto visual.

# 6) Dejar el árbol limpio para el build
git -C $wt status --short
Si aparecen M en Customization.renderTexture o AtkinsonHyperlegible-Regular SDF.asset: copiarlos a $V/preserved y luego
git -C $wt restore -- "unity/Assets/LetMeSleep/Bootstrap/Customization.renderTexture" "unity/Assets/LetMeSleep/UI/Fonts/AtkinsonHyperlegible-Regular SDF.asset"
Condición para seguir: status vacío, sin untracked.

# 7) (opcional) Preparación facial y de settings; revisar y commitear el resultado
& $unity -batchmode -quit -projectPath $proj -executeMethod LetMeSleep.Editor.WindowsAlfaBuild.PrepareV030 -logFile "$V/prepare-01.log"; git -C $wt status --short

# 8) Build Windows (unos 45-50 s con la Library caliente)
$b="$V/WindowsCandidateBuild01"; New-Item -ItemType Directory $b | Out-Null
$p=Start-Process $unity -ArgumentList @('-batchmode','-quit','-projectPath',$proj,'-executeMethod','LetMeSleep.Editor.WindowsAlfaBuild.BuildV030','-logFile',"$b/unity-build.log") -WindowStyle Hidden -PassThru; $p.Id|Set-Content "$b/pid.txt"; $p.WaitForExit()
$out=((Select-String -Path "$b/unity-build.log" -Pattern '^LMS_ALFA_BUILD Succeeded (.+)$').Matches[0].Groups[1].Value)
Get-Content "$out/build-receipt.json"   # result=Succeeded, errors=0, sourceDirty=false, version=0.3.0, sourceCommit=HEAD
git -C $wt status --short    # debe seguir vacío

# 9) Smoke del exe (unos 60 s por caso, límite 180 s)
Copiar N:/LetMeSleep/Validation/V020/WindowsCandidateSmoke04/run-smoke.ps1, poner $exe="$out/Let-me-sleep.exe" y $outputRoot="$V/WindowsCandidateSmoke01", y ejecutar `pwsh -File run-smoke.ps1`.
Invocación equivalente para un caso: Start-Process "$out/Let-me-sleep.exe" -ArgumentList '--lms-probe-output',$case,'--lms-probe-mode','blood','--lms-probe-map','hf-casa-del-patio-v1','--lms-validation-data',"$case/data",'-logFile',"$case/player.log" -PassThru
Preferir ventana visible (sin -WindowStyle Hidden) y no pasar -force-d3d11. Rechazar los PNG si pesan menos de ~100 KB (27260 B = negro).
Matriz completa: la de WindowsCandidateSmoke03/run-remaining-matrix.ps1 con los 5 IDs × blood, survival y tasks.

# 10) Paquete (alrededor de 1 min; unos 115 MB)
pwsh -File "$wt/work/package-v030.ps1" -BuildDirectory $out
Guarda el JSON de salida en $V/Delivery01/package.json. Revisar: el ZIP, el .zip.sha256.txt, Let-me-sleep-series-2.json, y BUILD.json con version 0.3.0 y releaseSeries 1.

# 11) Tag y release (sólo con autorización explícita del usuario)
git -C $wt tag v0.3.0 <sha-del-receipt>; git -C $wt push origin v0.3.0
$pk='N:/LetMeSleep/Artifacts/v0.3.0/packages/<run>'
gh release create v0.3.0 "$pk/Let-me-sleep-0.3.0-Windows.zip" "$pk/Let-me-sleep-0.3.0-Windows.zip.sha256.txt" "$pk/Let-me-sleep-series-2.json" --repo Sauri0/LetMeSleep --verify-tag --draft --prerelease --title 'Let me sleep v0.3.0 — versión de prueba' --notes-file "$V/Delivery01/release-notes.md"
gh release download v0.3.0 --repo Sauri0/LetMeSleep --dir "$V/Delivery01/verify"   (comparar Get-FileHash con los locales)
gh release edit v0.3.0 --repo Sauri0/LetMeSleep --draft=false
gh release view v0.3.0 --repo Sauri0/LetMeSleep --json url,isDraft,isPrerelease,assets

# 12) Instalación pública real, sin lanzar el juego
pwsh -File "$wt/work/build-launcher.ps1" -OutputDirectory "$V/launcher-tmp"   (compila y ejecuta los tests de work/updater-tests.exe; no publicar ese launcher)
& "$wt/work/updater-tests.exe" --install-latest-no-launch "$V/Delivery01/downloaded-install"   # esperado: LIVE_INSTALL checks=3 game_launched=false version=0.3.0

# 13) Registrar en docs/ceo/STATE.md y en docs/ceo/runs.jsonl
Ticket V030-DELIVERY, con evidencias y el enlace de la release.
