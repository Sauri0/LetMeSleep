# Menú, personalización, emotes y voz — alcance propuesto

Estado: inspección de fuente, sin cambios de runtime ni ejecuciones de Godot. El checkpoint facial actual conserva UI/Preview y las piezas que Visual está cerrando. Este documento define el siguiente trabajo autorizado; no describe funciones ya implementadas.

## Hallazgos concretos

- `game/scripts/ui.gd:81–139`: el mosquito de inicio pertenece a `ComicBackdrop._draw()`. Son elipses y líneas 2D con coordenadas fijas: alas claras, abdomen coral, cabeza amarilla de un ojo y tres trazos de patas visibles. No usa el mosquito B aprobado, la personalización ni el rig. Las dos rayas centrales cruzan el abdomen y su silueta/color difieren del personaje del juego. Es una mascota anterior, no un fallo del GLB actual.
- `ui.gd:521–568`: logo grande a izquierda y todas las acciones en una columna derecha; la ventana/mascota está dibujada detrás en una posición fija. `EDICIÓN NOCTURNA / N.º 05` sigue literal aunque la versión se toma de ProjectSettings en otro rótulo.
- `avatar_preview.gd` ya ofrece un mundo aislado, instancia del ActorView real, giro/zoom, vistas y enfoque. El editor admite 12 categorías humanas/9 mosquito. El alias `face` conserva el encuadre de los fixtures; debe mantenerse. Una mascota de inicio no debe activar el editor ni reutilizar el lobby como fondo.
- `client.gd:270–367` bloquea locomoción y acciones al consultar `ui.is_menu_open()`. `_input()` observa la cancelación de un lanzamiento antes de que UI consuma Escape. Ese patrón sirve para liberar PTT aunque el evento sea consumido. No hay emote, PTT ni transporte de voz.
- `preferences.gd` sólo agrega defaults de acciones que no existen y guarda bindings. Las letras **B y V están libres** en el mapa actual. `ui.gd` genera las filas de reasignación desde `Prefs.ACTION_NAMES`.
- `project.godot:27` tiene `audio/driver/enable_input=false`. `AudioCatalog.BUSES` contiene Music, Effects, Ambience y UI; falta separación de recepción de voz y captura.
- `assets/art/characters/shared/facial_expression.gd:85–99` calcula `MouthOpen` también mediante una oscilación de respiración. Esa oscilación no prueba que alguien hable. La futura capa de habla debe depender de audio real; no de reloj, PTT pulsado o una bandera enviada por el jugador.

## Menú principal: cambio acotado

Conservar Bangers, Atkinson, papel crema, coral/menta, contornos y organización en dos zonas. Sustituir el mosquito dibujado por **el mosquito B real con la apariencia guardada**, encuadrado entero en una viñeta 3D aislada. No regenerar sus mallas ni alterar materiales de personaje para esta tarea.

Reducir el bloque del logo lo justo para alojar esa viñeta sin solapar ventana, pie o acciones a 1280×720. Suprimir la edición «05» fija y el dibujo viejo del mosquito; conservar un motivo nocturno sencillo. Mantener Práctica como entrada inmediata, Crear/Unirme juntas, Tu pinta visible, Ajustes/Salir secundarios. Los formularios de conexión existentes conservan sus mensajes, invitación y estados.

La viñeta no captura ratón ni añade un foco invisible. Puede usar la animación de vuelo/descanso existente y una oscilación mínima de presentación; no un giro permanente que esconda la cara. Renderiza sólo cuando inicio está realmente visible, se suspende detrás de ajustes y se libera al salir. Presupuesto inicial: un SubViewport pequeño, sin nuevas sombras ni luces del mapa; se medirá coste de menú, no se supondrá gratuito.

## Tu pinta y emotes humanos

Separar en el propio editor dos secciones visibles: **Apariencia** y **Emotes**. Apariencia conserva todos los IDs, las partes independientes y los colores actuales; no se agregan categorías vacías ni se reinterpretan opciones antiguas. Emotes presenta tarjetas con nombre, miniatura y previsualización del gesto corporal real. Catálogo propuesto: saludo con el brazo, festejo breve, encogerse de hombros y bostezo con gesto de manos/torso; IDs finales pertenecen al catálogo compartido, no a números inventados en la UI. Son movimientos corporales legibles con raíz inmóvil, no sólo cambios de cara. Mosquito conserva su editor de apariencia sin una sección humana vacía.

La ampliación facial ya implementada permanece y mejora su acceso/organización. Por la última delimitación de Root/Director, este paquete no añade nuevos índices de pelo, ropa ni accesorios: las piezas actuales siguen bajo el cierre de mallas. La mascota de menú reutiliza B tal como está aprobado.

Asignar hasta cuatro favoritos humanos. En lobby o ronda, mantener **B** abre un selector pequeño de esos favoritos; ratón/flechas eligen y soltar confirma sólo una selección válida. Escape/clic exterior cancelan. Pausa tendrá un botón «Emotes» accesible sin mantener tecla. El selector cuenta como menú abierto: no dispara, mueve la cámara ni deja un lanzamiento cargado. El cambio de snapshot no lo cierra. Una vez enviado el gesto, el selector se cierra y devuelve la captura sin salto.

En gameplay el servidor acepta emote sólo para humano vivo en estado permitido. Moverse, saltar, interactuar o atacar lo cancela de forma explícita; la cancelación llega también al rig. Si un gesto mueve brazos/torso, sus superficies de picadura deben seguir la misma pose compartida: no basta una animación sólo visual con blancos en la postura anterior. Sim/Pose y Visual deben cerrar ese contrato antes de activar gestos durante ronda. Vista previa del menú usa ese mismo gesto en modo aislado.

No introducir una fila permanente de cuatro controles sobre el juego: descubribilidad breve al entrar y acceso estable desde pausa/ayuda. Mantener reloj, tareas, aturdimiento y rescate en sus esquinas actuales.

## Voz: decisiones confirmadas y comportamiento de UI

Todos los cercanos pueden oírse, **entre ambos bandos**. Humano habla con su tono normal y proximidad. Mosquito suena más agudo sin acelerar el habla: mosquito→humano tiene menos volumen/alcance; mosquito→mosquito mayor inteligibilidad y algo más de alcance. Distancia, ambientes, paredes y estado de puertas afectan audibilidad. Los metros, atenuación y pitch finales son parámetros de audio/servidor que Root medirá, no opciones del jugador que permitan escuchar más lejos.

**PTT por defecto, V reasignable.** La primera pulsación deliberada en un contexto válido inicia la captura; no se inicia al abrir menú, entrar a sala, elegir dispositivo o volver a enfocar la ventana. Soltar V, silenciar micrófono, perder foco, desconectar o abandonar sala detiene captura y vacía datos pendientes. Si se pierde foco con V sostenida, volver a la ventana exige soltar y pulsar de nuevo. Ninguna activación por voz automática en este alcance.

No captar V al escribir nombre/invitación o al reasignar teclas. Una pulsación en el lobby sin un campo de texto enfocado sí puede hablar. En ronda, abrir pausa/ayuda/selector cancela la captura activa; una nueva pulsación puede hablar desde pausa si no hay edición de texto/rebinding (útil sin reanudar la ronda). Este estado requiere un predicado de voz separado de `is_menu_open()`: bloquear locomoción no significa abrir el micrófono ni impedir toda conversación.

Revisar conflictos de bindings en el mismo contexto: PTT no debe también golpear, lanzar, interactuar o abrir emotes. Si un perfil antiguo ya usa V/B para otra acción, conservar esa asignación y dejar la acción nueva pendiente de reasignar con aviso concreto. No secuestrar ni cambiar silenciosamente un control guardado. En la captura de binding, un conflicto devuelve error visible y conserva el binding anterior; los duplicados legítimos de roles mutuamente excluyentes siguen permitidos.

En ajustes: dispositivo de entrada, volumen de recepción, silencio propio y prueba **«Mantener para probar micrófono»**. La prueba es local, sólo dura mientras se presiona y no crea WAV ni transmite a la sala. No escuchar el propio retorno por defecto. Silenciar a alguien afecta sólo la recepción local y su indicador; no se persiste un peer ID reciclable como identidad de usuario.

Indicador compacto debajo del equipamiento, máximo aproximado 190×26 px: icono de micrófono y binding vigente en reposo; «Transmitiendo»/nivel real durante captura; «Mic silenciado» o error específico si corresponde. No tapar centro ni franja inferior. En pausa/lobby, control visible de mute y lista de participantes para silenciar individualmente. Mostrar nombres hablando sólo si su voz es efectivamente audible para este receptor; no crear marcadores de enemigos detrás de paredes o fuera de alcance.

La boca recibe una envolvente de voz **realmente capturada** para el propio jugador y **realmente decodificada/reproducida** para otros. PTT sostenido en silencio deja la boca en reposo. Cerrar/descartar audio vence rápidamente la envolvente; no continuar una falsa conversación por snapshots viejos. Visual eliminará la oscilación de apertura que parezca habla aleatoria y separará reacciones/gestos intencionales del canal de habla. UI no modifica morphs directamente.

## División exacta propuesta (para después del checkpoint)

| Dueño | Archivos | Responsabilidad |
|---|---|---|
| UI | `game/scripts/ui.gd` | Composición home, pestaña Emotes, ajustes/estados de voz, wiring de selectores y foco. Preservar conexión/HUD. |
| UI | nuevo `game/scripts/menu_mascot.gd` | Viñeta del mosquito B con apariencia guardada; ciclo de vida visible/suspendido, sin GLB propio. |
| UI | `game/scripts/avatar_preview.gd` | Modo de presentación de mascota y previsualización de gesto. Modo editor y alias `face` conservados. |
| UI | nuevo `game/scripts/emote_selector.gd` | Selector/favoritos, teclado/ratón, cancelación; emite ID permitido, sin autoridad. |
| UI | nuevo `game/scripts/voice_indicator.gd` | Estados PTT/mute/error/nivel; ninguna captura ni red. |
| Root | `game/scripts/client.gd`, `main.gd` | Contexto de entrada, liberación segura, conexión entre UI/audio/red/rig y ciclo de vida. |
| Root | `game/scripts/preferences.gd`, `audio_catalog.gd`, `project.godot` | Defaults B/V, preferencias de voz, buses Voice/VoiceCapture y habilitación de capacidad; migración conserva todo lo existente. UI no escribe estos archivos en paralelo. |
| Root | nuevos `game/scripts/voice_session.gd`, `voice_spatial.gd`, codec/transporte y `network.gd` | Captura explícita, codec/colas acotadas, envío/recepción, filtrado autorizado por distancia/puertas/zonas y reproducción espacial. Mantener compatibilidad/rechazo explícito del handshake. |
| Root/Sim | nuevo `game/scripts/emote_catalog.gd`, `simulation.gd` | Catálogo allowlist, estado/cancelación y réplica pública del gesto. Coordinar locomoción. |
| Visual, tras liberar el cierre facial | `human_pose.gd`, `actor_view.gd`, `assets/art/characters/shared/character_skin.gd`, `facial_expression.gd`, fuentes/mallas | Gesto corporal de raíz inmóvil, superficies sincronizadas y envolvente real de voz. Coordinar el traspaso de Pose con Sim; UI no rediseña GLB. |
| Root (un único escritor) | persistencia de favoritos/catálogo | Favoritos junto a preferencias, separados de apariencia de red. `cosmetics.gd` y sus índices actuales se conservan; no redefinir `eyes/brows/mouth`. |

## Interfaces propuestas

- UI → Client: `emote_requested(id:String)`, `voice_mute_requested(muted:bool)`, `voice_peer_mute_requested(peer_id:int,muted:bool)`, `voice_test_requested(held:bool)`.
- Client → UI: `set_voice_state(data:Dictionary)` con `status` (`idle/capturing/muted/unavailable`), `level:0..1`, `receiving:Array` ya filtrado y `error:String`; nunca muestras PCM. `set_emotes(catalog:Array,favorites:Array,available:bool)`.
- Selector: `opened`, `selected(id)`, `cancelled`; consulta de visibilidad integrada en `is_menu_open()` y en cancelación de entradas sensibles.
- Preview: `set_emote(id)` y `stop_emote()` consumen el catálogo/pose compartidos. `set_presentation("editor"|"home")` conserva el modo editor por defecto.
- Audio → render: nivel y marca temporal por actor audible; ausencia/silencio vence a cero. No se acepta una amplitud arbitraria de un RPC de cosméticos como prueba de voz.
- Root conserva el único escritor de Preferences. Nombres sugeridos: acciones `push_to_talk`/`emote_menu`; preferencias `voice_mic_muted`, `voice_input_device`, `voice_receive_volume` y cuatro `emote_favorites`. La captura activa nunca se guarda.

## Dependencias de motor verificadas, sin activarlas

Godot requiere `audio/driver/enable_input=true` para AudioStreamMicrophone; eso es capacidad, no el consentimiento para llamar a play. Su implementación 4.5.2 crea el playback inactivo, y `start/stop` llaman al inicio/parada de entrada. WASAPI inicializa salida al arrancar; abre/inicia entrada al llamar a `input_start`. Es una base concreta para PTT explícito, pero hay que verificar permisos, fallo de dispositivo y parada real en Windows antes de prometerlo. [Documentación 4.5](https://docs.godotengine.org/en/4.5/classes/class_audiostreammicrophone.html), [implementación del stream](https://github.com/godotengine/godot/blob/4.5.2-stable/servers/audio/audio_stream.cpp#L340), [WASAPI](https://github.com/godotengine/godot/blob/4.5.2-stable/drivers/wasapi/audio_driver_wasapi.cpp#L543).

Para el mosquito, `AudioEffectPitchShift` modifica tono independientemente del tempo. Tiene coste/latencia dependientes de FFT y oversampling; Root debe medir inteligibilidad y retraso. No reemplazarlo por `AudioStreamPlayer.pitch_scale`, que no satisface el requisito de conservar duración del habla. [AudioEffectPitchShift 4.5](https://docs.godotengine.org/en/4.5/classes/class_audioeffectpitchshift.html).

El transporte de voz no se mezcla en cada snapshot fiable de gameplay. Root determinará el codec y canal acotado, con destinatarios autenticados, pérdida/timestamps y cuotas. No se promete voz WAN sólo por construir su UI; deberá demostrarse en el transporte elegido.

## Verificación acotada y orden

1. Cerrar checkpoint facial; después asignar archivos y aprobar los IDs compartidos de emote. No volver a abrir mallas para arreglar la mascota 2D antigua.
2. `menu09_checks.gd`: inicio 1280×720/1920×1080, logo/acciones sin solapar, instancia del mosquito B (no dibujo viejo), Home→Tu pinta→volver refleja perfil; viewport suspendido oculto; Tab/flechas/Enter/Esc.
3. `emote09_ui_checks.gd`: favoritos persistidos sin tocar apariencia; selector por B y ratón/flechas; cancelar/reabrir; snapshots no cierran; movimiento, cámara y acciones bloqueados mientras está abierto. Sim/Visual verifican cancelación y blancos adheridos en poses.
4. `voice09_ui_input_checks.gd`: backend falso que cuenta start/stop; cero captura al inicio, al unirse, en foco de texto, rebinding o simple selección de dispositivo. PTT presionar/soltar, mute, desenfoque y desconexión; sin reanudar automáticamente con tecla retenida. Prueba micrófono local únicamente mientras se pulsa. Texto usa binding reasignado; perfiles que ya ocupan V/B mantienen sus acciones y no disparan al hablar.
5. Root audio/red: señal sintética conocida para pitch con duración intacta, boca en silencio=0 y envolvente alineada; rutas H→H/H→M/M→H/M→M, distancia, ambas plantas y puertas abiertas/cerradas; colas/permisos/error/mute. Prueba de micrófono real sólo con pulsación explícita, sin capturas ambientales automáticas.
6. Demo nativa preparada con menú, selección de gesto y dos interlocutores locales controlados; rotular audio sintético si se usa. No simular visualmente «hablando» sin audio. Perfiles respaldados/restaurados y mismos encuadres antes/después. Prueba WAN y rendimiento final quedan en Root.

Único archivo producido por esta inspección: `work/ui09-scope.md`. Ningún Godot/import/micrófono iniciado ni archivo de juego modificado.
