# Protocolo de reglas de tareas y estados privados

Gameplay schema 4 y Begin 4 transportan recuperación del plazo, gracia y
decaimiento; RoomSession anuncia `lms-unity-020-2`. Se rechazan paquetes del
schema anterior. Los tres campos participan del balance compartido.

La revisión independiente detectó un predicado invertido en recepción de estado
privado: rechazaba tareas conocidas y estados sin asignación. Envío y recepción
comparten ahora la validación de ronda, actor y objetivo; el receptor conserva
su comprobación adicional de que el actor pertenece al jugador local.

Revisión: modos_dominio, gpt-6-astra/high. Validación CPU: 22 casos sin fallos.
Unity6000.3.24f1: `N:/LetMeSleep/Validation/V020/profile4-voice-native-01.xml`,
72 PASS, 0 FAIL, 0 omitidos. Incluye reglas de modos, codec, preparación local,
propiedad de herramientas y núcleo de voz. No certifica EOS, escucha ni WAN.

Cobertura restante: TryBegin completo con paquetes truncados y hash alterado,
además de prueba de HUD privado en dos clientes. Esta entrega no afirma
que el catálogo final de tareas ni la versión v0.2.0 estén terminados.
