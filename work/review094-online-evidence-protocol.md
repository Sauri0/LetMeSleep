# Protocolo de evidencia EOS — 0.9.4 alfa

Este protocolo distingue una prueba local con dos identidades reales de una prueba WAN entre dos participantes. Dos peers ENet de loopback, identidades fixture, un host solo o dos procesos que comparten `user://eosg-cache` no acreditan A08.

## Evidencia local con dos identidades reales

EOS Connect usa Device ID y el addon guarda su caché en `user://eosg-cache`. Dos procesos lanzados desde el mismo perfil y proyecto comparten esa ruta y, por lo tanto, no constituyen dos identidades independientes.

El escenario local primario usa dos cuentas reales de Windows en el mismo equipo. La instancia A se ejecuta bajo el perfil QA A y la instancia B bajo el perfil QA B, con carga de perfil habilitada. El `project.godot` actual no redefine el directorio de usuario, de modo que cada cuenta resuelve `user://eosg-cache` dentro de su propio perfil. Una VM o Windows Sandbox con almacenamiento persistente separado puede ocupar el lugar de B, pero conserva la misma exigencia de dos cachés y dos Device IDs persistentes. Cambiar sólo la carpeta del EXE no aísla `user://`.

No se borrará el Device ID entre rondas para fabricar identidades. Antes de crear el lobby, cada participante obtendrá un Product User ID distinto. El recibo sólo guarda un hash truncado y salado por corrida, suficiente para demostrar desigualdad sin publicar el PUID.

Secuencia mínima:

1. Registrar checksum del mismo EXE/ZIP, versión/protocolo, hora UTC, identificador de escenario y hash de la configuración no secreta.
2. Arrancar A y B bajo los dos perfiles y registrar el hash de cada ruta `ProjectSettings.globalize_path("user://eosg-cache")`; los hashes deben ser distintos. No guardar las rutas completas en el reporte público.
3. Completar login EOS en ambas instancias y registrar `identity_a_hash != identity_b_hash` y `eos_sdk=true`.
4. A crea lobby; B lo encuentra y se une. Ambos registran el mismo hash de lobby/capability y membership de dos identidades.
5. Registrar callback P2P de ambos extremos, tipo de red informado por EOS y peer remoto autenticado. No inferir relay/WAN por el texto de la UI.
6. Completar al menos dos rondas, alternando acciones de ambos roles, con retorno al lobby entre rondas.
7. Probar salida del invitado y cierre explícito del dueño; ambos deben observar el estado esperado sin migración de host.
8. Repetir una vez desde arranque limpio conservando las identidades persistentes.

El resultado se clasifica `local_eos_pair=true`, `wan=false`. Si los hashes de identidad coinciden, falta un login, sólo hay un participante o el transporte real no está confirmado, la corrida es inválida.

## Evidencia WAN con un amigo

La aceptación WAN requiere dos personas/equipos en redes físicas distintas. No se sustituye por dos ventanas del mismo PC, loopback, LAN compartida o una simulación de pérdida.

Antes de la sesión, ambos participantes acuerdan un ID de corrida y verifican el mismo checksum publicado. Cada extremo registra, con reloj UTC razonablemente sincronizado:

- versión/protocolo y checksum;
- hash sanitizado de su Product User ID y del remoto observado;
- hash de lobby/capability, sin publicar la invitación reutilizable;
- eventos create/search/join/membership y callback P2P con `network_type` informado por EOS;
- comienzo/fin de dos o más rondas, roles, retorno al lobby, salida y cierre;
- latencia, pérdida/desconexiones observadas y resultado final.

Cada participante aporta una captura o log propio y una breve conformidad de que estaba en otra máquina y otra red. Dirección conserva los originales; el reporte público usa hashes y elimina PUID, lobby ID, capability, IP, rutas de perfil, credenciales y tokens.

## Regla de dictamen

| Evidencia observada | Clasificación permitida |
|---|---|
| Host crea/cierra sin invitado | ciclo de vida de host solamente |
| Dos peers ENet o identidades fixture | integración local simulada |
| Dos identidades EOS reales en un mismo equipo con cachés aisladas | par EOS local, `wan=false` |
| Dos equipos en la misma LAN | par EOS/LAN; WAN no acreditada |
| Dos participantes en redes físicas distintas, callbacks y recibos concordantes | candidato a `wan=true` |

Una afirmación `wan=true` exige además que ambas trazas compartan checksum, lobby y ventana temporal, que los hashes de identidad sean distintos y cruzados, y que las rondas/closures coincidan. Cualquier ausencia queda registrada como brecha; no se completa por inferencia.
