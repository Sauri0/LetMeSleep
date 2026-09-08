# Voz contextual en la interfaz

La UI ya diferencia disponibilidad del códec y permiso contextual para transmitir. La sala de espera muestra «Disponible durante la ronda» en ajustes; la práctica, «Voz sólo online»; la eliminación, «Voz · eliminado» con la razón completa en el tooltip. El aviso de mantener el control para hablar aparece sólo durante una ronda online elegible. Un estado viejo de ronda no vuelve a habilitar ese aviso al regresar a la sala.

El indicador conserva su espacio compacto. Los errores del dispositivo siguen visibles en ajustes. La comprobación local del micrófono se presenta por separado y continúa dependiendo de `can_test` y de una acción explícita; seleccionar dispositivos y silenciar mantienen sus preferencias existentes. La UI no enumera dispositivos ni abre el micrófono.

Prueba nativa final: **38/38 PASS**. Regresión del selector de entrada: **27/27 PASS**. Los procesos terminaron con código 0, stderr vacío y bytes originales de preferencias restaurados. A 1280×720 y 1920×1080 se comprobó que el indicador de eliminado mide como máximo 202 píxeles, permanece dentro del viewport y contiene el texto compacto completo. El tooltip conserva «No disponible mientras estás eliminado». La primera ejecución de 34 controles se conserva como antecedente, antes de añadir esas cuatro comprobaciones geométricas.

`voice_context09_ui_checks.gd` llama al publicador real `VoiceSession._publish_ui()` con contexto, captura y transporte inertes. El objeto de captura de prueba sólo expone campos de presentación y carece de una API para abrir hardware. Se cubren sala, práctica, actor eliminado, ronda elegible, mute, prueba local, estados de captura, errores, ausencia de códec, carga antigua y transición tardía a sala. No se prueba transmisión de audio en este fixture.

La PNG muestra el control defensivo con un estado de ronda antiguo que todavía solicitaba un indicador visible. La UI lo convierte en «Voz durante la ronda». No contiene un mundo 3D: es una prueba de presentación de la sala. En la publicación normal de sesión, `visible=false` mientras no hay ronda y el indicador permanece oculto.

Fuentes de esta corrección: `game/scripts/ui.gd`, `game/scripts/voice_indicator.gd` y el fixture nuevo `game/tests/voice_context09_ui_checks.gd`. El cambio del contrato de `VoiceSession` fue realizado por Root y la prueba consume sus campos `codec_available`, `can_transmit` y `reason`.

Comando nativo usado:

```powershell
& work/run-native07.ps1 -Name voice-context09-ui-layout -Source -GameArguments @('--rendering-method','gl_compatibility','--script','res://tests/voice_context09_ui_checks.gd','--max-fps','60')
```

Godot 4.5.2 Compatibility, fuente local; sin micrófono, enumeración real, voz por red ni cambios persistentes de configuración. No es una prueba del EXE final.
