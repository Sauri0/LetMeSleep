# Correcciones visuales directas — 20/09/2026

Origen: tres mensajes nuevos de Branko durante la revisión de la candidata04.
Son requisitos de la entrega activa. Los PASS técnicos anteriores conservan
su alcance, pero no aprueban UI, personajes ni calidad visual de los mapas.

## UI: rediseño, no retoque de color

El usuario rechaza la interfaz por simple y cuadrada. Primer corte: menú
principal, HUD y componentes comunes; después extender coherentemente a sala,
ajustes, selección, personalización y resultados sin perder flujos existentes.

Dirección de implementación Unity:
1. Juego social de humanos y mosquitos, legible durante acciones rápidas.
2. Noche cálida, humor y expresividad; coherencia con el arte facetado original.
3. Menú con escena/personajes protagonistas, navegación ligera y asimétrica;
   HUD compacto, inventario/contexto sin paneles gigantes que oculten el mundo.
4. Jerarquía entre título, acción principal, datos de juego y ayudas; tipografía
   expresiva en títulos y legible en texto español, sin mayúsculas indiscriminadas.
5. Noche/tiza/ámbar, acentos de rol, superficies con profundidad y contornos
   suaves o biselados; no una pila uniforme de rectángulos azules delineados.
6. Entradas y foco de120–220ms, feedback de acciones/estado sin mareo; reloj
   independiente de pausa, conservar movimiento reducido y objetivos clicables.
7. Diferenciador: escena nocturna viva integrada a la navegación y estados.

PER08 orienta riqueza/profundidad y protagonismo del personaje, no autoriza
categorías, textos, logos ni reglas adicionales. La dirección concreta anterior
es una decisión CEO para implementar y revisar, no aprobación visual del usuario.
Se aplica skill ui-design dentro del framework Unity existente.

Aceptación: capturas reales a720/1080 y secuencia de navegación/foco/transiciones;
teclado/ratón, vacío/error/carga/selección, legibilidad y HUD con acción en curso.
Comparar antes/después y preservar las funciones de inventario, carga, modos,
PTT y publicación privada/explícita de personalización. No cerrar sólo por CPU.

## Mapas: choques de superficies y parpadeos

Revisar los cinco mapas con cámara en movimiento: suelo, paredes, techos,
escaleras/barandas, agua/orillas y uniones entre terreno/edificios/props.
Separar caras coplanares (z-fighting), transparencias/orden de dibujo, sombras
inestables y detalle que centellea a distancia. Un collider superpuesto es una
pista física, no demostración de conflicto entre caras renderizadas.

Auditoría geométrica debe guardar objeto/material/triángulo/posición por sospecha;
la comprobación visual requiere clip reproducible de acercamiento, alejamiento
y giro. Corregir la fuente responsable sin esconder todo bajo offsets globales,
desactivar sombras de forma indiscriminada ni retirar colisiones necesarias.
PuertoCoplanar01 es el primer diagnóstico acotado; no cubre los cinco mapas.

## Personajes: postura, deformación y caídas

El usuario observa rigidez, marcha humana antinatural, brazos estirados que no
acompañan la forma al moverse/golpear y caídas de desmayo/muerte sin peso.
Solicita más animación, vida y huesos donde sean necesarios.

Auditar por separado esqueleto, pivotes, pesos, clips, retarget, IK/constraints
y orden de escritores. Más huesos por sí solo no corrige un clip o pesos malos.
Añadir articulaciones/deformadores según necesidad comprobada, conservando
silueta y facetas del boceto; no suavizar ni reciclar la base alfa como arte final.

Humano: postura relajada, marcha/carrera con transferencia de peso y apoyo,
clavícula/hombro/codo/muñeca coherentes, anticipación/impacto/retorno del golpe,
agarre estable y brazos sin alargamiento artificial ni colapso de volumen.
Mosquito: articulación de patas, cuerpo/abdomen, alas y cabeza; cambios de vuelo,
contacto, giro y recuperación con continuidad, sin rigidez de una pieza única.
Caídas: pérdida de equilibrio y contacto con suelo/obstáculos, peso y asentamiento;
transición coherente a desmayo, muerte y recuperación. Evaluar animación física
o mezcla con ragdoll, sin introducir decisiones de gameplay por física visual.

Aceptación por clips completos de frente/perfil/espalda, cámara de juego y
primer plano de hombros/codos: idle→marcha→carrera→giro→golpe→retorno,
vuelo→posado→picadura→salida, caída en suelo/plano inclinado/escalón y recuperación.
Revisar variaciones corporales/ropa/accesorios y actores remotos. No certificar
con una pose, conteo de huesos, imagen generada o sólo valores numéricos.

## Propiedad y continuidad

- Red: auditoría luces Casa breve y luego UI/common components; código por
  fuera de Assets mientras técnica use editor, integración coordinada.
- Estado: auditoría rig/motion sólo lectura, informe CharacterMotionAudit01.
- Técnica: PuertoCoplanar01, geometría y defecto de adquisición; turno Unity.
- CEO: integración, auditoría visual de cinco mapas, criterios y evidencia.

Modelo/esfuerzo y mediciones según TEAM.md; no activar chats históricos.
La candidata04 es privada y provisional. Objetivo completo sigue activo.
