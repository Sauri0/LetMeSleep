# Captura de entrenamiento real — evidencia provisional

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
