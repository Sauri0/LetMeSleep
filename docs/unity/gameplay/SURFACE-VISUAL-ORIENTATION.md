# Orientación visual al posarse — 2026-09-12

Delta sobre central `38b88d65813c81e0a60fc29d28b641dc607fd5ca`, rama `codex/gameplay-surface-visual`. W2 transfirió la sección de pose de `ActorVisualBinding`; Director autorizó este ajuste.

`SetWorldPose` resuelve la normal mundial mediante `UnityGameplayWorld.ResolveSurface`, sin confundir `LocalNormal` con una dirección mundial. Para mosquitos en `ApproachingSurface`/`Surface`, el eje visual +Y apunta hacia afuera del apoyo y las patas -Y hacia él. El frente se obtiene proyectando el yaw corporal interpolado en el plano del apoyo. Cerca de una dirección normal se conserva la tangente anterior; sin historia válida se elige una tangente determinista. Los giros de rumbo se acotan a 12 radianes/s, incluso al invertir 180 grados. La aproximación y el retorno a vuelo suavizan la inclinación; una vez posado se alinea exactamente +Y con el apoyo.

La orientación se conserva en el binding visual, independiente del proxy físico. No modifica posición, autoridad, BodyRotation del snapshot, cámara, collider, Root del modelo ni anclaje de boca. Las llamadas repetidas dentro del mismo instante no consumen dos veces el tiempo de interpolación. Los estados de picadura conservan su pose corporal/anclaje preexistentes.

## Verificación

- 16 casos CPU: seis caras y plano oblicuo; dirección tangente/yaw y eje de apoyo; mirada normal sin historia; ruido a ambos lados de la normal; giro inverso acotado; igualdad del rumbo a 30/120 Hz en un instante intermedio y al converger; tiempo cero; entradas no finitas y normal inválida.
- Compilación externa C# 9/netstandard2.1 de helper y binding contra las DLL de Unity 6000.3.24f1 y las dependencias actuales: cero errores y advertencias.
- Evidencia local: `N:/LetMeSleep/Validation/SurfaceVisual-20260912/`, proyectos reproducibles, `checks.log` y `compile.log`.

No se abrió Editor ni Blender y no se ejecutó una prueba renderizada. Director debe integrar junto al modelo M1 (GroundContact -0.057 m) y comprobar piso/pared/techo, caminar, mirar hacia/contra apoyo y despegar. Medir también el posible salto de inclinación si la aproximación termina antes de completar su suavizado. La raíz física observada a 0.056 m del piso admite aproximadamente 1 mm de penetración nominal con ese modelo; el código no compensa la geometría.
