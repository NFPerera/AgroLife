"""Constantes del pipeline de Pergamino. Lo que se calibra a mano está acá, con su fuente."""
import os
from pathlib import Path

RAIZ_PIPELINE = Path(__file__).resolve().parents[1]
RAIZ_REPO = RAIZ_PIPELINE.parents[1]
CACHE = RAIZ_PIPELINE / "cache"
SALIDA = RAIZ_REPO / "AgroLife" / "Assets" / "Data" / "Regions" / "pergamino"

REGION_ID = "pergamino"
REGION_NOMBRE = "Pergamino"
OSM_PARTIDO = "2459663"  # relación OSM del partido

PUNTO_CLIMA = (-33.89, -60.57)  # lat, lon (spec §6.1)
PERIODO = ("19910101", "20251231")
UMBRAL_LLUVIA_MM = 1.0

EPSG_UTM = 32720  # UTM 20S

LOTE_HA_OBJETIVO, LOTE_HA_MIN, LOTE_HA_MAX = 80, 30, 150
LOTE_LARGO_MAX_M = 2000  # más largo que esto, el bloque se parte en tramos (cuadras) antes de las franjas
LOTES_MIN, LOTES_MAX = 3000, 5000
FRANJA_ARROYO_M = 100
FRANJA_URBANA_M = 50

# Referencias de zona núcleo; se ajustan en la calibración.
PRECIO_BASE_TIERRA_USD_HA = 14000
ARRENDAMIENTO_BASE_QQ_HA = 17
# ppm Bray; relevamientos regionales del norte de Buenos Aires.
FOSFORO_BRAY = {"media": 12, "desvio": 5, "minimo": 4}

VIAS_BLOQUE = ["motorway", "trunk", "primary", "secondary", "tertiary", "unclassified", "residential", "track",
               "motorway_link", "trunk_link", "primary_link", "secondary_link", "tertiary_link"]
VIAS_RUTA = ["motorway", "trunk", "primary", "secondary"]

GEOFABRIK_ARGENTINA = "https://download.geofabrik.de/south-america/argentina-latest.osm.pbf"
ZENODO_INTA = "https://zenodo.org/records/7837681/files/"
BSDTAR = os.environ.get("BSDTAR", r"C:\Windows\System32\tar.exe")
