# Surface probe — entrega offline 2026-09-12

Worker Código / Gameplay retomó el borrador según TEAM-RECOVERY. No se modificó Presentation/ActorVisualBinding, ningún asset ni reglas del juego. La orientación fc3d97b permanece bajo Presentación.

## Archivos

- `validation/native/SurfaceVisualProbe.cs`: diagnóstico externo de práctica propia, rutas con inputs/actions normales piso/pared/techo, observación tras cámara de juego propia y checkpoints.
- `validation/native/Compile-Probe.ps1`: compilación netstandard2.1 externa, DLL de nombre único, loaders eval_file y recibo de hashes/assemblies.
- `validation/native/README.md`: receta exacta, métricas, límites, limpieza y aceptación pendiente.
- `PAUSED-CHECKPOINT-20260912.md`: conserva historia y señala reanudación.

Paquete definitivo de esta entrega: `N:/LetMeSleep/Validation/SurfaceVisual-20260912/probe-20260912-195958-387-6f7b3f20`. Contiene DLL, `Load.cs`, seis `Start-{floor,wall,ceiling}-{Manual,Continuous}.cs`, `Status.cs`, `Next.cs`, `Stop.cs`, fuente/receta y `compile-receipt.json`. Compilación: **0 errores, 0 advertencias**, Unity 6000.3.24f1, APIs de ScriptAssemblies centrales; HEAD central registrado b7d9ca615c5eb5c4d3eb8f396e24de001be03b14. El recibo registra hashes y timestamps de los assemblies: no implica que toda fuente central pendiente haya sido importada.

Los paquetes `probe-*` anteriores se conservan como historia de compilación; usar sólo el paquete indicado. Los viejos `Binding.csproj`, `Checks.csproj` y 16 checks del directorio padre corresponden a orientación CPU, no a este helper.

También se compilaron offline los 10 snippets eval_file envueltos en métodos (`LoaderCompileOnly.csproj`, `loader-compile.log`): 0 errores, 0 advertencias. Es comprobación sintáctica; no ejecuta los snippets ni certifica la carga dentro del editor.

## Comportamiento y cobertura

Muestrea cada frame de la cámara de juego propia: estado/tick, velocidad, MotionPhase, fases/mezclas de Animator, plano mundial resuelto, Root físico/visual, +Y/normal, malla evaluada y mínimos/centroides de seis grupos distales de patas. Guarda nombres de huesos, escala, fuentes de malla y controlador. Detecta pesos inaccesibles/faltantes; no inventa contacto antes de resolver un plano. Tras despegar usa el último plano y lo etiqueta como histórico. El movimiento tangencial de centroides es un indicador de posible deslizamiento, no prueba automática de contacto plantado.

Modo continuo para movimiento; modo manual para PNGs con pose retenida. Ruta incluye entrada mirando al apoyo, giro tangente/opuesto (180°), mirada hacia afuera, marcha y salida. Tiempos limitados no garantizan ciclo completo ni todas las combinaciones de entrada/salida: el revisor debe comprobar cobertura real. Conserva bots, colisiones y resultados normales. Una obstrucción en el mapa revisado produce INCONCLUSIVE. La hipótesis de stride 0.3 m no está confirmada y no se cambió.

Limpieza implementada para Stop, límite de frames/tiempo, render ausente, error de inicio, recarga y salida de Play: libera callbacks/stream, restaura binding/Animator propios y cancela sólo la práctica identificada. No altera cámaras, volumen global o timescale. Exige -noaudio o mute previo. No puede probarse esta limpieza nativa mediante compilación; el Director debe observarla en el primer uso. No quedó proceso de Unity/Blender/render/test iniciado por este worker.

## Pendiente del Director

Ejecutar en slot nativo con modelo importado, guardar HEAD/dirty/modelo/controlador exactos, capturas reales y revisión de clips/transiciones completos. Verificar rutas contra living revisado y apoyar eventual diagnóstico de stride en el contrato final de Mosquitos. `COMPLETE_UNREVIEWED` nunca es aceptación visual ni PASS de apoyo. No se produjo evidencia nativa en esta entrega.
