# Checklist corto de candidata — Unity 0.9.4 alfa

Marcar `PASS`, `BLOCK` o `N/A` con motivo y vínculo al recibo. `N/A` no se permite para los gates U094-01…17 de la matriz.

## Preparar

- [ ] Commit exacto limpio; Unity `6000.3.24f1`; `Packages/packages-lock.json` fijado; escenas, prefabs, scripts y `.meta` versionados.
- [ ] Sin códigos de sala, PUID, DeviceID, IP, tokens, caches Unity ni credenciales administrativas, personales o `TrustedServer` en Git, logs o ZIP. La configuración EOS incluida en el build pertenece sólo al cliente de juego `Peer2Peer`/`User required`, con política revisada y sin permisos Connect.
- [ ] Build informa `0.9.4-alfa`; un único protocolo online coincide con manifiesto/handshake; UI sólo ofrece Sangre y Casa con patio.
- [ ] Los tests Unity trazan su gate U094 y contienen al menos un negativo que falla al inyectar la regresión.

## Probar fuente y juego

- [ ] EditMode y PlayMode completos: cuerpo/cámara, manos, clipping, puertas, vuelo/superficies, contacto sin marcas, Sangre, casa/patio, lobby, código, práctica y guardado.
- [ ] Dos rondas Sangre host/invitado vuelven al lobby; roles se sortean otra vez; sangre, contacto, puertas y herramienta se reinician.
- [ ] Build nativo recorrido de ambos roles: arte/audio real, capturas de testigos y profiler 1080p con hardware/escenario declarados.
- [ ] Par EOS local con dos identidades reales: crear/unir/cancelar/reintentar/salir/cerrar, clasificado `wan=false`.
- [ ] WAN según protocolo: dos equipos, dos redes físicas, dos o más rondas y recibos cruzados. Si falta, el dictamen sigue BLOCK.

## Empaquetar y decidir

- [ ] ZIP, SHA-256, manifiesto y ejecutable pertenecen al mismo commit aprobado; instalación limpia inicia sin editor.
- [ ] Launcher 1.1.0 selecciona alfa en el orden acordado, instala en carpeta elegida, sobrevive a interrupción, actualiza y rechaza paquete/etapa incompatible.
- [ ] Descarga pública mediante launcher coincide byte a byte con el artefacto aprobado; segundo arranque no reinstala ni altera guardados.
- [ ] Informe final lista PASS/BLOCK U094-01…17 y defectos abiertos. Sólo el Director publica; QA no convierte una candidata con BLOCK en alfa aprobada.
