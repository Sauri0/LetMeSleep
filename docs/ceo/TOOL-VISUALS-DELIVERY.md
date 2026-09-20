# Presentación de herramientas — entrega funcional v0.2.0

Fecha: 2026-09-20

## Alcance entregado

- `GameplayVisualPresenter` selecciona de forma estricta un prefab distinto para `flyswatter`, `slipper`, `electric_racket` y `aerosol`.
- Cada prefab configurado debe declarar el `ToolId` exacto, `Grip` e `Impact` propios y una separación no degenerada. Un tipo desconocido o un prefab discordante se rechaza sin sustituirlo por otro modelo.
- Los cuatro tipos se montan en `ToolSocket_R`; `ActorVisualBinding` mantiene una sola herramienta visible según `EquippedToolId` y oculta todas al seleccionar manos.
- La presentación de mundo sigue la pose autoritativa por `PickupId`, conserva visibles los proyectiles sin dueño, oculta el ejemplar de mundo cuando está en inventario y elimina entradas ausentes.
- Si un mismo `PickupId` cambia de tipo, el visual anterior se destruye y se crea el tipo correcto.
- `AlfaPresentationBuilder` busca los cuatro prefabs. La ausencia conocida de arte nuevo queda diagnosticada una vez por tipo, sin usar el matamoscas como reemplazo.

## Evidencia

- Compilación offline de `LetMeSleep.Presentation.Gameplay`, `LetMeSleep.Presentation.Editor` y `LetMeSleep.Tests.PlayMode`: 0 errores.
- Gate nativo inicial junto con música: 6/6 PASS, 0 skip, incluidos los 4 casos base de herramientas.
- Gate nativo final de presentación: 7/7 PASS, 0 skip.
- XML final: `N:/LetMeSleep/Validation/V020/tools-visual-native-02.xml`.
- Log final: `N:/LetMeSleep/Validation/V020/tools-visual-native-02.log`.

Los siete casos verifican exclusión de herramienta equipada, montaje por `Grip`, pose World/Projectile, ocultamiento Held, limpieza, ausencia sin fallback, tipo desconocido, reutilización de `PickupId` y prefab configurado con contrato inválido.

## Pendiente de arte

Sólo existe arte aprobado para `LMS_Flyswatter.prefab`. Todavía faltan los prefabs `LMS_Slipper.prefab`, `LMS_ElectricRacket.prefab` y `LMS_Aerosol.prefab`, además de su colocación en contenido jugable. La entrega valida la integración funcional y sus fallos explícitos; no afirma que las tres herramientas nuevas se rendericen en una partida ni aprueba su apariencia final.
