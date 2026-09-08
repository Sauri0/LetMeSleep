# HUD: una aplicación por grupo de callbacks

Estado: implementado y verificado en fuente; pendiente inclusión en el candidato exportado. Director devuelve ownership de Client y motor a Worker tras las pruebas.

PracticeSession publica público y privado consecutivamente. Client antes aplicaba UI.show_game en ambos callbacks; la primera aplicación aún utilizaba el privado anterior. Ahora conserva inmediatamente los datos, cámara/superficies, audio y música, y agrupa únicamente la aplicación del HUD mediante la cola deferred del motor. El callback consume público y privado más recientes; no captura una copia obsoleta al programarse. No hay temporizador ni reducción de frecuencia de recepción.

El comienzo de ronda sigue mostrando la pantalla inmediatamente. Resultados y sala invalidan el refresco pendiente; un privado tardío no vuelve a abrir la partida. La comprobación de playing y fase protege también el abandono. Se mantiene el argumento de objetivo privado incorporado por HumanPresentation antes de World.sync_actors. No se modificaron Simulation, Network, PracticeSession, UI ni assets.

## Pruebas ejecutadas

- Parse de hud09_coalescing_checks.gd: exit0, stderr vacío.
- Fixture nuevo: 20 comprobaciones, ninguna falla, 2.270s, exit0 y stderr vacío. Usa callbacks reales de Client y cola deferred del motor con UI/World/audio inertes. Cubre pares, llegada de un solo canal, ráfagas, datos más recientes, audio inmediato, resultados, privado tardío, lobby, callback de ronda anterior durante revancha y pausa. No mide layout renderizado ni FPS.
- Regresión nativa real, humano/Sangre: 29/29, exit0 y stderr vacío. Menú, práctica, bots, mundo, cámara, mapa generado, revancha y salida.
- Regresión nativa real, mosquito/Tareas: 25/25, exit0 y stderr vacío. Misma integración real, con rol mosquito.
- Ambas regresiones restauran preferencias byte por byte, deshabilitan explícitamente micrófono y usan AudioDriverDummy. El resultado de fin de ronda se inyecta para probar navegación, no para certificar victoria natural.

Motor Godot4.5.2 SHA256 446E08F71624052572F96DE9031850BA96382CE6752ADDE38BB955B0A49BED01. Regresiones nativas Compatibility, fuente. No prueba de EXE, WAN ni hardware de audio.

## Fuentes y evidencia

Client SHA256: F08EEF2F47FD6D4E587603736409FD37A5E284F702D6E5E714341D96F2944CD1.

Fixture SHA256: E337D3BA161BCE86CA7A50545FE949CD1427FFEDE77F8DCBDCD04807AE08DC55.

Evidencia: work/director09-hud-parse.*, work/director09-hud-coalescing.*; work/release07-director09-hud-practice-human.* y work/release07-director09-hud-practice-mosquito.*, con informes director09-hud-practice-human.json y director09-hud-practice-mosquito.json.

La eliminación de una aplicación redundante está comprobada por el fixture. No se cuantificó ahorro CPU ni mejora FPS. El perfil integrado deberá medir el Client congelado, sin comparar fuentes mientras se editan.
