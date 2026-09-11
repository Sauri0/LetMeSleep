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
