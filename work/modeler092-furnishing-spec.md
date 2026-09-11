# 0.9.2: mobiliario funcional y contrato de interacción

Implementación aislada en curso; runtime v2 de0.9.1 congelado. Depende del plano v3 de
`modeler092-implementation-plan.md`; no necesita nuevos assets ni decisiones
estéticas del usuario.

## Equipamiento por uso

Contar esenciales por ID de asset, no por etiqueta traducida ni por número
total de cajas. `dresser` o `wardrobe` satisfacen guardado de dormitorio.

| Uso | Esenciales | Total orientativo | Ocupación de huella |
|---|---|---|---|
| baño compacto | bath_vanity, toilet, bath_shower | 3 | 18–35% |
| lavadero compacto | washer, hamper, wardrobe, table | 4 | 18–35% |
| despensa compacta | pantry_shelf, pantry_crates, dresser, table | 4 | 18–35% |
| cocina-comedor | sink, stove, fridge, dining_set, table, dresser | 6–8 | 14–28% |
| dormitorio individual | bed, wardrobe/dresser, desk/table/nightstand | 4–6 | 12–25% |
| dormitorio familiar | >=2 bed, >=2 elementos de guardado, superficie | 6–8 | 14–28% |
| estar | sofa, armchair, table, bookcase | 5–7 | 14–28% |
| recibidor/trabajo | wardrobe, desk/table, asiento, dresser | 4–6 | 12–25% |
| música/lectura | piano, asiento, bookcase, table | 6–8 en cuarto compuesto | 14–28% |
| costura/estudio | sewing_table, wardrobe, desk/table, dresser | 6–8 | 14–28% |
| juegos/lectura | game_table, asiento, bookcase, table | 6–8 | 14–28% |

**Baño:** quitar la mesa auxiliar independiente del plan de colocación, sin
editar FurnitureBlueprint. El vanitory cumple la función de apoyo doméstico;
no se convierte automáticamente en soporte de herramienta: su lavabo no es
una mesa plana validada. El baño no necesita un pickup propio. Existen al
menos siete habitaciones en cada planta y se garantizan las cinco herramientas
en otras superficies/soportes accesibles. Tres sanitarios reales satisfacen
el mínimo actual y evitan amueblar el baño como oficina.

Ocupación = suma de huellas físicas de muebles / área útil de habitación;
no contar tareas, soportes pequeños, decoración ni áreas de circulación como
ocupación. Rangos de la tabla son objetivos de composición. Hard gates:
esenciales completos, máximo 40% ocupado y acceso real a todos los grupos.
Por debajo del objetivo se intenta un complemento funcional hasta ocho objetos;
si no cabe, se registra `under_target`, no se coloca un obstáculo para inflar
una métrica. La entrega visual no acepta un salón grande claramente vacío.
Presupuesto total inicial: <=144 muebles por casa y <=8 por habitación;
revisar rendimiento antes de ampliar. Ningún límite de FPS nuevo.

## Composiciones, con anclajes y acceso

Usar el espacio local del cuarto: Z positivo hacia su interior desde puerta,
X a lo largo del muro de entrada. Convertir al mundo mediante rotación y
traslación; no escalas negativas ni cambios de tamaño para que un modelo quepa.
Respetar dimensiones y `visual_scale` medidos del blueprint.

- Servicios: sanitarios/aparatos contra paredes, frente hacia zona libre.
  Ducha en esquina opuesta a la entrada; vanitory en lateral; inodoro en
  otro lateral con aproximación frontal. Lavadero/despensa: apoyos bajos bajo
  ventana, guardado alto a un lado de la entrada o en pared lateral.
- Cocina/comedor: zona de cocina en el tercio/mitad próximo a la despensa;
  pileta y horno sobre el perímetro, heladera en un extremo, mesa auxiliar
  separada del paso. Comedor en el otro extremo con su acceso propio.
- Dormitorio: cabecera en pared y mesa de luz a <=.35 del costado de la cama;
  frente del guardado hacia pasillo interior. Escritorio separado del pie de
  cama. En familiar, dos camas con pasillo >=1.30 entre grupos, no pegadas
  entre sí para incrementar ocupación.
- Estar/lectura: sofá y sillón mirando hacia la mesa/zona común; al menos
  un asiento se coloca en el interior, no todos alineados al perímetro.
- Música/costura/juegos: mueble específico con frente accesible, biblioteca/
  guardado en pared, mesa y asientos relacionados. La segunda zona funcional
  tiene un grupo propio y una ruta desde el punto principal.

Se puede duplicar una especificación del blueprint para complementos, con
`pickup_surface=false`. Para usos compuestos, combinar los planes, deduplicar
mesas auxiliares y asignar una sola superficie principal de pickup. No perder
sink/stove/fridge para que entre una segunda mesa decorativa.

## Colocación acotada y determinista

1. Calcular volumen interior, ventanas y barrido de puerta. Reservar banda
   puerta-punto principal de 1.56 m y 2.40 m de altura, y accesos a grupos
   de 1.30 m y 2.05 m de altura. Ningún spawn sobre cama/mesa.
2. Evaluar hasta cinco offsets de puerta y hasta cuatro orientaciones de
   composición. Usar 6 variantes de anclaje por grupo (esquinas, pared larga,
   grupo interior), con orden determinista por subsemilla e ID.
3. Colocar esenciales completos antes de complementos. Hacer retroceso local
   acotado a 16 estados candidatos por cuarto, en lugar de aceptar los tres
   primeros objetos y omitir el sanitario que no entró.
4. Contrastar huella y altura visual con límites, otros muebles, ventanas y
   todas las bandas reservadas. Colocar muebles relacionados a distancias
   definidas por sus bounds, no por centros aleatorios.
5. Para el barrido de hojas cardinales a90°, usar su sector circular completo
   en coordenadas de bisagra, con margen para grosor y redondeo. Contrastar
   el sector con posiciones reales de DoorGeometry a ángulos intermedios.
   No usar sólo hoja cerrada/abierta para validar un mueble dentro del arco.
6. Elegir la primera composición que satisfaga todos los esenciales y accesos;
   optimizar complemento/ocupación dentro de ese conjunto. Si ninguna cabe,
   informar error con semilla, cuarto, bounds y esencial faltante. No cambiar
   a otra semilla dentro del test ni tratar esa habitación como terminada.

No se permite usar el camino de un rayo inflado como única prueba de acceso:
el grafo nominal y la autoridad física tienen comportamientos distintos junto
a las puertas. Se exige recorrido real cuando exista un contacto conservador.

## Soportes y tareas: fuente única de orientación

Problema actual: la colocación de pickups y `_assign_tasks` recalculan su
aproximación usando Z, mientras un mueble puede estar girado. v3 debe publicar
la aproximación una vez, desde la colocación validada.

Por superficie elegida, guardar en `room.pickup_surface: Dictionary`:

- `structure_id: String`, `box: AABB` físicos ya publicados.
- `approach: Vector3` pies, `access: AABB`, `facing: Vector3` horizontal unitario.
- `support_point: Vector3` medido sobre la superficie útil; `rotation: Vector3`
  para la herramienta colocada (contrato de PickupPlacement).
- `task_display_p: Vector3`, `task_display_yaw: float`: otra zona de la mesa,
  separada de la huella de la herramienta.

Derivar y conservar `pickup_table`/`pickup_approach` como compatibilidad interna
mientras se migran consumidores. Pickups, tareas, props y nav links leen esta
metadata; no vuelven a inferir un costado por signo de Z. World ya recibe
`station.display_p/display_yaw`: ese contrato no cambia y Worker 2 no necesita
interpretar nuevas rotaciones para las tareas.

Primera implementación: superficies principales con las orientaciones actuales
0/180; liberar 90/270 sólo cuando soporte visual, posición de pickup, prop de
tarea y accesos pasen su prueba conjunta. Esto es una secuencia técnica, no
una decisión pendiente del usuario.

## Ocho tareas y herramientas

Asignar con restricciones primero: mantas/sábanas a dos cuartos con cama;
ventana/mosquitero a dos habitaciones exteriores con acceso a ventana;
vajilla preferentemente cocina-comedor; equipo a estudio/música; repelente
al recibidor/estar; ventilador a estar/dormitorio. Ocho habitaciones distintas,
todas alcanzables y al menos dos plantas representadas.

En cada planta: swatter, racket y newspaper sobre tres superficies validadas;
broom y slipper en sus soportes actuales. No depender del índice de cuarto
módulo tres después de excluir el baño: asignar explícitamente los tres tipos
a superficies distintas y distribuir extras después. Reservar accesos de estos
soportes antes del último pase de complementos.

No generar nuevos task IDs, tool IDs o assets; no editar simulation/World/
FurnitureBlueprint sin transferencia del Director.

## Contrato acordado con Worker 2

Cada estructura furniture conserva `room` y publica `room_id:String`,
`functional_zone_id:String`, `essential:bool` y `placement_order:int` estable
y consecutivo por mapa. Worker2 puede distinguir complementos de esenciales
para medir costo; `functional_zones[].bounds` no crea focos ni colisiones.
