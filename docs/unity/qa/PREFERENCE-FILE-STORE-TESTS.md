# Pruebas de persistencia segura — PreferenceFileStore

## Objetivo

Cerrar la parte de almacenamiento de U094-15 sin Unity ni datos del perfil real. La suite usa directorios temporales aislados y un clasificador mínimo para probar el contrato puro de archivos de `PreferenceFileStore`.

## Casos

1. Perfil nuevo y segundo guardado: activa el documento nuevo y conserva el anterior en `.backup`.
2. Primario malformado: recupera el backup actual sin modificar ningún archivo durante `Load`.
3. Guardado tras recuperación: activa el documento nuevo, conserva el backup sano y archiva el primario dañado en `.rejected-*`.
4. Versión superior en primario o backup: bloquea escritura incluso con una instancia nueva y deja bytes/nombres intactos.
5. Límite UTF-8: acepta exactamente 16,384 bytes; rechaza 16,385 sin cambiar el primario. Un primario sobredimensionado puede recuperar un backup actual.
6. Fallo de E/S durante `File.Replace`: conserva el primario previo y elimina el temporal sin activación parcial.

## Ejecución externa

Comando:

```powershell
powershell -NoProfile -File docs/unity/qa/Run-PreferenceFileStoreExternalHarness.ps1
```

El harness compila únicamente NUnit, `PreferenceFileStore.cs` desde el worktree indicado y `PreferenceFileStoreTests.cs`. No carga UnityEngine, editor, escena, perfil del usuario ni build del juego.

Resultado del 12 de septiembre de 2026 contra `N:/LetMeSleep/Worktrees/director-persistence`: **8 pruebas, 0 fallos**. El recibo `PREFERENCE-FILE-STORE-EXTERNAL-RESULT.json` fija SHA-256 de runtime, test y harness; la fuente runtime todavía no estaba confirmada en un commit al ejecutar.

## Reproducción negativa

El mismo harness genera cuatro copias mutadas sólo dentro de `.codex-tmp-preference-store-tests`, exige que fallen y elimina el directorio al terminar:

- quitar la devolución del backup válido;
- volver a usar `.backup` al reemplazar un primario malformado;
- retirar la comprobación en disco que impide sobrescribir una versión futura.
- retirar la revalidación del backup futuro cuando `Save` se invoca sin un `Load` previo.

Resultado: **4/4 mutantes rechazados**. Por tanto la corrida positiva distingue las dos regresiones originales y ambas protecciones contra downgrade; no es un test que pase sólo porque el archivo se puede escribir.

## Alcance

PASS para atomicidad y recuperación del almacenamiento puro en el sistema de archivos Windows probado. No acredita `JsonUtility`, saneamiento de campos, mensajes UI, permisos de otros sistemas operativos, fallo físico de disco, proceso terminado a mitad de syscall ni persistencia dentro del ejecutable publicado. La importación completa desde Godot corresponde a gamma; alfa debe dejar esos datos intactos. U094-15 continúa BLOCK hasta Unity EditMode y smoke del build con perfiles limpio, corrupto y de versión futura.
