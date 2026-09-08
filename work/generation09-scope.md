# Distribución modular aleatoria — alcance de integración

Pedido explícito: cada partida cambia distribución real de habitaciones, pisos y zonas. No basta cambiar colores ni elegir un catálogo fijo. Lobby conserva su espacio separado.

## Estado comprobado
MapCatalog.HOUSE es un diccionario fijo de dos pisos. Arena._map y MapNavigation._data lo leen directamente; DoorCatalog usa diez definiciones fijas y HouseDetails usa alturas/ventanas de la casa autoral. La generación debe cambiar datos compartidos y consumidores, no sólo dibujo. Simulation toma map_id al crear la ronda; cachés de geometría usan ese ID.

## Contrato propuesto
El anfitrión elige semilla entera y revisión de generador al comenzar cada ronda. Identidad pública de mapa incorpora revisión+semilla y un fingerprint determinista de geometría; clientes regeneran y comprueban el fingerprint antes de entrar. Sin RNG local para colisión, puertas, tareas o spawns. ID distinto por geometría evita compartir cachés entre salas/semillas. El mapa publicado es inmutable durante una ronda.

Generador modular parametrizado: cantidad de pisos, divisiones y dimensiones de cuartos, conexiones/corredores y ubicación de funciones domésticas. Mantener escala humana, escalones0,2m/huella0,5m, pasillos y puertas transitables por radio humano0,60m/altura1,95m; conservar tamaño mosquito0,04m. El resultado entrega obstáculos, pisos, escaleras/huecos, habitaciones/zonas, puertas, muebles/soportes, estaciones, pickups, spawns, grafo de rutas y datos de decoración/luz derivados de la misma distribución.

Cada seed se valida antes de publicar: grafo conectado, acceso a todas las tareas/spawns, rutas con soporte y altura libre, hojas de puertas que no bloquean el único recorrido, muebles sin solapamiento de circulación. Reintento determinista con subsemilla cuando falla una propuesta, límite acotado y error explícito; no ocultar un fallback a la casa fija como supuesto mapa aleatorio. Conservar casas generadas fallidas como fixtures de regresión.

## Ownership
Root: generador nuevo, MapCatalog, MapNavigation, DoorCatalog y render/decoración procedimental; network/client/practice/config para semilla+fingerprint y handshake. Sim: locomoción superficies y Arena; Root coordinará sólo delegación de Arena._map/limpieza de cachés a la API MapCatalog.get_map, sin editar simultáneamente su solver. Visual: modelos/animación de personajes, sin arquitectura. UI: menú y controles por contratos.

## Comprobaciones
Corpus fijo de semillas más muestreo reproducible; medir variación estructural real (plantas, particiones, zonas, rutas), no hashes de color. Igual seed produce mismos datos/fingerprint en procesos separados. Validar grafos, rutas de humanos/mosquitos y bots, puertas, acceso tareas/pickups, colisión y malla equivalentes, cámaras, voz por zonas, resets/revanchas y dos salas simultáneas. Vistas nativas de plantas/cuartos y recorridos reales; rendimiento16actores medido sobre candidato común.

Este archivo es contrato previo, no implementación ni validación completada. Se empieza después del checkpoint facial inmediato.
