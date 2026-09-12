# Corrección tras video rechazado: proximidad, mirada y diagnóstico de alas

Estado: fuente candidata, no validación visual. Rechazo explícito de Branko conservado. Revisadas láminas `N:/LetMeSleep/Validation/LivingMenu-20260912/UserVideo/contact-sheet.png` y `motion-sheet.png`: espera larga, gesto pequeño y poca microanimación visible. La captura PNG concurrente impide atribuir tirones al runtime sin una comparación limpia. bd3d4ca de luz sigue candidato independiente.

## Ritmo y contacto espacial

Esto **sustituye** el disparo por porcentaje del ciclo del primer componente y la propuesta intermedia de episodios a horas fijas. Bindings nuevos:

- `CycleSeconds=12`: sólo recorrido mosquito; antes24. `FirstLookAfterSeconds=1.2`: mínimo de reposo antes de atender, también después de un gesto; no asegura un evento a ese instante.
- `LookSeconds=2`, `SwatSeconds=1.2`, `ReturnSeconds=1.6`: duraciones de reproducción, retime de clips completos conservando extremos; archivos Humanos siguen8/4/1.4/1.8. No acelerar todos los clips globalmente. Idle se reproduce1x.
- `HumanReactionAnchor` opcional; si no existe, `HumanReactionOffset=(.4,1.25,.78)` transformado por HumanSeatRoot. `NoticeRadius=1.8`, `SwatRadius=.7` son candidatos a medir con nuevo brazo/herramienta. No heredan como garantía el gap.204m del export previo.

Idle inicia Look sólo por cercanía real y cooldown. Look termina su preparación antes de poder golpear. Swat requiere mosquito actual dentro NoticeRadius y posición predicha sobre la ruta real al tiempo `SwatSeconds*SwatContactNormalized` dentro SwatRadius **y más cercana que la posición actual**. Un mosquito alejándose no dispara Swat. Si sale de la zona o no llega al pase, Look se desvanece desde su muestra actual a Idle; no usa Return con pose inicial incompatible. Swat→Return conserva enlaces autor. No daño, contacto autoritativo, inventario ni teletransporte.

La ruta cuadrática conserva puntos/envolvente. Warp temporal monótono con derivada.6..1.4 hace aproximación más lenta/salida más rápida; pasa por el mismo punto de control ponderado. Primer encuentro se trae al comienzo del recorrido, pero eventos reales dependen de distancias. No se promete el instante4.6s de la propuesta intermedia. Al aplicar/quitar movimiento reducido se vuelve a Idle; reloj de vuelo se conserva y no reaparece medio golpe. Ocultar/root fuera de escena conserva cleanup del graph y luces. Regreso reanuda escena con referencias propias.

## VisualAttentionRig compartido

Nuevo componente Presentation, sin dependencia Gameplay/Content ni red. API:

`Configure(VisualAttentionRig.Bindings)`; `SetLookTarget(Transform)`; `SetLookPoint(Vector3)`; `ClearLookTarget()`; `SetReducedMotion(bool)`; `IsConfigured`, `SupportsBlink`, `IsManualEvaluation`, `LookOrigin`.

Bindings de rig explícitos: Head obligatorio, Neck/LeftEye/RightEye opcionales; HeadForward/HeadUp y EyeForward/EyeUp; límites HeadYawLimit55/HeadPitchLimit25/EyeYawLimit22/EyePitchLimit15 configurables. Humanos propone Head+Z/up+Y y Eye+Y/up+Z, pivotes nuevos; Mosquitos12°/10° de pupila con ejesUnity aún pendientes. No instalar a ciegas en rig viejo. Head/Neck/ojos reciben corrección sobre pose animada hacia posición actual, suavizada; no suma una mirada bakeada hacia un objetivo fijo. Autores neutralizan esa mirada. Un solo componente por visual.

Humano: Eyelids SkinnedMeshRenderer y arrays4 `LeftBlinkShapes=[Blink25.L,Blink50.L,Blink75.L,Blink.L]`, equivalentesR. Mezcla sólo muestras vecinas, pesos totales≤100 (Basis implícita en primer cuarto). `ReadLegacyEyeScaleBlink=false` por defecto; activar sólo en nuevo rig certificado si Human_Blink aún escribe Eye.scaleZ: lee cierre(1-z)/.93, combina máximo con blink procedural y restituye ojoabierto escala1 visualmente. No escala el blanco para inventar párpado.

Mosquito: `LeftLids`/`RightLids` arrays de `BlinkBone{Bone,LocalAxis,ClosedAngleDegrees}`; aplica baseRotation*AngleAxis(angle*cierre,axis). Ejes y ángulos proceden del nuevo export importado, NO de una conversión supuesta Blender→Unity. Conserva escalas. Pupil.L/R se suministran en LeftEye/RightEye y giran sobre pivote real. No se añaden IDs de animación.

Parpadeo local espaciado3.2–5.8s, cierre70ms/pausa30ms/apertura120ms, desfaseR12ms; sin tráfico ni sincronía de combate. Movimiento reducido sostiene mirada neutral, conserva parpadeo. No modifica apariencia/materiales/root/collider ni estado de primera persona.

Menú: Director configura atención en raíces decorativas con `ManualEvaluation=true`, luego pasa `MainMenuLivingScene.Bindings.HumanAttention/MosquitoAttention`. El menú llama PrepareForAnimation ANTES de graph.Evaluate y EvaluateAfterAnimation DESPUÉS, en el mismo flujo; apunta humano al mosquito actual y mosquito al origen visual humano. Sin LateUpdate duplicado. Gameplay: ManualEvaluation=false, Restore en Update y aplicación LateUpdate1200 tras ActorVisualBinding1100. Hookup consumer único en Presentation.Gameplay EnsureVisual y lobby HandleVisualCreated/recuperación primera snapshot (contrato Gameplay ce5d240), pendiente de integración de rigs/bindings. No dependencia inversa ni paquetes de red nuevos. Instalador sólo usa rig cosmético nuevo con contrato certificado.

Al ocultar/destruir, restaura sólo último valor propio si otro writer no lo reemplazó; elimina offsets acumulados antes de evaluar siguiente frame. GameObject/Animator dueños y orden de ejecución siguen siendo gate nativo. No afirmar pupilas funcionales en todo gameplay hasta completar instalación local/remotos/lobby/menú/customizer y revisar persistencia.

## Alas: medir antes de cambiar frecuencia

Auditor Mosquitos confirma fuenteR3 Fly/ Hover .4s,3batidas=7.5Hz, ±24.6–30.4° y movimiento secundario; GUIDUnity483916dd41effed479826b9930069204/fileID-1491716763371280848 corresponde Mosquito_Fly0–12frames, compression0/optimize0. Eso no acredita binding/deformación efectiva Unity ni lectura perceptible. No se cambia frecuencia ni se añade writer procedural de alas.

`diagnostics/Read-Menu-Flight.cs` para Director eval_file después de integrar: sólo lectura de frame/renderedFrame, unscaledTime, foco, ReducedMotion, CurrentBeat, FlightClipTime, clip/ruta/GUID/length/frameRate, Animator y Wing.L/R local/world quaternions. Ejecutar varias veces en frames distintos con separaciones no idénticas, sin captura PNG por frame. ReducedMotion=true sostiene Flight0 explícitamente. Tiempo que avanza sin cambio de rotaciones en muestras suficientes requiere inspección de curvas/path/otros writers; quaterniones cambiantes no prueban deformación ni percepción de aleteo. Leer huesos no reemplaza video limpio a velocidad normal.

## Verificación hecha y pendiente

23 checks offline de la política real y morph weights: fuera/dentro, preparación, predicción que falla, alejamiento, timeout, Return/Settle, reducción desde cada estado, regreso con cooldown, muestras de blink0/.4/1 y barrido0..1. `diagnostics/MenuReactionPolicyTests.cs` compila junto a MenuReactionPolicy.cs como consola net10; proyecto local en work/menu-reaction-tests. Componentes y snippet de alas compilan contra Unity6000.3.24f1, cero errores/advertencias. Sin Unity/Blender/render por este worker.

Native pendiente: enter/exit/reduced en cada gesto, sin graphs/luces/facial writers residuales; target fuera/dentro/alejándose; movimiento de cabeza y pupilas continuo con límites naturales; alas deformando visibles; parpadeos sin colisión con globo; nuevo arco de brazo/agarre; secuencia completa en720/1080 y todos contextos gameplay. Fuente no acredita amplitud estética ni FPS.

## Captura integrada: límite combinado cuello/cabeza

Director mostró `N:/LetMeSleep/Validation/AlfaMenu-Integrated-20260912.png`: cabeza visualmente inclinada en exceso hacia mosquito alto. Fuente revisada: cuello tiene límites20yaw/12pitch internos (no55/25); cabeza55yaw/25pitch del marker. Apply secuencial calculaba el límite de cabeza respecto a una base ya girada por cuello: corrección conjunta podía llegar75yaw/37pitch sobre pose animada. Defecto de presupuesto combinado confirmado; la foto no demuestra acumulación entre frames.

Corrección: capturar frame de cabeza ANTES de aplicar cuello, aplicar ambos y limitar el resultado conjunto al yaw/pitch de cabeza respecto a esa pose animada inicial. Guardar el quaternion local final limitado en Joint.after para que PrepareForAnimation/Restore reconozca exactamente la escritura que debe retirar. Mantiene límites existentes del marker, ojos y clips. Compilaciónoffline0/0, sin Unity. Es límite de corrección sobre pose del clip, no certificación anatómica absoluta si el clip ya contiene inclinación excesiva. Si tras este arreglo25° totales siguen demasiado altos en encuadre, candidato visual sería HeadPitchLimit18–20°, a decidir en captura nativa; no se cambia de oficio la pose ni el target.
