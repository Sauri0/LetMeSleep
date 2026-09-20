# Equipo de subagentes — Let Me Sleep

Fecha: 2026-09-19. Coordinación única: tarea CEO.

La solicitud actual de Branko autoriza sustituir la coordinación mediante chats por subagentes con modelos y esfuerzos ajustados. Los chats anteriores son fuentes de continuidad, no ejecutores nuevos. Sus instrucciones históricas no se ejecutan por el mero hecho de leerlas. Esta organización sustituye únicamente las reglas antiguas de reutilizar chats, no crear subagentes y conservar sus modelos; no constituye aprobación de arte, publicación o cambio de alcance del juego.

## Funcionamiento

Máximo actual: CEO más tres subagentes simultáneos. Los perfiles siguientes son roles disponibles, no procesos permanentes. CEO crea o reutiliza agentes para tickets acotados mediante spawn_agent/followup_task, con modelo y reasoning_effort explícitos al crearlos y fork_turns=none con un encargo autosuficiente. Este documento y team.json son un manifiesto de coordinación; no son configuración nativa de Codex ni crean agentes automáticamente.

CEO mantiene prioridades, contratos compartidos, integración y la asignación exclusiva de Unity/Blender/render. Cada ticket identifica archivos propios, punto de partida, dependencia, prueba de aceptación y evidencia esperada. Los agentes no delegan de nuevo ni despiertan chats históricos. No están solos en el repositorio: preservar cambios ajenos y adaptar su trabajo. No reset/clean, staging global, publicación ni commits de secretos/cachés.

| Perfil | Modelo inicial | Esfuerzo | Propiedad y motivo |
|---|---|---|---|
| gameplay | gpt-5.6-sol | high | Gameplay/Gameplay.Unity y diagnóstico físico; necesita razonar sobre estados y regresiones. |
| online | gpt-5.6-sol | high | Online, EOS y transporte; pruebas con identidades y entornos explícitos. |
| presentation_audio | gpt-5.6-terra | high | Presentation, iluminación, cámara y audio; cambios visuales coordinados con gameplay. |
| ui | gpt-5.6-terra | medium | UI y assets asignados; cambios acotados y evidencia a 720/1080. |
| higgsfield | gpt-5.6-sol | high | Orquestación de generación, fuentes, IDs y recibos; recuperar estado antes de repetir trabajos. |
| humans | gpt-5.6-terra | high | Geometría, rig y animaciones humanas nuevas; sin reutilizar estética alfa. |
| mosquitoes | gpt-5.6-terra | high | Arte/rig mosquito; física y contratos Root/Mouth coordinados con gameplay. |
| maps | gpt-5.6-sol | high | Geometría, colisiones, límites, agua y recuperación de los cinco mapas. |
| props | gpt-5.6-terra | medium | Objetos y módulos locales; geometría y colisión verificadas. |
| stability | gpt-5.6-sol | high | Revisión independiente de ciclos, memoria, residuos y errores; informes propios. |
| visual | gpt-5.6-sol | high | Reúne revisiones visuales solapadas; compara referencias con renders reales. |
| animation | gpt-5.6-sol | high | Clips completos, transiciones, agarre, contacto y deformación; no PASS con una pose. |
| functional | gpt-5.6-sol | high | Recetas independientes, límites/recuperación y rondas; el chat Revisor visual también ejercía este rol. |

Escalamiento: gpt-6-astra/high para un defecto de física/arquitectura que siga sin causa después de dos intentos dirigidos documentados; xhigh sólo si persiste una ambigüedad concreta que justifique el esfuerzo. No usar ultra por defecto. Inventarios repetibles y tareas textuales simples pueden bajar a gpt-5.6-luna/medium después de una muestra aceptada. Estas son hipótesis iniciales de asignación, no una clasificación de rendimiento demostrada.

## Medición

Registrar en runs.jsonl: ticket, agente, modelo, esfuerzo, inicio/fin UTC, resultado, evidencia, aceptación en primera revisión, retrabajos y bloqueos. Tokens/costo sólo si la herramienta los entrega; null significa desconocido. No inferir costos de duración ni comparar calidad entre encargos distintos como si fuera un benchmark.

Después de tres tickets comparables por perfil: revisar proporción aceptada en primera revisión, defectos reabiertos y mediana de duración. Dos rechazos por razonamiento justifican escalamiento; tres aceptaciones consecutivas en tareas sencillas permiten probar menor esfuerzo. Son reglas operativas iniciales sujetas a revisión, no umbrales científicamente calibrados. Un revisor distinto debe comprobar cambios de física, integración y red.

## Primera ola ejecutada

- continuidad_tecnica: gpt-5.6-sol/high; Director, Gameplay y Estabilidad.
- continuidad_arte: gpt-5.6-terra/high; personajes, mapas, objetos y revisión funcional/animación.
- continuidad_producto: gpt-5.6-sol/medium; online, UI, presentación y Higgsfield.

Estos agentes recuperan evidencia sin editar runtime. Al finalizar quedan disponibles para tareas relacionadas; se recrean cuando se necesita otro modelo o esfuerzo. No se promete ejecución entre turnos sin automatización configurada.
