# Sendero sur: contacto tangente y candidato local de collider

**Actualización:** el candidato local fue ejecutado por el coordinador y el
soporte completo por el worker. Ambos avances y fallos se documentan en
`SOUTH-ARRIVAL-COMPLETE-SUPPORT.md`. Este informe conserva la propuesta original;
su sección «sin ejecución nativa» describe el estado previo, no el actual.

Investigación offline 2026-09-13, sin abrir Unity/Blender ni alterar assets.
El archivo `preparation-04/measured-path-meshes.json` no contiene este sendero;
se extrajo la geometría del **FBX realmente importado** con `Inspect-IslaFbx.py`,
lector binario Python estándar. Arte original preservado.

Evidencia: `N:/LetMeSleep/Validation/Higgsfield/IslaV2-20260913/SouthArrivalCollider/`:
`south-arrival-source-mesh.json` y `south-arrival-geometry-audit.json`.
Fuente SHA256 `8ce74c4f24dbbc1e11896c206ba2e9879b4826f3047cafb3ad2cf086b508d633`.
Modelo FBX `Path_South_Arrival_COLLIDABLE`, `collision_role:static_solid`.
Su bbox convertido X/Z/Y coincide con el bbox del native review, salvo
micrómetros de transformación. El FBX describe una hoja abierta sin espesor.

## Hallazgos

| Comprobación offline | Resultado |
|---|---|
| Topología | 640 vértices FBX /205 posiciones exactas, 320 triángulos |
| Degenerados / triángulos repetidos / bordes no manifold | 0 /0 /0 |
| Retícula | 5×41 posiciones, 160 celdas, exactamente 2 triángulos por celda |
| Bordes | 88 de perímetro, 436 internos; sin agujero interno |
| Área mínima | .202035 m² |
| Pendiente máxima | 9.027°; normal up mínima .987614 |
| Par cercano al contacto | Triángulos fuente 195/196, costura longitudinal X=0 |
| Diferencia entre normales de ese par | .187567° |

El import nativo registra 210 vértices, no 640: el mesh importado ya comparte
muchas posiciones. El importer usa MeshCollider no convexo ligado al mismo
mesh visual. No se halló una clasificación de colisión equivocada. Cambiar
normales de render o quitar arbitrariamente triángulos no tiene justificación.

Pies registrados: `(0.000026,2.119883,-24.428965)`; centro de esfera inferior
`(0.000026,2.369883,-24.428965)`, radio .25m. La geometría fuente da distancia
.25000249m al triángulo196 y .25000316m al195: tangencia a escala de micrómetros.
La precisión de transformación FBX→Unity impide decidir el signo del contacto
a esa escala; no es una prueba offline de penetración PhysX.

El triángulo196 tiene normal `(.002561,.989244,−.146254)`, y195
`(−.000712,.989247,−.146255)`: piso casi horizontal. En cambio el cast nativo
fallido devolvió distance0, point0 y normal `(.000045,.159232,.987241)`, opuesta
al barrido forward+gravedad. Ground snap devolvió distance0 en el mismo camino.
El terreno está .075009m debajo; no participa en el primer contacto.

**Causa probable:** interacción de barrido que empieza casi tangente con la
hoja triangular y su costura interna, que produce un contacto de distancia
cero. El motor central proyecta el desplazamiento contra esa normal opuesta;
puede consumir los cinco intentos sin avanzar, y snap0 conserva la posición.
ComputePenetration no informa profundidad positiva. Esto concuerda con las
muestras inmóviles y descarta el diagnóstico de un escalón alto. No queda
demostrado que el defecto sea de la geometría fuente: es una interacción de
colisión. No se modificó el motor, Skin, gravedad ni step.

## Candidato aislado, todavía sin ejecución nativa

`SouthArrivalColliderCandidate.cs` usa Unity API **sólo sobre el clon temporal
del fixture**. Se activa mediante `colliderCandidate:south-arrival-local-convex-band`.
No tiene una operación que guarde prefab, Mesh asset, escena o ContentHash.

Transformación reproducible:

1. Comprueba isla v2, un MeshCollider sur no convexo, source de320 triángulos
   compartido con MeshFilter.
2. Separa únicamente24 triángulos de tres filas completas de la retícula:
   Z desde −25.385000 hasta −23.870001, ancho X de−1.6 a1.6. El resto del mesh
   de colisión conserva sus triángulos originales. No cambia MeshFilter/material.
3. Para la franja usa20 posiciones superiores originales y copias .04m hacia
   abajo, forma un sólido cerrado y deja cocinar un **collider convexo** de40
   vértices de entrada. Mantiene layer, material físico y contactOffset; hereda
   el GameplaySurface existente, sin nuevos IDs. Añade un solo collider.
4. La envolvente convexa cambia únicamente el soporte de esa franja. Una cota
   por plano de soporte demuestra elevación máxima <=.00833606m sobre las
   superficies triangulares originales, antes del cooking. El helper recalcula
   la cota con vértices importados y aborta si supera .01m. La frontera comparte
   posiciones con el resto del camino; se debe probar su unión en ambos sentidos.
5. DestroyImmediate elimina el clon y los dos meshes generados al terminar.

La cota es un máximo conservador, no una medición del collider cocinado. La
convexificación podría no resolver el contacto o trasladarlo a una frontera.
No declarar fix hasta pasar el recorrido completo y revisar llegada/salida.
Cerrar una malla manteniendo exactamente los mismos triángulos cóncavos no
garantiza cambiar el barrido que falla; por eso el candidato prueba una forma
convexa local en vez de un simple cambio cosmético de normales.

## Verificación preparada para un slot posterior

Compilación offline: 0 errores/0 advertencias en
`N:/LetMeSleep/Validation/Higgsfield/MapChecks/20260913-061137-048`.
Config `isla-v2.collider-candidate.json` junto al helper mantiene los48 casos del
baseline y activa el candidato. El wrapper de esa compilación ejecuta el config
copiado sin instalar DLLs en Assets. **No se ejecutó durante esta investigación.**

Comparar con baseline de `final-applied-02/map-checks-20260913-055804-946.json`:
exigir48/48, ida/vuelta completa al muelle, ninguna penetración >2mm, ningún
fallo nuevo en los263 portales, y receipt del candidato con24 caras separadas,
40 vértices y cota <1cm. Si falla en la unión, conservar las coordenadas y no
extender la modificación al resto de la isla sin revisar la evidencia.

El turno de Unity posterior está asignado a CASA; este candidato de isla queda
pendiente del slot específico que decida el coordinador.
