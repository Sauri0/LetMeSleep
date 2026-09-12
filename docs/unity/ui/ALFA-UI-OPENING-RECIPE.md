# Apertura reproducible de las vistas alfa

Responsable actual: Worker UI. API contrastada con runtime `5f4a381`, documentación heredada `e269be1`. El Director ejecuta esta receta en su turno de Unity, después de integrar ambos commits; Worker UI no abre otro editor. Usar el bootstrap real y el único AlfaUiController existente. No crear un segundo Canvas/EventSystem ni sustituir los callbacks del juego.

## Preparación común

En Play Mode y dentro del contexto C# de diagnóstico del Director:

```csharp
var ui = UnityEngine.Object.FindFirstObjectByType<LetMeSleep.UI.AlfaUiController>();
if (ui == null) throw new System.InvalidOperationException("El bootstrap real todavía no creó AlfaUI.");
```

Los fragmentos siguientes usan `using LetMeSleep.UI;`. Son llamadas a la API actual; no comandos de menú Editor ni un nuevo ejecutable. Las variables `customizationState`, `settingsState` y `lobbyState` representan los estados actuales producidos por el adaptador real. No sustituirlos por opciones inventadas. El flujo manual indicado permite abrir las vistas sin acceso a esas variables.

Capturar en Game View a 1920×1080 y 1280×720 nativos, después de un frame completo de layout y cámara. Si el capturador permite coroutine: `Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();` antes de capturar. Registrar commit central, commit de UI, nombre de escena, selección actual del EventSystem, resolución y SHA-256 del PNG. Nombre recomendado: `menu-1080.png`, `custom-human-front-720.png`, etc., dentro del directorio de evidencia en N: que asigne Director. Conservar los PNG fallidos.

## Menú

```csharp
ui.ShowMainMenu();
// Esperado: ui.CurrentScreen == AlfaUiScreen.MainMenu
// EventSystem.current.currentSelectedGameObject.name == "MainPlayButton"
```

Capturar foco inicial y después foco en `MainQuitButton` mediante navegación real. Abrir confirmación de salida y cancelar con Escape; registrar a qué botón regresa. No confirmar SALIR durante esta tanda. Comprobar personajes visibles detrás del riel y las cinco acciones completas.

## Personalizador con modelos reales

Ruta manual: menú → PERSONALIZAR; elegir HUMANO/MOSQUITO y FRENTE/PERFIL/ESPALDA/CENTRAR. Los botones de rol deben actualizar también el borrador/paleta; no cambiar sólo el modelo del orbitador para representar una selección de rol.

Para un estado real ya disponible:

```csharp
ui.PresentCustomization(customizationState);
ui.ShowCustomization();
var orbit = ui.GetComponentInChildren<CharacterPreviewOrbit>(true);
if (orbit == null || !orbit.IsBound)
    throw new System.InvalidOperationException("Faltan cámara, stage, textura o prefabs reales del visor.");
orbit.ResetView();
orbit.SetAngle(PreviewAngle.Front); // Side o Back para las otras vistas
// Esperado: ui.CurrentScreen == AlfaUiScreen.Customization
```

`PresentCustomization` carga datos pero NO abre la pantalla. `ShowCustomization` la abre; si no hubo datos utiliza defaults internos, por lo que no prueba la configuración persistida del adaptador. `ShowCustomization` no recibe rol ni ángulo. Para cada rol, usar su botón visible o presentar el estado real correspondiente antes de abrir.

Capturar humano frente guardado, humano perfil tras un cambio de color y mosquito frente. Hacer el cambio desde una opción visible existente. Verificar GUARDAR activo sólo cuando hay cambios y Escape → SEGUIR EDITANDO sin perderlos. No llamar SaveCustomization como atajo de captura. Validar arrastre y rueda en una secuencia separada y comprobar proporción de RawImage/RenderTexture.

## Ajustes

Ruta manual: menú → AJUSTES. En partida real: Escape → AJUSTES.

```csharp
ui.PresentSettings(settingsState);
ui.OpenSettings(AlfaUiScreen.MainMenu);
// Esperado: Settings y foco MasterVolumeSlider.
```

Desde una partida que ya está activa, conservando el contexto real:

```csharp
ui.ShowPause();
ui.OpenSettings(AlfaUiScreen.Pause);
```

`OpenSettings` requiere `returnTo`; no existe overload sin argumento. `PresentSettings` no abre la pantalla. No forzar SupportsVideo/SupportsRebinding a true: usar capacidades del runtime. Capturar AUDIO/VIDEO/CONTROLES con valores reales; para borrador modificar un slider visible. Probar Escape y descarte; volver a Pausa debe mantener gameplay bloqueado. Reanudar con el control visible. Este flujo pausa pero no detiene la simulación online.

## Lobby administrador e invitado

Ruta manual: JUGAR ONLINE → CREAR SALA en anfitrión; UNIRME CON CÓDIGO en invitado. Esperar conexión confirmada y usar ambos estados reales. No mostrar código ficticio como si fuera una sala conectada.

```csharp
ui.PresentLobby(lobbyState);
// PresentLobby sí abre Lobby si no era la pantalla activa.
// Inspeccionar lobbyState.IsOwner/CanStart/CanExplore/ReadyPending/StartPending.
```

No existe `ShowLobby`. La UI recibe reglas y permisos; no los decide. Capturar código completo, roster y LISTO; propietario con INICIAR RONDA según CanStart y motivo si está bloqueado; invitado sin acción de inicio habilitada. No iniciar ronda sólo para obtener una imagen de lobby.

Si el adaptador real no expone CanExplore, no forzarlo para una captura funcional. Una fixture construida con `new LobbyUiState(...)` debe etiquetarse como presentación sintética y no acredita EOS, permisos autoritativos ni recorrido 3D. La tanda funcional requiere sala real con al menos dos participantes y un cambio confirmado de LISTO.

## Otras entradas de la receta JSON

- Entrenamiento: `ui.PresentTraining(trainingState); ui.ShowTraining();`. PresentTraining solo actualiza datos; no abre la vista. Seleccionar rol no inicia; no invocar EMPEZAR para capturar la selección.
- Online: `ui.ShowOnlineChoice()`, `ui.ShowCreateRoom(nombre)` o `ui.ShowJoinRoom(nombre)`; luego `ui.PresentOnline(onlineState)` actualiza su estado. No basta PresentOnline para abrir formulario.
- HUD: usar una partida activa, `ui.ShowGameplay(esEntrenamiento)` y `ui.PresentHud(hudState)`. PresentHud actualiza datos y conserva modal/ajustes abiertos. No usar ShowGameplay para fingir una ronda existente.
- Resultados: `ui.PresentResults(resultsState)` abre la vista. Para evidencia funcional, el estado debe provenir de la ronda finalizada.

## Límites de aceptación

Las aperturas por API facilitan imágenes reproducibles, pero no prueban navegación de usuario, callbacks, persistencia ni red. La aprobación gráfica sigue pendiente de comparación con las seis referencias y revisión de Branko. La revisión estática y sus defectos abiertos están en `RECOVERY-REVIEW-20260912.md`.
