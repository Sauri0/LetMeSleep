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
