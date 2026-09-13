# Isla v2: navegación aplicada, recorrido al muelle bloqueado

Turno batch CPU delegado por Higgsfield root `01a09869-54a6-7f71-8e90-05de22618557`.
Unity 6000.3.24f1 central, procesos secuenciales `-batchmode -nographics -noaudio`,
sin render, PlayMode ni Blender. Último PID 37960 terminó con exit 0; fixture
`cleanup:true`. Turno devuelto explícitamente al coordinador. **Isla no cerrada.**

## Resultado final sobre el prefab aplicado

Informe completo:
`N:/LetMeSleep/Validation/Higgsfield/IslaV2-20260913/final-applied-02/map-checks-20260913-055804-946.json`.
Config reproducible: `isla-v2.routes.json` junto a este informe (27 rutas/casos).
Log: `N:/LetMeSleep/Validation/Higgsfield/IslaV2-20260913/final-applied-02.log`.
Compilación exacta, 0 errores/0 advertencias:
`N:/LetMeSleep/Validation/Higgsfield/MapChecks/20260913-055732-984`.

| Comprobación | Resultado |
|---|---|
| Contrato schema 1, constructor integrado y conectividad | PASS, 244 regiones / 263 portales |
| Clearance de portales con esfera real .055m | 263/263 PASS |
| Spawns y asentamiento Authority30Hz | 21/21 PASS: 5 humanos +16 mosquitos, sin penetración |
| Rutas humanas | 3/4 PASS: puente ida/vuelta, arco oeste lago→puente, entrada cabaña |
| Rutas de vuelo desde spawns 1/10/11/12/13/15/16 | 7/7 PASS |
| Patrol Explore + Authority + input del fixture, 600 ticks por spawn | 16/16 PASS, 31–62 regiones y 49–69m por caso |
| Total de casos | 47/48 PASS; estado agregado **FAIL** |

Los casos aprobados no penetraron colliders más de 2mm. Los 21 spawns dieron
penetración cero. Las patrullas permanecieron dentro de regiones, sin quedarse
inmóviles 150 ticks. Se ejecutó GameplayBotNavigation.Explore integrado con el
adaptador de input del fixture; no es una prueba del BotController completo ni
de todas las decisiones de combate. El archivo final no usa override: lee
**SpatialData del prefab aplicado**.

## Fallo reproducible de bajada al muelle

`arrival-dock-return`, humano desde spawn index0, walk normal; llegó a 3 de 22
puntos y quedó detenido hasta agotar 600 ticks. Posición final mundial/local:
`(0.000026, 2.119883, -24.428965)`. Siguiente punto medido:
`(0, 2.035646, -25)`. Grounded true, penetración cero. No es un spawn defectuoso.

Collider: **`Environment/Path_South_Arrival_COLLIDABLE`**. Cast de diagnóstico
con desplazamiento forward+gravedad `(0,-.01667,-.10333)`:
distance0, normal `(0.000045,.159232,.987241)`. Ground snap también devuelve
distance0 contra el mismo collider. `point:(0,0,0)` es el valor que devuelve
esa consulta de distancia cero, **no** la ubicación del defecto.

El snap encuentra `Environment/Terrain_Island_COLLIDABLE` .075009m debajo,
en `(−.001461,2.047596,−24.392204)`, normal `(.005948,.989113,−.147041)`.
El cast horizontal solo no encontró bloqueo. Esto acota la investigación a
contacto inicial/cast descendente sobre la malla del camino, con terreno debajo;
no demuestra un escalón demasiado alto ni un collision_role mal configurado.
No cambié colliders, geometría, regla de step, Skin ni motor. El coordinador
puede revisar localmente esa sección conservando el origen; no hace falta otro
seguimiento de generación para localizarla.

## Cabaña: lo que quedó comprobado

La ruta correcta sigue el eje local X=0 de la abertura; los intentos anteriores
cruzaban la hoja decorativa abierta. El umbral lleva los pies a y=3.525033m,
unos .205m sobre el piso/porche y=3.320m. El motor subió y luego entró: **PASS**.
El waypoint de umbral se ajustó a la posición medida, sin mover el actor ni
modificar el umbral. No atribuir los fallos de configs candidatos a un defecto
de arte: éstos incluían un giro atravesando la hoja y una altura objetivo baja.

Los probes en el frente y las mallas medidas quedan en
`N:/LetMeSleep/Validation/Higgsfield/IslaV2-20260913/preparation-04/`:
`isla-preparation.json`, `measured-path-meshes.json`. Las cajas conservadoras
de 1m no encontraron conexión aérea hasta `(29.5,5.5,4.5)` dentro de la cabaña.
Se probó también una discretización de .6m y tampoco conectó ese punto. Esto
no prueba inaccesibilidad para una esfera de .055m: **navegación interior aérea
pendiente**, sin enlace ficticio a través de paredes.

## Plan aplicado y hashes

Sólo isla v2 en central, mediante Unity APIs:

- Nuevo `Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-isla-del-laguito-v2/Data/isla-navigation-schema1.json` y `.meta` generado por Unity.
- `.../Prefabs/hf-isla-del-laguito-v2.prefab`: SpatialData y ContentHash.
- `.../Scenes/hf-isla-del-laguito-v2.unity`: SpatialData y ContentHash de esa isla.

El JSON contiene 244 cajas de aire de 1m. Cada caja expandida .056m fue comprobada
libre de colliders. Los 263 portales están en caras compartidas con normales
de eje; el constructor integrado produce sus puntos reales y éstos pasaron
spherecast. La red une los 16 spawns, corredores exteriores y aproximaciones
a puente/muelle. Puede volar sobre agua a altura libre; ninguna caja incluye
el terreno o el volumen del lago como una región gigante. No cubre toda la isla
ni todo el aire disponible y no sustituye navegación humana.

SHA256 navegación:
`ff47a91de5ffab18c51f73e2de317dfc0edec9ed5349f41056d10d7f553c446a`.

ContentHash anterior:
`dd1f223894e4efdbd09e5a5a2a8aab707a50bbcce7cb31837dbedebd50016f46`.

ContentHash nuevo:
`157a5af113a05652a5621bd998e01d798a8613b01fa5103cda7d04f8ebcf28cd`.

Derivación explícita: SHA256(oldHash + LF + `nav-schema1` + LF + navSHA256).
El catálogo/hash autoritativo global **no está certificado**. La receta y
receipt del import original quedan intactos como evidencia histórica: su hash
anterior no debe confundirse con el prefab posterior a esta asignación.
Receipt de aplicación: `preparation-04/isla-navigation-apply.json`.

Los assets centrales eran un lote todavía no versionado del coordinador:
no hice stage/commit central ni agregué el lote de arte a mi commit. Mi commit
sólo contiene soporte externo en docs, con el WIP de golpes intacto.

## Pendientes y evidencia descartada como aprobación

- Desbloquear la bajada al muelle y repetir ida/vuelta.
- Medir y validar el sendero real del mirador. El trazado preliminar cortaba
  terreno fuera del recorrido; no se considera un fallo confirmado del sendero.
  Por instrucción del coordinador, se detuvo esa búsqueda y se conserva el
  intento en `candidate-02`/`candidate-03`.
- Conectar y validar navegación aérea interior de cabaña con volúmenes más
  adecuados; no usar un portal que atraviese colliders.
- Cobertura completa de mapa, roles simultáneos, agua en motor, límites de
  mundo, red, rendimiento y revisión visual no certificados por este fixture.

`candidate-01` detectó un defecto del parser externo JsonUtility: omitía arrays;
se reemplazó por Newtonsoft de Unity. Ese run informó FAIL/INCOMPLETE y sólo
sirve para spawns. `candidate-02/03` usaron plan en clon, sin modificar assets.
`final-applied` es anterior al ajuste de altura del umbral; la evidencia vigente
es **final-applied-02**. No se borraron logs ni fallos intermedios.
