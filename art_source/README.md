# Let me sleep — fuentes artísticas 0.6

Dirección: caricatura cómica para todas las edades, noche doméstica cálida y azul, personajes somnolientos y traviesos. Humano inicial con pijama, pantuflas y gorro de noche. No usar proporciones de bebé ni microdetalle fotográfico.

Fuentes Blender 4.5.3 LTS, exportación GLB explícita a `game/assets/art`. Unidades en metros, eje vertical Blender Z convertido por glTF a Godot Y. Las escenas de Godot conservan física, comportamiento y puntos de contacto; la exportación artística no genera nuevas colisiones de forma implícita. Los scripts de modelado, paletas y presets permiten reproducir las fuentes editables.

Blender portátil oficial se conserva fuera de Git, en `work/tools/blender-4.5.3`. ZIP oficial Windows x64 SHA256: `6B657C8BDD3A7B65B07B9E1AE17EB4BE7DD4AA23121DA7F3D3354FC2551330A7`, comparado con https://download.blender.org/release/Blender4.5/blender-4.5.3.sha256.

Cada área registra sus entradas y licencias. Los modelos y composiciones del proyecto son originales; los samples instrumentales externos, cuando se usan, se identifican por origen y licencia en audio. No se incluyen caches, herramientas descargadas ni credenciales.

Presupuestar por la imagen dentro del juego: silueta y superficies de contacto primero. Materiales compartidos, mallas estáticas agrupadas por objeto, LOD de importación y rango de visibilidad de detalles. Compatibility/OpenGL no depende de SSAO, SSR ni iluminación volumétrica. Los límites de triángulos del plan son techos iniciales, no objetivos de consumo.
