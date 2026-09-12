# Suite de protocolo y propiedad de herramientas

Estado: **6/6 PASS** tanto en el arnés C# externo contra `fffa035` como en la corrida Unity EditMode integrada de **61/61** sobre `768ffa8` más cambios Root aún sin commit. El resultado Unity quedó en [`ALFA-INTEGRATED-EDITMODE-20260912.json`](ALFA-INTEGRATED-EDITMODE-20260912.json).

La suite `AlphaProtocolToolOwnershipTests` comprueba:

- coincidencia exacta entre `RoomSession.Protocol` alfa-2 y `GameplayWireCodec.Version=2`;
- rechazo del cliente alfa-1 sin mutar roster ni revisión de sala;
- roundtrip del snapshot con un humano dueño de un matamoscas;
- rechazo de propietario inexistente, propietario mosquito y humano equipado sin pickup asociado;
- rechazo de un paquete con versión binaria anterior sin entregar estado parcial.

Repetición desde la raíz del worktree, sin abrir Unity:

```powershell
pwsh -File docs/unity/qa/Run-ProtocolToolExternalHarness.ps1
```

Durante desarrollo en un worktree cuya rama todavía no contiene el codec, se puede indicar la fuente integrada:

```powershell
pwsh -File docs/unity/qa/Run-ProtocolToolExternalHarness.ps1 -SourceWorkspaceRoot N:\LetMeSleep\Repository
```

El arnés compila `RoomSession`, Gameplay puro, `GameplayWireCodec`, el soporte mínimo de sala y esta suite. No acredita transporte EOS, física Unity, orden entre canales ni WAN.
