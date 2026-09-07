# Dejame dormir 0.1.0 — informe de pruebas

**7 de septiembre de 2026. Prototipo nativo Windows, con servidor en la PC anfitriona.**

El juego, los tres modos y el servidor están implementados y se exportaron. La validación se realizó en una sola computadora mediante procesos independientes conectados por ENet en loopback, pruebas deterministas y renderización gráfica real. **No se probó todavía una partida entre distintas computadoras o conexiones a Internet.**

## Evidencia ejecutada

| Verificación | Resultado |
|---|---|
| Godot4.5.2 importación y exportación release Windows64bits | Correctas. Ejecutable con recursos embebidos; se ejecutó sin `--path` ni acceso al proyecto fuente. |
| Reglas autoritativas headless | **3212 comprobaciones,0fallos.** |
| Geometría visual y privacidad de marca | **13 comprobaciones,0fallos.** |
| Audio original procedural | **28 comprobaciones,0fallos.** Corrida nativa con WASAPI activo. |
| Sangre1humano/2mosquitos, EXE exportado | Cuota compartida alcanzada, mismo ganador en3clientes y vuelta a sala. |
| Sangre5humanos/10mosquitos, EXE exportado |15clientes entraron, comenzó ronda, picaduras y cuota completada. Sin errores registrados en la repetición corregida. Es prueba técnica local, no capacidad pública garantizada. |
| Supervivencia2humanos/4mosquitos, EXE exportado | Ronda completa; mosquitos ganaron al sobrevivir al reloj, sin picaduras obligatorias. |
| Tareas2humanos/4mosquitos, EXE exportado | Ronda completa y tareas realizadas por humanos; meta colectiva y resultado compartido correctos. |
| Desconexión de participante durante ronda | Regreso a sala sin ganador; sin personajes activos abandonados. |
| Cliente de versión/protocolo incompatibles | Rechazado antes de entrar, mensaje legible y ningún snapshot de partida recibido. |
| Gráficos reales1280×720 | Cliente nativo OpenGL en ambas perspectivas y menú; imágenes de viewport inspeccionadas. |
| Interfaz |7pantallas renderizadas e inspeccionadas, controles y configuración por propietario, vidas solo enTareas. |

Los informes JSON de las últimas ejecuciones se incluyen en `validaciones/`. Cada cliente registra recepción de estados y de datos privados, movimientos/picaduras observados, errores y resultado. No se presentan los bots como rivales jugables ni como personas reales. Las capturas son escenas de prueba deterministas renderizadas por el ejecutable, no imágenes generadas ni una sesión real por Internet.

## Reglas cubiertas específicamente

- Rechazo de equipos inválidos; cinco humanos permitidos; tope técnico16participantes/12mosquitos; capacidad y alternativas de zonas verificadas incluso en5v10.
- Asignaciones distintas y dirigidas solo al mosquito correspondiente. Snapshot público usa una lista explícita de campos permitidos y no contiene reservas, índices de zonas o calendarios.
- Picadura persistente al soltar botón; anclaje al cuerpo; cambio inmediato al desprenderse sin reiniciar calendario. Coincidencia desprendimiento/rotación sin doble transición. Secuencias de acciones y movimiento repetidas o antiguas rechazadas.
- Extracción progresiva y sangre conservada tras desprendimiento/muerte. Final único por cuota/reloj/eliminación.
- Tareas interrumpidas conservan progreso; fallo solo reduce plazo futuro personal. Frecuencia y ronda se mantienen. Inicio/revancha reinician estado.
- Tres vidas **totales** por mosquito enTareas. Muerte descuenta una de ese jugador; reaparición solo si quedan vidas. Todos temporalmente muertos con vidas pendientes no finaliza; todos sin vidas finaliza a favor humano.
- Manos iniciales, bandas de autoayuda, zonas traseras que requieren compañero; golpes con orientación, distancia, obstáculos y recuperación. Recogida atómica, intercambio y devolución de objetos únicos.
- Movimiento acotado, colisión con muebles/cuerpos y posado cercano a una superficie.
- Marca privada oculta detrás de pared, mesa, torso o lado opuesto; vista desde insecto y cámara. Cabeza local humana oculta/cuerpo visible; muerto oculto y reaparición restaurada.
- Audio espacial atenuado por obstáculos; muerte silencia; piscina fija de sonidos, ausencia de eventos repetidos y limpieza al cerrar.

## Problemas encontrados y corregidos

Se corrigieron nombres de métodos que chocaban con Godot, estados UDP demasiado grandes mediante compresión DEFLATE, un modo de descompresión incompatible, notificaciones ENet entre clientes innecesarias y duplicados de preparación de sala que saturaban los bots al entrar15a la vez. Se desactivó el relay entre clientes: toda intención pasa por el servidor.

La revisión independiente detectó un mosquito adherido que podía quedar detrás de una pared al arrimarse el humano. Se amplió su margen de colisión y se verificaron22zonas×16giros×8bordes/esquinas, además de rutas e interacción en los tres puestos. También se corrigió el ingreso durante resultados y una desconexión posterior a la victoria que se anunciaba incorrectamente sin ganador.

La cámara de mosquito tiene un origen acotado para evitar comenzar dentro del techo o paredes; el brazo de cámara usa volumen de colisión. No se transparenta la geometría.

## Límites conocidos

1. Falta prueba humana porInternet con variasPCs. No se configuró router/firewall, no hay relay automático y no se contrató alojamiento. UDP27840 y código de sala necesitan una dirección alcanzable; CGNAT puede requerir otra solución.
2. No hay balance validado ni requisitos mínimos medidos. El render gráfico observado usó unaRTX3060Ti; no representa el resto dePCs. No se midió una tasa de cuadros sostenida con todas las combinaciones de jugadores.
3. Los clientes interpolan posiciones recibidas; no hay compensación histórica de golpes ni predicción de movimiento avanzada. La respuesta con latencia/pérdida real debe evaluarse.
4. Arte y animaciones son originales y funcionales de prototipo. Falta evaluación de comprensión, accesibilidad práctica y comodidad de audio/cámaras con jugadores. AudioWASAPI activo no equivale a evaluación subjetiva con auriculares.
5. Una sola sala por servidor, sin cuentas ni persistencia de ronda. Reiniciar servidor pierde la sala. Los eliminados ven espera neutra, sin cámara libre. El EXE no lleva firma comercial.

## Reproducir

En el ZIP de fuentes: `work/setup-tools.ps1` descarga editor/plantillas oficiales y verificaSHA512; `work/build.ps1` importa, ejecuta reglas/geometría/audio y exporta. `work/test-network.ps1` reproduce partidas locales y variantes de desconexión, incompatibilidad y vuelta a sala. `-Executable` dirige la prueba al EXEexportado. Cada ejecución conserva sus logs bajo`work/network-*`.

La siguiente prueba útil es descargar el mismo ZIP en tresPCs, conectar1v2 entre al menos dosredes, alternar roles, completar/repetir cada modo y registrar latencia, comodidad de defensa y claridad de las tareas. La entrega actual permite preparar esa sesión, sin afirmar que ya ocurrió.

## Identificación del paquete final

SHA256 del EXE: `FBECC23497C38BD646B4727A5F79E07241382ADEB43A0613AD18D0ED3DF199A2`. Las tres capturas finales de inicio/humano/mosquito provienen de este ejecutable; las tres ejecuciones cerraron con código0 y stderr vacío. Se corrigieron la superposición del nombre de zona y una advertencia de limpieza al cerrar la captura.
