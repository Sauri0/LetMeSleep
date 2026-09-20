# Efectos visuales de herramientas — entrega v0.2.0

Fecha: 2026-09-20

## Alcance entregado

- `GameplayVfxPresenter` consume `ToolEffectSnapshot` aceptados por `GameplayRuntime` y conserva un visual por `EffectId`.
- La raqueta eléctrica presenta un pulso visible mediante `LineRenderer` durante los 21 semiticks autoritativos (0,35 s).
- El aerosol presenta una nube mediante `ParticleSystem` durante los 72 semiticks autoritativos (1,2 s); una actualización del mismo efecto mueve el emisor y conserva su identidad.
- Los visuales se eliminan cuando el snapshot deja de incluirlos, cambia la identidad de sesión/ronda o alcanza `EndHalfTick`, incluso si no llega otro snapshot.
- Origen y dirección se actualizan desde el snapshot. Un paquete retrasado que ya alcanzó su vencimiento no puede recrear el efecto.
- Este módulo no crea `AudioSource` ni inventa señales de sonido.

La percepción de herramientas para bots también quedó cubierta en el mundo Unity: devuelve el punto visible de `Collider.ClosestPoint`, y rechaza paredes, otro objeto como primera obstrucción, revisiones antiguas y herramientas que ya están en inventario.

## Evidencia

- Compilación offline de `LetMeSleep.Presentation.Gameplay` y `LetMeSleep.Tests.PlayMode`: 0 errores.
- Gate nativo VFX/percepción: 18/18 PASS, 0 skip.
- XML: `N:/LetMeSleep/Validation/V020/vfx-perception-native-02.xml`.
- Log: `N:/LetMeSleep/Validation/V020/vfx-perception-native-02.log`.
- Cobertura nueva: 3 casos de VFX y 4 casos de percepción; el gate incluye además 11 casos previos del mundo de equipamiento.

Los casos VFX comprueban creación de componentes, actualización por identidad, limpieza por ausencia/cambio de ronda/vencimiento, duración mínima y máxima, rechazo de snapshots retrasados y ausencia de audio.

## Límites de la evidencia

El gate nativo valida comportamiento y ciclo de vida con snapshots sintéticos válidos. No certifica la apariencia final dentro de una partida, una captura renderizada del efecto, mezcla de audio ni un recorrido multijugador completo. Los colores, densidad y forma procedurales son una presentación funcional que todavía admite revisión estética en juego.
