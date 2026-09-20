# MODES-WORLD-V020 — adaptador físico y objetivos

Fecha: 2026-09-20  
Estado: implementación central compilable; fixture Unity nativo aprobado. Diagnóstico temporal de cinco candidatos completo: uno aceptado y cuatro rechazados por los criterios vigentes. El catálogo final depende de definición de producto.

## Alcance entregado

- `UnityGameplayWorld` implementa `IGameplayModeWorld` en un partial independiente.
- El catálogo del prefab produce las mismas `ObjectiveDefinition` para local, host y cliente online.
- Un actor eliminado deja de participar en colliders y consultas; al reaparecer recupera sus colliders.
- El respawn de mosquito reutiliza `TrySafe` y los puntos authored del volumen de recuperación.
- Los bots humanos reciben sólo su asignación privada y navegan hacia ella por regiones y pasajes authored. No hay teletransporte ni vector directo a través de paredes.
- La asociación de región conserva X/Y/Z. Dos pisos con el mismo X/Z no se consideran conectados sin un pasaje authored.
- El presupuesto de ruta se calcula sobre los extremos de los pasajes y los tramos dentro de cada región, no contra centros arbitrarios de volúmenes grandes.

## Candidatos de diagnóstico

Cada fila identifica un único candidato físico para comprobar el adaptador y las rutas existentes. No define el contenido final de Tareas. La repetición por cadencia la decide `TaskRules`; el catálogo no inventa resultados ni completa tareas por proximidad.

| Mapa | Objetivo | Clave visible | Blanco físico | Región |
|---|---|---|---|---|
| Isla del Laguito v2 | `isla.clean.01` | `task.isla.cabin_access` | `Path_Cabin_Access_COLLIDABLE` | `air_20_6_-4` |
| Casa del Patio v1 | `casa.clean.01` | `task.casa.bathroom_tile` | `CASA_Bathroom_Ground_TileFloor` | `gf_bathroom` |
| Campamento Pinar v2 | `camp.clean.01` | `task.camp.washroom` | `CAMP_Washroom_Floor` | `washroom_interior` |
| Yate a la Deriva v3 | `yacht.clean.01` | `task.yacht.main_deck` | `YATE_MainDeck_Continuous` | `aft_center` |
| Puerto del Faro v1 | `port.clean.01` | `task.port.lighthouse_floor` | `Lighthouse_GroundFloor` | `lighthouse_entry` |

El arnés usa provisionalmente los valores ya existentes `task.action.hold_clean`, 90 ticks de trabajo, radio 1,25 m y presupuesto de ruta 330 ticks sólo para validar el recorrido. Esos valores quedan por definir como producto. El límite técnico del catálogo runtime es 24, igual al codec online.

`V020GameplayObjectiveInstaller.ValidateCandidatesOnly()` instancia copias temporales de los cinco prefabs, prueba testigo físico y rutas desde todos los spawns humanos y destruye las copias. No guarda prefabs, no recalcula `ContentHash` y no presenta un objetivo por mapa como catálogo definitivo.

## Diagnóstico nativo de los cinco candidatos

La corrida `N:/LetMeSleep/Validation/V020/objective-candidates-04.log` terminó con exit 1 porque cuatro candidatos fueron rechazados. El resultado es válido como diagnóstico: evaluó los cinco mapas, no guardó ningún asset y mantuvo los puntos, regiones y presupuesto de 330 ticks. SHA-256 del log: `4f163c1c6d49ecfadcfA299951f0ed387402e0d3e0f4c330cffceaf2f16a9225`.

| Mapa | Contacto exacto | Aproximación | Región | Rutas desde spawns | Resultado |
|---|---:|---:|---:|---:|---:|
| Isla del Laguito v2 | PASS | PASS | PASS | 1/5 PASS; fallan 0, 2, 3 y 4 | FAIL |
| Casa del Patio v1 | PASS | PASS | PASS | 5/5 PASS | PASS |
| Campamento Pinar v2 | PASS | FAIL | PASS | 2/5 PASS; fallan 0, 2 y 4 | FAIL |
| Yate a la Deriva v3 | PASS | FAIL | PASS | 5/5 PASS | FAIL |
| Puerto del Faro v1 | PASS | FAIL | PASS | 1/5 PASS; fallan 0, 1, 2 y 3 | FAIL |

Los cinco blancos tienen testigo físico exacto y las cinco regiones existen. Por eso no corresponde cambiar el collider ni la región por conjetura. Tampoco corresponde aumentar 330 ticks: `RouteWithin` todavía no distingue en el recibo si cada fallo proviene de spawn fuera de región, grafo desconectado o distancia authored que excede el presupuesto.

El siguiente diagnóstico debe conservar todos los valores y registrar:

1. para cada aproximación fallida, el collider y punto del rayo de apoyo, `normal.y`, diferencia vertical respecto del punto authored y todos los colliders que bloquean la cápsula de radio 0,249 m entre +0,251 m y +1,469 m;
2. para cada spawn con ruta fallida, región de origen elegida, distancia del pie+1 m al volumen de esa región, validez del punto en la región destino, conectividad del grafo, distancia authored mínima y ticks requeridos antes de comparar con 330;
3. para cada corrección posterior, una prueba negativa que confirme que huecos, obstáculos y pisos sin pasaje siguen rechazados.

Sólo con esos datos se puede decidir si corresponde corregir el punto de aproximación, un pasaje authored o un presupuesto de producto. El candidato de Casa sirve como control positivo; los otros cuatro no se deben instalar todavía.

## Rechazos de autoría y runtime

El arranque de Tareas falla si falta catálogo o navegación, si el mapa no coincide, si el objetivo no resuelve exactamente un collider sólido, si el punto no tiene evidencia en el collider, si la aproximación carece de soporte/espacio, si una región es desconocida o si cualquier spawn humano no tiene ruta dentro del presupuesto. En runtime, una puerta cerrada deja la tarea en espera mediante `IsObjectiveAvailable`.

Trabajar exige rango, dirección de mira y primer impacto de raycast sobre el collider exacto. Un ID válido no puede sustituir el objeto físico.

## Pruebas

El fixture dirigido cubre:

1. eliminación y restauración de colliders;
2. ruta authored en piso inferior y línea de vista al blanco exacto;
3. rechazo de un piso superior con X/Z coincidente pero sin pasaje;
4. respawn que descarta un punto authored ocupado y usa el siguiente seguro.
5. testigo de rayo sobre un `MeshCollider` real no convexo, sin depender de `Collider.ClosestPoint`.

Compilación offline central: 0 advertencias, 0 errores, evidencia en `N:/LetMeSleep/Validation/V020/SurfaceV2Offline/build`. SHA-256: `75fc3d1ce96f8e943d65a592d26fb2ec7de61373606a3a92fe724973321a5d1b` (`compile.log`) y `79c4ee209aac54d575528ae522d7c15546a1e061dea9dc6c3661d4aa17d81da0` (`SurfaceV2.csproj`). Esta evidencia no ejecuta PhysX, Unity PlayMode, render ni WAN.

Unity/PhysX nativo más reciente: `N:/LetMeSleep/Validation/V020/surface-mode-witness-03.xml`, 16/16 PASS, 0 fallos, 0 omitidos; incluye las 5 pruebas de `GameplayModeWorldPlayModeTests`. SHA-256: `a5b20c5ad2db69c67db473e1640aeb459100c5fe69e7e75d983bf5cbc1d748b0`. Esto confirma el testigo sobre `MeshCollider` no convexo y el fixture del adaptador; no valida render ni WAN.

Huellas de las fuentes evaluadas: `5f448d8e77e979f02221c7c5a212594f1278b62ed082802594d16d21e17f7cb1` (`UnityGameplayWorld.ModeRules.cs`), `0e3a0706d765256f69e4c9e886e4b915c3c31db76363f100e5200ad514f20477` (`GameplayModeWorldPlayModeTests.cs`) y `cdf0f23257fadbdcb0ccc48055f7da44b8cfc0a91bc9d2401f2a171c16c49d83` (`V020GameplayObjectiveInstaller.cs`).

## Clearance corporal de adquisición — 2026-09-20

La adquisición de una superficie valida el volumen corporal de destino antes de
aceptar el contacto. Mientras el mosquito permanece en
`ApproachingSurface`, la misma consulta actor-aware se repite en cada tick y
desprende al actor si aparece un obstáculo. La consulta ignora los colliders del
propio actor, conserva `BlocksMotor` como filtro de solidez e incluye triggers
para no omitir superficies anatómicas de otros actores que sobresalgan de su
cápsula locomotora. No cambia alcance, primer sólido, continuidad de 2 mm ni el
timeout de 30 ticks.

Unity/PhysX nativo:
`N:/LetMeSleep/Validation/V020/clearance-anatomy-native-02.xml`, 14/14 PASS,
0 fallos y 0 omitidos. El lote contiene tres casos de clearance y once de
regresión de superficies. Verifica destino estático bloqueado, obstáculo que
aparece después de adquirir y anatomía trigger de otro actor fuera de su
cápsula locomotora. SHA-256 XML:
`6515001D2108B3B72DE4A91CF860897EDE8C5F96B6C4F8F693F7E0DA04E8B048`;
log:
`0BE8F532550FFA933C4F178B7F0A4FD65C2371E493B7D51B35937A50D101C30E`.

