# Supuestos registrados — Unity 0.9.4 alfa

Fecha: 2026-09-12. Se aplican para avanzar sin preguntas durante este lote y se revisan si el código integrado fija un contrato más preciso.

| Tema | Supuesto de QA | Consecuencia en los gates |
|---|---|---|
| Autorización | El encabezado antiguo “propuesta” del plan queda superado por la autorización de alfa en `AGENTS.md` y la instrucción del Director. | QA prepara y ejecutará alfa; no inicia beta. |
| Alcance jugable | Alfa expone sólo Sangre, Casa con patio y lobby separado. | Supervivencia, Tareas, inventario amplio, estamina y voz no pueden aparecer como seleccionables. |
| Mapas del ciclo | El ciclo tendrá casa/patio, isla marítima, pantano nocturno, cabaña de montaña y granja/granero. | Sólo casa/patio se implementa o acepta en alfa; los otros IDs pueden reservarse como datos no jugables sin UI ni carga. |
| Lobby | Para alfa se valida lobby 3D en tercera persona, coherente con el plan y la separación de escenas. | La cámara de ronda humana sigue siendo primera persona; lobby no hereda su máscara de cabeza. |
| Código de sala | El formato y límite exacto los define el contrato Online del Director. | QA exige opacidad, tamaño acotado, protocolo, integridad y ausencia de endpoint/secretos, sin inventar un prefijo. |
| Protocolo | El número Unity aún no está fijado en este worktree. | El gate exige una sola fuente y coincidencia build/manifiesto/handshake; nunca reutiliza un número Godot por inercia. |
| Balance Sangre/desmayo | Tasas, cuota y duración de desmayo provienen de la configuración alfa integrada y se miden en partidas reales. | Los tests comprueban invariantes y el valor declarado; no fijan los antiguos 12, 0,8/s o 35 s. |
| Tareas | Las tres vidas personales y el desmayo siguen siendo regla del ciclo, pero Tareas pertenece a beta. | Se documenta para no perderla en arquitectura/guardados; no se habilita ni bloquea la candidata alfa por gameplay de Tareas. |
| Capacidad | Hasta 16 participantes y 1–5 humanos son límites provisionales sujetos a red/rendimiento. | Alfa debe validar sus límites declarados y nunca prometer 16 si el perfil real los reduce con evidencia. |
| WAN | No hay amigo/equipo externo disponible dentro de este lote documental. | U094-13 permanece BLOCK hasta dos equipos y dos redes físicas; no se sustituye por local/LAN. |
| Arte y rendimiento | Director coordina Unity/GPU y QA no abre editor o juego sin turno. | U094-16 permanece BLOCK hasta evidencia nativa sobre el build candidato. |
| Referencias visuales | `work/references094/**` orienta estilo y composición. | Nombres, crafting, clases, armas, recompensas y modos dibujados no se convierten en requisitos. |
