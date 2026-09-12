# Implementación alfa de Presentation/Audio

## Lote disponible

El código fuente vive bajo `unity/Assets/LetMeSleep/Presentation/**` y
`unity/Assets/LetMeSleep/Audio/**`, en assemblies separados. Incluye:

- `AlfaPresentationPreset`, `AlfaFramePolicy` y `AlfaLightingRig`.
- `HumanViewCamera`, que aplica el pitch autoritativo sin reconstruirlo desde
  un vector, y `MosquitoFollowCamera`, con overlap y dos barridos sin asignar
  dirección de vuelo.
- `AudioCue`, pool acotado de 48 emisores, reproductor de loops y catálogo alfa.
- `GameplayAudioPresenter`, que se conecta al `GameplayRuntime` de W1, deduplica
  `(SessionEpoch, RoundId, EventId)`, traduce eventos confirmados y mantiene un
  único loop de alas por actor vivo.
- `GameplayVisualPresenter` y `ActorVisualBinding`: instancian los
  `CharacterView` de M1 sobre cada `GameplayActorProxy`, interpolan remotos con
  hasta 100 ms de extrapolación, seleccionan los 15 estados publicados, alinean
  manos con `BodySurfaces` y mantienen `ProboscisTip` en el ancla autoritativa.
- `GameplayPresentationRoot`, fachada de binding que desactiva la cámara
  auxiliar de W1 y conecta visuales, cámaras propias y eventos de audio.
- `GameplayVfxPresenter`, pool de ocho bursts URP para `StrikeImpact` y
  `MosquitoKnockedDown`; deduplica eventos y no crea decals de picadura.
- quince WAV audibles originales: música de menú/ronda, ambiente nocturno,
  alas, defensa, impacto, picadura, puertas, vida, stings y ready de UI.
- fuente editable determinista en `art_source/unity/audio/`, manifest con
  SHA-256 y copia byte a byte a Assets.
- `AlfaPresentationBuilder`, que crea materiales URP, Volume Profile, prefab de
  luz, AudioCue assets y prefab de audio mediante APIs de Unity.

El adapter de audio usa los assemblies reales `LetMeSleep.Gameplay` y
`LetMeSleep.Gameplay.Unity`. El binding visual se agrega al integrar
`LetMeSleep.Content.Characters`; no se incluye un substituto de `CharacterView`.

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

Ejecutar primero el builder de personajes de M1. Con sus cuatro prefabs
presentes, este builder también crea
`Assets/LetMeSleep/Presentation/Generated/Prefabs/LMS_GameplayPresentation.prefab`
con una cámara compartida, AudioListener, audio root y los dos presenters. La
escena instancia ese prefab y llama una vez:

```csharp
presentationRoot.Bind(gameplayRuntime);
```

El binding incluye actores que ya existían antes de la llamada y también los
futuros `ActorCreated`. Visuales y loops consumen `SnapshotApplied`, que W1
emite tanto para host como para réplicas; `SnapshotReady` queda reservado a la
salida de red. `GameplayRuntime.UseBuiltInCamera` queda en `false`; la
vista local lee `LocalViewYaw/LocalViewPitch` cada frame, por lo que no depende
de que `ViewRevision` cambie con cada movimiento del mouse.

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
