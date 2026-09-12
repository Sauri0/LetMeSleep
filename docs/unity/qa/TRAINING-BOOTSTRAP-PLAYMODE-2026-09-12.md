# Evidencia PlayMode de entrenamiento — 2026-09-12

Alcance: arranque de la escena `LetMeSleepBoot`, creación y limpieza del entrenamiento local para ambos roles. La ejecución se hizo con Unity `6000.3.24f1` en el editor D3D11. El recibo declara base `7ca1e54` y fuente dirty, por lo que acredita el estado ensayado pero todavía no una candidata reproducible.

Resultado final: **PASS — 2/2**, 0 fallos, 0 omitidos y 0 inconclusos; duración total `1.33 s`.

| Caso | Resultado | Duración |
|---|---:|---:|
| `HumanTrainingBuildsAndCleansACompleteRuntime` | PASS | `0.9491106 s` |
| `MosquitoTrainingBuildsAndCleansACompleteRuntime` | PASS | `0.3552103 s` |

## Contrato observado

Cada caso carga la escena desde Build Settings, espera a `AlfaApplication.Start` y llama a `StartTraining` con Sangre, Casa con patio y el rol elegido. Antes y después de cinco frames comprueba:

- autoridad local y avance automático activos;
- snapshot de ronda en curso y mapa alfa correcto;
- tres actores físicos con IDs únicos: un humano y dos mosquitos;
- actor local con el rol elegido y collider motor habilitado;
- una sola cámara renderizando, llamada `PlayerCamera`, con controlador humano o mosquito según el rol;
- siete `GameplayToolPickup` físicos bajo el mapa activo, con collider e identidad concordantes con el snapshot;
- posiciones, rotaciones, escalas y métricas principales sin `NaN` ni infinito.

Después llama a `CancelTraining` y verifica que desaparezcan runtime, actores y pickups de la casa, y que la cámara de menú vuelva a ser la única habilitada.

## Trazabilidad

- Implementación inicial de la suite QA: `6c4e031`, integrada en principal como `5d02083`.
- Corrección portable del conteo NUnit: `27be705`, integrada en principal como `f7e9606`.
- Resultado detallado: [`ALFA-TRAINING-PLAYMODE-20260912.json`](ALFA-TRAINING-PLAYMODE-20260912.json).
- Recibo de procedencia y límites: [`ALFA-TRAINING-PLAYMODE-RECEIPT.json`](ALFA-TRAINING-PLAYMODE-RECEIPT.json).

La primera ejecución alcanzó la creación real de 3 actores, 7 pickups, cámara y limpieza, pero los dos casos quedaron rojos por usar `Has.Count` sobre un array de definiciones. El error pertenecía al matcher del test. Tras cambiar ambos conteos a la propiedad tipada `.Count`, la repetición completa quedó verde.

## Límite de la evidencia

Esta suite acredita integración local PlayMode y ciclo de vida de entrenamiento. No acredita partida EOS entre pares, WAN, FPS, composición visual ni una candidata limpia/reproducible.
