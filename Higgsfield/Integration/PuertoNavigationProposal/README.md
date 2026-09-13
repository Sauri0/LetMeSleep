# Puerto v1 — propuesta de navegación para Gameplay

Usar **`navigation-v3.json` y `prepare-v3.json`**, en `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/05-pueblo/NavigationProposal`. La carpeta de este documento conserva las mismas propuestas y sus generadores. No se modificaron helpers existentes, geometría, materiales, assets Unity ni spawns. No se ejecutó Blender, Unity ni generación paga.

## Revisión v3: continuidad de la hélice

Gameplay señaló que comprobar cada portal aislado no garantiza que el tramo entre portales esté libre. La inspección confirmó colisiones con peldaños en el trayecto desde la entrada al primer portal vertical y entre las vueltas: por ejemplo, [28.05,11.25,26] → [28.05,13.05,26]. **La navegación v1/v2 queda reemplazada.**

V3 sustituye las tres regiones verticales amplias del faro por veinte secciones cortas siguiendo los centros de los peldaños, a una altura de vuelo de peldaño+1 m. Resultado: **46 zonas, 53 portales, grafo conectado y 16 spawns cubiertos**. Pasan la comprobación de esfera fuente los 53 portales, 39 tramos de la hélice/salida y las veinte conexiones entre extremos reales de portales que comparten una región del faro. Las cajas son regiones semánticas, no volúmenes garantizados vacíos; aún se debe ejecutar Runtime.

`prepare-v3.json` conserva exactamente las coordenadas y los presupuestos de las 28 rutas v2; cambia únicamente la ruta al nuevo archivo de navegación. El riesgo de cápsula humana en los accesos al puente sigue pendiente. `revision-v3.json` conserva los dos tramos antiguos bloqueados, la cadena nueva y todos los hashes congelados anteriores.

SHA256 navegación v3: `02ef2bd431a05cf3fb2932c9f04ddeb00d6a0b870c6e94575350adb5944c006d`.
SHA256 configuración v3: `52d22a08795df602f650eca57c380cd27874851067043343aee02b676a446fa7`.

## Base anterior y comprobaciones comunes

La propuesta tiene **29 zonas, 36 portales, grafo conectado y los 16 spawns de mosquito cubiertos**. Los puntos centro ± normal × 0.55 pertenecen a sus regiones, con tolerancia 0.04. Cada corredor de portal se comprobó contra los triángulos de las 458 mallas sólidas con esfera de radio 0.055 m: muestreo de hasta 1 cm y margen adicional de 5 mm. Esto es evidencia offline, no validación nativa ni prueba de volumen interior. Las regiones exteriores son semánticas amplias que contienen elementos locales; el motor debe evitar obstáculos dentro de ellas.

La configuración mantiene 8 rutas humanas, 4 vuelos y 16 patrullas Runtime (28 rutas; máximo 62 puntos por ruta). `maxTicks` sigue en 600. Sumadas a las comprobaciones de los 21 spawns, el runner puede producir los 49 casos previstos. Gameplay conserva la medición nativa, la cápsula y el motor real, y la aprobación técnica.

## Advertencia concreta: extremos del puente

**La ruta humana del puente no está certificada como libre.** Las rocas `Rock_Shore_043` y `Rock_Shore_045` se superponen parcialmente al área de acceso. La revisión v2 pasa al norte de los extremos de las barandas por X ±8.4, Z −11.4, luego X ±7.4, Z −11.5 y el centro del puente. No hay puertas ni huecos inventados a través de las barandas.

Aun así, al colocar una cápsula de radio 0.25 sobre la cota del conector +0.003 m, el muestreo encuentra contacto de la parte inferior con roca: distancia mínima al eje 0.17875 m en el acceso oeste y 0.16777 m en el este. La roca este sobresale 0.06858 m bajo ese punto central. En el oeste puede haber contacto lateral incluso sin roca por encima del centro. `revision-v2.json` incluye diez muestras con coordenadas, soporte elegible y superficie sólida real.

El filtro `IsSupport` del runner excluye rocas para evitar apoyos accidentales en muebles; no implica que el producto prohíba caminar por roca. Gameplay debe decidir con pendiente, step y cápsula reales si el recorrido natural admite ese apoyo y cómo medirlo. No se elevó artificialmente el pie ni se alteró el terreno para ocultar el contacto. Los resultados favorables de torso/cabeza excluyen precisamente la zona de pies y no resuelven este riesgo.

## Convención y apoyos

Coordenadas Unity **[X,Y,Z] = Blender [X,Z,Y]**, en metros. En las rutas humanas, Y es el origen del rayo descendente, nunca una afirmación de la posición final del pie. La revisión v2 cruza la misma lista `IsSupport` de Gameplay y normal Y > 0.55. Los marcadores fuente 5/16 permanecen intactos.

| Elemento | Coordenadas / evidencia Unity |
|---|---|
| Plaza continua | X [−6.5,6.5], Z [−3.5,9.5], apoyo Y 3.265 |
| Casa 1, puerta frontal | Centro X −14, Z −5; apoyo Y 3.32; hueco nominal 1.3 × 2.3 m |
| Casa 2, puerta frontal | Centro X −12, Z 10; apoyo Y 3.32; hueco nominal 1.3 × 2.3 m |
| Casa 3, puerta frontal | Centro X 2, Z 13; apoyo Y 3.32; hueco nominal 1.3 × 2.3 m |
| Taller, puerta doble | Centro X 15, Z −8; apoyo interior 3.32 / porche 3.33; hueco nominal 3.2 × 2.65 m |
| Taller, paso alrededor del bote | X 13.1 y 17.1, entre Z −6.4 y −1; evita casco y cunas de reparación |
| Puente | X [−8,8], centro Z −12.2; tablero arqueado Y 3.26–3.56; accesos sujetos al riesgo indicado |
| Muelle 1 | Centro X −14, tablero Y 1.24, Z [−29.434,−19.234] |
| Muelle 2 | Centro X 14, tablero Y 1.24, Z [−29.2,−19]; ruta X 13.2 rodea Barrel_08 |
| Escaleras de muelles | Diez niveles centrales, Y 1.24–3.04, contrahuella nominal 0.20; desembarco superior 3.25 |
| Escalera exterior del faro | Peldaños extraídos de caras horizontales, Y 4.8662–7.27; entrada inferior por flanco NW |
| Faro, puerta | Centro X 29, Z 23.6; piso 7.2; portal a Y 8.45, hueco nominal 1.6 × 2.4 m |
| Faro, escalera interior | 56 peldaños, Y 7.36–16.16, subida 0.16 por peldaño; centros serializados en source-evidence.json |
| Faro, salida superior | Rayo humano final [30.1,16.5,24.1] sobre balcón Y 16.16, fuera del hueco de escalera |

Se redujo el ancho de portal de casas a 1.1 m y del taller a 3 m por la presencia de las hojas abiertas. No se agregaron portales por ventanas: aunque los vidrios decorativos no tengan colisión, no se interpretan como ventanas abiertas. Senderos, adoquines y tablas visibles del piso son decorativos; se usa el terreno, pavimento continuo o piso estructural inferior.

## Duración y evidencia

`revision-v2.json` incluye longitud XZ desde cada spawn y comparación con v1. Plaza pasa de 70.20 a 48.20 m (466.5 ticks ideales a 3.1 m/s y 30 Hz); Casa 1 de 56.98 a 39.04 m (377.8 ticks). Todas las rutas humanas quedan por debajo de 500 ticks ideales, con 600 de presupuesto. La estimación no incluye aceleración, frenadas, giros, ascenso ni resolución de contactos; no se cambió a sprint.

`route-diagnostics-v2.json` registra 1.607 muestras de torso/cabeza humano sin bloqueos detectados y 92 segmentos de vuelo sin contacto de la esfera. El diagnóstico humano toma una sección vertical de radio 0.25, entre pie+0.60 y pie+1.45, muestreada cada 0.25 m de recorrido; no es una comprobación de cápsula completa. Las pendientes, bordes, contrahuellas, saltos, capacidad del motor, duración real y patrullas quedan pendientes de Gameplay.

La geometría numérica se lee del GLB serializado, con transformación mundial y reflexión de eje Z. Los 730 nodos y 458 mallas sólidas coinciden con el audit; sus vértices quedan dentro de las cajas auditadas con discrepancia máxima 0.00000137 m. Las cajas auditadas de rocas rotadas son envolventes de esquinas y no se confundieron con sus superficies reales. El FBX limpio se fijó por SHA256 `aa4bb192353bfbebe5f3d48e4dd6950245b854f397b95a7fb7d7ac85a2a834a4`. Esto no reemplaza verificar la importación nativa del FBX.

Navigation SHA256: `bdc2ae4e6af11150469483c7802b3b1c4f1f06b06817b80181586d9d50648126`.
Prepare v2 SHA256: `73d892990891668ea013af1a2266fcba30d5d60565f94230b0d03834a1a3aefa`.

Los cuatro archivos de primera entrega (`navigation.json`, `prepare.json`, `source-evidence.json`, `route-diagnostics.json`) están congelados por coordinación con Gameplay y se verificaron nuevamente por hash al emitir v2. No volver a ejecutar `build_proposal.py` sobre esa entrega; usar otra carpeta/version para una revisión futura. Los scripts requieren Python y NumPy ya disponibles, sin instalación de dependencias.
