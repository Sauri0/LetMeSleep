# Integración de inventario y protocolo 5

La definición vigente usa tres slots más manos, estamina humana y lanzamiento
de pantufla con carga. El dominio conserva recursos por pickup; la UI de
inventario y los efectos de raqueta/aerosol avanzaron como se detalla abajo.

## Evidencia posterior: efectos

`effects-latejoin-native-01.xml`:88/88 PASS, cero omitidos. Incluye los
efectos de armas y sus recursos, cooldown, duración en medios ticks, codec,
inventario y entrada tardía. `effects-motor-hud-native-01.xml`:16/17 PASS;
los once casos físicos de equipo y los dos de textoHUD pasan. El fallo
corresponde a una expectativa incorrecta del comparador de escalón legado,
fuera del equipo. No declarar toda esa suite PASS.

El mundo comprueba alcance/conos, primera obstrucción, emisión bloqueada,
mosquito que toca la boquilla y puerta cerrada sobre una nube persistente.
El codec exige efecto vinculado a pickup del tipo correcto y humano existente,
con ID único, caducidad canónica y cooldown/combustible conservados.

La captura gráfica real `equipment-hud-visual-01.xml` falló por desbordamiento
del texto SwapOffer a720p. Falta corregir y repetir el layout. Los VFX de armas,
modelos visibles y pickups completos de los cinco mapas siguen pendientes.

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
  ambos también quedan incluidos en la ventana nativa posterior de88/88.

Las pruebas no acreditan EOS/WAN, micrófonos reales, cinco mapas jugados ni
aprobación artística. Casa conserva catálogo sin instalar: diagnóstico motor
control PASS en19 ticks y ruta entre habitaciones FAIL. La traza03 muestra
avance en tick31 y vuelta en32: oscilación de dirección, no inmovilidad del motor.
Se instrumentó la elección del controlador para aislar la causa. La modificación
de tangencia del motor no resolvió Casa y se evalúa por separado.
No existe aún build final v0.2.0.
