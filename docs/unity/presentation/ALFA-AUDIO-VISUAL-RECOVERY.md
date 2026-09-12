# Recuperación audiovisual de alfa

Este documento registra la fuente recuperada, su conexión al runtime y la
receta de verificación. No reemplaza una escucha ni una revisión visual humana.

## Música y contextos

| Contexto | Clips | Comportamiento |
|---|---|---|
| Menú y lobby | `MUS_Legacy_Menu_Base`, `Menu_Rhythm`, `Menu_Melody` | Los tres stems empiezan en el mismo tiempo DSP y cruzan desde otros contextos sin rebobinar llamadas repetidas. |
| Partida | `MUS_Legacy_Gameplay_Base`, `Gameplay_Rhythm`, `Gameplay_Melody` | Base continua; ritmo y melodía cambian entre calma, actividad local y urgencia. Urgencia comienza con 20 s restantes o 85 % del objetivo de sangre. |
| Personalización y ajustes | `MUS_Legacy_Quiet` | `EnterQuietMenu()` separa el tema tranquilo del menú principal. |
| Ambiente | `AMB_NightHouse`, `AMB_Legacy_NightAir` | Dos beds continuos en el grupo `Ambience`. |

`AlfaAudioDirector` expone `EnterMenu`, `EnterQuietMenu`, `EnterRound`,
`FinishHumansWin`, `FinishMosquitoesWin` y `StopAll`. `EnterRound` es
idempotente: la llamada de Bootstrap y el primer snapshot no duplican
`RoundStart`. Desactivar un `AudioBedPlayer` detiene su fuente y resetea su
estado, por lo que el menú puede volver a iniciarse después de una ronda.

## Catálogo y disparadores

| Familia | Clips o cue | Fuente de reproducción |
|---|---|---|
| Defensa | `StrikeSwing` con `SFX_StrikeSwing`/`Legacy_Swish`; `StrikeImpact` con impacto, clap y variante anterior | `StrikeStarted` y `StrikeImpact`, deduplicados por época, ronda e ID de evento. `MosquitoKnockedDown` usa stun/fall aparte y no duplica el impacto. |
| Mosquito | `Buzz_Flight`, `Buzz_Perch`, `Buzz_Bite`; `Perch`, `Detach`, `BiteStarted` | Estado continuo y cambios autoritativos de attachment. Cambiar estado apaga el loop anterior en 40 ms. |
| Humano | `Step_Wood`, `Step_Tile`, `Step_Cloth`; `Land_*`; `Cloth` para salto | Cruces de `MotionPhase` y cambios de `Grounded`. La superficie se elige por el `AudioZone_*` más cercano dentro de `World.MapRoot`: dormitorios son tela; cocina, baño y lavadero son baldosa; el resto es madera. |
| Herramienta alfa | `Pickup`, `Drop` | Cambio de propietario y revisión de `ToolPickupSnapshot`; el primer snapshot sólo inicializa y no reproduce historia. |
| Puertas | WAV alfa más `Door_Move`/`Door_Latch` | `DoorChanged` en la posición autoritativa. |
| Ronda | `RoundStart`, `HumansWin`, `MosquitoesWin` | Cambio de contexto y resultado. |
| UI | `UI_Legacy_Select`, `Confirm`, `Error` | `PlayUiSelect`, `PlayUiConfirm`, `PlayUiError`, con cooldown compartido de 65 ms. |

Todos los one-shots 3D pasan por `AudioEmitterPool`, respetan el límite de 48
voces y conservan mixer group, prioridad, distancia y curva de atenuación. La
procedencia CC0 y el origen binario se detallan en
`LEGACY-AUDIO-PROVENANCE.md`.

## Iluminación recuperada

Las luces interiores se proyectan hacia abajo desde los anchors de luminaria
como spots de 125 grados, inner cone de 80 grados. Patio conserva una point
light. El lobby usa luces cálidas locales de intensidad 0.48/rango 3.6, un fill
direccional frío de intensidad 0.55 y un spot frontal suave de intensidad
0.72/rango 8.5 alineado con `MainMenuCamera`; ninguno proyecta sombras. Ambos
fills se desactivan en la casa. El volumen global usa bloom 0.025, threshold
1.35 y scatter 0.35.

La captura de diagnóstico previa al ajuste es
`N:/LetMeSleep/Validation/Alfa-VisualRecovery/menu-1080.png`, 1920×1080,
465226 bytes, SHA-256
`6373597f92acc2ca0cddd31f6af8b18d1361ec9f2009a0ec8a4ad5b7d2f3bdd1`.
Allí el humano y el mosquito quedan subexpuestos, mientras los faroles queman
la pared. Esa imagen justifica el fill, la reducción de luz cálida/bloom y la
reducción de emisión que M2 aplica en la fuente del farol. No acredita el
resultado posterior.

## Receta de generación y verificación

1. Integrar las fuentes de Environment, Characters, UI y Presentation/Audio.
2. Abrir el proyecto con Unity 6000.3.24f1 y `-noaudio`.
3. Ejecutar `Tools/Let me sleep/Build Alfa Presentation Library` después de los
   builders de Environment y Characters. Debe terminar con
   `LMS_ALFA_PRESENTATION_LIBRARY_BUILT`, sin cues sin mixer y sin clips nulos.
4. Confirmar en `LMS_AlfaAudioRoot.prefab` los nueve beds, los cues restaurados
   y las referencias del catálogo. Confirmar en `LMS_AlfaLightingRoot.prefab`
   `Lobby_CharacterFill` y las dos plantillas de luces locales. El
   `LMS_LobbyCameraFill` se crea en runtime al enlazar `PrivateLobby`.
5. Ejecutar tests EditMode con audio deshabilitado. Recorrer después menú,
   entrenamiento humano y mosquito en un build candidato, con escucha humana
   coordinada. Verificar retorno de ronda a menú, cambios de zumbido, dos pasos
   por ciclo, salto/aterrizaje, pickup/drop, puertas y stingers.
6. Capturar menú, living, pasillo y dormitorio a 1920×1080. Deben verse caras,
   siluetas y props sin halos quemados, el techo sin elipse puntual y las
   luminarias físicamente presentes. Registrar hashes de las imágenes y del
   log del builder.
