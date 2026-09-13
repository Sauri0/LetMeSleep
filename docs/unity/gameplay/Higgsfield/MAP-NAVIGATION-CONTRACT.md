# Higgsfield: contrato y fixture de mapas

Ticket nuevo MAPAS, 2026-09-13. Soporte técnico del Worker Código / Gameplay.
El alfa y su WIP de golpes continúan congelados. Estos archivos externos no
modifican Assets, escenas, importer, Core, Bootstrap ni reglas de movimiento.

## Ejecutar sobre la revisión importada

1. El coordinador termina la importación de isla v2 y deja que Unity compile.
2. Copiar `isla-review-01.checks.json` a la carpeta de evidencia de esa revisión.
   Ajustar `mapId` y `prefabPath` al prefab real. El ejemplo apunta a revisión 01;
   no presupone el nombre final de v2. Mantener pools mínimos 5 humanos/16 mosquitos.
3. Agregar rutas medidas sobre el mapa importado. `points` usa objetos XYZ en
   coordenadas locales del root: posición de los **pies** humanos y **centro** del
   mosquito. `spawnIndex` empieza en cero. Definir al menos dos rutas de cada rol,
   con puntos intermedios suficientes para curvas y desniveles. Ejemplo de forma
   (coordenadas ilustrativas, NO ruta validada de isla):

   ```json
   {
     "id": "acceso-cabana-humano",
     "role": "human",
     "spawnIndex": 0,
     "maxTicks": 600,
     "crouch": false,
     "sprint": false,
     "points": [{"x": 1, "y": 2.5, "z": 3}, {"x": 2, "y": 2.7, "z": 4}]
   }
   ```

4. Compilar offline desde PowerShell; este script **no abre Unity**:

   ```powershell
   & 'N:/LetMeSleep/Worktrees/gameplay/docs/unity/gameplay/Higgsfield/Compile-HiggsfieldMapChecks.ps1' -Config 'N:/ruta/a/isla-v2.checks.json'
   ```

   Lee exclusivamente `Repository/unity/Library/ScriptAssemblies` integrados,
   nunca compila fuentes del worktree. Produce un directorio fechado bajo
   `N:/LetMeSleep/Validation/Higgsfield/MapChecks`, copia de fuentes/config,
   DLL con nombre único, `compile.log`, hashes de dependencias y `receipt.json`.
   Si central cambia assemblies después, recompilar. La versión nativa deberá
   coincidir con `UnityEditorData` (por defecto Unity 6000.3.24f1).
5. En el slot nativo del coordinador, con Editor central inactivo y fuera de
   Play Mode, ejecutar el contenido de `run-in-coordinator-slot.cs` mediante su
   ejecutor C# del Editor. Usa reflection para cargar la DLL externa y llamar
   `HiggsfieldMapChecks.Run(configPath, outputDirectory)`. **No copiar la DLL a
   Assets.** La devolución es la ruta exacta del JSON nativo fechado.
6. Leer el JSON completo: `errors`, `pending`, `cases`, `passages`, `cleanup`.
   Cada caso contiene muestras por tick, penetración y ruta del collider cuando
   corresponde. Un timeout de ruta no distingue mal waypoint de paso bloqueado:
   revisar muestras y geometría antes de atribuir el defecto al motor.

La entrega inicial no incluía ejecución nativa. El turno delegado posterior de
isla v2 está documentado en `ISLA-V2-NATIVE-20260913.md`. Compilación no acredita motor,
colliders, importación ni recorrido. El archivo ejemplo conserva `routes: []`
deliberadamente y nunca permite certificar traversal. V1 además falla pools y
SpatialData mientras mantenga 2/1 y la receta de importación.

Entrega compilada offline con 0 errores y 0 advertencias:
`N:/LetMeSleep/Validation/Higgsfield/MapChecks/20260913-052439-078`.
`run-in-coordinator-slot.cs` en esa carpeta permite ejecutar inmediatamente
el config ejemplo sobre revisión 01. Para isla v2 usar la receta anterior y
un config nuevo; no presentar la ejecución del ejemplo como prueba de v2.

## Contrato común para los cinco mapas

| Área | Mínimo verificable |
|---|---|
| Identidad | Un EnvironmentMapDefinition en root, MapId exacto/único por mapa, ContentHash no vacío, root identidad, PlayBounds finito y con volumen. |
| Spawns | Al menos 5 humanos y 16 mosquitos, referencias únicas activas dentro del mapa, posiciones sin superposición del mismo rol, dentro de PlayBounds, cápsula/esfera libre. Son pools: Authority admite 2–16 actores totales, con 1–5 humanos. |
| Piso humano | Spawn sin penetración >2mm y asentamiento neutral real de 30 ticks: grounded, desplazamiento <=0.5m, sin salir de bounds. No corregir el spawn dentro de la prueba. |
| Geometría | Colliders activos, SurfaceId/door IDs válidos según RegisterGeometry. Troncos sólidos separados del follaje; intención de colisión explícita. No deducir comportamiento de nombres como REMOVABLE. |
| Movimiento | Cápsula humana radio .25m, alto 1.72m/1m agachado, step .22m; mosquito esfera radio .055m. Mantener reglas integradas. Probar entradas, escalones, puentes y espacios entre vegetación con el motor. |
| Navegación | SpatialData es el plan schema 1 descrito abajo, separado de import-recipe. Zonas conectadas del espacio libre y spawns mosquito incluidos. |
| Cobertura por mapa | Dos recorridos humanos y dos de vuelo como mínimo para este fixture, ampliados hasta cubrir los accesos y cuellos de botella reales. Rutas de ida/vuelta y altura diferenciadas cuando geometría lo requiera. |

Los cinco mapas deben tener config y evidencia separados, vinculados por ID,
hash de config, dependency hash del prefab y MVID/path de assemblies cargados.
No copiar coordenadas de isla a otro mapa. La cantidad mínima de rutas es un
umbral del fixture, no prueba exhaustiva de cinco mapas completos.

## SpatialData schema 1: comportamiento integrado

```json
{
  "schema_version": 1,
  "map_id": "ID_EXACTO_DEL_MAPA",
  "zones": [
    {"id": "zona_a", "min": [0, 0, 0], "max": [2, 3, 2]},
    {"id": "zona_b", "min": [2, 0, 0], "max": [4, 3, 2]}
  ],
  "portals": [
    {"id": "paso_ab", "from": "zona_a", "to": "zona_b",
     "center": [2, 1.1, 1], "normal": [1, 0, 0],
     "width": 0.8, "height": 1.6, "door": false}
  ]
}
```

Ejemplo sintáctico solamente. GameplayBotNavigation guía **patrullas mosquito**;
no crea NavMesh ni ruta humana. Coordenadas locales del MapRoot. IDs de zonas y
enlaces no vacíos y únicos, min < max, coordenadas finitas, normal no nula. El
fixture rechaza endpoints desconocidos y portales width < .3m / height < .5m,
porque runtime los descarta silenciosamente. La conectividad se exige en el
contrato; no se agrega esa restricción al runtime.

Cada portal genera tres puntos: center − normal normalizada × .55m, center,
center + normal × .55m. El fixture lee esos puntos del **objeto runtime real**
por reflection y comprueba esfera de .055m en puntos y segmentos contra colliders
del mapa. Esto es clearance estático, no ejecución de BotPatrol completo.

`door:true` exige exactamente un GameplayDoor cuyo `name` coincida con el ID.
Una malla decorativa abierta no satisface el contrato. El runtime abre el enlace
a partir de |AngleRadians| >=60°. Una puerta cerrada queda `PENDING_CLOSED_DOOR`:
la prueba de apertura y tránsito requiere otro caso; el fixture no gira hojas.

Existe un único `stair` opcional con `id/from/to`, `lower_flight`, `upper_flight`
y `mid_landing`. Cada flight usa `clear_x:[min,max]`, `start_y/end_y/start_z/end_z`;
landing usa `min/max` XYZ. El runtime deriva siete puntos de vuelo con offset
vertical .85m. El fixture exige campos completos y comprueba los siete puntos
derivados. No inferir que representan escaleras caminables para humanos.

BotPatrol puede elegir puntos interiores a `minY+1.1` (limitados por maxY−.3).
Una caja gigante que abarque lago, montaña y cabaña produce destinos dentro de
obstáculos. Dividir volúmenes libres, evitar solapamientos ambiguos y probar
patrulla real aparte. El fixture no muestrea todos los interiores de cada zona.

## Alcance, resultados y límites

El fixture instancia el prefab real bajo un root temporal inactivo, elimina
scripts desconocidos **del clon**, desactiva render/audio/Animator y conserva
definición, surfaces, doors y pickups. Rechaza Rigidbody: geometría dinámica
necesita otro banco. No modifica prefab fuente, escenas guardadas ni assemblies.
Limpieza síncrona DestroyImmediate en finally. No Physics.Simulate ni Play Mode;
ejecuta queries nativas de Unity y Advance de Authority a 30Hz, con dos proxies
por caso (actor de prueba y rol opuesto lejano). No prueba colisión simultánea
de un roster completo. Queries del motor filtran por MapRoot y actores propios.

Recorridos envían PlayerInputCommand autenticado y llaman la Authority real.
El controlador apunta al siguiente waypoint y reduce la magnitud de input cerca
del destino; no teletransporta, salta, abre puertas ni modifica velocidad,
gravedad, step o colisión. Valida llegada humana horizontal <=.18m y vertical
<=.15m; vuelo <=.12m. Mide penetración de la forma real cada tick. El umbral 2mm
es criterio diagnóstico del mapa y se informa incluso si el motor resuelve una
penetración inicial.

Máximos: 32 spawns/rol, 32 rutas, 64 puntos/ruta, 600 ticks/ruta + 30 de
asentamiento, 1024 zonas y 3072 portales. Presupuesto cooperativo global 45s;
una llamada nativa individual no se puede interrumpir. Partir configs si agota
tiempo. `FAIL` indica contrato/caso fallido; `INCOMPLETE` falta cobertura o tiempo;
`PASS_SCOPED` aprueba solamente las comprobaciones enumeradas en ese JSON.

No acredita arte, navegación humana automática, patrulla completa, animación,
perch/bite/strike, puertas interactivas, red/WAN, FPS ni límites efectivos del
mundo. PlayBounds es metadato, no barrera del motor. Agua no define natación ni
protección contra caídas. Esas decisiones/reglas quedan en coordinación.

El fixture admite rutas `patrol:true`, rol mosquito y `points:[]`: **stress del
adaptador propio**, sin BotController/SteerBot. Ejecuta
GameplayBotNavigation.Explore real durante maxTicks, con el motor habitual,
exige visitar tres regiones, recorrer >2m y no salir de toda región ni detenerse
150 ticks. No representa exploración exhaustiva. `navigationOverridePath` permite
probar un plan candidato sólo en el clon antes de aplicarlo a un asset.

Para entrega de bots usar `runtimePatrol:true`, mutuamente excluyente con
`patrol`, mosquito y `points:[]`. Instancia GameplayRuntime sobre el world
temporal, BeginRound con un mosquito IsBot y humano remoto no visible, y llama
TickHost600veces: Authority30Hz y decisiones reales10Hz, ObserveBot/BotController/
SteerBot originales. CaptureLocalInput/AutomaticTick/UseBuiltInCamera desactivados.
No vuelve a llamar Explore para observarlo. Registra regiones visitadas, metros,
máximo tiempo inmóvil y tiempo sin región ni pasaje activo. Usa BotRegion.Contains
real (tolerancia.04m); permite tránsito sin región con pasaje activo. Exige dos
regiones,>2m y menos150ticks consecutivos inmóvil/perdido; conserva checks de
PlayBounds/estado finito/penetración2mm. No prueba combate, todos los pasajes ni
sesiones largas. CASE.driver etiqueta explícitamente ambos modos. El compilador
copia el JSON candidato a la evidencia inmutable; no lee un override mutable
durante la ejecución posterior.

El parser de DTO externos utiliza Newtonsoft.Json incluido con Unity: la primera
ejecución nativa mostró que JsonUtility omitía arrays de tipos en DLL externa.
El constructor de GameplayBotNavigation sigue usando su parser integrado real.
El writer externo evita la pérdida de listas en informes. Los fallos de rutas
incluyen casts de diagnóstico horizontal, forward+gravedad y ground snap; sus
contactos no sustituyen la traza interna del motor.

`HiggsfieldIslaPreparation.cs` está limitado por código al prefab de isla v2.
`action:prepare-isla` mide colliders, construye corredores de celdas de 1m
expandidas .056m sin colisión y guarda candidatos externos. Presupuesto 90s,
máximo 1024 celdas seleccionadas. A* se usa sólo para autoría offline del grafo;
no cambia el algoritmo del juego. Un interior que no cabe en esa discretización
queda explícitamente pendiente; no prueba que sea físicamente inaccesible.
`action:apply-isla` requiere el mismo dependency hash del prefab medido y el hash
del JSON preparado; usa Unity APIs para asignar SpatialData y ContentHash en
prefab/escena isla. El nuevo hash es SHA256(oldHash + LF + nav-schema1 + LF +
navigationSHA256), local a esa revisión, sin certificar el catálogo autoritativo.

## Riesgos observados en isla revisión 01

Evidence previo del coordinador en `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/01-isla`:
471 renderers y 321 colliders; sólo dos human spawns medidos. Sus pies aparecen
aproximadamente 2.5cm bajo el piso registrado, por confirmar con motor nativo.
Pine_Tree_* figura solid/perch; revisión v2 separará tronco/follaje. La caja total
de escalones de cabaña mide .54m de alto, pero eso **no** demuestra un riser >.22m:
se necesita medir cada escalón y recorrerlo. No derivar aprobación de accesos
del número de colliders o de dos consultas de clearance.
