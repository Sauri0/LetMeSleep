# Revisión candidata Unity 0.9.4-alfa.2 — 2026-09-12

Candidata para pruebas. No aprueba alfa completa ni autoriza iniciar beta.

## Identidad

- Fuente/tag: 0a82314da866f94ae71e3807cdaea875b3d111a9 / v0.9.4-alfa.2.
- Unity 6000.3.24f1, Windows x64 Development; build Succeeded, cero errores, fuente limpia. ALFA2-WINDOWS-BUILD-20260912.json; output 230,521,130 bytes.
- ZIP 94,123,712 bytes; SHA256 8957f116541aa0fd027dbcc3c5cfe2d325060d431bcbcd88045e98041abec121.
- Launcher 1.1.1 sin cambios: SHA256 3b84a8ab713e1b96e8c351a1d568d788542596e98ccc666b36f4d784c56a0158.

## Cambios y evidencia

1. W1: b3cf37f mantiene alcance de adquisición únicamente durante aproximación al mismo soporte. Antes, el posado se cancelaba antes de tocar el piso (2/2 reproducciones). Nueve casos nuevos de normales/pérdida/cambio de superficie y suite CPU Gameplay 76/76 revisada por QA. Pruebas CPU no sustituyen geometría física Unity.
2. Recorrido Unity con comandos normales, sin teletransportar: escalera, planta superior, salida trasera al patio y vuelta. Posado real de piso y despegue comprobados. Ver ../gameplay/U09409-06-TRAVERSAL.md; entrada frontal y otras superficies físicas quedan pendientes.
3. W2: d2110be corrige origen de cámara superpuesto antes del barrido. Antes: cámara/pivot y=.056, radio .08, distancia cero. Después del mismo posado real: cámara y=.095, distancia .85, contactos vacíos; despega normalmente. Dos PlayMode pasan (3.04 s), incluyendo piso/pared/techo sintéticos, recuperación libre y obstrucción fina de 3 cm a 45 grados con controles negativos. No certifica todos los marcos/puertas reales.
4. W2: e038f72 + 8d977de y prefab generado usan plantillas URP Low/Medium admitidas; se descartó el intento Custom con enum no válido. Lobby: dos PointLight de 512, 3,145,728 texels (75% de 2048²). Casa: Living512, BedroomA/B256, 2,359,296 (56.25%). Conservan sombras. QA independiente verificó fuente, anclas y recibos ALFA2-LOBBY/HOUSE-LIGHTS. Los recibos nativos se atribuyen por workspace/tiempo, no sellan por sí solos commit/asset hash.
5. Build Windows visible: seis comprobaciones PASS (ambos roles, humano quieto, retorno al menú, crear/cerrar sala EOS). ALFA2-BUILD-PLAYER-20260912.json. Capturas reales de ambos roles inspeccionadas; no aparece el aviso de reducción automática del atlas en este log PC. Es una observación de este recorrido, no prueba visual exhaustiva.
6. Un primer build Succeeded tuvo sourceDirty=true por cachés de fuentes generadas en Play. No se distribuyó. Se recargaron los dos assets desde fuente versionada; el build final conserva sourceDirty=false. No se debilitaron comprobaciones de integridad.

## Límites

Unión de dos identidades y dos redes independientes, rondas online, recorrido completo, aprobación artística/diversión de Branko y GTX1660Ti continúan pendientes. Creación de sala no acredita transporte entre amigos. Mediciones breves de application-loop en RTX3060Ti no son FPS sostenidos. La ocupación teórica del atlas no prueba memoria GPU, ausencia de acne/leaks ni presupuesto de mapas futuros. Alfa y alfa.1 siguen inmutables; no se inició beta.

## Descarga pública verificada

Release no draft: https://github.com/Sauri0/LetMeSleep/releases/tag/v0.9.4-alfa.2 . GitHub coincide en tamaño y SHA256 con ZIP, checksum y launcher locales. Actualización real desde alfa.1: descarga, integridad, extracción/manifiesto, activación y segundo inicio sin reinstalar (3/3); ALFA2-PUBLIC-UPDATER-20260912.txt. Conserva los slots alfa, alfa.1 y alfa.2.

Ejecutable descargado, ventana visible y datos de prueba aislados: ALFA2-PUBLIC-PLAYER-20260912.json, seis comprobaciones PASS, versión alfa.2, 1920x1080/RTX3060Ti. El log tampoco informa reducción automática de sombras. No modifica el perfil real del jugador. El proceso terminó normalmente. Director cerró su editor PID5800 después de finalizar el lote; no queda GPU asignada a este lote.
