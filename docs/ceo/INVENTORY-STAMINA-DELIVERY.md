# Inventario, estamina y carga — entrega de dominio

Primera etapa integrada en Gameplay; fuente de decisiones J01–J03,
J17–J22 y D07 de `definicion-v020/respuestas-20260920-065440/DECISIONES.md`.
No reemplaza el gate de mundo/UI/red/WAN ni acredita armas consumibles listas.

Estamina humana100, sprint16/s, recuperación12/28 por postura, salto10;
tres slots más manos, confirmación de reemplazo vinculada a revisiones,
depósito validado antes de transferir, retirada definitiva de todos los slots.
Reconexión conserva equipo y estamina; cancela carga/control sin invulnerabilidad.

Pantufla usa el mismo pickupId al lanzar y recuperar. Carga27ticks, auto45
con input fresco de hasta6ticks, potencia35–100% SmoothStep y coste8–15.
Neutral no lanza: congela potencia y espera ReleaseThrow/CancelThrow fiable;
cancelación gratis a30ticks, por desconexión/revisión/stale. Begin+Release
en el mismo lote neutral produce mínimo35%, sin exigir que el canal unreliable
haya adelantado el paquete held. Begin no renueva una carga activa.
Cambiar slot cancela; estamina insuficiente cancela sin degradar potencia.

El host simula trayectoria y consume el primer impacto una vez; mundo consulta
volumen y devuelve origen seguro. StartedOverlapping aborta vuelo y recupera
en pose authored conservando ID/recursos, sin daño nuevo. Pared/actor mantienen
caída; sólo normal ascendente de **mundo estático** permite reposar. Máximo300
ticks antes de recuperación authored, sin rellenar consumibles. Limitación
acordada actual: el barrido ignora al lanzador durante todo el vuelo.

DTO mantienen overloads anteriores; inventario/estamina/carga/oferta son
privados. ToolPickupSnapshot añade fase/velocidad/lanzador/recursos/impacto;
el ledger público conserva identidad para late join y los slots no son parte
de ActorSnapshot. EquipmentProfileHash se agrega al hash compuesto; el CEO
coordina versionado de red, adapters Unity y Runtime.

Evidencia CPU en `N:/Validation/V020/InventoryDraft`: núcleo15 pruebas;
autoridad16 después del ajuste de actor-cabeza. Regresión combinada79/79 PASS
(modos/reconexión, picaduras y puertas incluidos). El CPU usa fuentes centrales
reales y capacidades de mundo simuladas en tests: no sustituye PhysX.
El CEO informó gate nativo anterior65/65 EditMode y13/13 mundo (incluye6equipo),
antes del último ajuste de reposo sobre actor; dicho ajuste tiene regresión CPU
pero queda pendiente de siguiente gate nativo.

Pendiente de la etapa siguiente: pulsos/cargas raqueta, aerosol/nube/combustible,
balance físico/temporal matamoscas, perfil completo de esos parámetros y
validación nativa de efectos. No declarar esas armas terminadas por existir
sus IDs o recursos iniciales en el ledger.

## Etapa de efectos — estado CPU posterior

Raqueta y aerosol ya tienen lógica autoritativa en
`GameplayAuthority.ToolEffects.cs`. Raqueta consume una de5cargas por pulso,
registra EndHalfTick=2*inicio+21 para0,35s exactos y cooldown36ticks. Aerosol
consume una de120unidades por tick de emisión; una nube por pickup sigue la
boquilla al emitir y permanece en la última pose72medios ticks (1,2s) al parar.
Cambiar, depositar o volver a recoger no rellena recursos ni reinicia cooldown.
La geometría se consulta mediante IGameplayToolEffectWorld; el dominio aplica
KnockDown existente una vez por actor/efecto y respeta protección/vidas.

Concreciones CEO, no respuestas literales adicionales: raqueta1,05m y cono de
semiángulo35°, aerosol2m/30°, una nube por pickup, efecto KnockDown común.
La desconexión cancela emisión pero deja expirar aerosol emitido; la retirada
definitiva del actor limpia todos sus efectos para no publicar una fuente
ausente. Fin de ronda limpia efectos. CancelThrow fiable también limpia held
para evitar un tick adicional de emisión si adelanta al input neutral.

Matamoscas usa alcance base de mano0,72m multiplicado una sola vez por1,35
(0,972m) y tiempos de golpe multiplicados por1,25; la presentación conserva
progreso normalizado. El perfil incluye el balance efectivo de lanzamiento,
depósito, gravedad, retención, recuperación, armas, cono/rango y duraciones;
EquipmentProfileHash cambia automáticamente para impedir mezcla de perfiles.

ToolPickupSnapshot añade CooldownUntilTick manteniendo overloads previos;
GameSessionState añade ToolEffects con lista copiada e independiente. El
snapshot de efecto contiene EffectId/PickupId/SourceActorId/Kind/Origin/Forward/
StartTick/EndHalfTick. La duración de aerosol puede extenderse durante emisión:
el límite relevante es EndHalfTick≤2*HostTick+72, no72desde el inicio original.

Regresión CPU posterior: **92/92 PASS** (44 pruebas de equipo/núcleo y48 de
modos/reconexión/picaduras/puertas). Incluye recuperación real con protección,
5pulsos hasta agotar,4s de aerosol y cola1,2s, cancelación entre canales,
recursos/cooldown persistentes, fin de Supervivencia y retirada de una fuente
con otra humana aún en ronda. Gate nativo de esta etapa pendiente del CEO;
sin afirmación de VFX, audio, ergonomía visual o WAN terminados.

Gate nativo posterior comunicado por CEO: `effects-latejoin01` **88/88 PASS**;
`effects-motor-hud01` **16/17 PASS**, con11/11 mundo,2/2HUD y3/4motor. El fallo
restante corresponde al comparador de escalón que asumía desplazamiento legado
cero y permanece bajo revisión del CEO; no se oculta ni atribuye a aceptación
de locomoción. Los módulos de dominio de efectos se entregan con metas y
pruebas; todavía no se declara QA visual/sonoro completo ni WAN.
