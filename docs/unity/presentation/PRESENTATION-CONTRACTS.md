# Contratos de integración de Presentation/Audio

Estos contratos evitan que presentación, arte y gameplay compartan autoridad o
editen el mismo prefab. Los nombres son estables para alfa; cambios de API se
acuerdan con Director antes de producir assets dependientes.

## 1. Director → Worker 2

Director entrega:

- Unity 6000.3.24f1, proyecto URP Windows x64 y color space Linear.
- Versiones fijadas de URP y, si se acepta, Animation Rigging/Cinemachine.
- Assemblies y namespaces de `Core`, `Gameplay`, `Presentation` y `Audio`.
- Capas: `WorldStatic`, `WorldDynamic`, `CharacterRemote`, `CharacterLocal`,
  `LocalHeadHidden`, `CameraCollision`, `VFX`, `UI`.
- Un prefab/scene hook por mapa para insertar `LightingRoot`, `ProbeRoot`,
  `ReflectionRoot`, `AudioRoot` y `VolumeRoot` sin que Worker 2 edite la escena
  de Modelador 2.
- API para obtener actor local, rol, mapa, fase de ronda y estado de pausa.
- Presets serializados por Unity a partir de
  `alfa-presentation-presets.json`; sus GUID se publican antes de asignarlos a
  prefabs.

Worker 2 no edita `Packages/manifest.json`, ProjectSettings, scenes de mapa,
prefabs de personajes fuente ni assembly definitions del Director.

## 2. Modelador 1 → Presentation

### Humano

Prefab fuente con raíz a escala 1, eje `+Y` arriba, `+Z` adelante, 1 unidad =
1 metro. Avatar Humanoid válido y T-pose reproducible. Jerarquía mínima:

```text
HumanRoot
├── VisualRoot
│   └── Armature/Hips/.../Head
├── PresentationAnchors
│   ├── CameraEye
│   ├── AimChest
│   ├── HandGrip_L
│   ├── HandGrip_R
│   ├── ToolSocket_R
│   ├── Foot_L
│   └── Foot_R
└── Renderers
```

Requisitos:

- `CameraEye` centrado entre ojos, mirando `+Z`, sin scale heredada distinta
  de uno.
- Cabeza, pelo y gorro en renderers separables para ocultarlos sólo al dueño.
- Cuerpo bajo la cabeza permanece cerrado y visible desde primera persona.
- Manos con dedos orientados hacia el grip; ningún hueso tiene scale negativa.
- Clips in-place: idle, walk, run, crouch, jump, fall, land, turn, clap/defend,
  hit, faint y recover.
- Root transform y root bone sin traslación horizontal en clips in-place.
- Contacto de pies coherente: planta a `Y=0` en idle; curvas de pie sin saltos
  mayores a 2 cm entre primer y último frame de loops.
- LOD0 conserva silueta/manos/cara; LOD1 conserva extremidades; LOD2 conserva
  silueta. Umbrales iniciales de pantalla: 0.12, 0.04, 0.015; no culling dentro
  de 30 m.

### Mosquito

Rig Generic con root en tórax, `+Z` adelante, `+Y` arriba. Anchors:

```text
MosquitoRoot
├── VisualRoot/Armature
├── PresentationAnchors
│   ├── CameraTarget
│   ├── AimForward
│   ├── ProboscisTip
│   ├── WingRoot_L
│   ├── WingRoot_R
│   └── GroundContact
└── Renderers
```

Clips in-place: hover, fly, brake, perch_enter, perch_idle, surface_walk,
bite_start, bite_loop, detach, hit, fall y recover. Membrana y venas de alas
son materiales/renderers separables. La hitbox y el root autoritativo no
cambian entre LOD ni cosmético.

### Archivo de entrega de M1

Por prefab: versión, altura/longitud, bounds de render, lista de renderers,
Avatar válido, lista de clips con duración/loop, paths de anchors, hashes de
FBX/Blend y capturas frente/perfil/espalda. La inspección debe señalar clips o
huesos faltantes; no los completa silenciosamente en Presentation.

## 3. Modelador 2 → Presentation

Cada módulo de ambiente entrega:

- Prefab raíz con scale `(1,1,1)`, pivote y forward documentados.
- Renderers estáticos marcados `Contribute GI`; objetos móviles separados.
- UV2 sin solapamientos para cada renderer lightmapped y margen suficiente
  para 16 texels/m.
- Materiales URP compatibles, sin Standard/Built-in ni shader faltante.
- MeshCollider/BoxCollider en hijos separados de la malla visual.
- LODGroup en árboles, cercas repetidas y props grandes. Crossfade sólo si el
  material soporta el factor; de otro modo transición `None` para no duplicar
  overdraw.
- Flags por renderer: cast/receive shadows, reflection probe usage y light
  probe usage. Hojas pueden usar alpha clip; vidrio no proyecta sombra.
- Anchors vacíos `LightAnchor_*`, `ReflectionVolume_*`, `AudioZone_*` y
  `CameraCollision` donde la receta necesita ubicación, sin crear luces,
  probes, AudioSources o Volumes de Worker 2 dentro del prefab fuente.

Contrato de escena para casa/patio:

| Dato | Requisito |
|---|---|
| Bounds interiores | un volumen por planta y habitación |
| Portales | centro, ancho, alto y normal para cada puerta/ventana |
| Superficies | `Wood`, `Tile`, `Carpet`, `Grass`, `Stone`, `Metal` |
| Zonas acústicas | habitación, pasillo, escalera, patio, lobby |
| Occlusion | paredes/techos sólidos en `WorldStatic`; hojas y alas excluidas |
| Probes | anchors suficientes para malla tridimensional, puertas y patio |
| Repetidos | mismo mesh/material; sin clones de material por instancia |

M2 publica un scene manifest legible sin editar assets de Presentation. Worker
2 genera o instancia los roots de luces/audio mediante la herramienta del
Director.

## 4. Worker 1 → Presentation

Esta sección queda alineada con `docs/unity/gameplay/INTERFACES.md` del commit
`68353fe`; Director integra ambos documentos en la rama común.

Gameplay publica `GameSessionState` inmutable a 20 Hz desde una autoridad que
avanza a 30 Hz. Sus DTO Core usan `Float2`, `Float3` y `Rotation`, sin tipos
`UnityEngine`; el adaptador de Presentation hace la conversión. Presentation
consume estos campos ya fijados por Worker 1:

| Campo | Tipo/unidad | Uso visual |
|---|---|---|
| `ActorId`, `Role` | uint, enum | lookup, rig, cámara y audio |
| `Position`, `Velocity`, `BodyRotation` | m, m/s, quaternion | root visual/interpolación |
| `ViewForward`, `ViewRevision` | vector, uint | aim visual sin reconstruir rama angular |
| `LifeState`, `MotionPhase` | enums | locomoción, caída y recuperación |
| `Grounded`, `CrouchFraction` | bool, 0..1 | blend locomotor |
| `SurfaceAttachment` | ID/revisión, localPoint/normal, tangentForward | pose del mosquito sobre piso/pared/techo |
| `BiteAttachment` | víctima, superficie anatómica, localPoint/normal, poseRevision | ancla visual continua, sin marker |
| `StrikeState` | StrikeId, toolId, hand, phase, ticks, origin/target/normal | misma pose que resolvió el daño host |
| `RecoveryEndTick` | tick host | fase y timing de recuperación |

Presentation puede suavizar el root remoto y los huesos, pero no cambia estos
campos ni escribe de vuelta posición, hitbox, cooldown, extracción o resultado.
El actor local consume vista inmediata y reconcilia posición autoritativa sin
buffer largo. Remotos interpolan dos snapshots del mismo RoundId/StateRevision,
extrapolan como máximo `100 ms` y luego mantienen pose. Cambio de ancla, vida o
respawn corta interpolación y reproduce una transición segura.

Eventos de audio/VFX llegan como el `GameplayEvent` fijado por Worker 1:

```text
GameplayEvent
  SessionEpoch, RoundId, EventId, HostTick
  Kind: StrikeStarted | StrikeImpact | BiteStarted | BiteEnded |
        MosquitoKnockedDown | RecoveryStarted | HelpStarted | HelpEnded |
        Recovered | HumanFainted | DoorChanged | RoundEnded
  SourceActorId, optional TargetActorId, StateRevision, bounded payload
```

`StrikeImpact`, `MosquitoKnockedDown`, `BiteStarted` y `HumanFainted` sólo salen
después de confirmación autoritativa. Los clips de Animator y raycasts visuales
nunca los fabrican. Presentation descarta duplicados por
`(SessionEpoch,RoundId,EventId)` y reconstruye estado continuo desde snapshot.

Contrato de cámara de W1:

- La vista humana conserva yaw libre y pitch `-110°..+75°`. Presentation no
  reconstruye ángulos con `asin` ni introduce el flip de 180° al mirar piernas.
- `CameraCollision` contiene paredes, techo, puertas cerradas y terreno.
- Mosquito resuelve overlap inicial y dos barridos: ancla segura→pivot y
  pivot→cámara. Su cámara no determina dirección de vuelo; W1 consume input y
  publica la dirección autoritativa.
- El cuerpo local conserva colliders gameplay separados de huesos renderizados.
- `BiteAttachment` debe mantener el punto renderizado a `<=3 mm` del ancla
  anatómica resuelta por host mientras coincidan `PoseRevision` y snapshot.

## 5. Worker 2 → equipo

Worker 2 entrega:

- Presets URP/Volume/Material/Audio creados por APIs Unity y con `.meta`.
- Prefabs de rig de presentación que referencian el prefab fuente de M1 sin
  modificarlo.
- Roots aditivos de lighting/audio para escenas de M2.
- Animator Controllers, Avatar Masks y rigs de constraints propios.
- AudioMixer y snapshots de alfa, pools de AudioSource y mapeo de eventos.
- Capturas y perfiles asociados al commit exacto.

Presentation debe tolerar que audio esté deshabilitado, que un clip opcional
falte y que un actor aparezca tarde. Un recurso crítico faltante falla el gate
de contenido con path/ID exacto; no lanza una excepción dentro de una ronda.

## 6. Gates compartidos

- Frente, perfil y espalda de humano/mosquito, idle y locomoción.
- Humano local ve torso, brazos, piernas y pies sin interior de cabeza, clipping
  de cámara ni giro bloqueado.
- Mosquito mantiene cámara fuera de pared/techo y sigue visible al posarse.
- Puerta abierta/cerrada, escalera, una habitación y patio sin fugas de luz,
  z-fighting ni objetos flotantes.
- Alas visibles sobre superficies claras y oscuras, sin ordenar por delante de
  pared opaca.
- Eventos autoritativos suenan una vez; replay y late join no los duplican.
- Ningún material/shader rosa, ningún AudioSource sin mixer group y ningún
  renderer con material instanciado por frame.
- Performance cumple el protocolo de `PERFORMANCE-BUDGET.md` o se registra el
  desvío con captura del profiler antes de cambiar el preset.
