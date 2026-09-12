# Living crafted1 — ajuste localizado candidato

Base visual real: N:/LetMeSleep/Validation/Alfa-VisualRecovery/living-crafted1.png, 1920x1080, SHA-256 7E0A8E0F6AA5F25949CEC0902C7D5E2B79F72F74A987C7327EA6255F58B7D873. Director informa integración b7d9ca6 y BuildAlfaMaps PASS 19:53:20 UTC. PNG abierto y comparado con living-round5 y referencia EnvironmentQuality/01. No aprobación artística.

## Diagnóstico

La mesa muestra tablas/remates, el sofá bastidor y almohadones, y la cortina pliegues reales. Aún se ven cojines rígidos, respaldos muy oscuros y una gran pared uniforme. La lámpara a la derecha se lee apagada: fuente de Elementos ec36a946 crea pantalla y bulbo emisivos pero ninguna Light/ancla propia. Emisión de material no prueba iluminación sobre objetos vecinos. La forma de los cojines sigue siendo responsabilidad de geometría; no se intenta resolver elevando ambiente.

## Delta Presentation

Sólo AlfaLightingRig.cs (meta existente intacta): perfil exacto LightAnchor_Living_StandingLamp, exclusivo de casa, anterior al match Living genérico. Point cálida Color(1,.66,.36), intensidad .65, rango 2.4m, candidata a sombras Low. No crea ancla ni luz si falta el ancla: mapa anterior mantiene resultado anterior. El punto acompaña el bulbo real mediante su Transform, sin coordenadas de mueble duplicadas en runtime.

Sesgo exclusivo de esa lámpara: shadowBias .025, shadowNormalBias .08, shadowNearPlane .05, frente a .075/.35/.1 de luminarias generales. Valores candidatos para contacto cercano, sujetos a acne/fugas en captura. Se conservan máximo cuatro luces locales con sombra y dos por zona, alcance y perfil del plafón, luna, ambiente, reflectionIntensity, fills de lobby y bloom. Una point con sombras requiere varias caras de atlas: el límite de luces no acredita coste GPU aceptado; Director deberá revisar atlas/coste si se mantiene este perfil.

## Contrato solicitado a Elementos

Agregar LightAnchor_Living_StandingLamp bajo PresentationAnchors, centro mundial de bulbo (.65,1.37,3.85), manteniendo transformación coherente con el mapa. Ordenarlo inmediatamente después de LightAnchor_Living para reservar el segundo candidato Living antes de consumir presupuesto global. No modificar LightAnchor_Living ni Window_Glass. La luz efectiva se crea en Presentation, nunca duplicada por el builder de mapas.

Propuesta localizada de materiales, a cargo de Elementos: pantalla Quality_LampShade emisión(.38,.18,.045). Bulbo emisión(.70,.34,.09) en material separado, porque Quality_LampShadeInner también cubre el plafón. Mantener pantalla/bulbo sin ShadowCaster ni receiveShadows, opacos, sin especular/reflejo de entorno. Conservar sombras de base/asta y muebles. No subir emisión del plafón ni aplicar override global de materiales. Confirmación/commit del autor pendientes al preparar este documento.

## Verificación posterior, todavía no ejecutada

1. Director integra ambos commits y regenera sólo lo necesario; comprobar ancla única, posición del bulbo, Light Point activa/rango/color, sombra asignada y dos candidatos Living. Sin ancla, el perfil nuevo está inerte y no debe anunciarse corrección visible.
2. Captura A/B en encuadre crafted1, exposición/ambiente/cámara idénticos; alternar sólo nueva Light permite separar su contribución de materiales. Agregar esquina opuesta y detalle bajo de pantalla/base/sofá/estante.
3. Exigir que la pantalla se lea encendida conservando facetas, y un aporte cálido localizado alcance brazo/cojín próximo, pared cercana y base. No aceptar pared quemada, anillo de luz, sombra negra dura del asta, fuga a otra habitación ni pérdida de detalle. Si falla, ajustar punto/rango/intensidad o distribución local, nunca ambiente global para compensar.
4. Revisar contacto bajo base/mesa/cojines y self-shadow acne con estos sesgos. Registrar Light.shadows efectivo y atlas; verificar que no desplazó inadvertidamente sombras de otra zona. Recorrer después, no dar por válidas todas las vistas desde un PNG.
5. Registrar hash/build y comparación independiente del Revisor visual. El menú y la transmisión de alas siguen pendientes separados; este ajuste no cambia sus contratos.

Alcance de comprobación realizada: lectura de fuentes y PNG, diff --check. Sin compilación C#, importador, Play, render, escucha ni procesos nativos propios. La entrega prepara la comparación; no certifica la mejora visual.
