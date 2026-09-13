# Propuesta de gait humano con rig actual y velocidades3.1/5

Diseño fuente solicitado por Director, coordinado con Audio/Presentation. No cambia assets, clips, Animator, motor ni protocolo. Las velocidades nominales son máximos con input1:3.1 sin sprint,5 con sprint,1.55 agachado. La velocidad efectiva puede ser menor. Las cadencias2.5/3.3 propuestas previamente por Audio son exploratorias.

Conclusión: mantener3.1/5 es **cinemáticamente posible** con las piernas actuales si3.1 se representa como trote y se reautoriza el apoyo/vuelo/pelvis. Exige una cadencia más rápida que la propuesta de Audio. No equivale a una marcha tranquila ni demuestra todavía apariencia natural o viabilidad dinámica de fuerzas. El rig tiene muslo.34m+pantorrilla.32m; se reservan20mm de extensión, longitud operativa máxima.64m.

## Por qué no basta acelerar o escalar el clip

Para velocidad `v`, contactos de ambos pies por segundo `c` y fracción de ciclo apoyada por pie `d`:

`T=2/c`, `D=v*T`, `S=D*d`.

El clip actual Walk usa `S=.20,d=.62` (D.322581); Run `.28,.48` (D.583333). Runtime asumeD1.2 y elige Run desde2.45, por lo que a3.1 ya usa Run. Con c2.5/3.3 y duty actual, los spans1.538/1.455 superan incluso el diámetro2*.66 de una pierna sin altura vertical: no se resuelven con pesos de piel, escala7x o IK adicional.

## Cuatro puntos de diseño medidos

Altura de cadera aquí es **origen UpperLeg** en Z fuente, no origen Hips; Hips queda30mm debajo. Tobillo apoyado enZ.12 con orientación de pie de referencia. Valores medidos sobre4802 muestras por gait, ambos pies, apoyo y vuelo completos.

| Gait de diseño | v m/s | contactos/s | T s | D m/ciclo | duty por pie | span apoyo m | cadera min/max m | alcance pierna min/max m |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Caminar lento |1.0|2.4|.833333|.833333|.60|.500000|.695/.720|.507362/.638876|
| Caminar rápido |1.55|3.2|.625|.968750|.56|.542500|.685/.720|.498583/.636913|
| Trote rápido |3.1|4.0|.500|1.550000|.32|.496000|.660/.699933|.390000/.633816|
| Carrera |5.0|4.6|.434783|2.173913|.24|.521739|.645/.695670|.325000/.636180|

El punto1.55 es caminar erguido a esa velocidad, **no certifica locomoción agachada**. Crouch necesita su propia altura/pasos. Los márgenes de extensión mínimos son21.1/23.1/26.2/23.8mm. Máxima flexión de rodilla calculada79.6/81.9/107.6/121.1°; las máximas aparecen al recoger la pierna en vuelo. Revisar ropa/rodilla en esas poses antes de aceptar.

L contacta en fase0, R en.5. Por pie, apoyo `[0,d)` y swing `[d,1)`; aplicar desplazamiento de fase. En marcha hay doble apoyo; en trote cada mitad de ciclo tiene apoyo.32 y aire.18 (tiempo aéreo por paso.09s); carrera apoyo.24 y aire.26 (.113043s). Son ventanas del diseño normalizado, no clips existentes aprobados.

## Curvas fuente y apoyo

Coordenada longitudinal del pie en actor, positiva hacia delante. En apoyo, `x=S/2-D*q`, altura de tobillo.12. Con Root avanzando v y fase avanzando1/T, el pie apoyado queda fijo en mundo: `dx/dt=-v`. Drift máximo numérico de la prueba≤1.8e-16m; es el contacto puntual ideal, no una medición de la suela en juego.

En swing usar `u=(q-d)/(1-d)`, `K=D*(1-d)` y `x=-S/2-K*u+D*(10u³-15u⁴+6u⁵)`. Conserva velocidad horizontal-v y aceleración0 en ambos extremos, evitando un salto al despegar/contactar. **Sobrepasa los extremos del span**: la carrera alcanza±.465403m aunque el span de apoyo sea sólo.521739m. Por eso se comprobó el swing entero; medir sólo el apoyo habría aprobado candidatos que casi estiraban la pierna al límite.

Levantar el tobillo con dos quinticas de entrada/salida: `H*smooth(u/r)*smooth((1-u)/r)`, cada argumento limitado a0..1. H=.09/.10/.15/.20m y r=.22/.22/.18/.16 respectivamente. La elevación temprana evita que la retracción inicial exceda el alcance. Esta primera curva tiene una parte alta amplia; debe pulirse junto con rodilla, orientación de pie y toe-off. La rapidez del pie, aceleraciones y aspecto requieren revisión: el test sólo descarta estiramiento geométrico.

En marcha la cadera sube al apoyo medio (`hip+rise*sin²(2πphase)`). En trote/carrera comprime durante apoyo y sube en el intervalo aéreo mediante parábola con gravedad9.81, empalmada con velocidad vertical continua. Alturas de contacto.69/.68, compresión.03/.035. Las fórmulas exactas y sus muestras están en `diagnostics/gameplay-e9d15e7/gait_feasibility.py` y `gait-feasibility.json`.

El objetivo para el autor es una trayectoria válida de pies/pelvis y rotaciones de muslo/pantorrilla, no escalar huesos. Incorporar giro del pie talón→planta→punta sólo después de verificar el punto real de suela; rotar el pie cambia el ancla de contacto respecto al tobillo. Balanceo lateral y yaw de pelvis deben entrar con poca amplitud y volverse a medir en3D.

## Clock y transición con Audio/Gameplay

Audio propuso un clock único `phase += dsHorizontal / D(speed)` con contactos L/R en0/.5; eventos una vez por cruce. Usar distancia efectiva del motor, no sólo input deseado. No reiniciar fase al cambiar gait y mantener la misma fase de apoyo durante crossfade. No disparar pasos con el pie en aire ni en pose Crouch estática. La velocidad de Animator deriva de duración real del clip y D, coherente con el mismo clock; el límite actual de playback no debe recortar la cadencia silenciosamente.

Audio confirmó que el mínimo provisional de.28s entre cues debe retirarse/reemplazarse al integrar ese clock: los contactos propuestos ocurren cada.25s a3.1 y.217391s a5, por lo que ese mínimo descartaría pasos válidos.

Los cuatro puntos no son cuatro skins ni una fórmula válida para todas las velocidades. Requieren muestras fuente o variantes de gait; un único Run retimado no cambia su duty/altura y no reproduce simultáneamente las dos propuestas. Director decide una variante Trot/Run o blend de clips preservando IDs actuales. Los T de la tabla son duraciones reproducidas a la velocidad nominal: la duración de archivo puede conservar intervalos de frames a30fps y compensar playback con el mismo D.

Antes de interpolar parámetros o crossfades, muestrear todo el intervalo de velocidades y transiciones con pie de apoyo en mundo. Los cuatro puntos aislados **no** validan esa interpolación. Si hace falta conservar un apoyo durante el cambio, su ajuste debe ser pequeño y medido; no esconder metros por segundo de deslizamiento con IK.

La pelvis propuesta baja respecto de la pose actual; comparar cámara/Head, ropa, cápsula y superficies de mordida, cuyos huesos proxy hoy usan otras alturas/longitudes. Escalones requieren además lectura de soporte y adaptación de pies/pelvis al terreno; el diseño plano no los resuelve. Si la cadencia rápida y la postura no resultan naturales al usuario, hay que revisar proporciones del rig o velocidades con Director en vez de forzar este candidato.

Siguiente trabajo autorizado aquí termina en propuesta/documentación. Reautoría y aceptación posteriores: contacto de suelas, rodilla/prenda, ritmo audible y visible compartido, giros y arranque/freno, slope/escalón y cambio de gait. Sin nueva generación de assets ni nativos de este trabajador para esta propuesta.
