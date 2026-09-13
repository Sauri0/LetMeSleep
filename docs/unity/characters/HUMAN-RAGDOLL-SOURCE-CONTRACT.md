# Contrato fuente humano para caída/desmayo físico

Estado: propuesta de integración solicitada por Director, no implementación física ni aceptación de una caída. Fuente joints3 `98f238c`; FBX humano SHA256 `9e85748b5ec38f57791bb7377dbe43bc6328792986837c7fdb260371f6a8d20b`. Unity humano Generic, escala1, Root in-place. El modelo mantiene65 huesos y todos sus nombres/sockets.

## Cuerpos y jerarquía

Propuesta inicial de17 cuerpos para que columna, cuello, codos, muñecas, rodillas y tobillos puedan articularse. La simulación debe derivar posiciones, ejes y longitudes de la instancia Unity, no copiar las cápsulas de gameplay.

| Rigidbody sobre hueso | Cuerpo conectado | Segmento fuente en metros (origen→extremo) | Fracción de masa propuesta |
|---|---|---|---|
| Hips | ninguno; raíz física | `(0,0,.75)→(0,0,.88)` |18%|
| Spine | Hips | `(0,0,.88)→(0,0,1.05)` |10%|
| Chest | Spine | `(0,0,1.05)→(0,0,1.23)` |24%|
| Neck | Chest | `(0,0,1.23)→(0,0,1.34)` |2%|
| Head | Neck | `(0,0,1.34)→(0,0,1.70)` |8%|
| UpperArm.L/R | Chest, a través de Shoulder.L/R | `(s*.25,0,1.17)→(s*.52,0,1.16)` |3% cada uno|
| LowerArm.L/R | UpperArm correspondiente | `(s*.52,0,1.16)→(s*.75,0,1.15)` |2% cada uno|
| Hand.L/R | LowerArm correspondiente | `(s*.75,0,1.15)→(s*.837,0,1.15)` |.5% cada una|
| UpperLeg.L/R | Hips | `(s*.125,0,.78)→(s*.125,0,.44)` |8% cada una|
| LowerLeg.L/R | UpperLeg correspondiente | `(s*.125,0,.44)→(s*.125,0,.12)` |4.5% cada una|
| Foot.L/R | LowerLeg correspondiente | `(s*.125,0,.12)→(s*.125,-.12,.07)` |1% cada uno|

`s=+1` para L y `-1` para R. Fuente: Z arriba, -Y frente. Unity aplica conversión y orientación del prefab; no pegar estos vectores sin convertir. Las fracciones suman100% de una masa total elegida por Gameplay; son valores iniciales de ajuste, no mediciones biomecánicas.

Shoulder.L/R quedan bajo Chest, sin cuerpo separado al inicio; mantienen su pose local de clavícula. Root no recibe Rigidbody. Toe, los30 huesos de dedos, Eye, Brow, Jaw y sockets conservan pose local y siguen sus padres. No crear cuerpos extra para anclajes de cámara/agarre ni para el gorro. Si se libera el matamoscas al caer, Elementos/Gameplay deciden y ejecutan esa transición; el modelo no lo suelta por sí mismo.

## Colliders y juntas

- Ajustar cápsulas de brazos/piernas al volumen de piel o prenda interior de cada segmento; manos y pies admiten caja redondeada/forma simple. Torso/pelvis necesitan volumen obtenido de los vértices en bind. Gorro, ribetes, bolsillos y dedos no definen el volumen principal. Los radios/centros finales quedan pendientes de medición en Unity.
- Anchors en el origen real del hueso hijo y el punto equivalente del cuerpo conectado; comprobar que ambos coincidan en mundo antes de activar física. No mover Hand para cerrar una distancia: el brazo fuente mide `.270185+.230217 m`, mientras el proxy usa `.28+.28 m`.
- Codos/rodillas necesitan flexión dominante y mínima hiperextensión/torsión lateral. Muñecas/tobillos permiten flexión y desvío limitados. Hombros/caderas permiten giro en varios ejes; columna/cuello requieren límites más contenidos. Director/Gameplay eligen el tipo de joint y sus límites iniciales, luego comprueban extremos contra la malla real. No hay ejes Unity de bisagra certificados aún.
- Derivar eje longitudinal del par de huesos y plano de flexión de una pose válida (Idle/Crouch), transformándolos al espacio local de cada joint. No usar un eje world fijo ni asumir que Blender y Unity conservan el mismo eje local de giro.
- Evitar que colliders de segmentos vecinos se expulsen en reposo; comparar solapamientos y contacto con suelo. Conservar colisión entre cuerpo y escenario. Los triggers de superficies de gameplay siguen bajo su dueño y no se vuelven masas físicas por accidente.

## Un escritor por estado y transición

| Estado | Dueño de huesos | Animator / IK / atención | Motor, contenedor visual y transición |
|---|---|---|---|
| Vivo | Animator + ajustes visuales coordinados | Animator anima; IK de brazo después; atención facial al final. | Motor autoritativo mueve actor y `ActorVisualBinding` coloca visual. Ragdoll kinematic, colliders físicos inactivos. |
| Entrada a caída/desmayo físico | Gameplay toma snapshot de la pose final visible | Cancelar Strike temporal/IK y restaurar offsets de atención; capturar luego una pose consistente y desactivar Animator. | Copiar mundo de los17 cuerpos y velocidad inicial desde estado vigente. Activar todos como una transición; no arrancar desde bind/T-pose ni desde Fall frame0. |
| Física activa | Rigidbody/joints | Animator no muestrea; IK de manos no rota; atención no corrige Head/Neck. Pupilas/blink pueden congelarse al inicio para no agregar otro escritor durante la primera integración. | El motor lógico puede seguir su protocolo, pero `SetWorldPose` no debe arrastrar la jerarquía de cuerpos cada LateUpdate. Separar temporalmente el contenedor físico en mundo o aplicar una estrategia explícita equivalente bajo un único dueño. |
| Reposo desmayado | Física o pose física congelada por Gameplay | Misma exclusión de escritores; Faint animado no sustituye la pose física asentada. | El cuerpo debe permanecer donde cayó, con apoyos del escenario real. La autoridad decide reposo, duración y recuperación. |
| Recuperación | Transición pose física→Animator | Volver cuerpos kinematic y retirar sus fuerzas; alinear recuperación y mezclar desde pose capturada. Habilitar IK/atención sólo después de fijar esa base. | Ubicar raíz/cápsula en sitio válido cercano a la pelvis asentada y reanclar visual conservando pose mundial. No teletransportar el cuerpo al Root previo ni permitir impulso por colliders aún dinámicos. |
| Vivo restaurado | Pipeline visual habitual | Una sola reactivación de Animator y ajustes, sin temporales de ataque anteriores. | Recuperar seguimiento de snapshots y controles normales. |

Detalle de entrada: si se decide conservar exactamente la última mirada visible, capturar esa pose final primero y luego impedir que `OnDisable/Restore` la cambie antes de asignar cuerpos. La secuencia concreta debe ser única y verificable; mezclar captura de un frame con restauración del siguiente causa un salto. El mismo criterio aplica al último IK de brazo y al agarre.

La lógica actual escribe `ActorVisualBinding.SetWorldPose` y `ApplyAuthoritativeHands` en LateUpdate; `VisualAttentionRig` es otro escritor posterior. Además `SelectMotion` reproduce Fall/Faint/Recover como animaciones completas. La integración física debe condicionar esas rutas al estado de ownership; añadir Rigidbodies sin hacerlo dejaría sistemas compitiendo por los mismos huesos.

## Responsabilidades y evidencia exigida

Modelador Humanos entrega nombres, continuidad/skin, poses fuente y adaptación de geometría si una prueba física identifica una articulación que colapsa. No añade un motor físico paralelo al export. Gameplay implementa y prueba simulación, estado, juntas/colliders, control del motor y transferencia de poses; Director define integración/autoridad online y edita bootstrap/prefabs bajo coordinación. Presentation respeta exclusión de escritores y cámara/visibilidad, Elementos coordina el objeto equipado, Revisión de Animación observa la caída y recuperación completas.

Para aceptar: caída desde quieto y corriendo, desmayo en pie/agachado, golpe con/sin herramienta, impacto lateral contra pared, pendiente/escalón y recuperación en espacio limitado. Medir continuidad de longitudes/anclajes, pose al transferir, apoyos/penetración, jitter en reposo y único escritor. Captura gráfica del episodio completo y observación del usuario pendientes. Política de simulación/replicación remota y manejo de obstrucción al levantarse son decisiones de Gameplay/Director que este contrato fuente no sustituye.
