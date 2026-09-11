# Resultados parciales de revisión funcional

Fecha: 2026-09-11. Motor: Godot 4.5.2 oficial. Renderer: headless. Este bloque
no incluye UI nativa, galería visual, movimiento con `ActorView`, benchmark,
EOS real ni WAN.

| Ejecución | Exit | Resultado |
|---|---:|---|
| Import completo | 0 | 18,2 s |
| `procedural09_test.gd`, primera corrida | 1 | 49/53; cuatro fallos de metadata de descansos |
| `procedural09_test.gd`, después de `a8dd799` | 0 | 53/53 |
| `map_tasks09_test.gd` | 0 | 88/88 |
| `contact_orientation06_test.gd` | 0 | 469/469 |
| `network_privacy_audit_test.gd` | 0 | 48/48 |
| `review091_online_cosmetics_contract.gd`, primer intento | 1 | error de parse del fixture, sin runtime |
| `review091_online_cosmetics_contract.gd`, repetición | 0 | 27/27; ENet loopback, EOS/relay/NAT/WAN falsos |
| `review091_house_contract.gd`, primer intento | 1 | error de parse del fixture, sin geometría evaluada |
| `review091_house_contract.gd`, corpus | 1 | 5345 checks, 205 hits conservadores de hoja abierta |
| `review091_route_probe.gd` | 0 | dos hits aislados; ambos completan físicamente |

El probe de semilla 1 siguió desde `(-5.8,0,-9.225)` al pickup `racket`.
`ray_doors` marcó `room-07` en el último tramo; la autoridad llegó a 0,0957 m
del destino en 321 ticks, sin estancarse. En semilla 2 siguió desde
`(5.2,0,-0.05)` a `swatter`; el rayo marcó `room-06` y la autoridad llegó a
0,0511 m en 220 ticks. Por eso el fixture cambió de “línea inflada libre” a
“si la línea toca una hoja, la autoridad debe completar físicamente”.

Artefactos versionados: `review091-procedural-results.json`,
`review091-map-tasks-results.json`, `review091-online-cosmetics-results.json`,
`review091-house-results-open-leaf-pre-fix.json` y
`review091-route-probe-results.json`. Los logs locales usan el prefijo
`review091-` y conservan por separado los intentos fallidos y las repeticiones.

## Cierre de los gates prioritarios

El candidato de este bloque incorpora la corrección de movimiento de Worker 1
como `15586a2` y la corrección final de entorno como `3de62da` en la rama QA.

El primer intento monolítico de `review091_house_contract.gd` alcanzó el corte
duro de 55,051 s y fue terminado con exit 124, sin proceso huérfano. Como el
fixture escribe el JSON al finalizar, ese intento no produjo informe parcial.
El soporte de shards quedó en `cb481b8`; las once semillas se ejecutaron luego
por separado, todas con exit 0 y en menos de 14,5 s. El consolidado acredita
5591/5591 checks, once firmas estructurales únicas y 413 rutas humanas seguidas
mediante autoridad real. No hubo fallos de rutas mosquito. El probe lateral de
colisión, muestreado cada 0,025 m, informó mínimos de 3,30 m en pasillo, 11,97 m
de vano transversal abierto en los extremos de escalera, 2,85 m sobre la
escalera y 1,93 m con la puerta abierta. Estos dos últimos son estimaciones del
muestreo: la metadata generada define exactamente 2,80 m de ancho de escalera y
2,00 m de portal. Los 11,97 m no representan la profundidad del descanso; el
contrato de layout separado acredita al menos 2,13 m de profundidad libre.

`review091_motion_combat_contract.gd` terminó 150/150 en 4,233 s con renderer
nativo OpenGL 3.3 Compatibility sobre NVIDIA RTX 3060 Ti. La entrada a
`bitten` desplazó la raíz 0,069934 m frente al límite 0,088333 m; los errores de
adhesión y pose fueron 0, y la defensa produjo una sola transición. El error
angular final del torso fue menor que 0,00000181 rad a 30, 60 y 120 Hz.

Dos repeticiones previas descubrieron falsos rojos en el propio escenario: la
primera exigía recentrado mientras la vista aún se movía; la segunda contaba el
segundo intencional de pausa como congelamiento. Las correcciones están en
`254d39c` y `ab81c6a`, y ambos JSON previos se conservan. El log de la corrida
final no contiene `SCRIPT ERROR`, `ERROR:` ni `WARNING:`. Este bloque no ejecutó
preview, corpus de 1000 semillas, benchmark, EOS/relay/NAT ni WAN.
