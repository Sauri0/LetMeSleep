# Verificación visual integrada Alfa

Esta guía valida la unión de personajes, contactos y mapas con los presets de
Presentation. No reemplaza una prueba jugable ni una medición de rendimiento.
Cada captura debe identificar commit, escena, Unity, resolución y preset.

## 1. Preparación reproducible

1. Importar y refrescar assets antes de ejecutar builders o capturas. Un prefab
   nulo después de cambiar código o FBX indica import incompleto; no es una
   evaluación visual.
2. Exigir `success=true` en `Content/Characters/BuildReceipt.json` y en el
   recibo de mapas. El recibo de personajes debe corresponder al builder
   `alpha-characters-4-bake-scale` o posterior.
3. Regenerar `LMS_GameplayPresentation.prefab` después de cualquier cambio en
   sus componentes. Comprobar en el prefab las referencias a
   `GameplayVisualPresenter`, `GameplayVfxPresenter`, `LobbyVisualPresenter` y
   `GameplayPresentationRoot`.
4. Cargar sólo `HousePatio` o `PrivateLobby` como mapa activo y asignar su raíz
   a `UnityGameplayWorld.MapRoot`. Las escenas aditivas auxiliares pueden seguir
   cargadas, pero no deben afectar casts, contactos o cámara.

## 2. Gate de poses renderizadas

Capturar al menos:

- Humano: Idle frente/perfil/espalda, Crouch, Clap y Swat al 45 %.
- Mosquito: Idle frente/perfil/espalda, Fly y BiteLoop al 45 %.

La captura debe muestrear la pose. Después de `Animator.Rebind()` y
`Animator.Play(state, 0, 0.45f)`, avanzar el Animator con un delta positivo,
por ejemplo `1/60 s`, o muestrear explícitamente el `AnimationClip`. Un
`Animator.Update(0)` aislado no acredita que el estado se haya evaluado.

Rechazar la revisión si poses deliberadamente distintas producen PNG con el
mismo SHA-256. Este comando muestra duplicados exactos:

```powershell
Get-ChildItem N:/LetMeSleep/Artifacts/review/alfa-characters/*.png |
  Get-FileHash -Algorithm SHA256 |
  Group-Object Hash |
  Where-Object Count -gt 1
```

Además de una pose claramente distinta, cada imagen debe conservar materiales
URP, piel finita, pies sin separación del piso y silueta sin ejes invertidos.

## 3. Escala y orientación

Comprobar antes de revisar contactos:

| Elemento | Gate |
|---|---:|
| Raíz de actor humano/mosquito | scale `(1,1,1)` |
| `SourceOrientation` | scale `(1,1,1)`, corrección Y `180°` |
| `VisualRoot` humano | scale `(1,1,1)` |
| `VisualRoot` mosquito | scale `(0.5,0.5,0.5)` |
| `CameraEye` humano en reposo | Y `1.530 ± 0.005 m` |
| `ProboscisTip` mosquito en reposo | actor local `(0,0,0.095) ± 0.001 m` |
| Vértice visible más cercano al tip | `<= 0.001 m` |
| Flyswatter `Grip→Impact` | `0.35–0.38 m`, referencia `0.365 m` |
| Cara de impacto | delante del grip sobre `+Z` |

No normalizar `SourceOrientation` a identidad durante la prueba. La corrección
de 180° está fuera del Animator y forma parte del contrato importado.

## 4. Contacto anatómico y herramienta

Ejecutar una ronda con snapshots y eventos reales:

1. **Picadura:** congelar un snapshot con `BiteAttachment` y revisarlo desde
   perfil y vista cercana. Si `PoseRevision` coincide, la distancia entre
   `ProboscisTip` y el punto anatómico debe ser `<=3 mm`. Cambiar postura del
   humano y confirmar que un attachment con revisión anterior no deforma el
   rig ni salta a otra superficie.
2. **Golpe con manos:** verificar ambos lados; la mano seleccionada termina en
   el extremo de la superficie autoritativa del antebrazo y el impacto visual
   coincide con `StrikeState.Target`.
3. **Golpe con matamoscas:** recoger una unidad y comprobar que `Grip` queda en
   `ToolSocket_R` y `Impact` llega al target. La longitud de `0.365 m` se aplica
   una sola vez; una separación cercana a esa longitud revela doble offset.
4. **Propiedad replicada:** antes de recoger, el clon de mano está oculto y la
   unidad de mundo visible. Con `OwnerActorId>0`, la unidad de mundo se oculta y
   sólo el actor cuyo `EquippedToolId=flyswatter` muestra el clon de mano. Al
   soltar, la regla se invierte en la pose publicada.
5. Los clones de Presentation no tienen colliders activos. El único collider
   interactuable pertenece a `GameplayToolPickup` bajo `MapRoot`.

Capturar host, cliente remoto y late join. La visibilidad se juzga desde
snapshots; `StrikeState` no decide qué herramienta está equipada.

## 5. Mapas y luces

Los prefabs fuente `HousePatio.prefab` y `PrivateLobby.prefab` no contienen
luces finales. Sus `LightAnchor_*`, `ReflectionVolume_*` y `AudioZone_*` son
datos de integración y deben permanecer como transforms vacíos.

Para una revisión final:

- Instanciar exactamente un `LMS_AlfaLightingRoot`: luz direccional Mixed,
  sombras suaves, color `(0.663,0.749,0.902)`, intensidad `1 lux`, y el Volume
  global ACES.
- Usar FOV/near/far del preset: humano `75°/0.03/100 m`, mosquito
  `68°/0.02/100 m`; distancia inicial mosquito `0.85 m`, máxima `2.5 m`, radio
  de cámara `0.08 m`.
- Configurar URP con distancia de sombra `28 m`, dos cascadas, SMAA High y SSAO
  según `URP-ALFA.md`.
- Mantener luces prácticas horneadas y probes como roots de escena finales. Si
  todavía no existen bake y probes, registrar la captura como revisión de
  geometría/materiales, no como aprobación de iluminación.
- `ReviewOnly_LightingRoot` de las escenas generadas sirve sólo para inspección
  de autor. Desactivarlo cuando se instancia el lighting root final; nunca usar
  ambos para acreditar el mismo frame.

Capturar habitación, pasillo con puerta abierta/cerrada, escalera, patio y
lobby. Rechazar fugas de luz, interiores sin lectura, sombras dobles, acne o
separación visible, objetos flotantes, materiales rosa y alas que desaparecen
contra fondos claros u oscuros.

## 6. Evidencia mínima

Guardar los PNG originales y una tabla con:

- hashes de prefab, FBX, escena, preset y build;
- valores de escala y distancias de anchors medidos;
- IDs de actor, pickup, superficie, `PoseRevision` y snapshot usado en cada
  contacto;
- cantidad de luces realtime y con sombra en el frame;
- resultado separado `PASS/FAIL/NOT_RUN` para pose, escala, contacto y luces.

Sólo `PASS` en los cuatro grupos permite aprobar visualmente la integración.
Los gates de FPS permanecen en `PERFORMANCE-BUDGET.md` y requieren Player real.
