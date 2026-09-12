# Menú principal vivo — requisito de Branko

Fecha: 2026-09-12. Parte de la recuperación de alfa. Pendiente de implementación y validación; no es una entrega aprobada.

Branko pide que el humano y el mosquito del menú reflejen la personalización guardada y habiten la habitación: humano sentado con una raqueta u objeto equivalente, animación y mosquito volando. También rechaza la iluminación y estética actuales. Este pedido sustituye la exposición de ambos personajes quietos en el centro.

## Escena que debemos entregar

- Humano sentado de manera creíble en un sofá/sillón real: pelvis apoyada, piernas relajadas, pantuflas apoyadas y mano sujetando el mango. Respira, cambia levemente el peso, mira al mosquito y realiza ocasionalmente un intento cansado de espantarlo; regresa a reposo sin saltos. Pijama, piel y demás opciones disponibles proceden de la personalización guardada.
- Mosquito recorre una zona acotada de la habitación con curvas suaves, aleteo, giros e inclinación coherentes y breves cambios de velocidad. Pasa cerca del humano en un momento coordinado. Debe permanecer visible y no atravesar paredes, muebles ni el personaje, ni dominar la escena por su escala.
- Raqueta/matamoscas como objeto de presentación. Reutilizar el objeto de producción adecuado si cumple el aspecto; no crear una mecánica o inventario nuevo por esta escena.
- Composición doméstica nocturna: luz cálida localizada junto al asiento, relleno frío suave y contacto visible. Rostros, manos, ropa y mosquito legibles, sin zonas negras dominantes, materiales magenta ni lámparas quemadas. Fondo y botones diseñados juntos; mantener una zona tranquila detrás del texto.
- Movimiento discreto y sin repetición nerviosa. Control de movimiento reducido disponible/coordinado con UI: sostener una pose sentada y aleteo mínimo si corresponde, sin bloquear navegación. Los sonidos respetan mezcla/volúmenes y no quedan activos al salir.

## Propiedad y dependencias

| Responsable | Entrega acotada |
|---|---|
| Presentación y Audio | Componente propio de escena de menú bajo Presentation/Runtime: recorrido, secuencia, ciclo de vida, luces de presentación y contrato de enlace. No editar Bootstrap ni UI. Entregar API Configure/activar/desactivar y dependencias reales. No mover actores de partida. |
| Humanos | Clips de menú separados del contrato de IDs de locomoción/combate: reposo sentado, mirar y espantar/regresar; agarre real. Coordinar asiento/altura con Elementos y nombres con Presentación. No modificar clips de mosquito. |
| Elementos | Posición y superficie real de asiento, puntos de apoyo y objeto con socket/mango. Cambios propios de montaje en AlfaLobbyDressing si necesarios, sin invadir circulación/spawns del lobby. Proporcionar anchors, no desplazar cámara unilateralmente. |
| UI | Composición menú + cámara propuesta en 720/1080, zonas reservadas y contraste. Conservar callbacks y personalización; coordinar movimiento reducido sin controles vacíos. |
| Director | Integrar Bootstrap/CreateMenuCharacter, cámara y visibilidad con lobby/personalizador/partida; conservar ApplyLiveAppearance. Decidir contrato final antes de compartir archivos. |
| Revisores visual/animaciones/funcional | Revisar secuencia real y apoyos/agarre, encuadre/iluminación, personalización guardada y salida/retorno sin duplicados ni audio residual. |

## Punto de partida comprobado

AlfaApplication.LoadMap(false) crea MenuCharacterDisplay, usa HumanMenuStage/MosquitoMenuStage y amplía el mosquito decorativo cuatro veces. CreateMenuCharacter reproduce un motion pero no hace una escena coordinada. OnUiScreenChanged oculta/restaura el root en personalización; el lobby también controla su visibilidad. ApplyLiveAppearance ya aplica los colores guardados a los CharacterView del menú. AlfaLobbyDressing posee cámara/stages y sofás traseros. Conservar estas responsabilidades; no parchear animación haciendo mutaciones permanentes en los rigs de gameplay.

## Cierre de esta mejora

Una secuencia completa real en Unity/build, a 720p y 1080p, con inicio/reposo/reacción/regreso; apoyos y agarre visibles, mosquito sin atravesamientos ni recortes y botones utilizables. Cambiar personalización, guardar, volver y verificar ambos personajes; entrar/salir de lobby, entrenamiento y personalizador sin duplicación ni restauración incorrecta. Revisar luz y movimiento contra los bocetos. Fotos sueltas, scripts compilados o trayectoria calculada no cierran la mejora.

Primer lote: una escena de menú completa con los personajes base y cosméticos alfa existentes; no ampliar el catálogo. Un único turno Unity/Blender concedido por Director; los autores preparan archivos propios sin procesos nativos hasta recibirlo.
