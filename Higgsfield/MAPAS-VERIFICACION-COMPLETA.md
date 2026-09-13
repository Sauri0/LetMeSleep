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
| Humano | Gameplay | Camp corregido: 49/49 casos y 25/25 pasos; regresión final tras límites/recuperación pendiente |
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

## Evidencia actual de la ampliación

- Camp persistido: `Gameplay/Camp/applied-regression-01/map-checks-20260913-101818-510.json`,
  PASS_SCOPED, 49/49 casos, 25/25 pasos, cero errores/pendientes. Máxima
  penetración medida 0.868112 mm, sin elevar la tolerancia. Se bajaron sólo dos
  vértices del borde 3 mm; se conserva el spawn 05 previamente corregido.
  ContentHash `3551537b2702090fb67e41e10ed8dccd9a3703dc33a6cfd8c01fa4fa8ce90f36`.
- Fuente Camp editable guardada mediante Blender visible:
  `N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento/UnityAdjustedSource/HF_MAP_03_campamento_UNITY_ADJUSTED.blend`,
  SHA256 `7eea09617625cbbfb967883c443ebd992613cd2d0f147e1c63db798cd049445e`.
  Recibo hermano `adjustment-receipt.json`. Originales y sesión restaurados;
  FBX/GLB previos no se regeneraron y la colisión cerrada sigue siendo de Unity.
- Mosquito A/B calibrado: 85 casos reales, baseline 52 PASS/33 FAIL y candidato
  63 PASS/22 FAIL. Doce transiciones reparadas y una regresión en techo de Isla.
  Ocho negativos/positivos sintéticos pasan en candidato (baseline 3/8).
  Las sondas sí detectan el solapamiento de calibración de unos 35 mm. **Candidato
  no integrado**; quedan distinguir caras tapadas de rutas realizables y resolver
  fallos físicos reales. Evidencia `Gameplay/calibrated-{baseline,candidate}-native-01`.
- Límites baseline: 45 salidas en 65 intentos, con ascenso fuera de los cinco
  mapas. Un intento detenido antes por un obstáculo no prueba límite correcto.
  Evidencia `Functional/BOUNDARY-NATIVE-RESULT.md`. Installer de cuatro paredes
  invisibles y techo, sin suelo, con volúmenes de recuperación explícitos preparado.
- Recuperación v2 incorporada como capacidad opcional del motor, todavía sin
  volumen activo en mapas. No está aprobada: revisión independiente detecta
  bloqueo permanente si se vuelve a salir antes de 30 ticks estables; v3 en
  reparación y pruebas nativas sobre assemblies centrales pendientes.
- Presentación: distancia de cámara configurable y shader de cielo/fog integrados,
  pero catálogo/configuraciones finales aún pendientes. La prueba de Casa elimina
  triángulos negros del techo desactivando sólo sombras de la lámpara del living.
  Horizonte con gradiente y contraste del agua se está evaluando con capturas.

Cada recibo corresponde a su versión: no transferir estos resultados a cambios
posteriores de assets o motor sin la regresión dirigida correspondiente.

No se solicitan generaciones de pago para hacer inventario o pruebas técnicas.
WAN y otros equipos no pueden certificarse desde este banco local; se separarán
de las condiciones efectivamente probadas sin ocultar fallos locales.
