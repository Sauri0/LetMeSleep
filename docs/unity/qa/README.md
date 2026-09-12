# QA funcional — Unity 0.9.4 alfa

Estado: **candidata pública alfa.1 distribuible para diagnóstico; alfa todavía BLOCK**. Pasaron build limpio, paquete, actualización pública, UI, almacenamiento nativo/Windows y rondas automáticas de ambos roles. Siguen pendientes el par EOS/WAN, rondas online y recorrido manual integral, rendimiento objetivo y aprobación de Branko.

- [Informe alfa.1](FINAL-REVIEW-0.9.4-ALFA1-CANDIDATE.md): recuperación de guardados, protección de esquemas futuros, launcher 1.1.1 y nueva identidad publicada. El informe alfa original de abajo conserva su evidencia histórica.

- [Informe QA final de la candidata](FINAL-REVIEW-0.9.4-ALFA-CANDIDATE.md): identidad pública, evidencia, dictamen U094-01…17 y condiciones para aprobar alfa.
- [Matriz de aceptación](ACCEPTANCE-MATRIX-0.9.4-ALFA.md): requisitos, negativos y evidencia mínima.
- [Checklist de candidata](CANDIDATE-CHECKLIST-0.9.4-ALFA.md): secuencia corta de decisión para un commit candidato.
- [Protocolo EOS/WAN](EOS-WAN-PROTOCOL-0.9.4-ALFA.md): ejecución entre dos equipos y dos redes físicas.
- [Auditoría de riesgos](MIGRATION-RISKS-0.9.4-ALFA.md): guardados, versión/launcher, EOS y diferencias de motor.
- [Supuestos registrados](ASSUMPTIONS-0.9.4-ALFA.md): decisiones aplicadas mientras Branko no está disponible.
- [Revisión Gameplay 68353fe](DELIVERY-REVIEW-GAMEPLAY-68353fe.md): un bloqueo de interfaz de puertas y dos precisiones antes del runtime.
- [Revisión funcional UI dde06ff](DELIVERY-REVIEW-UI-DDE06FF.md): cancelación async, deduplicación de intenciones y pausa de lobby.
- [Revisión funcional de pickups ff5f290](DELIVERY-REVIEW-TOOL-PICKUPS-FF5F290.md): autoridad/equipo replicado, composición, eje físico y gates nativos.
- [Revisión funcional de Bootstrap](BOOTSTRAP-FUNCTIONAL-REVIEW-2026-09-12.md): ciclo sala/ronda/resultados, errores online, entrenamiento y persistencia.
- [Evidencia PlayMode de entrenamiento](TRAINING-BOOTSTRAP-PLAYMODE-2026-09-12.md): escena de arranque, ambos roles, actores, cámara, pickups, estabilidad numérica y limpieza.
- [Recibo nativo de defensa humana](ALFA-TRAINING-HUMAN-DEFENSE-RECEIPT.json): apuntado y acción primaria manual contra mosquitos cercanos, con atribución exacta del golpe y candidata limpia fuera de alcance.
- [Suite Core RoomSession](CORE-ROOMSESSION-TESTS.md): cobertura EditMode y límite de la comprobación externa previa.
- [Suite Gameplay Authority](GAMEPLAY-AUTHORITY-TESTS.md): puertas autoritativas y vigencia física de la unión de picadura.
- [Suite MessageFraming](MESSAGE-FRAMING-TESTS.md): límites, fragmentación y presupuesto acotado de reensamblado Online.
- [Suite protocolo/herramientas](PROTOCOL-TOOL-OWNERSHIP-TESTS.md): alfa-2, roundtrip de equipo y rechazo de propiedad inconsistente.
- [Suite de persistencia segura](PREFERENCE-FILE-STORE-TESTS.md): recuperación/backup, versiones futuras, límite de 16 KiB y activación atómica, con tres reproducciones negativas.
- [Estado del probe EOS Windows](EOS-WINDOWS-PROBE-2026-09-12.md): evidencia parcial de arranque, identidad y lobby; transporte/WAN pendientes.
- [Revisión de política del cliente EOS](EOS-CLIENT-POLICY-REVIEW-2026-09-12.md): criterio de distribución de la configuración P2P y permisos observados en el portal sin exponer credenciales.
- [Auditoría de trazabilidad](BUILD-TRACEABILITY-REVIEW-2026-09-12.md): corrige la base temporal del build EOS y separa fuente dirty de candidata reproducible.
- [Suite RoomWireCodec](ROOM-WIRE-CODEC-TESTS.md): roundtrip, límites y rechazo estricto del snapshot binario de sala.

La fuente normativa es `docs/unity/PLAN-UNITY-0.9.4.md`, autorizada para alfa por `AGENTS.md`. Los tests Godot citados son antecedentes para portar intención y diseñar negativos. Sus resultados no cuentan como evidencia Unity.

