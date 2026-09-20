# Cámara y horizonte del mosquito — A20

La decisión del usuario permite mirar alrededor sin girar el mosquito cuando
está quieto, y vuelve a vincular el horizonte con el cuerpo al moverse.

## Implementación

La autoridad conserva la orientación corporal separada de la mira. En vuelo,
el cuerpo cambia su rumbo con movimiento y mantiene horizonte vertical; sobre
una superficie, su normal y tangente se transportan con el soporte. La mirada
quieta sigue siendo válida para apuntar y picar, pero no gira esa tangente.
En picadura, la orientación corporal sigue la normal del contacto. Desprenderse
y entrar en recuperación restauran un horizonte de vuelo vertical.

`MosquitoLookFrame` conserva la orientación de cámara entre fotogramas. Transporta
el cambio de normal del soporte, sin realimentar el rumbo del cuerpo. Limita la
inclinación a89° respecto del soporte para evitar atravesar un polo y producir
un giro brusco. En una pared esto puede corresponder a90° en coordenadas del
mundo; autoridad y codec aceptan ese valor para el mosquito. El límite del
humano no cambia. Se conservan los campos existentes del protocolo.

La cámara principal y la incorporada en Runtime usan el mismo frame. La cámara
incorporada resuelve primero orientación y después posición, para no mezclar
direcciones anteriores y posteriores a un cambio de soporte.

## Evidencia y límites

- `horizon-bots-authority-native-01.xml`:71/71PASS; incluye los nueve casos de
  horizonte, recuperación Tasks desde techo sin nuevo movimiento y límite
  inferior de mirada. Verifica también bots, modos, picadura, puertas y codec.
- `vfx-perception-native-02.xml`:18/18PASS tras detener el ParticleSystem antes
  de configurar su duración. Esta ventana cierra el fallo de partículas inferior.
- `horizon-vfx-world-native-01.xml`:11/11 del helper y5/5 de colisión/ocultación
  de cámara existentes pasan. La misma ventana completa fue25/27: dos VFX
  fallaron por configurar duración de partículas mientras estaban emitiendo.
  No informar la ventana completa como aprobada.
- CPU `N:/LetMeSleep/Validation/V020/MosquitoHorizon/Checks.csproj`: siete casos
  iniciales de orientación corporal, vuelo, suelo/pared/techo, desprendimiento,
  contacto de picadura cambiante y límite de mirada pasan.
- CPU `N:/LetMeSleep/Validation/V020/EquipmentNetwork/HorizonChecks.csproj`:
  47/47, incluyendo ida/vuelta de mirada vertical y rechazo por encima de90°.

La revisión independiente detectó y orientó la corrección de salto de roll,
realimentación de inclinación en vuelo y recuperación con horizonte invertido.
Queda observar cámara en una partida real con los personajes nuevos. Estas pruebas
no aceptan por sí solas la apariencia ni el comportamiento completo en red.
