# Informe QA final — candidata Unity 0.9.4 alfa

Fecha: 12 de septiembre de 2026

Fuente candidata: `35c2af4b168f5b95943f09fbb2c556924dd7b2cf`

Release pública: [Let me sleep 0.9.4-alfa — candidata Unity](https://github.com/Sauri0/LetMeSleep/releases/tag/v0.9.4-alfa)

## Dictamen

**PASS para publicar y distribuir esta candidata diagnóstica. BLOCK para declarar la alfa aprobada, aprobar el online o iniciar beta.**

La excepción está prevista por el plan: puede existir una candidata descargable antes de la prueba WAN, pero no se declara online aprobado ni se avanza automáticamente de etapa. Siguen faltando dos identidades EOS, dos redes físicas, rondas completas, recorrido funcional integral, rendimiento objetivo y aprobación de Branko.

## Identidad del artefacto

| Campo | Resultado |
|---|---|
| Build Windows | `Succeeded`, 0 errores, Unity `6000.3.24f1` |
| Procedencia | `sourceCommit=35c2af4b168f5b95943f09fbb2c556924dd7b2cf`, `sourceDirty=false` |
| Tamaño de build | 230,513,038 bytes |
| ZIP público | `Let-me-sleep-0.9.4-alfa-Windows.zip`, 94,118,628 bytes |
| SHA-256 ZIP | `e94038057ef08b23b91ed7246983c4a96f0dfaf8b651906f3fb7d970f8ab4e35` |
| SHA-256 EXE | `30d4b41a85303abf83c686f06b75ab1681471c79b9dc7fe530ead09099fdf63d` |
| Manifiesto | `0.9.4-alfa`, Unity `6000.3.24f1`, mismo commit, fuente limpia y hashes por archivo |

El [recibo de build](ALFA-WINDOWS-BUILD-20260912.json) conserva la procedencia dentro del repositorio. El original está en `N:/LetMeSleep/Artifacts/alfa-20260912-115149/build-receipt.json`; el paquete local aprobado está en `N:/LetMeSleep/Artifacts/packages-alfa-20260912/`.

Es un Development Build de diagnóstico. El Director aceptó conservar 123 PDB anidados, 13,145,640 bytes sin comprimir. El escaneo binario no encontró JWT, claves PEM, tokens personales ni valores de `ClientId`/`ClientSecret` fuera de `StreamingAssets/online.local.json`. Las coincidencias restantes fueron metadatos de dependencias upstream y la ruta del workspace público; no apareció una ruta `Private`.

## Evidencia ejecutada

- Unity EditMode integrado: 61/61, 0 fallos. Cubre dominio de sala, protocolo alfa-2, framing, autoridad de puertas/picadura, réplica y propiedad de herramienta; no acredita física/render/red real.
- Unity PlayMode final: 2/2 en 1.45 s. Arranca y limpia entrenamiento de ambos roles, actores, cámara y pickups; no es una ronda completa ni certifica navegación de bots.
- Ejecutable candidato local: entrenamiento humano y mosquito, humano estacionario, regreso al menú y ciclo host EOS crear/salir. `human.png` y `mosquito.png` pertenecen a esos roles. `menu.png` fue capturada después de una transición asíncrona y ya muestra entrenamiento; no acredita el menú.
- UI: revisión visual por la tarea de interfaz a 720p PASS, sin glifos faltantes, truncamiento, clipping ni solapamientos en el personalizador.
- [Descarga pública](ALFA-PUBLIC-DISTRIBUTION-20260912.json): `EnsureLatest` pasó checksum, extracción, manifiesto, activación persistida y segundo inicio sin reinstalar en una ruta con espacios. El Launcher EXE público coincide con el hash aprobado `38410f29b7436d14c986dcafdd49fae7629282eaf3d8e43134862f40d94ad90a`.
- [Ejecutable público visible](ALFA-PUBLIC-VISIBLE-PLAYER-20260912.json): repitió todos los booleanos funcionales del probe a 1920×1080, produjo una captura humana visible y terminó el proceso.
- Launcher: 54 comprobaciones offline, 0 fallos; instalación pública mediante updater, 3 comprobaciones, 0 fallos.

Los probes locales se ejecutaron a 1920×1080 sobre una RTX 3060 Ti. Sus deltas de loop no son frames GPU fiables: la segunda corrida pudo quedar minimizada y produjo valores artificialmente bajos. Este informe anula cualquier afirmación de FPS basada en esos campos.

## Estado U094-01…17

| Gate | Estado | Evidencia y falta concreta |
|---|---|---|
| U094-01 Identidad y alcance | **PASS** | Build, manifiesto, protocolo alfa-2 y UI coinciden con `0.9.4-alfa`; sólo Sangre y Casa con patio están expuestos. |
| U094-02 Cuerpo y cámara humana | **BLOCK — parcial** | Runtime humano y estabilidad estacionaria observados; faltan giro 360°, carrera/agachado/salto, extremos de mirada y testigos completos sobre la candidata. |
| U094-03 Manos y defensa manual | **BLOCK — parcial** | Autoridad, propiedad de herramienta y defensa manual parcial pasan; faltan ambas manos, continuidad, coincidencia palma/contacto ≤5 mm y vistas de herramienta sin penetración. |
| U094-04 Clipping y oclusión | **BLOCK — parcial** | Hay capturas nativas acotadas; falta barrido reproducible de cámaras/cuerpo/ropa/herramienta contra todos los sólidos y estados. |
| U094-05 Puertas | **BLOCK — parcial** | Contrato autoritativo, bloqueo y nueve poses iniciales abiertas están cubiertos en fuente; falta recorrido físico cerrado/45°/abierto, ambos sentidos y roles en la candidata. |
| U094-06 Vuelo y superficies | **BLOCK — parcial** | Runtime mosquito y navegación parcial observados; faltan las seis normales, posado/despegue, puertas y recorrido interior/patio completo. |
| U094-07 Picadura sin marcas | **BLOCK — parcial** | Privacidad, contacto continuo y limpieza al desprender pasan en dominio; falta PlayMode sobre poses, obstáculos y superficies, más inspección completa del HUD/build. |
| U094-08 Sangre y defensa | **BLOCK — parcial** | Autoridad y defensa manual parcial acreditadas; faltan dos rondas host/invitado, finales autoritativos y limpieza completa entre rondas. |
| U094-09 Casa fija y patio | **BLOCK — parcial** | Contenido fijo, normales y composición visual revisados; falta recorrer físicamente todas las rutas en ambos sentidos y roles, incluidos reinicios. |
| U094-10 Lobby y rondas | **BLOCK — externo** | Host, lobby y reglas de roster cubiertos parcialmente; faltan invitado real, restricciones de dueño y dos rondas con retorno, nuevo sorteo y reset. |
| U094-11 Código de sala | **BLOCK — externo** | Host creó código y el codec rechaza entradas inválidas; falta resolución/unión con segunda identidad y negativos EOS reales. |
| U094-12 EOS local y cierre | **BLOCK — externo** | Crear/salir host funciona; faltan dos identidades, transporte, cancelaciones/callbacks tardíos, salida guest y cierre host sin migración. |
| U094-13 WAN en dos redes | **BLOCK — externo** | Sin ejecución entre dos equipos, identidades y redes físicas ni dos recibos cruzados de varias rondas. |
| U094-14 Entrenamiento | **BLOCK — parcial** | Ambos roles arrancan y limpian; bots navegan y la defensa funciona parcialmente. Falta ronda completa, interior/patio/puertas y reinicio sin estado residual. |
| U094-15 Personalización y guardado | **BLOCK — defecto confirmado** | UI y guardado básico fueron integrados, pero la candidata publicada no recupera correctamente un backup sano tras un primario malformado y puede sobrescribir datos recuperables o de versión futura. La corrección requiere nueva candidata y smoke con perfil limpio, corrupto y futuro. Alfa debe dejar los datos Godot intactos; su importación completa corresponde a gamma. |
| U094-16 Arte, audio y rendimiento | **BLOCK — externo** | UI 720p y capturas de ambos roles pasan parcialmente. Faltan recorrido audiovisual continuo y profiler 1080p/60 en GTX 1660 Ti con escenario, percentiles y memoria válidos. |
| U094-17 Launcher y artefacto | **PASS** | ZIP/checksum/manifiesto públicos, ruta con espacios, activación persistida, segundo inicio sin reinstalar, arranque del juego descargado y hash del Launcher EXE acreditados. Los tests negativos offline conservan la versión previa ante paquete o activación fallidos. |

Conteo al emitir este informe: **2 PASS, 15 BLOCK**. Los BLOCK indican evidencia faltante; no todos representan un defecto reproducible.

## Condiciones de cierre

1. Ejecutar par EOS local con dos identidades y cubrir create/join/transporte/cancel/retry/salidas/cierre y dos rondas.
2. Ejecutar WAN entre dos equipos y redes físicas con el mismo ZIP, al menos dos rondas y recibos cruzados sanitizados.
3. Completar recorrido manual de ambos roles, entrenamiento y persistencia sobre este artefacto.
4. Medir el escenario acordado en GTX 1660 Ti y revisar arte/audio en movimiento.
5. Corregir defectos encontrados, generar nueva candidata si cambia fuente y repetir sólo la evidencia afectada. Branko prueba y aprueba antes de beta.
