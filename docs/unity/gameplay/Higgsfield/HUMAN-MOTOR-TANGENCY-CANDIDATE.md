# Candidato experimental: barrido humano separado del contacto tangente

Commit del motor `37d0b8b`, sólo CastMotor en UnityGameplayWorld.cs. Se aisló el
hunk mediante git apply --cached; cambios alfa de BeginRound/strikes permanecen
sin stage ni modificaciones. No afecta cápsula física, endpoints, Skin=.001,
motor mosquito, velocidades/balance, arte o colliders de ISLA/CASA.

## Hipótesis y límite geométrico antes de integrar

La query humana usa radius−Skin para iniciar separada de superficies tangentes,
con endpoints originales. Se extiende sólo un Skin adicional: longitud delta+
2Skin frente a delta+Skin anterior. Sobre cada hit retornado se calcula
closing=−dot(dirección,normal) y se resta Skin/closing de su distancia para
reconstruir el contacto con el radio completo sobre una cara plana. El solver
existente vuelve a restar Skin al decidir el avance, como antes. Se reordena
por distancia corregida porque la compensación depende de la normal. Se filtran
hits corregidos fuera del alcance delta+Skin original.

Si closing<=.0001 o closing×distance<=Skin, devuelve distancia0 conservadora:
no divide por incidencia casi cero ni extiende el barrido arbitrariamente.
Restar sólo Skin sería equivalente únicamente en incidencia frontal. Esta
compensación por normal es exacta para el mismo contacto planar retornado,
sin prometer equivalencia en aristas o superficies curvas.

**Limitación pendiente:** la query finita puede omitir un contacto rasante que
la cápsula completa habría encontrado. El aumento de distancia por reducir
radio es Skin/closing y puede superar la extensión fija adicional de1mm. No
se afirma que conserve todo clearance previo. Los tests oblicuos están para
detectar esa regresión; una mejora del sendero no bastará para aprobarla.
No se compensará una falla aumentando tolerancia2mm o modificando balance.

## Evidencia offline

Código central Gameplay.Unity más únicamente CastMotor del commit candidato,
compilado en `N:/LetMeSleep/Validation/Higgsfield/HumanMotorCandidate/compile-37d0b8b`:
0errores/0advertencias. Fuentes copiadas y hashes en receipt; no se compila el
WIP alfa del worktree. Esto acredita sintaxis/referencias, no consultas PhysX.

Fixture externo `HumanMotorContactChecks.cs`, acción `human-motor-contacts`,
config `human-motor-contacts.json`. Usa motor central directo a30Hz, cápsula
humana.25×1.72, sin reimplementar solver, geometría sintética descartable.
Casos: plano triangulado tangente; pendientes trianguladas±8°; pared frontal;
dos aproximaciones oblicuas; techo; peldaño.20m permitido/.24m bloqueado;
mosquito contra pared como control del camino no modificado. Registra posición,
distancia, tick y profundidad/collider del pico, clearance esperado/medido en
pared/techo. Requiere penetración<=2mm y conservación de límites geométricos.
El Skin existente se resta a distancia de barrido: su margen normal sobre una
pared oblicua es Skin×incidencia, no1mm constante. La prueba usa ese valor previo
con tolerancia numérica10µm. Las rutas de mapas luego prueban Authority real.

## Secuencia nativa preparada, pendiente de integración y turno

1. Coordinador integra únicamente el hunk candidato; Unity compila central.
2. Ejecutar `human-motor-contacts.json` y
   `isla-v2.original-motor-diagnostic.json`: ruta afectada22puntos ida/vuelta
   sobre ISLA original, sin hulls/proxies ni cambio de asset. Se omiten otros
   casos durante este primer diagnóstico; no es una aprobación de mapa.
3. Sólo si pasa, ampliar al lote ISLA original y CASA aplicada para comprobar
   escaleras, entradas, suelos/pendientes y vuelo real previamente aprobados.
4. Si falla, guardar pico/clearance/traza y revertir únicamente el commit
   candidato en central por coordinación. No tocar WIP alfa ni publicar hipótesis.

No se abrió Unity durante la preparación. Root conserva turno de importación
Campamento y devolverá slot después de integrar/compilar el candidato.
