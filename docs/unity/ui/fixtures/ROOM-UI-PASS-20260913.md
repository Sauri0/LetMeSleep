# Cierre UI de sala — PASS acotado a RenderTexture

Fecha: 2026-09-13. Root ejecutó el runner y aprobó visualmente ambas capturas. Worker UI documenta el cierre; no ejecutó Unity ni nuevos tests durante este cierre.

## Evidencia final

- Directorio: `N:/LetMeSleep/Validation/Higgsfield/RoomUI/run-20260913-090108-724ce4e3`.
- Recibo nativo: `N:/LetMeSleep/Validation/Higgsfield/RoomUI/run-20260913-090108-724ce4e3/batch.json`.
- Comprobación posterior al cierre: `N:/LetMeSleep/Validation/Higgsfield/RoomUI/run-20260913-090108-724ce4e3/post-exit.json`.
- Captura 720p: `N:/LetMeSleep/Validation/Higgsfield/RoomUI/run-20260913-090108-724ce4e3/room-720-RT.png`.
- Captura 1080p: `N:/LetMeSleep/Validation/Higgsfield/RoomUI/run-20260913-090108-724ce4e3/room-1080-RT.png`.
- Detalle 720p: `N:/LetMeSleep/Validation/UI-RoomMaps-Native-20260913/run-20260913-090120-e69417c8/receipt.json`.
- Detalle 1080p: `N:/LetMeSleep/Validation/UI-RoomMaps-Native-20260913/run-20260913-090121-adec8fc3/receipt.json`.

`batch.json` registra `checks720=true`, `checks1080=true`, `sourcesUnchanged=true`, `optionsRestored=true`, sin failure. Conserva su estado original `captured-awaiting-visual-review`: la aprobación humana posterior de Root se registra aquí, sin reescribir el recibo.

Root revisó ambas PNG y confirmó textos, nombres y mapa largo completos, y marcador `[1]` legible sin glifo faltante. El fixture comprueba también `[AUTO]` y texto completo con esa selección.

## Alcance aprobado

UI real uGUI/TMP en URP RenderTexture 1280×720 y 1920×1080, con escalado explícito 16:9. Sala y acciones simuladas, dos miembros, IDs/nombres reales del catálogo. Incluye recorrido de los cinco mapas, etiqueta autoritativa tras snapshot, guardas de anfitrión/invitado/fase/pending, rechazo, wrap, texto completo del fixture y límites de botones.

Catálogo leído: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/UnityPackage/catalog-input.json`, SHA-256 `7b1f820580dead1d5e2580204a0fdf3dfa1d11eda1a4bdf9718b75ec8b983241`. Nombres: Isla del laguito, Casa del patio, Campamento del pinar, Yate a la deriva y Puerto del faro.

`post-exit.json` registra exitCode 0, hashes antes/después iguales para los cinco archivos de fuentes/TMP Settings rastreados y `editorSettingsUnchanged=true`. Opciones originales/restauradas de este run: enabled=false, options=0. Hash de EditorSettings antes/después: `04fcdb2810147a389b3d5ff5100b9e1c201b63cd1596f05ce542198e46921d94`. El aislamiento queda comprobado para esta ejecución normal; no certifica recuperación ante crash o recarga inesperada del dominio.

No constituye evidencia de WAN, entrada física, GameView, carga/geometría de mapas, rendimiento ni una partida integrada. Las elipsis de nombres arbitrariamente largos tampoco se evalúan aquí: los nombres del fixture son cortos y deben mostrarse completos.

## Estado de entrega

Cerrado por Root tras el ajuste ASCII `4925114`. Sin cambios adicionales de UI ni nuevas ejecuciones solicitadas. Este documento actualiza el estado final de [ROOM-RT-RUNNER.md](ROOM-RT-RUNNER.md), conservando allí el historial de correcciones y las instrucciones de reproducción.
