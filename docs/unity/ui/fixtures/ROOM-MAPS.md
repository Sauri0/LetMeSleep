# Selector de mapa de sala

Contrato de integración UI, 2026-09-13. No modifica Bootstrap ni Core.

- `IRoomMapActions.SetRoomMap(string mapId)` es una capacidad opcional, separada de `IMenuActions`.
- `SetRoomMaps(IReadOnlyList<TrainingMapOption>)` recibe el catálogo real. Copia la lista, rechaza elementos nulos e IDs duplicados antes de modificarla. `null` y lista vacía dejan el mapa en lectura.
- `LobbyUiState` agrega argumentos opcionales `isWaiting = true` y `rulesPending = false`. `FromRoomView` calcula `IsWaiting` a partir de `RoomPhase.Waiting` y acepta `rulesPending`.
- Las flechas requieren anfitrión, Waiting, capacidad y al menos una alternativa al mapa actual. Se bloquean mientras Ready, Start o reglas están pendientes, incluyendo el latch local anterior al primer snapshot.
- La UI envía sólo IDs recibidos. Si el mapa actual no está en el catálogo, siguiente pide el primero y anterior el último. No añade ni selecciona IDs automáticamente al recibir un catálogo.
- `MapId` y su nombre permanecen autoritativos. El mapa alfa muestra Casa con patio. Los demás usan el nombre del catálogo, luego `MapLabel` explícito, y finalmente el ID; el nombre alfa por defecto no se reutiliza para IDs desconocidos.
- Cada `PresentLobby` es un snapshot completo y autoritativo de los flags pendientes. Al terminar o rechazar `SetRoomMap` **y `SetHumanCount`**, publicar otro snapshot para liberar el latch. Una operación asíncrona debe presentar `RulesPending = true` durante su ejecución, incluso en snapshots causados por otros eventos.
- Ready, Start y edición de cantidad de humanos quedan bloqueados durante cambios de reglas. Sus callbacks también validan permisos/pending; no dependen sólo del aspecto deshabilitado del botón.

## Validación ejecutada

Compilación de toda UI contra referencias Unity/Core existentes: cero errores y cero advertencias. Sin abrir Unity.

`CheckRoomMaps.py` extrae DTOs, predicados y métodos reales de selección/etiqueta; usa spy de acciones y sustitutos mínimos de texto/botón. Veintidós comprobaciones pasan: default alfa, host, invitado, fase, pending, doble intención, wrap, ausencia de capacidad/catálogo, nombre autoritativo, rechazo, validación atómica y callback síncrono. Verifica estáticamente el enlace de pending en PresentLobby y guardas de Ready/Start. El refresco de controles ajenos al selector se sustituye; no constituye cobertura nativa de esos controles.

```powershell
python docs/unity/ui/fixtures/CheckRoomMaps.py --repository N:/LetMeSleep/Worktrees/ui --output N:/LetMeSleep/Validation/UI-RoomMaps-20260913 --core N:/LetMeSleep/Repository/unity/Library/ScriptAssemblies/LetMeSleep.Core.dll
dotnet build N:/LetMeSleep/Validation/UI-PreviewBounds-20260912/UI.csproj --nologo -v minimal --no-restore
```

La fila del mapa usa flechas de 48 unidades, caption de 64 y valor flexible dentro del panel de reglas existente. No se ejecutó Unity, captura de pantalla, entrada física ni prueba online. La legibilidad y el encaje nativos de esta fila quedan pendientes de la validación coordinada; los PASS de entrenamiento anteriores no se aplican a este selector.
