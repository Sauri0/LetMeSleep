# Implementación alfa de Presentation/Audio

## Lote disponible

El código fuente vive bajo `unity/Assets/LetMeSleep/Presentation/**` y
`unity/Assets/LetMeSleep/Audio/**`, en assemblies separados. Incluye:

- `AlfaPresentationPreset`, `AlfaFramePolicy` y `AlfaLightingRig`.
- `HumanViewCamera`, que aplica el pitch autoritativo sin reconstruirlo desde
  un vector, y `MosquitoFollowCamera`, con overlap y dos barridos sin asignar
  dirección de vuelo.
- `AudioCue`, pool acotado de 48 emisores, reproductor de loops y catálogo alfa.
- quince WAV audibles originales: música de menú/ronda, ambiente nocturno,
  alas, defensa, impacto, picadura, puertas, vida, stings y ready de UI.
- fuente editable determinista en `art_source/unity/audio/`, manifest con
  SHA-256 y copia byte a byte a Assets.
- `AlfaPresentationBuilder`, que crea materiales URP, Volume Profile, prefab de
  luz, AudioCue assets y prefab de audio mediante APIs de Unity.

No se incluye un adapter ficticio de gameplay. Se agrega cuando los assemblies
`LetMeSleep.Gameplay` y `LetMeSleep.Content.Characters` estén integrados, usando
sus contratos concretos.

## Ejecución en Unity

Después de integrar el commit en el proyecto residente, esperar una compilación
limpia y ejecutar:

```text
Tools > Let me sleep > Build Alfa Presentation Library
```

El equivalente batch es:

```text
-executeMethod LetMeSleep.Presentation.Editor.AlfaPresentationBuilder.BuildFromCommandLine
```

El builder es idempotente y sólo escribe dentro de los `Generated/` que le
pertenecen a Presentation y Audio. Director inserta sus prefabs aditivos en las
escenas; el builder no abre ni guarda escenas de M2, no toca ProjectSettings y
no modifica paquetes.

## AudioMixer

El API público de editor permite consultar y asignar un AudioMixer, pero no
expone una construcción estable de su grafo de grupos, snapshots, sends y
ducking. Director crea una vez
`Assets/LetMeSleep/Audio/Generated/LMS_AlfaMixer.mixer` con la jerarquía de
`AUDIO-ALFA.md`. Al volver a ejecutar el builder, éste resuelve y asigna por
nombre los grupos `Music`, `Ambience`, `Critical`, `Character`, `Mosquito` y
`UI`. Si el mixer o un grupo falta, emite un diagnóstico con path/nombre y los
clips siguen audibles por Master para permitir inspección; ese fallback no
aprueba el gate de integración.

## Evidencia actual

La verificación sin editor comprueba:

- sintaxis C# contra assemblies de Unity 6000.3.24f1 y URP 17.3.0;
- todos los `.meta` de código, carpetas y clips;
- 15 WAV PCM 48 kHz/24 bit y hashes fuente/runtime idénticos;
- JSON/asmdef parseables y alcance limitado a alfa.

Director aún debe importar y ejecutar el builder en Unity residente. Capturas,
escucha de mezcla, validación de shader/material, bake y perfil GTX 1660 Ti
siguen pendientes de evidencia real.
