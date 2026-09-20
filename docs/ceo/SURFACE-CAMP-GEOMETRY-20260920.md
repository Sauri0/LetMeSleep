# Camp: causa geométrica de las tres filas Frozen85

El diagnóstico nativo independiente refuta la hipótesis de falso bloqueo por
el volumen envolvente del propio MeshCollider. Las tres entradas Frozen85
empiezan dentro de un objeto cerrado; sus dos triángulos inferiores apuntan
hacia adentro. El ray acepta ese fondo como apoyo y el recorrido sigue por el
interior. Al alcanzar una pared lateral hay intersección real de la esfera.

Se conservan las filas originales FAIL; no se convierten en PASS ni acreditan
un recorrido exterior. Tampoco justifican una excepción de colisión runtime.

| Objeto | Altura del sólido | Y inicial Frozen | Distancia a cara al fallar | Radio de clearance |
|---|---|---|---|---|
| Cooler 01 | 0–0,40 m | 0,12 m | 0,052543 m | 0,054 m |
| Crate 01 Base | 0–0,52 m | 0,12 m | 0,041256 m | 0,054 m |
| Crate 02 Base | 0–0,52 m | 0,12 m | 0,053680 m | 0,054 m |

En las tres mallas, los triángulos 0 y 1 tienen orientación interior y los
otros diez, exterior. Se comprobó que los 24 vértices pertenecen al volumen
convexo de caja y que tanto el inicio como el último punto están dentro de sus
semiespacios. La distancia se calculó independientemente sobre las caras y
segmentos transformados al mundo, sin usar ComputePenetration como oráculo.

La documentación de Unity 6.3 explica que ComputePenetration ignora caras
orientadas hacia atrás, incluso al activar queriesHitBackfaces. Por eso un
resultado falso no demuestra ausencia de esta intersección.
[API oficial](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.ComputePenetration.html).

## Evidencia y alcance

- `N:/LetMeSleep/Validation/V020/SurfaceCampEscalation01/native01/geometry.json`:
  exportación Unity 6000.3.24f1 sobre checkout aislado a654d8b, mismos puntos y
  direcciones finales de Trace09; incluye todos los vértices/triángulos y hits.
- `SurfaceCampEscalation01/analysis.json` y `analyze_geometry.py`: distancias,
  pertenencia al sólido, orientación y SHA del JSON nativo.
- `SurfaceCampSelf01/surface-camp-self-native-02.xml`: seis PASS y un FAIL.
  El positivo no reprodujo self-overlap. Los nuevos negativos compartían el
  BoxCollider creado por SetUp y no prueban la nueva excepción. Se conserva
  el resultado completo sin usarlo como aprobación.
- El intento anterior native01 llevaba -quit y no ejecutó los tests; su exit 0
  no es una prueba. La invocación correcta está documentada en TEAM.md.

La propuesta de excepción y sus pruebas se preservaron con patch, originales
y hashes en `SurfaceCampEscalation01/rejected-candidate/`. Sólo esos dos archivos
asignados fueron devueltos a su versión HEAD. No se descartó WIP ajeno ni se
integró el helper de triángulos en el motor.

## Continuación

Crear positivos exteriores en caras realmente expuestas, con cuerpo fuera del
sólido y corredor libre. El fondo de estos objetos apoyados en el terreno no es
una ruta exterior válida para esos mismos inputs. Revisar por separado si la
orientación inferior incorrecta afecta alguna interacción alcanzable antes de
modificar arte o colisiones. No cambiar radios ni ignorar el collider soporte
para hacer pasar un inicio dentro de un sólido.

La captura real de entrenamiento usa nuevamente el runtime comprometido, sin
la excepción rechazada. Persisten otras verificaciones de mapas y entrega;
esta clasificación no certifica por sí sola los cinco mapas completos.
