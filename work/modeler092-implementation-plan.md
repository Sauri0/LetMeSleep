# Casa 0.9.2: especificación implementable del generador v3

Estado: diseño preparado, sin modificar runtime v2 ni ejecutar motor.
Se implementará después del cierre/publicación de 0.9.1 por Director.
Este documento sustituye las opciones abiertas de `modeler091-zoning-plan.md`
para el siguiente tramo. Las dimensiones son decisiones de diseño; todavía
no son resultados de pruebas del generador v3.

Plano navegable: `modeler092-floorplans.html`. Coordenadas editables de los
cuatro casos nominales: `modeler092-floorplans.json` (PB, alta de2pisos y dos
altas de3pisos). Dibujos de diseño, no capturas del juego.

## 1. Decisiones cerradas

- Nueva identidad `house-v3-<seed>`. No reutilizar v2 para esta geometría.
  Director decide app/protocolo y autoriza el cambio al iniciar implementación.
- Dos o tres plantas; habitaciones de dormir principales arriba, servicios y
  vida común en PB. Un baño completo por planta.
- PB de 8 habitaciones físicas; una planta alta de 8–10 en casa de dos plantas,
  o dos plantas altas de 7 cada una: total 16–18 o 22.
- Algunos ambientes grandes contienen dos usos complementarios sin pared
  intermedia: cocina/comedor, música/lectura o costura/estudio. Son una sola
  habitación, puerta y luz, con grupos de muebles específicos para ambos usos.
- Mantener escaleras, cuatro pasos laterales por planta, ancho de puertas,
  reservas de acabados y contratos de circulación ya validados en v2.
- Reutilizar los GLB y FurnitureBlueprint existentes, en sólo lectura. No
  requerir nuevas mallas para resolver proporciones, equipamiento o disposición.

## 2. Envolvente y cotas

| Parámetro | Decisión v3 | Motivo |
|---|---|---|
| `half_x` | 13.00–14.00, paso .05 | reducir salas largas sin perder los pasos laterales |
| `half_z` | 11.00–11.40, paso .05 | fondos exteriores proporcionados |
| `hall_half` | 1.70–1.80, paso .05 | eje central físico 3.20–3.40 libres |
| extremos distribuidores | 6.40–6.60, paso .05 independiente N/S | descanso suficiente y servicios con fondo útil >=4.05 |
| escalera/hueco | 2.80/3.20; 16 x .20 de subida, .50 de huella | preservar locomoción |
| offsets lateral/room | 3.65/7.10 desde fachada | pasos físicos 1.7025/1.7525 |
| reserva de acabados | .06 por cara | pasos declarados 1.5825/1.6325 y descansos >=2.13 |
| paredes interiores/exteriores | .20/.25 | mismo contrato de superficies |
| altura entre plantas/puerta | 3.20/2.45; puerta 2.00 de ancho | no reducir circulación para decorar |

La reducción de envolvente afecta rooms, muros, losas, huecos, spawns,
ventanas/entorno dependientes de bounds y rutas: repetir todas sus pruebas.
Los pasos laterales conservan ancho porque ambos límites se expresan como
offsets de la misma fachada. No se introducen huecos de escalera distintos.

## 3. Planta nominal exacta

Caso de referencia: half_x=13.50, half_z=11.10, hall_half=1.75, distribuidores
en z=+-6.50. Unidades de coordenadas: metros. Rectángulos indican **bounds
nominales**, antes de descontar el grosor interior de paredes.

| Banda | X | Z |
|---|---|---|
| cuadrante NO | -13.25 a -1.75 | -10.85 a -6.50 |
| cuadrante NE | 1.75 a 13.25 | -10.85 a -6.50 |
| cuadrante SO | -13.25 a -1.75 | 6.50 a 10.85 |
| cuadrante SE | 1.75 a 13.25 | 6.50 a 10.85 |
| habitación central O | -6.40 a -1.75 | -3.80 a 3.80 |
| habitación central E | 1.75 a 6.40 | -3.80 a 3.80 |
| eje N/S | -1.75 a 1.75 | -10.85 a 10.85 |
| distribuidor N/S | -13.25 a 13.25 | -6.50 a -3.80 / 3.80 a 6.50 |
| centro de escaleras | x=-9.85 / 9.85 | tramo -4.00 a 4.00 |

### PB: ocho habitaciones

1. NO exterior, X=-13.25..-9.40: lavadero.
2. NO medio, X=-9.40..-5.60: baño.
3. NO interior, X=-5.60..-1.75: despensa.
4. NE completo: cocina y comedor, dos grupos funcionales.
5. Centro O: recibidor y pequeño espacio de trabajo/guardado.
6. Centro E: sala de estar, relacionada con cocina/comedor por eje central.
7. SO: juegos y lectura, con ambos grupos amueblados.
8. SE: estudio y música, con piano/escritorio/lectura.

Los tres servicios del NO tienen ~16.5 m² nominales y ~15–16 m² útiles en
este caso. Se cambia la partición binaria por dos cortes en ese cuadrante;
consume dos divisiones y mantiene ocho habitaciones totales. Así no se
asigna un baño a un rectángulo de 50–80 m².

### Planta alta: base de siete habitaciones

- NO: baño compacto X=-13.25..-9.50 y dormitorio X=-9.50..-1.75.
- NE: dormitorio familiar, con dos camas y guardado; no una cama aislada en
  toda la superficie. En casa de dos plantas dividir este cuadrante en dos
  dormitorios de ancho nominal ~5.75: la planta pasa a ocho habitaciones.
- Centro O: biblioteca/estudio. Centro E: dormitorio individual con escritorio.
- SO: música y lectura. SE: costura y estudio. En la segunda planta alta de
  una casa de tres plantas, alternar SO/SE entre juegos, lectura, música y
  trabajo; al menos dos dormitorios reales en esa planta.
- Con sólo dos plantas, opcionalmente dividir SO y/o SE en dos cuartos
  individuales de esos usos: 8–10 habitaciones arriba, 16–18 en total.

La variedad no intercambia baños con salones: se conserva el patrón doméstico
y varían orientación de alas, cortes admitidos, dimensiones, habitaciones de
ocio, posiciones de puertas y composiciones de mobiliario.

## 4. Variación determinista sin decisiones adicionales

Separar subsemillas de estructura, asignación y muebles; generador entero
estable, sin RNG global ni dependencia de nombres traducidos. Orden final
de IDs por planta, banda, lado y columna; empates por ID.

1. Elegir dos/tres plantas, dimensiones de la tabla y ala de servicios
   (O/E), banda de servicios (N/S). Construir coordenadas por esos parámetros;
   no reflejar mallas mediante escalas negativas.
2. Reservar PB: cuadrante de servicios con tres columnas; cuadrante opuesto
   de la misma banda para cocina/comedor. Estar en el centro de ese lado.
3. Cada servicio tendrá ancho libre 3.40–4.10, fondo 4.00–4.70 y área útil
   entre13.60 y20.00m²; no asignar un cuarto mayor como baño/despensa/lavadero.
   Calcular los
   cortes repartiendo el sobrante sobre tres anchos mínimos, con jitter
   cuantizado de hasta .10; si un jitter viola el mínimo, usar el corte
   nominal del mismo template. Eso no cambia de semilla ni oculta un fallo.
4. Arriba, baño sobre el cuadrante de servicios (no necesariamente el mismo
   sanitario exacto): ancho nominal 3.70–3.90, resto dormitorio. En dos plantas
   dividir NE y opcionalmente SO/SE; en tres plantas no exceder siete rooms
   por planta alta.
5. Elegir usos secundarios de los pools compatibles y numerar nombres sólo
   cuando se repite ese uso. Los IDs y validación usan semántica estable.
6. Colocar portales orientados a vecindad: cocina/comedor cerca del eje central
   (borde interior +1.25..1.40), despensa en la columna próxima al eje;
   estar cerca del distribuidor de cocina, entrada cerca del opuesto.
   Servicios: centro de cada columna, con alternativas de offset 0, +.15,
   -.15, +.30, -.30 para alojar muebles al costado de la bisagra; elegir la
   primera composición completa y válida, con jitter final <=.10 sólo si
   conserva sus reservas. Validar ancho,
   jambas >=.25, barrido real y ruta puerta-centro en todos los casos.

## 5. Relaciones y distancias de aceptación

Medir recorridos en el grafo horizontal real, entre **portales**, con puertas
abiertas; no distancia directa que atraviesa paredes. Validar además conexión
desde cada portal a sus puntos funcionales mediante Geometry.

| Relación | Contrato |
|---|---|
| cocina y comedor | mismo cuarto, zonas contiguas, acceso entre grupos >=1.30 |
| despensa -> cocina | mismo distribuidor, recorrido entre portales <=11 m |
| lavadero -> despensa | mismo cuadrante de servicios, recorrido <=12.5 m |
| cocina -> estar | cuarto central del lado social, recorrido <=12 m |
| baño -> dormitorios de su planta | alcanzables, sin usar escaleras; <=30 m entre portales |
| entrada -> eje y ambos distribuidores | sin atravesar otro cuarto |

No colocar puertas nuevas entre habitaciones para pasar una métrica: generar
los portales adecuados en los muros que ya dan a circulación pública.

## 6. Metadata nueva y compatibilidad visual

Por habitación, conservar `id/name/label/bounds/floor/color/portal/door_axis`,
`theme_id` y datos de interacción existentes. Añadir:

- `zone: String`: `service`, `social`, `sleep` o `work`.
- `uses: Array[String]`: uno/dos IDs existentes de FurnitureBlueprint; el
  primero también es `theme_id` para los consumidores actuales.
- `area_m2: float`: área interior útil, después de paredes; no área decorativa.
- `functional_zones: Array[Dictionary]`: `{id:String,use:String,bounds:AABB,
  anchor:Vector3}`. No crean colisión, puertas, habitaciones ni luces extras.
- `furnishing_report: Dictionary`: área ocupada, esenciales esperados/puestos,
  objetos totales y acceso por grupo; también exportado por los tests.

Mantener un punto principal de habitación/spawn y rutas explícitas a las
zonas funcionales. El grupo de comedor de un cuarto `theme_id=kitchen` no
exige un nuevo theme ID ni cambiar FurnitureBlueprint. Worker 2 debe basar
el acabado primario en `theme_id` y respetar las reservas de ventanas existentes.
En el primer tramo v3 se conserva una ventana por habitación exterior; las
zonas se distribuyen sin tapar esa ventana. No ampliar su contrato ahora.

Mantener `stair_light_anchors` y `circulation_routes` de v2. Presupuesto máximo:
22 rooms +6 distribuidores +4 tramos =32 luces. Las zonas internas no suman
focos; revisar alcance/ángulo de la luz de cada ambiente compuesto con Worker 2.

## 7. Secuencia de implementación y propiedad

1. Después de liberar 0.9.1, Director autoriza baseline/versión. Capturar antes
   de cambiar las semillas/cámaras de comparación. Modelador 1 adapta estructura
   y asigna usos v3 en procedural_house; MapCatalog rechaza IDs incompatibles.
2. Prueba nueva de estructura/zonificación y corpus de proporciones antes de
   cambiar colocación. Cualquier rechazo se registra por semilla; no usar
   `new_house` como máscara. Director/QA migran fixtures antiguos por versión.
3. Implementar grupos de muebles y contrato de interacción detallado en
   `modeler092-furnishing-spec.md`. Conservar FurnitureBlueprint en sólo lectura.
4. Añadir validación de esenciales, accesos y ocupación; QA independiente
   según `modeler092-acceptance.md`.
5. Integrar con Worker 2: superficies, ventanas y luces; capturas/recorridos
   y rendimiento con turno exclusivo. Corregir sin retroceder anchos útiles.
6. Director integra, exporta, prueba y publica 0.9.2. Modelador 1 no toca
   red, protocolo, scripts de build ni archivos visuales ajenos.

Si una configuración geométrica no admite su equipamiento esencial, ajustar
los cortes dentro del mismo template y repetir pruebas. No rebajar esenciales,
reducir puertas/cápsula ni pedir otra decisión estética al usuario.
