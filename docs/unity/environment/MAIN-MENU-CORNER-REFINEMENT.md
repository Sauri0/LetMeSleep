# Rincón del menú — propuesta y fuente revisable

2026-09-12, Elementos. Responde al rechazo de Branko del video conservado en `N:/LetMeSleep/Validation/LivingMenu-20260912/UserVideo/contact-sheet.png`. Revisadas también referencias EnvironmentQuality-20260912/01.png, 03.png y 05.png. Estado: fuente preparada; sin nuevo render ni aprobación visual.

## Lectura y propuesta

En el recorte del usuario predominan una pared gris vacía, el bloque rojo del sofá y un farol rectangular cortado por arriba. El asiento no presenta estructura y el textil decorativo se lee como una hoja rígida. La referencia 03 ofrece una guía concreta: madera con uniones legibles, tapicería con volumen, una imagen enmarcada y luz cálida localizada dentro de una habitación ordenada.

Se concentra el cambio en el sofá derecho, una lámina nocturna sobre él y el aplique contiguo. No se añaden muebles independientes, libros dispersos, vegetación ni decoración repetida. El resto del lobby conserva su montaje. La cámara, personajes, UI y trayectoria no se editan.

## Fuente en AlfaLobbyDressing

- **Sofá derecho:** el Mesh del asiento y su transformación permanecen exactamente iguales a Kit_Sofa; su material pasa a terracota más clara. Se conservan el bisel original, plano Y .575 y todos los datos usados por la pose humana. Se reemplazan sólo las otras geometrías visibles: cuatro patas de 12 × 12 cm y 20 cm de alto, bastidor con faldones de madera, tablero bajo asiento, postes y largueros de brazos y respaldo. Dos almohadones de respaldo de .91 × .57 × .16 m descansan a Y .575; brazos con almohadillas de .20 × .16 × .85 m. Se usan lofts y normales textiles del kit Quality, sin tocar sus materiales globales.
- **Textiles del extremo:** manta doblada de .40 × .60 m con 20 mm de volumen, una hoja plegada superior y borde cosido; cojín de .36 × .30 × .14 m con base comprimida, inclinación de 8° y giro de -8°. Se mantienen los centros X 2.68 y 3.85, liberando la zona del humano. El contacto inferior sigue en Y .575.
- **Única lámina:** paisaje nocturno original de luna, laderas y río, sin texto. Marco de madera de 1.26 × .85 m, centro (3.10, 2.10, 5.97), parte posterior contra pared Z 6.00. Planos separados para evitar parpadeo. El borde inferior queda a Y 1.675; el próximo encuadre debe confirmar que no forma una tangencia con el gorro/cabeza.
- **Aplique derecho:** conserva el root Lantern_4p8 en (4.8, 2.45, 5.74). Base de madera redondeada, brazo corto, portabombilla torneado y pantalla textil hueca de 12 lados: radio inferior .22 m, superior .14 m, altura .29 m, dobladillos de 12 mm. Bulbo visible pequeño. Se conservan todos los colliders originales del farol.

No se destruyen, añaden o desplazan colliders del lobby: permanecen los proxies existentes, también bajo las piezas visuales reemplazadas. Las patas son geometría visual apoyada en suelo; no se añaden superficies independientes de apoyo de mosquito. El generador contrasta cantidad, identidad y bounds de todos los colliders antes/después. Los contratos de asiento, pies, spawns y recorrido mantienen sus gates anteriores.

## Paleta y luz coordinada

| Material exclusivo del menú | Base RGB | Uso |
|---|---|---|
| Menu_Upholstery | (.40, .18, .13) | Tapicería terracota |
| Menu_Seam | (.28, .105, .07) | Costuras |
| Menu_Linen | (.66, .56, .40) | Manta y dobladillos |
| Menu_Accent | (.14, .24, .29) | Cojín y paisaje |
| Menu_Shade | (.74, .64, .48) | Pantalla |

Menu_Shade tiene emisión tenue (.08, .04, .012), con BakedEmissive y _EMISSION. Pantalla y bulbo no proyectan sombras. Se reutiliza Lobby_LanternGlow sólo en el bulbo; los valores globales de Quality, pared y luces no cambian. El cuadro usa tres tonos exclusivos adicionales de cielo, laderas y papel.

Presentación comunicó candidato bd3d4ca: key cálida Spot en MenuWarmLight (4.6, 1.75, 4.9), RGB (1, .76, .53), intensidad 2.2, rango 4.3, cono 100°/70°; relleno Point en MenuFillLight con RGB (.58, .72, 1), intensidad .45 y rango 3.8. Esos valores son responsabilidad de Presentación y no se modifican aquí. **La key baja no coincide con el aplique alto.** Director decidirá esa relación mediante A/B; esta entrega no presenta el bulbo decorativo como origen físico ya validado de toda la luz del rincón.

## Validación y siguiente evidencia

Preparados gates para conservar el Mesh de asiento después de serialización, pies visuales al suelo, respaldos a altura de asiento, marco contra pared y emisión persistente del aplique. Se mantienen las nueve muestras del contacto humano sobre la geometría real, despeje de textiles y pantuflas, spawns y envolventes estáticas de la trayectoria.

Verificación propia: compilación C# offline contra Unity 6000.3.24f1 y diff-check. No se abrió Unity ni Blender. Director debe ejecutar BuildAlfaMaps, comprobar los gates, conservar la secuencia humana vigente y capturar encuadre equivalente al video a 720/1080. Revisar volumen del sofá, legibilidad de madera/tela, relación cuadro/cabeza, recorte del aplique y coherencia de la luz cálida. Después revisar animación completa, apoyos, barrido y recorrido. El pedido de calidad sigue abierto hasta comparar el resultado real con las referencias.
