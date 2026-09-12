# Let me sleep 0.9.3 — alcance de esta corrección

Corrección del flujo online sobre 0.9.2: menú de crear/unirse, copiar/pegar,
reintentos, mensajes de error y eliminación de la modalidad LAN de la interfaz,
la guía y los lanzadores. Se mantiene entrenamiento con bots.

Verificación dirigida SIN abrir ventanas del juego ni usar el renderizador,
a pedido del usuario. Se ejecutan contratos de interfaz con controles aislados,
invitaciones, conexión, sesión, red, tamaño de paquetes y el ciclo real de
creación/cierre de sala en Epic. El EXE final también ejecuta el contrato UI
y el ciclo real de anfitrión EOS con --headless. VERIFICATION.json contiene los
resultados, argumentos, hashes y origen exacto del ejecutable.

No se repitió la suite gráfica completa de 0.9.2 ni se certificó visualmente
el diseño de este menú. La prueba de dos jugadores desde casas distintas y
el relay de tráfico real entre ellos todavía requiere verificación. Crear una
sala en Epic no demuestra por sí solo una conexión entre dos redes.

Mapas, personajes, combate, voz y rendimiento mantienen el alcance de 0.9.2.
No se agregaron inventario de tres slots, estamina ni colisiones detalladas
para posarse en todos los muebles.