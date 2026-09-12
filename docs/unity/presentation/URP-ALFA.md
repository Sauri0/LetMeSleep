# Receta URP para la muestra alfa

## 1. Pipeline y cámara

El preset predeterminado se llama `AlfaReference1080p`. Usa un Universal
Renderer en ruta **Forward**, `renderScale=1.0`, HDR activo, SRP Batcher activo,
Dynamic Batching desactivado, Opaque Texture desactivada y Depth Texture activa
porque el SSAO la consume. Depth Priming queda en `Auto`. El proyecto usa color
space `Linear`.

Forward es suficiente para una escena con una luz principal y hasta cuatro
luces locales por objeto. El equipo sólo cambia a Forward+ si un perfil real
demuestra que necesita más luces por objeto; Forward+ hace todas las luces por
píxel y elimina ese límite, pero no es una mejora gratuita. Unity documenta las
diferencias en [rutas de render de URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/rendering-paths-comparison.html).

Antialiasing predeterminado: MSAA desactivado y SMAA `High` en cada cámara de
juego. TAA queda fuera de alfa porque el historial temporal puede dejar estela
en alas pequeñas, patas y giros rápidos. El perfil de respaldo usa SMAA
`Medium`. Unity describe SMAA como más nítido que FXAA en su
[guía de antialiasing](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/anti-aliasing.html).

Volumen global común:

| Override | Valor inicial |
|---|---:|
| Tonemapping | ACES |
| Post Exposure | 0 EV |
| Contrast | +4 |
| Saturation | -3 |
| White Balance Temperature | -5 |
| Bloom | intensidad 0.08, threshold 1.15, scatter 0.55 |
| Vignette | intensidad 0.10, smoothness 0.30 |
| Motion Blur | desactivado |
| Depth of Field | desactivado durante juego |
| Chromatic Aberration | desactivado |
| Film Grain | desactivado |

El brillo se corrige en luces, skybox y materiales. No se usa exposición
automática, porque cambiaría la lectura del mosquito al pasar entre habitación
y patio.

## 2. Iluminación nocturna

La escena usa Baked Global Illumination. Las luces prácticas de techo y pared
son `Baked`; el personaje móvil recibe su rebote mediante Light Probes. Unity
explica que las luces horneadas no añaden cálculo de luz en runtime y que los
objetos móviles necesitan probes para recibir ese rebote en
[Baked lighting](https://docs.unity3d.com/6000.3/Documentation/Manual/LightMode-Baked.html)
y [Light Probes para objetos móviles](https://docs.unity3d.com/6000.3/Documentation/Manual/LightProbes-MovingObjects.html).

Valores iniciales de la casa:

| Elemento | Configuración |
|---|---|
| Luz principal | Directional, Mixed/Shadowmask, color `#A9BFE6`, 1.0 lux, Soft Shadows |
| Sombras principales | atlas 2048, distancia 28 m, 2 cascadas, split 0.35, last border 0.10 |
| Bias inicial | Depth 0.7, Normal 0.35; ajustar por acne o separación visible |
| Soft Shadows | activas, calidad Medium |
| Luces de habitación | Baked, color 2700–3200 K, 350–650 lm por artefacto |
| Luz local dinámica | máximo 2 visibles, sin sombras, rango 5.5 m, sólo si probes no dan lectura suficiente |
| Sombras adicionales | desactivadas por defecto |
| Environment Lighting | Skybox, intensidad 0.32, reflejo 0.40 |

Sólo la luz principal genera sombras dinámicas en la configuración base. Una
luz puntual con sombra se admite temporalmente para un momento de gameplay si
el profiler demuestra que el presupuesto se conserva. El costo depende de
distancia, cascadas, cantidad de casters, resolución y filtro, según
[optimización de sombras URP](https://docs.unity3d.com/6000.3/Documentation/Manual/shadows-optimization.html).

Light Probes:

- Tres alturas por planta en zonas transitables: `0.35 m`, `1.25 m`, `2.20 m`.
- Separación base `2.5 m`; reducir a `1.25 m` en puertas, escalera, cambios de
  color o paso interior/exterior.
- Patio: alturas `0.40 m`, `1.60 m`, `3.20 m`, con una corona exterior que
  encierre el volumen de vuelo.
- Nunca dejar una nube coplanar. Debe existir volumen tetraédrico por encima y
  por debajo de las alturas de personajes.

Bake inicial: Progressive Lightmapper, Directional lightmaps, 16 texels/m,
atlas máximo 2048, 2 bounces, compresión High Quality. El bake final registra
tiempo, cantidad/tamaño de lightmaps y hash del scene commit; estos valores son
un punto de partida, no evidencia de calidad sin captura.

## 3. Reflejos y contacto

Reflection Probes horneados, HDR, resolución 128, box projection activa y blend
distance `1.0 m`:

- Uno por planta, ceñido al interior de la casa.
- Uno para patio/porche, con `BlendProbesAndSkybox` en renderers exteriores.
- Uno en lobby, separado del mapa jugable.
- Objetos que cruzan dos volúmenes usan `BlendProbes`; no se usan probes
  `Realtime` en alfa.

URP calcula la influencia por píxel dentro del volumen y por distancia de
mezcla, como documenta [Reflection Probes en URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/lighting/reflection-probes-introduction.html).

SSAO es el contacto visual de muebles, pies y zócalos. Renderer Feature:
`Interleaved Gradient Noise`, `Source=Depth`, `Downsample=true`, intensidad
`0.35`, radio `0.22 m`, direct lighting strength `0.15`, normal quality `Medium`.
El perfil de respaldo lo desactiva. El radio es el control de mayor costo en la
[referencia de SSAO](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/ssao-renderer-feature-reference.html).

## 4. Materiales

El entorno usa paleta compartida y pocos shaders:

| Familia | Shader | Metallic | Smoothness | Regla |
|---|---|---:|---:|---|
| Pared pintada/yeso | URP Simple Lit | 0.00 | 0.08 | superficies grandes limpias |
| Madera pintada | URP Simple Lit | 0.00 | 0.18 | veta sólo donde cambia silueta/uso |
| Madera barnizada | URP Lit | 0.00 | 0.30 | puertas y tapas cercanas |
| Cerámica/teja | URP Simple Lit | 0.00 | 0.20 | color por material compartido |
| Metal | URP Lit | 0.75 | 0.52 | herrajes; área pequeña |
| Tela/pijama | URP Simple Lit | 0.00 | 0.10 | sin clear coat |
| Vidrio | URP Lit Transparent | 0.00 | 0.72 | sólo ventanas necesarias |
| Alas | shader URP simple transparente | 0.00 | 0.28 | Premultiply, doble cara, sin shadow casting |

Alfa usa Opaque siempre que la silueta lo permita y Alpha Clipping para hojas,
césped y detalles recortados. Transparencia queda reservada a vidrio y alas.
Las alas combinan una membrana `alpha=0.42` con venas opacas/alpha-clipped para
seguir visibles sobre pared clara y cielo oscuro. `ZWrite=Off`, `Cull=Off`,
Receive Shadows desactivado y ningún shadow caster.

El ambiente comparte materiales; colores de variantes ambientales se hornean
en vertex color o atlas. Personajes pueden usar un material instanciable con
colores por renderer si la prueba confirma batching. Ningún prefab crea una
copia de material durante `Update` o al hacer spawn.

Presupuesto de escena alfa:

- Máximo 32 materiales de entorno y 12 de personajes visibles en la muestra.
- Texturas de props/personajes hasta 1024; atlas de arquitectura hasta 2048.
- Normal maps sólo cuando el relieve cambia la lectura a la distancia de juego.
- Unidades en metros, transform scale `(1,1,1)` en prefab raíz.
- Piezas menores a 10 cm y membranas no proyectan sombra.
- Árboles, personajes, arquitectura, puertas y props grandes sí proyectan.

## 5. Cámaras y rig en juego

| Cámara | FOV vertical | Near | Far | Reglas |
|---|---:|---:|---:|---|
| Humano ronda | 75° | 0.03 m | 100 m | una cámara, cuerpo local visible, cabeza local oculta |
| Mosquito ronda | 68° | 0.02 m | 100 m | tercera persona, pivot en tórax, esfera de colisión 0.08 m |
| Lobby | 58° | 0.05 m | 80 m | tercera persona, cuerpo completo |
| Personalizador | 35° | 0.05 m | 20 m | luz y fondo propios de UI, sin DOF obligatorio |

Humano: cámara anclada a `CameraEye`, sin parent directo al hueso de cabeza que
introduzca bob/roll no controlado. El yaw pertenece al root de gameplay; pitch
al pivot de cámara. Presentation distribuye pitch visual entre pecho/cuello con
límites y conserva cuerpo, brazos, piernas y pies visibles. Sólo los renderers
de cabeza, pelo y gorro que intersectan el near plane se excluyen para el dueño
local; otros clientes siguen viendo el modelo completo.

Mosquito: distancia inicial `0.85 m`, zoom `0–2.5 m`, damping de posición
`0.08 s` y de rotación `0.05 s`. Desde un ancla segura hace un primer barrido
hasta el pivot y otro hasta la cámara contra `CameraCollision`; resuelve por
separado cualquier overlap inicial. Se retrae al primer obstáculo y sólo
suaviza la expansión hacia afuera. No cambia la dirección autoritativa de
vuelo. Si Director admite
Cinemachine, `ThirdPersonFollow` ofrece filtro, radio y distancia de colisión;
el paquete y versión siguen bajo propiedad del Director.

Animator:

- Root motion desactivado. Gameplay escribe posición y rotación del actor.
- Base locomotion: idle/walk/run/crouch/jump/fall para humano; fly/land/perch/
  walk/fall para mosquito.
- UpperBody override con Avatar Mask para palmada/defensa y acciones.
- Additive aim/lean con peso derivado de pitch, yaw delta y aceleración.
- Two Bone IK de manos sólo para el arma de muestra; targets y sockets vienen
  del rig. El constraint se apaga durante caída/desmayo.
- Crossfade locomotion `0.10 s`, acción `0.06 s`, caída `0.04 s`, recuperación
  `0.14 s`. Los eventos autoritativos incluyen tiempo normalizado para que un
  cliente tardío entre en la fase correcta.
- Animation Events pueden reproducir Foley local, pero no confirmar golpe,
  defensa, picadura, extracción o caída.

Las capas y máscaras siguen la función documentada por Unity en
[Animator Layers](https://docs.unity3d.com/6000.3/Documentation/Manual/AnimationLayers.html).

## 6. VFX alfa

Sólo se incluyen señales asociadas a acciones alfa:

- Palmada/golpe: arco breve de anticipación y `6–12` partículas al impacto,
  vida máxima `0.22 s`, pool de 8 emisiones.
- Mosquito golpeado: `8–16` partículas y flash de `0.08 s`; la trayectoria de
  caída pertenece a Gameplay.
- Extracción completada: pulso de color en abdomen y sting de audio; no marca
  de zona sobre el cuerpo humano.
- Polvo de aterrizaje: máximo 6 partículas opacas, sólo en piso exterior.

No hay decal de picadura, sangre persistente, motion blur ni partículas que
oculten la lectura manual de defensa.
