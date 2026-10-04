# AgroLife v1 — Documento de diseño

- **Fecha:** 2026-10-04
- **Estado:** borrador para revisión

## 1. Visión

AgroLife es un juego y un simulador del sector agropecuario al mismo tiempo. El jugador elige una zona del mundo, consigue tierra y lleva adelante prácticas productivas con datos reales: clima, suelos, rindes y costos. A largo plazo va a incluir agricultura, ganadería, apicultura, forestación y otras actividades. La v1 es solo agricultura, en una región.

El objetivo es educativo: enseñar los conceptos y las operaciones del sector (planificación de campaña, margen bruto, rinde de indiferencia, manejo del agua, umbral de daño económico) jugando, no leyendo.

## 2. Público

- **Principal:** estudiantes de escuelas agrotécnicas y de agronomía.
- **Secundario:** gente del sector (productores, asesores) que quiere probar estrategias.

Qué implica para el diseño:
- **Vocabulario técnico real, sin infantilizar.** Los términos (V6, R1, rinde de indiferencia, margen bruto) aparecen tal cual, con una ayuda opcional que explica cada uno.
- **Todo resultado se explica.** El juego siempre muestra por qué se obtuvo un rinde, desglosado por causa.
- **Números creíbles.** Los rindes simulados caen dentro de los rangos históricos reales de la región. Esto se verifica con calibración (sección 9).

## 3. Alcance

### En la v1
- **Una región:** el partido de Pergamino (Buenos Aires, zona núcleo). El juego arranca directo ahí, sin pantalla de selección.
- **Cultivos:**
  - trigo,
  - maíz,
  - soja de primera,
  - doble cultivo trigo / soja de segunda.
- **Modo libre:** sin escenarios ni objetivos predefinidos.
- **Tierra:** compra y arrendamiento anual.
- **Labores:** todas por contratista.
- **Sanidad:** plagas y enfermedades como eventos, con decisión de aplicar o no.
- **Comercialización:** venta inmediata o almacenamiento en silobolsa.
- **Reporte de campaña** con desglose de pérdidas y margen bruto.
- **Guardar y cargar partida.**
- **Idioma:** español.
- **Plataforma:** PC (Windows). No se usa nada que impida una versión web en el futuro.

### Fuera de la v1
- Otras regiones y la pantalla de selección de región.
- Escenarios (es lo siguiente después de la v1).
- Maquinaria propia.
- Malezas.
- Riego.
- Granizo y seguros.
- Piso para cosecha.
- Efectos de rotación más allá del agua, el nitrógeno y el fósforo que el modelo ya arrastra.
- Ganadería, apicultura, forestación y demás actividades.
- Economía argentina real (inflación, retenciones, brecha cambiaria). Queda como posible modo "ultra hardcore" a futuro.

## 4. Experiencia de juego

### Tiempo
- Calendario de 365 días, sin años bisiestos.
- Las fechas se muestran como día/mes + "Año N" de la partida. El clima es generado, así que no se corresponde con años reales.
- La partida empieza el **1 de mayo del Año 1**.
- La simulación avanza **de a un día**.
- **Velocidades:** pausa, x1 (6 días por segundo, unos 5 segundos por mes), x2 y x4.

**Pausa automática** solo ante eventos que piden una decisión:
- plaga o enfermedad por encima del umbral en un lote propio,
- inicio de campaña, el 1 de mayo, cuando se muestra el pronóstico estacional,
- saldo cerca del límite de descubierto.

Los demás avisos (cosecha terminada, helada registrada, vencimiento de un arrendamiento) son notificaciones que no frenan el juego.

### Ciclo
1. **Capital inicial.** Su valor se define así: lo que cuesta arrendar 250 ha de índice de productividad promedio y sembrarlas con soja de primera, con los precios de referencia.
2. **Tierra.** Al hacer clic en un lote se abre su ficha: superficie, clase textural, índice de productividad (IP), agua útil, fósforo y cultivo anterior.
   - **Compra:** precio por ha = precio base de la región × IP / 100.
   - **Arrendamiento anual:** quintales de soja por ha = base de la región × IP / 100. Se paga completo al firmar, al precio de soja del día, y vence el 30 de abril siguiente.
3. **Planificación** de cada lote propio o arrendado:
   - cultivo,
   - fecha de siembra, dentro de la ventana válida,
   - genética: ciclo corto o largo,
   - nitrógeno: kg N/ha, solo en trigo y maíz,
   - fósforo: kg P/ha.

   Mientras se ajustan los valores, un presupuesto en vivo muestra costo total, rinde esperado, margen bruto proyectado y rinde de indiferencia.
4. **Campaña.**
   - El contratista siembra en la fecha planificada.
   - El cultivo avanza día a día con el clima generado.
   - Ante una plaga sobre el umbral, el jugador ve el costo de aplicar frente a la pérdida esperada, y decide.
5. **Cosecha automática** al madurar.
   - El contratista cobra un porcentaje del valor cosechado.
   - El flete hasta el acopio se cobra en el momento de la cosecha, según la distancia por caminos del lote al acopio más cercano.
   - El grano queda en stock, agrupado por tipo de grano.
6. **Comercialización.**
   - Vender al precio del día, menos la comisión.
   - O mantenerlo en silobolsa, con un costo de embolsado por tonelada y un riesgo mensual pequeño de perder una parte.
7. **Reporte de campaña** por lote:
   - un gráfico en escalera: rinde potencial → pérdida por cada causa → rinde real,
   - margen bruto, resultado después del arrendamiento y rinde de indiferencia,
   - comparación con el rinde promedio de la zona para el mismo cultivo.
8. **Reinvertir** y repetir.

### Plata
- La cuenta permite un descubierto hasta un límite fijo, con interés diario.
- Si el saldo baja del límite, la partida termina por quiebra.

### Lotes de terceros
Los lotes que no son del jugador también se simulan, con un plan típico de rotación y manejo estándar, y siempre tratan las plagas sobre el umbral. Esto sirve para tres cosas:
- el mapa se ve vivo,
- la capa de agua muestra todo el partido,
- el reporte puede comparar con el "promedio de la zona".

## 5. Arquitectura

### Tecnología
- Unity 6.3 LTS (6000.3.14f1).
- URP 2D.
- UI Toolkit.
- Unity Test Framework.
- Newtonsoft Json (paquete oficial `com.unity.nuget.newtonsoft-json`) para los datos y las partidas guardadas.

### Estructura del repo
```
AgroLife/                         ← raíz del repo
├── AgroLife/                     ← proyecto de Unity (6000.3.14f1)
│   └── Assets/
│       ├── Scripts/
│       │   ├── Sim/              ← AgroLife.Sim (asmdef con noEngineReferences: true)
│       │   └── Game/             ← AgroLife.Game (referencia a Sim)
│       ├── Tests/Sim/            ← AgroLife.Sim.Tests (EditMode)
│       └── Data/
│           ├── crops.json, pests.json, economy.json, soils.json, glosario.json
│           └── Regions/pergamino/  ← salida del pipeline
├── tools/data-pipeline/          ← scripts Python; cache/ ignorado por git
└── docs/
```

### AgroLife.Sim: el núcleo, C# puro
- **Sin `using UnityEngine`.** Se puede testear sin escenas y correr sin gráficos para calibrar.
- **Estado completo del juego:**
  - fecha,
  - lotes (suelo, agua, nitrógeno, fósforo, cultivo y etapa, factores de pérdida acumulados),
  - contratos,
  - stock de grano,
  - cuenta,
  - precios,
  - estado del generador aleatorio.
- **`StepDay()`** avanza un día y devuelve los eventos de ese día.
- **Acciones del jugador:** son métodos de `Simulation` (`ArrendarLote`, `ComprarLote`, `PlanificarCultivo`, `AplicarTratamiento`, `VenderGrano`, etc.). Validan el pedido y devuelven ok o el motivo del rechazo.
- **Números aleatorios:**
  - Un generador propio, chico y con estado serializable (tipo PCG32).
  - **Hay un flujo separado para cada subsistema** (clima, precios, plagas), cada uno sembrado desde una semilla maestra. Así las decisiones del jugador no alteran la secuencia de clima ni de precios: la misma semilla da el mismo clima. Esto también es necesario para los escenarios futuros.
- **Lote, suelo y clima no dependen del código de cultivos.** Cuando entren otras actividades, el lote va a poder alojar otra cosa sin reescribir el núcleo. En la v1 no se crea ninguna estructura genérica para eso.

### AgroLife.Game: Unity
- **Controlador de tiempo:** acumula el tiempo real según la velocidad y llama a `StepDay()` cada 1/6 s a x1. Si un evento pide una decisión, pausa.
- Lee el estado de Sim para dibujar el mapa y la interfaz. **Nunca lo modifica directamente:** todo pasa por las acciones.
- Carga los JSON de `Assets/Data` como `TextAsset` y los entrega a Sim.

### Datos como archivos
- **Generados por el pipeline:** lotes, geografía y parámetros de clima.
- **Editados a mano y versionados:**
  - parámetros de cultivos,
  - plagas,
  - economía,
  - tabla de suelos de respaldo,
  - glosario.

Con eso se puede ajustar el modelo sin tocar C#.

## 6. Modelo de simulación

Todo avanza día a día. Los valores concretos de cada parámetro están en los JSON y se ajustan con la calibración (sección 9).

### 6.1 Clima generado

**Generador estadístico tipo WGEN**, con parámetros por mes:
- **Si llueve o no:** depende de si llovió el día anterior (probabilidad de día lluvioso después de uno seco, y después de uno lluvioso).
- **Cuánto llueve:** se sortea de una distribución gamma.
- **Temperatura máxima, temperatura mínima y radiación:** distribución normal con media y desvío distintos según el día sea seco o lluvioso, y correlación con el día anterior.
- **Origen de los parámetros:** datos diarios de NASA POWER para Pergamino (aprox. −33,89, −60,57), período 1991–2025.

**El Niño / La Niña:**
- Al comienzo de cada campaña (1 de mayo) se sortea la fase (Niño, Neutro o Niña) con las frecuencias históricas. Las frecuencias se clasifican con el índice ONI de NOAA.
- La fase multiplica los parámetros de lluvia de agosto a marzo. Los multiplicadores se calculan separando los años históricos por fase.
- **Pronóstico que ve el jugador:** probabilidades para las tres fases. Con un 70% de probabilidad, el pronóstico favorece la fase real. Si no, favorece otra.

### 6.2 Suelo y agua

**Textura:** proporción de arena, limo y arcilla de cada lote, de SoilGrids. Con eso:
- se calcula la clase textural (triángulo USDA), que se muestra en la ficha,
- el modelo usa las proporciones continuas, no la clase.

**El "balde" de agua:**
- Un perfil único de 150 cm para todos los cultivos.
  - **Simplificación:** un solo balde para todos. Si la calibración lo pide, se pasa a perfiles por cultivo o por capas.
- **Agua útil máxima** = capacidad de campo − punto de marchitez, integrada hasta 150 cm. Ambos valores vienen de SoilGrids (agua retenida a 33 kPa y a 1.500 kPa).
- **Velocidad de infiltración y drenaje:** se estima con la fórmula de Saxton & Rawls (2006) a partir de arena, arcilla y materia orgánica. SoilGrids no trae este dato.

**Balance diario**, en este orden:
1. **Entrada de lluvia.** La parte que supera la capacidad de infiltración del día escurre.
2. **Consumo del cultivo.** Se parte de la evapotranspiración de referencia (fórmula de Hargreaves), se ajusta por la etapa del cultivo y se reduce cuando el suelo está seco. En suelo desnudo hay evaporación reducida.
3. **Drenaje.** El agua por encima de capacidad de campo drena según la velocidad de infiltración del suelo.
4. **Anegamiento.** Los días con agua por encima de capacidad de campo en suelos de drenaje lento suman estrés por anegamiento.

**Continuidad:** el agua queda guardada entre campañas. Así se representan el barbecho y lo que deja el trigo para la soja de segunda.

### 6.3 Nitrógeno y fósforo

**Nitrógeno:**
- **Oferta** = nitrato inicial + mineralización diaria + fertilizante.
  - La mineralización depende del carbono orgánico del suelo, la temperatura y la humedad.
- **Lavado:** el nitrógeno que se va cada día es proporcional a la fracción del agua del perfil que drenó ese día. Por eso un suelo arenoso con lluvias fuertes pierde fertilizante.
- **Demanda** = rinde esperado × necesidad por quintal, según el cultivo.
- La soja fija su propio nitrógeno, así que no tiene factor de pérdida por nitrógeno.

**Fósforo:**
- Fósforo Bray inicial por lote, sorteado de una distribución regional. No hay una capa pública de alta resolución.
- La respuesta al fósforo sigue una curva por cultivo.
- Al cerrar cada campaña se actualiza con un balance simple: fósforo aplicado − fósforo exportado en el grano.

### 6.4 Cultivo

**Etapas por grados-día:**
- Cada cultivo tiene su temperatura base.
- Los umbrales de cada etapa dependen del cultivo y la genética (`crops.json`).
- Etapas: siembra → emergencia → vegetativo → floración → llenado → madurez → cosecha.
- En soja, los umbrales se acortan con la siembra tardía (factor por día de atraso), como aproximación al efecto del fotoperíodo.

**Rinde:**
```
Rinde = Rpot(cultivo, genética) × f(IP)
        × (1 − Lfecha) × (1 − Lagua) × (1 − Lanegamiento) × (1 − Ltemp)
        × (1 − LN) × (1 − LP) × (1 − Lplagas)
```

- **f(IP):** factor lineal con el IP del lote; sus parámetros están en `crops.json`.

Cómo se calcula cada pérdida:
- **Lfecha:** una curva por cultivo según la distancia a la fecha óptima.
- **Lagua:** la suma diaria de (1 − consumo real / consumo potencial), ponderada por la sensibilidad de cada etapa. El período crítico pesa mucho más: alrededor de la floración en maíz, R3–R6 en soja y la antesis en trigo.
- **Lanegamiento:** los días anegados, ponderados por etapa.
- **Ltemp:** pérdida por cada helada (temperatura mínima bajo el umbral) y cada golpe de calor (temperatura máxima sobre el umbral) en las etapas sensibles.
- **LN y LP:** dependen de la relación entre oferta y demanda.
- **Lplagas:** la severidad de las plagas no tratadas.

**Para el gráfico del reporte**, las pérdidas se atribuyen en este orden fijo:
1. fecha,
2. agua,
3. anegamiento,
4. temperatura,
5. nitrógeno,
6. fósforo,
7. plagas.

Cada pérdida se expresa en quintales sobre el rinde que quedaba después de las anteriores.

**Cultivos de la v1 y ventanas de siembra iniciales** (valores de referencia en `crops.json`, ajustables):

| Cultivo | Ventana de siembra |
|---|---|
| Trigo | 20/5 – 20/7 |
| Maíz | 15/9 – 31/12 (temprano y tardío, según la fecha) |
| Soja de primera | 15/10 – 15/12 |
| Soja de segunda | desde la cosecha del trigo hasta el 15/1 |

### 6.5 Plagas y enfermedades

Definidas en `pests.json`. En la v1 hay una por cultivo:

| Cultivo | Plaga o enfermedad | Etapas |
|---|---|---|
| Soja (primera y segunda) | chinches | R3–R6 |
| Maíz | isoca cogollera | |
| Trigo | roya | ligada a la humedad |

**Cómo aparece:**
- Cada día hay una probabilidad de aparición que depende de la etapa del cultivo y del clima.
- Al aparecer, se sortea su severidad.

**Si supera el umbral en un lote propio:**
- El juego se pausa y muestra el nivel frente al umbral, el costo de aplicar (producto + pulverización) y la pérdida esperada en quintales y en dólares.
- **Si se aplica:** la pérdida se reduce según la eficacia del producto.
- **Si no se aplica:** la severidad se suma a Lplagas.

### 6.6 Economía ideal
- **Moneda:** todo en dólares, sin inflación, sin retenciones y sin brecha.
- **Precios:**
  - Precio diario por grano = referencia × factor estacional del mes × exp(x).
  - x es un proceso aleatorio que tiende a volver a cero.
  - Referencia, estacionalidad y volatilidad se toman de la historia real de precios de la Bolsa de Comercio de Rosario.
- **Costos fijos en dólares** (`economy.json`), con proporciones tomadas de referencias públicas:
  - labores: tarifario de FACMA,
  - insumos: informes de márgenes de la Bolsa de Cereales de Buenos Aires y de la BCR.
- **Fórmulas:**
  - **Margen bruto** = ingreso bruto − gastos de comercialización (flete, comisión) − costos directos (semilla, fertilizantes, agroquímicos, labores, cosecha).
  - **Resultado después de arrendamiento** = margen bruto − arrendamiento.
  - **Rinde de indiferencia** = costos directos por ha que no dependen del rinde / (precio neto × (1 − % de cosecha)).

## 7. Datos y pipeline

### Herramientas
- **Lenguaje:** Python 3.12, en `tools/data-pipeline/`.
- **Librerías:** requests, numpy, scipy, geopandas, shapely, pyproj, rasterio, osmnx.
- **Cache:** las descargas crudas se guardan en `tools/data-pipeline/cache/`, ignorada por git.
- **Salida:** los archivos que genera el pipeline **se versionan en el repo**, así Unity no necesita Python.

### Fuentes

| Dato | Fuente | Licencia |
|---|---|---|
| Clima diario | NASA POWER (precipitación, temperatura máxima y mínima, radiación) | Pública |
| Fases de El Niño / La Niña | Índice ONI, NOAA CPC | Pública |
| Textura, carbono orgánico, agua retenida | SoilGrids 250 m v2.0, por WCS o GeoTIFF (la API REST de ISRIC está pausada) | CC BY 4.0 |
| Unidades de suelo e IP | Cartas de suelo INTA, Buenos Aires 1:50.000 (Zenodo 7837681) | CC BY 4.0 |
| Contorno, caminos, arroyos, localidades, acopios | OpenStreetMap | ODbL |
| Rindes históricos (solo calibración) | Estimaciones agrícolas, Ministerio de Agricultura, por departamento | Pública |
| Precios históricos | Bolsa de Comercio de Rosario | Pública |

### Pasos
1. **Contorno del partido** y proyección a UTM, en metros, con el origen en el centro del partido.
2. **Lotes:**
   - Se arman las cuadras rurales a partir de los caminos de OpenStreetMap.
   - Se excluyen las zonas urbanas y una franja alrededor de los arroyos.
   - Cada cuadra se divide en lotes de 30 a 150 ha, con cortes paralelos a su lado largo.
   - Se esperan entre 3.000 y 5.000 lotes.
3. **Suelo de cada lote:**
   - Se muestrea SoilGrids en el centro del lote: arena, limo, arcilla, carbono orgánico y agua a 33 y 1.500 kPa hasta 150 cm.
   - Se toma de la carta INTA la unidad de suelo y su IP.
4. **Distancia por caminos al acopio más cercano**, calculada sobre la red de OpenStreetMap. Si OpenStreetMap no tiene acopios mapeados, se pone uno por localidad.
5. **Parámetros del generador de clima** y multiplicadores por fase de El Niño / La Niña.
6. **Controles automáticos** (con assert): todo lote tiene suelo e IP, las superficies están dentro del rango y los JSON tienen el formato esperado.

### Salida en `AgroLife/Assets/Data/Regions/pergamino/`

| Archivo | Contenido |
|---|---|
| `region.json` | metadatos, origen de la proyección, localidades, acopios, precios base de tierra y arrendamiento, distribución regional de fósforo |
| `lotes.json` | id, polígono, superficie, propiedades del suelo, IP, distancia al acopio |
| `geografia.json` | contorno, rutas, caminos, arroyos |
| `clima.json` | parámetros mensuales del generador, multiplicadores y frecuencias de fase |

## 8. Mapa e interfaz

### Mapa
- **Cada lote** es un GameObject con `PolygonCollider2D`.
  - El collider sirve para detectar el clic.
  - `Collider2D.CreateMesh()` genera la malla que se dibuja.
  - **Si el rendimiento con miles de lotes no alcanza:** se combinan las mallas en una sola por capa, con color por vértice.
- **Capas de color:**
  - Estado (la vista por defecto): libre / propio / arrendado, y cultivo con su etapa.
  - Clase textural.
  - IP.
  - Agua útil actual (%).
  - Rinde estimado.
- **Encima de los lotes:** rutas, arroyos, localidades y acopios.
- **Cámara** ortográfica con desplazamiento y zoom, limitada al contorno del partido.
- **Estilo** plano: color por cultivo, que se va oscureciendo con la etapa y pasa a amarillo a cosecha. Sin sprites.

### Interfaz (UI Toolkit)
- **Barra superior:** fecha, velocidad, saldo, pronóstico de El Niño / La Niña, acceso al mercado.
- **Ficha lateral del lote:** datos, triángulo textural, barra de agua útil, cultivo y etapa. Las acciones cambian según el estado del lote.
- **Planificación:** formulario con el presupuesto en vivo.
- **Decisión ante un evento:** qué pasó, costo frente a pérdida esperada, y los botones "Aplicar" y "No aplicar".
- **Mercado:** precios, gráfico de la evolución del precio, stock en silobolsa, venta.
- **Reporte de campaña:** gráfico en escalera de pérdidas y margen bruto por lote.
- **Glosario:** términos técnicos marcados, con ayuda emergente (`glosario.json`).
- **Gráficos:** se dibujan con `Painter2D`, sin librerías externas.
- **Pantallas:** solo dos, el menú (nueva partida / cargar) y el mapa.

## 9. Tests y calibración

### Tests de AgroLife.Sim (EditMode, NUnit)
- **Balance de agua:** lluvia = cambio en el agua guardada + consumo + drenaje + escurrimiento.
- **Textura:** con la misma lluvia, un suelo arenoso drena más rápido y lava más nitrógeno que uno arcilloso.
- **Etapas:** con temperaturas fijas, cada etapa llega en el día esperado.
- **Economía:** margen bruto, rinde de indiferencia, arrendamiento en quintales e interés del descubierto.
- **Acciones:** se rechazan cuando falta saldo, cuando la fecha está fuera de la ventana o cuando el lote no es del jugador.
- **Repetibilidad:** misma semilla, 10 años → estado idéntico.
- **Independencia del clima:** misma semilla con decisiones distintas del jugador → clima idéntico.
- **Guardar y cargar:** guardar → cargar → mismo estado.

### Calibración
Son tests marcados con `[Category("Calibration")]` que se corren a pedido.

**Clima:** se generan 1.000 años y se comparan con los datos de 1991–2025. Tolerancias:
- lluvia media mensual: ±10%,
- días de lluvia por mes: ±10%,
- temperatura media mensual: ±0,5 °C,
- diferencia de lluvia entre años Niño y Niña: mismo signo y orden de magnitud.

**Rindes:** se simulan 30 campañas o más por cultivo, con manejo típico y suelo típico del partido. Tolerancias frente a la última década del departamento Pergamino:
- rinde medio: ±15%,
- desvío estándar: ±30%.

### Pipeline y Game
- El pipeline se valida con los controles del paso 6 de la sección 7.
- Game (interfaz y mapa) se prueba a mano en la v1, sin tests automáticos de interfaz.

## 10. Manejo de errores
- **Acción rechazada:** se muestra el motivo al jugador. Por ejemplo: "Fuera de la ventana de siembra de maíz (15/9–31/12)".
- **Datos de región faltantes o inválidos:** error claro al arrancar. El juego no arranca con datos parciales.
- **Partidas guardadas:**
  - Se guardan en JSON en `Application.persistentDataPath/saves/`, con un campo `version`.
  - Se escriben en un archivo temporal que después reemplaza al anterior, así un cierre a mitad del guardado no destruye la partida previa.
  - Si la versión es incompatible, el juego avisa en lugar de romperse.

## 11. Riesgos conocidos y su respaldo

| Riesgo | Respaldo |
|---|---|
| Las cartas INTA no traen el IP o la textura en un formato usable | El IP sale de una tabla por clase textural y drenaje (`soils.json`) y la textura sale solo de SoilGrids. Es lo primero que se verifica en el pipeline. |
| Un solo balde de 150 cm no calibra bien | Perfiles por cultivo o por capas |
| El modelo de rinde por factores no calibra | Calcular la biomasa a partir de la radiación interceptada, manteniendo los factores de pérdida para el reporte |
| El render de miles de lotes es lento | Mallas combinadas por capa con color por vértice |

## 12. Después de la v1 (orientativo)
1. Escenarios: estado inicial preparado + objetivo + evaluación.
2. Una segunda región, por ejemplo el oeste arenoso de Buenos Aires, y la pantalla de selección de región.
3. Maquinaria propia frente a contratista.
4. Malezas, efectos de rotación, piso para cosecha, granizo y seguros.
5. Ganadería, luego apicultura (ligada a la floración de los cultivos) y forestación.
6. Modo de economía argentina real ("ultra hardcore").

## 13. Criterios de éxito de la v1
- Se puede jugar una partida libre de varios años en Pergamino con los cuatro planes de cultivo.
- La calibración de clima y de rindes pasa dentro de las tolerancias de la sección 9.
- Cada reporte de campaña explica el rinde obtenido, desglosado por causa.
- Guardar y cargar funciona sin perder estado.
