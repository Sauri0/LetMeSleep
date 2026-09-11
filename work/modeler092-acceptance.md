# Aceptación de casa v3 / 0.9.2

Diseño de pruebas para ejecución después del cierre de 0.9.1 y autorización
de motor. Nuevas pruebas de Modelador 1: `modeler092_*`; QA independiente:
`review092_*` si Director asigna ese prefijo. No editar fixtures ajenos.

## Capas y criterios

| Prueba propuesta | Gate y evidencia |
|---|---|
| modeler092_structure_test | v3 canónico; geometría nueva no se publica como v2; 2/3 plantas; 8 PB +8–10 arriba o8+7+7; dimensiones y anchos físicos/declarados |
| modeler092_zoning_test | baño por planta; PB sin dormitorios principales; >=2 dormitorios por planta alta; servicios útiles >=3.40x4.00 y <=20m²; usos compuestos completos |
| modeler092_adjacency_test | distancias entre portales sobre grafo real según tabla; cocina/comedor misma habitación; servicio en misma banda; baños sin cambiar de planta |
| modeler092_furnishing_test | esenciales por asset; muebles dentro del volumen útil; no barrido de puertas ni ventana invadida; acceso >=1.30 y entrada1.56; <=8 por cuarto/144 por mapa |
| modeler092_surface_test | GLB real, pickup y prop de tarea en soporte medido; approach/facing correctos a0/180 y después90/270; no tarea flotante ni útil dentro de lavabo |
| modeler092_routes_test | 5 spawns humanos/12 mosquitos;8 tareas diferentes;5 herramientas/planta; grafo conectado; recorrido físico cuando ray hit conservador humano; mosquito conserva paso lineal |
| modeler092_visual_probe | misma semilla/encuadre antes/después; servicios, cocina-comedor, dormitorios, dos usos de salón; pasos y descansos con acabados reales |

La prueba de zonificación calcula interior útil desde las paredes, no acepta
`room.area_m2` sin contrastarla con geometría. Una cocina-comedor primaria
`theme_id=kitchen` no aprueba con cocina sola: debe contener dining_set y su
zona accesible. Un dormitorio familiar exige dos camas reales separadas.

## Casos negativos obligatorios

- Baño cambiado a un salón de50m² con mismos sanitarios: se rechaza por
  proporción/tipo de template, aunque tenga tres muebles.
- Cocina sin heladera, dormitorio sin cama o cuarto compuesto sin su segundo
  grupo: rechazar aunque el total de muebles sea alto.
- Mueble que invade arco a45° pero deja libre hoja a0/90°: rechazar.
- Intrusión fuera de la línea central pero dentro del ancho prometido: rechazar.
- Franja de losa ausente en un paso lateral: rechazar soporte completo.
- Approximación de mesa rotada que pasa por su propio cuerpo: rechazar.
- Pickup/prop que comparte área visual con otro objeto o no toca superficie:
  rechazar usando geometría real, no sólo bounding boxes optimistas.
- Despensa cercana en línea recta pero separada por un muro sin portal:
  la prueba de vecindad usa el recorrido y falla si excede el máximo.
- Más de32 luces o segundo foco por zona funcional: rechazar presupuesto.

## Corpus y reproducibilidad

Mantener corpus existente: 500 semillas consecutivas +500 distribuidas por el
dominio. Generar directamente cada semilla v3, sin `new_house` ni sustitución.
Dos pasadas en procesos independientes para firma completa y firma estructural
(excluir colores/muebles al medir variedad geométrica). Registrar todos los
rechazos con cuarto/uso/bounds/esencial/ruta; no parar en el primer fallo.

Antes del corpus completo: las once semillas de v2 (1,2,7,31,97,257,997,2026,
65537,1234567,2147483646), extremos de cotas con jitter mínimo/máximo y ambos
casos de2/3plantas. El mismo número no conserva planta exacta entre versiones:
guardar dimensiones resultantes y elegir pares de comparación equivalentes.

El test de fixtures geométricos debe poder forzar los parámetros del template
en un generador de prueba, sin exponer controles de depuración al jugador.
Si el corpus encuentra un fallo, convertir esa semilla en regresión específica
y corregir la causa antes de declarar v3 completo.

## QA visual con criterios concretos

Recorrer ambos sentidos de los dos tramos, ambos laterales y los descansos;
girar la cámara en los cruces, seguir marcos/techos en movimiento y acercarse
a las molduras. Confirmar al menos1.50m visible/usable después de acabados.

En cada categoría: captura general desde la puerta +posición del jugador en
el punto de interacción. En baño se distinguen sanitarios y acceso frontal;
cocina/comedor se leen como dos grupos conectados; camas/guardado no se
interpenetran; el asiento interior del estar no bloquea el eje de entrada.
Los cuartos compuestos deben tener ambos usos ocupados, sin media sala vacía.

## Rendimiento y entrega

Misma resolución1080p, configuración y secuencia de recorrido; calentamiento
y captura de tiempos p50/p90/p99, %frames>16.67ms, nodos/mallas/draw calls,
luces y número de actores. Comparar en la misma PC y declarar su GPU real;
no certificar1660Ti con otra tarjeta. FPS siguen ilimitados por defecto.

Director decide integración/exportación/publicación. La casa no está terminada
por pasar sólo unidades o conteo de objetos: requiere corpus, recorridos,
integración visual y paquete final. WAN real entre redes se informa aparte.
