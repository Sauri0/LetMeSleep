# Revisión online estática acotada — alfa.1

Fuente revisada: ec95269 (runtime publicado f0e6b80). No cambia el dictamen externo.

- EosPeerTransport valida identidad desde callback EOS, membresía y socket antes de entregar payload; cuatro canales y tamaño acotados. Aceptación de conexión sólo para miembros actuales. No se inspeccionaron credenciales ni se alteró identidad.
- MessageFraming acota MTU, tamaño total y ensamblajes pendientes; límites previos a copiar fragmentos. No sustituye prueba de transporte real o pérdida de paquetes.
- OnlineRoomCoordinator acepta vistas sólo del dueño autenticado; autoridad de sala rechaza cambios de invitado. Join/ready comunican revisiones para invalidar estados obsoletos.
- OnlineGameplaySession comprueba mapa, protocolo, roster, herramientas y barrera de inicio; reintento Begin cada segundo y timeout explícito. GameplayAuthority decide input/acciones de cada actor. No se encontró en este barrido un defecto demostrable que justificara cambiar esos archivos.
- AlfaApplication descarta coordinador/transporte/lobby anteriores al reintentar; una salida no convierte al invitado en anfitrión. El recorrido de cancelaciones tardías queda pendiente de prueba con dos identidades.

Estos puntos son evidencia de código, no PASS de U094-10 a U094-13. Los siguientes testigos necesarios continúan siendo invitado real, rondas, salida del dueño/invitado, cancelación/reintento y dos redes físicas.
