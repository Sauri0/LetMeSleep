# Equipo reorganizado — recuperación artística alfa

Branko creó/renombró las doce tareas y autorizó retomar con «listo todos». Se conservan sus modelos configurados. Los títulos y responsables actuales sustituyen los anteriores: el historial de un chat no determina su propiedad nueva. Director integra y publica; alfa aún no aprobada, no iniciar beta.

## Responsables y carpetas

| Tarea exacta | ID | Carpeta asignada | Propiedad / primera entrega |
|---|---|---|---|
| Worker Online | 01a07995-46b7-79a2-8659-73ebacb9f0b2 | N:/LetMeSleep/Worktrees/online | Online, informe de pruebas EOS/código/unión/cierre de anfitrión; separar pruebas locales de WAN pendiente. No portal comercial, pagos ni publicación. |
| Worker Código / Gameplay | 01a09293-2464-7ed0-bc87-d15e2155f53b | N:/LetMeSleep/Worktrees/gameplay | Gameplay y Gameplay.Unity; retomar SurfaceVisualProbe borrador, validar comportamiento y medir contacto con modelo real. Presentation requiere coordinación. |
| Worker Presentación y Audio | 01a09722-d059-76a3-adc5-429985d8831b | N:/LetMeSleep/Worktrees/presentation | Presentation/Audio; iluminación, rig en runtime, mezcla/ciclo de audio. Evaluar living nuevo después de la geometría. |
| Worker UI | 01a09723-0340-70b0-b73f-d910db30405e | N:/LetMeSleep/Worktrees/ui | UI Runtime/Assets; hereda 5f4a381 y e269be1 pendientes de importar. Corregir con capturas reales 720/1080 y referencia. |
| Modelador Humanos | 01a09723-96eb-7331-9047-8849d78cb5a4 | N:/LetMeSleep/Worktrees/characters | Hereda borrador humano sin commit. Geometría/rig/ropa/manos/animación humana; separar funciones por especie antes de producción paralela. |
| Modelador de Mosquitos | 01a09723-6fab-72b3-98ca-3dea16d7420f | N:/LetMeSleep/Worktrees/mosquito | Geometría/rig/animación mosquito en módulos propios; nueva silueta, planos faciales, abdomen, patas y alas. Sin cambiar colisión/Root/Mouth unilateralmente. |
| Modelador Terreno y Mapas | 01a09293-7001-79e1-9881-1480d3eac9fc | N:/LetMeSleep/Worktrees/maps | Arquitectura y composición casa/patio/lobby, terreno/vegetación/exterior de ventanas. Ya no modifica personajes. Comenzar módulo exterior propio, sin editar montaje del living en curso. |
| Modelador Elementos | 01a09293-d828-7b03-955e-c2c961341b24 | N:/LetMeSleep/Worktrees/environment | Hereda living nuevo sin commit: geometría, muebles, textiles, luminarias y export editable. Conserva temporalmente su montaje y puntos de integración hasta entregar el lote. |
| Revisor Estabilidad | 01a09294-2600-7833-91f9-fb55fb9582b2 | N:/LetMeSleep/Validation/TeamRecovery/stability | Lectura central, informes propios; errores, procesos/audio residual, memoria, instalación. No edición runtime ni perfilar sin turno. |
| Revisar diseño visual | 01a09721-9161-7941-959d-662d56662c51 | N:/LetMeSleep/Validation/TeamRecovery/visual | Revisión independiente referencia/resultado de personajes, objetos, entorno/UI. Hallazgos concretos con ángulo/imagen y dueño. |
| Revisar animaciones | 01a09721-6ada-7ba1-9eee-d74e07a2ce93 | N:/LetMeSleep/Validation/TeamRecovery/animation | Revisión de clips completos, deformación, agarre, apoyo/transición. Auditar cobertura existente sin llamar PASS a poses estáticas. |
| Revisor Funcional | 01a09293-e751-7b91-8348-56526efb3eca | N:/LetMeSleep/Validation/TeamRecovery/functional | Revisión funcional independiente; ya no implementa UI. Recetas alfa con foco/Escape, personalización, permisos lobby, rondas, objetos, límites y pruebas pendientes. |

## Transferencias y archivos compartidos

- Fuentes humanas actuales: `characters/docs/unity/characters/PAUSE-20260912.md`. build_characters.py, author_motion.py, auditores y manifiesto contienen ambas especies. Humanos conserva esos archivos de orquestación durante la separación; Mosquitos crea módulos de geometría/movimiento propios y coordina su conexión. Nunca regenerar y entregar FBX de la otra especie accidentalmente. Director integra contratos/builders comunes.
- Living: `environment/docs/unity/environment/QUALITY-LIVING-WITNESS.md`. Elementos conserva AlfaQualityMeshes.cs, AlfaQualityLiving.cs y su delta existente en AlfaHouseDressing.cs/AlfaMapBuilder.cs/AlfaLobbyDressing.cs/hash hasta entregar. Mapas añade módulos exteriores propios y entrega el punto de conexión a Director; no pisa esos archivos. Separar montaje tras la primera importación si hace falta.
- Gameplay: `gameplay/docs/unity/gameplay/PAUSED-CHECKPOINT-20260912.md`. Borrador SurfaceVisualProbe.cs no compilado ni ejecutado. SetWorldPose integrado fc3d97b pertenece a Presentation; cambios adicionales se acuerdan con ese dueño.
- UI: 5f4a381 y e269be1 están en rama codex/unity-ui. Director los integra y captura; autor nuevo revisa y corrige. Capturas antiguas son historia.
- Arte central está rechazado. Nuevos cambios necesitan revisar render real y movimiento, no sólo compilar o contar piezas. Contratos obligatorios CHARACTER-QUALITY-BAR.md y UI-ENVIRONMENT-QUALITY-BAR.md, bajo docs/unity/art-review.

## Reglas de trabajo

**Prioridad de cierre actual (Branko, último mensaje):** cerrar la versión
actual con este equipo y reservar la renovación del modelado 3D para la
siguiente etapa con Higgsfield. Su encargado trabaja directamente con el
usuario y queda fuera de las asignaciones de cierre. Congelar rediseños,
variantes, materiales y expansiones; conservar únicamente cambios de rig/clip
que reparen fallos funcionales actuales. No confundir pendiente visual futuro
con controles, contactos, caídas o estabilidad todavía defectuosos. Director
prepara candidata testeable, evidencia de validación y pendientes separados.

**Monitor de pruebas:** por instrucción explícita de Branko, usar exclusivamente el monitor principal horizontal. En este equipo Windows identifica DISPLAY1 como principal (0,0,1920,1080) y DISPLAY2 como secundario vertical (-1080,-309,1080,1920). La invocación del player con `-monitor 1` produjo la captura horizontal correcta el 2026-09-13; verificar el destino real en cada sesión, sin asumir que la numeración de captura DXGI coincide con Unity. Nunca usar el monitor vertical para capturas, recorridos o evaluación visual. Esta regla del banco local no obliga a otros jugadores a usar una pantalla concreta.

No están solos: no revertir trabajo ajeno, no reset/clean, no stage global, no cambiar modelos, no crear subagentes ni publicar por cuenta propia. Cada autor usa sólo su carpeta en N:. Las carpetas de revisión son informes, no repositorios runtime alternativos. Leer central y fuentes de otros está permitido; escribir requiere propiedad explícita.

Un único editor/render/proceso Blender de carga por turno concedido por Director. Por defecto los doce preparan fuentes y revisiones; Director importa, ejecuta y captura. Tests del juego con -noaudio hasta prueba de escucha asignada. Sin colas de generación invisibles ni procesos huérfanos. Informar proceso/resultado/cleanup.

Primera muestra: humano en pijama/pantuflas/gorro, mosquito, living doméstico completo y menú/personalizador. Referencias originales en N:/LetMeSleep/References/{CharacterQuality,UIQuality,EnvironmentQuality}-20260912. Los textos de conceptos no crean modos, armas, crafting ni contenido extra. Respetar Let me sleep y reglas del plan.

Entrega breve: commit o lista exacta de borradores, archivos, pruebas realizadas con alcance, evidencia visual real, fallas restantes y siguiente dependencia. Los revisores devuelven defectos a su dueño; Director integra sólo entregas identificables. WAN entre casas, aceptación de Branko y rendimiento en hardware objetivo no pueden certificarse por pruebas locales.
