# Protocolo EOS/WAN — Unity 0.9.4 alfa

## Propósito

Demostrar una sesión real de `Let me sleep 0.9.4-alfa` entre dos personas/equipos y dos redes físicas distintas. Dos procesos en un equipo, dos perfiles EOS locales, loopback, LAN compartida, fixture, transporte simulado o una partida ENet no acreditan WAN.

El dueño crea la sala dentro del juego y el invitado pega un código. El flujo no muestra IP, puerto, PUID ni herramientas externas al jugador. EOS debe informar la ruta establecida; el texto de la UI no basta.

La configuración EOS distribuida con el juego se rige por `EOS-CLIENT-POLICY-REVIEW-2026-09-12.md`: sólo credenciales del cliente `Peer2Peer`/`User required` con mínimo privilegio. Credenciales administrativas o `TrustedServer`, tokens personales y claves privadas siguen prohibidos.

## Preparación, todavía sin acreditar WAN

1. Director entrega un único ZIP candidato, SHA-256, commit, versión, protocolo y manifiesto. Ambos participantes comparan esos valores antes de abrirlo.
2. Cada participante usa una instalación limpia o validada y una identidad EOS real persistente. Los hashes sanitizados de identidad deben ser distintos.
3. Los equipos se conectan a redes físicas diferentes. Cada persona deja una conformidad breve de máquina y red distintas; no registra el nombre de red, IP, puerto ni ubicación precisa.
4. Relojes se sincronizan aproximadamente en UTC. Se acuerda un `run_id` aleatorio que no sea código de sala.
5. Se habilita log QA que redacte secretos y registre callbacks de Connect/Lobby/P2P, estado de juego y tipo de ruta EOS.
6. Se prueba primero el mismo build como par EOS local. Ese paso detecta configuración básica, pero se etiqueta `local_eos_pair=true`, `wan=false`.

## Ejecución principal

### Crear y unir

1. Ambos arrancan desde proceso cerrado. Cada recibo registra `run_id`, UTC, build SHA-256, commit, versión/protocolo, plataforma y hash de identidad local.
2. A crea sala. El log registra éxito de Connect, creación/membership y hash del lobby/capability. La UI muestra un código opaco y copiable; el recibo no conserva el código reutilizable.
3. B pega el código con espacios exteriores. Debe resolver el mismo lobby, unirse y establecer P2P con A. Los recibos cruzan hashes de identidad local/remota, lobby y ventana UTC.
4. Ambos registran el callback EOS de conexión y `network_type`/ruta reportada. `Direct` o `Relay` son resultados posibles; ninguno se inventa cuando el callback falta.
5. B intenta cambiar mapa o configuración de dueño. La UI puede mantener el control deshabilitado, pero se envía además un intento controlado por el harness y el host lo rechaza sin cambiar estado.

### Dos rondas

6. Ambos marcan listo. A inicia ronda Sangre. Se registran roster sorteado, inicio y epoch/tick autoritativo.
7. Durante la ronda, ambos mueven su rol real, cruzan una puerta e interior/patio. Debe observarse al menos una picadura por contacto sin marcador, progreso de Sangre y una defensa manual o desprendimiento. Un ataque detrás de hoja cerrada se rechaza.
8. La ronda termina por una condición autoritativa conocida. Ambos reciben el mismo ganador/motivo y regresan al lobby; sangre, contacto, herramienta y estado de puerta no contaminan la siguiente ronda.
9. Se repite una segunda ronda. El sorteo se ejecuta otra vez; no se exige alternancia de roles porque un sorteo válido puede repetirlos. Se repiten movimiento de ambos peers y una interacción Sangre observable.
10. B sale voluntariamente. A conserva lobby y ve la baja. B vuelve mediante un código vigente o A genera uno nuevo según el contrato implementado; se registra cuál ocurrió, sin inferir reusabilidad.

### Cierre

11. Con ambos en lobby, A cierra sala. B recibe cierre explícito, vuelve al menú y no se promueve a dueño. No queda código copiable ni callback que reabra la sesión.
12. Se repite el cierre con A saliendo durante una ronda corta o fixture dedicado. B recibe pérdida/cierre, sin resultado inventado ni migración de host.
13. Ambos cierran el proceso. Se registra salida 0 y ausencia de crash/excepción de teardown. Un segundo arranque conserva la identidad local pero no la sala anterior.

## Negativos obligatorios

- Código vacío, alterado, sobredimensionado, vencido y de protocolo diferente: error comprensible, recursos liberados y reintento posible.
- Unión a sala propia: rechazo específico; no debe presentarse como incompatibilidad de versión.
- Cancelar mientras create/search/join está pendiente: cualquier callback tardío deja/destruye su recurso y no cambia el nuevo intento.
- Mensaje del invitado que declara ser host o publica snapshot/resultado: rechazo por identidad autenticada, sin cambio de autoridad.
- Interrupción de red: timeout acotado, estado claro, sin lobby fantasma ni reconexión de una ronda ya abortada.
- Cierre del dueño: todos salen; nunca se migra host en alfa.

## Recibo por extremo

Cada archivo JSON debe contener como mínimo:

```json
{
  "schema": 1,
  "run_id": "valor compartido sin secretos",
  "side": "host|guest",
  "started_utc": "ISO-8601",
  "finished_utc": "ISO-8601",
  "build": {
    "version": "0.9.4-alfa",
    "protocol": "valor del candidato",
    "commit": "40 hex",
    "exe_sha256": "64 hex",
    "zip_sha256": "64 hex"
  },
  "identity": {
    "local_hash": "hash con salt del run",
    "remote_hash": "hash con salt del run",
    "distinct": true
  },
  "lobby_hash": "hash con salt del run",
  "network_type": "valor literal informado por EOS",
  "rounds": [
    {"index": 1, "role": "human|mosquito", "started": true, "ended": true, "returned_to_lobby": true}
  ],
  "closures": {"guest_leave_seen": true, "owner_close_seen": true, "host_migrated": false},
  "process_exit_code": 0,
  "stderr_bytes": 0,
  "participant_attestation": "otro equipo y otra red física",
  "redactions_verified": true
}
```

Los eventos detallados pueden ir en un sidecar con secuencia y UTC. Dirección conserva originales privados si fueran necesarios; el repositorio recibe sólo recibos sanitizados.

## Redacción y cruce

Nunca guardar en evidencia: código/capability reutilizable, PUID o DeviceID completo, IP, puerto, nombre de red, token, valor literal de `ClientId`/`ClientSecret`, ruta de perfil o variables EOS. Para correlacionar se usa un salt exclusivo del `run_id`; ambos extremos deben producir hashes cruzados compatibles sin publicar el dato base.

La corrida puede marcar `wan=true` sólo si:

- build/ZIP, versión, protocolo, lobby y ventana temporal concuerdan;
- identidades locales son distintas y cada remoto coincide con el otro extremo;
- existen dos equipos y dos redes físicas según ambas conformidades;
- EOS confirma la conexión/ruta;
- hay al menos dos rondas concordantes, retornos y cierre;
- no hay secretos en recibos;
- ambos procesos terminan sin error.

Si falta cualquiera, se conserva el resultado útil (`host_lifecycle`, `local_eos_pair` o `same_lan_pair`) y U094-13 permanece BLOCK.

## Última evidencia parcial

El estado del probe Windows del 12 de septiembre de 2026 se registra en `EOS-WINDOWS-PROBE-2026-09-12.md`. Acredita sólo parte del ciclo de host. El guest reutilizó la misma identidad del sistema aun con cache separado, fue rechazado como `SameDeviceIdentity` y no estableció transporte. U094-12 y U094-13 permanecen BLOCK.
