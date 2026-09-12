# Revisión funcional de Bootstrap — 2026-09-12

Alcance: `AlfaApplication*.cs`, `OnlineGameplaySession`, coordinador de sala, movimiento de lobby, preferencias y su enlace con UI/Presentation. Revisión estática sobre la integración principal; no se abrió Unity.

Dictamen: **BLOCK hasta cerrar ciclo, personalización y evidencia nativa/WAN**.

## Bloqueos encontrados

### B1 — Alta — Una interrupción del host no podía volver al lobby

`InterruptGame` mostraba Resultados mientras `RoomSession` seguía en `Playing`; el botón invocaba `ReturnToWaiting`, que exige `Results`. La corrección activa llama `FinishRound` antes de presentar la interrupción. Falta verificar timeout de barrera, botón de retorno y publicación concordante en ambos extremos.

### B2 — Alta — Rechazos anteriores al primer `RoomView` quedaban ocultos

Para `IncompatibleVersion`, sala llena o fase incorrecta, `room.Current` todavía es null. La aplicación llamaba `PresentRoom(null, ...)`, por lo que el formulario seguía ocupado. La corrección activa devuelve un error online y libera el latch; debe mapear incompatibilidad a su estado específico y probar reintento/cancelación.

### B3 — Alta — Salir de Resultados de entrenamiento dejaba la partida viva

La UI cambiaba de pantalla sin llamar a la aplicación. Gameplay, casa y audio de ronda quedaban activos detrás del menú. El commit UI `40b136d` cambia el flujo para ejecutar `CancelTraining`; queda pendiente de integración y prueba nativa.

### B4 — Alta — Personalización guardada sólo afecta el preview

`appearance` se carga, guarda y aplica al visor. El roster usa `CosmeticProfileId=default` y los presentadores de gameplay/lobby no aplican ni replican los colores guardados. La personalización base no cambia el personaje real.

### B5 — Alta — El snapshot final del invitado podía perderse

El estado final viajaba como datagrama no fiable mientras `RoomView.Results` viajaba por el canal fiable. Como Bootstrap no construye Resultados desde el `RoomView`, perder el último snapshot dejaba al invitado en HUD. La corrección activa marca el snapshot final como fiable; falta probar orden inverso entre canales y entrega única de Resultados.

## Hallazgos restantes

- El nombre se carga de preferencias pero no se coloca en el formulario al reiniciar. `40b136d` agrega el enlace de UI; queda pendiente de integración.
- `SavePreferences` no controla fallos de escritura/reemplazo. Una excepción deja el estado en memoria aplicado pero el latch visual en Guardando/Aplicando.
- La ruta de usuario dependía de la mera existencia de `N:`. La corrección activa limita esa ruta al editor y usa `Application.persistentDataPath` en build.
- Al cerrar una sala remota, `ShowOnlineChoice` activa una vista y `PresentOnline(RoomClosed)` actualiza otra vista oculta; el motivo no queda visible.
- Tras una salida intermedia, un nuevo miembro del lobby puede recibir el mismo spawn físico que conserva un sobreviviente. Elegir un spawn libre evita solapamiento.
- `docs/unity/CORE-CONTRACT.md` todavía declara protocolo alfa-1 mientras el código ya usa alfa-2.

## Matriz mínima de reverificación

1. Crear, cancelar y reintentar antes/durante autenticación, creación, búsqueda y entrada.
2. Rechazar versión distinta, sala llena, código inválido y entrada con ronda iniciada; cada caso muestra causa y permite volver/reintentar.
3. Host e invitado completan ronda; forzar llegada de `RoomView.Results` antes del snapshot final y comprobar una sola vista de resultados.
4. Forzar timeout de carga y pérdida del host; host vuelve al lobby o ambos pueden salir sin reiniciar el proceso.
5. Dos rondas seguidas: nuevo sorteo, cero sangre/contacto/equipo, puertas y pickups iniciales.
6. Entrenamiento de ambos roles: repetir, cancelar durante preparación, volver al menú desde pausa/resultados; mapa, audio y runtime anteriores desaparecen.
7. Guardar nombre, colores y ajustes, reiniciar proceso y comprobar formulario, preview, lobby y partida.
8. Simular escritura denegada/corrupta en preferencias; conservar último estado válido y liberar controles.

