# MainMenuLivingScene — integración alfa

Componente propio `LetMeSleep.Presentation.MainMenuLivingScene`. Agregar al root exclusivo `MenuCharacterDisplay`; Director conserva creación de decorativos, personalización, cámara y visibilidad entre pantallas. No requiere referencias Content/Gameplay/UI en Presentation. No editar actores de partida ni sus controllers.

## API final

`bool Configure(MainMenuLivingScene.Bindings bindings)` valida y reemplaza configuración; false es dependencia incompleta, sin fallback a humano parado. Configurar root/actores/asiento antes de llamar. Si el componente está activo comienza inmediatamente; si root inactivo comienza en OnEnable. Para evitar un frame previo del controller, crear/configurar con root inactivo y activar después. Campos:

- `Transform HumanRoot, MosquitoRoot, HumanSeatRoot`: raíces decorativas disjuntas, descendientes del root del componente. Animator propio dentro de cada actor. SeatRoot es la raíz de rig acordada con Humanos/Elementos, no la superficie de contacto de pelvis.
- `Animator HumanAnimator, MosquitoAnimator`.
- `AnimationClip MenuSeatedIdle, MenuLook, MenuSwat, MenuReturn, Flight`: todos obligatorios, nonlegacy. Nombres de los cuatro exports humanos EXACTOS como campos. Duraciones previstas 8/4/1.4/1.8s. Flight usa clip existente compatible con rig, no un motion ID.
- `Transform[] FlightPoints`: mínimo4 puntos ordenados; recibir MenuMosquitoPath_00..07 de Elementos. World positions, no descendientes de los actores movidos.
- `Transform WarmLightAnchor, CoolLightAnchor`: opcionales, MenuWarmLight/MenuFillLight. Se crean sólo dos luces hijas Point, cálida(.7/range3.4) y relleno(.25/range4.5), sin sombras adicionales. Valores iniciales sujetos a revisión nativa; el contacto depende del rig de luz/mapa existente. No cambia ambiente/exposición/materiales ni luz de partida.
- `float CycleSeconds=24`, extendido si clips no caben; `float SwatContactNormalized=.5` (0.7s para Swat1.4s); `Vector3 MosquitoRotationOffset` para forward del asset, default cero.

`SetSceneActive(bool)` también cambia activeSelf del GameObject dueño (por eso debe ser root decorativo exclusivo). Ocultar el root directamente igualmente dispara cleanup. `SetReducedMotion(bool)` debe recibir preferencia guardada/aplicada por UI; sostiene Idle fase0 y Flight fase0, sin órbita ni gesto, sin pose humana intermedia. No crea setting ni audio. No mantener llamadas CharacterView.PlayMotion mientras el graph posee Animator; CharacterView/ApplyLiveAppearance permanecen disponibles.

## Secuencia, movimiento y lifecycle

Primera activación comienza con idle fase0 después del punto temporal de Return. El reposo ocupa mayor parte del ciclo. Look termina en pose de atención que coincide con Swat inicial; Swat final coincide con Return inicial; Return final coincide con Idle0. Enlaces intermedios exactos sin mezcla hacia idle; envelope suave .18s sólo al entrar/salir de la secuencia. Resto reproduce cantidad entera de ciclos Idle ajustada al intervalo para llegar a fase0; Look/Swat/Return son1x. Sin root motion ni IK inventado. Animación por graph manual con reloj unscaled propio; no toca Time.timeScale, simulación ni IDs gameplay.

Ruta cuadrática cerrada: tramo i va de midpoint(prev,i) por punto i a midpoint(i,next), velocidad y tangente continuas en uniones; cambios de velocidad proceden de geometría. No overshoot fuera del triángulo de control. Yaw/pitch siguen tangente, bank discreto limitado12°. El punto de pase es `.125*P_last + .75*P_0 + .125*P_1`, sincronizado con SwatContactNormalized. No es exactamente P_0. No cambia escala: retirar ampliación4x antigua corresponde a Director, según composición aprobada. Toda la envolvente del mosquito/alas y herramienta necesita despeje; curva matemática no prueba colisiones.

OnDisable/SetSceneActive(false)/OnDestroy destruyen graph, apagan y eliminan luces, restauran enabled/rootMotion/culling/updateMode/speed previos de Animators y pose local de raíces. El root oculto evita que el controller restaurado se vea. Reabrir conserva tiempo de secuencia; Configure reemplaza e inicia reposo. No audio, eventos globales ni objetos persistentes. Destrucción de referencias durante uso detiene la escena. Las apariencias/materiales/escalas no son mutados. ToolView/Grip y seguimiento de ToolSocket_R se integran por Director con contrato Elementos/Humanos; el componente no crea un arma o inventario.

## Evidencia y pendientes

Compilación offline contra Unity6000.3.24f1 y referencias centrales. No lanzamiento Unity/Blender ni capturas. Falta export humano real, anchors finales, integración Director, agarre del prop y revisión temporal a720/1080. Evaluar ciclo completo, apoyo pelvis/pantuflas, continuidad de cuatro clips, vuelo/alas sin atraviesos, contraste/luces y zonaUI; cambiar personalización/guardar/regresar, reducido, salir/entrar personalizador/lobby/práctica, comprobar sin duplicados/graphs/luces residuales. Clips compilados o curva calculada no acreditan calidad visual ni requisito terminado.

## Revisión de integración Director

Revisado readonly AlfaApplication.MenuScene.cs y hooks actuales: creación root inactivo, Configure/ReducedMotion/activar, colliders deshabilitados, grip alineado, escala original, ocultación por personalizador/lobby y ApplyLiveAppearance tras retorno compatibles con API. Sin defecto concreto encontrado en ese orden; no ejecutado nativo. Coordenadas cámara asumen PresentationAnchors local identidad, como montaje actual; si se transforma ese contenedor, utilizar su espacio y revisar composición.

Corrección propia: salir del modo reducido mientras el reloj retenía Look/Swat/Return omite el resto de ese gesto, sostiene Idle0 y retoma sólo una secuencia completa posterior. Conserva fase/posición de vuelo y su siguiente sincronización, sin teletransportar para reiniciar reloj. La entrada a reducido sostiene la pose exacta solicitada; no acredita transición visual suave sin revisión nativa. Además, una excepción durante construcción/evaluación inicial del graph ejecuta limpieza/restauración y marca IsConfigured=false, evitando recursos parciales retenidos. Bootstrap puede comprobar IsConfigured/IsRunning tras activar para señalar fallo; el contenido compatible aún requiere importación/ejecución real.
