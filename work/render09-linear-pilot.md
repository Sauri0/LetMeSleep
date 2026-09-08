# Piloto de tonemapping lineal — descartado

Se capturaron tres cámaras fijas de `house-v1-1` con Forward+/Vulkan y
tonemapping LINEAR, sin modificar el proyecto ni World. El proceso nativo
terminó limpio en 4,04 s; las imágenes y el informe están en
`outputs/0.9-render-forward-linear-pilot`.

La habitación queda más oscura que el testigo Forward+/Filmic, mientras que
la cortina, la cómoda y las paredes mantienen poco relieve. Los pasillos
tampoco justifican integrar este ajuste. Se conserva Compatibility/Filmic.
Esto no prueba que otro ajuste de iluminación o backend no pueda mejorarlo.

No se atribuye un ahorro de dibujo al cambio de tonemapping: los recuentos
observados de 102/224/204 difieren de los testigos anteriores 327/516/204, de
modo que el trabajo de renderizado no está demostrado como equivalente.

Compatibility de Godot 4.5.2 sí implementa Filmic. Por tanto este piloto fue
un ajuste artístico y no una corrección de un modo supuestamente ignorado.
La diferencia entre mapear cada contribución de luz antes de sumarla y mapear
el resultado combinado es una hipótesis sobre las diferencias visuales entre
backends, no una causa medida en estas imágenes.

No se migró el renderer ni se cambiaron exposición, luces, sombras, materiales
o preferencias del juego.
