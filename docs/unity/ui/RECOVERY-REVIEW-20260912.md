# Worker UI — revisión de recuperación 2026-09-12

Base revisada: rama `codex/unity-ui`, HEAD `e269be1`; runtime `5f4a381`. Worktree limpio al recibirlo. Propiedad actual: Worker UI, según TEAM-RECOVERY-20260912. No se ejecutó Unity, Blender, render, compilación ni prueba pesada.

## Evidencia revisada

Se abrieron con `view_image` las seis referencias originales `N:/LetMeSleep/References/UIQuality-20260912/01.png` a `06.png`. Se leyeron ambos contratos de calidad y la spec/receta/API de UI. Las referencias aportan jerarquía, iconos reconocibles, densidad controlada, separación de paneles y escena; sus modos, armas, inventarios, facciones elegibles y recompensas no se trasladan al alfa.

La revisión de fuente confirma riel de menú de 500×920, marca por capas y botones con título/subtítulo; visor con AspectRatioFitter; tarjetas AUDIO/CONTROLES/VIDEO; roster a la izquierda y acciones de lobby separadas. Esto acredita estructura, no acabado visual. No se reutiliza ninguna captura anterior a 5f4a381 para aprobar este lote.

## Hallazgos y siguiente delta

| Prioridad | Evidencia de fuente | Acción delimitada / comprobación pendiente |
|---|---|---|
| P1 | `CloseConfirm` enfoca `DefaultFocusName(screen)`; no conserva el control invocador. Cancelar una salida desde SALIR devuelve a JUGAR ONLINE; cancelar descarte puede perder la ubicación dentro de paletas/ajustes. | Próximo cambio funcional UI: conservar selección al abrir modal y restaurarla si sigue activa/interactuable; fallback seguro. Validar Escape y descarte con Revisor Funcional. |
| P1 | `Update` procesa Escape y feedback; no hay `tabKey`, `<Keyboard>/tab` ni `Key.Tab` en fuentes LetMeSleep. `AlfaUiFactory` usa navegación Automatic. El menú anuncia TAB. | No dar por probado Tab/Shift+Tab. Director/Revisor Funcional deben comprobar el InputSystemUIInputModule integrado; si no aporta recorrido, implementar navegación acotada a vista/modal sin interferir con inputs o dropdown abierto. |
| P2 | `Button` mantiene Outline estático; la transición es ColorTint. `UpdateSelectionFeedback` emite audio, no modifica borde ni posición. | El contorno ámbar de foco y desplazamiento de la spec no están implementados en estas rutas. Preparar indicador de foco separado del hover, tras ver captura normal/seleccionado de la misma acción. |
| P2 | `FeatureButton` usa subtítulo de 13 unidades; menú también contiene textos de 13/14. La spec fija mínimo 16 a 1080p. | Ampliar texto secundario y ajustar caja sólo con comprobación de título/subtítulo a ambas resoluciones; revisar especialmente pestañas del personalizador. |
| P2 | Botones de vista del personalizador tienen 48 unidades y botones secundarios típicos 58. CanvasScaler usa 1920×1080: a 720p equivalen a 32 y 38,7 px de alto, por debajo de los 44 exigidos. | Medir RectTransform real en captura; aumentar objetivos interactivos y revisar layout conjunto. No declarar cumplido el mínimo porque `minHeight` vale 44 unidades de Canvas. |
| P2 | Encabezados AUDIO y VIDEO reutilizan símbolos Settings y Explore; `AlfaUiIcon` no contiene Audio/Video. El roster muestra icono de estado, no avatar de participante. | Revisar iconos reales a 720p contra referencias. Siguiente delta visual acotado: pictogramas propios de altavoz/pantalla, conservando etiquetas. Avatares reales requieren datos/asset disponibles; no inventar retratos ni nuevas opciones. |

No se modifica composición runtime antes de la primera captura del lote heredado. Los hallazgos anteriores quedan abiertos; la primera entrega cambia documentación para hacer reproducible esa captura. Online/Bootstrap quedan fuera de esta entrega.

## Captura solicitada al Director

Usar `ALFA-UI-OPENING-RECIPE.md` junto al JSON de capturas. Primera tanda: menú, humano frente/perfil y mosquito frente, ajustes desde menú/pausa, lobby administrador/invitado; todos a 1920×1080 y 1280×720. Registrar HEAD central, runtime UI integrado, resolución nativa, estado/selección y SHA-256. Compartir rutas en N: para revisión con view_image.

Para foco, acompañar capturas con recorrido real: flechas/Tab/Shift+Tab/Enter/Escape, modal seguro y restauración. Para el visor, giro y extremos de zoom necesitan secuencia o video; una imagen no acredita movimiento. Lobby real necesita estado de sala confirmado; una fixture sólo acredita presentación.

## Validación de esta entrega

Contraste manual de nombres y firmas con `AlfaUiController`, `AlfaUiContracts`, `AlfaUiRuntime` y `CharacterPreviewOrbit`; parsing JSON y `git diff --check`. No se acredita compilación, render, calidad gráfica, funcionalidad online ni persistencia con esta revisión de fuente. Siguiente dependencia: importación y capturas del Director; después se implementa el delta delimitado por los hallazgos y la evidencia visual.
