# HUD privado de equipamiento — entrega v0.2.0

Fecha: 2026-09-20

## Alcance entregado

- HUD visible únicamente para el humano local a partir de `ActorPrivateState`.
- Manos y tres espacios con selección por `0`, `1–3` y rueda.
- Nombre, pictograma y recurso legible para matamoscas, pantufla, raqueta eléctrica y aerosol.
- Estamina, carga de pantufla y espera de liberación.
- Confirmación explícita de reemplazo con `E`; inventario lleno sin espacio seleccionado pide elegir `1`, `2` o `3`.
- Textos de puerta, objeto y tareas alineados con la interacción vigente en `E`.

El aerosol presenta tiempo restante en segundos con formato español (`2,9 s`), sin exponer ticks internos. El HUD resuelve cada nombre y recurso por `PickupId` contra el ledger del snapshot, pero sólo muestra los tres identificadores del inventario privado local.

## Evidencia

- Compilación offline de `LetMeSleep.UI`, `LetMeSleep.Bootstrap` y `LetMeSleep.Tests.PlayMode`: 0 errores.
- Gate nativo HUD03: 3/3 PASS, 0 skip.
- XML: `N:/LetMeSleep/Validation/V020/equipment-hud-visual-03.xml`.
- Log: `N:/LetMeSleep/Validation/V020/equipment-hud-visual-03.log`.
- Receipt de tamaño real: `N:/LetMeSleep/Validation/V020/EquipmentHudVisual03/equipment-hud-visual-evidence.txt`.
- Capturas reales D3D11 de los estados factibles carga y reemplazo, en 1280×720 y 1920×1080, dentro de `N:/LetMeSleep/Validation/V020/EquipmentHudVisual03/`.

Las cuatro capturas verifican escala real de canvas/RenderTexture, ausencia de overflow, barra de carga visible, marcador ASCII de selección y oferta en dos líneas sin solape.

## Límites de la evidencia

La evidencia visual usa estados sintéticos válidos del HUD y no prueba un recorrido completo de recoger, cargar, lanzar o reemplazar durante una partida. La aprobación cubre layout funcional de esos estados; la revisión estética final y la validación de juego completo siguen separadas.
