# HUM-INT-001 — requisitos técnicos separados del diseño nuevo

Estado: lectura y diagnóstico; sin cambios de runtime, modelos ni tests ejecutados.

La nueva autoría parte de cero desde bocetos. No se transfiere geometría, material,
proporción ni rig antiguo a Higgsfield. Esta ficha sólo permite preparar un adaptador
de integración después de aprobar el diseño nuevo.

- Unity 6000.3.24f1, URP17.3.0, proyecto N:/LetMeSleep/Repository/unity.
- CharacterView/ToolView y builders tienen contratos de IDs, clips y puntos de
  conexión. Medir los sockets del modelo NUEVO; adaptar enlaces y calibración de
  forma explícita. No escalar una malla para disimular un error de interfaz.
- Menú, primera persona y gameplay deben consumir la misma identidad nueva;
  no reusar la malla de menú vieja bajo la etiqueta de humano nuevo.
- Los recibos VisualAttentionContracts se invalidan al importar FBX; regenerarlos
  con los builders y no editar hashes manualmente.
- Bloqueo real de pruebas leído en locomotion-transitions.log: Tests.PlayMode no
  referencia LetMeSleep.Content.Characters ni LetMeSleep.Presentation.Gameplay.
  Ambas asambleas existen; HumanLocomotionTransitionTests usa sus tipos. Reparación
  acotada pendiente antes de validar el candidato en Unity. No es aprobación visual.
- Las fuentes/render históricos no integran el paquete creativo enviado a Higgsfield.

La primera referencia se cotizó en MCP a 3 créditos exactos con Seedream5.0Pro,
2K,3:4, una imagen y sólo UI-06/PER-06. El MCP respondió Ultra3000 en esta sesión.
Subir esas dos imágenes no encargó ninguna generación. Reserva en REGISTRO-TRABAJOS.json.
