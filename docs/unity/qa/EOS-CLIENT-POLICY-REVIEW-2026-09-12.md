# Revisión de política del cliente EOS — 2026-09-12

## Dictamen

**PASS para incluir la configuración del cliente de juego en el build**, condicionado a que la política observada siga asignada y no amplíe permisos. Esto no acredita dos identidades, transporte P2P ni WAN.

## Criterio de distribución

EOS necesita que el cliente del juego entregue `ProductId`, `SandboxId`, `DeploymentId`, `ClientId` y `ClientSecret` al SDK. La [guía oficial de Lyra con EOS](https://dev.epicgames.com/documentation/unreal-engine/using-lyra-with-epic-online-services-in-unreal-engine) muestra esos valores dentro de la configuración del juego distribuido y pide una política de tipo `Peer2Peer` para el cliente P2P.

Por eso `online.local.json` puede viajar dentro del build sólo cuando contiene esas credenciales del cliente de juego y ese cliente está limitado por una política de mínimo privilegio. El nombre `ClientSecret` de la API no lo convierte en una credencial administrativa o personal, y no autoriza a publicar otros secretos.

Queda prohibido incluir en Git, logs, recibos o ZIP:

- credenciales de `TrustedServer`, administración o automatización del portal;
- tokens personales, cookies, contraseñas, PUID/DeviceID, códigos de sala o capabilities reutilizables;
- claves privadas o de cifrado para Player Data Storage/Title Storage;
- cualquier permiso adicional que no corresponda al cliente P2P de este build.

Un cambio de cliente o política, habilitar acciones de Connect, permitir `Achievements: Unlock for any user` o pasar a `TrustedServer` vuelve este gate a **BLOCK** hasta una nueva revisión.

## Inspección sanitizada del portal

El Director abrió el [portal de desarrolladores de Epic](https://dev.epicgames.com/portal/) en una sesión autenticada y revisó `Settings > Clients` sin editar ni guardar. No copió ni mostró IDs o valores de credenciales.

Estado observado:

- cliente: `Let me sleep Windows`;
- política asignada: `Peer2Peer P2P`;
- tipo: `Peer2Peer`;
- `User required`: habilitado y bloqueado por el preset;
- Connect: todas las acciones deshabilitadas;
- Achievements, desbloqueo para cualquier usuario: deshabilitado;
- permisos de juego acotados a Lobbies (5 acciones), Sessions (8 acciones) y Voice (1 acción);
- ninguna política `TrustedServer` asignada.

La inspección acredita el tipo y alcance visible de la política en esa fecha. No acredita que el archivo empaquetado corresponda al commit candidato; esa trazabilidad se comprueba por separado en `BUILD.json`, el recibo de build y el SHA-256 del ZIP.
