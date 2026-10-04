# AgroLife v1 — Plan 2: pipeline de datos de Pergamino

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un pipeline en Python que baja los datos públicos de Pergamino y genera `region.json`, `lotes.json`, `geografia.json` y `clima.json` en `AgroLife/Assets/Data/Regions/pergamino/`, listos para que `AgroLife.Sim` los cargue y simule.

**Architecture:**
- Paquete `pipeline` en `tools/data-pipeline/`, con un módulo por paso del spec §7: clima, suelos (SoilGrids e INTA), lotes y distancias. Funciones puras con tests de pytest.
- `descargas.py` y `osm.py` bajan todo a `cache/` (ignorada por git) y no repiten descargas.
- `__main__.py` orquesta los pasos, valida y recién ahí escribe las salidas. Las salidas se versionan.
- El último paso corre una simulación de un año en Unity con los datos reales.

**Tech Stack:** Python 3.12 con uv; requests, numpy, scipy, pandas, geopandas (con pyogrio), shapely, pyproj, rasterio, networkx y pypdf; pytest. Unity Test Framework para la prueba de integración.

**Spec:** [docs/superpowers/specs/2026-10-04-agrolife-v1-design.md](../specs/2026-10-04-agrolife-v1-design.md), §7, §11 (riesgo 1) y §9 (validación del pipeline). Contrato de los JSON: [Plan 1, Tarea 4](2026-10-04-agrolife-sim-nucleo.md).

### Hallazgos de la verificación previa (2026-10-04)

**1. Cartas de suelo del INTA** (Zenodo 7837681, CC BY 4.0)
- `1_Mapa_de_Suelos_BA_50000_V2.rar` trae un solo shapefile, `Suelos_BA_50mil_V2.shp`, en WGS84 y UTF-8, con 2.274 unidades.
- Campos útiles: `SIMBC`, `SERIE1..6`, `PORC1..6`, `CAP_USO` y `taxo_*`.
- **No trae IP, ni textura, ni drenaje.**
- El IP sí está en las **fichas por serie** (un PDF por serie), dentro de `3_Series de suelos y Perfiles representativos.rar`. Cada ficha dice, por ejemplo, "Índice de productividad según la región climática: 85,50 (A)".
- En el partido hay 22 series. El IP se leyó de 21. La que falta, "Wheelwright y el Recuerdo", es combinada: se reparte entre Wheelwright (31,5) y El Recuerdo (22,5).
- Conclusión: el riesgo 1 del spec se resuelve con datos del propio INTA.
  - El IP de una unidad es el promedio de los IP de sus series, ponderado por `PORC`.
  - **La tabla `respaldoIP` de `soils.json` que anticipaba el plan 1 no hace falta.**

**2. SoilGrids**
- En `files.isric.org` las capas `wv0033` y `wv1500` solo vienen agregadas a 1–5 km.
- El WCS de `maps.isric.org` sí las sirve a 250 m. Se verificó con `map=/map/<prop>.map`, `COVERAGEID=<prop>_<prof>_mean` y `SUBSET=long(...)`/`lat(...)` en EPSG:4326.
- Los valores vienen en int16. El 0 significa enmascarado (zona urbana o agua).

**3. NASA POWER**
- Endpoint `/api/temporal/daily/point` con `parameters=PRECTOTCORR,T2M_MAX,T2M_MIN,ALLSKY_SFC_SW_DWN` y `community=AG`.
- Los valores están en `properties.parameter.<P>.<YYYYMMDD>`. El faltante se marca −999.
- En 1991–2025 da 1.079 mm/año, y unos 10 días/mes con ≥ 1 mm en verano.

**4. OSM**
- El partido es la relación 2459663 (3.008 km²).
- Hay 6.689 km de vías, con 4.358 `track` y 879 `unclassified`: la red rural existe.
- Con GDAL, en el extracto: el límite sale en 27 s y las líneas del bbox en 18 s (51.912 líneas). Hay 5.664 `drain` y 561 `ditch`, que son canales de drenaje y no arroyos; solo `river` (36) y `stream` (626) llevan franja.
- **Overpass no es confiable desde acá.** `overpass-api.de` respondió una consulta y después cortó por timeout. Los espejos `overpass.kumi.systems` y `overpass.private.coffee` también fallaron.
- **Se usa el extracto de Geofabrik:**
  - `south-america/argentina-latest.osm.pbf`, 432 MB, que redirige a un archivo fechado (`argentina-261003.osm.pbf`).
  - Se lee con el driver OSM de GDAL que trae pyogrio (GDAL 3.12, solo lectura).
  - Es una sola descarga, reproducible y sin límites de uso.
  - El grafo vial se arma con networkx desde las líneas. **osmnx no se usa.**

**5. ONI** (`oni.ascii.txt`)
- Columnas `SEAS YR TOTAL ANOM`. La fila `NDJ 2015` corresponde a nov 2015 – ene 2016.

**6. Caché ya descargada en la verificación**, reutilizable con estos nombres:
- `cache/inta/suelos_ba_50000.rar`
- `cache/inta/series.rar`, ya extraídos
- `cache/nasa/power_pergamino_1991_2025.json`
- `cache/osm/argentina.osm.pbf`

(`cache/osmnx/` quedó de la exploración y no se usa.)

## Global Constraints

- **Python 3.12** con uv: `requires-python = ">=3.12,<3.13"`. Se corre desde `tools/data-pipeline/` con `uv run ...`. La máquina tiene 3.14 por defecto; uv usa el 3.12 instalado.
- **Dependencias:** las del spec, con dos cambios. Quedan requests, numpy, scipy, geopandas, shapely, pyproj y rasterio.
  - Sale osmnx: Overpass no responde, ver el hallazgo 4.
  - Entran networkx (para el grafo vial), pandas y **pypdf** (para el IP de las fichas).
  - pytest va como dependencia de desarrollo. No se agrega nada más.
- **Descargas:** solo a `tools/data-pipeline/cache/`, que está ignorada por git (`tools/data-pipeline/.gitignore` ya existe con `cache/`, `.venv/` y `__pycache__/`). Si el archivo ya está en caché, no se vuelve a bajar.
- **Salidas** en `AgroLife/Assets/Data/Regions/pergamino/` y versionadas. Se escriben **solo si la validación pasa**.
- **Contrato JSON:**
  - `lotes.json` usa los campos del plan 1, Tarea 4, más `poligono`.
  - `region.json` y `clima.json` también siguen el esquema del plan 1.
  - Todo tiene que pasar `DatosJuego.Desde` (Tarea 9).
- **Coordenadas:**
  - UTM 20S (EPSG:32720), en metros enteros.
  - El origen es el centroide del partido redondeado al metro, y se guarda en `region.json`.
  - Los polígonos van sin el punto de cierre repetido.
- **Determinismo:** con la misma caché, la salida es idéntica byte a byte. Para eso:
  - ids estables,
  - redondeos fijos,
  - nada de orden de `set` o `dict` sin ordenar,
  - nada de azar.
- **RAR:** se extrae con `bsdtar` (`C:\Windows\System32\tar.exe`, libarchive 3.8). La ruta se puede cambiar con la variable de entorno `BSDTAR`. El `tar` de Git Bash es GNU y no lee RAR.
- **Atribución:** `region.json` lleva una lista `fuentes` con nombre y licencia: NASA POWER (pública), NOAA CPC (pública), SoilGrids (CC BY 4.0), INTA (CC BY 4.0), OpenStreetMap (ODbL).
- Nombres y mensajes en español.
- **No commitear** (CLAUDE.md).

**Cómo correr tests:** `cd tools/data-pipeline && uv run pytest -q tests/<archivo>`. Para los de Unity (Tarea 9), el procedimiento del plan 1.

## Review Focus

1. **Una descarga que se corta a mitad** (el `.pbf` de 432 MB, un `.rar` o un GeoTIFF). Se escribe en `.part` y se renombra solo al terminar, así una corrida nueva vuelve a bajarla. Si después falla, el error dice qué archivo y de dónde. Nunca quedan salidas a medio escribir. → Tareas 1 y 8.
2. **El punto de un lote cae en un pixel enmascarado de SoilGrids o en una unidad "Misceláneas" del INTA.** Se usa el pixel válido más cercano, y la unidad con IP más cercana (a 500 m como máximo). Si no hay ninguno, el lote se descarta y se cuenta. Nunca sale un 0 como textura ni un IP nulo. → Tareas 3, 4 y 8.
3. **Arena + limo + arcilla de SoilGrids no suma 100.** Se normaliza, porque Sim rechaza desvíos de más de 2. → Tarea 3.
4. **Nombres de serie que no cruzan** (acentos, guiones bajos, "A y B") y dejan un IP faltante sin aviso. El pipeline afirma que toda serie de las unidades del partido, salvo "Misceláneas", tiene IP, y lista las que faltan. → Tareas 4 y 8.
5. **Volver a correr con la misma caché cambia la salida** (orden de lotes, flotantes) y ensucia los diffs de git. Se ordena y se redondea de forma estable, y se verifica corriendo dos veces. → Tareas 5 y 8.

---

### Task 1: Proyecto, configuración y descargas con caché

**Files:**
- Create: `tools/data-pipeline/pyproject.toml`
- Create: `tools/data-pipeline/pipeline/__init__.py` (vacío)
- Create: `tools/data-pipeline/pipeline/config.py`
- Create: `tools/data-pipeline/pipeline/descargas.py`
- Test: `tools/data-pipeline/tests/test_descargas.py`

**Interfaces:**
- Produces:
```python
# config.py — constantes; rutas absolutas derivadas de __file__
RAIZ_PIPELINE: Path; CACHE: Path; SALIDA: Path   # SALIDA = <repo>/AgroLife/Assets/Data/Regions/pergamino
REGION_ID = "pergamino"; REGION_NOMBRE = "Pergamino"; OSM_PARTIDO = "2459663"   # relación OSM
PUNTO_CLIMA = (-33.89, -60.57); PERIODO = ("19910101", "20251231"); UMBRAL_LLUVIA_MM = 1.0
EPSG_UTM = 32720
LOTE_HA_OBJETIVO, LOTE_HA_MIN, LOTE_HA_MAX = 80, 30, 150; LOTES_MIN, LOTES_MAX = 3000, 5000
FRANJA_ARROYO_M = 100; FRANJA_URBANA_M = 50
PRECIO_BASE_TIERRA_USD_HA = 14000; ARRENDAMIENTO_BASE_QQ_HA = 17      # referencias de zona núcleo, ajustables
FOSFORO_BRAY = {"media": 12, "desvio": 5, "minimo": 4}               # ppm, relevamientos regionales del norte de Bs. As.
VIAS_BLOQUE = ["motorway", "trunk", "primary", "secondary", "tertiary", "unclassified", "residential", "track",
               "motorway_link", "trunk_link", "primary_link", "secondary_link", "tertiary_link"]
VIAS_RUTA = ["motorway", "trunk", "primary", "secondary"]           # en geografia.json: "rutas"; el resto, "caminos"
GEOFABRIK_ARGENTINA = "https://download.geofabrik.de/south-america/argentina-latest.osm.pbf"
ZENODO_INTA = "https://zenodo.org/records/7837681/files/"
BSDTAR = os.environ.get("BSDTAR", r"C:\Windows\System32\tar.exe")

# descargas.py
def obtener(url: str, destino: Path, timeout: int = 600) -> Path   # baja solo si falta; escribe .part y renombra; 3 intentos
def extraer(archivo: Path, carpeta: Path) -> Path                  # bsdtar -xf en carpeta, solo si carpeta no existe
def nasa_power() -> pd.DataFrame        # DatetimeIndex diario; columnas lluvia, tmax, tmin, rad; falla si hay −999
def oni() -> pd.DataFrame               # columnas seas (str), anio (int), anom (float)
def soilgrids(prop: str, prof: str, bbox: tuple[float, float, float, float]) -> Path  # GeoTIFF EPSG:4326 en cache/soilgrids/
def inta_shapefile() -> Path            # cache/inta/1_Mapa_de_Suelos_BA_50000_V2/Suelos_BA_50mil_V2.shp
def inta_fichas() -> Path               # cache/inta/Series de suelos y Perfiles representativos/
```

- [ ] **Step 1: Crear `pyproject.toml` y sincronizar el entorno**

```toml
[project]
name = "agrolife-data-pipeline"
version = "0.1.0"
requires-python = ">=3.12,<3.13"
dependencies = ["requests", "numpy", "scipy", "pandas", "geopandas", "pyogrio", "shapely", "pyproj", "rasterio", "networkx", "pypdf"]

[dependency-groups]
dev = ["pytest"]

[tool.pytest.ini_options]
testpaths = ["tests"]
```
Correr `cd tools/data-pipeline && uv sync`. Esperado: crea `.venv` con Python 3.12 y `uv.lock`, que se versiona.

- [ ] **Step 2: Escribir los tests que fallan** (`test_descargas.py`)

```python
def test_obtener_no_repite_la_descarga(tmp_path, monkeypatch):
    llamadas = []
    class Resp:
        status_code = 200
        def raise_for_status(self): pass
        def iter_content(self, n): yield b"hola"
        def __enter__(self): return self
        def __exit__(self, *a): pass
    monkeypatch.setattr(descargas.requests, "get", lambda *a, **k: (llamadas.append(1), Resp())[1])
    destino = tmp_path / "x" / "a.bin"
    descargas.obtener("https://ejemplo/a", destino)
    descargas.obtener("https://ejemplo/a", destino)
    assert destino.read_bytes() == b"hola" and len(llamadas) == 1 and not destino.with_suffix(".bin.part").exists()

def test_oni_parsea_columnas(tmp_path, monkeypatch):
    texto = " SEAS  YR   TOTAL   ANOM\n  DJF 1950  25.01  -1.32\n  NDJ 2015  29.30   2.64\n"
    archivo = tmp_path / "oni.ascii.txt"; archivo.write_text(texto)
    monkeypatch.setattr(descargas, "obtener", lambda url, destino, **k: archivo)
    df = descargas.oni()
    assert list(df.columns) == ["seas", "anio", "anom"] and df.iloc[1].tolist() == ["NDJ", 2015, 2.64]
```

- [ ] **Step 3: Correr `uv run pytest -q tests/test_descargas.py`.** Esperado: FAIL con ImportError o AttributeError, porque no existe `pipeline.descargas`.

- [ ] **Step 4: Implementar `config.py` y `descargas.py`.**
  - `nasa_power`:
    - baja la URL del hallazgo 3 con `PUNTO_CLIMA` y `PERIODO` a `cache/nasa/power_pergamino_1991_2025.json`;
    - renombra PRECTOTCORR→lluvia, T2M_MAX→tmax, T2M_MIN→tmin, ALLSKY_SFC_SW_DWN→rad;
    - afirma que no quedan −999.
  - `oni`: baja `https://www.cpc.ncep.noaa.gov/data/indices/oni.ascii.txt` a `cache/oni.ascii.txt`.
  - `soilgrids`:
    - URL: `https://maps.isric.org/mapserv?map=/map/{prop}.map&SERVICE=WCS&VERSION=2.0.1&REQUEST=GetCoverage&COVERAGEID={prop}_{prof}_mean&FORMAT=image/tiff&SUBSET=long({xmin},{xmax})&SUBSET=lat({ymin},{ymax})&SUBSETTINGCRS=http://www.opengis.net/def/crs/EPSG/0/4326&OUTPUTCRS=http://www.opengis.net/def/crs/EPSG/0/4326`;
    - destino: `cache/soilgrids/{prop}_{prof}.tif`;
    - si la respuesta no es `image/tiff`, lanza `RuntimeError` con el texto del servidor.
  - `inta_shapefile` e `inta_fichas`: `obtener` + `extraer` con los nombres del hallazgo 6. Los nombres en Zenodo son `1_Mapa_de_Suelos_BA_50000_V2.rar` y `3_Series de suelos y Perfiles representativos.rar` (con `%20` en la URL).

- [ ] **Step 5: Correr `uv run pytest -q tests/test_descargas.py`.** Esperado: 2 passed.

---

### Task 2: Parámetros del clima (WGEN y El Niño / La Niña)

**Files:**
- Create: `tools/data-pipeline/pipeline/clima.py`
- Test: `tools/data-pipeline/tests/test_clima.py`

**Interfaces:**
- Consumes: el DataFrame de `descargas.nasa_power()` y el de `descargas.oni()`.
- Produces:
```python
MESES_ENSO = [8, 9, 10, 11, 12, 1, 2, 3]                       # agosto a marzo
def campania(fecha: pd.Timestamp) -> int                        # año si mes >= 5, si no año − 1
def markov(humedo: pd.Series) -> pd.DataFrame                   # índice 1..12; pSecoAHumedo, pHumedoAHumedo (transición t−1→t, contada en el mes de t)
def gamma_mensual(lluvia: pd.Series, humedo: pd.Series) -> pd.DataFrame   # índice 1..12; forma, escala (scipy.stats.gamma.fit, floc=0)
def medias_por_estado(df: pd.DataFrame, humedo: pd.Series) -> dict      # {mes: {"tmaxSeco": {"media", "desvio"}, ... 6 claves}}; desvío con ddof=1
def autocorrelacion(df: pd.DataFrame, humedo: pd.Series) -> dict        # {"tmax", "tmin", "rad"}: corr. lag-1 de los residuos estandarizados por mes y estado
def fase_por_campania(oni: pd.DataFrame, campanias: range) -> dict[int, str]  # media de ANOM de JAS, ASO, SON, OND, NDJ con anio == campaña; >= 0.5 Nino, <= −0.5 Nina, si no Neutro
def multiplicadores(lluvia: pd.Series, humedo: pd.Series, fases: dict[int, str]) -> dict[str, list[dict]]
def armar_clima(df: pd.DataFrame, oni: pd.DataFrame, umbral_mm: float) -> dict  # el dict de clima.json (esquema del plan 1)
```
`multiplicadores`:
- Se juntan los días de agosto a marzo de las campañas de cada fase.
- `frecuencia = (fracción de días húmedos de la fase) / (fracción de todas las campañas)`.
- `cantidad = (lluvia media de los días húmedos de la fase) / (la de todas las campañas)`.
- El mismo par se aplica a los 8 meses de `MESES_ENSO`. De abril a julio vale 1. Una fase sin campañas vale 1.
- Redondeo a 3 decimales.
- Se usa un solo par por fase, y no uno por mes, porque con unas 10 campañas por fase los valores mensuales son ruido. Si la calibración del plan 4 lo pide, se pasa a mensual.

`armar_clima`:
- Campañas = 1991..(último año con datos hasta marzo del año siguiente).
- `frecuencias` = la proporción de campañas de cada fase, en el orden Nino, Neutro, Nina.
- Redondeos: probabilidades 3 decimales, gamma 2, medias y desvíos 1, autocorrelaciones 2.

- [ ] **Step 1: Escribir los tests que fallan**

```python
def serie(fechas, valores): return pd.Series(valores, index=pd.DatetimeIndex(fechas))

def test_markov_cuenta_transiciones_en_el_mes_del_dia():
    lluvia = serie(pd.date_range("2000-01-01", periods=10), [0, 2, 3, 0, 0, 1.5, 0, 4, 0, 0])
    m = clima.markov(lluvia >= 1.0)
    assert m.loc[1, "pSecoAHumedo"] == pytest.approx(0.6) and m.loc[1, "pHumedoAHumedo"] == pytest.approx(0.25)

def test_gamma_recupera_los_parametros():
    x = scipy.stats.gamma.rvs(0.8, scale=15, size=20000, random_state=1)
    lluvia = serie(pd.date_range("2000-01-01", periods=20000), x)
    g = clima.gamma_mensual(lluvia, lluvia > 0)
    assert g.loc[1, "forma"] == pytest.approx(0.8, abs=0.04) and g.loc[1, "escala"] == pytest.approx(15, rel=0.06)

def test_medias_por_estado():
    idx = pd.date_range("2000-01-01", periods=4)
    df = pd.DataFrame({"tmax": [20, 30, 22, 34], "tmin": [10, 10, 12, 12], "rad": [5, 20, 7, 22]}, index=idx)
    r = clima.medias_por_estado(df, pd.Series([True, False, True, False], index=idx))
    assert r[1]["tmaxHumedo"]["media"] == 21 and r[1]["tmaxSeco"]["media"] == 32
    assert r[1]["tmaxSeco"]["desvio"] == pytest.approx(2.828, abs=0.001)

def test_autocorrelacion_de_un_ar1():
    rng = np.random.default_rng(2); z = np.zeros(20000)
    for i in range(1, len(z)): z[i] = 0.6 * z[i - 1] + 0.8 * rng.standard_normal()
    idx = pd.date_range("2000-01-01", periods=len(z))
    df = pd.DataFrame({"tmax": 25 + 3 * z, "tmin": 12 + 2 * z, "rad": 18 + 4 * z}, index=idx)
    assert clima.autocorrelacion(df, pd.Series(False, index=idx))["tmax"] == pytest.approx(0.6, abs=0.03)

def test_fase_por_campania():
    filas = [(s, a, v) for a, v in [(2015, 2.0), (2010, -1.2), (2012, 0.3)] for s in ["JAS", "ASO", "SON", "OND", "NDJ"]]
    oni = pd.DataFrame(filas, columns=["seas", "anio", "anom"])
    assert clima.fase_por_campania(oni, range(2010, 2016)) == {2010: "Nina", 2012: "Neutro", 2015: "Nino"} | {
        2011: "Neutro", 2013: "Neutro", 2014: "Neutro"}   # campañas sin filas: Neutro

def test_multiplicadores_comparan_contra_todas_las_campanias():
    idx = pd.date_range("2000-05-01", "2002-04-30")
    agosto_marzo = np.array([d.month in clima.MESES_ENSO for d in idx])
    camp = np.array([clima.campania(d) for d in idx])
    alterna = np.cumsum(agosto_marzo & (camp == 2001)) % 2 == 1     # 2001: día por medio, arrancando húmedo
    lluvia = pd.Series(np.where(agosto_marzo & ((camp == 2000) | alterna), 10.0, 0.0), index=idx)
    m = clima.multiplicadores(lluvia, lluvia >= 1, {2000: "Nino", 2001: "Nina"})
    assert m["Nino"][7]["frecuencia"] == pytest.approx(486 / 365, abs=0.001)   # agosto (índice 7)
    assert m["Nina"][0]["frecuencia"] == pytest.approx(244 / 365, abs=0.001)   # enero
    assert m["Nino"][7]["cantidad"] == 1 and m["Nino"][4] == {"frecuencia": 1, "cantidad": 1}   # mayo
    assert all(x == {"frecuencia": 1, "cantidad": 1} for x in m["Neutro"])
```

- [ ] **Step 2: Correr `uv run pytest -q tests/test_clima.py`.** Esperado: FAIL, porque no existe `pipeline.clima`.
- [ ] **Step 3: Implementar `clima.py`.**
  - Residuos para la autocorrelación: `(x − media del mes y estado) / desvío del mes y estado`. La correlación se calcula entre el día t y el t−1 sobre la serie completa.
  - En `armar_clima`, los meses van del 1 al 12 en orden y las claves con el formato del plan 1 (`pSecoAHumedo`, `tmaxSeco`, ...).
- [ ] **Step 4: Correr `uv run pytest -q tests/test_clima.py`.** Esperado: 6 passed.

---

### Task 3: Suelo desde SoilGrids

**Files:**
- Create: `tools/data-pipeline/pipeline/soilgrids.py`
- Test: `tools/data-pipeline/tests/test_soilgrids.py`

**Interfaces:**
- Consumes: `descargas.soilgrids`.
- Produces:
```python
PROF_TEXTURA = ["0-5cm", "5-15cm", "15-30cm"]; ESPESOR_TEXTURA = [5, 10, 15]
PROF_AGUA = ["0-5cm", "5-15cm", "15-30cm", "30-60cm", "60-100cm", "100-200cm"]; ESPESOR_AGUA_MM = [50, 100, 150, 300, 400, 500]  # hasta 150 cm
def textura_0_30(arena: list[float], limo: list[float], arcilla: list[float]) -> tuple[float, float, float]  # g/kg → %, ponderado y normalizado a 100
def corg_0_30(soc: list[float]) -> float                    # dg/kg → %, ponderado
def agua_150(wv: list[float]) -> float                      # 10^-3 cm³/cm³ por profundidad → mm hasta 150 cm
def leer_pixel(banda: np.ndarray, fila: int, col: int, radio: int = 5) -> float | None  # valor > 0 más cercano (distancia euclídea en celdas)
class Rasters:                                              # abre los 24 GeoTIFF y muestrea por punto lon/lat
    def __init__(self, bbox: tuple[float, float, float, float]): ...
    def suelo(self, lon: float, lat: float) -> dict | None  # {"arena", "limo", "arcilla", "corgPct", "ccMm", "pmpMm"} o None si falta algún valor
```
Propiedades que se piden:
- `sand`, `silt`, `clay` y `soc` en `PROF_TEXTURA`;
- `wv0033` y `wv1500` en `PROF_AGUA`.

En total son 24 pedidos WCS. El bbox es el del partido más 0,02° de margen.

- [ ] **Step 1: Escribir los tests que fallan**

```python
def test_textura_ponderada_0_30():
    assert soilgrids.textura_0_30([100, 200, 300], [600, 600, 500], [300, 200, 200]) == pytest.approx((23.333, 55.0, 21.667), abs=0.001)

def test_textura_se_normaliza_a_100():
    assert soilgrids.textura_0_30([100] * 3, [600] * 3, [200] * 3) == pytest.approx((11.111, 66.667, 22.222), abs=0.001)

def test_carbono_ponderado():
    assert soilgrids.corg_0_30([200, 150, 100]) == pytest.approx(1.3333, abs=0.0001)

def test_agua_hasta_150_cm():
    assert soilgrids.agua_150([300] * 6) == pytest.approx(450)
    assert soilgrids.agua_150([400, 400, 400, 300, 300, 200]) == pytest.approx(430)

def test_pixel_enmascarado_usa_el_valido_mas_cercano():
    b = np.zeros((5, 5)); b[2, 4] = 7; b[0, 0] = 9
    assert soilgrids.leer_pixel(b, 2, 2) == 7
    assert soilgrids.leer_pixel(np.zeros((5, 5)), 2, 2) is None
```

- [ ] **Step 2: Correr `uv run pytest -q tests/test_soilgrids.py`.** Esperado: FAIL, porque no existe el módulo.
- [ ] **Step 3: Implementar `soilgrids.py`.** `Rasters` abre cada `.tif` con rasterio, lee la banda 1 a memoria y usa `ds.index(lon, lat)` para pasar del punto a la celda.
- [ ] **Step 4: Correr `uv run pytest -q tests/test_soilgrids.py`.** Esperado: 5 passed.
- [ ] **Step 5: Probar contra el servicio real.** Correr `uv run python -c "from pipeline.soilgrids import Rasters; print(Rasters((-60.96,-34.2,-60.12,-33.52)).suelo(-60.67,-33.85))"`.
  - Esperado: un dict con arena + limo + arcilla = 100, `ccMm` > `pmpMm` y `ccMm − pmpMm` entre 100 y 400.
  - El punto es el perfil de la serie Pergamino.

---

### Task 4: IP desde las cartas del INTA

**Files:**
- Create: `tools/data-pipeline/pipeline/inta.py`
- Test: `tools/data-pipeline/tests/test_inta.py`

**Interfaces:**
- Consumes: `descargas.inta_shapefile`, `descargas.inta_fichas`.
- Produces:
```python
SERIES_SIN_IP = {"miscelaneasciudadesyopoblados"}           # claves que se ignoran sin error
def clave(nombre: str) -> str                               # NFKD → ASCII, minúsculas, solo letras
def ip_de_ficha(texto: str) -> float | None                 # r"ndice de productividad[^:]*:\s*([0-9]+(?:[.,][0-9]+)?)"
def partes(serie: str) -> list[str]                         # "Wheelwright y el Recuerdo" → ["Wheelwright", "el Recuerdo"]; si no, [serie]
def ip_unidad(fila: dict, ips: dict[str, float]) -> float | None  # promedio de IP ponderado por PORCi, solo series con IP; None si ninguna
def tabla_ip(fichas: Path, series: list[str]) -> tuple[dict[str, float], list[str]]  # (clave → IP, series sin ficha o sin IP)
def unidades(shp: Path, limite_4326: gpd.GeoDataFrame) -> gpd.GeoDataFrame  # UC que tocan el partido: SIMBC, ip, geometry (EPSG:32720)
```
- En `ip_unidad`, una serie combinada reparte su `PORC` en partes iguales.
- `tabla_ip` lee con pypdf las primeras 2 páginas de cada ficha. El archivo se busca por `clave(nombre del archivo sin extensión)`.
- `unidades` filtra con el `bbox` del límite, calcula `ip` con `ip_unidad` y lanza `AssertionError` si alguna serie, salvo las de `SERIES_SIN_IP`, queda sin IP. El mensaje lista esas series.

- [ ] **Step 1: Escribir los tests que fallan**

```python
IPS = {inta.clave(k): v for k, v in {"Pergamino": 85.5, "Ramallo": 65, "Wheelwright": 31.5, "El Recuerdo": 22.5}.items()}
def uc(*pares):
    fila = {f"SERIE{i}": "" for i in range(1, 7)} | {f"PORC{i}": 0 for i in range(1, 7)}
    for i, (s, p) in enumerate(pares, 1): fila[f"SERIE{i}"], fila[f"PORC{i}"] = s, p
    return fila

def test_clave_ignora_acentos_y_guiones():
    assert inta.clave("Santa_Lucía") == inta.clave("Santa Lucia") == "santalucia"

def test_ip_de_la_ficha():
    assert inta.ip_de_ficha("Índice de productividad según la región climática:\n85,50 (A)") == 85.5
    assert inta.ip_de_ficha("Indice de productividad: 90") == 90
    assert inta.ip_de_ficha("sin dato") is None

def test_ip_de_la_unidad_pondera_por_porcentaje():
    assert inta.ip_unidad(uc(("Pergamino", 60), ("Ramallo", 40)), IPS) == pytest.approx(77.3)
    assert inta.ip_unidad(uc(("Pergamino", 70), ("Desconocida", 30)), IPS) == pytest.approx(85.5)

def test_serie_combinada_se_reparte():
    assert inta.ip_unidad(uc(("Wheelwright y el Recuerdo", 100)), IPS) == pytest.approx(27.0)

def test_miscelaneas_no_tiene_ip():
    assert inta.ip_unidad(uc(("Miscelaneas ciudades y o poblados", 100)), IPS) is None
```

- [ ] **Step 2: Correr `uv run pytest -q tests/test_inta.py`.** Esperado: FAIL, porque no existe el módulo.
- [ ] **Step 3: Implementar `inta.py`.**
- [ ] **Step 4: Correr `uv run pytest -q tests/test_inta.py`.** Esperado: 5 passed.
- [ ] **Step 5: Probar contra los datos reales.** Correr `uv run python -c "import pipeline.inta as i, pipeline.descargas as d, pipeline.osm as o; u=i.unidades(d.inta_shapefile(), o.limite_partido()); print(len(u), u.ip.describe())"`.
  - Esperado: unas 97 unidades, IP entre 3 y 95, y ninguna serie sin IP.

---

### Task 5: Lotes a partir de los caminos

**Files:**
- Create: `tools/data-pipeline/pipeline/lotes.py`
- Test: `tools/data-pipeline/tests/test_lotes.py`

**Interfaces:**
- Produces (todo en metros, EPSG:32720):
```python
def bloques(lineas: list[LineString], limite: Polygon) -> list[Polygon]   # polygonize(unary_union(lineas + [limite.boundary])); se quedan los de representative_point dentro del límite
def excluir(bloques: list[Polygon], zonas: BaseGeometry) -> list[Polygon] # diferencia; los MultiPolygon se separan en partes
def dividir(bloque: Polygon, ha_obj: float = 80, ha_min: float = 30, ha_max: float = 150) -> list[Polygon]
def ordenar(lotes: list[Polygon]) -> list[Polygon]                         # por (−y, x) del representative_point redondeado al metro
def a_local(p: Polygon, x0: int, y0: int) -> list[list[int]]               # anillo exterior simplificado a 2 m, menos el origen, redondeado, sin el punto de cierre
```
Algoritmo de `dividir` (el resto lo determinan las firmas):
```
si área < ha_min: []
θ = ángulo del lado más largo de bloque.minimum_rotated_rectangle
r = rotate(bloque, −θ, origin=centroide)                 # el lado largo queda sobre el eje x
n = max(1, round(área/ha_obj)); mientras área/n > ha_max: n += 1; mientras n > 1 y área/n < ha_min: n −= 1
franjas: cortes horizontales en y = miny + k·(maxy − miny)/n, k = 1..n−1    # paralelos al lado largo
piezas = (r ∩ franja) separadas en partes, rotadas +θ con el mismo origen
por pieza: < ha_min se descarta; > ha_max → dividir(pieza) recursivo; si no, se queda
```

- [ ] **Step 1: Escribir los tests que fallan**

```python
def test_bloques_salen_de_los_caminos():
    b = lotes.bloques([LineString([(500, -10), (500, 1010)]), LineString([(-10, 500), (1010, 500)])], box(0, 0, 1000, 1000))
    assert len(b) == 4 and all(p.area == pytest.approx(250_000) for p in b)

def test_excluir_franja_de_arroyo():
    partes = lotes.excluir([box(0, 0, 1000, 1000)], LineString([(500, -10), (500, 1010)]).buffer(100, cap_style="flat"))
    assert sorted(round(p.area) for p in partes) == [400_000, 400_000]

def test_rectangulo_en_franjas_paralelas_al_lado_largo():
    piezas = lotes.dividir(box(0, 0, 3000, 1000))
    assert len(piezas) == 4
    assert all(p.area / 1e4 == pytest.approx(75, abs=0.1) for p in piezas)
    assert all(p.bounds[2] - p.bounds[0] == pytest.approx(3000, abs=1) for p in piezas)

def test_piezas_fuera_de_rango():
    assert lotes.dividir(box(0, 0, 400, 500)) == []                    # 20 ha
    assert len(lotes.dividir(box(0, 0, 600, 600))) == 1                # 36 ha
    assert all(30 <= p.area / 1e4 <= 150 for p in lotes.dividir(Polygon([(0, 0), (5000, 0), (5000, 300), (0, 2500)])))

def test_orden_estable_norte_a_sur_oeste_a_este():
    a, b, c = box(0, 100, 10, 110), box(100, 100, 110, 110), box(0, 0, 10, 10)
    assert lotes.ordenar([c, b, a]) == [a, b, c]

def test_coordenadas_locales():
    assert lotes.a_local(box(1000, 2000, 1100, 2100), 1000, 2000) == [[100, 0], [100, 100], [0, 100], [0, 0]]
```
El orden de vértices de `a_local` sigue el de shapely para `box` (antihorario desde (maxx, miny)).

- [ ] **Step 2: Correr `uv run pytest -q tests/test_lotes.py`.** Esperado: FAIL, porque no existe el módulo.
- [ ] **Step 3: Implementar `lotes.py`.**
- [ ] **Step 4: Correr `uv run pytest -q tests/test_lotes.py`.** Esperado: 6 passed.

---

### Task 6: OSM (extracto de Geofabrik) y distancia por caminos al acopio

**Files:**
- Create: `tools/data-pipeline/pipeline/osm.py`
- Create: `tools/data-pipeline/pipeline/distancias.py`
- Test: `tools/data-pipeline/tests/test_distancias.py`

**Interfaces:**
- Consumes: `descargas.obtener`, `config.GEOFABRIK_ARGENTINA`.
- Produces:
```python
# osm.py — todo sale de cache/osm/argentina.osm.pbf con pyogrio (driver OSM de GDAL)
def extracto() -> Path                                       # obtener(GEOFABRIK_ARGENTINA, CACHE/"osm"/"argentina.osm.pbf")
def limite_partido() -> gpd.GeoDataFrame                     # capa "multipolygons", where="osm_id = '2459663'"; EPSG:4326; cache/osm/limite.gpkg
def capa(nombre: str, bbox: tuple[float, float, float, float]) -> gpd.GeoDataFrame
    # nombre ∈ {"lines", "points", "multipolygons"}; read_dataframe(extracto(), layer=nombre, bbox=bbox); cache/osm/<nombre>.gpkg
def vias(lineas: gpd.GeoDataFrame) -> gpd.GeoDataFrame       # highway ∈ VIAS_BLOQUE
def arroyos(lineas) -> gpd.GeoDataFrame                      # waterway ∈ {river, stream}: los drain/ditch (≈ 6.200 canales de drenaje) no llevan franja
def urbano(multipoligonos) -> gpd.GeoDataFrame               # landuse ∈ {residential, commercial, industrial, retail}
def localidades(puntos) -> gpd.GeoDataFrame                  # place ∈ {city, town, village} con name
def silos(puntos, multipoligonos) -> gpd.GeoDataFrame        # man_made == "silo" (puntos y polígonos), o "building=silo" en other_tags; con name; polígono → centroide

# distancias.py — en EPSG_UTM
def grafo_vial(lineas: list[LineString]) -> nx.Graph         # nodo = vértice (round(x, 2), round(y, 2)); arista entre vértices consecutivos con length
def nodos_cercanos(G: nx.Graph, puntos: list[tuple[float, float]]) -> list    # scipy.spatial.cKDTree sobre los nodos
def distancias_km(G, fuentes: list, destinos: list) -> list[float]            # multi_source_dijkstra_path_length(weight="length") / 1000
def acopios(silos: gpd.GeoDataFrame, localidades: gpd.GeoDataFrame, radio_m: float = 1000, minimo: int = 3) -> list[dict]
    # silos con nombre agrupados a menos de radio_m; si hay >= minimo grupos: uno por grupo
    # ({"nombre": el nombre más frecuente, "x", "y": centroide}); si no: uno por localidad ({"nombre": "Acopio " + name, ...})
```
- Las vías de OSM comparten vértices en los cruces. Por eso, usar cada vértice redondeado como nodo arma la topología sin tener que cortar líneas.
- El `bbox` de `capa` es el del partido más 0,05°, que cubre el margen de 2 km del grafo.
- Leer una capa recorre los 432 MB. En la verificación tardó 27 s para el límite y 18 s para `lines`. Igual se lee solo `lines`, `points` y `multipolygons`, una vez cada una, y se guardan.
- Distancia de un lote:
  - nodo más cercano al `representative_point`;
  - más la distancia por grafo desde el acopio más cercano (`fuentes` = nodos más cercanos a los acopios);
  - más la recta del punto al nodo;
  - redondeada a 0,1 km.

- [ ] **Step 1: Escribir los tests que fallan**

```python
def test_grafo_une_vias_en_vertices_compartidos():
    G = distancias.grafo_vial([LineString([(0, 0), (1000, 0)]), LineString([(1000, 0), (1000, 500)]), LineString([(0, 0), (0, 300)])])
    assert G.number_of_nodes() == 4
    assert nx.shortest_path_length(G, (0.0, 0.0), (1000.0, 500.0), weight="length") == pytest.approx(1500)

def test_nodo_mas_cercano():
    G = distancias.grafo_vial([LineString([(0, 0), (1000, 0)])])
    assert distancias.nodos_cercanos(G, [(990, 10), (-5, 3)]) == [(1000.0, 0.0), (0.0, 0.0)]

def test_distancia_al_acopio_mas_cercano():
    G = nx.Graph(); G.add_edge("a", "b", length=1000); G.add_edge("b", "c", length=2000); G.add_edge("c", "d", length=500)
    assert distancias.distancias_km(G, ["a", "d"], ["b", "c"]) == [pytest.approx(1.0), pytest.approx(0.5)]

def puntos(xy, **cols): return gpd.GeoDataFrame(cols, geometry=[Point(p) for p in xy], crs=32720)

def test_sin_silos_hay_un_acopio_por_localidad():
    a = distancias.acopios(puntos([]), puntos([(0, 0), (9000, 0)], name=["Pergamino", "Manuel Ocampo"]))
    assert [x["nombre"] for x in a] == ["Acopio Pergamino", "Acopio Manuel Ocampo"]

def test_silos_cercanos_forman_un_acopio():
    s = puntos([(0, 0), (300, 0), (5000, 0), (5200, 100), (20000, 0)], name=["Coop A", "Coop A", "ACA", "ACA", "Silos X"])
    a = distancias.acopios(s, puntos([(0, 0)], name=["Pergamino"]))
    assert [x["nombre"] for x in a] == ["Coop A", "ACA", "Silos X"] and a[0]["x"] == pytest.approx(150)
```

- [ ] **Step 2: Correr `uv run pytest -q tests/test_distancias.py`.** Esperado: FAIL, porque no existe el módulo.
- [ ] **Step 3: Implementar `distancias.py` y `osm.py`.**
  - Los grupos de `acopios` salen en el orden en que aparece su primer silo en la entrada, para que la salida sea estable.
  - En `silos`, `building=silo` va en `other_tags` con el formato `"building"=>"silo"`; se filtra con `str.contains`.
- [ ] **Step 4: Correr `uv run pytest -q tests/test_distancias.py`.** Esperado: 5 passed.
- [ ] **Step 5: Probar contra el extracto real.** Correr `uv run python -c "import pipeline.osm as o; l=o.limite_partido(); b=tuple(l.total_bounds); print(round(l.to_crs(32720).area.iloc[0]/1e6), len(o.vias(o.capa('lines', b))), len(o.localidades(o.capa('points', b))))"`.
  - Esperado: unos 3.008 km², más de 10.000 vías y unas 10 localidades.
  - La primera corrida tarda menos de un minuto por capa; las siguientes leen el `.gpkg`.

### Task 7: Validación de las salidas

**Files:**
- Create: `tools/data-pipeline/pipeline/validar.py`
- Test: `tools/data-pipeline/tests/test_validar.py`

**Interfaces:**
- Produces:
```python
def validar(region: dict, lotes: list[dict], clima: dict, geografia: dict,
            lotes_min: int = LOTES_MIN, lotes_max: int = LOTES_MAX) -> list[str]   # lista de errores; vacía = ok
```
Reglas y texto exacto de cada error:

| Regla | Mensaje |
|---|---|
| Cantidad de lotes | `cantidad de lotes {n} fuera de [{min}, {max}]` |
| Ids 1..N consecutivos | `ids no consecutivos en la posición {i}` |
| Superficie de 30 a 150 ha | `lote {id}: superficie {ha} ha fuera de [30, 150]` |
| Arena + limo + arcilla = 100 ± 0,5 | `lote {id}: textura suma {s}` |
| 0 < ip ≤ 100 | `lote {id}: ip {ip} fuera de rango` |
| ccMm > pmpMm ≥ 0 | `lote {id}: agua cc {cc} <= pmp {pmp}` |
| corg ≥ 0 | `lote {id}: corg negativo` |
| 0 ≤ distanciaAcopioKm < 100 | `lote {id}: distancia {d} fuera de rango` |
| Polígono de 3 o más vértices | `lote {id}: polígono con menos de 3 vértices` |
| clima: 12 meses | `clima: {n} meses` |
| clima: probabilidades en [0, 1] | `clima: mes {m}: probabilidad fuera de [0, 1]` |
| clima: forma y escala > 0 | `clima: mes {m}: gamma inválida` |
| clima: frecuencias de fase suman 1 ± 0,01 | `clima: frecuencias suman {s}` |
| clima: abril a julio con multiplicador 1 | `clima: {fase} mes {m} debería valer 1` |
| region: tiene acopios y localidades | `region: sin acopios` / `region: sin localidades` |
| geografia: contorno de 3 o más puntos | `geografia: contorno vacío` |

En los mensajes, `ha` va con 1 decimal y `s` con 2.

- [ ] **Step 1: Escribir los tests que fallan.** Usan un caso mínimo válido armado en el test: 1 lote de 80 ha con textura 20/65/15, IP 85, cc 560, pmp 260 y distancia 15. El clima se arma con el `clima.json` de la región de prueba del plan 1: `Path(__file__).resolve().parents[3] / "AgroLife/Assets/Tests/Sim/Datos/clima.json"`.

```python
def test_caso_valido_no_tiene_errores():
    assert validar.validar(REGION, [LOTE], CLIMA, GEO, lotes_min=1, lotes_max=10) == []

def test_errores_de_lote():
    malo = LOTE | {"superficieHa": 20.0, "arena": 10}
    e = validar.validar(REGION, [malo], CLIMA, GEO, lotes_min=1, lotes_max=10)
    assert "lote 1: superficie 20.0 ha fuera de [30, 150]" in e and "lote 1: textura suma 90.00" in e

def test_cantidad_de_lotes():
    assert "cantidad de lotes 1 fuera de [3000, 5000]" in validar.validar(REGION, [LOTE], CLIMA, GEO)

def test_multiplicador_fuera_de_temporada():
    clima = copy.deepcopy(CLIMA); clima["enso"]["multiplicadores"]["Nino"][5]["frecuencia"] = 1.2
    assert "clima: Nino mes 6 debería valer 1" in validar.validar(REGION, [LOTE], clima, GEO, lotes_min=1, lotes_max=10)
```

- [ ] **Step 2: Correr `uv run pytest -q tests/test_validar.py`.** Esperado: FAIL, porque no existe el módulo.
- [ ] **Step 3: Implementar `validar.py`.**
- [ ] **Step 4: Correr `uv run pytest -q tests/test_validar.py`.** Esperado: 4 passed.

---

### Task 8: Orquestación y corrida real

**Files:**
- Create: `tools/data-pipeline/pipeline/__main__.py`
- Create (generados): `AgroLife/Assets/Data/Regions/pergamino/{region,lotes,geografia,clima}.json`
- Test: `tools/data-pipeline/tests/test_main.py`

**Interfaces:**
- Consumes: todo lo anterior.
- Produces: `python -m pipeline` y `escribir(carpeta: Path, archivos: dict[str, object]) -> None`.
  - `escribir` usa JSON UTF-8 con `ensure_ascii=False`.
  - `region.json` y `clima.json` van con `indent=2`. `lotes.json` y `geografia.json` van sin indentar y con `separators=(",", ":")`.
  - Primero escribe todo en `<carpeta>.tmp/` y después reemplaza los archivos uno por uno.

**Orden de `main()`:**
1. Contorno del partido y capas de OSM:
   - `limite = osm.limite_partido()`; se proyecta a `EPSG_UTM`.
   - `x0, y0` = el centroide redondeado.
   - `latitud`/`longitud` = el centroide en WGS84, a 4 decimales.
   - `bbox` = el del límite más 0,05°; `lineas, puntos, poligonos = osm.capa("lines" | "points" | "multipolygons", bbox)`.
2. Clima: `clima.armar_clima(descargas.nasa_power(), descargas.oni(), UMBRAL_LLUVIA_MM)`.
3. Unidades INTA: `uc = inta.unidades(descargas.inta_shapefile(), limite)`. Las unidades sin IP (Misceláneas) van a las zonas excluidas.
4. Zonas excluidas = unión de tres cosas:
   - `osm.urbano(poligonos)` con buffer de `FRANJA_URBANA_M`,
   - las unidades sin IP con buffer de `FRANJA_URBANA_M`,
   - `osm.arroyos(lineas)` con buffer de `FRANJA_ARROYO_M`.
5. Lotes:
   - `lotes.bloques(osm.vias(lineas) recortadas al límite, límite)`, luego `excluir`, luego `dividir` a cada parte, luego `ordenar`.
6. Suelo de cada lote, con `pt = representative_point`:
   - SoilGrids en `pt` (pasado a WGS84);
   - IP y `SIMBC` de la unidad que contiene `pt`. Si no hay unidad con IP en `pt`, se usa la más cercana a 500 m o menos.
   - Si falta algo, el lote se descarta y se cuenta por motivo.
   - Después de descartar, se renumeran los ids de 1 a N.
7. Distancias:
   - `acopios(osm.silos(puntos, poligonos), osm.localidades(puntos))` en EPSG:32720;
   - `G = distancias.grafo_vial(osm.vias(lineas) dentro del límite con buffer de 2 km)`;
   - `distanciaAcopioKm` de cada lote.
8. Armado de los dicts:
   - **region:** `id`, `nombre`, `latitud`, `longitud`, `precioBaseTierraUsdHa`, `arrendamientoBaseQqHa`, `fosforoBray`, `origenUtm {epsg, x, y}`, `localidades [{nombre, tipo, x, y}]`, `acopios [{nombre, x, y}]` (x/y locales), `fuentes`.
   - **lotes:** `{"lotes": [{id, superficieHa (1 decimal), arena, limo, arcilla (1 decimal), corgPct (2), ccMm, pmpMm (0), ip (1), unidadSuelo, distanciaAcopioKm (1), poligono}]}`.
   - **geografia:** `{contorno, rutas, caminos, arroyos}`. Las líneas se simplifican a 10 m y van en coordenadas locales enteras. `rutas` son las vías de `VIAS_RUTA`; `caminos`, el resto.
   - **clima:** el de la etapa 2.
9. `errores = validar.validar(...)`. Si hay errores, se imprimen y el proceso sale con código 1 sin escribir nada. Si no, `escribir(SALIDA, ...)`.
10. Resumen impreso:
    - cantidad de lotes y hectáreas,
    - IP medio ponderado,
    - agua útil media,
    - lotes descartados por motivo,
    - lluvia anual esperada del generador (Σ meses de π·días·forma·escala),
    - frecuencias de fase.

- [ ] **Step 1: Escribir el test que falla** (`test_main.py`)

```python
def test_escribir_reemplaza_sin_dejar_temporales(tmp_path):
    carpeta = tmp_path / "pergamino"; carpeta.mkdir(); (carpeta / "region.json").write_text("viejo")
    __main__.escribir(carpeta, {"region.json": {"id": "pergamino", "nombre": "Pergamino"}, "lotes.json": {"lotes": []}})
    assert json.loads((carpeta / "region.json").read_text(encoding="utf-8"))["nombre"] == "Pergamino"
    assert (carpeta / "lotes.json").read_text(encoding="utf-8") == '{"lotes":[]}'
    assert not (tmp_path / "pergamino.tmp").exists()
```

- [ ] **Step 2: Correr `uv run pytest -q tests/test_main.py`.** Esperado: FAIL, porque no existe `pipeline.__main__`.
- [ ] **Step 3: Implementar `__main__.py`.**
- [ ] **Step 4: Correr `uv run pytest -q`.** Esperado: todos pasan (34 tests).
- [ ] **Step 5: Corrida real.** Correr `uv run python -m pipeline`.
  - Esperado: sale con código 0.
  - El resumen muestra entre 3.000 y 5.000 lotes, un IP medio entre 60 y 85, agua útil media entre 150 y 350 mm, lluvia anual esperada de 1.079 ± 10 % y frecuencias que suman 1.
  - Si falla una validación, corregir la causa en el paso que corresponda. Los umbrales no se tocan.
- [ ] **Step 6: Determinismo.** Copiar los cuatro JSON a un temporal, volver a correr `uv run python -m pipeline` y comparar con `cmp`. Esperado: archivos idénticos.

---

### Task 9: Integración con Sim: un año en Pergamino

**Files:**
- Test: `AgroLife/Assets/Tests/Sim/PergaminoTests.cs`

**Interfaces:**
- Consumes: `DatosJuego.Desde`, `Simulation.Nueva`, `DatosPrueba.AvanzarHasta` (plan 1) y las salidas de la Tarea 8.

- [ ] **Step 1: Escribir el test**

```csharp
public class PergaminoTests
{
    static string Leer(string ruta) => File.ReadAllText(ruta);

    [Test]
    public void DatosRealesDePergaminoCorrenUnAnio()
    {
        const string R = "Assets/Data/Regions/pergamino/";
        var d = DatosJuego.Desde(new ArchivosDatos
        {
            Crops = Leer("Assets/Data/crops.json"), Pests = Leer("Assets/Data/pests.json"),
            Economy = Leer("Assets/Data/economy.json"), Soils = Leer("Assets/Data/soils.json"),
            Region = Leer(R + "region.json"), Lotes = Leer(R + "lotes.json"), Clima = Leer(R + "clima.json"),
        });
        Assert.That(d.Lotes.Count, Is.InRange(3000, 5000));
        var s = Simulation.Nueva(d, 1);
        DatosPrueba.AvanzarHasta(s, Fecha.Crear(1, 6, 2));
        foreach (var c in new[] { "trigo", "maiz", "soja1", "soja2" })
        {
            double r = s.PromedioZonaQqHa(1, c);
            TestContext.WriteLine($"{c}: {r:F1} qq/ha");
            Assert.Greater(r, 0, c);
        }
    }
}
```

- [ ] **Step 2: Correr `PergaminoTests` con el procedimiento del plan 1.** Esperado: PASS.
  - Este test no tiene fase roja: los datos ya existen y lo que prueba es el contrato entre Python y C#.
  - Si `DatosJuego.Desde` rechaza algo, el mensaje dice el archivo y la regla. La corrección va en el pipeline, no en Sim.
  - Anotar en el ledger los rindes que imprime: son la línea de base del plan 4.
- [ ] **Step 3: Correr toda la suite de Unity (`AgroLife.Sim.Tests`).** Esperado: todo PASS, y la consola sin errores.
