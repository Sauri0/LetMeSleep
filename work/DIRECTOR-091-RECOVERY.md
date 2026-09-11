# Director — recuperación y estado del 11 de septiembre

Repositorio de integración: dejame-dormir, rama codex/0.9.1-video-polish.
El reinicio interrumpió procesos. Se recuperaron los índices de UI/QA con
respaldo, sin reemplazar sus archivos de trabajo. Los siete puestos conservan
su propiedad en TEAM-0.9.1.md. Objetivo vigente: publicar0.9.1 y después0.9.2.

## Integrado después de la recuperación

- UI7d43fcb -> 8d4d1f4; navegación, opciones seleccionadas, vistas y zoom.
- QA879a058/9edb121/2e22610/241663f/e461029 -> 1fb90b4/e2f5549/e207721/6c38eb7/21d0dd8.
- Movimiento f1796c2 -> 44e0c09: suavizado remoto, respuesta angular local exacta.
- Casa a8dd799 -> 43c4507: reserva6cm de acabados en descansos,204/0 en11semillas.
- Director4987609: attached_to público sólo durante picadura visible real;
  cero al estar libre, desprenderse o recibir golpe. Sin zona ni reserva futura.
- Director2861420: negativos del auditor para impedir IDs fuera de contacto.
- Build incluye gates nuevos; movimiento con ActorView requiere renderer nativo.
- Guías preparadas para0.9.1/protocolo10; todavía no es un paquete publicado.

## Hallazgos que frenan el cierre

1. Entrada bitten del primer cambio de movimiento añade10.54cm de salto.
   Worker1 corrige conservando filtro humano y trasladando insecto por delta
   de su humano visible después de sync_actors. Consumirá attached_to explícito,
   sin inferencias por proximidad. Pruebas del consumidor pendientes.
2. QA de casa nueva produjo5345checks/205fallos en11semillas. Se investigan
   rutas frente a hojas abiertas; no modificar asserts para esconder fallos.
3. Acabado/luces sobre casa nueva y rendimiento todavía requieren medición.

Resultados QA locales ya observados: contacto469/0; privacidad48/0;
procedural53/0 tras fix de descansos; online loopback27/0 (sinEOS/relay/WAN).
La evidencia final consolidada la entrega QA con códigos de salida y logs.

## Cola de motor

Una única sesión pesada cada vez, reserva y liberación explícitas.
QA cierra bloque lógico y diagnóstico de casa; después Worker2 entorno corto,
Worker1 corrección de adhesión, Modelador2 Blender/Godot de personajes0.9.2.
Director revalida candidato integrado antes de exportar y publicar.

## Trabajo0.9.2 conservado

Modelador1 prepara distribución doméstica/muebles en work/modeler092-*;
runtimev2 congelado salvo arreglos. Nueva geometría tras publicar requierev3.
Modelador2 prepara rasgos/ropa y debe revisar bandas dentadas de muñecas,
rayas cortadas de pijama y cuello desde primera persona, además de perfiles.
UI tiene commitc1fc717 de miniatura Seria para acompañar geometría0.9.2;
NO integrado en0.9.1 aún.

No se tocó work/voice09-acoustics-results.json (cambio generado previo).
No se publican video privado, cachés, credenciales ni SDK completo.
Última descarga existente sigue siendo v0.9.0-rc.1; fuente nueva en ramaGitHub.
