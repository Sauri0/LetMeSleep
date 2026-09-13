# Poses físicas: contrato aislado, pendiente de integración

Director, 2026-09-13 UTC. `RagdollPoseCodec` y `RagdollPoseGate` son opt-in:
ningún paquete de la partida los utiliza todavía. No se modificó el protocolo
V2, la barrera Begin/ACK, los snapshots ni la autoridad de gameplay. El módulo
físico del mosquito continúa fallando su prueba de separación de articulaciones;
no activar replicación ni simular clientes por separado para suplirlo.

Formato little-endian con magic propio, versión1 y kind1. Header42 bytes:
magic4/version2/kind1/epoch8/round8/tick4/actor4/revision4/generation4/profile2/count1.
Cada cuerpo lleva posición mundial xyz y rotación xyzw float32 (28 bytes).
Humano17 =518 bytes; mosquito18 =546. Con framing actual10 caben en un paquete
de1170. No se agregan al snapshot de16 KiB ni se aumenta ese límite.

El decoder comprueba longitud exacta, perfil/cantidad, IDs, revisión/generación,
tick<=54000, posiciones finitas en mundo ±10000, extensión gruesa desde cuerpo0
de3 m humano/2 m mosquito y quaternions finitos unitarios con tolerancia .002.
El array queda copiado y de sólo lectura. Estos límites no certifican anatomía,
orden de huesos ni coherencia con el root autoritativo: se debe negociar y
validar perfil/rig/hash antes de permitir el protocolo.

El gate recibe lifecycle sólo después de aceptar un snapshot autenticado;
un paquete de pose no puede iniciarlo. Hay como máximo16 actores, generación
y revisión monotónicas, invalidación al recuperarse, reloj por actor y rechazo
de replay/reordenación, muestras anteriores a revisión, ronda ajena y remitente
que no sea el dueño autenticado. La identidad de dueño la debe resolver la
sesión desde el transporte, jamás desde un campo declarado en el paquete.
La ventana temporal provisional es pasado30 ticks/futuro6. La barrera de nueva
revisión usa el tick observado y es conservadora frente a paquetes adelantados.

Revisión independiente detectó y corrigió: revisión0 y reinicio del watermark
al cambiar revisión dentro de la misma caída. Nueve casos pasaron por invocación
directa de NUnit en Editor, y nueve por runner CPU propio después de agregar
regresión que aísla el watermark; no equivale a Unity Test Runner ni a WAN.
Evidencia: `N:/LetMeSleep/Validation/alfa3-corrections-20260913/`:
`ragdoll-protocol-checks.txt` y `protocol-cpu/result.txt`.

Antes de conectar: lifecycle persistente en snapshot, versión de sala y
Begin/ACK compatibles, orden y hash real del rig, root físico del host,
recuperación/clearance, buffer de interpolación con límite, cuotas/frecuencia,
limpieza en ronda/despawn/desconexión y medición de pérdida/reordenación.
Sólo host simula; cliente representa pose y no produce fuerzas/colisiones.
No se debe afirmar sincronización online a partir de estos tests aislados.
