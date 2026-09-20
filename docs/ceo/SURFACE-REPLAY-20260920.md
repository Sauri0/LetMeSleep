# Comparabilidad del replay de superficies

La matriz original permanece intacta: 85 casos, 64 PASS y 21 FAIL. El replay
`N:/LetMeSleep/Validation/V020/SurfaceValidatedRoutes/Build/20260920-070945-835/native-results/surface-maps.json`
terminó con 84 casos, 65 PASS y 19 FAIL, cleanup verdadero. **Los totales no
demuestran una mejora:** el selector consulta TrySurface en tiempo de ejecución
y el nuevo control de volumen corporal cambia qué candidatos se seleccionan.

Faltan tres casos originales: Isla edge3/StructuralFrames, Camp crawl/ceiling/
WashroomFloor y Yate negative/LoungePort. Aparecen dos sustitutos: Isla
edge3/Kitchenette y Yate negative/LowerCabinFloor. Deben registrarse como
cobertura ausente y casos nuevos, respectivamente. También hay cambios de
resultado en casos cuyo ID se conserva; es necesario comparar punto, normal,
objeto y entrada antes de clasificarlos como regresiones de física.

El siguiente replay debe usar entradas congeladas del original y conservar
los casos que ya no pueden adquirirse como resultados explícitos. No volver
a seleccionar superficies para mejorar el conteo. Los trazados existentes
quedan preservados y no se han declarado aceptación final de los cinco mapas.

La suite dirigida v2, ejecutada separadamente en `Build/20260920-070942-567`,
obtuvo 7 PASS y 1 COVERAGE_GAP, sin FAIL; el hueco corresponde al perfil de
escalera Casa. Incluye comprobación del destino corporal ocupado y techo de
Camp. Esta evidencia tiene alcance dirigido y no reemplaza la matriz original.

## Entrega REPLAY-FROZEN85 — preparación externa

Candidato estable: `N:/LetMeSleep/Validation/V020/SurfaceValidatedRoutes/Build/20260920-074546-702`.
Compilación offline contra las DLL reales de Library: **0 errores y 0 advertencias**.
El agente no ejecutó Unity. `run-in-coordinator-slot.cs` contiene el comando
completo para el turno nativo del CEO; `receipt.json` conserva fuentes y
dependencias con sus hashes. Los Build anteriores y ambos JSON originales
permanecen intactos.

`frozen85-manifest.json` fija los 85 pares mapa/ID, objeto, SurfaceId, punto,
normal, inicio, extremos de arista y normal objetivo. El runner crea las 85
filas antes de construir fixtures y nunca ejecuta el selector RunMap,
TrySurface, Witness ni FindGap para elegir casos. Las consultas físicas
durante la simulación siguen delegándose al mundo real, incluyendo clearance.
Una adquisición rechazada conserva la fila como COVERAGE_GAP, con razón y
trazado; no se considera una mejora ni se reemplaza por otro objeto.

Hay **80 criterios recuperables y cinco huecos históricos explícitos**.
Los cinco casos `negative/real-gap` no serializaron dirección ni gapDistance;
el source preservado demuestra que dependían de consultas físicas durante
la selección. No existe captura confirmada de esos valores. Su adquisición
se ejecuta como evidencia parcial separada en `frozenEvidence.partialAcquisition`,
pero la fila completa permanece COVERAGE_GAP. Nunca se infiere el umbral a
partir del runtime actual.

Para los otros casos, las direcciones se reconstruyen con las fórmulas
originales, sin consultas de selección. Se exige coincidencia exacta de los
componentes float del centro, normal e inicio, y de la arista cuando corresponde;
una diferencia produce cobertura ausente. No se cambiaron las tolerancias de
movimiento, penetración, transición ni las duraciones. La prueba de agua
conserva su criterio original, que no añadía la aserción de penetración de Walk.
Los métodos Acquire/Walk/Edge/Gap/Forbidden y la enumeración geométrica se
comparan con el source del Build original mediante auditoría offline.

La auditoría registra ausencia de cambios tracked en los cinco mapas frente
al HEAD de compilación original `51913bba2eb3d5098c536df3a4d584f619cacef9`.
También congela hashes de modelos, prefabs y sus metadatos; el runner los
verifica antes de cada mapa. Esto respalda la reconstrucción geométrica, pero
el JSON histórico no contenía los vértices ordenados: la procedencia y ese
límite quedan declarados en `frozen85-audit.json`. Cualquier cambio posterior
de esos archivos bloquea el mapa con filas explícitas, sin seleccionar otros.

Baseline SHA-256:
`5D4C656AB09189D6311C6097B0F4F72B8382B8BEEF3F5A31784B2040B2873047`.
Manifiesto SHA-256:
`D819921506E8C0CEF45BCE9D3B8B5396BDF67F89AB96C0DA9C3F18315CE685C5`.

Como evidencia funcional separada, `run-separate-synthetic-checks.cs` permite
ejecutar la batería sintética existente, cuyo caso determinista
`gap_and_nonperch_blocker_do_not_become_neighbors` cubre un hueco y un soporte
no permitido. No ocupa ninguna de las 85 filas ni reconstruye sus datos
ausentes. Ambos comandos están preparados, no ejecutados por este agente.

## Resultado nativo comunicado y verificado — Frozen85

CEO ejecutó el candidato `Build/20260920-074546-702`; el reporte
`native-results/surface-maps.json` conserva **85 filas:60 PASS,14 FAIL y11
COVERAGE_GAP**, cleanup verdadero, salida del runner0. El estado del reporte
es FAIL; la salida0 no representa aceptación del juego.

Los once huecos se desglosan en cinco `negative/real-gap` sin dirección/distancia
históricas y seis adquisiciones rechazadas sobre el input congelado: Isla
edge0/Footings y edge3/StructuralFrames; Camp ceiling/WashroomFloor; Yate
edge1/MainDeck; Puerto join0 y join1 GroundFloor/HollowTower. No se buscaron
reemplazos. Las filas conservan su razón y la adquisición parcial de los
cinco negativos tiene un campo separado.

Estos conteos **no declaran mejora ni cierran los21 fallos originales**:
la etiqueta COVERAGE_GAP distingue condiciones que impiden ejercer el criterio,
no convierte un fallo histórico en éxito. Quedan14 fallos actuales observados
y once límites explícitos de cobertura. No se programó otro replay como parte
de esta entrega.
