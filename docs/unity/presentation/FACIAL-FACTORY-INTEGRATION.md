# Instalación facial explícita — Bootstrap, gameplay y preview

Factory entregada en Presentation/Runtime. No dependencia UI→Presentation ni Gameplay.Unity→Presentation. No instala por nombres de huesos ni activa fallback de rig viejo.

## Contrato que debe escribir el builder Director

Agregar `VisualAttentionContract` **al mismo GameObject raíz de CharacterView** del prefab nuevo. Sólo marcador, no preinstalar driver. Campos obligatorios:

- `Schema = VisualAttentionContract.SupportedSchema` (`lms.visual-attention.v1`).
- `RigRevision`: revisión exacta del candidato; `SourceSha256`:64 caracteres hex del artefacto certificado. Se valida formato, no se rehashea un FBX en runtime.
- `UnityAxesVerified=true` únicamente después de verificar ejes/pivotes importados. No copiar ejes Blender por supuesto; mientras estéfalse la instalación se omite explícitamente.
- `Rig`: `VisualAttentionRig.Bindings` con referencias serializadas al Head, ojos/pupilas nuevos, ejes, límites y párpados reales. Ambos ojos requeridos. Human4morphs porlado o mosquito BlinkBone[] completos; falta de Blink usable rechaza instalación.
- `LegacyScaleBlinkVerified`:false por defecto. Sólo true con evidencia del canal legado y conversión; requerido si Rig.ReadLegacyEyeScaleBlink=true. No fallback silencioso al escalar ojos.

Nombres concretos/pivotes/ejes/nuevosmeshes pertenecen a los contratos de Humanos/Mosquitos. Campos sin llenar rechazan. El marcador es una declaración de integración del builder, no prueba visual ni detector criptográfico de geometría. Si se cambia el mesh/rig hay que revalidar y regenerar marcador.

## API para Bootstrap

`bool VisualAttentionFactory.TryInstall(GameObject visual, bool manualEvaluation, out VisualAttentionRig controller, out string reason)`.

Una instalación por instancia. Repetición de la misma instancia/modo devuelve el controlador de propiedad del marker sin reconfigurarlo ni reiniciar blink. Otro driver descendiente o un cambio de modo rechaza, no sobrescribe. En fallo devuelve razón explícita y elimina el driver parcial si lo creó. No crea marcador ni guesses. Caller registra `LMS_FACIAL_SKIPPED` con motivo; no afirmar requisito cumplido.

Menú: crear visuales con root inactivo, instalar ambos con manualEvaluation=true y pasar resultados a `MainMenuLivingScene.Bindings.HumanAttention/MosquitoAttention`. Si falla cualquiera, señalar contenido facial pendiente; no tratar aparición de modelos como aceptación. Shared menu sequence invoca único Prepare→graphEvaluate→facial. No añadir otro LateUpdate facial.

## Consumers implementados

GameplayVisualPresenter.EnsureVisual instala una vez tras Initialize del visual. GameplayAttentionTarget pertenece al clone y se destruye con él; LateUpdate1150 fija objetivo, VisualAttentionRig1200 escribe finalmente. Local usa GameplayRuntime.LocalViewForward; remoto usa proxy.State.ViewForward aceptado; punto visual ocho metros desde LookOrigin. No selecciona blancos ocultos ni añade red. Verifica proxyactual en World y epoch/round capturados. Bind/newWorld limpia visuales propios; ActorId reusado con distinto proxy recrea visual, no reutiliza su driver. No modifica ActorVisualBinding (central incluye orientación de superficies), autoridad, collider ni cámara.

LobbyVisualPresenter instala en HandleVisualCreated y recupera explícitamente visuales que existían antes de suscripción desde primera SnapshotApplied/TryGetVisual. Mismo clone no duplica componentes. Pose visual actual aporta yaw interpolado; no se inventa pitch remoto. Epoch/player/instancia vivos deben coincidir. Unsubscribe desactiva target y shared rig para dejar de escribir; reSubscribe reutiliza con nueva identidad explícita. Runtime conserva propiedad/destrucción de sus clones.

## Preview sin dependencia nueva UI

`bool VisualAttentionFactory.TryInstallPreview(GameObject visual, Camera camera, out string reason)` instala modo automático y un PreviewAttentionTarget propio. Sigue cámara sólo en sector frontal (dot forward>.25); perfil/espalda limpian objetivo y dejan parpadeo. Root oculto detiene LateUpdate, OnDisable restaura facial; clone destruido elimina todos los componentes.

UI propuso `Action<GameObject,Camera>` opcional en CharacterPreviewSetup, inyectada por Bootstrap e invocada una vez tras Instantiate en CharacterPreviewOrbit.Show. Director autoriza seam; este worker NO edita UI. Lambda de Bootstrap llama TryInstallPreview y registra motivo de omisión. Reutilizar mismo rol, guardar/aplicar colores o mostrar de nuevo no requiere reinstalar. Bind/cambio de rol destruye clone viejo normalmente. Sin callback/marker no hay claim facial.

## Verificación y límites

Compilación offline de factory, marker, driver, consumidores reales, ActorVisualBinding y cámaras contra Unity6000.3.24f1+ensamblados centrales: cero errores/advertencias. Archivos de consumidor comparados con central antes de editar: eran iguales. No se reemplaza contenido central ajeno ni se edita Bootstrap/importer/asmdefs/UI. No Unity/Blender.

Pendiente prueba nativa de markers ausentes/invalidos, segunda instalación, rig humano/mosquito nuevo, local/remotos, primer snapshotcliente lobby, cambio de epoch/round/roster, ocultar/volver/destruir, previewfrente/perfil/espalda y personalización. No afirmar controladores instalados hoy: autores aún deben certificar ejesUnity y Director configurar prefabs. Native verification debe contar un writer por clone y confirmar blink/ojos visibles sin deformación/globoatravesado. Compilación no valida ejes ni pose.

Gameplay confirmó readonly contra central30fd420: BeginRound siempre recrea proxies (UnityGameplayWorld49–61), desactiva roots antiguos y limpia World.Actors; Bootstrap además recrea runtime/presentation. Cliente captura identidad al primer snapshot válido. SetRoster con revisión nueva recrea visuales; misma revisión/misma membresía es NO-OP. Por eso mismatch de epoch/round limpia objetivo sin rebind silencioso de instancia vieja. Pooling futuro requeriría otro contrato explícito.
