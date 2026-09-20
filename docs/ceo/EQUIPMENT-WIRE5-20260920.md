# Integración de inventario y protocolo 5

La definición vigente usa tres slots más manos, estamina humana y lanzamiento
de pantufla con carga. El dominio conserva recursos por pickup; la UI de
inventario y los efectos de raqueta/aerosol todavía están en implementación.

## Cambios integrados por CEO

- GameplayWireCodec 5 transmite PrimaryHeld, comandos con revisión de inventario
  y pickup, inventario/carga/oferta privados, y fase/velocidad/recursos públicos
  del objeto. Acepta hasta tres objetos guardados por humano, incluso usando manos.
- El hash canónico incluye HumanEquipmentProfile. Begin 5 y protocolo de sala
  `lms-unity-020-4` se coordinan con la entrega de ingreso tardío del agente red.
- Cancelación fiable al perder foco/bloquear controles; liberar el botón envía
  ReleaseThrow explícito. Un input neutral adelantado nunca implica lanzar.
  Números 1–3 seleccionan slots, 0 manos, rueda recorre los cuatro estados.
  E interactúa y mantiene trabajo humano; G suelta el objeto seleccionado.
- El mundo prepara depósitos a 0,6/0,8/1 m medidos al centro del volumen del
  objeto. Comprueba trayecto, primer soporte, pendiente, volumen final y zonas
  de caída configuradas. No busca debajo de un primer soporte inválido.
- El volumen se guarda antes de ocultar el collider del objeto guardado.
  Un lanzamiento comprueba origen y salida; el proyectil usa barrido de caja.
  Un solapamiento inicial se señala explícitamente para recuperación por el
  dominio, sin afirmar que la posición penetrada sea un apoyo válido.

## Evidencia

- `Validation/V020/equipment-latejoin-native-01.xml`: 65/65 PASS, cero omitidos.
  Incluye núcleo/equipo de autoridad, codecs de equipo/modos, preparación de
  ingreso tardío y compatibilidad. El filtro no constituye toda la suite de salas.
- `Validation/V020/equipment-world-native-01.xml`: 13/13 PASS, cero omitidos;
  seis casos de volumen oculto, depósito, primera obstrucción, salida bloqueada,
  pared fina y solapamiento inicial, más siete casos de mundo/tareas.
- Compilación offline Bootstrap y dependencias: cero advertencias/errores.
- Revisión independiente de red detectó ThrowerActorId huérfano/no humano.
  Se agregó validación cruzada y dos negativos. Harness CPU posterior: 41/41;
  esos dos negativos esperan la siguiente ventana nativa.

Las pruebas no acreditan EOS/WAN, micrófonos reales, cinco mapas jugados ni
aprobación artística. Casa conserva catálogo sin instalar: diagnóstico motor
control PASS en 19 ticks y ruta entre habitaciones FAIL por atasco, pendiente
de corrección de tangencia del motor. No existe aún build final v0.2.0.
