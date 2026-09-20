# Captura de entrenamiento real — evidencia provisional

## Resultado posterior aceptado: diez vistas con HUD

Smoke11 `Run-20260920-171529-439`: 1/1 PASS, Yate/humano inspeccionado por CEO.
Full12 `Run-20260920-171633-335`: 1/1 PASS con diez vistas, cinco mapas por ambos
roles, todas inspeccionadas por CEO. Texto, reloj, rol, ayuda y panel humano
legibles, sin oclusión del hint por la baranda. No se observa clipping de cámara
en esas posiciones de inicio. No acredita todas las posiciones ni movimiento.

La solución del capturador conserva el render del mundo y dibuja el HUD real en
una cámara Overlay temporal del stack URP. Necesita emitir explícitamente la
geometría UI de esa cámara antes del request. La API está documentada por
[Unity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rendering.ScriptableRenderContext.EmitGeometryForCamera.html).
El contraste entre intentos09/10 observó el cambio de cero píxeles HUD a 266461
en Yate sin cambiar el juego. No se pintaron ni compusieron textos en PNG.

El control sin HUD y el compuesto se renderizan síncronamente con la misma
cámara/pose del jugador. Los diez compuestos verifican cambio en RoleBadge,
ClockBadge y ContextHintPanel, y conservación del resto del mundo. El fixture
restaura modo Canvas, cámara, layers, target y configuración originales.

| Intentos posteriores | Resultado |
|---|---|
| 05 / 165212-753 | FAIL por comparar la pose previa a un frame de seguimiento normal; sin evidencia visual aceptada. |
| 06 / 165614-122 | FAIL correcto: el pase HUD borraba el color del mundo. HUD aislado visible, sin composición aceptada. |
| 07 / 170026-933 | FAIL: stack sin píxeles HUD. |
| 08 / 170630-167 | FAIL diagnóstico: Overlay activa y renderizada, Canvas/TMP con geometría, cero píxeles HUD; viewport auxiliar 640×480. |
| 09 / 170934-738 | FAIL: alinear target/viewport no bastó. |
| 10 / 171325-559 | Compuesto visualmente correcto; FAIL por comparar restauración contra componente temporal en lugar del estado original. |
| 11 / 171529-439 | Smoke 1/1 PASS, restauración corregida y compuesto inspeccionado. |
| 12 / 171633-335 | Full 1/1 PASS, diez compuestos inspeccionados. |

Las carpetas están bajo GameplayVisualCapture01 y conservan todos los intentos.
Personajes antiguos siguen visibles: esto no aprueba el arte final ni acredita
animaciones, FPS, audio o WAN.

Repositorio observado: `0671c6d`. Unity 6000.3.24f1, D3D11, 1920×1080.
Harness externo en `N:/LetMeSleep/Validation/V020/GameplayVisualCapture01`.
Importación temporal de un test y su meta; ambos retirados al finalizar cada
ejecución. No se modificó la presentación del juego para producir estas imágenes.

El ensayo inicia entrenamiento Sangre con cámaras y HUD reales de los cinco mapas,
desde humano y mosquito. Comprueba identidad del mapa, snapshot, apoyo inicial,
cámara y píxeles no negros. Esas condiciones no demuestran que el HUD se haya
dibujado: la revisión de PNG es necesaria y prevalece sobre el PASS del XML.

| Intento / carpeta Run | Ejecución | Revisión visual |
|---|---|---|
| 20260920-163303-329 | 1/1, diez PNG | CEO inspeccionó los diez. Mundo y texto HUD visibles; al convertir Canvas Overlay a Camera a 1 m, una baranda de Yate tapa el hint. No aceptar fidelidad completa del HUD. |
| 20260920-163600-671 | 1/1, diez PNG | Acercar Canvas al near clip hizo desaparecer el texto TMP. Rechazado. |
| 20260920-163843-239 | 0/1 | Setter inexistente de clearDepth de URP; no captura aceptada. |
| 20260920-164328-871 | 1/1, diez PNG | Cámara overlay temporal con clearDepth comprobado. Yate humano inspeccionado: mundo visible, HUD ausente. Rechazado para validar interfaz. |

Cada carpeta conserva XML, log, configuración y manifest. El manifest PASS y
hudActive sólo acreditan las comprobaciones programadas, no visibilidad efectiva.
El siguiente intento requiere un smoke de un mapa con verificación de composición
del HUD antes de repetir diez capturas. Propiedad: personalizacion_red_v020;
ninguna ejecución Unity sin turno exclusivo.

Las imágenes del primer intento muestran los escenarios existentes y personajes
antiguos. No equivalen a arte nuevo aprobado, rondas completas, animaciones,
rendimiento, audio, prueba WAN ni entrega v0.2.0 final.
