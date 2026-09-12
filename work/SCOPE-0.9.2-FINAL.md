# Cierre 0.9.2 con el saldo restante

Branko actualizó el objetivo: llegar a lanzar 0.9.2. Informó 15% semanal
restante y autorizó incluir las mejoras nuevas que entren sin comprometer
esa publicación. 0.9.1 ya está publicada y verificada.

Prioridad de cierre: casa v3 recorrible y amueblada, manos/agarres correctos,
rasgos de personalización, regresiones de movimiento y paquete online
integrado. Los fallos físicos encontrados en seed1 deben corregirse antes
de publicar; no basta que valide el generador.

Se asignó a Worker1 la cámara del mosquito: zoom con rueda desde primera a
tercera persona, cámara libre estando quieto y orientación al moverse. No
modifica poses de manos que Modelador2 está cerrando. Debe mantener vuelo
hacia la mirada, posado y defensa, sin consumir rueda para cámara humana.

Se informó a Branko que inventario de tres slots (1/2/3 + rueda), estamina
para humano/mosquito y nuevos ajustes de balance quedan para la siguiente
entrega: requieren autoridad, replicación, interfaz y pruebas específicas.

Revisión F: SurfaceLocomotion.configure usa ArenaData.obstacles y _face
proyecta sobre caras de AABB. HouseLibrary declara visuales GLB separados de
la autoridad de colisión. ASSETS en FurnitureBlueprint contiene dimensiones
envolventes; sofá/sillón no tienen aquí primitivas que sigan asiento, respaldo
y brazos. Esa simplificación explica caminar en aire dentro de la envolvente.
La solución pendiente requiere superficies físicas por piezas, alineadas a
los modelos, tanto para autoridad como presentación; ajustar sólo F o un
offset no corrige el problema. No declarar este defecto solucionado en092.

Picadura: FOCUS_SECONDS actual1.20. El usuario quiere revisar la sensación
y dará mejor opinión al probar con amigos. Conservar el balance actual
hasta disponer de esa prueba; registrar duración, abandonos de carga y
respuesta del humano al ajustar después.
