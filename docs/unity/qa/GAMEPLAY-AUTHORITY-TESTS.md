# Suite EditMode — Gameplay Authority

Estado: **29/29 PASS en arnés C# externo; ejecución Unity pendiente tras integrar Gameplay**.

La ampliación agrega diez casos NUnit sobre `GameplayAuthority` y un `IGameplayWorld` falso controlable. El arnés externo compiló el runtime Gameplay actual de W1 junto con las 19 pruebas Core existentes y ejecutó los 29 casos. Esta comprobación detecta errores de C# y de estado puro, pero no acredita Test Runner, física, render ni red de Unity.

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

## Evidencia pendiente

Después de integrar `LetMeSleep.Gameplay`, ejecutar `LetMeSleep.Tests.EditMode` en el Unity Test Runner y conservar XML/log. G10 todavía exige el manifiesto geométrico finito, mutantes y revisión física/visual definidos en Gameplay; estos tests sólo prueban que Authority consulta al mundo cada tick y no conserva una marca tras perder contacto.
