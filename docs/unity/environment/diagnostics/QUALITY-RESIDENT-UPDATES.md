# Dos actualizaciones visibles del mismo asset

Ejecutar manualmente por Director, después de integrar11dd53b, en Humantraining Play con una sola casa y una única cámara de juego activa sin RenderTexture, excluyendo cámaras/escenas de preview. No requiere etiqueta MainCamera ni cambia tags:

`N:/LetMeSleep/Worktrees/environment/docs/unity/environment/diagnostics/ProbeQualityResidentUpdates.cs`

El script está fuera de Assets, no tiene autorun y no fue ejecutado por Elementos. Usa el método privado de producción PersistQualityMesh mediante reflexión; no mantiene una implementación paralela del arreglo.

Prueba acotada sobre Cushion_m0p57 (cojín azul): original → escala uniforme55% → escala uniforme80% → original. Las dos actualizaciones cambian posiciones de vértices dentro del mismo asset; escala alrededor de la base para conservar el apoyo. Las normales, topología, materiales, transformación del objeto y escena permanecen iguales. No vuelve a probar el cap de la manta.

Cuatro capturas1920×1080, misma cámara lateral(2.05,1,1.25) hacia(.85,.7,2.35), FOV60, dentro de una sola llamada síncrona y frame. Guarda cada PNG y dump; exige huellas de datos diferentes en ambas actualizaciones, píxeles diferentes, identidad/binding/GUID iguales y huella de datos/PNG original al restaurar. También compara controles de cámara, luces y materiales para no atribuir al mesh un cambio ajeno.

Antes de cada captura vuelve a exigir la misma cámara elegida y comprueba que la selección del helper AlfaReviewCapture (MainCamera o fallback sin RT) coincide con ella; si hay ambigüedad, aborta y restaura en lugar de cambiar tags o capturar otra cámara.

En finally restaura la malla original usando el mismo helper si una etapa falla, limpia el snapshot temporal y devuelve CameraPose, target y RenderTexture.active. Restaura el dirty flag previo sólo si los datos originales están íntegros. No invoca SaveAssets, SaveScene, reimport ni rebuild; exige hashes del asset y su .meta en disco iguales antes/después. Si hay fallo de restauración/control, deja el error en receipt.json y la llamada falla; no presentar el resultado como prueba válida.

Salidas: `N:/LetMeSleep/Validation/Alfa-VisualRecovery/quality-resident-updates-<UTC>/`, con00-original,01-update-55pct,02-update-80pct,03-restored y receipt.json. La igualdad exacta de PNG restaurado es un criterio estricto: si falla por estados temporales del renderer, revisar evidencia y controles antes de atribuir un fallo de datos.

Validación propia previa: compilación C# contra Unity6000.3.24f1 y Newtonsoft instalado, sin errores; advertenciaCS1701 por referencia netstandard2.0/2.1. La ejecución nativa y revisión posteriores se registran abajo.

## Resultado nativo y revisión — 2026-09-12

Director ejecutó la revisión del script 60c3e87 sobre central 2ce1fb2, Unity PID 37000. Evidencia: `N:/LetMeSleep/Validation/Alfa-VisualRecovery/quality-resident-updates-20260912-215607-255/receipt.json`. Elementos abrió las cuatro imágenes y contrastó sus SHA256 con el recibo: el cojín azul cambia visiblemente de original a 55%, luego a 80%, y recupera su tamaño original.

Las cuatro etapas conservan frame 480523, meshID 43304, filterID -78978, cameraID -83528 y GUID c5134a576eb744f44bc7690a69990771. Conservan 328 vértices y 280 triángulos de cuerpo. Los tamaños de bounds son (0.382868, 0.300309, 0.170943), (0.210578, 0.165170, 0.094019), (0.306295, 0.240247, 0.136754) y otra vez el original.

Todos los controles del recibo son verdaderos: dos actualizaciones distintas y visibles, identidad conservada, controles estables, cámara y dirty state restaurados, disco sin cambios, datos CPU y píxeles restaurados exactamente; error null. Original y restaurado comparten SHA256 de PNG `b45c5226a09f3d45a8b1a852c4d0e7ac74dbb85d40a077c8890a17a17c77001a` y de datos CPU `9715a753fd4259d10430be1e165dc17b910c97bbe7e5f5b3f5fc2c998ae315ef`.

Conclusión acotada: **PersistQualityMesh demuestra dos actualizaciones visibles de posiciones de vértices y restauración sobre la misma malla residente mediante la ruta de producción, sin modificar los archivos del asset.** La topología permanece igual en esta prueba: no acredita todos los casos de reemplazo de topología, no identifica la causa interna histórica del estado gráfico anterior y no aprueba el arte. El campo pending del recibo original queda satisfecho sólo en cuanto a revisión de las cuatro imágenes; el recibo se conserva intacto.
