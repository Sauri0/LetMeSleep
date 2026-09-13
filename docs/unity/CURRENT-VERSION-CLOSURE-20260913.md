# Cierre de versión actual antes de renovación 3D

**Sustituido por la orden final de detener todo y congelar como está.**
El orden de trabajo de abajo queda histórico, no es una cola activa.
Estado y pendientes: `ALFA-FROZEN-20260913.md`.

Branko solicita cerrar todo lo posible de la versión actual con el equipo,
reservando la renovación de modelos para Higgsfield. La tarea de Higgsfield
trabaja con él directamente. No generar reemplazos, nuevas variantes ni
expansiones en esta tanda. Las referencias artísticas siguen como dirección
para esa etapa, sin impedir aislar y probar las correcciones funcionales.

## Orden de cierre

1. Corregir contacto y alcance manual del golpe sobre el rig actual; conservar
   crouch, huesos y agarre. Un objetivo físicamente inalcanzable sigue siendo un
   fallo si la colisión afirma que allí llega la mano. No autoapuntar.
2. Terminar sólo clips de locomoción necesarios y conectarlos al reloj único
   de pies/audio; validar transición a golpe, pausa, escalera y recuperación.
3. Comprobar cámara/rueda del mosquito, F y superficies, picadura/defensa y HUD
   contextual con controles reales en monitor principal.
4. Resolver la caída articulada y bloqueo de control, sin activar el módulo
   físico aislado hasta que sus articulaciones sean estables. No sustituirla
   por una pose aleatoria y llamarla física. Registrar alcance si sigue abierta.
5. Construir candidata limpia, probar flujo menú→entrenamiento/sala→partida→
   salida, persistencia de ajustes y ausencia de proceso/audio residual.
   Online local y paquete para amigos; WAN sigue pendiente del amigo otro día.

## Responsables activos

- Gameplay: alcance y contactos coherentes con rig, fallos de movimiento/control.
- Humanos: únicamente clips funcionales y datos de hombro/longitudes verificables.
- Presentación/Audio: integración de clips, un único reloj y mezcla/ciclo de sonido.
- Mosquitos: estabilidad física de caída; sin rediseño del modelo.
- UI: legibilidad/estados y ajustes reales, sin rediseño de pantallas.
- Revisores: comprobar fallos funcionales con evidencia de la candidata, separar
  limitaciones del banco, defectos de publicación y mejoras visuales futuras.
- Director: integración, nativos, contratos online, compilación/paquete y cierre.

## Se posterga a renovación visual

Siluetas y detalle de nuevos modelos, variantes/cosméticos adicionales,
materiales, nueva composición artística de habitaciones/mapas y pulido estético
de párpados. No se posterga un artefacto que impida ver, interactuar o reconocer
un impacto; esos son defectos de funcionamiento en la versión actual.

La candidata anterior e9d15e7 no contiene estas correcciones y no debe
redistribuirse como solucionada. Estado individual de los nueve reportes:
`docs/unity/qa/USER-GAMEPLAY-CORRECTIONS-20260913.md`.
