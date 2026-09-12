# Revisión funcional de Bootstrap — 2026-09-12

Alcance: `AlfaApplication*.cs`, `OnlineGameplaySession`, coordinador de sala, movimiento de lobby, preferencias y su enlace con UI/Presentation. La revisión estática se complementó con la suite PlayMode nativa de entrenamiento del 2026-09-12.

Dictamen: **BLOCK hasta cerrar los flujos online/WAN, personalización entre dos pares, revisión visual y candidata reproducible**.

## Bloqueos encontrados

### B1 — Alta — Una interrupción del host no podía volver al lobby

`InterruptGame` mostraba Resultados mientras `RoomSession` seguía en `Playing`; el botón invocaba `ReturnToWaiting`, que exige `Results`. La corrección activa llama `FinishRound` antes de presentar la interrupción. Falta verificar timeout de barrera, botón de retorno y publicación concordante en ambos extremos.

### B2 — Alta — Rechazos anteriores al primer `RoomView` quedaban ocultos

Para `IncompatibleVersion`, sala llena o fase incorrecta, `room.Current` todavía es null. La aplicación llamaba `PresentRoom(null, ...)`, por lo que el formulario seguía ocupado. La corrección activa devuelve un error online y libera el latch; debe mapear incompatibilidad a su estado específico y probar reintento/cancelación.

### B3 — Resuelto — Salir del entrenamiento dejaba la partida viva

La UI cambiaba de pantalla sin llamar a la aplicación. Gameplay, casa y audio de ronda quedaban activos detrás del menú. La corrección integrada ejecuta `CancelTraining`; la prueba PlayMode verificó en ambos roles que desaparecen runtime, actores y pickups y vuelve la cámara de menú.

### B4 — Alta — Personalización online pendiente de prueba entre pares

El problema original hacía que `appearance` sólo afectara el preview. La corrección activa replica un paquete acotado por el canal 2, valida miembro, código de sala e IDs permitidos, y aplica los colores a lobby y gameplay. Falta verificar con dos pares el primer envío, un cambio en vivo, entrada a ronda y reconexión.

### B5 — Alta — El snapshot final del invitado podía perderse

El estado final viajaba como datagrama no fiable mientras `RoomView.Results` viajaba por el canal fiable. Como Bootstrap no construye Resultados desde el `RoomView`, perder el último snapshot dejaba al invitado en HUD. La corrección activa marca el snapshot final como fiable; falta probar orden inverso entre canales y entrega única de Resultados.

## Hallazgos restantes

- El nombre guardado ya se coloca en el formulario mediante la corrección UI integrada.
- La escritura de preferencias ahora revierte el estado y muestra feedback si falla. Aún debe envolver `Directory.CreateDirectory(DataPath)` y capturar `UnauthorizedAccessException` también durante carga, para que una carpeta inaccesible no aborte `Start`.
- La ruta de usuario ya queda limitada a `N:` en editor y usa `Application.persistentDataPath` en build.
- La corrección activa muestra el estado `RoomClosed` sobre el formulario visible de unión.
- La corrección activa selecciona el spawn más alejado de los miembros persistentes para evitar solapamientos al reconstruir el lobby.
- La corrección activa alinea `docs/unity/CORE-CONTRACT.md` con el protocolo alfa-2 del código.

## Matriz mínima de reverificación

1. Crear, cancelar y reintentar antes/durante autenticación, creación, búsqueda y entrada.
2. Rechazar versión distinta, sala llena, código inválido y entrada con ronda iniciada; cada caso muestra causa y permite volver/reintentar.
3. Host e invitado completan ronda; forzar llegada de `RoomView.Results` antes del snapshot final y comprobar una sola vista de resultados.
4. Forzar timeout de carga y pérdida del host; host vuelve al lobby o ambos pueden salir sin reiniciar el proceso.
5. Dos rondas seguidas: nuevo sorteo, cero sangre/contacto/equipo, puertas y pickups iniciales.
6. Entrenamiento de ambos roles: repetir, cancelar durante preparación, volver al menú desde pausa/resultados; mapa, audio y runtime anteriores desaparecen.
7. Guardar nombre, colores y ajustes, reiniciar proceso y comprobar formulario, preview, lobby y partida.
8. Simular escritura denegada/corrupta en preferencias; conservar último estado válido y liberar controles.

## Evidencia nativa agregada

La suite `TrainingBootstrapPlayModeTests` quedó verde `2/2` en Unity `6000.3.24f1`. Cubre arranque real, entrenamiento humano y mosquito, tres actores, cámara por rol, siete pickups físicos, ausencia de valores no finitos durante varios frames y limpieza con `CancelTraining`. Ver [recibo PlayMode de entrenamiento](TRAINING-BOOTSTRAP-PLAYMODE-2026-09-12.md).
