# EOS probe de dos rondas — contrato y evidencia

## Alcance

El probe de desarrollo debe comprobar con dos procesos y dos identidades distintas el ciclo de una misma sala durante dos rondas consecutivas:

1. handshake fiable de transporte en canal 2;
2. ambos miembros listos;
3. ronda 1: Playing, Results y regreso a Waiting observados por ambos;
4. ambos vuelven a marcarse listos;
5. ronda 2: Playing, Results y regreso a Waiting observados por ambos;
6. el host recibe la confirmación remota de regreso de la ronda 2 y solicita el cierre;
7. el guest observa el cierre y ambos procesos terminan con éxito.

El probe transita `OnlineRoomCoordinator` y el host llama `FinishRound` de forma manual. No ejecuta dos partidas de `GameplayAuthority`/`OnlineGameplaySession` ni certifica movimiento, herramientas, voz, objetivos o las rondas completas del ejecutable normal. El cambio tampoco prueba EOS ni WAN por sí solo. Los tests CPU sólo prueban la máquina de estados, la asociación de ACK con la ronda y el rechazo de replay. Una ejecución real exige dos redes y dos identidades EOS válidas, recibos de ambos extremos y revisión externa; aun así, estos dos ciclos de sala no sustituyen el gate de dos partidas reales acordado.

## Protocolo del probe

Los mensajes fiables de progreso llevan número de ronda canónico:

- `ProbePlaying:1` / `ProbePlaying:2`
- `ProbeResults:1` / `ProbeResults:2`
- `ProbeReturned:1` / `ProbeReturned:2`

El host acepta un progreso únicamente si:

- llega por canal 2;
- proviene del único miembro remoto conectado de una sala de exactamente dos miembros;
- el número está entre 1 y 2 y el texto es canónico;
- coincide con la ronda actual;
- el host ya observó localmente el mismo checkpoint;
- ese checkpoint de esa ronda no había sido registrado.

Un duplicado puede retransmitirse para tolerar pérdida, pero no incrementa contadores ni satisface otra ronda. Un mensaje de ronda 1 recibido durante ronda 2 se rechaza. El host no inicia la primera ronda hasta completar `UnityHello` / `UnityAck` / `UnityConfirmed`, no inicia la segunda hasta confirmar el regreso remoto de la primera y no cierra hasta confirmar el regreso remoto de la segunda.

El timeout total permanece acotado a 120 segundos. No se escriben PUID, owner ID, código de sala, ruta de cache, ruta de configuración ni IP en el recibo o el log del probe.

## Recibo propuesto

Cada extremo escribe un JSON `lms-eos-unity-probe-3` independiente. Campos permitidos:

```json
{
  "schema": "lms-eos-unity-probe-3",
  "role": "host|guest",
  "result": "Success|codigo_acotado",
  "networkType": "estado_no_identificante",
  "closeReason": "codigo_no_identificante",
  "unityVersion": "6000.3.24f1",
  "received": 0,
  "requiredRounds": 2,
  "completedRounds": 2,
  "remotelyConfirmedRounds": 2,
  "assignedRole": "Human|Mosquito",
  "roomHandshake": true,
  "ownerCloseRequested": true,
  "ownerCloseObserved": false,
  "wanVerified": false,
  "rounds": [
    {
      "round": 1,
      "started": true,
      "resultObserved": true,
      "returnedToLobby": true,
      "remotePlayingAcknowledged": true,
      "remoteResultsAcknowledged": true,
      "remoteReturnAcknowledged": true
    },
    {
      "round": 2,
      "started": true,
      "resultObserved": true,
      "returnedToLobby": true,
      "remotePlayingAcknowledged": true,
      "remoteResultsAcknowledged": true,
      "remoteReturnAcknowledged": true
    }
  ]
}
```

En el guest, los tres campos `remote*Acknowledged` son `false` porque el dueño es quien registra esos ACK; eso no es un fallo. Para aceptar una corrida real:

- ambos recibos deben tener `result=Success`, `requiredRounds=2`, `completedRounds=2`, `roomHandshake=true` y dos filas completas de observación local;
- el host debe tener `remotelyConfirmedRounds=2`, los seis ACK de ronda en `true` y `ownerCloseRequested=true`;
- el guest debe tener `ownerCloseObserved=true`;
- los procesos deben provenir de dos identidades y dos redes documentadas fuera del recibo público, sin publicar identificadores;
- `wanVerified` permanece `false`: ni el player ni un verificador pueden cambiarlo sólo por reunir los dos recibos de este probe. La verificación WAN requiere el gate externo de partidas reales.

Los archivos de invitación son insumos privados y efímeros; no se adjuntan al recibo publicado. Cualquier log del SDK debe sanitizarse antes de compartirlo.

## Evidencia CPU

`EosProbeLifecycleTests` cubre:

- dos rondas secuenciales completas del host;
- cierre únicamente tras `ProbeReturned:2` aceptado;
- replay de ronda 1 durante ronda 2 rechazado;
- ACK duplicado de la misma ronda rechazado;
- ACK futuro y checkpoint fuera de orden rechazados;
- guest incompleto si el owner cierra después de una sola ronda;
- guest completo sólo tras dos regresos y cierre del owner;
- la primera ronda no inicia sin handshake confirmado y las acciones no se repiten mientras cambia el estado remoto.
- callbacks rápidos no consumen Ready, Start o Return antes de que el player pueda ejecutar la acción;
- salto directo a ronda 2 y snapshots obsoletos de ronda 1 se rechazan;
- un regreso confirmado sólo para ronda 2 no permite cerrar si falta la secuencia de ronda 1.

El harness puro externo usado durante el freeze ejecutó 43 comprobaciones y no tocó `Assets`. Ese resultado sirve para preparar el delta; la aceptación requiere luego el fixture EditMode nativo sobre las fuentes importadas.

## Verificación nativa del CEO

Unity 6000.3.24f1: `eos-probe-two-rounds-native-01.xml`, 9/9 PASS, cero
omitidos, sobre las fuentes integradas. Evidencia en
`N:/LetMeSleep/Validation/V020/`. La revisión previa corrigió una acción
consumida por Advance antes del throttle y añadió la secuencia obligatoria
antes del cierre. Esta ejecución fue EditMode local, sin cargar identidades
EOS ni abrir una conexión de red; no acredita WAN ni partidas de gameplay.
