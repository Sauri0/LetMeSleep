# Agua GPU Higgsfield: alternativa opt-in

Alternativa técnica para el océano grande de Yate, sin aumentar el límite CPU de 20 000 vértices, reducir geometría ni modificar el mesh asset. **No instalada ni activada en ningún mapa.** Root decide su uso después de la importación y prueba nativa. El recuento 16 160 vértices fuente / 96 000 entradas GLB es contexto de la solicitud; aquí no se certifica el recuento importado en Unity.

## Contrato y aspecto

`HiggsfieldGpuWater.Configure(MeshRenderer water, Shader shader, Parameters settings, Color[] flatColors = null)` es la única activación. Requiere Play Mode y un helper habilitado; no tiene `ExecuteAlways`, búsqueda de mapas, `Shader.Find`, autoactivación en OnEnable ni cambios de importer/Bootstrap. Admite MeshRenderer + MeshFilter, incluyendo mallas no legibles por CPU. No admite SkinnedMeshRenderer; las blendshapes autoradas permanecen en el flujo actual.

Shader explícito: `LetMeSleep/Higgsfield/FlatGpuWater`. Root debe conservar una referencia de asset al shader en su integración/build; esta entrega no cambia inclusión de shaders ni escenas.

Conserva todos los triángulos, submeshes y buffers originales, incluidas normales. Cada slot recibe un material privado de color plano, obtenido de `_BaseColor` o `_Color` y su MPB efectivo. Un bloque por slot tiene precedencia sobre el bloque general, como en Unity. Alternativamente se pasa una paleta explícita de igual longitud. `UseVertexColors` permite multiplicar los colores de vértice existentes y exige que la malla tenga ese atributo; está apagado por defecto para no añadir interpolación a una paleta por caras.

La salida es **opaca y Unlit**, sin texturas, reflejos, refracción, metal, brillo ni normal smoothing. Conserva las facetas de geometría y colores autorados; no reproduce el sombreado PBR anterior ni recalcula normales para las ondas. La luminosidad visible no cambia con luces de la escena. Root debe evaluar esa elección con la paleta del mapa, especialmente de noche. Las caras son visibles por ambos lados.

No añade ni modifica colliders y rechaza colliders en el objeto del agua o sus descendientes. Root sigue siendo responsable de que no exista otro collider independiente que represente esa misma superficie. Mientras está vinculado desactiva casting/recepción de sombras; el shader no tiene ShadowCaster ni muestreo de sombras. No agrega caras ni lee/escribe `mesh.vertices`, `mesh.normals`, `mesh.bounds` o `sharedMesh`.

## Onda y parámetros

La función es la de `HiggsfieldLowPolyWater.Update` central, con `Phase=0` por defecto:

```text
k = 2π / max(0.1, Wavelength)
phase = timeSeconds * Speed + Phase
height = Amplitude * (
    0.65 * sin((world.x + 0.37 * world.z) * k + phase)
  + 0.35 * sin((world.z - 0.21 * world.x) * k * 0.71 + phase * 0.83))
world.y += height
```

`Amplitude` está validada en [0, .15] metros; `Wavelength` debe ser positiva y usa el mínimo efectivo .1 m del CPU; `Speed` es velocidad angular y `Phase` radianes. Todos los números deben ser finitos. Receta `waveAmplitude/waveLength/waveSpeed` se asigna directamente a `Amplitude/Wavelength/Speed`. La suma de pesos 1 limita el desplazamiento vertical a ±Amplitude.

Root propuso para Yate `Amplitude=.14`, `Wavelength=13.24` como aproximación a la fuente; no hay valores específicos de Yate activados en el componente. El loop Blender de 10 s no se garantiza con estas dos frecuencias; esto reproduce el modelo CPU, no sus drivers Blender exactos.

`SetTimeOverride(seconds)` fija tiempo explícito para comparar dos capturas o renderers en la misma fase. `UseGameTime()` vuelve a `_Time.y`, que URP local alimenta desde Time.time durante Play Mode. `SetParameters(settings)` permite cambiar los valores sin recrear materiales. No escribe materiales en cada frame: el shader consume tiempo global. LateUpdate sólo comprueba conflicto CPU y actualiza bounds, independientemente del número de vértices; la lista de componentes se reutiliza.

El mismo `WaterVertex` se usa en `UniversalForwardOnly` y `DepthOnly`: color y profundidad reciben posiciones idénticas y escriben profundidad opaca con ZWrite On. El DepthOnly conserva `ColorMask R` y la salida de profundidad del patrón URP 17.3. No hay pase de motion vectors, DepthNormalsOnly, Meta ni integración DOTS; los efectos que los necesiten deben revisarse antes de elegir esta alternativa. No se certifican variantes de shader ni render paths mediante compilación C#.

## Exclusión del Update CPU e instalación futura

1. Root selecciona explícitamente el renderer del agua de la instancia importada. Preferir preparar la instancia inactiva antes de activarla para evitar el OnEnable del agua CPU sobre una malla grande.
2. Deshabilitar `HiggsfieldLowPolyWater` del mismo objeto **antes** de Configure. Si ya estaba activo, su OnDisable restaura su mesh original; no deshabilitarlo después de vincular GPU. El helper rechaza un CPU activo y no altera ese componente.
3. Activar la instancia y llamar desde el integrador autorizado, con una referencia de shader serializada:

```csharp
cpuWater.enabled = false; // Sólo si existe. Ownership de esta decisión: Root.
var settings = HiggsfieldGpuWater.Parameters.Default;
settings.Amplitude = recipe.waveAmplitude;
settings.Wavelength = recipe.waveLength;
settings.Speed = recipe.waveSpeed;
gpuWater.Configure(waterRenderer, flatGpuWaterShader, settings);
gpuWater.SetTimeOverride(2.5f); // Sólo prueba determinista; UseGameTime() para jugar.
```

Si alguien vuelve a habilitar CPU durante el vínculo, LateUpdate registra el conflicto y libera GPU para evitar doble deformación en el render siguiente. Otros deformadores, scripts que cambien materiales/MPBs y el helper no deben poseer simultáneamente el mismo renderer. La tabla local de propietarios rechaza un segundo helper.

`Release`, OnDisable y OnDestroy restauran materiales, MPB general y todos los MPB por slot, casting/recepción de sombras y valor original de localBounds. Los materiales privados se destruyen y el mesh asset sigue intacto. Rehabilitar el helper no reanuda nada: requiere Configure explícito. Root decide si vuelve a activar CPU después de Release.

Para evitar recorte en bordes del frustum, el componente extiende **renderer.localBounds** en el equivalente local del desplazamiento world-Y y lo actualiza si cambia la transformación. Al liberar restaura el valor capturado; este es un override numérico de renderer, no una restauración de un modo automático de bounds. Esta opción es para MeshRenderer estático: no compartir ownership con otro sistema de bounds ni reemplazar su mesh durante el vínculo.

## Fuentes locales y verificación

Referencias leídas de la instalación central, sin búsquedas web ni sesiones nativas:

- `N:/LetMeSleep/Repository/unity/Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/HiggsfieldLowPolyWater.cs`: fórmula CPU, eje world-Y y exclusión por componente.
- `N:/LetMeSleep/Repository/unity/Library/PackageCache/com.unity.render-pipelines.universal@a8b4b2fc3560/package.json`: URP **17.3.0**, Unity 6000.3.
- `.../Shaders/Unlit.shader` y `.../Shaders/DepthOnlyPass.hlsl`: tags de pipeline, buffers, macros de instancia/estéreo, ZWrite, ColorMask R y profundidad de fragmento.
- `.../ShaderLibrary/Core.hlsl` y `N:/LetMeSleep/Repository/unity/Library/PackageCache/com.unity.render-pipelines.core@0bb36005e9ba/ShaderLibrary/SpaceTransforms.hlsl`: TransformObjectToWorld / TransformWorldToHClip.
- `.../Runtime/ScriptableRenderer.cs`, SetShaderTimeValues y sus llamadas: `_Time` desde Time.time.
- `N:/Unity/Editors/6000.3.24f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.xml`: MaterialPropertyBlock.HasColor y Renderer.localBounds.

Compilación **C# offline** con dotnet 10.0.202 y referencias Unity 6000.3.24f1: 0 errores / 0 advertencias. Shader revisado estructuralmente contra esas fuentes: un único vertex para ambos pases, sin ShadowCaster ni escrituras de malla. No hay dxc/glslangValidator disponible en PATH y no se inició UnityShaderCompiler: la compilación/importación real HLSL queda pendiente del turno nativo de Root.

Prueba nativa pendiente: importar shader sin errores, fijar tiempos 0/2.5/5, verificar desplazamiento Y y profundidad coincidentes, paleta/facetas, ausencia de collision/sombras, bordes de frustum, conservación de asset Mesh y restauración de materiales/MPBs tras Configure→Disable/Destroy/reconfiguración. Medir después rendimiento real si Root selecciona esta alternativa. **No se prometen FPS, calidad visual ni igualdad exacta entre aritmética CPU/GPU.**
