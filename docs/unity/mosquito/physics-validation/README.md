# Módulo físico independiente del mosquito R4

Entrega desde `53722687b8da5d3477b0734bf7ddd2ffdcefe83c`, rama `codex/unity-mosquito-physics`. El trabajo artístico anterior permanece en `codex/unity-mosquito-specialist` / `42f67d5`. Director integra. Sólo se añaden cuatro scripts y sus `.meta` en `Presentation/Physics`, más esta receta externa. No hay conexión con Gameplay, Authority, protocolos, ActorVisualBinding, prefab, colliders existentes, asmdefs o ajustes globales.

**Comprobado:** compilación Roslyn del módulo y de la prueba externa con referencias reales de Unity **6000.3.24f1**, C#9 y advertencias como errores. **No ejecutado:** Unity, contactos, sueño, límites, reproducción de clips de entrada, revisión visual ni red. La prueba de abajo está preparada para el turno de motor del Director; no constituye un PASS físico.

## Contenido y unidades

- `MosquitoRagdollBuilder`: valida R4 y obtiene bind mundial desde `SkinnedMeshRenderer.sharedMesh.bindposes`, incluso si el esqueleto ya está animado. Recupera el marco fuente mediante huesos deformantes. Rechaza escala distinta de .5 uniforme, huesos ausentes y piernas incompatibles. No requiere bindposes de sockets sin pesos.
- Crea una raíz auxiliar sin padre, posición0, rotación identidad, escala1, en la misma escena que el visual. Sus **18 Rigidbody / 17 ConfigurableJoint / 28 colliders** conservan escala mundial1; el skin sigue en su jerarquía original escalada. Masa total inicial .060kg, escalable por instancia. Alturas, radios, COM e inercia se calculan en metros finales.
- Thorax libre; Head, dos segmentos de abdomen, dos alas, seis muslos y seis segmentos distales articulados. Traslaciones bloqueadas, límites angulares, sin drives de pose ni proyección. Rodillas con flexión sobre el producto cruzado de sus segmentos reales: ambos lados usan flexión positiva que pliega. Las alas usan una caja ajustada al plano de los vértices R4; radios/contact offset y límites siguen siendo valores iniciales para medir.
- Probóscide y seis tarsos forman colliders compuestos. Al soltar, sus offsets se actualizan desde la pose capturada antes de recalcular COM/inercia. Root y siete Socket.* no llevan cuerpos. Los seis huesos faciales no se escriben ni se restauran desde este módulo.
- Pose inmutable `Mosquito-R4-18-v1`: 18 poses mundiales en orden `MosquitoBodyId`, y15 poses locales auxiliares en el orden de `GetAuxiliaryName`. Posición en metros, velocidad en m/s **en el origen del hueso**, velocidad angular mundial en rad/s. Se convierte a/desde velocidad del COM. Los arrays de entrada se copian y se rechazan rotaciones nulas y valores no finitos. El transporte es responsabilidad externa.

## Uso explícito

```csharp
// El llamador elige gravedad, capa y la instancia con autoridad; Build no inicia la caída.
var rig = MosquitoRagdollBuilder.Build(visualRoot, animator,
    new MosquitoRagdollSettings(new Vector3(0, -12, 0), ragdollLayer));

// Al terminar una evaluación de animación, guardar una muestra anterior.
var previous = rig.CaptureAnimatedPose(actorVelocity, actorAngularVelocity, actorPosition);
// Tras la siguiente evaluación, el intervalo REAL de ambas muestras incluye movimiento del actor y clip.
var release = rig.CaptureAnimatedPose(previous, elapsedSampleSeconds);
// Con los demás escritores y el motor ya suspendidos por integración:
rig.BeginLocalSimulation(release);
rig.ApplyImpulse(MosquitoBodyId.Thorax, impulseNewtonSeconds, worldHitPoint);
var snapshot = rig.CapturePose();

// En OTRA instancia remota, preparada por su integrador:
remoteRig.BeginRemotePose(snapshot);
remoteRig.ApplyRemotePose(interpolatedSnapshot);

var recoveryStart = rig.HoldPose(); // Captura, detiene fuerzas/contactos; mantiene el skin en esa pose.
rig.RestoreAnimation(); // Restaura locales previos y Animator.enabled previo; no mezcla ni decide recuperación.
rig.Dispose(); // Idempotente: colisión inerte inmediatamente, objetos destruidos al terminar el frame.
```

Si sólo existe una muestra, la primera sobrecarga transfiere `v + ω×offset` del actor, sin inventar velocidad articular de clips. La de dos muestras exige .0001–.25s y usa la rotación relativa de arco corto; evitar muestras separadas por teleport, cambio de clip discontinuo o un ciclo angular ambiguo. `ApplyImpulse` recibe **N·s**, una sola vez en un solo cuerpo. El valor actual de KnockDown llamado impulse es Δvelocidad y no se debe pasar directamente.

Estados: Prepared → LocalSimulation o RemotePose → Held → RestoreAnimation → Prepared. La restauración también es válida directamente desde local/remoto. Cambiar de dueño requiere restauración explícita; se puede conservar antes el snapshot. Remoto mantiene proxies cinemáticos, colliders deshabilitados y ninguna gravedad/impulso. No escoge host, interpola paquetes ni modifica autoridad.

Local usa gravedad explícita por instancia con `useGravity=false`, amortiguación .02/.15, solver12/4, contactOffset .0005m, CCD especulativo y velocidad angular máxima80rad/s. Esto último limita velocidad en pasos físicos; revisar las velocidades heredadas de alas rápidas. Sueño explícito tras todos los cuerpos con velocidad<.02m/s y giro<.3rad/s durante.35s, más contacto con entorno. No depende de duración de clips. Segundo impacto despierta la cadena; perder un soporte eliminado/deshabilitado evita reposo en el aire. Validar soportes móviles y falsas separaciones de contacto en Unity.

## Condiciones de integración

1. Suspender motor, corrección por superficie/Mouth y cualquier escritor corporal, incluido ActorVisualBinding y el canal Head de VisualAttentionRig. Este módulo sólo desactiva el Animator recibido y después devuelve su estado. Puede mantenerse el canal de los seis huesos faciales. El orden LateUpdate1150 precede al driver facial1200; CharacterView1000 debe refrescar sus lectores de sockets después de la escritura física si los necesita en ese mismo frame. Revisar bounds de render y posición del actor/cámara si el skin se aleja de la raíz visual.
2. Separar física del motor y consultas de juego. **Todas** las formas nuevas se identifican mediante `MosquitoRagdollPart.TryGet(collider, out part)`, también hijos compuestos. La capa la decide el llamador. IgnoreRaycast no basta para las consultas actuales `~0`: recorrer resultados y escoger el primero permitido, o implementar una exclusión equivalente que no convierta el collider descartado en una obstrucción. Integración decide si deshabilita/ignora el collider motor existente. El módulo sólo ignora pares entre sus propias formas nuevas.
3. El llamador decide recuperación, espacio libre, reubicación y mezcla desde `recoveryStart`; no lanzar Recover R4 como si cualquier pose caída coincidiera con su inicio fijo. `RestoreAnimation` devuelve la pose local anterior a la caída y puede producir un salto visible si se usa como mezcla. No hay poses objetivo ni correcciones de contacto durante la simulación pasiva.
4. No reparentar/escalar/mutar los objetos auxiliares obtenidos por getters. No desactivar el componente para pausar: `OnDisable`, destrucción del visual y `Dispose` limpian los auxiliares; construir un módulo nuevo al reactivar. `HoldPose` sirve para conservar una pose con el componente activo.

## Compilación reproducible sin motor

Ejecutar desde este worktree, sin abrir Unity:

```powershell
& ./docs/unity/mosquito/physics-validation/Compile-Offline.ps1
```

Compila únicamente los cuatro scripts del módulo y la prueba de esta carpeta. Referencias NUnit/TestRunner de la importación central ya existente; no restaura paquetes ni abre procesos nativos de motor. Deja DLLs, logs y `result.json` con hashes de fuentes en `N:/LetMeSleep/Validation/MosquitoPhysics-20260912/compile`. No modifica assets centrales. Se necesita importación normal del Director para verificar ensamblado completo y serialización Unity.

## Prueba PlayMode pendiente para Director

Copiar **sólo cuando corresponda el turno de pruebas** `MosquitoRagdollPlayModeProof.cs` a la carpeta de la asamblea PlayMode existente y dejar que Unity genere su `.meta`; no requiere cambiar asmdefs. Ejecutar en Editor PlayMode el test `LetMeSleep.Tests.PlayMode.MosquitoRagdollPlayModeProof.R4TransfersArticulatesContactsRestoresAndCleansUp`, con `-noaudio`. Si hay captura, usar exclusivamente DISPLAY1 horizontal. No usar este archivo como comportamiento de producción.

La prueba carga el prefab real `LMS_Mosquito`, inactiva sus escritores/colliders dentro de la instancia sintética y crea una PhysicsScene aislada con suelo. Construye desde una pose de Head fuera de bind, mide transferencia de posición/rotación/velocidad y COM, deja caer con impulso descentrado durante10s, registra contacto, sueño, separación máxima de joints, altura mínima y articulación relativa. Comprueba modo remoto sin fuerzas/contactos, preservación facial, restauración de ambos estados del Animator, limpieza idempotente y desactivación del dueño durante simulación local. Siempre descarga la escena de prueba.

El umbral inicial de separación12mm sólo detecta roturas grandes y debe estrecharse con medición real; no es aceptación visual. La prueba requiere contacto, sueño y articulación medidos para pasar. Si falla, conservar el log y corregir a partir de él: no ampliar tolerancias para ocultar penetraciones. Quedan fuera de esta receta: pared/techo/muebles, clips completos al soltar, límites angulares medidos, apoyos móviles, golpes extremos, entrada/salida gameplay, oclusión de queries, cámara, recuperación obstruida y coherencia de host/remoto por red.

Fuente R4 esperada en esta base: SHA256 FBX `2f73f7f08c3fba4191cc08b46fcf4547d5b3acc1a7e6ecf36c98974649ed8d06` (39 huesos, 3 skins). Los parámetros siguen la propuesta anterior y las APIs oficiales de Unity: [ConfigurableJoint](https://docs.unity3d.com/6000.3/Documentation/Manual/class-ConfigurableJoint.html), [estabilidad de ragdolls](https://docs.unity3d.com/6000.3/Documentation/Manual/RagdollStability.html), [cálculo de inercia](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody.ResetInertiaTensor.html). Esas referencias no sustituyen la prueba física pendiente.
