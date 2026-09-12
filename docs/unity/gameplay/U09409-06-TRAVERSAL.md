# U094-09/06: recorridos, aproximación al posado y cámara

**Gate parcial: U094-06 continúa bloqueado por cámara.** La ruta humana ensayada pasó; se reprodujo y corrigió un defecto de aproximación al posado. Al comprobar el posado nativo corregido apareció un solapamiento del volumen de cámara con el piso, entregado a W2. Se detuvieron las rutas restantes.

Entorno: Unity6000.3.24f1, Windows, D3D11, editor por lotes PID5800. Fuente inicial root `ec95269d08dde5c8515047ac7cab94ca0312f124`, candidata alfa.1 `f0e6b80fd067ea7f25768c21d1e2cf898a8c65d1`. No se publicaron artefactos ni se modificó Content o Presentation. Los recorridos parten de los spawns authored y usan PlayerInputCommand autenticado, movimiento y acciones normales del runtime; no se alteraron transforms de actores, sangre o temporizador.

## Ruta humana observada

Desde (6.06, .02, 1.2): pasillo bajo → HallWest0 → aproximación a escalera → tramo inferior → descanso y giro → tramo superior → UpperWestLanding → UpperHall. Luego vuelta por el mismo recorrido, descenso completo y salida por PatioDoor hasta el patio z14.91172, seguida de regreso al interior.

Los 18 puntos de control se alcanzaron, sin salto ni teletransporte, hasta tick517. Altura de apoyo observada: planta alta y3.000159 y planta baja y.000159. Es evidencia del corredor y de la escalera en ambos sentidos, más la puerta trasera y patio; no acredita todas las habitaciones ni un segundo acceso exterior.

## Defecto de aproximación al posado

Reproducido dos veces desde spawn mosquito mediante vuelo normal a la zona libre de Living, aproximadamente (3.587296, .230651, 1.298534), seguido de frenado y una pulsación F mirando hacia abajo. La consulta de adquisición `.25` devolvía piso válido SurfaceId10190, normal +Y, y el comando era aceptado sin rechazo.

En el primer tick, el mosquito avanzaba .021667 m hacia el piso, quedando a y.208985, pero volvía a Flying sin ancla. Seguía así 15 ticks después. La comprobación posterior usaba inmediatamente el alcance de mantenimiento `.12`, más el margen de radio `.055`: `.175` no alcanzaba el piso desde `.208985`. El contraste nativo en esa misma posición confirmó adquisición=true y mantenimiento=false. No faltaba collider ni GameplaySurface.

Parche W1 **`a8015a9`**, integrado con autorización en root como **`b3cf37f9639722d0951626521defac621074d6a4`**:

- ApproachingSurface conserva el alcance inicial `.25` mientras se acerca.
- Surface ya establecido mantiene el alcance corto `.12`.
- La aproximación exige que el primer soporte alcanzado conserve SurfaceId; una superficie interpuesta cancela la aproximación. El motor sigue resolviendo colisiones físicas.

Regresión dirigida en `validation/SurfaceApproachChecks.cs`: seis normales de plano desde .23 m, pérdida del soporte durante aproximación y estando posado, y obstáculo interpuesto. **Original: 9 fallos; parche: 9 casos sin fallos.** Se usaron inputs válidos de mirada antes de F. Estos son casos CPU con plano analítico, no pruebas PhysX de seis caras; no se repitieron suites ajenas.

Regresión nativa posterior sobre el mapa real: F alcanzó **Surface con ancla** en el piso, estable en (3.615048, .056, 1.301736), tick80. Esto acredita la reparación del caso original sobre piso. Pared y techo nativos quedan pendientes.

## Testigo de cámara entregado a W2

En ese posado, con vista local paralela al piso (+Z), se midió:

| Dato | Valor |
|---|---|
| Actor, anchor, pivot y cámara | (3.615048, .056, 1.301736) |
| CameraCollisionRadius | .08 m |
| DesiredDistance / ResolvedDistance | .85 m / 0 m |
| Collider solapado | HousePatio(Clone)/house_alfa_static/Collider_HouseShell_Solid_0_0 |

El centro de cámara es finito y está por encima del piso, pero su esfera de colisión penetra .024 m en él. El fallback vuelve al anchor coincidente y mantiene el volumen solapado. No se afirma clipping visible a partir de esta consulta, ni se acredita LOS con anchor y cámara coincidentes. La mirada local se mantuvo en +Z porque la captura de ratón estaba desactivada; las miradas delante/detrás y el primer frame de despegue no se ejecutaron después del fallo.

Director asignó el editor congelado a W2: PlayMode, AutomaticTick=false, tick80. W1 no cerró ni alteró la escena después del traspaso. No se ensayaron más rutas, otras superficies o accesos tras este testigo.

## Evidencia y pendiente

Recibo: `U09409-06-TRAVERSAL-20260912.json`, con SHA256 de scripts, trazas y log parcial preservado en `N:/LetMeSleep/Validation/U09409-06-Traversal-20260912/`. Archivos principales: `human-attempt1.json`, `floor-attempt1.json`, `floor-attempt2.json`, `reach-contrast.json`, `floor-patched.json`, `floor-camera.json`; regresión CPU en `targeted-cpu/original-valid-input.log` y `patched.log`.

Pendientes: segundo acceso exterior, habitaciones no recorridas, vuelo interior/exterior completo, posado/despegue nativo de pared/techo, cámara en las orientaciones solicitadas y validación independiente. No se cierran U094-09 ni U094-06 completos. No hay afirmación WAN, FPS, validación del paquete publicado, partida manual o aceptación visual.
