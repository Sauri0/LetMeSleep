# Reserva online de 30 segundos sin IA

Regla explícita de Branko: «Ningún bot online: reservar al jugador 30 s sin que
una IA lo controle». La reserva usa identidad autenticada y reloj monotónico
del host; nunca nombre visible ni un identificador enviado libremente por el cliente.

Contrato de implementación:

- En Playing, perder un invitado conserva miembro, rol y actor durante 30 s.
  Notificaciones repetidas no extienden ese plazo. La salida del host cierra
  la sala, conforme a la política existente sin migración.
- El actor deja de recibir órdenes, cancela acciones mantenidas y permanece
  sujeto a física, daño, recuperación y reglas del modo. No se crea un bot ni
  se le concede invulnerabilidad. La reconexión no restaura vidas ni progreso.
- Volver con la misma identidad antes del vencimiento valida protocolo y
  contenido, prepara el cliente y envía estado actual. Se preservan las
  defensas contra paquetes atrasados; las secuencias nuevas pertenecen a una
  revisión de vista renovada.
- Al vencer exactamente los 30 s, se retira el actor mediante la política de
  abandono existente. Si queda un equipo sin participantes, termina la ronda
  por abandono del contrario. Esta concreción por modo es decisión CEO; no es
  una selección adicional del cuestionario.
- Sala de espera: no reservar un actor que todavía no participa en la ronda.
  Al volver de resultados, se limpian reservas pendientes antes de marcar Listo.

Implementación en revisión: RoomWireCodec3 incluye Connected y protocolo
`lms-unity-020-3` exige soporte de reconexión. El host vuelve a enviar Begin con
IDs de actor originales y un challenge nuevo de64bits para cada reanudación;
los reintentos conservan el mismo challenge. ResumeAck debe devolverlo junto
a la identidad de ronda. ACK inicial o de una reanudación anterior no abre
Input/Action. La espera de ese invitado no pausa a los demás.

Evidencia CPU:34 tests de sala y24 de codec/preparación pasan, incluido el
challenge nuevo que rechaza ACK inicial y anterior sin bloquear al otro peer.
Unity6000.3.24f1: `N:/LetMeSleep/Validation/V020/reconnect-voice-native-01.xml`,
118 PASS, 0 FAIL, 0 omitidos; sala, codecs, autoridad, preparación y núcleo/DSP
de voz. EncodeBegin reserva ocho bytes para que el envoltorio de reanudación
respete el máximo del transporte; TryBegin aplica el mismo límite.
GameplayActor:3d563eb,46CPU PASS, cancelación/control/daño/vidas/progreso.
El revisor modos_red identificó la carrera del ACK antiguo que motivó el
challenge. Los casos de dos clientes reales/EOS siguen pendientes; este
documento no declara validación de reconexión WAN. El hook Bootstrap y la
exclusión de voz de miembros reservados forman parte de la integración voz
asignada a modos_red, probada en el mismo árbol de trabajo.
