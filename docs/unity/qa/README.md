# QA funcional — Unity 0.9.4 alfa

Estado: **evidencia parcial**. Hay resultados Unity EditMode y un build/arranque EOS host; una partida WAN, PlayMode, revisión visual y paquete candidato permanecen sin acreditar.

- [Matriz de aceptación](ACCEPTANCE-MATRIX-0.9.4-ALFA.md): requisitos, negativos y evidencia mínima.
- [Checklist de candidata](CANDIDATE-CHECKLIST-0.9.4-ALFA.md): secuencia corta de decisión para un commit candidato.
- [Protocolo EOS/WAN](EOS-WAN-PROTOCOL-0.9.4-ALFA.md): ejecución entre dos equipos y dos redes físicas.
- [Auditoría de riesgos](MIGRATION-RISKS-0.9.4-ALFA.md): guardados, versión/launcher, EOS y diferencias de motor.
- [Supuestos registrados](ASSUMPTIONS-0.9.4-ALFA.md): decisiones aplicadas mientras Branko no está disponible.
- [Revisión Gameplay 68353fe](DELIVERY-REVIEW-GAMEPLAY-68353fe.md): un bloqueo de interfaz de puertas y dos precisiones antes del runtime.
- [Revisión funcional UI dde06ff](DELIVERY-REVIEW-UI-DDE06FF.md): cancelación async, deduplicación de intenciones y pausa de lobby.
- [Suite Core RoomSession](CORE-ROOMSESSION-TESTS.md): cobertura EditMode y límite de la comprobación externa previa.
- [Suite Gameplay Authority](GAMEPLAY-AUTHORITY-TESTS.md): puertas autoritativas y vigencia física de la unión de picadura.
- [Suite MessageFraming](MESSAGE-FRAMING-TESTS.md): límites, fragmentación y presupuesto acotado de reensamblado Online.
- [Estado del probe EOS Windows](EOS-WINDOWS-PROBE-2026-09-12.md): evidencia parcial de arranque, identidad y lobby; transporte/WAN pendientes.
- [Auditoría de trazabilidad](BUILD-TRACEABILITY-REVIEW-2026-09-12.md): corrige la base temporal del build EOS y separa fuente dirty de candidata reproducible.
- [Suite RoomWireCodec](ROOM-WIRE-CODEC-TESTS.md): roundtrip, límites y rechazo estricto del snapshot binario de sala.

La fuente normativa es `docs/unity/PLAN-UNITY-0.9.4.md`, autorizada para alfa por `AGENTS.md`. Los tests Godot citados son antecedentes para portar intención y diseñar negativos. Sus resultados no cuentan como evidencia Unity.

