# Personajes: opciones faciales de 0.9.2

Base `59af8ad`, worktree `lms091-characters`, rama `codex/091-characters`.
Este tramo pertenece a 0.9.2 según Director. No incluye manos ni ropa.

## Resultado

Las bocas Sonrisa, Reposo y Seria tienen distinta abertura oscura y contorno
en reposo: curva ascendente, boca corta y curva descendente. Las cejas
Arqueadas/Altas son más estrechas y elevadas; Bajas del mosquito más anchas.
El mosquito tiene tres aperturas palpebrales distintas; antes E0/E1 compartían
exactamente los mismos párpados. El puente de sus gafas se adelanta localmente
hasta 4 mm de fuente (1,4 mm en juego), resolviendo contactos anteriores con
los globos oculares de las tres variantes.

Se conservan nombres, IDs, cantidades, categorías y canales públicos. Se
regenera desde humano A y mosquito B congelados. El contraste binario verifica
rig/bind, atributos y morphs de las piezas no afectadas, incluyendo cuerpo,
manos, pelo y prendas. En las piezas editadas conserva vértices, pesos y
canales. Blender cambia diagonales de algunos polígonos de cejas de mosquito:
el test comprueba igual cantidad de triángulos y mismo contorno orientado de
los parches retesselados; no exige identidad de todos sus índices.

## Evidencia de esta revisión

| Comprobación | Resultado | Archivo |
|---|---:|---|
| GLB, variantes, preservación de geometría/rig | 241 / 0 fallos | `character091-after-r2.json` |
| Contactos humanos, 66 pares compatibles | 702 muestras / 0 fallos | `character091-contacts-r2.json` |
| Contactos mosquito, 45 pares compatibles | 525 muestras / 0 fallos | `character091-contacts-r2.json` |
| Geometría ocular humana en Blender | 81525 / 0 fallos | `character091-blink-human-r2.json` |
| Geometría ocular mosquito en Blender | 81663 / 0 fallos | `character091-blink-mosquito-r2.json` |
| Fixture nativo de capturas finales | 1121 / 0 fallos | `character091-after-views-r2/character091-views.json` |
| Fixture nativo de base, caras y prendas | 1549 / 0 fallos | `character091-before-views-r2/character091-views.json` |
| Selección/expresiones faciales en Godot | 7946 / 0 fallos | `character091-facial-parts-r2.run.json` |
| Parpadeo importado en Godot | 3392 / 0 fallos; delta ocular 0 | `character091-native-blink-r2.json` |

Exportación, importación final, auditorías finales y capturas r2: exit 0,
sin timeout ni stderr. Las fichas `character091-*.run.json` registran ejecutable,
argumentos, PID y duración. Godot 4.5.2 Compatibility sobre RTX 3060 Ti;
esto no constituye una medición del objetivo GTX1660Ti/1080p60.

Las diez láminas finales están en `character091-after-views-r2/`: frente/perfil
de ojos, cejas y bocas; frente/perfil/espalda de pelo y accesorios. Revisadas
visualmente cejas y bocas de ambas especies y gafas. La sonrisa y la seria son
legibles en frente; en perfil las bocas mantienen relieve discreto. Los ojos
del mosquito y sus gafas quedan separados en la captura. El encuadre lateral
del estudio recorta probóscide/cola del mosquito; no certifica su silueta
completa. Las alas usan el reloj del actor y varían entre columnas, por lo que
la comparación estática se limita a las piezas faciales. Los ojos humanos no
cambian de geometría respecto a la base.

UI revisó manualmente las diez láminas y dio PASS visual: variantes legibles,
sin penetraciones ni piezas flotantes evidentes. Conservó la observación del
encuadre lateral del mosquito. Esta revisión es distinta de las pruebas de
geometría y no sustituye el muestreo de animación.

## Desarrollo y límites

El GLB original incumplía 11 de 47 condiciones nuevas de distinción de
variantes (`character091-before.json`). En contactos, la base humana daba
0 fallos y la del mosquito 3 pares con gafas (`character091-contacts-before-r1.json`).
La primera propuesta añadió contactos entre cejas/gorro, cejas/ojos y
sonrisa/ojos. Se redujo el arco humano, se conservó la ceja firme humana y se
bajó ligeramente la sonrisa del mosquito; r2 elimina los fallos muestreados.

Las primeras capturas tenían dimensiones incorrectas por content_scale_size;
`views-before-r1` es inválido. `views-before-r2` corrige tamaño y se capturó
antes de exportar los cambios. Las capturas `after-views-r1` corresponden a
geometría intermedia y no deben presentarse como resultado final.

Los contactos son comprobaciones exactas de triángulos en estados finitos
neutrales, intermedios y extremos descritos en el test. No prueban todas las
combinaciones continuas de animación. Las regresiones nativas
`facial_parts08_checks.gd` y `facial_blink08_checks.gd` sobre r2 terminaron
el 11/09 a las 23:54:56 y 23:55:13 UTC: ambas exit 0, stderr 0 y sin timeout.
Se ejecutaron después de integrar HumanPose b138a99 y con el candidato de
manos local; las funciones faciales de CharacterSkin permanecen intactas.
No se ha probado WAN ni se ha generado/publicado un EXE.

Ropa y manos continúan por separado: el abanico del cuello procede de UV
cilíndricas; un shader candidato sigue experimental. La matriz de primera
persona muestra bandas oscuras en muñecas aun sin sombras y con malla
completa; desaparecen con material sin iluminación. Los cortes de franjas
del pantalón permanecen sin iluminación. Son diagnósticos, no arreglos
incluidos aquí. La flexión invertida y el estiramiento de dedos requieren
integración conjunta de CharacterSkin y HumanPose con Worker 1.

## Reproducción

Con turno de motor del Director, ejecutar el exportador seleccionado, importar
el proyecto y usar `work/character091-run.ps1` para registrar cada proceso.
Los argumentos completos de las etapas exitosas quedan en sus `.run.json`.

- Exportador: `art_source/export_presets/characters_selected_pipeline.py`.
- GLB sin motor: `python art_source/export_presets/character091_glb_check.py --baseline work/character091-baseline --output work/character091-after-r2.json --verify`.
- Contactos: Blender `--background --python art_source/export_presets/character091_contact_check.py -- --output work/character091-contacts-r2.json`.
- Cierre: Blender `--background --python art_source/export_presets/characters_blink_audit.py`.
- Capturas: Godot `--path game --windowed --resolution 480x520 --audio-driver Dummy --script res://tests/character091_views.gd -- --only=faces --output=<carpeta absoluta>`.

La carpeta local baseline no se versiona por duplicar binarios. Se reconstruye
con los GLB de `59af8ad` llamados `human_lms06.glb` y `mosquito_lms06.glb`
directamente en esa carpeta. Las fuentes originales de ese commit también permiten repetir
la auditoría de contactos anterior mediante `--source-dir`.
