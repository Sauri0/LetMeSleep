# AUDIO-001 — integración y mezcla para v0.2.0

Fecha: 2026-09-19. Propiedad del cambio: `unity/Assets/LetMeSleep/Audio/Runtime/**` y `unity/Assets/LetMeSleep/Presentation/Gameplay/GameplayAudioPresenter.cs`. No se editaron escenas, Bootstrap, builders ni assets generados; no se abrió Unity, Blender ni se reprodujo audio.

## Correcciones aplicadas

| Diferencia comprobada | Cambio | Efecto previsto |
|---|---|---|
| Godot ordenaba los zumbidos por distancia, limitaba seis voces y silenciaba las que quedaban a 8 m. Unity podía iniciar un loop por cada mosquito que volara o se aproximara; el límite efectivo dependía del asset/pool, sin seleccionar por oyente. | `Audio/Runtime/MosquitoBuzzPolicy.cs` y `GameplayAudioPresenter.cs` seleccionan los seis mosquitos aéreos más cercanos al actor local, hasta 8 m, con desempate estable por `ActorId`. Un mosquito posado, mordiendo, aturdido o fuera de alcance no produce loop. | Un enjambre no llena el mix ni revela actividad distante. Al acercarse o alejarse, los loops cambian mediante el fade de 40 ms ya existente en `AudioEmitterPool`. |
| `GameplayAudioPresenter.ApplyEvent` deduplicaba por epoch/ronda/ID, pero no descartaba un evento de una ronda anterior ni uno que llegara después del resultado. | `Audio/Runtime/GameplayAudioEventGate.cs` acepta eventos sólo después del primer snapshot de esa ronda, una sola vez y hasta `RoundFinished`; al disable/rebind se suspende y vuelve a activar la misma ronda sin borrar IDs aceptados. `GameplayAudioPresenter` lo usa sin cambiar sus APIs. | Un golpe, picadura, puerta o recuperación retrasados no deben sonar encima del resultado o de la siguiente ronda, y reactivar Presentation no repite eventos ya oídos. Los eventos producidos en el tick que termina la ronda siguen permitidos: Runtime entrega snapshot, eventos y después `RoundFinished`. |

Las dos mejoras preservan los disparadores existentes: pasos por madera/baldosa/tela, aterrizajes, salto, posarse/desprenderse, herramienta, golpe, picadura, caída, recuperación, puerta, inicio y resultado. No se agregaron sonidos para estados, mapas u objetos inexistentes.

## Evidencia de fuente

- Godot: `game/scripts/audio_fx.gd` ordena los candidatos por distancia, usa `MAX_BUZZ_VOICES`, corta a 8 m y sólo produce el loop adecuado al estado. `music_director.gd` usa actividad local, últimos 20 s y resultado sin inferir información oculta del enemigo.
- Unity: `GameplayAudioPresenter.cs` ya resuelve `AudioZone_*` a madera/baldosa/tela y usa `FootstepCadence`; el loop de alas sólo corresponde a `Flying`/`ApproachingSurface` sin attachment. `AlfaAudioDirector.cs` mantiene intensidad local y señales de fin; `AudioEmitterPool.cs` ya implementa pool, seguimiento y fade de 40 ms.
- La documentación heredada `AUDIO-FLIGHT-AND-FOOTSTEPS.md` confirma que los pasos deben depender de desplazamiento horizontal y que el zumbido no debe persistir en superficie, mordida, caída o recuperación.

## Validación realizada

- `AudioRuntimePolicyChecks.cs` compila las dos nuevas políticas fuera de Unity y pasó **16 checks**: evento antes de snapshot, duplicado, epoch/ronda obsoletos, suspensión/rebind sin replay, evento tardío, nueva ronda, seis voces, alcance, `NaN`/infinito, distancia inválida y cap de voces.
- `GameplayAudioPresenter.cs`, `FootstepCadence.cs` y las dos nuevas clases compilaron offline como assemblies separados contra los assemblies centrales y Unity `6000.3.24f1` sin errores.
- No se ejecutó Test Runner, PlayMode, editor, captura ni escucha. La compilación no acredita mezcla, volumen, espacialidad, assets, sincronía visual, FPS o WAN.

## QA auditiva posterior al turno Unity

1. En cada uno de los cinco mapas, escuchar desde humano y mosquito una aproximación aérea, seis o más mosquitos, salida de 8 m, posado, desprendimiento, picadura, caída, recuperación, despawn y fin de ronda. Confirmar que no queda loop después de resultados/menú ni que un mosquito distante entra al mix.
2. Caminar, correr, frenar, subir/bajar escalera y aterrizar sobre madera, baldosa y tela. Comparar dos ataques audibles por ciclo con las pantuflas renderizadas; repetir con jitter de snapshots, teletransporte y recuperación para detectar bursts.
3. Reproducir golpe sin impacto, golpe con impacto, puerta, pickup/drop, desmayo y resultado. Confirmar que los eventos del tick final se oyen antes del sting, y que paquetes retrasados posteriores no se oyen.
4. Escuchar menú → lobby → ronda → resultados → menú, personalización/ajustes y pausa. Verificar que los beds y UI no se duplican, que no persiste audio 3D después de salir y que la intensidad responde sólo a actividad local, tiempo final y progreso público.
5. Medir el mixer/cues generados tras el builder del CEO: routing de Music/Ambience/Critical/Character/Mosquito/UI, headroom y ducking. Registrar dispositivo de escucha, build/hash, mapa, rol, resultado y clips de captura. No declarar calidad por la compilación.

## Límites pendientes de audio para la versión completa

- Escucha humana y sincronía con clips/animaciones finales siguen abiertas.
- La voz cercana planeada para v0.2.0 no tiene módulo Unity ni validación de privacidad, hardware o WAN; debe entrar después del hito base con contrato propio.
- Supervivencia, Tareas, inventario/estamina, herramientas y personalización ampliada siguen como backlog de v0.2.0 tras Sangre + cinco mapas. Cada uno necesita eventos/cues y QA propios; esta entrega no los simula ni los excluye.
- Un build GitHub y la prueba con amigos entre redes independientes permanecen pendientes. El cambio no declara EOS, voz ni publicación como aprobados.

## S02 — entradas puntuales (20/09, gate nativo aprobado)

AlfaAudioDirector ya no inicia los tres loops musicales al comenzar la ronda.
Conserva el sting de inicio y los resultados. Una única frase de urgencia por
ronda usa los stems existentes durante cuatro segundos más su fade configurado;
se dispara sólo por tiempo público <=20s o Sangre >=85% de la cuota pública.
Golpes, picaduras y estados locales dejaron de controlar la música. Repetir
snapshots o volver a enlazar el mismo director no repite la frase; resultado y
salida cancelan su plazo. Esta duración es decisión CEO reversible para S02.
No certifica identidad musical final, balance auditivo, duración del sting de
resultado ni capas ambientales por mapa: los clips existentes siguen pendientes
de escucha/comparación. RoundMusicPlayModeTests: 2/2 PASS en tools-music-native-01.xml (gate completo6/6). Los stings actuales de resultado miden1,8s: S08 exige3s y sigue pendiente el reemplazo musical final.
### S08 — fuentes nuevas de tres segundos

Generador `art_source/unity/audio/generate_v020_stings.py`: dos motivos originales
con muestras glock/marimba/contrabajo del banco ya preservado y verificado por SHA.
No se llamó servicio remoto. WAV estéreo44.1kHz, duraciónexacta3s, pico−10dBFS,
RMS−22.90/−22.85dBFS y cola final en cero. Recibo de fuentes y notas en
`art_source/unity/audio/v020-result-stings.json`; originales alfa intactos.
V020AudioInstaller instaló ambas referencias mediante Unity (audio-stings-install-01,
exit0, installed2); el builder quedó apuntando a los archivos nuevos.
La escucha y mezcla dentro de la partida siguen pendientes: las medidas numéricas
no acreditan calidad subjetiva ni comprensión de voz durante el motivo.