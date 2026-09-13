# Candidato experimental: barrido humano separado del contacto tangente

Commit del motor `37d0b8b`, sólo CastMotor en UnityGameplayWorld.cs. Se aisló el
hunk mediante git apply --cached; cambios alfa de BeginRound/strikes permanecen
sin stage ni modificaciones. No afecta cápsula física, endpoints, Skin=.001,
motor mosquito, velocidades/balance, arte o colliders de ISLA/CASA.

## Hipótesis y límite geométrico antes de integrar

La query humana usa radius−Skin para iniciar separada de superficies tangentes,
con endpoints originales. Se extiende sólo un Skin adicional: longitud delta+
2Skin frente a delta+Skin anterior. Sobre cada hit retornado se calcula
closing=−dot(dirección,normal) y se resta Skin/closing de su distancia para
reconstruir el contacto con el radio completo sobre una cara plana. El solver
existente vuelve a restar Skin al decidir el avance, como antes. Se reordena
por distancia corregida porque la compensación depende de la normal. Se filtran
hits corregidos fuera del alcance delta+Skin original.

Si closing<=.0001 o closing×distance<=Skin, devuelve distancia0 conservadora:
no divide por incidencia casi cero ni extiende el barrido arbitrariamente.
Restar sólo Skin sería equivalente únicamente en incidencia frontal. Esta
compensación por normal es exacta para el mismo contacto planar retornado,
sin prometer equivalencia en aristas o superficies curvas.

**Limitación pendiente:** la query finita puede omitir un contacto rasante que
la cápsula completa habría encontrado. El aumento de distancia por reducir
radio es Skin/closing y puede superar la extensión fija adicional de1mm. No
se afirma que conserve todo clearance previo. Los tests oblicuos están para
detectar esa regresión; una mejora del sendero no bastará para aprobarla.
No se compensará una falla aumentando tolerancia2mm o modificando balance.

## Evidencia offline

Código central Gameplay.Unity más únicamente CastMotor del commit candidato,
compilado en `N:/LetMeSleep/Validation/Higgsfield/HumanMotorCandidate/compile-37d0b8b`:
0errores/0advertencias. Fuentes copiadas y hashes en receipt; no se compila el
WIP alfa del worktree. Esto acredita sintaxis/referencias, no consultas PhysX.

Fixture externo `HumanMotorContactChecks.cs`, acción `human-motor-contacts`,
config `human-motor-contacts.json`. Usa motor central directo a30Hz, cápsula
humana.25×1.72, sin reimplementar solver, geometría sintética descartable.
Casos: plano triangulado tangente; pendientes trianguladas±8°; pared frontal;
dos aproximaciones oblicuas; techo; peldaño.20m permitido/.24m bloqueado;
mosquito contra pared como control del camino no modificado. Registra posición,
distancia, tick y profundidad/collider del pico, clearance esperado/medido en
pared/techo. Requiere penetración<=2mm y conservación de límites geométricos.
El Skin existente se resta a distancia de barrido: su margen normal sobre una
pared oblicua es Skin×incidencia, no1mm constante. La prueba usa ese valor previo
con tolerancia numérica10µm. Las rutas de mapas luego prueban Authority real.

## Secuencia nativa preparada, pendiente de integración y turno

1. Coordinador integra únicamente el hunk candidato; Unity compila central.
2. Ejecutar `human-motor-contacts.json` y
   `isla-v2.original-motor-diagnostic.json`: ruta afectada22puntos ida/vuelta
   sobre ISLA original, sin hulls/proxies ni cambio de asset. Se omiten otros
   casos durante este primer diagnóstico; no es una aprobación de mapa.
3. Sólo si pasa, ampliar al lote ISLA original y CASA aplicada para comprobar
   escaleras, entradas, suelos/pendientes y vuelo real previamente aprobados.
4. Si falla, guardar pico/clearance/traza y revertir únicamente el commit
   candidato en central por coordinación. No tocar WIP alfa ni publicar hipótesis.

No se abrió Unity durante la preparación. Root conserva turno de importación
Campamento y devolverá slot después de integrar/compilar el candidato.

## Primera ejecución nativa sobre central8e9a150

Coordinador integró motor como8e9a150 y tests como1151858. Slot posterior
autorizó10regresiones y rutaISLA original, ampliando sólo si ambas pasaban.
Compilaciones externas `MapChecks/20260913-071940-537` y
`MapChecks/20260913-072001-642`,0errores/0advertencias. Motor cargado desde DLL
central, MVID `004842f1-0257-41d9-b84e-572f5438ef96`; Unity6000.3.24f1 batch CPU.

`N:/LetMeSleep/Validation/Higgsfield/HumanMotorCandidate/native-8e9a150/human-motor-contacts-20260913-072011-214.json`:
**9/10PASS**, penetración0 en todos. Plano tangente avanza3.10m; pendientes
avanzan4.46/4.66m; pared frontal conserva gap1.00005mm y techo.99999mm.
Roce oblicuo0.1 da gap.0323057mm contra mínimo esperado.0222413mm;
oblicuo1 da.307083mm contra.297003mm. Ambos deslizan>8m sin penetración.
Peldaño20cm y mosquito pasan.

Único fallo: expectativa de bloquear24cm; actor llegaZ2.08849,Y.240159 sin
penetración. **Aún no establece una regresión:** el helper infirió un límite
de altura desde TryStep(.22), pero la proyección del movimiento contra la
esquina redondeada de la cápsula también puede subir. Falta ejecutar la misma
geometría con el motor anterior para saber si la expectativa del test es válida.
Se mantiene elFAIL histórico; no se cambia balance ni se declara causa probada.

`N:/LetMeSleep/Validation/Higgsfield/IslaV2-20260913/original-motor-candidate-01/map-checks-20260913-072045-121.json`:
**rutaPASS22/22**, ida/vuelta en394ticks más30settle. Sin proxies/hulls,
colliderCandidate:null; máximo.0009417086m (0.942mm) en tick186 sobre
`Environment/Dock_Arrival_Beach_Approach_COLLIDABLE`, pies
(-.000010231, .83906436, -34.8494949), bajo tolerancia2mm.
AgregadoINCOMPLETE corresponde sólo a cobertura deliberadamente omitida en
config de diagnóstico; erroresvacíos ycleanuptrue.

No se ejecutaron48casosISLA niCASA ampliada debido al test24cm pendiente de
baseline. No se sumaron cambios de motor/geometría. Procesos37588/32464
terminaronexit0; turno devuelto/disponible para coordinación y comparación.

## Baseline24cm: comportamiento previo confirmado

Coordinador autorizó sustituir temporalmente sólo el archivo central por padre
de8e9a150, ejecutar24cm, y restaurar exactamente el candidato. Se añadió filtro
externo caseFilter, comprobando que un filtro vacío de resultados nunca daPASS.
Los nombres de casos de peldaños pasan a step-20cm/step-24cm para evitar locale.

`Run-Step24BaselineGuard.ps1` verificó blob actual igual al candidato antes de
escribir; copia de bytes guardada en
`N:/LetMeSleep/Validation/Higgsfield/HumanMotorCandidate/baseline-step24-01/UnityGameplayWorld.candidate.bytes`.
Ejecutó UnityPID2676 y restauró en finally después de su exit0. Recibo
`restore-guard.json`: estadoCANDIDATE_RESTORED_EXACT_BYTES, SHA antes/después
`c72db690508ddd361e30f82bf4a4f51120ce2d10fa67467f091962af40de7b8a`.
Blob candidato011c3733e48297d4042abf3e20f003897afa2874; baseline
9af4e025bd6f195955b119866c0900c0c5863d88. No se commiteó la sustitución temporal.

Baseline MVIDbd865ebc-d9de-444f-8024-9fe1e06fe8bd, informe
`human-motor-contacts-20260913-072507-719.json` en esa carpeta: **también sube24cm**.
En21ticks llegaY.240159169/Z2.0874424, penetración0. Candidato anterior:
21ticks,Y.240159109/Z2.088491, penetración0. La expectativa «24cm bloqueado» no
era un límite previo del motor. Se conservan ambosFAIL del criterio antiguo;
el resultado comparado es comportamiento previo preservado, no10/10retroactivo.
El origen exacto de la subida por cápsula no se trazó dentro del solver.

El coordinador autorizó ampliar tras ese resultado. ISLA original completa:
`N:/LetMeSleep/Validation/Higgsfield/IslaV2-20260913/original-motor-full-01/map-checks-20260913-072625-647.json`
da **PASS_SCOPED48/48**,263/263portalesPASS, máximo.000965312m, errores/pending
vacíos ycleanuptrue. MVID004842f1 confirma el motor candidato recompilado tras
restauración. Ningún hull/proxy ni cambio de asset. Incluye21spawns,4rutas
humanas,7vuelos por input y16stress Explore/input propio; estos últimos no se
presentan comoBotController completo.

CASA aplicada completa:
`N:/LetMeSleep/Validation/Higgsfield/CasaV1-20260913/applied-motor-regression-01/map-checks-20260913-072724-110.json`
da **PASS_SCOPED44/44**,17/17pasajesPASS, máximo.000856190862m, errores/pending
vacíos ycleanuptrue. Incluye28físicos y16patrullas GameplayRuntime real, con
30.732666–37.729120m por bot. NavigationSha256 yContentHash son los ya persistidos
documentados en CASA-SEMANTIC-NAVIGATION.md; sin override deJSON ni cambios de
geometría. Config `casa-v1.applied-regression.json`.

Procesos2176/14452 terminaronexit0, turnoCPU liberado después deCASA. El motor
candidato supera la ruta defectuosa original y estas regresiones. No se afirma
equivalencia universal de contacto rasante, cobertura de todos los mapas,
render/FPS, combate niWAN. Campamento v1 excluido por defecto conocido dewinding;
Campamento v2 debe validarse después de recibir su fuente/import correcto.
