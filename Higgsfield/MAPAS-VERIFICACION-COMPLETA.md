# Cinco mapas: cierre del alcance completo

Estado: EN CURSO. Coordinación: Encargado de Higgfield.

La entrega c5d2ee2 conserva cinco mapas construidos, navegación humana parcial,
agua, iluminación, UI y diez cargas locales verificadas. No demuestra por sí sola
el objetivo activo de Branko: límites, funcionamiento, estabilidad, fondos y
lejanía, superficies para caminar/pegarse como mosquito e hitboxes correctas.
El relevo definitivo al Director queda pendiente hasta cerrar este alcance.

## Matriz de cierre

Cada fila debe tener evidencia sobre los cinco IDs finales del catálogo, versión
de código/activos y resultado reproducible. No se sustituyen recorridos por
inventario ni pruebas visuales por conteos. Los fallos requieren corrección y
repetición dirigida; una captura o un test genérico no certifican todo el mapa.

| Área | Responsable | Pendiente verificable |
|---|---|---|
| Mosquito | Gameplay | Aterrizaje, marcha suelo/pared/techo, aristas y uniones, despegue, negativos, penetración por tick |
| Humano | Gameplay | Cerrar Camp 48/49 sin subir tolerancia; regresión dirigida de cambios compartidos |
| Límites e hitboxes | Funcional + integración Encargado | Bordes horizontales/techo/suelo, agua/caída/recuperación, geometría visible y colisión, puertas/huecos, interacción/golpes |
| Fondos y distancia | Presentación + Encargado | Vistas reales desde extremos y alturas, ventana/interior/exterior, recorte de lejanía, horizonte/agua, cámara y noche |
| Estabilidad y carga | Estabilidad | Cinco mapas, capacidad máxima permitida, ciclos de cambio/reinicio, memoria/residuos/excepciones y medición local representativa |
| Fuentes e integración | Encargado | Conservar originales, copias ajustadas Blender, prefabs/catálogo coherentes, recibos e índice actualizados |

Evidencia y fixtures de esta continuación:
`N:/LetMeSleep/Validation/Higgsfield/CompleteScope`.

## Coordinación

Director confirmó que no inicia procesos ni modifica central durante esta etapa.
Un solo turno Unity/render a la vez, otorgado por Encargado. Los responsables
preparan herramientas externas y propuestas en sus carpetas; cambios de activos
centrales se integran con propiedad explícita. Blender visible 37360 inicialmente
idle en Puerto; fuentes y WIP previos se conservan.

Hallazgo inicial de lectura (todavía pendiente de reproducción nativa): el motor
limita transiciones sobre MeshCollider no convexo a la misma pieza y normales
casi coplanares. Los mapas importados emplean ese tipo de colisión. Gameplay
medirá y reparará la continuidad segura de marcha, sin permitir cruzar huecos.

No se solicitan generaciones de pago para hacer inventario o pruebas técnicas.
WAN y otros equipos no pueden certificarse desde este banco local; se separarán
de las condiciones efectivamente probadas sin ocultar fallos locales.
