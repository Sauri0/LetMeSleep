# Regresión: mosquito desplazaba al humano sin controles

Validación local de Unity 6000.3.24f1, 12 de septiembre de 2026. Mapa HousePatio, entrenamiento como humano, `CaptureLocalInput=false`.

## Antes: causa y contraste nativo de W1

El humano aparecía en (6.06, .02, 1.2), sin solapamientos iniciales. Tras asentarse a y=.001 permanecía quieto hasta que un mosquito alcanzaba su cuerpo. En tick137, Actor_3 Flying penetraba .069 m en la cápsula de locomoción humana y la corrección de solapamiento desplazaba al humano (.00940, 0, -.06937).

En tick353 se comparó exactamente el mismo `MoveHuman` con velocidad cero y estado restaurado entre variantes. Actor_2 Flying penetraba .069022 m:

| Variante | Posición antes | Posición después |
|---|---|---|
| Colisiones originales | (6.071337, .001, 1.116320) | (6.083530, .001, 1.047368) |
| Esferas de mosquitos desactivadas temporalmente sólo durante MoveHuman | (6.071337, .001, 1.116320) | (6.071337, .001, 1.116320) |

Las esferas y el snapshot original se restauraron al terminar. Los recibos y scripts de inspección permanecen en `N:/LetMeSleep/Validation/PhysicsPush-20260912/`: `initial.json`, `trace.json` y `contrast-contact.json`. El ensayo preliminar `contrast.json` no encontró desplazamiento en tick138; el contraste causal se tomó después, durante un solapamiento efectivo en tick353.

La cápsula humana de locomoción es más ancha que algunas superficies anatómicas usadas por el mosquito. El mosquito podía acercarse a la piel mientras el motor humano trataba su esfera como un obstáculo interior; unido al cuerpo, ese conflicto producía correcciones repetidas.

## Corrección y observación posterior

W1 `45168cb`, integrado por Director como `f5b98d7`: `UnityGameplayWorld.BlocksMotor` excluye actores Mosquito al resolver el motor de un Humano. Las consultas de picadura y golpe, y la colisión del mosquito con anatomía, conservan sus reglas.

Director comunicó la regresión nativa posterior: **33.4 s sin captura de controles**, humano **x=6.06, z=1.2 intactos**; sólo y=.02→.001 por asentamiento. Mosquito3 estaba **Biting en (6.057, 1.1, 1.476)**; Mosquito2 había explorado el segundo piso hasta **(11.101, 4.1, 7.091)**; sangre acumulada **12.9**. Pasa el caso observado de humano quieto mientras recibe picadura. Esta observación posterior procede del Director; W1 no abrió otra sesión ni repitió el playtest.

Alcance: causalidad física y regresión del desplazamiento en entrenamiento local. No acredita WAN/EOS, rendimiento, cobertura de todos los contactos o terminación de una partida completa.
