# Revisión funcional de pickups `ff5f290`

Entrega revisada: `ff5f290`, integrada inicialmente como `9df9d43`; corrección geométrica `f5955f7`, integrada como `0450436`; presentación `3d36f55`.

Dictamen: **BLOCK de integración y evidencia nativa**. El dominio autoritativo y el codec pasan sus comprobaciones externas, pero el eje del objeto visible todavía difiere del contrato físico y no hay prueba Unity/PlayMode ni réplica WAN.

## Evidencia ejecutada por QA

- `Run-GameplayExternalHarness.ps1` contra la fuente de `ff5f290`: **33 tests / 0 fallos**; no usa Unity Runner.
- `docs/unity/gameplay/validation/Run-Validation.ps1` sobre `f5955f7`: **58 comprobaciones / 0 fallos**, compilación C#9/netstandard2.1 contra Unity `6000.3.24f1`: **0 errores / 0 advertencias**.
- Recibo local: `N:/LetMeSleep/Validation/Gameplay-3f6bef929b3341bb9d30b0d5c1fb3cc5`.

## Conformidades observadas

- Cada humano empieza con manos y puede poseer como máximo una unidad; el mosquito no puede recoger ni dejar.
- La primera acción autoritativa consume la unidad. Repeticiones, segundo actor, distancia inválida y revisión obsoleta no duplican el pickup.
- Dejar conserva la herramienta cuando no hay una pose libre. Una desconexión usa la pose inicial como fallback.
- El snapshot contiene equipo por actor y estado/revisión por pickup; el codec rechaza dueños no humanos, equipo huérfano, IDs repetidos y exceso de 32 unidades.
- Una nueva ronda reconstruye pickups y equipo desde el contrato inicial.
- `f5955f7` limita geometría, puertas, herramientas y consultas físicas al `MapRoot` activo. También compara la pose recibida con la autorada antes de aplicar el lote completo, con tolerancia de 1 mm y 0,1°.

## Hallazgos de integración

### T1 — Crítica — `Begin` online omitía herramientas y el protocolo no cambió

La primera integración serializaba actores pero reconstruía el cliente con `ToolDefinitions` vacío. Un mapa con pickups abortaba `BeginTools`; además `GameplayWireCodec` pasó a versión 2 mientras `RoomSession.Protocol` seguía en alfa-1.

La corrección activa del Director amplía `Begin` a versión 2, valida las herramientas contra el mapa local antes del ACK, pasa definiciones en host/cliente/entrenamiento y cambia el protocolo a `lms-unity-094-alfa-2`. Falta sellarla en un commit y ejecutar dos extremos sobre el mismo build.

### T2 — Alta — El pickup visible usa un eje distinto del volumen autoritativo

El contrato de mapa coloca el grip en el marker y usa `+Z` como eje longitudinal: el trigger mide 0,40 m centrado en `z=0,18`. El prefab integrado tiene `Grip=(0,0,0)` e `Impact≈(0,+0,365,+0,005)`, por lo que su longitud está en `+Y`. `GameplayVisualPresenter` aplica la pose replicada directamente al root.

En mesa o suelo, la malla queda perpendicular al trigger y al eje declarado. Unificar la convención en prefab o aplicar una corrección de base compartida; agregar un gate nativo que compare `Grip→Impact` con el eje físico y el centro del trigger.

### T3 — Alta — La composición no estaba conectada al contrato de ronda

La entrega inicial encontraba objetos globalmente, los markers eran sólo `Transform`, el host y entrenamiento pasaban únicamente puertas, y todos los humanos mostraban el matamoscas desde spawn.

Los commits `0450436`, `e0c8a1a` y `3d36f55` corrigen estáticamente el root de mapa, siete pickups estables y la visibilidad por propietario. Deben reverificarse juntos después de regenerar los prefabs en Unity.

## Gates pendientes

- Unity Runner del conjunto integrado y recibo ligado al commit exacto.
- PlayMode: recoger, disputar, golpear, soltar libre/bloqueado, desconectar dueño y reiniciar segunda ronda.
- Capturas de grip, impacto, trigger y malla sobre cada tipo de apoyo; tolerancias de U094-03/U094-04.
- Host/invitado: aparición/desaparición y transferencia idénticas con pérdida/reordenamiento; dos rondas WAN.

