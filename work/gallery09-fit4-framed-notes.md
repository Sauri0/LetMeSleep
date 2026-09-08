# Calibración fit4: encuadre completo de la cabeza

Estado: tres páginas nativas terminadas y verificadas. Las doce hojas fueron inspeccionadas por encuadre; no se detectaron recortes de cabeza, antenas o probóscide. Esto no aprueba todas las combinaciones, la malla en movimiento ni la galería completa. Resultado y métricas en `outputs/0.9-facial-gallery-fit4-framed/CALIBRACION.md`.

La calibración anterior se conserva en `outputs/0.9-facial-gallery-fit4`. Sus tres páginas capturaron 51 cabezas, 408 vistas y 12 hojas en 33,323 segundos. La inspección encontró puntas de antenas cortadas en la vista inferior oblicua del mosquito. El control anterior de presencia de píxeles no podía detectar ese defecto.

Esta revisión mantiene 24 cabezas por página, ocho ángulos, tiles de 480 × 560, hojas de 3840 × 3360, ordinales y combinaciones. Sólo modifica la cámara del fixture de catálogo. No cambia AvatarPreview, cámara de juego, geometría ni materiales.

El encuadre incluye cabeza, ojos, boca, cejas, pelo/antenas, accesorios y vello visible. En el mosquito incluye expresamente el hueso `proboscis`, hijo de `head`, y también admite una malla separada de probóscide. Alas y abdomen no determinan el encuadre y pueden aparecer parcialmente.

Los puntos deformados combinan el bake nativo del esqueleto con el delta de morph transformado por la misma paleta. Se comprueba la coincidencia de la base contra el bake nativo, con tolerancia de 0,15 mm. Se calcula un centro y una distancia comunes a las ocho vistas de cada combinación/estado; los vértices proyectados deben conservar 20 px de margen en el viewport útil de 480 × 496. Cada vista registra rectángulos, conteos y error del bake. Un conteo positivo identifica la probóscide, evitando omitirla silenciosamente.

El informe pasa a esquema 4. El validador rechaza informes previos sin estos controles, incluso si ya contenían ocho ángulos. Los tests sintéticos del validador no son imágenes del juego ni aprobación artística.

Comandos empleados tras cesión del motor (el plan existente no se sobrescribe):

```powershell
python -B work/gallery09-validator-tests.py
# Comprobar primero el parse del fixture en Godot; crear el plan después.
python -B work/gallery09-manifest.py plan --plan work/gallery09-fit4-framed-plan.json --output outputs/0.9-facial-gallery-fit4-framed
& work/gallery09-fit4-framed-calibrate.ps1 -Execute
```

El wrapper selecciona sólo las páginas `neutral-human-000`, `neutral-mosquito-000` y `neutral-mosquito-010`, con límite de 55 segundos por proceso. No lanza automáticamente las 133 páginas. Medirá nuevamente el coste del encuadre y del bake antes de extrapolar; la revisión visual sigue separada de la captura validada.
