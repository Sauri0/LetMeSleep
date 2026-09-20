# Continuidad de pasaje de escalera — entrega para gate nativo

Estado: gates nativos y comparador verificados; cierre selectivo autorizado por CEO. 2026-09-20.

## Causa observada

`casa-spawn3-coffee-ab-01.log`: raw PASS152, predictor FAIL330. En tick44 el sample real es `(1.22218943,3.73779845,1.940733)`. El nearest-region es uf_west_hall a0.922m, frente a uf_rear_hall a1.209m. La implementación previa clampa X a0.3 y descarta straight_stair por el cambio de región. El actor todavía está en el ancho authored de escalera `[0.65,1.8]`. El vector correcto al waypoint activo es `(0.00281057,-1.13479829,-1.310733)`.

## Delta acotado

Sólo dos fuentes runtime: BotPatrol.cs y GameplayBotNavigation.cs. Dos tests existentes propios ampliados; no hay .meta nuevos ni cambios en steering, motor, catálogos, instalador, UI, STATE o runs.

GameplayBotNavigation separa el identificador de región y el sample físico `localFoot+Up`. El segundo siempre determina dirección, llegada y distancia restante. TryHumanRegion conserva su contrato para las consultas de catálogo/costes.

BotPassage conserva su constructor y añade un overload con volúmenes de tránsito. Sólo la escalera del esquema recibe esos volúmenes: clear_x de cada vuelo, rangos authored de altura/longitud y landing. El margen vertical `.35` ya es el de llegada a waypoint; el centro corporal sigue `.85` del esquema; las extensiones longitudinales incluyen los puntos existentes de entrada/salida `.45`. No se amplió la tolerancia de llegada.

Una escalera sólo puede continuar entre regiones después de alcanzar el primer waypoint. El sample debe permanecer en su volumen y dentro del tramo activo: proyección longitudinal entre extremos con margen `.24`, y altura entre extremos con margen `.35`. Otra planta, salida lateral/longitudinal o un objetivo/approach diferente descartan el anclaje.

Un cierre o blacklist devuelve cero. Exclusivamente dentro del hueco authored, con un tramo ya entrado y ninguna región física que contenga al actor, conserva ese tramo suspendido para revalidarlo cuando abra/expire. No borra retryAfter ni reinicia el contador/cooldown de J32; no descubre rutas alternativas dentro del hueco. Si hay región física válida, vuelve al comportamiento normal de invalidación y búsqueda. `InvalidatePassage(null, ...)` mantiene su semántica de limpieza global y no suspende.

La prohibición de adquirir rutas desde una región ficticia sólo se aplica dentro del corredor de escalera sin anclaje. Fuera se conserva la política nearest-region previa, calculando desde el punto físico. Esa política legacy no acredita una trayectoria libre: motor y steering siguen comprobando la geometría real.

## Evidencia final y filtros

- CPU central `stair-cpu-results.txt`: 67 PASS, 0 FAIL (54 previos +13 nuevos).
- Player/Editor Gameplay.Unity: 0 advertencias, 0 errores, `stair-player-compile.txt` y `stair-editor-compile.txt`.
- Adapter tests compilados: 0 advertencias, 0 errores, `stair-adapter-tests-compile.txt`.
- Un assert inicial de OrdinaryPassage suponía parada total; se corrigió para comprobar replanteo ordinario a upper_neighbour según la política legacy autorizada. El resultado anterior permanece en `stair-cpu-results-pre-legacy-expectation.txt`.

Filtro EditMode, esperado67: `LetMeSleep.Tests.GameplayBotTrainingTests;LetMeSleep.Tests.BotReplanTests;LetMeSleep.Tests.EditMode.BotPatrolDirectedRouteTests`.

Filtro PlayMode propio, esperado7: `LetMeSleep.Tests.PlayMode.GameplayBotNavigationContextPlayModeTests` (4 previos +3 replay/blacklist/no adquisición dentro del hueco).

## Gate nativo y Casa

- `N:/LetMeSleep/Validation/V020/bot-stair-native-02.xml`: 67/67 PASS, 0 fallos y 0 omitidos, Unity 6000.3.24f1/Windows. Incluye cierre/reapertura, blacklist durante seis segundos, conservación del vencimiento y parada real del input durante suspensión.
- `N:/LetMeSleep/Validation/V020/stair-customization-native-01.xml`: 7/7 del adaptador PASS. Los 7 casos de steering también pasan; los 3 de UI son independientes y no se atribuyen a este cambio.
- `N:/LetMeSleep/Validation/V020/casa-spawn3-coffee-ab-02.log`: raw y predictor PASS155. En tick44 el predictor conserva straight_stair/pointIndex2 y sample físico `(1.22256243,3.73779845,1.94073308)`; dirección `(0.00243747234,-1.13479829,-1.31073308)`. El nearest-region todavía puede ser uf_west_hall, pero ya no cambia el pasaje ni desplaza la muestra. La diferencia X frente al replay fijo procede de la trayectoria real.
- `N:/LetMeSleep/Validation/V020/casa-catalog-08.log`: catálogo PASS con 10 objetivos distintos, `saved=0`; no instalado.
- `N:/LetMeSleep/Validation/V020/casa-catalog-04-to-08-all-comparable.json`: las 50 rutas de spawn son exactamente comparables, mismo presupuesto330 ticks; 22→40 PASS, 18 mejoras, 0 regresiones. El conjunto08 incluye también el trabajo de steering/motor coordinado por técnica: no se atribuyen las 18 mejoras exclusivamente a este delta.
- Onward: 21 pares comunes, 0 regresiones y 1 mejora. Un par sólo existe en baseline (`bedroom_one_lamp→kitchen_sink`) y permanece incomparable; no se lo convierte en PASS. Los 10 objetivos tienen dos salidas aceptadas, pero el verificador termina anticipadamente tras esos éxitos y no prueba todos los pares posibles.

Quedan 10 fallos entre las 50 rutas de spawn: spawn0 hacia ground_basin, ground_toilet y bedroom_two_lamp; spawn4 hacia kitchen_sink, upper_basin, fridge, oven, bedroom_one_lamp, bedroom_two_lamp y upper_toilet. También queda el fallo onward ejecutado bedroom_two_lamp→ground_basin. El PASS del catálogo expresa sus criterios mínimos, no navegación universal.

No se instaló aún el catálogo y esta entrega no certifica gráficos, rendimiento ni WAN. Las pruebas CPU y el replay del adaptador no reemplazan la locomoción PhysX; la evidencia física aquí citada es el A/B y Casa08 ejecutados por CEO. Tokens/costo desconocidos: null.
