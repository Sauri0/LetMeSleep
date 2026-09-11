# Movimiento/cámara 0.9.1 — tramo C01

Base: `59af8ad`, worktree `lms091-motion`, rama `codex/091-motion`.

## Diagnóstico

La raíz posicional se aproxima al snapshot a 18/s mientras el yaw humano,
cabeza y fase de marcha cambian directamente a 20/30 Hz. La cámara utiliza
el ratón actual pero reconstruye el desplazamiento del ojo con el yaw de ese
snapshot. El mosquito adherido tenía dos filtros independientes (posición y
orientación), aunque la pose esquelética de su humano ya era exacta.

## Diseño aplicado

- HumanPresentation mantiene el filtro previo de crouch y añade muestra
  angular/de marcha acotada entre valores recibidos, sin predecir simulación.
- ActorView usa la muestra al representar el cuerpo y expone su origen del
  ojo para Client. El ratón y los comandos enviados siguen intactos.
- Colliders conservan el yaw público mediante compensación del padre, con
  cache de segmentos por snapshot y sin recrear geometría entre fotogramas.
  Fuera de adhesión comparten el origen render interpolado previo: no se
  afirma coincidencia absoluta con la posición de autoridad.
- Amenaza/golpe/lanzamiento mantienen el filtro de raíz previo y restauran
  pose local exacta, sin añadir salto traslacional por comenzar un ataque.
- La corrección posterior conserva también el filtro basal durante bitten.
  Client alinea el insecto biting con el desplazamiento render de su humano
  después de actualizar todos los actores; la pose/normal siguen exactas.

## Decisión local

El cálculo independiente del filtro (snapshot 20 Hz/render 60 Hz, radio
del ojo .38 m, 18/s) arroja hasta .0474 m de desplazamiento orbital añadido
a 2 rad/s y .1415 m a 6 rad/s. Por eso se conserva yaw/pitch local exacto:
el filtrado angular se limita a remotos, la marcha se suaviza en ambos.
La prueba nativa exige que el ojo local añada cero desplazamiento al
comportamiento anterior. La medición nativa y los límites restantes están
en `motion091-results.md`.

Director autorizó e implementó después el campo público `attached_to`, sólo
durante contacto activo. El consumidor está en Client/ActorView, sin editar
World. El contrato de simulation.gd pertenece al Director. No se modifican
hitboxes, reglas, activos, EOS ni se publican asignaciones/zonas privadas.

## Validación

`game/tests/motion091_presentation_test.gd`:

- Cadencias 20/30 -> 60/120 Hz, cruce ±PI, amplitud de paso de yaw/pitch/fase.
- Ausencia de predicción del reloj y de mutaciones en snapshots.
- Reset por teletransporte/round/static y entrada/salida de estados críticos.
- Cápsulas en coordenadas de mundo incluso entre paquetes con padre rotando.
- Ojo de cámara en la misma referencia del cuerpo mostrado.
- Adhesión real Simulation -> World con marcha y giro.

Ejecutada con renderer nativo, tras turno explícito del Director e import
del worktree. Resultados en `motion091-results.md`. No es una prueba WAN
ni una aprobación visual global.
