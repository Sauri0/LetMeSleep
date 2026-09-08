# Piloto de alcance de luces — descartado

Diagnóstico nativo Compatibility, misma casa `house-v1-1`, textura 1920×1080,
cinco cámaras antes/después en un proceso. Mantiene geometría, luces, sombras,
tonemapping y puertas abiertas. No incluye actores ni acredita FPS de juego.
Salida 0, stderr vacío; `release07-light-range09-pilot.run.json`.

Se redujo sólo el alcance de las luces de habitaciones que tenían margen
geométrico, conservando al menos la distancia a la esquina del piso más 1 m
y el 85 % del alcance anterior. Se compensó la energía en el centro del piso.
Esto no conserva toda la caída: la contribución radial propia calculada en
esquinas de cuartos modificados queda aproximadamente en 50–57 % de la previa.
No equivale a una reducción de ese porcentaje en la imagen, que contiene
ambiente y otras contribuciones.

| Vista | Draws antes/después | Mediana CPU render antes/después |
|---|---:|---:|
| Cuarto | 429 / 663 | 1,917 / 1,990 ms |
| Pasillo | 1762 / 4321 | 4,217 / 9,462 ms |
| Pasillo superior | 1437 / 4540 | 3,629 / 7,446 ms |
| Esquina | 182 / 174 | 1,403 / 1,295 ms |
| Paso con puerta abierta | 1342 / 4878 | 4,019 / 7,228 ms |

La propuesta no produjo la reducción de coste esperada. No se integra y no
se atribuye el aumento a una causa interna del renderizador sin medirla.
Root revisó antes/después de esquina y paso: la diferencia visible es leve,
pero eso no demuestra conservación de luz en todas las habitaciones.
Los diez PNG y datos completos están en `outputs/0.9-light-range-pilot`.
World y la configuración del juego permanecen sin esta modificación.

La compensación usa únicamente el término radial de Godot 4.5.2,
`(1 - (distancia / alcance)^4)^2`, conservando el exponente de distancia y
el cono. Fuente primaria: [scene.glsl, get_omni_spot_attenuation](https://raw.githubusercontent.com/godotengine/godot/4.5.2-stable/drivers/gles3/shaders/scene.glsl).
