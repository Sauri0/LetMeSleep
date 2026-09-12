# SurfaceWalk — contrato recibido y dependencia de fase

2026-09-12. Fuente Mosquitos 11a2cc9f214060200bbaec25639573f3a4961fd9, leída CANDIDATE-RECIPE-20260912.md. Estado SOURCE_ONLY: no atribuir al FBX previo el contrato nuevo.

Contrato: Mosquito_SurfaceWalk id6; 31 frames a 30 FPS, duración1s; stride .026 m fuente durante duty .65; escala Unity .5 aplicada una vez. Distancia de avance compatible por ciclo=.026/.65*.5=.020m. El analítico del autor no sustituye evaluación/exportación nativa.

ActorVisualBinding leído conserva MosquitoStrideMeters=.3 compartido por motion2 y6; limita Animator.speed a .35..2.5. ResolveMotion elige marcha de superficie sólo con velocidad>.08m/s. Sustituir .3 por .020 de manera aislada requeriría speed/.020>4 ciclos/s ya al umbral de entrada y toparía siempre en2.5; por tanto esa sustitución no satisface el contrato. Es deducción del código, no patinaje medido en video.

Además SynchronizeLoopPhase de actores remotos compara normalizedTime con MotionPhase autoritativa y fuerza Play si error>.20 ciclo; cambiar solamente velocidad del Animator puede introducir correcciones repetidas si la fase autoritativa sigue otra distancia por ciclo. La entrada inmediata también usa MotionPhase. Vuelo id2 debe conservar tratamiento separado.

Coordinación enviada a Gameplay para confirmar archivo/fórmula de integración de MotionPhase y velocidad normal Surface, antes de decidir transformación/acumulación de fase visual. No cambiar velocidades/reglas de juego ni longitud de patas unilateralmente. Una eventual reproducción rápida debe medirse por frame: aliasing y número de muestras por ciclo dependen del FPS real, no sólo de coincidencia matemática.

SurfaceVisualProbe de Gameplay entregado al Director en9426435a2d1035b5e5bc8cb54849989c7617753c, paquete N:/LetMeSleep/Validation/SurfaceVisual-20260912/probe-20260912-195958-387-6f7b3f20. Autor informa compilación offline sin errores/advertencias; no es ejecución nativa. Próxima evidencia: velocidad real, MotionPhase, normalizedTime/mezclas, escala y mesh/bones, mínimos de vértices por pata y delta tangencial durante apoyo, en recorrido continuo separado de checkpoints manuales.

Sin modificación de ActorVisualBinding ni fc3d97b. Pendientes: respuesta de Gameplay, FBX/export Mosquitos y probe ejecutado por Director. No se certifica apoyo/patinaje ni se aplica una constante que quedaría limitada por el clamp.

## Confirmación de Gameplay y decisión pendiente

Gameplay identificó GameplayAuthority.cs:245–260 y404; se cotejaron esas líneas centrales por lectura. En Surface, velocidad solicitada=ClampLength(ProjectPlane(aim*MovePlanar.Y+right*MovePlanar.X,normal))*.65+(target-position)*8; target=WorldPoint+normal*.057. MoveMosquito devuelve posición/velocidad resueltas. Motion acumula distancia real3D/(human?1.2:.3), sin separar superficie/vuelo, y Snapshot expone ese acumulado como MotionPhase.

A input1, sin colisión ni corrección, velocidad tangencial nominal=.65m/s. El clip nuevo exige .65/.020=32.5 ciclos/s; la fase actual avanza .65/.3=2.1667 ciclos/s. El cociente15 confirma desacople de contratos en ese escenario teórico, no una medición de patinaje real. A60FPS serían apenas1.85 frames por ciclo nuevo: retirar clamp no demuestra marcha legible y requiere observar aliasing. Tampoco multiplicar fase por15 valida gait ni transiciones.

Recomendación enviada a Director/Mosquitos: acordar locomoción visual sobre velocidad de juego preservada; evaluar alcance admisible de patas y cadencia legible para reautoría de SurfaceWalk antes de fijar otra distancia/ciclo. No alargar apoyos por factor15 sin revisar rig, no reducir velocidad de juego unilateralmente y no introducir IK ni modificar reglas como solución implícita. La decisión puede requerir otro contrato de marcha; cualquier cambio Presentation de fase/velocidad será específico id6, preservará vuelo y fc3d97b, y se comprobará local/remoto con malla exportada. El probe puede medir el candidato actual y documentar su límite; no es necesario alterar código para fabricar un PASS.

## Encargo del Director: acuerdo previo al adapter

Director asignó a Mosquitos resolver SurfaceWalk antes del lote pesado, preservando .65m/s. Distancia .08–.12m/ciclo y cadencia5–8Hz son rango de estudio, no valores aceptados: .65/.08=8.125Hz y .65/.12=5.4167Hz. Mosquitos debe confirmar alcance físico por pata, duty, escala y legibilidad con el clip final.

Contrato requerido para binding: D=distancia Unity/ciclo; T=duración real del clip, extremos de frames/FPS; stride fuente/duty; escala aplicada una vez; origen de fase/trípodes; rango de velocidades y cadencias soportado; identidad fuente/export. Presentation no fija D antes de ese acuerdo.

Propuesta de adapter pendiente de acuerdo, exclusiva SurfaceWalk id6: faseClip=Repeat(MotionPhase_acumulado*.3/D,1). Convertir antes del módulo; usar la misma función en entrada inmediata y corrección remota, preservando el acumulado autoritativo y protocolo. Velocidad de reproducción=T*velocidadReal/D, con límites específicos coherentes con el contrato; no conservar clamp2.5 incompatible ni quitarlo aislado. Vuelo id2 y marcha humana conservan sus caminos. Verificar que MotionPhase mide desplazamiento3D resuelto, incluidas correcciones normales; el probe distinguirá esas correcciones del avance tangencial al juzgar apoyo. No introducir cambio de autoridad como parte del adapter.

Antes de implementación: confirmar origen/convención de fase con Mosquitos y revisar delta sobre ActorVisualBinding central que ya contiene fc3d97b. Después: casos local/remoto a velocidad estable, cambio de velocidad, inmóvil, aproximación, giro, detach/reentrada y salto de snapshot; comparar fase esperada, Animator y contactos en continuo. Todavía no hay D final nuevo ni modificación de runtime.

## Implementación candidata con contrato cc4c871

Mosquitos confirmó cc4c871ac066cdbc47db29db9efda556fd7163ac: D=.100m Unity/ciclo, T1s, stride_source .116m, duty .58, escala .5 una sola vez. Fase0 L1/L3/R2 y+.5 L2/R1/R3, según contrato del autor. Se conservan geometría/huesos; skin/FBX/video todavía pendientes. El estudio .020 queda histórico y no es la constante implementada.

ActorVisualBinding implementa exclusivamente para mosquito motion6:
- Fase Repeat(MotionPhase*(.3/.1),1), tanto entrada inmediata/crossfade (offset en segundos=phase*duración real) como corrección de loop. La misma conversión rige local y remoto; durante transición Animator activa no se interrumpe el blend con corrección de fase.
- Velocidad tangencial de state.Velocity proyectada sobre normal de SurfaceAttachment resuelta; si el contacto no resuelve, conserva magnitud de velocidad como fallback. Animator.speed=duraciónReal*velocidad/.1, clamp0–8 propuesto por autor; .65m/s con T1 exige6.5x. No se escala velocidad física.
- Distancia autoritativa permanece3D/.3; las correcciones normales pueden aportar fase aunque no velocidad tangencial. El probe debe medir el tamaño de ese efecto y snaps, además de apoyo de patas. No se oculta con una afirmación de PASS.
- Vuelo id2 y humano conservan distancia/límites anteriores. SetWorldPose/ResolveVisualRotation/fc3d97b no forman parte del delta. Integrar por cherry-pick del commit, nunca copiar encima el archivo de esta base antigua.

Evidencia realizada: dotnet build del ActorVisualBinding real contra referencias Unity6000.3.24f1 y ensamblados centrales, netstandard2.1/LangVersion9/compilación compartida desactivada;0 errores y0 advertencias. Sin carga del DLL en Unity ni proceso de juego. Proyecto reproducible/log/receipt/DLL en N:/LetMeSleep/Worktrees/presentation/work/surface-cadence-validation-20260912 (salidas locales ignoradas, no assets). Comando: dotnet build work/surface-cadence-validation-20260912/Cadence.csproj --nologo -v minimal --no-restore. SourceSHA1AEE9E32C5D4E22CF000A5F2A04EC159CA1CD4B2B3A93B39BE8A00EE8DAF306D; DLLSHA A9B1E680EF9A58985096CBF970234FCB54F5F2E477ACB92B2EED3CAD1C67F7F9. diff --check sin errores.

Dependencia de entrega: activar junto al clip/export nuevo de cc4c871, no validar con FBX anterior. Director conserva turno nativo. Revisor animaciones informado del contrato nuevo; validar conversión una vez, cadencia, transiciones/cancelaciones, apoyo local/remoto y aliasing con SurfaceVisualProbe continuo. La compilación no demuestra éxito visual ni contacto.
