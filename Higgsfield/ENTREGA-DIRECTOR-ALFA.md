# Entrega de Director a Encargado de Higgfield

## Mandato y cierre real

Alfa **congelada por orden expresa del usuario**, no aprobada técnicamente.
La parada, documentación y actualización de GitHub están terminadas. No queda
producción activa a cargo del Director ni hace falta completar los defectos
para reconocer esta parada. Se transfieren como pendientes, no como resueltos.

Encargado de Higgfield comunicó el relevo temporal autorizado por el usuario y
su plan. Director entrega contexto y no dirigirá ni asignará en paralelo la
actualización. El coordinador entrante organiza las tareas existentes, turnos,
integración y revisiones; devuelve el mando al terminar con evidencia explícita.
Este documento no reabre por sí solo las asignaciones antiguas del alfa.

Documento en N: por la instrucción del usuario de trabajar siempre en ese disco.
Plan recibido: `C:/Users/brank/OneDrive/Documentos/ChatGPT/Let me sleep/Higgsfield/`.
No se movieron ni editaron los archivos del coordinador entrante en esa carpeta.

## Base recuperable

- Repositorio: `N:/LetMeSleep/Repository`, remoto `https://github.com/Sauri0/LetMeSleep.git`.
- Rama: `codex/unity-094-alfa`.
- Cierre verificado local/remoto: `67bfd94cd223a3985e7f45d6b0b0badfa7a5c18a`.
- Última fuente funcional integrada previa al cierre documental: `5c82b12`.
- Proyecto: `N:/LetMeSleep/Repository/unity`.
- Unity `6000.3.24f1` (6.3 LTS), URP `17.3.0`, Windows D3D11.
- Editor: `N:/Unity/Editors/6000.3.24f1/Editor/Unity.exe`.
- Último binario local: `0.9.4-alfa.3`, fuente `e9d15e7`,
  `N:/LetMeSleep/Artifacts/alfa-20260913-000715`.
- Paquete, checksum y diferencias respecto a la fuente: ver
  `docs/unity/ALFA-FROZEN-20260913.md`. La prerelease remota observada sigue siendo
  `v0.9.4-alfa.2`. No existe ejecutable publicado con todas las correcciones actuales.
- No mezclar builds, capturas y tests de esas tres bases como una sola versión.

## Fuentes y pipeline

Rutas siguientes relativas a `N:/LetMeSleep/Repository`:

| Área | Fuente / integración |
|---|---|
| Humanos | `art_source/unity/characters/human/LMS_Human_alpha.blend` y `.fbx`; menú separado en `characters/menu/LMS_HumanMenu.blend` y `.fbx`. |
| Mosquitos | `art_source/unity/characters/mosquito/LMS_Mosquito_alpha.blend` y `.fbx`. |
| Herramienta actual | `art_source/unity/characters/flyswatter/LMS_Flyswatter_alpha.blend` y `.fbx`. Históricamente está con personajes; coordinar traspaso a Elementos sin duplicar. |
| Entorno | `art_source/unity/environments/alfa_maps/`: house_alfa_static, lobby_alfa_static, furniture_kit_alfa en blend/FBX. RoomSample es referencia previa, no reemplazo automático del mapa vigente. |
| Personajes Unity | `unity/Assets/LetMeSleep/Content/Characters/{Models,Materials,Controllers,Prefabs}`. Prefabs LMS_Human, LMS_Human_FirstPerson, LMS_HumanMenu, LMS_Mosquito, LMS_Flyswatter. |
| Mapas Unity | `unity/Assets/LetMeSleep/Content/Environment/AlfaMaps/{Models,Prefabs,Scenes}`: HousePatio y PrivateLobby separados. |
| Presentación | `unity/Assets/LetMeSleep/Presentation/`, en especial Gameplay/ActorVisualBinding.cs, Runtime/CharacterVisualPresenter y cámaras/atención; audio en su módulo actual. |
| UI y entrada al juego | `unity/Assets/LetMeSleep/UI`, `Bootstrap/AlfaApplication`; bootstrap/build de Editor en `unity/Assets/Editor/ProjectBootstrap`. |
| Online | `unity/Assets/LetMeSleep/Online/OnlineGameplaySession.cs`, Core/RoomSession.cs y RoomWireCodec.cs. |

Los scripts de autoría/auditoría de personajes todavía tienen puntos compartidos
entre especies: leer los cambios de Humanos/Mosquitos antes de ejecutar un builder
global. `characters/candidates/` contiene ensayos; no elegir el archivo más reciente
como canónico sin su recibo. El canónico humano fue promovido en `1aff7ca`.

FBX humanos actuales: SHA-256
`ccb0f1f92918ea52e738719190be934d7ec14843b4b6206284bd4cb89895297b`.
HumanMenu: `44738cda7b3cba4baa7b4e0b68e40acc1eda30a0bed701a153ebd0348aa3678c`.
Mosquito: `2f73f7f08c3fba4191cc08b46fcf4547d5b3acc1a7e6ecf36c98974649ed8d06`.

Builder real: `LetMeSleep.Content.Characters.Editor.CharacterContentBuilder.BuildAll()`.
Genera importación/prefabs/controladores/BuildReceipt; no sólo copiar un FBX.
**Reimportar un FBX conocido invalida los cuatro VisualAttentionContracts.**
Después ejecutar `LetMeSleep.Editor.FacialContentBuilder.BuildAll(AlfaApplication)`
y verificar recibos; no editar hashes manualmente ni desactivar esa validación.
Shaders/normales importados tienen corrección de párpados que hay que revisar con
el modelo nuevo. Conservar GUID en reemplazos controlados; nuevos recursos tienen
sus propios .meta. Escenas/prefabs/settings mediante Unity APIs, no edición masiva YAML.

## Contratos que deben consultarse antes de reemplazar modelos

- Metros Unity, Y arriba/Z adelante. Contrato de colisión actual: humano radio
  0.25 m, altura 1.72 m, ojo 1.53 m, agachado 1.0 m; mosquito radio 0.055 m.
  Son parámetros funcionales, no una instrucción de escalar visualmente a ciegas.
  Los tamaños artísticos históricos variaron: medir prefab y cámara canónicos.
- `CharacterView.cs` contiene VisualRoot, HitVolume, HeadRenderers, anclajes con
  SourceBone/RotationOffset, colores por Renderer/material/category y Motions con
  IDs estables. La autoridad de movimiento/daño es Gameplay, no el mesh.
- Builder `Content/Editor/Characters/CharacterContentBuilder.cs` contiene la lista
  exacta de sockets. Entre ellos humanos: CameraEye/Socket.Eye, AimChest,
  HandGrip_L/Socket.Grip.L y HandGrip_R/Socket.Grip.R; mosquito ProboscisTip/
  Socket.Mouth, WingRoot_L/R y GroundContact. Preservar o migrar todos los enlaces,
  no sólo esos ejemplos. Animator sin root motion aplicado.
- ToolView expone ToolId, Grip, Impact, HeadRadius y GripToImpact. Defaults actuales
  flyswatter / 0.085 m / 0.365 m; medir el asset real y mantener colisión/agarre
  coherentes. El defecto de alcance demuestra que un socket válido no basta.
- Humano actual: 65 huesos, 17 movimientos; IDs 0..14 conservados, 15 WalkSlow y
  16 Trot. WalkSlow/Walk/Trot/Run son cuatro loops de 2 s; reloj de locomoción
  por desplazamiento horizontal/longitud real del clip, compartido con pasos.
  No reemplazarlo por acelerar audio independientemente del pie.
- Revisar `ActorVisualBinding`, `HumanLocomotionPresenter`, `HumanViewCamera` y
  `VisualAttentionRig/Contract`: varios escritores sobre huesos requieren orden
  y exclusión entre locomoción, golpe, picadura, mirada y futura física.
- Mirada solicitada: personaje humano o mosquito cercano dentro de radio 2 m y
  visión frontal, ±60 grados; cambia objetivo sólo si otro permanece más cerca
  2 s continuos. Sin candidato: mira hacia la orientación del jugador con movimiento
  y parpadeo natural. No ojos mirando detrás de la cabeza. Oclusión no validada.
- Menú: ambos avatares usan personalización del usuario; mosquito vuela/bate alas,
  humano sentado sigue con mirada y trata de golpear al acercarse. Animación de
  video o desplazamiento del root solos no cumplen ese comportamiento.
- Personalización actual: categorías Skin/Pajamas/Mosquito en CharacterView;
  snapshots de OnlineGameplaySession serializan CosmeticProfileId y SpawnId.
  Revisar codec, validaciones y perfiles de bootstrap antes de ampliar IDs; no
  asumir que el catálogo de accesorios de los bocetos ya está implementado.
- Online exige coherencia de estado autoritativo/contenido entre miembros.
  El borrador de nueva calibración de golpe requiere ContentHash propio y bootstrap;
  no instalar sus partes aisladas. RagdollPoseCodec/Gate están desconectados de sesión.

## Equipo y pendientes

Inventario de tareas/IDs/carpetas: `docs/unity/TEAM-RECOVERY-20260912.md`.
Todos los workers cargados quedaron idle tras la orden de parada. Online,
Elementos y Terreno/Mapas figuraban notLoaded. Revisor Funcional idle; algunas
de esas tareas devolvían thread not found al intentar mensajería anteriormente.
No se crearon reemplazos. Higgsfield queda a cargo del usuario y no fue detenido.

| Dueño | Pendiente conservado / cómo comprobar |
|---|---|
| Gameplay | Golpe agachado, alcance real, contacto contra pared y movimiento durante ataque. Probar humano real de pie/agachado y comparar barrido Authority con mano/Impact, no sólo largo de huesos. Borrador SIN COMMIT en worktree gameplay, rama codex/gameplay-rig-strike-contract, base9b214e8; STOP-CHECKPOINT-20260913.md allí. |
| Humanos | Clips importados y auditoría numérica preservan geometría/rig/13 acciones anteriores; falta observación continua de crossfades, brazos/ropa/apoyos. Último worker773a341 integrado. |
| Mosquitos | Ragdoll v4 falla separación55.1 mm y reposo. Host de subpasos/entorno SIN COMMIT en worktree mosquito-physics sobre757a587. No probado ni integrado, no activarlo por ser más nuevo. |
| Presentación/Audio | Transiciones/cámara/pasos/zumbido requieren prueba real. bf59d3a test de orden efectivo de cámara permanece sólo en rama worker. |
| UI | Helper de validación en N:/LetMeSleep/Validation/UI-NativeHudSettings-20260913; falta 720/1080, persistencia y reapertura. No rediseño aprobado por esas comprobaciones. |
| Online / integrador entrante | WAN con amigo pendiente; codec/gate de física tienen pruebas aisladas pero faltan sesión/negociación/ragdoll remoto. Humano físico aún sin implementar. |
| Revisores | Evidencia en N:/LetMeSleep/Validation/TeamRecovery/{animation,visual,stability}; también N:/Validation/TeamRecovery/stability. Cotejar mismo commit/build y alcance de cada PASS. |

Último intento PlayMode de locomoción **no ejecutó tests**: error de compilación
por referencias faltantes Content.Characters y Presentation.Gameplay en
`Tests/PlayMode/LetMeSleep.Tests.PlayMode.asmdef`. Se dejó sin corregir por STOP.
Log: `N:/LetMeSleep/Validation/alfa3-corrections-20260913/locomotion-transitions.log`.

Evidencia existente: superficies 8/8 PhysX, picadura 6/6, cámara mosquito 5/5,
bloqueo de entrada 104 CPU, protocolo aislado 9/9; son bancos acotados, no aceptación
de juego online. A/B facial estático corrigió triángulos de mentón/cachetes, pero
no aprueba toda animación. Las cuatro pruebas físicas de caída fallaron.
Lista completa y todos los nueve reportes del usuario:
`docs/unity/ALFA-FROZEN-20260913.md` y `docs/unity/qa/USER-GAMEPLAY-CORRECTIONS-20260913.md`.

## Recursos liberados y reglas

Al comprobar esta entrega no había Unity, Blender ni player de LetMeSleep activos.
No hay turno reservado por Director, generaciones ni trabajos asíncronos propios.
Los workers activos de Gameplay/Mosquitos confirmaron parada y ausencia de procesos.
El coordinador entrante debe comprobar de nuevo antes de abrir escena porque el
usuario puede estar trabajando. No terminar procesos ajenos por nombre genérico.

Usar un único turno de Unity/player/Blender nativo coordinado, mantener WIP en
worktrees, commits selectivos; nunca reset/clean/add-all. No exponer configuración
privada EOS ni credenciales en commits, logs públicos o mensajes.

Monitor visible principal: DISPLAY1 (0,0,1920,1080); no el vertical DISPLAY2
(-1080,-309,1080,1920). Para banco: -monitor 1, ventana1920x1080. No imponerlo
a todos los jugadores. Validación manual aislada puede usar
`--lms-validation-data N:/.../userdata` en Editor/Development build; no confundir
con BuildProbe, que automatiza resolución/capturas/salida. Flag todavía sin
prueba de persistencia completa.

Restricciones: Let me sleep, pijama/pantuflas/gorro y noche doméstica; imágenes
Bite & Build son referencias, no autorización de armas/modos/crafting/pesca.
Tres vidas personales en Tareas, desmayo humano al completar extracción; balance
se ajusta con online real. Sin marcadores de picadura; defensa humana manual;
W de mosquito según dirección de mirada. Lobby privado 3D, códigos, roles
aleatorios por ronda, sin ratio2:1. Al menos cinco mapas para el ciclo general;
alfa casa/patio y lobby aparte. Nuevos mapas no se certifican por un render.

Los nueve defectos del usuario siguen siendo deuda funcional aunque se renueven
los meshes. Revisar sobre el piloto integrado antes de escalar producción.
La evidencia gráfica anterior corresponde a RTX3060Ti; no hay certificación
de FPS en otra GPU ni prueba WAN. No se reinicia producción desde Director.
