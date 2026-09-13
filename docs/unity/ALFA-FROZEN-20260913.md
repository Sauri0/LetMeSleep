# Alfa congelada por decisión del usuario

Orden final de Branko: detener el equipo, cerrar la alfa tal como está,
anotar lo no logrado y actualizar GitHub. Esta orden sustituye las órdenes
anteriores de continuar autónomamente. No iniciar beta ni nuevas correcciones,
pruebas, builds o importaciones hasta una nueva indicación. Higgsfield sigue
bajo coordinación directa del usuario y queda fuera de este cierre.

## Estado conservado

- Fuente integrada: `5c82b12`, rama `codex/unity-094-alfa`, más este cierre documental.
- Se conservan los cambios de locomoción, superficies, cámara, audio, UI,
  parpadeo y contacto ya integrados. No se incorporan entregas tardías ni borradores.
- Último ejecutable local: `0.9.4-alfa.3`, fuente `e9d15e7`, en
  `N:/LetMeSleep/Artifacts/alfa-20260913-000715`.
- ZIP local: `N:/LetMeSleep/Artifacts/packages-alfa3/Let-me-sleep-0.9.4-alfa.3-Windows.zip`.
  SHA-256: `1095b3e6fa49a74a53f264ab7533f1767e51de3c18bc84a27254f575c872e473`.
- Ese ejecutable es anterior a las últimas correcciones de fuente. No se
  reconstruye ni se anuncia como corregido. La última prerelease publicada
  observada en GitHub es `v0.9.4-alfa.2`; actualizar la rama no reemplaza ese binario.
- Cierre administrativo solicitado, no certificación de calidad ni aprobación
  de las pruebas pendientes.

## Pendientes para retomar

1. **Pruebas de locomoción no ejecutadas:** la última corrida nativa no pudo
   compilar `HumanLocomotionTransitionTests.cs`: faltan referencias de
   `LetMeSleep.Content.Characters` y `LetMeSleep.Presentation.Gameplay` en el
   asmdef PlayMode. No se corrigió después de la orden de parada. Recibo:
   `N:/LetMeSleep/Validation/alfa3-corrections-20260913/locomotion-transitions.log`.
2. **Golpes y alcance:** persisten objetivos admitidos fuera del alcance físico.
   La corrección interna de muñeca `9b214e8` todavía no tiene medición nativa
   posterior. La nueva calibración por rig y bloqueo de paredes queda en borrador
   de Gameplay; no está integrada. Falta validar golpe agachado y transiciones
   con controles reales, sin estiramiento ni contacto ficticio.
3. **Caída articulada:** bloqueo de entrada integrado con pruebas CPU; ragdoll
   humano pendiente. El módulo aislado de mosquito sigue sin activar y falló
   cuatro corridas; última separación máxima 5.51 cm frente al límite de 12 mm,
   sin reposo a los 10 s. Host de subpasos en borrador, no probado ni integrado.
4. **Red de poses físicas:** codec y gate aislados tienen pruebas, pero faltan
   conexión de sesión, negociación, perfiles, simulación autoritativa,
   representación remota y recuperación. No hay ragdoll online certificado.
5. **Locomoción/audio:** cuatro clips importados y preservación de modelo/rig
   auditada numéricamente. Faltan PlayMode de transiciones, secuencia efectiva
   de cámara, apoyos, escalera, escucha de pasos y zumbido en todos los estados.
   Test adicional de cámara `bf59d3a` permanece en la rama de Presentación,
   sin integrar ni ejecutar nativamente.
6. **Interacción y cámara:** F/superficies, picadura y zoom tienen comprobaciones
   acotadas; falta recorrido real en mapas, rueda, orientación y claridad de
   defensa/impacto para ambos jugadores.
7. **Ojos y parpadeo:** A/B estático corrigió artefactos de mentón/cachete y cierre
   de ojos. No equivale a aprobación de animación continua en partida; quedan
   seguimiento y límites de mirada por verificar, incluida oclusión por paredes.
8. **UI y salida:** quedan pruebas nativas de HUD 720/1080, persistencia de
   resolución/audio en carpeta aislada, reapertura, input/foco y todas las rutas
   de salida sin proceso o audio residual. Usar sólo el monitor principal.
9. **Paquete y amigos:** falta construir y sellar un nuevo ejecutable de esta
   fuente, pruebas completas de flujo y partida desde otra PC/conexión. Crear
   una sala local no valida WAN. El amigo lo probará cuando esté disponible.
10. **Renovación visual:** modelos, materiales, variantes y composición de
    escenas se reorganizarán después con Higgsfield. No se inició esa migración
    dentro de este cierre.

Detalle por reporte: [USER-GAMEPLAY-CORRECTIONS-20260913.md](qa/USER-GAMEPLAY-CORRECTIONS-20260913.md).
Evidencia local conservada en `N:/LetMeSleep/Validation/`. Los worktrees y sus
borradores se preservan sin reset, limpieza ni integración automática.

## Entregas detenidas sin integrar

- Gameplay: `N:/LetMeSleep/Worktrees/gameplay`, base `9b214e8`, rama
  `codex/gameplay-rig-strike-contract`; checkpoint
  `docs/unity/gameplay/STOP-CHECKPOINT-20260913.md`. Contrato/perfiles/world/proxy
  y Authority en borrador sin commit; requiere bootstrap y ContentHash coherentes.
- Mosquitos: `N:/LetMeSleep/Worktrees/mosquito-physics`, último commit `757a587`.
  Borrador sin commit de PhysicsHost/EnvironmentPart, Builder/Simulation y prueba.
  No probado en Unity ni conectado a gameplay.
- Presentación: `bf59d3a` añade test de secuencia de cámara; permanece fuera de la
  rama integrada. No equivale a validación nativa.
- Humanos: entregas hasta `773a341` conservadas e integradas previamente;
  director importó los clips en `1aff7ca`. Sin nuevas exportaciones.
