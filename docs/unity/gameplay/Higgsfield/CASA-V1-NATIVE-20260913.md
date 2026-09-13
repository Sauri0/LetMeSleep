# Casa v1: recorridos aprobados, navegación pendiente

**Informe histórico del primer lote.** Estado posterior: navegación semántica
aplicada y44/44casos aprobados; ver `CASA-SEMANTIC-NAVIGATION.md`. La interpretación
de las patrullas de stress se corrige abajo; los28casos físicos siguen aprobados.

Turno Unity CPU delegado por coordinador Higgsfield. Batch central Unity
6000.3.24f1, `-nographics -noaudio`, sin render, PlayMode ni Blender. Se usaron
procesos secuenciales y se liberó el turno tras terminar PID10600, exit0 y
cleanup true. **CASA no cerrada: falta un plan válido asignado a SpatialData.**

## Resultado vigente tras corregir el spawn autorizado

`N:/LetMeSleep/Validation/Higgsfield/CasaV1-20260913/final-spawn-fixed/map-checks-20260913-062849-867.json`

| Caso físico | Resultado |
|---|---|
| 5 spawns humanos y16 mosquitos, Authority30Hz /settle30ticks | 21/21 PASS |
| Patio delantero → entrada → sala | PASS,9 puntos |
| Hall → cocina, rodeando consola | PASS,5 puntos |
| Única escalera, subida humana | PASS,19 puntos |
| Única escalera, bajada humana | PASS,19 puntos |
| Patio trasero → hall | PASS,7 puntos |
| Vuelo por escalera arriba y abajo | 2/2 PASS,19 puntos cada uno |
| Total físico posterior al cambio | **28/28 PASS**, sin penetraciones >2mm |

Se repitió una vez la suite física completa después de corregir el marcador.
El agregado del fixture es **FAIL**, porque SpatialData todavía contiene datos
que no satisfacen schema1; no presentar el total físico como aprobación integral.
Config vigente reproducible: `casa-v1.routes.json`. Assembly exacto usado:
`N:/LetMeSleep/Validation/Higgsfield/MapChecks/20260913-062610-738`, compilado con
0 errores/0 advertencias. `final-spawn-fixed.log` prueba el cierre del proceso.

## Mediciones que sustentan los recorridos

La fuente `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/02-casa/HF_MAP_02_casa_report.json`
describe16 contrahuellas de.18125m, huellas de.28m, ancho1.30m y descansillo1.71m.
El helper obtiene los centros y cotas superiores de los16 colliders importados
`CASA_Stair_Step_01..16`, no una rampa aproximada. Eje X=1.225; primera huella
Z=−1.33,Y=.43125; última Z=2.87,Y=3.15. Hueco superior X[.4,2.05],Z[−1.6,3.15].
El descanso medido permite girar a Z≈3.7 antes de cruzar hacia el hall superior.

Puertas medidas por los segmentos de partición importados: oeste X≈−1.4,
abertura Z[−.9,.4], centroZ≈−.25; cocina X≈2.2, abertura Z[−3.65,−2.35], centro−3.
Los probes de soporte quedan en `preparation-03/casa-preparation.json`.

El primer waypoint de cocina en baseline iba hacia la consola del recibidor
(`CASA_Foyer_Console_Top_Plank_3`). Se corrigió una vez para desplazarse primero
al hall libre X=.5,Z=−2.65 y luego a Z=−3; todos los puntos del caso final pasaron.
Ese intento inicial **no es un defecto de la consola ni de la puerta**. El
muestreador de soporte ahora filtra pisos, porches, senderos, terreno y baldosas:
una mesa no se convierte en piso por ser el primer hit de un rayo descendente.

## Delta de integración: Spawn_Human_03.001

Autorización específica del coordinador: elevar sólo este punto al apoyo real
medido más.01m, conservarX/Z, usar Unity API y repetir caso/suite una vez.

- Antes Unity: `(-2.5,.25,2.1)`; penetración inicial **.02380028m** contra
  `Environment/CASA_Floorboard_GF_10_03`; el motor asentaba aY≈.2741115.
- Después Unity: `(-2.5,.2841115,2.1)`; penetración inicial/final **0**; terminó
  asentado enY=.2741592. No se movió el tablón ni otro collider.
- Prefab/escena: `Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-casa-del-patio-v1/Prefabs/hf-casa-del-patio-v1.prefab`
  y `.../Scenes/hf-casa-del-patio-v1.unity`, guardados mediante Unity APIs.
- **Sincronización de fuente pendiente del root cuando Blender esté libre:**
  escena CASA, marcador `Spawn_Human_03.001`, Blender X=−2.5,Y=2.1,Z de.25 a.2841115.
  Este worker no abrió ni guardó `.blend`, FBX o GLB; el cambio es override de
  integración. El import receipt original se conserva histórico.

Receipt: `preparation-04/casa-apply.json`, status
`SPAWN_ONLY_APPLIED_GLOBAL_HASH_NOT_CERTIFIED`. Hash anterior:
`3566021153c4eaeff9df7f804ab475da53e00dfb95197fdf464df3edfa41f893`.
Hash nuevo:
`f717f0b3837e8c80139cc804a806e400add68b863ff22450f7bbb188ffbd97b5`.
Derivación SHA256(oldHash + LF + `spawn-human03-support-plus-0.01` + LF +
`0.2841115`). **No incluye navegación nueva; catálogo autoritativo global no
certificado.** `fix-casa-spawn` rechaza aplicar otra vez sobre un marcador ya
corregido; no ejecutar ese action como si fuera un test de lectura.

## Navegación: candidata rechazada y conservada fuera de Assets

Se midieron celdas de.6m en espacio libre expandido.056m para la esfera. El
último candidato conecta208 regiones y211 portales de caras adyacentes, pasando
211/211 consultas estáticas. Usa Explore y Authority reales con un adaptador
propio de input máximo a30Hz; **no ejecuta BotController ni SteerBot**. Son
ensayos de stress del adaptador, no del runtime completo. Dos variantes:

- `candidate-01`: celdas originales.15/16 patrullas salen de toda región.
- `candidate-02`: crece cada cara hasta.15m extra sólo si el volumen ampliado
  sigue libre;15/16 patrullas también salen de la unión. **No aprobado**.

La retícula resulta demasiado ajustada para los puntos de portal integrados
(±.55m), el cambio de waypoint a.24m y la inercia del vuelo. Las celdas de.6m
dejan apenas.05m desde el punto extremo hasta su borde. Es una hipótesis de
incompatibilidad con el adaptador de stress, apoyada por las salidas
registradas; no acredita un fallo del BotController real ni de paredes/muebles. La siguiente autoría
debe usar volúmenes de salas/corredores con margen suficiente y transiciones
medidas. No se debe publicar este grafo por haber pasado clearance estático.

El chequeo estricto de Bounds también marcó un spawn sobre una frontera por
precisión float; el runtime admite.04m de tolerancia y sí inicia esa patrulla.
La decisión de no publicar ese candidato se apoyó en salidas durante15ensayos
de stress, no en ese mensaje de borde. El requisito de estar siempre dentro de
una caja también era más estricto que BotPatrol: admite tránsito sin región si
hay un pasaje activo. Estos resultados no demuestran que el runtime real falle.

Candidatos, hashes y logs completos en
`N:/LetMeSleep/Validation/Higgsfield/CasaV1-20260913/preparation-03`,
`preparation-04`, `candidate-01`, `candidate-02`. No se escribió
`Data/casa-navigation-schema1.json`, no se reasignó SpatialData y no se mezcló
su hash con el del mapa. El source de helper permite preparar/aplicar planes,
pero `apply-casa` **no fue ejecutado**; sólo se ejecutó `fix-casa-spawn`.

## Alcance y pendientes

Quedan autoría/validación de navegación y su integración, sincronización del
marcador fuente y actualización del catálogo/hash global. No se certifican
arte, iluminación, FPS, red/WAN, combate, todos los muebles ni cobertura total
de salas por comprobar estos siete recorridos. No se modificaron otros mapas,
reglas, colliders ni el WIP de golpes del alfa. No se hizo stage del lote CASA
central; el commit del worker contiene sólo soporte y documentación externa.
