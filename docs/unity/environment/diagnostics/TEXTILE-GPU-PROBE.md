# Prueba manual de subida GPU de textiles

Solicitada por Director después de integrar banco4a62d6b comoed18ab2 y MapasEX2 como3ee30e9; todavía pendientes de rebuild. No modifica fuentes del juego. El archivo C# está fuera de Assets, sin autorun, y sólo se ejecuta por `eval_file` en el próximo turno Unity concedido al Director.

Ejecutar `N:/LetMeSleep/Worktrees/environment/docs/unity/environment/diagnostics/ProbeTextileGpuUpload.cs` con Humantraining en Play y una sola casa activa. No necesita una captura externa entre pasos: ambas solicitudes de render URP ocurren sin ceder el control dentro de la misma llamada.

Secuencia:

1. Exige exactamente siete piezas textiles de una sola instancia activa y mallas legibles. Conserva referencias de MeshFilter/sharedMesh, cámara y destino de render originales.
2. Fija esa cámara en el lateral conocido: posición(2.05,1.0,1.25), objetivo(0.85,0.7,2.35), FOV60; usa el mismo RenderTexture1920×1080 en ambas capturas. No cambia lámpara, shader, material, exposición ni UI.
3. Guarda `before.png` y `before.json`; comprueba que el primer render no haya alterado los controles o datos CPU.
4. Llama exclusivamente `UploadMeshData(false)` una vez por malla textil única. No recalcula normales, no reasigna mallas y no usa setters de geometría.
5. Guarda `after.png` y `after.json`, compara identidades, huellas CPU, transformaciones, parámetros de cámara, luces, materiales y estados dirty. En `finally` restaura posición/rotación/FOV/destino de cámara y RenderTexture.active, y libera las texturas temporales incluso ante error.
6. Guarda `receipt.json` con controles/restauración, diferencias RGB y hashes PNG. Si falla un control, la llamada informa error y no presenta la pareja como comparación controlada. Un fallo intermedio deja `failure.json` junto a la evidencia parcial.

Salidas en una carpeta nueva `N:/LetMeSleep/Validation/Alfa-VisualRecovery/textile-gpu-probe-<UTC>`; no sobrescribe las capturas crafted3. Los dumps contienen instancia/cámara/mesh IDs, jerarquías, paths, vértices/triángulos, estadísticas y huellas de vértices/normales/UV/UV2/índices, materiales y luces. El script rechaza bloques de propiedades textiles desconocidos porque no podría controlar su contenido completo.

No invoca SaveAssets, SaveScene, reimport, rebuild, SetDirty ni cambios de binding; el único cambio gráfico intencionado es subir datos CPU existentes a GPU. Restaura el estado temporal de captura, pero no intenta reconstruir un posible buffer GPU obsoleto para deshacer la prueba. No modifica el contenido persistido.

Interpretación: revisar visualmente ambos PNG y exigir controles/restauración verdaderos. Un cambio de píxeles por sí solo no demuestra la causa original (pueden intervenir estados temporales del renderer), y los datos CPU correctos no aprueban el arte. Si la silueta/normales visibles se actualizan sólo tras la subida, esa evidencia orientará el arreglo persistente del builder; si no cambian, no declarar esta hipótesis confirmada.

Validación realizada: script envuelto como método C# compilado contra Unity6000.3.24f1, URP/Core y Newtonsoft del proyecto central. Una advertencia CS1701 de compatibilidad netstandard2.0/2.1 de Newtonsoft; sin errores de compilación. **No ejecutado en Unity; capturas, controles y cleanup nativos aún pendientes.**
