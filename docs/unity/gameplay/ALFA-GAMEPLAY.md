# Contrato de gameplay Unity 0.9.4/alfa

Estado: contrato inicial de Worker 1 para integrar con Core, 2026-09-12. Autoriza este documento el lote del Director y AGENTS.md; el rótulo antiguo «propuesta» del plan maestro no congela el trabajo. Branko autorizó resolver supuestos sin nuevas preguntas mientras duerme. No hay implementación Unity ni prueba en motor acreditada por esta entrega. Base auditada: `0ea6cca`. Propiedad actual: solamente `docs/unity/gameplay/**`.

Documentos asociados: [auditoría de referencia](GODOT-AUDIT.md), [interfaces propuestas](INTERFACES.md), [riesgos y aceptación](ACCEPTANCE.md). Fuente normativa: [plan maestro](../PLAN-UNITY-0.9.4.md), secciones 1 y 5, más instrucciones posteriores del Director.

## Alcance y decisiones

Alfa contiene Sangre, entrenamiento de ambos roles con bots, humano en primera persona, mosquito en tercera persona, defensa manual, picadura sin marcas, caída/recuperación, desmayo, superficies y casa/patio fijos. Sala privada 3D independiente, sorteo por ronda y salida del anfitrión corresponden a Core/Online; Gameplay recibe roster y mapa validados. Hay manos y una herramienta de muestra; no se implementa catálogo completo, inventario, estamina, voz, Supervivencia ni Tareas en este lote.

| Regla | Estado que debe preservar la implementación |
|---|---|
| Sin marcas ni rotación de zonas | Confirmada: se retiran asignaciones, espera de zona y objetivos corporales dibujados. Un contacto anatómico estable no es una marca de juego. |
| Sangre: golpe al mosquito causa caída recuperable | Base alfa solicitada; duración y rapidez de ayuda pertenecen al perfil de balance. Sin contador de vidas en Sangre. |
| Extracción completa causa desmayo humano | Confirmada; se propone progreso por contacto continuo, detallado abajo. El momento de completitud y la protección al despertar se ensayan. |
| Relación desmayo/caída | Un mismo `RecoveryBaseSeconds` para ambos; 12 s iniciales configurables desde aterrizaje, supuesto técnico para probar. No heredar 35 s. |
| Tareas: tres vidas personales | Reconfirmada para beta. No decidir aquí cuándo se consume una vida ni trasladar `lives=0` histórico. |
| Capacidad, tick, tasas y distancias | Parámetros/versiones de Core; cifras iniciales de este documento son hipótesis técnicas de prueba, no balance aprobado. |

## Convenciones y orden de autoridad

Unity: metros, segundos, +Y arriba, +Z adelante, +X derecha; ángulos en radianes dentro del dominio. El adaptador de vista convierte a grados sólo para APIs Unity. No copiar signos Godot: su avance era -Z. `ActorId`, `RoundId`, `MapId`, `SurfaceId` y `PoseRevision` son identidades estables asignadas por host/contenido, nunca nombres de objetos ni índices de arrays físicos.

Core entrega un reloj fijo único. Director propone 30 ticks/s; adoptar 30 ticks/s, comandos sostenidos a 30/s y snapshot a 20/s como perfil inicial medible. Nunca ejecutar lógica por `Update`, por evento de animación o por tasa de paquetes. Orden de un tick: validar entradas → puertas/soportes móviles → motores y pose de colisión → resolver golpes → invalidar/soltar contactos → preparar/extraer sangre → aplicar desmayos → ayudar/recuperar → resolver resultado → emitir snapshot y eventos. Cada fase usa estado coherente del mismo tick. Golpe y extracción que completarían en el mismo tick: el golpe cancela primero la extracción. Meta y fin de tiempo simultáneos: contar sólo el intervalo hasta el fin de ronda y comprobar primero la meta.

Anfitrión valida pertenencia a actor/ronda, secuencia, valores finitos, longitud de vectores, rol, fase, estado, cooldowns, alcance y obstáculos. No acepta posiciones, impactos, IDs de víctima, punto de piel o cantidad de sangre autoritativos enviados por cliente. Tiempo del cliente sólo sirve para reconciliación dentro de ventana acotada; no adelanta temporizadores. Input viejo >250 ms queda neutro como hipótesis inicial. Acciones puntuales deduplicadas no se repiten por pérdida o retransmisión.

El estado autoritativo no depende del Animator visible. Gameplay mantiene cápsulas/superficies de combate y pose cinemática estable acordada con M1/W2; Presentation recibe esa pose y objetivos de manos/contacto. Si se usa Animator para calcular alguna pose host, debe evaluarse explícitamente al mismo tick, aun fuera de cámara. Un LOD o culling jamás elimina la hitbox.

## Input y cámara

| Acción semántica | Humano | Mosquito |
|---|---|---|
| MovePlanar(X,Y), Look(world forward) | WASD relativo al yaw de vista; pitch no inclina movimiento | W/S según el vector completo de mira; A/D según derecha estable |
| Vertical(-1..1) | Ignorada | Ascenso/descenso mundial; normalizar combinado para no ganar velocidad diagonal |
| SprintHeld / CrouchHeld | Correr / agacharse; agachado gana prioridad | Ignoradas |
| JumpPressed | Salto sólo con apoyo y espacio | Despegar de superficie; en vuelo no genera salto humano |
| PrimaryPressed | Palmada/uso manual del objeto activo | Sin acción de combate automática |
| BiteHeld | Ignorada | Mantener para preparar; sostener no permite readherirse tras cancelación |
| DetachPressed | Ignorada | E de nuevo después de soltar la tecla de preparación; acción explícita separada en dominio |
| PerchPressed | Ignorada | Intentar posarse o despegar, según estado |
| UseHeld / UsePressed | Uso de puerta/objeto validado por mirada y alcance | Mantener ayuda a compañero caído; no abre puertas |
| Pause/LookZoom | Menú local, manda neutral; no pausa anfitrión | Menú local / zoom sólo de cámara |

UI/entrada convierte teclas reasignables a semántica. Una pulsación contextual tiene una sola acción: al picar no ayuda simultáneamente; `DetachPressed` domina sobre sostener preparación. Perder foco, abrir menú, cambiar ronda y recuperar control limpian sostenidos y bordes pendientes; se requiere liberación de Bite para rearmar.

Humano: yaw sin tope ni bloqueo al mirar abajo; envolver el valor para precisión no limita el giro. Pitch inicial a evaluar: -110° abajo a +75° arriba en la convención «positivo arriba» del dominio. Vista local inmediata; cuerpo sigue con amortiguación y límite angular de cuello sólo sobre animación. Inspección quieta permite mirar pecho, brazos y piernas; caminar mantiene seguimiento del torso. Origen de vista y rayo manual usan el mismo pivot anatómico, con cámara fuera de la cabeza propia y cuerpo/manos visibles. No ampliar alcance para compensar un rig incorrecto.

Mosquito: cámara con zoom inicial a validar 0.85 m, rango 0–2.5 m. Girar en reposo cambia sólo vista local; al mover/picar se envía la dirección deseada. Resolver solapamiento inicial, barrido desde ancla segura hasta pivot y barrido hasta cámara; retraer al primer obstáculo y amortiguar sólo expansión hacia fuera. No suavizar a través de pared. Para pared/techo usar marco tangente; cambio de soporte conserva mirada mundial y no invierte W. Toda posición de cámara es presentación, nunca origen de picadura o daño.

## Movimiento y superficies

Humano: motor cinemático con cápsula, velocidad horizontal normalizada, gravedad explícita, salto por borde, step con verificación de espacio sobre la cabeza, bajada con apoyo, agachado progresivo y prohibición de levantarse bajo techo. Dimensiones iniciales del Director: altura 1.72 m, radio .25 m, ojos 1.53 m, altura agachada 1.0 m; iguales entre cosméticos. Mosquito: esfera .055 m; visual largo aproximado .19 m y alas .24 m no amplían automáticamente hitbox. Corredor 1.8 m, escalera 1.6 m, vano 1.1 × 2.2 m. Validar escaleras y puertas con cápsula completa, en ambos sentidos. La envolvente amplia de brazos sirve sólo para descartar candidatos lejanos, nunca para colisión locomotora o golpe.

Mosquito libre: `forward = Normalize(AimForward)`, `right = Normalize(Cross(stableUp, forward))`; cerca del polo usar la derecha anterior proyectada o el yaw de vista, nunca normalizar cero. `wanted = ClampMagnitude(forward*Move.Y + right*Move.X + WorldUp*Vertical, 1)*FlightSpeed`. Velocidad se aproxima a wanted mediante aceleración o freno separado. Soltar W no detiene instantáneamente: con referencia 3.8 m/s y freno 28 m/s², parada ideal ≤0.136 s y ≤0.258 m antes de colisiones; son referencias de prueba, no balance final.

Desplazamiento con barrido de esfera/cápsula más deslizamiento, incluyendo hojas orientadas. Un raycast de centro no protege alas/cuerpo ni evita túneles. Altura del solar y techo físico del edificio son distintos; ningún plano de techo cubre el patio. Cosméticos no cambian radio/hitbox. Espacio visible bajo puerta sólo transitable si el volumen completo pasa.

Posado: el host busca un collider marcado `CanPerch` dentro de alcance. Identifica soporte y punto/normal locales. Aproximación barrida, luego movimiento tangente proyectado, con velocidad/freno propios. Una esfera solapada al inicio exige resolver penetración antes del cast; comprobar normal real de superficie para orientación. Bordes cóncavos/convexos necesitan prueba de continuidad: mantener identidad y transportar tangente si hay vecino válido; si no, detenerse o despegar de forma explícita, sin teleport. Piso/pared/techo cubiertos por igual. Propuesta alfa: soportes estáticos y puertas cinemáticas registradas; la política de girar una puerta ocupada la acuerda Core, con liberación segura si soporte deja de existir.

## Máquinas de estados

| Actor/estado | Entrada válida | Salida y efectos host |
|---|---|---|
| Humano Active | Inicio/recuperación | Move/crouch/jump y Strike; extracción completa → Falling, cancela golpe y bloquea movimiento |
| Humano Falling | FaintStarted | Caída física acotada; no ragdoll como autoridad. Al apoyo → Fainted |
| Humano Fainted | Aterrizaje | Reloj host; nuevas picaduras inválidas; al terminar → Recovering |
| Humano Recovering | Fin de desmayo | Pose de incorporación sin atravesar techo; breve protección parametrizada contra nueva extracción; final → Active |
| Mosquito Flying | Spawn/desprendimiento/despegue | ApproachingSurface o PreparingBite; impacto → Falling |
| Mosquito ApproachingSurface | Perch validado | Llegada → Surface; soporte/recorrido inválido → Flying en último punto seguro |
| Mosquito Surface | Contacto estable | Caminar tangente; despegue validado → Flying; preparar picadura sólo con línea/contacto válido; impacto → Falling |
| Mosquito PreparingBite | BiteHeld + contacto cercano visible | Preparación continua sin aspirar desde lejos; soltar/bloqueo/cambio de víctima → estado locomotor anterior; completar → Biting |
| Mosquito Biting | Ancla validada | Extracción por tick; Detach, impacto, desmayo/desconexión víctima, colisión o ancla inválida → liberación segura o Falling |
| Mosquito Falling | Impacto único | Quitar ancla/superficie/progreso; gravedad y apoyo real → Stunned |
| Mosquito Stunned | Aterrizaje | Countdown y ayuda válida; golpes repetidos no reinician contador; fin → Recovering |
| Mosquito Recovering | Reloj completo/rescate | Elegir espacio libre junto al apoyo, limpiar teclas; recuperación acotada → Flying |

`StrikePhase(None/Windup/Active/Recovery)` es un subestado del humano Active; `Grounded/Airborne/CrouchFraction` es locomoción. No multiplicar enums con cada combinación. Desconexión y fin de ronda cancelan todos los procesos y anclas. Todos los mosquitos caídos no implican victoria humana en Sangre: aún recuperan; sólo meta/tiempo o abandono decidido por Core termina la ronda.

## Picadura sin marcas, daño manual y Sangre

El host busca piel/cuerpo del humano elegible delante de la probóscide, dentro de distancia de contacto. No usa asignación ni teletransporte de concentración a 1.6 m. El primer contacto real bloqueante gana; no seleccionar una espalda atravesando pecho, ropa sólida, otro actor, mueble o puerta. Preparación pierde progreso al soltar, perder línea, alejarse o cambiar ancla; pequeña histéresis geométrica sólo evita parpadeo, no amplía alcance a través de paredes.

Ancla: `(VictimId, AnatomicalSurfaceId, LocalPoint, LocalNormal, PoseRevision)`. Superficies anatómicas continuas y estables en el rig/collision profile, independientes de mallas cosméticas; no enumeración de «ocho zonas», triángulos importados o SkinMesh mutable. El host transforma ancla cada tick y verifica volumen libre del mosquito antes de moverlo. Presentation aplica la misma ancla a la pose interpolada del humano. Tolerancias y errores se registran, no se corrigen aumentando offsets hasta ocultar el fallo.

Un humano solo debe defender cualquier contacto habilitado. Propuesta de aceptación: dominio continuo de piel elegible certificado contra alcance manual de una mano y rayo de vista; impedir el inicio fuera de ese dominio y dar razón contextual breve sin marcador. Con varios humanos, espalda se habilita sólo si la pose/ruta permite defensa cooperativa. Al quedar un solo humano, reevaluar contactos traseros: soltarlos de forma segura si no puede defenderlos. Confirmar con M1/W2 que la postura realmente cumple antes de ampliar superficie elegible; no certificar con sólo ocho puntos de prueba.

Preparación completada inicia `ExtractionProgress=0`; progreso requiere contacto continuo y humano Active. Supuesto de balance inicial: un contacto ininterrumpido llega a 1 después de `FullExtractionSeconds` y desmaya una sola vez al humano. El host agrupa extracción por víctima, limita la tasa máxima por víctima y distribuye crédito proporcional entre contactos; el incremento de progreso individual usa el mismo trabajo acreditado para impedir acelerar desmayo con muchos mosquitos. La sangre global suma el trabajo acreditado, acotada a cuota. Soltar/impacto elimina progreso individual, no sangre global. Registrar este modelo en BalanceProfile para iterar en partidas; no acumular desmayo entre picaduras separadas.

Al desmayar: retirar todos los mosquitos anclados a esa víctima, emitir un único evento y mantener sangre ganada; no otra picadura hasta terminar Recovering. La ronda Sangre termina por cuota mosquito o tiempo humano, una vez, con `RoundEnded`. El resultado no depende del último fotograma ni de un evento perdido.

Defensa manual: clic captura rayo de vista, herramienta y secuencia. Host obtiene objetivo geométrico de ese rayo, escoge mano que pueda alcanzarlo y fija objetivo de animación; no gira la mira hacia mosquitos ni lee una asignación. Barrer mano/cara de herramienta durante ventana Active contra cápsulas actuales/anteriores de insecto y obstáculos. Alcance medido desde hombro hasta cara de herramienta una sola vez; normales/radio reales. Mismo plan alimenta pose visual y daño. Una víctima recibe como máximo un impacto por StrikeId. Golpe cooperativo puede quitar mosquito de compañero; no inventar daño PvP humano. Mano defendiendo su propio antebrazo requiere la opuesta, sin huesos invertidos.

## Ayuda y bots

Ayuda propuesta: mosquito consciente mantiene Use mirando a un compañero Stunned, con distancia y línea libre host. Un ayudante acelera el mismo reloj; varios no multiplican velocidad sin límite. Cortar ayuda revierte a tasa normal, no reinicia reloj. No hay resurrección/vías de vidas en alfa. Si no hay espacio libre al recuperar, mantener estado y reintentar posiciones locales validadas; recuperación forzada sólo en punto del mapa aprobado y como evento explícito, nunca dentro de pared.

Bots producen exactamente los mismos comandos que jugadores, pasan permisos y cooldowns y tienen decisiones a cadencia acotada. Humano observa mosquitos visibles/audibles, mira y golpea manualmente; puede defender su cuerpo y compañeros, abrir puertas y recorrer interior/patio. Mosquito observa humanos, navega, busca contacto visible sin leer estado privado, prepara/pica, se desprende y rescata. No usar `_assignment` ni perseguir a través de paredes. `BotObservation` es una proyección con percepción, no acceso a `IGameplayAuthority` mutable. Rutas y mapa son conocimiento permitido; posiciones enemigas ocultas sólo memoria con caducidad.

Entrenamiento llama a la misma autoridad y catálogo, con modo Sangre y rol elegido; no monta un simulador simplificado ni deja los bots quietos para la prueba. Reinicio conserva mapa/rol/cosméticos y crea RoundId nuevo, limpia eventos, inputs, contactos, sangre y relojes. UI no ofrece otros modos hasta su entrega.

## Adaptación Unity propuesta

Motor humano mediante `CharacterController.Move` o capsule motor propio detrás de interfaz; Core decide ensamblado/adaptador. La API Move recibe desplazamiento y no añade gravedad: el motor debe integrarla explícitamente. [Unity 6.3 CharacterController.Move](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/CharacterController.Move.html).

Para vuelo/cámara, `SphereCast` exige manejo aparte del solapamiento inicial; su normal puede no ser la normal geométrica necesaria para posarse, por lo que el adaptador debe verificar ésta con una consulta apropiada. [Unity 6.3 SphereCast](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.SphereCast.html).

`FixedUpdate` puede ocurrir cero o varias veces por cuadro; el reloj de red/tick debe coordinarlo Core para evitar simular dos veces. La cadencia fija no constituye una garantía de determinismo físico entre máquinas. [Unity 6.3 FixedUpdate](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.FixedUpdate.html). No se fijan paquetes, Input System, transporte, Cinemachine ni NavMesh en esta entrega.
