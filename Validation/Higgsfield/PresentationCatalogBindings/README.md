# Fixture externo: cinco bindings de iluminación

Preparado y compilado offline; **no ejecutado en Unity**. Sólo requiere el catálogo final guardado y la ruta del prefab real `LMS_AlfaLightingRoot`. No contiene IDs de mapa fijos: admite Yate v3 u otra revisión aprobada indicada por Root.

Config y recibos usan Newtonsoft.Json incluido en Unity (`Editor/Data/Managed/Newtonsoft.Json.dll`), sin paquetes nuevos. Se eliminó JsonUtility porque Root confirmó campos/listas vacíos al deserializar tipos anidados de DLL externas cargadas con Assembly.LoadFrom. Se conservan los guards de identidad, cinco IDs y output nuevo; se rechazan miembros desconocidos y contenido adicional, con profundidad máxima32, cultura invariante y TypeNameHandling.None. Los recibos incluyen explícitamente listas, booleanos y errores también cuando el resultado es fallido.

## Uso por Root

1. Ejecutar `build.ps1` con PowerShell. Compila contra los assemblies Unity/Proyecto centrales existentes, sin compilar copias de las clases del juego ni instalar scripts dentro de Assets. Genera proyecto y DLL únicamente en `N:/LetMeSleep/Validation/Higgsfield/PresentationCatalogBindings-20260913/`; acepta otras rutas con `-OutputDirectory` bajo el directorio permitido por el adapter.
2. Copiar y completar `config.template.json` fuera de Assets: ruta del catálogo final, GUID, SHA256 en minúsculas y sus cinco IDs reales. El prefab rig predeterminado fue comprobado en central y tiene Moon, lobbyFill y GlobalVolume asignados. No se fabrican sustitutos de ese rig.
3. En el turno nativo autorizado, usar el adapter existente:

```text
-executeMethod LetMeSleep.Content.Editor.Higgsfield.HiggsfieldExternalFixture.Run
-higgsfieldFixtureAssembly N:/LetMeSleep/Validation/Higgsfield/PresentationCatalogBindings-20260913/bin/PresentationCatalogBindingsFixture.dll
-higgsfieldFixtureConfig N:/ruta/absoluta/catalog-bindings-config.json
-higgsfieldFixtureOutput N:/ruta/existente/NUEVO-catalog-bindings-report.json
```

El entrypoint es exactamente `HiggsfieldMapChecks.Run(string configPath, string outputPath)`, devuelve ruta de reporte y lanza excepción si falla. Es **síncrono en Edit Mode** y debe usarse en un proceso de validación dedicado: el adapter central ejecuta `EditorApplication.Exit(1)` ante error. Este harness no inicia editor, no entra en Play Mode, no renderiza y no llama Exit por sí mismo. Root controla el proceso y `-quit` como en sus fixtures existentes.

## Comprobaciones

Exige catálogo guardado, GUID/SHA coincidentes, cinco IDs distintos presentes, cinco categorías y skybox por mapa. `catalog.Validate` y `ResolveLighting` hacen la resolución real de rutas sobre prefab e instancia. El harness crea una escena aditiva desechable, instancia el prefab real del rig, llama ApplyPreset explícitamente y recorre los cinco IDs.

Por cada mapa instancia su prefab sin guardar nada y comprueba:

- BindHiggsfield sobre la instancia: primaria direccional, color/intensidad/rotación/máscara/sombras, skybox, Trilight, fog, reflexión y Volume.
- Exactamente una luz nueva por ancla, descendiente del mapa, posición local cero, color/intensidad/rango/tipo/máscara/sombras/ángulos/bounce correctos. Confirma supresiones explícitas, direccionales importadas y lobbyFill.
- Segundo Bind inmediato: destruye las luces anteriores y conserva el número esperado, sin duplicados.
- Unbind: elimina las nuevas luces y libera MapRoot; restaura estado de luces originales, RenderSettings, los 27 coeficientes del ambient probe y Volume. Tolerancia numérica relativa 1e-4; rotación .01 grados.
- DestroyImmediate del mapa: no quedan descendientes ni raíces extra; el rig se reutiliza para el mapa siguiente.

Al terminar contrasta SHA de catálogo, prefab rig, prefabs de mapas, materiales de cielo y VolumeProfiles, incluidas sus metas. Cierra la escena de prueba, restaura escena activa previa y comprueba RenderSettings y estado dirty originales. No guarda escenas/Assets ni modifica Build Settings. Si detecta diferencia global al regresar intenta restauración de emergencia pero **mantiene el resultado como fallo**, sin ocultarla como PASS.

El recibo externo nuevo registra éxito por paso/mapa, versión Unity, SHA de config/DLL/catálogo, identidad del catálogo y cleanup. Se reserva con success=false antes de mutar la escena; un fallo de preflight no produce recibo. No sobrescribe reportes. Los errores operativos se devuelven al adapter; un fallo de cleanup también invalida el resultado. No se borra ni recarga la escena original.

## Límite de evidencia

Esto prueba código real y referencias de iluminación mediante APIs nativas **en Edit Mode**. Las destrucciones son inmediatas; no cubre Destroy diferido ni Update/LateUpdate de Play Mode, agua GPU/CPU, sombras renderizadas, shader compilation, navegación, UI, audio, multiplayer, FPS o WAN. Tampoco ejecuta la aplicación alfa ni su transición de pantallas. No afirma calidad visual a partir de igualdad de parámetros.

Compilación offline: dotnet10.0.202 + Unity6000.3.24f1 + assemblies centrales; 0 errores/0 advertencias. La primera ejecución nativa y los cinco resultados PASS/FAIL quedan pendientes del turno de Root, cuando exista el catálogo final.

Regresión JSON offline: se cargó la DLL recompilada mediante Assembly.LoadFrom en PowerShell/.NET, se deserializó su Request real con cinco IDs y se invocó su método Write real sobre un Report con cinco MapResult. Se conservaron listas, enteros, booleanos false/true y texto de error; contenido JSON adicional fue rechazado. Evidencia: `N:/LetMeSleep/Validation/Higgsfield/PresentationCatalogBindings-20260913/json-roundtrip-92709d4cb6bd4939ba512d179f3ec6a8.json`. Es una prueba de serialización .NET sin APIs nativas, no una ejecución del ciclo de iluminación en Unity.
