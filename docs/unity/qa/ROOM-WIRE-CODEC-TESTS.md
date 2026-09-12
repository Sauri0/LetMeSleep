# Suite EditMode — RoomWireCodec

Estado: **10/10 PASS en arnés externo; Unity Test Runner pendiente**.

La suite valida el codec binario puro de `RoomView`:

- roundtrip de sala Waiting con reglas, ready y nombres UTF-8;
- roundtrip de Playing con ronda y roles sorteados;
- cierre con owner retirado y roster restante o vacío;
- roster máximo de 16 dentro del límite de 8192 bytes;
- rechazo de owner esperado distinto, versión desconocida, bytes sobrantes y toda truncación posible de un paquete válido;
- rechazo de revisión/ronda/enum/bool/rol/reglas/mapa/capacidad/duplicados/identidades inválidos;
- UTF-8 inválido sin caracteres de reemplazo y límites medidos en bytes;
- mil paquetes pseudoaleatorios acotados que no pueden propagar excepciones del parser.

La ejecución externa debe compilar `RoomSession.cs`, `RoomWireCodec.cs`, el soporte Core y esta suite contra el mismo NUnit instalado. Esa evidencia no sustituye Unity Test Runner ni transporte EOS.

La primera ejecución detectó que una longitud de texto inválida propagaba `InvalidDataException` fuera de `TryDecode`. Después de añadir el rechazo acotado en Core, el barrido de truncaciones y los mil paquetes de fuzz pasan sin excepciones.
