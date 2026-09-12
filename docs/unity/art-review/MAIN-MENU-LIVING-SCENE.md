# Menú principal vivo — requisito de Branko

Fecha: 2026-09-12. Parte de la recuperación de alfa. Primera implementación integrada y rechazada visualmente por el usuario; corrección y validación pendientes.

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

## Corrección obligatoria tras el video del usuario

El usuario señala que el mosquito sólo se desplaza y no mueve las alas perceptiblemente. Traslación de Root no acredita vuelo animado. Deben verse aleteo y movimiento secundario coherentes a velocidad normal, tanto en menú como en partida. Investigar clip, bindings y muestreo antes de atribuir una causa.

El humano sigue continuamente al mosquito con pupilas y/o cabeza, dentro de límites anatómicos. Cuando el mosquito se aproxima a la zona de alcance, el humano anticipa e intenta golpearlo y vuelve naturalmente al reposo. La reacción depende del acercamiento real; un gesto periódico desconectado del recorrido no cumple. Conservar agarre, apoyo de pelvis y pies, y evitar atravesamientos. Ambos actores usan los personajes y personalizaciones guardadas del usuario.

Movimiento de pupilas y parpadeo se incorporan a humanos y mosquitos en TODO el juego, no sólo a estos clones del menú. Autores de cada especie definen geometría/controles y compatibilidad de variantes; Presentación coordina controlador facial reutilizable; Gameplay integra estados y avatares remotos; Director integra contratos. Evitar controladores que escriban simultáneamente los mismos huesos y evitar torcer o aplastar el ojo completo como sustituto de una pupila y párpado legibles.

Aceptación adicional: secuencia en tiempo real muestra alas en movimiento, seguimiento ocular/cervical y aproximación→anticipación→golpe→retorno completos; comprobar varias aproximaciones y variantes guardadas, parpadeo con mirada y acciones, regreso desde personalización/lobby y estados de partida. Verificar también movimiento reducido. La fluidez se evalúa sin lectura GPU y escritura continua de PNG en el hilo principal.

Evidencia rechazada: N:/LetMeSleep/Validation/LivingMenu-20260912/UserVideo/REVIEW.md. La grabación coincide con captura diagnóstica pesada ya detenida; causa de tirones pendiente de comparación limpia. Este límite no invalida los defectos artísticos observados.
