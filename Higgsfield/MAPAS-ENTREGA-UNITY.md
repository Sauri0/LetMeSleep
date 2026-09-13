# Cinco mapas Higgsfield — entrega para Unity

Actualización de mapas terminada y validada localmente en Unity 6000.3.24f1. Los veinte bocetos aprobados se construyeron con Higgsfield Scene Builder en Blender. Las correcciones posteriores de navegación, colisión, materiales, agua e iluminación están documentadas y conservan las fuentes originales.

## Abrir y probar

Proyecto: `N:/LetMeSleep/Repository/unity`.

Abrir `Assets/Scenes/LetMeSleepHiggsfield.unity`, pulsar Play y elegir Entrenamiento, mapa y especie. El catálogo también alimenta el selector de sala; el anfitrión determina el mapa y la nueva escena comienza las salas con la primera entrada del catálogo. Cambiar el mapa reinicia Listo mediante la autoridad de sala existente. La conexión por Internet no se certifica con estas pruebas locales.

Catálogo: `Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset`.

| Mapa | ID final | Contenido |
|---|---|---|
| Isla del laguito | hf-isla-del-laguito-v2 | Isla diurna, lago, cabaña, senderos, vegetación y muelles. |
| Casa del patio | hf-casa-del-patio-v1 | Casa nocturna de dos plantas, interiores, escaleras, porche y patio. |
| Campamento del pinar | hf-campamento-pinar-v2 | Noche, seis carpas, fogata, refugio, bosque, arroyo y puentes. |
| Yate a la deriva | hf-yate-a-la-deriva-v3 | Casco, cubiertas, escaleras, camarotes, salón y cocina, océano animado. |
| Puerto del faro | hf-puerto-del-faro-v1 | Tres viviendas, taller, plaza, muelles, embarcaciones y faro con escalera interior. |

Índice visual y fuentes: `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/ENTREGA.html`. Incluye veinte bocetos, vistas de Blender, capturas Unity y enlaces a Blender, GLB y FBX. Casa, Yate y Puerto tienen copias Blender ajustadas preferidas; los exports anteriores se conservan y se identifican expresamente, sin afirmar que incorporan ajustes posteriores. Los prefabs instalados son la entrega jugable de referencia.

## Validación realizada

- Diez sesiones de entrenamiento reales por la API de la aplicación, cinco mapas por dos especies: identidad, runtime, navegación, tres actores con posiciones finitas, cámara real y retorno al menú. XML final: `N:/LetMeSleep/Validation/Higgsfield/FiveMapGameLoading-20260913/run-night-final/results.xml`, PASS 1/1. Diez capturas y detalle en `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/UnityPackage/GameLoadingFinal`.
- Isla: 48/48 recorridos y 263 conexiones. Casa: 44/44 y 17 conexiones. Yate: 49/49 y 27 conexiones. Puerto: 49/49 y 57 conexiones, 16/16 recorridos Runtime. Recibos exactos en los informes de `docs/unity/gameplay/Higgsfield` y `N:/LetMeSleep/Validation/Higgsfield`.
- Campamento: 48/49 bajo el umbral estricto de contacto, 25/25 conexiones y 16/16 Runtime; todos los recorridos completan. Excepción aceptada por coordinación: penetración transitoria de 2,326175 mm frente al contrato de 2 mm, con penetración final cero. No se cambió el umbral ni se informa 49/49.
- Agua animada CPU/GPU comprobada en Unity, incluido movimiento de píxeles para GPU y restauración de materiales/mallas. Los límites CPU originales se conservan; el océano del Yate usa GPU.
- Iluminación final: cinco mapas con bind/rebind/unbind, sin duplicados ni residuos; archivos y estado de escena restaurados. Recibo `UnityPackage/lighting-receipt-night-final.json`. Casa y Campamento tienen exposición y luz nocturnas revisadas en cuatro capturas de juego.
- UI de entrenamiento y de sala comprobada a 720p y 1080p; selección por autoridad, confirmación, rechazo y legibilidad. Las pruebas de sala usan acciones y participantes simulados. Contratos de sala nativos: 29/29.

La evidencia no equivale a una partida completa por Internet, un benchmark de FPS ni una build distribuible. La escena original `LetMeSleepBoot.unity` y Build Settings permanecen conservados. El trabajo previo de Humanos y alfa sigue separado; no se realizó un reset o limpieza global del repositorio.

## Detalles de acabado registrados

La revisión de cinco vistas generales y veinte bocetos no encontró bloqueos visuales P0/P1 en esas imágenes. Quedan dos observaciones menores: el patrón radial del agua del Yate desde arriba y el peso visual del acantilado del Puerto. Informe: `N:/LetMeSleep/Validation/Higgsfield/VisualFinal/MAPAS-FINAL-REVIEW.md`.

Saldo Higgsfield observado después de las construcciones: 1.233,59 créditos. Los ajustes técnicos y la validación final no solicitaron nuevas generaciones. El saldo es una observación, no una factura individual de cada mapa.

Los cambios temporales de pruebas en Atkinson y EditorSettings se respaldaron y restauraron a su contenido anterior. Recibo: `N:/LetMeSleep/Validation/Higgsfield/FixtureSourceCheckpoint/restore-receipt.json`.

El Director puede retomar la coordinación con esta escena y estos cinco IDs. Para preparar una publicación, debe conservar los límites de evidencia anteriores y comprobar build, rendimiento e interacción online en el entorno de destino.
