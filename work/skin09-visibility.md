# Selección visible sin trabajo repetido

`CharacterSkin.set_first_person` devuelve inmediatamente cuando el modo no
cambia. `set_appearance` sigue reconstruyendo su propia selección al cambiar
ropa, cara, pelo o accesorios, también durante primera persona.

La comparación usa los GLB importados de humano y mosquito y una subclase
con el setter anterior, que recorría todas las mallas incondicionalmente.
Pasa 1422 comprobaciones, incluyendo cambios de apariencia, primera/tercera
persona, ocho repeticiones de cada estado y ocultación facial en primera
persona. Las pasadas por mallas bajan de 446 a 37 por rol en esa secuencia,
con los mismos valores de visibilidad. No es una medición de FPS.

Ejecución headless Godot 4.5.2 con Dummy: salida 0 y stderr vacío.
Evidencia `skin09-visibility.json` y `release07-skin09-visibility.run.json`.
Se conserva la lógica de poses, materiales, shaders y selección de mallas;
la geometría no se modifica mediante este cambio.
