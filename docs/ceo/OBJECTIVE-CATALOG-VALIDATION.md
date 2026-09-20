# Catálogos de objetivos: validación v0.2.0

## Estado posterior y entradas adicionales

Casa ya tiene diez objetivos instalados tras la ronda nativa de 150 s:
20/20 tareas completadas por cinco bots humanos contra un mosquito sin control.
Se preservó backup del prefab; sólo cambió el componente catálogo, su referencia
en raíz y ContentHash. La prueba no cubre red ni combate competitivo.

`ValidateExternalTaskRoundOnly` ejecuta esa ronda para un manifiesto de un mapa.
Exige 20 oportunidades, cuota 14, resultado TasksMet/Human y 4500 ticks.
`InstallExternalCatalog` exige primero esa ronda y luego cobertura física con
motor antes de guardar. No se ha instalado aún ninguno de los otros cuatro.

`DiagnoseExternalRouteOnly` requiere `-objectiveSpawn` y `-objectiveTarget` y un
manifiesto de un mapa. Emite posición y decisiones para una ruta de 330 ticks.
Su exit0 significa diagnóstico ejecutado: el resultado de ruta está en
`LMS_OBJECTIVE_MOTOR`. Una llegada incidental sin asignación no cuenta como PASS.
Los campos de decisión se limpian antes de cada muestra; métodos no llamados
se registran como `not-called` para no atribuirles estados de ticks anteriores.

La búsqueda rechaza apoyo sobre el collider del propio objetivo. Conserva los
rayos originales como primera opción; sólo si no sirven prueba contactos cerca
de los bordes laterales. Los criterios de soporte, alcance y LOS no cambian.
Casa conserva las diez entradas instaladas; Yate static03 corrige una mesa que
antes situaba al humano encima del tablero. Sus rutas motor siguen pendientes.

## Contrato y evidencia inicial

`V020GameplayObjectiveInstaller.ValidateExternalCatalogsOnly` acepta un archivo
absoluto mediante `-objectiveManifest`. Es un diagnóstico sin guardado de mapas.
El manifiesto tiene `maps[]`, cada mapa con `mapId`, `prefabPath` y diez
`objectives[]`. Cada objetivo identifica `objectiveId`, `kind` (Clean, Repair,
Switch), `displayKey`, `actionKey`, `targetName` y `routeRegionId`.

Sólo admite las identidades y rutas de prefab finales conocidas. IDs y targets
deben ser distintos. Los metadatos no acreditan coordenadas ni accesibilidad:
se buscan contactos sobre el collider real, suelo, espacio corporal, región,
línea de visión y presupuesto de ruta. Los fallos de búsqueda incluyen contadores
acumulativos para localizar el primer filtro que rechaza las aproximaciones.

Sin `-objectiveStaticOnly` se ejecutan todas las combinaciones spawn/objetivo
con motor real. Se exige al menos dos destinos por spawn, un spawn por destino
y dos destinos posteriores por objetivo. Estos últimos paran tras dos éxitos;
los comparadores sólo comparan pares efectivamente ejecutados en ambos runs.
Una excepción de creación o tick invalida el fixture y se propaga: no cuenta como
un fallo normal que pueda esconderse bajo la cobertura mínima del catálogo.

Casa conserva su entrada pública, diez especificaciones originales, presupuesto
330 ticks y formato de log. La generalización se comprobó en Casa11:47/50
recorridos spawn,21 onward comparables, sin regresiones frente a Casa10.
No equivale a probar todas las combinaciones ni a instalar el catálogo.

Los probes externos iniciales01/02 evaluaron40 metadatos de los otros cuatro
mapas:10 aproximaciones encontradas,15 targets sin collider sólido directo,
15 con espacio corporal pero ninguna aproximación dentro de la región indicada.
Ningún mapa obtuvo catálogo completo y no se guardaron prefabs. El segundo probe
añade contadores; no modifica los criterios para convertir fallos en éxitos.

Evidencia: `N:/LetMeSleep/Validation/V020/remaining-map-catalogs-static-02.log`,
`casa-catalog-10-to-11-all-comparable.json`, `bot-weighted-adapter-native-02.xml`.
Fuentes candidatas versionadas en `RemainingMapObjectiveCandidates/`; conservar
manifiestos previos y señalar cualquier sustitución de objeto o región.
