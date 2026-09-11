# Siguiente tramo: usos domésticos y mobiliario

Preparación autorizada mientras se espera turno de motor. No implementado aún.
Primero cerrar y entregar el tramo de circulación.

## Distribución por plantas

- PB: baño, entrada, cocina, despensa, lavadero, comedor y estar garantizados;
  visitas/estudio según espacio disponible.
- Primera planta alta: baño y al menos dos dormitorios con cama real;
  biblioteca, estudio o sala de música en el resto.
- Segunda planta alta, cuando exista: baño, dormitorio(s) y usos de trabajo/
  ocio (costura, juegos, música, lectura), según áreas. No repetir una cocina
  o un recibidor por el mero índice del sorteo.

Para tres plantas, reservar dos divisiones de cuadrante en PB y una en cada
planta superior: 8+7+7=22 habitaciones. Eso ofrece salas pequeñas para baños
y dormitorios sin agotar el presupuesto de iluminación en una sola planta.
Para dos plantas, conservar 8–10 habitaciones por planta. Mantener variación
de cuadrantes divididos, proporción de división, cotas de paredes y puertas.

Asignación determinista por restricciones, con desempates derivados de la
semilla: baños en candidatos de menor área; cocina y despensa en una pareja
del mismo cuadrante dividido; lavadero en el mismo distribuidor si cabe;
comedor cerca de cocina; entrada/estar en las habitaciones centrales. En pisos
altos, reservar primero baño y dormitorios; asignar los cuartos grandes a
actividades compartidas. Guardar `zone` y `area_m2` por habitación, además de
`theme_id`, y verificar requisitos por planta. No agregar puertas a ciegas.

Si un pequeño servicio quedara en un cuarto desproporcionado, corregir su
asignación o corte antes de compensar el error con decoración. Si el corpus
muestra salones persistentemente gigantes, evaluar reducción de envolvente
en un cambio medido separado; no cambiar cotas sin repetir circulación.

## Equipamiento y colocación

Reutilizar FurnitureBlueprint en sólo lectura. Los IDs GLB y escalas uniformes
se conservan. Generar especificaciones suplementarias duplicando las existentes
con `pickup_surface=false`, sin inventar modelos fuera del catálogo.

| Uso | Equipamiento imprescindible | Complementos por tamaño |
|---|---|---|
| Cocina | pileta, cocina/horno, heladera, superficie de apoyo | guardado |
| Baño | vanitory, inodoro, ducha, superficie de apoyo | sin relleno extra |
| Dormitorio | cama, guardado, superficie de apoyo | mesa de luz, asiento |
| Visitas grande | camas y guardado proporcionados | mesas de luz, asiento |
| Comedor | comedor, vajillero, superficie | asiento/guardado auxiliar |
| Estar/lectura | asiento, mesa, biblioteca | grupo de asientos enfrentados |
| Lavadero | lavarropas, canasto, guardado, superficie | armario |
| Música/costura/juegos | mueble específico y superficie | asientos, guardado |

Colocación: imprescindibles primero, reserva de puerta y acceso siempre;
evaluar acceso a la superficie desde ambos ejes antes de rechazarla. Distribuir
objetos secundarios con alternativas interiores: grupo de asientos, mesa útil,
mesa de luz junto a cama. Evitar que ordenar por distancia a la puerta apile
todos los objetos en el rincón más remoto.

Objetivo inicial: 4 objetos en salas pequeñas y hasta 6–8 en las mayores,
ocupación de huella orientativa 12–25% según uso. Esa métrica no basta sola:
se comprobará relación entre muebles, orientación, acceso y lectura visual.
Pasillo puerta-centro de 1.56 m y accesos de al menos 1.30 m; ventanas,
soportes, tareas, spawns y barridos de puerta conservan su reserva.

## Aceptación

Prueba nueva por usos/áreas, con fixtures negativos de cocina incompleta,
dormitorio sin cama y agrupaciones que invaden un acceso. Medir objetos y
ocupación por habitación en el corpus, sin relajar errores ni esconder
rechazos mediante reintentos de semilla. QA visual comparable de cocinas,
baños, dormitorios y salones de distintas áreas en dos y tres plantas.
El rendimiento, recorridos reales y la integración de acabados siguen siendo
requisitos de la entrega, además del conteo de objetos.
