# Continuación 09 — fuente en desarrollo, sin exportación nueva

Este documento reemplaza como estado actual el checkpoint WIP anterior; sus
fallos y capturas históricos se conservan. No es una certificación de entrega.

El ejecutable en `outputs/Let-me-sleep-0.7.0-Windows` sigue siendo el anterior
al trabajo de personalización, con SHA-256
`675342F018EFCF9F7A2171A6F9062CA7FE17BB475B537D3C760E8D2AC1CE7D9A`.
La fuente actual usa protocolo 9 / invitaciones DD5. No se publicó ni exportó
esta continuación y no se debe atribuir su comportamiento al EXE anterior.

## Cambios implementados

- HUD compacto y ayuda contextual. En sangre y antes de dormir, los mosquitos
  caen aturdidos durante 35 segundos; la ayuda continua acelera la recuperación
  cuatro veces sin acumular ayudantes. Supervivencia conserva la eliminación.
- Caminar por piso, paredes y techo; cámara con colisiones y confirmación de
  orientación entre cliente y autoridad al cambiar de superficie.
- Emotes humanos, favoritos y menú con el mosquito B importado. Ajustes faciales
  y de materiales conservan geometría de ojos y pupilas.
- Casas generadas con 16–24 habitaciones, dos o tres pisos, semilla y huella
  compartidas antes de comenzar, puertas físicas, navegación y cinco tipos de
  herramientas en cada piso. Muebles con escala uniforme y orientación real;
  reservas de paso, ventanas y apoyos para tareas.
- Voz espacial con Opus, PTT configurable, selector de entrada, silencio propio
  y por interlocutor, permisos de autoridad, cola acotada y boca desde PCM.
  Mosquito: tono 1.55 sin acelerar; hacia humano 3 m / ganancia .25, entre
  mosquitos 8 m / .85; humano 10 m / 1.0, con distancia y puertas.
- El fin normal de PTT conserva el fragmento parcial final. La eliminación y
  el corte inmediato descartan pendientes. Tras vencer la cola por pérdida de
  red, un permiso nuevo permite recuperar audio fresco sin revivir el anterior.

## Evidencia cerrada antes del pase integrado actual

- Autoridad de orientación: 213 comprobaciones; cliente nativo 149 y ENet 53.
  Cámara de superficies: 1680 comprobaciones, con radio y plano cercano originales.
- Emotes: 1643 comprobaciones de autoridad, 31152 de pose, 64 de integración
  local. Mapa/emotes en ENet: 55. No representan latencia o rendimiento WAN.
- Mobiliario importado: 2490 comprobaciones de dimensiones, giros y apoyos.
  Modelo/boca: 552. Selector de micrófono: 27; restaura preferencias y no abre
  ni enumera dispositivos durante la automatización.
- PCM y fin parcial: 4758 comprobaciones sin fallos. Opus v2 aislado: 312,
  puente codec/cola: 608, jitter aislado: 1878. DLL integrada SHA-256
  `E5C2F5DEA7515D63E2218704BF457DBF806EE530C51C5C3C2BEFCA4D603A49B1`.
- Playout aislado conserva duración, tono, marcador final y drenaje medido.
  El Director conserva WAV, logs y límites de AudioDriverDummy; no demuestra
  micrófono real, sesión completa ni rendimiento de partida.
- VoiceSession integrado: 77/77. Dos frases de 50 paquetes, 50 decodificaciones
  y cero PLC; humano 440/880 Hz, mosquito 682/1364 Hz, con marcador final íntegro.
  El enlace se estabilizó durante 5.189 s / .276 s antes de esas frases. Los
  intentos de arranque frío con pérdidas siguen disponibles; no prueban WAN.
- Origen de escucha compartido: 9/9 con física real en headless/Dummy, más 36/36
  regresiones de Foley. Capturas nativas de casas: semilla 1, 62/62; semilla 2,
  49/49 sobre la colocación final de mobiliario y tareas.
- Corrección H0 revisada en 24 vistas sin las raíces blancas detectadas antes.
  Es una muestra limitada; no sustituye a la galería completa de personalización.

## Hallazgos activos y nuevas pruebas

1. **ENet:** en Godot 4.5.2, `create_server` pasa el máximo de canales al
   parámetro de ancho de banda entrante: nuestros cuatro canales resultaban en
   seis bytes por segundo. La traza observó `throttle_limit` pasar de 32 a 1
   con RTT de 1 ms. `Network.host` restablece `bandwidth_limit(0,0)` antes de
   aceptar conexiones. El transporte pasó 212/212 y conserva el límite de
   aplicación 12/100 frames por ráfaga. La sesión completa encontró además
   adaptación RTT normal a 26/32 tras cargar modelos; el control de frases pasó
   con una precondición de enlace estable medida, sin desactivar throttle.
2. **Patas cerrado:** 3 × 18776 comprobaciones de esquinas, 2547 de apéndices y
   552 de boca por voz, todas correctas. Cero penetraciones; pico de tarso
   36.240 mm por cuadro frente a 55.82 mm de la comparación anterior. El polo
   transportado del IK evita invertir la flexión; apoyo y velocidad se conservan.
3. **Generación:** el corpus inicial de 100 semillas crudas encontró diez
   distribuciones inválidas. El giro del mobiliario y la asignación primero de
   tareas con requisitos corrigieron esas 100. Una tanda posterior encontró un
   acceso que cortaba su propia mesa; se añadió comprobación del segmento y se
   unificó el cálculo del punto de acceso. Pasaron 1000 semillas y sus 1000
   repeticiones en procesos independientes, con huellas idénticas. Son 1000
   distribuciones físicas distintas al excluir semilla, colores y mobiliario:
   521 de dos pisos y 479 de tres; todas conservan al menos 16 habitaciones.
   El audit acústico externo pasó 40/40 semillas sin descartes: 6415 nodos,
   6440 enlaces, 714 rutas y 922 segmentos independientes sin problemas.
4. **Tareas:** la nueva distribución cambió la estación que sirve como testigo
   de ampliación de plazo. El fixture busca ahora una ruta real entre todas las
   estaciones, manteniendo los límites de tiempo y la ejecución física: 86/86
   comprobaciones correctas sobre la distribución nueva.

## Continuación de modelos, rendimiento y voz (8 de septiembre)

- La prenda humana tenía cruces visibles con cabeza, boca, barba y bigote.
  Se abrió el cuello real y se corrigieron forma y pesos de hombros en la fuente
  editable y el GLB. Los cuatro testigos corregidos suman 192 pares sin contacto
  inesperado; fit3 sólo ajusta UV respecto de fit2 (1782 invariancias correctas).
  Las 16 vistas actuales conservan la boca libre y corrigen los zigzags de las
  rayas. La exportación completa de 285 poses se está renovando con estos hashes;
  el resultado anterior de contactos no certifica esta fuente.
- El coste aislado de DoorState bajó de 2,454 a 0,406 ms en el replay ABBA.
  Pasaron 2578 equivalencias y 342 regresiones de puertas. No es un aumento de
  FPS demostrado. La última medición nativa de casa generada con 16 actores a
  1080p dio 28,308 / 60,754 / 91,729 ms (p50/p90/p99), todavía fuera del objetivo.
  DoorCatalog está en revisión separada. Un ensayo de MultiMesh empeoró el coste
  del pasillo y fue rechazado; no se integró al juego.
- VoiceSession publica ahora permiso y motivo contextual. La UI no promete
  hablar en práctica, sala o eliminación; conserva prueba local independiente.
  Pasaron 38 controles nativos de UI (incluido ancho a 720p/1080p), 27 del selector
  y 77 de Session con captura sintética/Dummy. No se abrió hardware real.
- El empaquetado comprueba versión/protocolo, fuente limpia, DLL Opus, avisos y
  hashes antes de crear el ZIP. Pasaron 13 casos de rechazo/aceptación en una
  carpeta de prueba inerte; no se exportó ni empaquetó un candidato nuevo.

## Antes de una entrega nueva

- Cerrar transporte y VoiceSession completos, regresiones de patas y corpus,
  capturas nativas actualizadas, rutas de bots y roster completo.
- Galería facial completa y 285 poses sobre una fuente congelada; resultados
  parciales y exportaciones históricas no cuentan como aprobación nueva.
- Exportar a un directorio/versionado nuevo, conservar avisos de Opus y verificar
  la DLL exportada. Repetir pruebas del EXE, matriz de red, vídeo y rendimiento.
  El resultado antiguo de 16 jugadores a 1080p no alcanzó 60 FPS; todavía no hay
  medición nueva que justifique afirmar ese objetivo.
- El acceso entre casas por descarga y código sigue pendiente del servicio
  WAN/EOS. ENet local y las pruebas de audio no lo sustituyen.

Nunca abrir micrófono real en automatizaciones. Nunca publicar, sobrescribir la
entrega anterior o declarar listo el online basándose sólo en estos módulos.
