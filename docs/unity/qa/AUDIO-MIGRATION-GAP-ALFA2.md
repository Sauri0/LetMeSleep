# Comparación de audio Godot → Unity alfa.2

Fecha2026-09-12. Inspección de archivos y lógica en fuente0a82314, motivada por rechazo explícito de Branko al audio publicado. No es una escucha comparativa ni acredita calidad por cantidad de assets.

Godot conserva13OGG musicales y54efectos (67archivos), con manifest.json/CREDITS/CC0 y procedencia de instrumentos. Unity contiene15WAV:2pistas musicales,1ambiente y12señales. Parte del catálogo Godot pertenece a Tareas/herramientas aún no presentes en alfa; esa parte no se incorpora como funcionalidad nueva por esta auditoría.

| Área | Evidencia Godot | Estado Unity alfa.2 | Corrección del mismo alcance alfa |
|---|---|---|---|
| Música | music_director.gd sincroniza base/ritmo/melodía de menú y gameplay | AlfaAudioDirector alterna dos AudioBedPlayer | Recuperar stems anteriores y transiciones coherentes, sin reiniciar/reducir volumen en llamadas de contexto repetidas |
| Dinámica | Actividad local, últimos20s, ajuste a compás y duck ante acciones | No equivalente en el director actual | Adaptar a snapshots Unity y señales del jugador; no revelar actividad enemiga invisible |
| Personalización/ajustes | quiet.ogg dedicado | Mismo contexto musical general | Tema tranquilo y transiciones al menú, conservando ajustes de volumen |
| UI | select/confirm/error con límite de repetición | Catálogo expone UiReady | Recuperar señales diferenciadas, conectar eventos reales y evitar spam |
| Movimiento | pasos/aterrizajes madera/baldosa/tela, perch/detach, buzz por estado | GameplayAudioPresenter usa principalmente un WingLoop y señales de eventos limitadas | Restaurar lectura de caminar, aterrizar, posarse y desprenderse; no añadir sonidos sin acción |
| Defensa/objetos alfa | clap, equip/hit/swatter, pickup/drop | Swing/Impact generales | Distinguir manos/matamoscas e interacciones, sincronizar contactos visuales |
| Ambiente | night_air, room_fan/fridge según contexto | Un AMB_NightHouse | Recuperar variedad motivada por lugares/objetos existentes; no zumbidos de aparatos inexistentes |

Responsable runtime, catálogo, importación, mix y generación: W2. Director revisó inventario y entrega este mapa para evitar duplicar auditoría. UI conserva sus archivos; cualquier callback nuevo se acuerda. Las fuentes Godot permanecen intactas y los archivos reutilizados retienen créditos/hash. Revisión audible final pendiente; la revisión de fuente no permite afirmar que el resultado ya suena mejor.

El sonido que persistió tras cerrar la descarga era Play de Unity de W1PID33400, no un proceso del juego publicado. Fue silenciado y ese editor cerró. Pruebas físicas/visuales futuras con-noaudio; no se cambian preferencias reales ni audio de otras apps.
