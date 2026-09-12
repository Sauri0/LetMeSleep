# Dos actualizaciones visibles del mismo asset

Ejecutar manualmente por Director, después de integrar11dd53b, en Humantraining Play con una sola casa y Camera.main:

`N:/LetMeSleep/Worktrees/environment/docs/unity/environment/diagnostics/ProbeQualityResidentUpdates.cs`

El script está fuera de Assets, no tiene autorun y no fue ejecutado por Elementos. Usa el método privado de producción PersistQualityMesh mediante reflexión; no mantiene una implementación paralela del arreglo.

Prueba acotada sobre Cushion_m0p57 (cojín azul): original → escala uniforme55% → escala uniforme80% → original. Las dos actualizaciones cambian posiciones de vértices dentro del mismo asset; escala alrededor de la base para conservar el apoyo. Las normales, topología, materiales, transformación del objeto y escena permanecen iguales. No vuelve a probar el cap de la manta.

Cuatro capturas1920×1080, misma cámara lateral(2.05,1,1.25) hacia(.85,.7,2.35), FOV60, dentro de una sola llamada síncrona y frame. Guarda cada PNG y dump; exige huellas de datos diferentes en ambas actualizaciones, píxeles diferentes, identidad/binding/GUID iguales y huella de datos/PNG original al restaurar. También compara controles de cámara, luces y materiales para no atribuir al mesh un cambio ajeno.

En finally restaura la malla original usando el mismo helper si una etapa falla, limpia el snapshot temporal y devuelve CameraPose, target y RenderTexture.active. Restaura el dirty flag previo sólo si los datos originales están íntegros. No invoca SaveAssets, SaveScene, reimport ni rebuild; exige hashes del asset y su .meta en disco iguales antes/después. Si hay fallo de restauración/control, deja el error en receipt.json y la llamada falla; no presentar el resultado como prueba válida.

Salidas: `N:/LetMeSleep/Validation/Alfa-VisualRecovery/quality-resident-updates-<UTC>/`, con00-original,01-update-55pct,02-update-80pct,03-restored y receipt.json. La igualdad exacta de PNG restaurado es un criterio estricto: si falla por estados temporales del renderer, revisar evidencia y controles antes de atribuir un fallo de datos.

Validación propia: compilación C# contra Unity6000.3.24f1 y Newtonsoft instalado, sin errores; advertenciaCS1701 por referencia netstandard2.0/2.1. **Pendiente ejecución nativa por Director y revisión de las cuatro imágenes.** La prueba no aprueba el arte y no acredita por sí sola actualización de topología; demuestra dos cambios geométricos visibles en una malla residente mediante la ruta de producción.
