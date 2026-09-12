# FacialContentBuilder — invalidación y bind pose

Base copiada del archivo central aún sin commit y su meta, por propiedad temporal exclusiva concedida por Director. No se modifica CharacterContentBuilder compartido, Bootstrap, importers ni asmdefs. Se replica su reconstrucción lógica de bind y se añaden comprobaciones. Sin ejecución nativa por este worker.

## Cambios

- BuildAll persiste UnityAxesVerified=false, LegacyScaleBlinkVerified=false, hash/revisión vacíos y Rig=null en los cuatro prefabs ANTES de cualquier validación/importación. Intenta todos aunque falle uno; cualquier error aborta la certificación. Si falla un gate posterior, vuelve a invalidar los cuatro para no dejar un lote parcialmente aprobado.
- Importa sincrónicamente los tres FBX conocidos con settings existentes (ForceUpdate). Este coste adicional es deliberado: centroides deben medir el import actual, no datos cacheados con un hash de otro archivo. Hash de archivo antes/después de cada import, hash de dependencia importada, comprobación antes/después de cada Bind y antes de terminar detectan cambios durante el lote; sólo certifica la identidad registrada al importar.
- Deshabilita Animator del prefab temporal durante reconstrucción y restituye enabled antes de guardar. Bind matrices world=renderer.localToWorld*bindpose.inverse, coherencia entre skins, orden explícito padre→hijo. Tras posición/rotación/escala comprueba recomposición; después verifica renderer.worldToLocal*bone.localToWorld*bindpose≈Identity en cada hueso. NaN/infinito, shear/reflexión no reconstruible y matrices inconsistentes fallan, no se compensan con offsets de ojos. Los centroides existentes usan vértices rígidos sólo después de este gate. El prefab guardado queda en bind; fallo descarta el contenido temporal.
- FacialContractImportGuard detecta import/reimport/settings que causan reimport, eliminación y movimiento de los tres FBX. Preprocess sólo deja dirty flag (SessionState + archivo Library/LetMeSleepFacialCertification.pending); Postprocess invalida antes de limpiar flag. No carga prefabs durante preprocessing. Imports de prefabs guardados no vuelven a disparar la condición de FBX.
- Si el postprocess no puede invalidar, registra excepción y programa un único retry de Editor.delayCall. El archivo Library conserva trabajo pendiente entre cierres; al cargar scripts se intenta una vez. No busy-loop. BuildAll llama FlushPending y falla si no se resuelve. Sólo elimina flag tras invalidación exitosa. Un delayCall tardío sin flag no invalida un lote ya comprobado.

## Verificación offline

Builder y postprocessor reales compilan contra Unity6000.3.24f1 y ensamblados centrales: cero errores/advertencias. Proyecto `N:/LetMeSleep/Worktrees/presentation/work/surface-r3-native/observer-package/FacialBuilder.csproj`, DLL `bin/Debug/netstandard2.1/FacialBuilderValidation.dll` en esa carpeta. No simula AssetDatabase ni constituye prueba de callbacks nativos.

## Próximo slot Director

1. Lote válido: cuatro marcadores de nueva identidad, centroides tras bind y mismos ejes importados; comprobar que ForceUpdate y callbacks terminan sin recursión.
2. Gate intencional fallido en una copia de prueba: ningún marker del lote conserva true/hash anterior. Confirmar que Bootstrap no continúa con lote fallido.
3. Reimportar un FBX ya certificado (incluido settings): markers false hasta rebuild. Borrar/mover sólo en copia de prueba para comprobar invalidación. Verificar que la marca pending desaparece únicamente tras guardar rechazo.
4. Reinicio con pending y caso prefab bloqueado/no escribible: excepción visible, pending conservado, BuildAll aborta; reintentar después de resolver acceso. No afirmar marker borrado si el sistema de archivos rechazó el guardado. La protección es editor/asset, no revoca drivers ya configurados de una sesión Play existente.
5. Cierre de párpados sigue pendiente por vértices/visibilidad real: centroide decide orientación candidata y no acredita cobertura. Esta corrección no cambia algoritmo de cierre, rig ni calidad de animación.

Límites: postprocessor cubre los tres paths exactos del lote actual; añadir modelos requiere actualizar la lista. El hash de fuente es FBX y el hash de dependencia se compara durante el lote, no es una firma criptográfica persistida del prefab. Un asset no escribible impide garantizar invalidación física: se propaga/retiene error y no se certifica el nuevo lote. No se altera ni termina Play automáticamente.
