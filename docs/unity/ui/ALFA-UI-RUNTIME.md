# Acople runtime de UI alfa

Este documento describe la API disponible en `LetMeSleep.UI`. El Director adapta Core, Online y Gameplay; la UI no referencia EOS ni decide autoridad.

## Creación

```csharp
var ui = AlfaUiRuntime.Create(menuActions, new AlfaUiDependencies(
    headingFont,
    bodyFont,
    panelSprite,
    buttonSprite,
    previewSetup));
```

`AlfaUiRuntime.Create` construye por Unity API:

- `Canvas` overlay con referencia 1920×1080.
- `CanvasScaler`, `GraphicRaycaster` y, si falta, un `EventSystem` con `InputSystemUIInputModule`.
- Menú, Online, Lobby, Entrenamiento, Personalización, Ajustes, HUD, Pausa, Resultados y confirmaciones.

No se necesita escena o prefab UI escrito manualmente. Los sprites y fuentes son dependencias opcionales; si faltan, usa formas planas y la fuente TMP predeterminada. El modelo del personalizador nunca se simula con UI: `CharacterPreviewSetup` recibe cámara, stage, `RenderTexture` y prefabs externos humano/mosquito.

El visor supone que el frente de los prefabs mira hacia `+Z`: coloca la cámara en `+Z`, calcula centro y distancia desde los `Renderer.bounds`, y vuelve a encuadrar cada rol sin modificar la escala ni los colliders. Cámara y modelo sólo permanecen activos mientras Personalizar está visible.

## Acciones que implementa el adaptador

`IMenuActions` expone:

```text
CreateRoom(name)
JoinRoom(name, normalizedCode)
CancelOnline()
CancelTraining()
CopyRoomCode(groupedCode)
SetReady(ready)
SetHumanCount(null | 1..5)
StartRound()
LeaveRoom()
SetLobbyExploration(exploring)
StartTraining(role, "blood", "house-patio-v1")
PreviewCustomization(draft)
SaveCustomization(draft)
ApplySettings(draft)
SetGameplayInputBlocked(blocked)
ResumeGame()
ReturnToLobby()
QuitGame()
```

Crear/Unirse no completan localmente: el adaptador presenta `OnlineUiState` para busy, cancelación, error y éxito. `CancelOnline` se usa únicamente durante una operación pendiente; `LeaveRoom` cierra una sala conectada.

La UI bloquea nombre, código, pegar, volver y envío desde el mismo frame de `CreateRoom`/`JoinRoom`; el adaptador debe responder con un `OnlineUiState` busy y después con cancelación, error o `PresentLobby`. En lobby, `ReadyPending` y `StartPending` mantienen los latches visibles hasta la respuesta autoritativa. Escape abre una pausa de lobby con `VOLVER A SALA` y `SALIR`, sin invocar `ResumeGame`.

## Estados presentados

```csharp
ui.PresentOnline(onlineState);
ui.PresentLobby(LobbyUiState.FromRoomView(roomView, localId, roomCode, canStart, reason));
ui.PresentTraining(trainingState);
ui.PresentCustomization(customizationState);
ui.PresentSettings(settingsState);
ui.PresentHud(hudState);
ui.PresentResults(resultsState);
```

La entrada al menú y formularios se controla con `ShowMainMenu`, `ShowOnlineChoice`, `ShowCreateRoom` y `ShowJoinRoom`. `PresentLobby` y `PresentResults` cambian al contexto correspondiente. `ShowGameplay` desbloquea el input y muestra el HUD sin volver a invocar `IMenuActions.ResumeGame`; el botón de Pausa llama una vez a ese callback y después usa la transición. `PresentHud` actualiza datos, pero conserva Pausa o Ajustes si están abiertos. `OpenSettings` recibe la pantalla a la que debe volver.

## Código de sala

`AlfaRoomCode.Normalize` elimina guiones y espacios y convierte a mayúsculas. `IsComplete` exige diez caracteres; el backend conserva la validación del alfabeto y existencia. `FormatForDisplay` produce `XXXXX-XXXXX`. Nunca mostrar IP, puerto o identificadores EOS.

## Mapeo Gameplay → HUD

El adaptador de Director puede mapear los eventos de `GameplayRuntime` sin crear lógica paralela:

Para refrescar la UI en host y clientes, debe suscribirse a `GameplayRuntime.SnapshotApplied`; `SnapshotReady` queda reservado a la salida de red del host.

| Gameplay | `BloodHudUiState` |
|---|---|
| `LatestSnapshot.TimeRemainingTicks` | `SecondsRemaining`, convirtiendo con el tick rate autoritativo. |
| `LatestSnapshot.BloodCollected/BloodGoal` | `BloodCurrent/BloodTarget`. |
| `LocalPrincipal` o rol local | `Role`. |
| `LocalPrivate.InteractionHint` | `Interaction`, traducido a un único prompt visible. |
| `PreparationProgress` | `ActorState.Recovering` o texto contextual sólo si el contrato Gameplay lo identifica como preparación. |
| `ExtractionProgress` | `ActorState.Extracting` + `StateProgress01`. |
| `RecoverySeconds` | `Fainted`/`Stunned` y texto/tiempo contextual. |
| `CanAct` | Ocultar acciones si es falso; no inventar motivo. |
| `LastDoorResult` / `Rejection` | Mensaje contextual breve que se limpia al siguiente estado válido. |
| `Snapshot.Result/Winner` | `ResultsUiState`. |

La UI llama a `GameplayRuntime.SetInputBlocked(true)` por medio del adaptador al abrir Pausa, Ajustes desde Gameplay o Resultados. Reanudar o volver directamente de Ajustes a Gameplay lo revierte. El runtime conserva la autoridad de input.

Bindings alfa que la copia contextual puede mostrar:

- Humano: WASD, ratón, `Shift` correr, `Ctrl` agachar, `Space` saltar, clic golpear, `F` puerta y `R` mantener ayuda.
- Mosquito: WASD, ratón, `Ctrl` bajar, `Space` subir, `E` mantener picadura/soltar para cancelar/nuevo `E` para desprenderse, `F` posarse y `R` mantener ayuda.

No aparecen marcas de picadura. El HUD sólo muestra prompt centrado, progreso autoritativo y estados confirmados.

## Persistencia

El adaptador carga el nombre recordado y lo pasa a `ShowCreateRoom(name)` o `ShowJoinRoom(name)`. Guarda el nombre únicamente después de una intención válida o una conexión confirmada, según el contrato del Director. Personalización y ajustes se persisten fuera de UI después de sus callbacks; la vista sólo conserva un borrador mientras está abierta.

`AlfaSettingsDraft.FrameLimit` usa `0` para `SIN LÍMITE`; las demás opciones son 30, 60, 90, 120, 144, 165 y 240. `VSync` inicia apagado. El adaptador aplica ambos valores mediante Presentation.

## Límites de este checkpoint

- No hay referencia a Online, Gameplay ni Presentation porque sus assemblies se conectan mediante el adaptador.
- No hay escena/prefab generado ni prueba gráfica. La compilación estática usa los assemblies instalados de Unity 6000.3.24f1.
- La verificación de jerarquía, navegación, clipboard, render y resolución requiere una importación/PlayMode posterior coordinada por Director.
