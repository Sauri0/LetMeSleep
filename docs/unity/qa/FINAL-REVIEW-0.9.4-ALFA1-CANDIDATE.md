# Revisión de candidata Unity 0.9.4-alfa.1 — 2026-09-12

**PASS para distribuir esta candidata; BLOCK para aprobar alfa y comenzar beta.** Esta revisión complementa el informe histórico de alfa, cuyo paquete no se modificó.

## Identidad publicada

- Fuente f0e6b80fd067ea7f25768c21d1e2cf898a8c65d1; tag v0.9.4-alfa.1.
- Unity 6000.3.24f1, Windows x64 Development, Succeeded, cero errores, contenido fuente limpio antes/después. Recibo ALFA1-WINDOWS-BUILD-20260912.json.
- ZIP 94,122,170 bytes; SHA256 d3d50527c55e7cb3800aaf918468371e48f188c47425af6c7325f71c93c90507.
- Launcher 1.1.1, 1,654,272 bytes; SHA256 3b84a8ab713e1b96e8c351a1d568d788542596e98ccc666b36f4d784c56a0158. Es necesario descargarlo una vez para reconocer revisiones numeradas; elegir la instalación anterior.
- Release pública no draft: https://github.com/Sauri0/LetMeSleep/releases/tag/v0.9.4-alfa.1 . Conserva símbolos Development managed/Burst como la candidata anterior.

## Cambios y comprobaciones

1. Almacenamiento: archivo principal inválido recupera respaldo sin sobrescribirlo. Se archiva el archivo rechazado. Perfiles futuros bloquean escritura, incluso si aparecen entre cargar y guardar. No se modificaron los ajustes reales del usuario ni la versión Godot.
2. Ocho StorageEditMode pasaron en Unity (0.16 s), recibo ALFA1-STORAGE-NATIVE-20260912.json. También pasaron cinco casos del clasificador real mediante JsonUtility: JSON dañado, esquema ausente, null, esquema 1 y esquema 2. Las pruebas externas y cuatro mutantes detectados siguen documentados en la entrega QA de almacenamiento.
3. Ejecutable Windows, respaldo: cargó Recovery QA, piel dark, pijama green, mosquito purple. preferencesRecovered=true. Tras guardar, respaldo original intacto y archivo rechazado de siete bytes conservado. Recibo ALFA1-RECOVERY-PLAYER-20260912.json.
4. Ejecutable Windows, esquema futuro: preferencesWriteBlocked=true, carga de valores predeterminados, navegación/entrenamientos/crear sala sin excepción; archivo conservado byte a byte. SHA256 antes/después 2a2f09cb41a5f0ed55b73fbe7fb0bffb958266da1ecb3622d6951b0fd3ee2a51. Recibo ALFA1-FUTURE-PLAYER-20260912.json.
5. Ambos probes pasaron sus seis comprobaciones: runtime de ambos roles, humano quieto, retorno al menú y crear/cerrar sala EOS. Esto NO demuestra unión entre dos usuarios ni WAN. Captura real del menú con versión alfa.1 y colores recuperados inspeccionada.
6. Launcher: 63 pruebas pasan, incluida ordenación alfa.1/alfa.2/alfa.10 y límite alfa/beta. Instalación real desde GitHub con checksum, manifiesto, activación y segundo inicio sin reinstalar: 3/3. El slot alfa anterior sigue presente. Log N:/LetMeSleep/Validation/launcher-public-alfa1.log.
7. U09414: W1 completó rondas nativas automáticas de ambos roles, resultado correcto, reinicio limpio y retorno al menú. Ver ../gameplay/U09414-NATIVE-TRAINING.md. No hubo input local: no sustituye recorrido manual ni aceptación de diversión/balance.
8. Build verifica diferencias reales de contenido, cambios staged y archivos untracked. Unity reescribe saltos de línea de fuentes dinámicas; ese cambio normalizado no altera el código y ya no genera falso positivo de fuente sucia.

## Dictamen y límites

- U09401/U09417: nueva identidad y distribución verificadas; sin extender sus conclusiones a aprobación del ciclo completo.
- U09414: PASS para ronda automática, resultado y reinicio dentro del alcance del informe W1.
- U09415: corrección de corrupción/esquema futuro probada en Unity y Windows; no implica validación manual de todas las combinaciones de ajustes. Importación completa Godot corresponde a gamma.
- U09402–13 y U09416 conservan los límites de sus informes originales. No se heredan aprobaciones online a partir de la creación de una sala.
- Quedan dos identidades y dos redes independientes, rondas online, recorrido manual completo, aprobación artística y balance por Branko, y rendimiento objetivo GTX 1660 Ti.
- El log informa reducción de resolución de sombras puntuales para encajar el atlas de 2048; conservar como limitación visual a evaluar, no como error de compilación ni justificación para afirmar rendimiento.
- Las medidas cortas de application-loop en RTX 3060 Ti no son una prueba de FPS sostenidos. El permiso para trabajar mientras el usuario duerme no aprueba automáticamente beta.

Ejecutable público instalado: ALFA1-PUBLIC-PLAYER-20260912.json, perfil nuevo por defecto, seis comprobaciones PASS, sin cambiar los ajustes reales del jugador. Launcher público descargado por separado y hash coincidente con GitHub.
