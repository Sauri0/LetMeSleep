# Revisión funcional UI `dde06ff`

Entrega revisada: `dde06ff2ec1f7295d0ebf3008971c777054ce4eb`, “feat(unity): build alfa runtime UI”, más `029eb7a` y `50dd992`.

Dictamen: **BLOCK de flujo hasta integrar la corrección y cerrar el último grupo de envíos repetidos; alcance alfa conforme**. Revisión estática, sin Unity Runner ni inspección visual.

## Hallazgos

### F1 — Alta — Volver no cancela una operación online activa

`BuildOnlineForm` crea `OnlineBackButton` con callback directo a `ShowOnlineChoice`, pero no conserva la referencia. `PresentOnline` deshabilita campos y primaria durante `IsBusy`, aunque nunca oculta ni deshabilita Volver. Un clic durante Connecting/Creating/Searching/Entering abandona la vista sin emitir `CancelOnline`; la operación puede completar detrás de otra pantalla.

El flujo normativo exige `CANCELAR` durante la operación y `← VOLVER` sólo en reposo. Guardar la referencia y alternar ambos controles con el estado busy, o hacer que Volver emita cancelación y permanezca en la vista hasta recibir el estado final.

### F2 — Alta — Las intenciones críticas no tienen latch local

`SubmitOnline`, Listo e Iniciar sólo consultan el último estado presentado. Entre el callback y la próxima publicación de `IsBusy`, `ReadyPending` o `CanStart`, un doble clic/Enter puede emitir dos veces `CreateRoom`, `JoinRoom`, `SetReady` o `StartRound`.

El controlador debe bloquear localmente cada intención en el primer envío y liberarla al recibir confirmación, cancelación o error. La autoridad sigue siendo responsable del resultado; el latch sólo impide solicitudes duplicadas desde una entrada repetida.

### F3 — Media — Escape del panel de lobby omite la pausa de lobby

`HandleEscape` llama a `ConfirmLeave` cuando el lobby no está en modo recorrido. El contrato requiere una pausa de lobby con `VOLVER A LA SALA` y `SALIR DE LA SALA`. La única vista Pause actual está construida para gameplay: su Continuar llama `ResumeGame` y `SetGameplayInputBlocked(false)`.

Implementar un contexto/vista de pausa de lobby que restaure panel y foco sin invocar acciones de gameplay. La confirmación destructiva de salida puede permanecer como segundo paso.

### F4 — Media — Entrenamiento, guardado y ajustes aún permiten doble envío

La corrección inicial de latches cubre Online/Listo/Iniciar, pero `TrainingStartButton`, `SaveCustomization`, `ApplySettings` y Repetir entrenamiento todavía dependen sólo de `IsLoading`, `IsSaving` o `IsApplying` recibidos. Dos activaciones antes del siguiente `Present*` pueden emitir dos intenciones.

Aplicar el mismo latch local y feedback inmediato a `StartTraining`, `SaveCustomization` y `ApplySettings`, incluida la repetición desde Resultados. Liberarlo únicamente cuando llegue el siguiente estado o la transición esperada.

## Conformidades observadas

- Menú expone sólo online, entrenamiento, personalización y ajustes del alfa.
- Modo y mapa están fijados a `blood` y `house-patio-v1`; no aparecen contenidos futuros.
- Lobby muestra roles como sorteados al comenzar y deja la selección de humanos sólo al host.
- Entrenamiento permite elegir Humano/Mosquito sin alterar modo o mapa.
- HUD no expone marcadores anatómicos ni datos de picadura futura.
- Resultados diferencian host, invitado y entrenamiento; salir de una sala usa confirmación.

## Reverificación

Revisado `5cb83a676d4c16e126189eaae53a5e5fffd38d9a`: F1, F2 para Online/Listo/Iniciar y F3 quedan cerrados por código. El commit todavía debe integrarse en la rama principal; F4 permanece abierto.

Después de cerrar F4, ejecutar navegación por teclado/mando y mouse a 1280×720 y 1920×1080. Mutantes mínimos: doble clic en cada acción, Volver/Escape en cada fase async y Escape panel→pausa→panel sin desconectar ni desbloquear gameplay.
