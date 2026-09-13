# CASA: navegación semántica aplicada y runtime real validado

2026-09-13. Trabajo externo en worktree gameplay; sin modificar motor ni arte.
Unity6000.3.24f1 central, batch CPU sin gráficos. Turno liberado al coordinador.

Regresión posterior al cambio de motor central8e9a150:44/44casos y17pasajesPASS
sobre estos datos persistidos, penetraciónmáxima.8562mm. Informe
`N:/LetMeSleep/Validation/Higgsfield/CasaV1-20260913/applied-motor-regression-01/map-checks-20260913-072724-110.json`.
Ver comparación de baseline/peldaño en `HUMAN-MOTOR-TANGENCY-CANDIDATE.md`.

## Resultado y persistencia

`N:/LetMeSleep/Validation/Higgsfield/CasaV1-20260913/semantic-runtime-01/map-checks-20260913-065612-598.json`
da **PASS_SCOPED44/44**: los28casos físicos previos (21spawns,5rutas humanas,
2vuelos por escalera) y16patrullas reales600ticks/20s cada una. Los17pasajes
(16portales+escalera) pasan clearance estático. Errores/pending vacíos, cleanuptrue.
Compilación `MapChecks/20260913-065513-470`,0errores/0advertencias; source/config/
dependencias con hashes en receipt. Dato exacto copiado allí, no mutado al probar.

| Métrica de16patrullas reales | Resultado |
|---|---|
| Distancia por bot | 30.732666–37.729120m |
| Regiones distintas por bot | 3–8 |
| Regiones cubiertas entre todos | 16/16 |
| Mayor pausa consecutiva (<.001m/tick) | 1tick |
| Tiempo consecutivo fuera de región y sin pasaje | 0ticks |
| Mayor tránsito sin región, con pasaje activo | 103ticks |

Los spawns superiores12–14 descendieron hasta planta baja. Las rutas manuales
pasaron escalera en ambos sentidos. No se afirma que la patrulla real haya
recorrido todos los enlaces en ambos sentidos, ni que haya probado combate,
persecución a velocidad máxima, red, render/FPS o sesiones prolongadas.

Autorización posterior del coordinador: persistir exactamente el dato validado,
conservar emisión/spawn, y hacer readback sin repetir44casos. Se ejecutó
`ApplyCasaSemanticNavigation.cs` mediante UnityAPI; recibo:
`N:/LetMeSleep/Validation/Higgsfield/CasaV1-20260913/semantic-applied-01/casa-semantic-apply.json`.
Estado **APPLIED_READBACK_PASS**, proceso32880exit0.

- Ruta central: `Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-casa-del-patio-v1/Data/casa-navigation-schema1.json`.
- TextAsset GUID: `3452b5182d3f1de4c89e186e010c27b5`.
- SHA256 dato: `a18d1506f5db50e1f5472497b24982e38390391ed51667b93d34bcbd0e135251`.
- ContentHash anterior, con emisión restaurada: `53882423821aae0f458b5d7bb8afa1b0ba259dcae3199e72e1202cc4dc1462e6`.
- ContentHash nuevo: `019cca020c67d1e1d4d758556f76e3b2c553f715f21a0207f618ce3be01eede1`.

Derivación SHA256(oldHash+LF+nav-schema1+LF+navSHA256); revisión local, catálogo
global no certificado. Prefab y escena reabiertos desde disco verifican MapId,
SpatialData/path/hash, ContentHash. Huellas de transformaciones/actividad,
colliders y assets de materiales coinciden en los cuatro estados antes/después:
`f0c441442aa107de0c2a5d1d610f9c5b094d5bc2d5f11a3a92a876ab64a82038`.
Esto incluye assets de materiales con su dependencyhash, preservando emisión.
SpawnHuman03 sigue en(-2.5,.2841115,2.1). No se edita fuenteBlend/FBX.

## Autoría y contrato real

`Build-CasaSemanticProposal.py` genera16zonas: patios delantero/trasero y
jardines laterales; seis por piso: hall delantero, corredor oeste, hall trasero,
cuarto oeste, cuarto este y baño. Ninguna caja exterior atraviesa la casa.
Particiones estructurales X−1.4/+2.2 y bañoZ1.9 separan habitaciones; huecos
reales de puertas están enZ−.25/−3/3.95. Puertas de arte abiertas son estáticas,
por eso door:false; no se inventa binding GameplayDoor ni atajo por ventanas.

Las cajas clasifican habitaciones transitables, no volúmenes totalmente vacíos:
pueden contener muebles que el steering y motor reales deben evitar. Las zonas
no atraviesan muros estructurales ni la losa. El hall se divide para no clasificar
el espacio de escalera como un corredor recto hacia una pared/baranda.

Portales tienen tres puntos center±normal*.55m. Anchos reales1.2/1.3m y altura2.2m,
centrosY1.5/4.4, por debajo de linteles. Extremos quedan dentro de su habitación
correcta. Ancho de corredor oeste1.55m; el centroX−.55 deja.70/.85m laterales.
Radio del motor mosquito.055m, evasión.065m a.65m; reconocimiento waypoint.24m.
En exploración sin objetivo, BotController usa forward.5: velocidad deseada1.9m/s.
Authority acelera13m/s² y frena28m/s² cuando recibe entrada cero. Orientación
puede cambiar instantáneamente pero velocidad no: referencia ideal de frenado
1.9²/(2×28)=.0645m, más hasta.19m durante un intervalo de decisión10Hz. No es
un margen garantizado en giro/colisión; se validó la trayectoria resultante.

La única escalera recta correX1.225, desdeZ−1.33 a2.87,16peldaños y desembarco
haciaZ3.8. Schema1 tiene dos tramos; se codifican como partes de la misma recta,
con midpoint duplicado, no una escalera nueva. Puntos runtime:
`(1.225,1.4,-2)`, `(1.225,2.603,.63)` repetido cuatro veces,
`(1.225,4.2,4.25)`, `(1.225,4.2,3.8)`.
El último tramo corto vuelve.45m por el formato integrado, todo dentro del hall
superior trasero. La trayectoria conserva espacio para girar sin cruzar la
baranda oeste. `mid_landing` es un campo adaptador del schema, no afirma que haya
un descanso físico intermedio. No se agregó zona cúbica en la diagonal de escalera:
BotPatrol mantiene el pasaje activo aunque la posición intermedia no tenga región.

## Corrección del informe de voxels

El antiguo `patrol:true` llamaba Explore+input propio máximo a30Hz, sin
BotController ni SteerBot. Los15/16fallos de aquellos candidatos siguen
conservados como stress del adaptador y no demuestran fallo del bot real.
Su regla de nunca salir de una caja era más estricta que el runtime. No se
retocó ese resultado histórico para convertirlo en éxito. La nueva entrega usa
GameplayRuntime.BeginRound/TickHost auténticos, con observación, decisiones,
steering y motor integrados; diagnóstico de estado por reflexión sólo lectura.

Los assets centrales quedan para el commit de contenido del coordinador. El
commit del worker incluye helpers/configs/docs propios, preserva todo WIP alfa.
