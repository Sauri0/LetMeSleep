# Suite EditMode — Gameplay Authority

Estado: **33/33 PASS en arnés C# externo**. Los casos Core, autoridad y réplica también están incluidos en la corrida Unity EditMode integrada de **61/61 PASS**.

La suite contiene diez casos NUnit sobre `GameplayAuthority`, cuatro sobre `ReplicaStateGate` y un `IGameplayWorld` falso controlable. El arnés externo compiló el runtime Gameplay integrado junto con las 19 pruebas Core y ejecutó los 33 casos. Esta comprobación detecta errores de C# y de estado puro, pero no acredita Test Runner, física, render ni red de Unity.

Repetición desde la raíz del worktree, sin abrir Unity:

```powershell
pwsh -File docs/unity/qa/Run-GameplayExternalHarness.ps1
```

El script usa por defecto el `nunit.framework.dll` del PackageCache de la instalación residente en `N:/LetMeSleep/Repository/unity`; se puede indicar otra ruta con `-NUnitFrameworkPath`. `-GameplaySourceRoot` permite contrastar temporalmente las pruebas con otro worktree antes de integrar el runtime.

## Puertas

- `ActionKind.Use` humano alterna el target, publica `DoorSnapshot`/`DoorChanged` y conserva snapshots previos.
- Repetir la misma secuencia es idempotente y no vuelve a consultar ni alternar.
- `PlayerInputCommand.UseHeld` no opera la puerta durante varios ticks.
- Mosquito se rechaza antes de consultar el mundo.
- Candidato ausente, fuera de alcance o con revisión obsoleta no muta la puerta.
- Dos usos en el mismo tick aceptan el primero y aplican cooldown al segundo.
- Una hoja ocupada se detiene en `SafeAngleRadians`, publica bloqueo, reanuda al liberarse y no desplaza actores.

## Contacto de picadura

- `BiteAttachment` público sólo existe mientras `ResolveBite` confirma contacto físico continuo.
- Al fallar la resolución se emite `BiteEnded`, se elimina el ancla y se exige soltar antes de volver a armar.
- La adquisición posterior ejecuta un `TryBiteContact` nuevo y recibe el conteo humano activo actualizado; el ancla anterior no se usa como objetivo futuro.

## Réplica entre rondas

- Tras `Reset` a ronda 2, snapshots, privados y eventos demorados de ronda 1 se rechazan aunque lleven ticks o IDs máximos.
- Un paquete rechazado por mapa o actor incorrecto no adelanta el watermark y no bloquea el siguiente estado válido.
- La ventana de eventos acepta reordenamiento hasta 1023 IDs, rechaza duplicados y excluye exactamente el ID ubicado 1024 posiciones atrás.
- `Reset(null)` cierra los tres canales hasta configurar otra ronda.

El arnés externo pasó los 33 casos contra el Gameplay integrado en `N:/LetMeSleep/Repository` el 12 de septiembre de 2026. La corrida Unity más reciente, [`ALFA-INTEGRATED-EDITMODE-20260912.json`](ALFA-INTEGRATED-EDITMODE-20260912.json), pasó 61/61 e incluye los cuatro casos de réplica agregados después del recibo anterior de 51/51.

## Evidencia pendiente

G10 todavía exige el manifiesto geométrico finito, mutantes y revisión física/visual definidos en Gameplay; estos tests sólo prueban que Authority consulta al mundo cada tick y no conserva una marca tras perder contacto.
