# Fixture nativa Training: catálogo de prueba, UI real

Ticket nuevo mapas20260913. No modificarBootstrap/Core/alfa ni instalar arte. Fuente TrainingMapNativeFixture.cs permanece bajo docs, no Assets; no necesita meta ni cambia asmdef compartido.

## Ejecución Root

Compilar fuente como DLL externa contra UnityEngine módulos usados, UnityEngine.UI, Unity.TextMeshPro y LetMeSleep.UI integrado con SetTrainingMaps. El runner externo usa las referencias ya disponibles. Sin UnityEditor en fixture; no necesita NUnit. No recompilar/proporcionar otra copia UI dentro de sesión: usar UI integrada cargada.

DLL preparada: N:/LetMeSleep/Validation/UI-TrainingMaps-Native-20260913/bin/Debug/netstandard2.1/TrainingMapNativeFixture.dll

Csproj: N:/LetMeSleep/Validation/UI-TrainingMaps-Native-20260913/Fixture.csproj

En PlayMode de escena de prueba vacía, sin AlfaApplication/AlfaUiController activo, usar evaluador existente para cargar DLL y llamar:

`LetMeSleep.Validation.TrainingMapNativeFixture.Run(headingFont, bodyFont, 1280, 720)`

Pasar fuentes TMP reales del proyecto: Bangers-Regular SDF.asset y AtkinsonHyperlegible-Regular SDF.asset. Root puede resolverlas con AssetDatabase.LoadAssetAtPath en su evaluador Editor. No abrir Editor adicional. Dimensiones Screen exactas deben estar configuradas por Root en monitor principal horizontal; fixture no llama SetResolution. Si existeUIreal, rechaza arranque: no oculta/destroye instancia del producto.

Run devuelve carpeta run única inmediatamente. Status devuelve reciboJSON. Tras estado checks-completed-awaiting-visual-review, llamar ShowMapForReview(index0..4), esperar un frame y capturar GameView real. Repetir índices necesarios a720 y1080. DisposeFixture destruye sólo UI/observer/eventSystem que esta fixture creó; conserva EventSystem existente. Esperar un frame antes de próximaRun. Si se terminaPlay durante ejecución, marca interrupted; excepciones producen failed y conservan UI para diagnóstico.

## Comprobaciones que aportan valor nativo

- Crea UIreal mediante AlfaUiRuntime.Create, fuentes/canvas/escalado/layout actuales.
- Inyecta5IDs fixture-only-map-1..5 y etiquetas explícitas de prueba. NO son catálogo final ni mapas verificados para jugar.
- Navega con PointerClickHandler de botones reales y registra IMenuActions.StartTraining en spy: ID correspondiente, inicio único, retrymismoID para las5opciones, navegacióncíclicaNext/Previous.
- PresentTraining(loading=true) y resultados son estados SINTÉTICOS señalados. Comprueba controlesbusy y también fuerza callbacks para verificar guardia contra duplicados, sin fabricar acierto de geometría.
- TMP real ForceMeshUpdate/isTextOverflowing y límitesviewport de controles visibles, a resolución real. PruebaBackmenú/reentrada y catálogo vacío impidiendo despacho.
- Deja UI visible para inspección real porRoot. No guarda PNG porframe ni certifica percepción/contraste con checksdecoordenadas.

No repite los12checks puros de normalización/reorden: siguen en N:/LetMeSleep/Validation/UI-TrainingMaps-20260913/check.py. Esta fixture añade controles/layout/contratos en motor. Ejercita rol humano; si hace falta cobertura mosquito, Root puede seleccionarlo en la UI durante revisión separada, sin confundir con estos resultados registrados.

## Límites

Spy registra intenciones; no carga escenas, geometría, física ni NavMesh. Interacción de PointerClickHandler es sintética, no movimiento/clic físico/teclado. No prueba red, ejecución de entrenamiento ni vuelta desde geometría real. Fuente24/labels y botonesrequieren foto720/1080/inspección deRoot. Completado-checks no es PASSvisual ni aprobación de los5mapas.

Comprobación offline realizada: compilaciónfixture0errores/advertencias. Nativos pendientesRoot. Ningún paquete/asmdef compartido/editor/render/crédito usado porUI.
