# Primera etapa de UI · integración en curso

Implementación desde WIP73aa6ff, no candidato de distribución. Alcance: `ui.gd`, `menu_mascot.gd`, `emote_selector.gd`, `voice_indicator.gd` y `tests/menu09_ui_checks.gd`. Sin cambios propios en AvatarPreview, mallas, perfiles, cliente, audio o red.

## API para Client

Señales: `emote_requested(id)`, `emote_favorite_requested(slot,id)`, `emote_preview_requested(id)`, `emote_preview_closed`, `voice_mute_requested(muted)`, `voice_test_requested(held)`, `voice_peer_mute_requested(peer_id,muted)`.

`set_emote_state(data)` recibe `available`, `preview_available`, `favorites` (normalizados con EmoteCatalog), `reason` y `preview_reason`. `available` expresa elegibilidad del personaje; no debe depender de `UI.is_menu_open()` porque el selector mismo es un modal. El cliente valida el gesto con el servidor; UI no mueve huesos ni inventa animación. `get_customization_preview()` entrega la instancia existente para conectar `play_emote/stop_emote` cuando Visual los integre.

`set_voice_state(data)` recibe `status` (`idle/capturing/muted/unavailable`), `available`, `can_test`, `muted`, `level`, `visible`, `error` y `peers:[{id,name,muted}]`. No recibe PCM. Apertura/cierre de pantallas nunca emite inicio de captura. `voice_test_requested(true)` sólo resulta de mantener el botón explícito en Ajustes; soltarlo, perder foco, cerrar Ajustes, iniciar rebinding o cambiar pantalla emite `false`. Root debe conectar parada al ciclo de vida de audio y aplicar permisos y errores reales. Las pruebas de UI inyectan estados; no se probó un micrófono con ellas.

Acciones consultadas: `emote_menu` y `push_to_talk`, usando el binding actual. Si aún no existen, se muestra «Sin asignar». Root crea/persiste las acciones y favoritos. Mantener el atajo abre selector; flechas/ratón eligen, soltar confirma. Soltar sin elegir o Esc cancela. Con ratón desde Pausa se confirma con botón/Enter. `is_menu_open()` incluye el selector; `screen_changed("emotes")` permite cancelar entradas del juego y recuperar contexto al cerrar. Los snapshots repetidos no lo cierran.

El inicio compone el mosquito B real con su apariencia guardada. Su viewport aislado se suspende al ocultarse y no toma foco/ratón. El segundo pase sólo vuelve transparente ese viewport y quita el panel rectangular; no modifica AvatarPreview global ni mallas.

## Evidencia

Pase final nativo Compatibility: `menu09_ui_checks` 45/45; regresión `ui_navigation_test` 156/156, cero stderr y preferencias restauradas byte por byte. Archivos `work/menu09-native.log`, `work/menu09-navigation.log`, `outputs/0.9-menu/checks.json`, `home.png`, `emote-selector.png`. La captura final confirma transparencia real, mosquito completo y continuidad de la diagonal crema/teal. También se comprueba conservar el foco al reordenar favoritos. Runtime de esta primera etapa congelado para integración de Root.

Limitaciones explícitas: la autoridad, persistencia de favoritos, gestos corporales y audio espacial están en integración de Root/Visual. Los botones «Probar» permanecen deshabilitados con motivo visible hasta `preview_available=true`. Los gestos se eligen por nombre; su previsualización exacta está en el personaje 3D al activar Probar, no se presentan miniaturas animadas falsas. No se ha validado voz WAN ni micrófono real en este fixture.
