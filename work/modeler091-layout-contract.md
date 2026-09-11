# Casa v2: contrato de arquitectura y entorno

Estado: implementación del primer tramo; pendiente de motor y revisión visual.
Propietario del generador: Modelador 1. Consumo visual: Worker 2.
Sólo `house-v2-<seed>` canónico se acepta; v1 se rechaza. No cambia red aquí.

Todas las coordenadas están en metros del mapa. `floor` empieza en cero.
Se conservan `floor_levels`, `rooms`, `corridors`, `stair_holes`, `steps`,
`barrier_parts`, `barrier_boxes` y el resto del contrato físico anterior.
`corridors` conserva exactamente tres AABB por planta, en orden central,
norte, sur. Los laterales nuevos están en `circulation_routes`, no en ese array.

## Dimensiones

`layout_dimensions: Dictionary` contiene:

| Clave | Tipo | Valor/significado |
|---|---|---|
| floor_height, wall, exterior_wall | float | 3.2, 0.2, 0.25 |
| stair_offset, room_inset | float | 3.65 y 7.1 desde fachada lateral |
| hall_end_min, hall_end_max | float | 6.4–6.9: coordenada absoluta de pared exterior del distribuidor |
| stair_width, hole_width, run_half | float | 2.8, 3.2, 4.0 |
| rise, tread | float | 0.2, 0.5 |
| steps | int | 16 por tramo |
| central_hall_width | float | 3.2–4.0 útiles entre caras de paredes |
| finish_allowance | float | .06 reservados por cara para acabados visuales |

Los laterales tienen 1.5825 m interiores y 1.6325 m exteriores declarados
libres, descontando una reserva de 6 cm por cara (12 cm total). Las distancias
físicas entre sólidos son 1.7025 y 1.7525 m. Worker 2 debe respetar esa reserva
y comprobar mallas/molduras reales durante QA. Los descansos miden 2.8 m de ancho
y al menos 2.25 m de profundidad. El diámetro físico humano actual es 1.20 m.
Los mínimos QA 1.50 m de circulación, 1.40 m de escalera, puerta 1.00 m,
huella .28 m y contrahuella máxima .22 m son compatibles. Se conservan
puertas de 2.00 m y escaleras de 2.80 m; no se reducen a los mínimos.

## Escaleras

`stair_connections: Array[Dictionary]`, una entrada por tramo entre plantas:

- `id: String`, estable `stair-<floor>-<side>`.
- `floor: int`, planta inferior; `direction: float`, +1 o -1 a lo largo de Z.
- `bottom, top: Vector3`, posiciones de pies para el acceso al tramo.
- `width, center_x, run_start_z, run_end_z, hole_width: float`.
- `rise, tread: float`, `steps: int`.
- `bottom_landing, top_landing: AABB`: volumen libre desde suelo hasta 2.05 m,
  descontando pared/baranda; no son losas ni nuevos sólidos.

`stair_light_anchors: Array[Dictionary]`, una entrada por tramo:

- `id: String`, `<stair-id>-light`.
- `p: Vector3`, punto dentro del hueco, bajo el siguiente tramo si existe.
- `target: Vector3`, objetivo dentro del mismo tramo, diferente de `p`.
- `range: float`, 7.5 m. Valores finitos.

Luz inicial: p=(center_x, floor*3.2+3.6, -1.5*direction),
target=(center_x, floor*3.2+1.8, .5*direction). Son propuestas a revisar
visualmente con Worker 2; el generador es la fuente de esos puntos.
Presupuesto: 2 plantas <=20 habitaciones +4 distribuidores +2 tramos =26;
3 plantas <=22 habitaciones +6 distribuidores +4 tramos =32 luces.
El límite de habitaciones se aplica en la generación estructural.

## Circulación

`circulation_routes: Array[Dictionary]`, tramos horizontales obligatorios:

- `id: String`, `route-<índice>` estable para semilla/versión.
- `kind: String`: `central`, `cross_hall`, `stair_side` o `landing_link`.
- `floor: int`; `from, to: Vector3` en pies, sin altura de cámara.
- `clear_width: float`, ancho libre exigido transversal al recorrido.
- `clearance: AABB`, banda libre completa, base 1 cm sobre el suelo,
  altura 2.05 m; excluye superficies de apoyo.

Cada planta tiene cuatro recorridos laterales: interior y exterior de ambos
huecos, conectados a los dos distribuidores. El grafo incluye todos los tramos;
la validación debe comprobarlos en ambos sentidos y el volumen declarado.
Los anchos mínimos no se deducen de una línea central que podría esquivar
obstáculos: el AABB completo se contrasta con sólidos y barridos de puertas.

## Validación y límites

Prueba nueva `modeler091_layout_test.gd`: IDs, determinismo, contrato, volumen,
soporte de descansos, rutas laterales y rechazos de obstrucciones inyectadas.
Corpus y capturas requieren turno del Director. El mobiliario y la zonificación
serán un tramo posterior. No hay afirmación de QA visual ni WAN completados.
