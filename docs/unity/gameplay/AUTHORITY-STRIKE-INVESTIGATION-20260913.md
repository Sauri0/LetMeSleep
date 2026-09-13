# Golpes: trayectoria autoritativa frente al rig real

## Pedido y propiedad

Director pidió medir `StrikeState` producido por Authority en cada tick efectivo, de pie/agachado, manos y herramienta, con inclinaciones distintas. Propiedad temporal: `ActorVisualBinding`, `GameplayActorProxy` y, si resulta necesario, helper puro de trayectoria. No cambiar superficies, alcance, radio, daño ni ventanas por intuición. Director conserva el slot Unity.

Base de investigación: `5d70493`; rama `codex/gameplay-authority-strike`.

## Hallazgo de fuente, antes de medición nativa

- `GameplayAuthority.UpdateStrike` produce `Progress = elapsed / .6`. Su centro de barrido recorre linealmente `Origin → Target` entre `.08` y `.25` segundos. Cada llamada a `SweepStrike` recibe extremos correspondientes a este tick y al anterior.
- A 30 Hz hay cinco ticks con fase Active (`3/30` a `7/30`) y un último segmento efectivo a `8/30`, ya marcado Recovery. Filtrar solamente por `Phase == Active` pierde el final.
- `GameplayActorProxy.PoseLimbs` interpola desde una muñeca de reposo hasta el destino, usando SmoothStep cuyo máximo ocurre en `Progress .42`, es decir `.252 s`. Después limita la muñeca a `.55 m` del hombro proxy.
- Con herramienta, ese destino descuenta `.365 m` en la dirección `Target-Origin`. El montaje real alinea el Grip al socket y conserva su orientación. Descontar una longitud en una dirección no garantiza que el vector real `Hand → Impact` tenga esa dirección.
- El IK visual conserva longitudes reales y limita el objetivo a su alcance. El rig real y el proxy no tienen exactamente las mismas proporciones ni el mismo descenso de hombro al agacharse.

Estos hechos justifican medir tres contribuciones separadas: trayectoria/tiempo, orientación y offsets del agarre, y alcance físico. El informe previo con objetivos sintéticos y una distancia hasta Target final no determina por sí solo un error del golpe legal.

## Harness de medición

Fuentes externas, fuera de Assets:

- `validation/attention/AuthorityStrikeProbe.cs`
- `validation/attention/Compile-AuthorityStrikeProbe.ps1`

54 escenarios: dos posturas × mano izquierda/mano derecha/matamoscas × pitch -45/0/+45 × objetivo libre/cercano/límite de alcance. Yaw de acción ±.12 rad está dentro de la tolerancia de dirección de Authority y selecciona ambas manos sin fabricar snapshots.

Cada escenario crea un mundo propio y usa las implementaciones reales de motor, adquisición de herramienta mediante Use, planificación, sincronización y SphereCast/OverlapSphere. Un decorador de `IGameplayWorld` registra los argumentos y resultados de `SweepStrike`; no sustituye la planificación ni modifica actores privados. El mosquito de la plantilla queda fuera del recorrido para conservar ambas especies en una ronda válida.

La medición incluye cada tick del golpe, incluso el barrido final y los ticks posteriores a un bloqueo. Registra:

- `From`, `To`, radio, resultado/hit y fase exactos de la colisión ejecutada.
- Posición de muñeca proxy, Hand del rig y, con herramienta, Impact real.
- Distancia del punto visual al extremo actual y al segmento de colisión entero; distancia mínima entre el segmento visual del tick y el segmento barrido. Una distancia mínima pequeña no acredita que coincidan ambos extremos.
- Alcance/clamp visual, longitudes del brazo antes/después, desplazamiento de pelvis/piernas y alineación Grip/socket.
- Identidad de assemblies/MVID y hashes de fuentes, DLLs y dependencias de assets.

El rig, Animator y FBX son reales. Los seeks deterministas usan segundos desde StartTick y longitud del clip; el agachado usa la pose Crouch estabilizada. El binding ejecuta su LateUpdate real por invocación manual. No certifica crossfades, orden automático del player-loop, render, calidad del agarre ni WAN.

Las manos se miden por el origen del hueso Hand: no existe un anclaje de contacto de palma equivalente a Impact. No convertir automáticamente su diferencia al centro de la esfera en un defecto de colisión.

## Propuesta condicionada a resultados

1. Si los barridos legales y la trayectoria visual difieren, definir una función pura para el centro de impacto usando exactamente las ventanas existentes, con aproximación durante Windup y regreso en Recovery. El barrido final debe mantenerse en el destino en su tick de cierre.
2. Usar esa misma posición temporal para el proxy y el objetivo del IK real. Separar posición de contacto de posición de muñeca; con herramienta, medir offsets reales del Grip/Impact y controlar su orientación, manteniendo el agarre montado y las longitudes óseas.
3. No ampliar brazos ni cambiar el alcance de Authority para ocultar objetivos fuera del volumen alcanzable. Cuantificar el residuo que permanezca y presentar una decisión de calibración al Director si el contrato geométrico de alcance lo exige.
4. Mantener snapshots y balance intactos. Una eventual extracción de fórmula desde Authority al helper debe conservar exactamente sus barridos y requiere coordinar esa línea fuera de la propiedad temporal solicitada.

## Estado inicial

Compilación offline vigente: `N:/LetMeSleep/Validation/AuthorityStrike-20260913/compiled-20260913-013523-080/`, cero errores y cero advertencias. No se ejecutó Unity desde esta tarea. Director recibió `run-in-director-slot.cs`; el JSON nativo se guardará en `N:/LetMeSleep/Validation/AuthorityStrike-20260913/native/`.

La primera compilación `compiled-20260913-013247-940` queda obsoleta: usaba JsonUtility, que según la evidencia del revisor omitió listas de DTO externos en este entorno. La versión vigente incorpora `AuthorityStrikeJson.cs`, adaptación nominal del escritor de campos ya usado por el revisor, para conservar muestras/hashes completos. No se repite ni elimina evidencia anterior.

## Medición legal recibida y corrección

Director ejecutó el wrapper vigente sobre central `3a52351`, FBX humano `2473a8e`. Recibo completo: `N:/LetMeSleep/Validation/AuthorityStrike-20260913/native/authority-strike-20260913-013849-515.json`. Contiene 54 escenarios, 972 muestras y 304 barridos reales; cero fallos de preparación/cobertura. Análisis reproducible en `analysis-before/` del mismo directorio de validación, generado por `Analyze-AuthorityStrike.ps1`.

| Configuración libre | Máximo punto visual → To | Máxima distancia entre segmento visual y barrido |
|---|---:|---:|
| Matamoscas agachado | 65.54 cm | 60.18 cm |
| Matamoscas de pie | 54.06 cm | 49.10 cm |
| Mano agachada | 41.08 cm | 41.08 cm |
| Mano de pie | 43.50 cm | 39.50 cm |

Los 18 barridos con matamoscas agachado/libre no requieren clamp del objetivo proxy. El desajuste no se explica sólo por alcance: en pitch 0, el último vector Grip→Impact es `(0.284, 0.167, -0.158)` m mientras el segmento avanza principalmente hacia +Z. Grip/socket coincide, pero su dirección no coincide con el recorrido. Las longitudes óseas se mantienen (variación máxima 9.35 micrómetros a coordenadas mundiales próximas a 100 m), pelvis/piernas no se desplazan por IK.

Se implementa, conservando Authority, balance y wire:

- `StrikeVisualTrajectory`: contacto temporal puro. Prepara Origin en el último tick anterior al barrido (`2/30`), recorre la misma recta `.08–.25`, conserva Target durante el tick final `8/30` y retorna suavemente hasta `.6`. Cinco checks CPU adicionales comparan contra barridos efectivamente emitidos por Authority en tres inclinaciones, incluyendo ambos extremos y el último segmento en Recovery, continuidad e interrupción.
- `GameplayActorProxy`: usa ese contacto común para su brazo, manteniendo su límite previo y longitud nominal de herramienta.
- `ActorVisualBinding`: resuelve el contacto directamente en el rig real, evitando volver a usar como objetivo la muñeca ya limitada de un proxy con otras proporciones. Con matamoscas mide Hand→Impact completo, orienta la muñeca y la herramienta montada juntas, y descuenta el offset rotado antes de resolver el brazo. Mantiene offsets/longitudes, sin mover pelvis/piernas; la rotación local de muñeca cambia intencionalmente cuando hay herramienta para controlar Impact. Los dedos y montaje permanecen ligados a la mano.
- `StrikePoseChecks` actualiza la entrada del solver sintético: contacto constante en StrikeState en lugar de mutar el endpoint proxy. No reemplaza la prueba de planificación legal.

Validación CPU: 121/121, `cpu-correction-v1/`. Compilación offline de Gameplay/Gameplay.Unity/Binding: cero errores/advertencias, `adapter-correction/20260913-014341-960/`. No es ejecución nativa de la corrección.

El harness añade exceso de alcance geométrico del contacto, longitud Hand→Impact y residuo por encima de ese límite, para separar sincronización y geometría en la siguiente ejecución. Recompilar `Compile-AuthorityStrikeProbe.ps1` después de importar el commit en central. Compilación del harness actualizado previa a integración: `compiled-20260913-014421-083`, 0/0; no cargar módulos compilados offline como reemplazos en Unity.

Pendiente: comparación nativa posterior a la corrección, evaluación visual de muñeca/agarre y fluidez, y calibración del alcance si persiste un objetivo fuera del volumen físico del rig. El golpe bloqueado deja de emitir barridos, pero StrikeState conserva Target; esta corrección no introduce nuevo estado replicado de contacto contra pared ni certifica una pose que se detenga en esa pared. La esfera colisiona por volumen: el punto de contacto del hit tampoco equivale necesariamente a su centro To.

## Comparación nativa de la primera corrección y ajuste final

Director integró `5a336dd` como `3582d72` y ejecutó sobre el mismo rig: `native/authority-strike-20260913-014832-813.json`. Misma cobertura: 54/972/304, cero fallos del harness, sin aprobación visual. Análisis `analysis-after-v1/`.

| Configuración | Máximo antes | Máximo después | Máximo exceso de alcance después |
|---|---:|---:|---:|
| Matamoscas agachado/libre | 65.54 cm | 9.47 cm | 9.47 cm |
| Matamoscas de pie/libre | 54.06 cm | 7.60 cm | 7.60 cm |
| Matamoscas agachado/cercano | 59.63 cm | 2.51 cm | 0 |
| Matamoscas de pie/cercano | 59.57 cm | 1.09 cm | 0 |
| Manos agachado/libre | 41.08 cm | 34.23 cm | 34.23 cm |
| Manos de pie/libre | 43.50 cm | 43.44 cm | 43.44 cm |

La diferencia entre distancia medida y exceso geométrico máximo para manos no supera 0.0122 mm: el solver alcanza el contacto si es físicamente alcanzable, pero hay targets legales claramente fuera del volumen del brazo real. Esto sigue abierto y no se corrige ampliando huesos, disminuyendo alcance ni relajando el criterio de medición. Con herramienta, el máximo al límite llega a 37.60 cm de pie y 28.54 cm agachado, también atribuible al alcance exterior.

El ajuste de orientación radial dejó otro caso resoluble: cuando distancia hombro→contacto y longitud Hand→Impact casi coinciden, la muñeca cae dentro de `|UpperArm-LowerArm|`, aproximadamente 4 cm. Eso explica residuos evitables de herramienta de hasta 3.88 cm. El ajuste final de `StrikeVisualTrajectory.ToolOffset` usa ley de cosenos para inclinar mínimamente el offset rígido y ubicar la muñeca sobre el radio mínimo, manteniendo Contact y la longitud del offset. Binding lo usa antes de resolver el brazo. No cambia proxy, Authority ni el alcance exterior.

Cuatro tests CPU adicionales cubren tres posiciones dentro de esa esfera y el alcance radial/caso cero. Resultado final 125/125 en `cpu-correction-v2/`; compilación final Gameplay/adapter/Binding 0/0 en `adapter-correction/20260913-015117-754/`. **El ajuste final todavía requiere ejecución nativa del Director**, cuyo editor se cerró para una prueba física separada.

Conservación en la primera corrección nativa: longitudes varían como máximo 8.41 μm, pelvis/piernas 0, Grip/socket 0 m/0°. La muñeca con herramienta cambia orientación intencionalmente; la apariencia requiere render y clip continuo. Se enviaron los recibos y análisis al revisor de Animaciones.

Precisión de la matriz: el escenario llamado `reach_limit` coloca un obstáculo a 1.20 m del ojo. Está dentro del raycast del matamoscas; para manos supera la distancia de búsqueda y usa el destino libre de .62 m. Por eso esas filas de manos duplican libre y **no acreditan una prueba adicional del máximo .72 m**. Se conserva la geometría para comparación antes/después; una matriz ampliada debe añadir un obstáculo de manos dentro de la búsqueda si se decide calibrar ese máximo.

Binding queda liberado después del commit final de muñeca para el seam de locomoción de Presentación. Mantener suspensión del controlador de gait antes de golpes/temporales/recuperación, y resolver brazos después de la posición base final. Cualquier prueba posterior debe identificar ambas integraciones y no heredar esta medición como aprobación del takeover.
