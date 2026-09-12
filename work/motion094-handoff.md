# Movimiento y entrenamiento alfa

Base: 2094817; contrato M1 a637f07 integrado localmente como 7a84987.

Revalidación: assets M2 d7d3d1e y ajuste QA 3d34628 integrados como 195a9bc/b044203. House 140/0 y practice 120/0, stderr vacío, con las colisiones definitivas del mobiliario y las cercas. El runner incluye el manifest JSON real para consultar geometría sin cargar mallas ni renderer.

- Arena resuelve el mapa predeterminado con MapCatalog.default_map_id. Los límites de movimiento y cámara usan el solar; el techo del edificio sigue siendo un obstáculo físico.
- La navegación reconoce las hojas abiertas de mapas authored y mantiene las rutas interiores, exteriores y entre plantas. No modifica la geometría authored.
- Los barridos verticales humanos y los apoyos de mosquitos aturdidos reconocen las puertas del mapa activo; sus definiciones inmutables usan caché limitada, no sus ángulos.
- Cliente recibe role/mode/map_id, valida el mapa antes de abandonar el menú y prepara entrenamiento sin generador. Reiniciar conserva mapa, rol y modo. BotBrain usa el mapa canónico por omisión y se aparta de una hoja bloqueada según su orientación real.
- ui_navigation_test recibe y comprueba el tercer argumento del selector. Su ejecución visual corresponde a integración con la UI nueva.

## Evidencia CPU

`python work/motion094-headless.py bounds`: 14 comprobaciones, 0 fallos.

`python work/motion094-headless.py house`: 140 comprobaciones, 0 fallos. Incluye subida/bajada física de ambas escaleras, 16 puertas abiertas/cerradas, paso inferior del mosquito, apoyo sobre hojas, acceso exterior bidireccional y rutas a tareas/patio.

`python work/motion094-headless.py practice`: 120 comprobaciones, 0 fallos. Incluye seis combinaciones rol/modo, arranque/movimiento de bots, tres reinicios por combinación, preferencia obsoleta y retirada correcta ante 16 hojas bloqueadas.

Todos usan Godot 4.5.2 headless, audio Dummy, sin autoload EOS ni renderer; stderr vacío. El runner copia únicamente dependencias de scripts a un proyecto temporal. Los recibos motion094-*.run.json registran dependencias y resultados.

Las pruebas no sustituyen la validación visual, UI completa, partidas largas ni WAN. QA cubre las partidas largas. Director conserva simulation/network/versionado; esos archivos no se modificaron aquí. No se abrió ninguna ventana del juego, no se exportó ni publicó.

## Corrección adicional de lanzamientos

Director asignó projectile_collision.gd por el retorno exclusivo para `house` que descartaba las puertas authored. `map_hit` usa ahora las definiciones cacheadas del mapa seleccionado y conserva el barrido continuo en coordenadas de la hoja y la prioridad del impacto más cercano. El límite superior usa los mismos bounds del solar que Arena.

`python work/motion094-headless.py throw`: 388 comprobaciones, 0 fallos, stderr vacío. Prueba diario y zapatilla contra las 16 puertas, desde ambos lados, abiertas/cerradas/a 45 grados, normales de impacto, alcance corto libre, techo prioritario y vuelo libre sobre patio. Antes de corregir runtime fallaron 128 comprobaciones de puertas; las trayectorias libres ya pasaban. No cambian daño, velocidad, alcance ni reglas de combate.
