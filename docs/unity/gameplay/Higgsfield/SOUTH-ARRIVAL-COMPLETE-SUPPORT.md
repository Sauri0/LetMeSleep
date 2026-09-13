# Sendero sur: soporte completo, avance y penetración en unión

2026-09-13, API Unity sólo sobre clon temporal. Ningún candidato ISLA persistido.
Se conservan motor, render mesh, materiales, FBX y demás colliders del mapa.

## Evidencia incremental

| Variante | Ruta al muelle | Resultado |
|---|---|---|
| Original | atascoZ−24.428965 | distance0 sobre hoja original, sin penetración |
| Franja3filas |5/22, atascoZ−27.385117 | mismo patrón en hoja original restante |
| Soporte completo,16hulls de1–3filas |22/22, ida/vuelta completa | FAIL por penetración transitoria18.9319mm |

Franja ejecutada por root:
`N:/LetMeSleep/Validation/Higgsfield/IslaV2-20260913/south-collider-candidate-01/map-checks-20260913-063938-908.json`.
Nueva posición de pies(-.0000016,1.681134,-27.385117), groundedtrue. Forward+gravedad
devuelve distance0/point0/normal(-.000001,.159232,.987241) opuesta al barrido;
snap0 normalup. Ambos sobre Path_South_Arrival_COLLIDABLE restante, fuera de la
franjaZ[−25.385,−23.87]. Terrain_Island da soporte.07495875m más abajo. El contacto
se trasladó2.956m al sur: no fue un bloqueo sobre el hull ni un escalón alto.

Soporte completo:
`south-complete-support-01/map-checks-20260913-065522-374.json` bajo la misma raíz.
47/48casos PASS,263/263portalesPASS; llegada/regreso22/22 en394ticks de movimiento
(tickfinal424incluye30settle). Initial/finalpenetration0, máximo.0189318657m.
No se rebaja la tolerancia2mm. Los16casos antiguos de Explore/input propio están
etiquetados stress, no certifican BotController real.

Instrumentación adicional, misma geometría:
`south-complete-support-diagnostic-02/map-checks-20260913-065657-302.json`.
Pico tick36, pies(.010004811,2.77558231,−19.6220627), collider
`Environment/Path_South_Arrival_COLLIDABLE/SouthPathSupport_32_34_TEMP`.
Ese hull cubreZ[−20.8399982,−19.829998]. El centro del actor está.208m fuera del
extremo norte, dentro del alcance de su cápsula. El defecto aparece junto a la
unión del soporte convexo, seis ticks después de empezar a caminar; no en el
atasco original ni en el muelle. Este dato localiza una interacción en el borde,
pero no prueba por sí solo un bug del motor o del cocinado PhysX.

## Construcción limitada al sendero

`CompleteSouthArrivalSupport.cs` comprueba el objeto esperado, source de320
triángulos y41filasX/Z. Particiona por filas completas sin duplicar/omitir caras:
40filas, ancho3.2m, longitud20.2m (Z−37..−16.8). Cada sólido conserva vértices
superiores y extruye.04m hacia abajo, con caras laterales cerradas. Desactiva
únicamente el MeshCollider original en el clon; conserva su MeshFilter. Hereda
GameplaySurface/layer/material físico/contactOffset/cookingOptions sin cambios.

Variante inicial agrupa como máximo3filas mientras su cota de elevación sea
<=.0099m. Resultó16hulls,560vértices de entrada sumados. Una cota por plano de
soporte con covarianza2×2 y margen numérico10µm limita el cambio vertical antes
del cocinado a.009704672m. Esto **no** acota ComputePenetration ni garantiza la
forma cocinada. El pico18.93mm es motivo suficiente para no persistirla.

`isla-v2.complete-support.json` reproduce el lote completo. La variante
`isla-v2.one-row-diagnostic.json`, autorizada después, sólo ejecuta ida/vuelta
con40hulls de una fila; no repite casos ajenos. Su resultado se agrega abajo.
`diagnosticRoutesOnly` etiqueta explícitamente la cobertura omitida como pending,
de modo que una rutaPASS no se confunda con aprobación del mapa.

## Una fila por hull: tampoco aprobada

Informe `south-one-row-diagnostic-01/map-checks-20260913-070433-691.json`.
Compilación `MapChecks/20260913-070311-534`,0errores/0advertencias. Proceso25836
terminóexit0, cleanuptrue; sólo se ejecutó la ruta afectada, sin repetir47casos.
40hulls, cota previa máxima.008834921m, espesor.04m, visual intacto.

**FAIL6/22**. Pico.0193524361m en tick36, pies(.010602839,2.77569222,−19.617300),
contra `SouthPathSupport_33_34_TEMP`. Comparte la fronteraZ−19.83 con el pico
de la variante agrupada; reducir la longitud del hull no elimina el defecto.

Atasco posterior en(-.000112265,1.40626657,−29.2380848), groundedtrue,
penetraciónfinal0. `SouthPathSupport_15_16_TEMP` devuelve distance0/point0 en
horizontal, forward+gravedad y snap; normales opuestas al barrido. Ya no es la
hoja cóncava original: el mismo patrón puede ocurrir sobre un hull convexo.
En ese punto también aparece Path_Outer_Coast con snap.005274699m y terreno
con snap.07548257m; el contacto0 del soporte sur es el primero.

Se midieron los valores reales de source y se copiaron sin cambios:

| Propiedad | Valor |
|---|---|
| contactOffset | .01m |
| cookingFlags | 30 |
| cookingOptions | CookForFasterSimulation, EnableMeshCleaning, WeldColocatedVertices, UseFastMidphase |

No se cambió contactOffset, Skin, gravedad, límite2mm ni opciones de cocinado.
Las fuentes integradas muestran que el motor resuelve penetración antes de
mover, proyecta hasta5barridos y luego hace snap; no hay una pasada final de
depenetración. Esto permite investigar la aparición del pico al terminar un
tick, pero **no demuestra cuál de las consultas lo introduce**. Hacen falta
trazas acotadas de Overlap/CastMotor/TryStep/snap en el tick36 y medidas de la
forma cocinada para distinguir unión geométrica de divergencia entre queries.
No se autoriza aquí ningún cambio del motor ni se recomienda publicar esta
geometría fallida. El soporte agrupado completa la ruta pero falla penetración;
el soporte por fila falla ambas condiciones. ISLA sigue abierta.

Turno CPU devuelto al coordinador tras este diagnóstico; CASA persistida no
se tocó durante las pruebas de ISLA.
