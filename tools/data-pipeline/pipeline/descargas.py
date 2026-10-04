"""Descargas con caché: cada archivo se baja una sola vez a cache/."""
import io
import json
import shutil
import subprocess
from pathlib import Path
from urllib.parse import quote

import pandas as pd
import requests

from . import config


def reemplazar_atomico(destino: Path, escribir) -> Path:
    """escribir(ruta_temporal) y recién al terminar se renombra a destino: un corte nunca deja un destino a medias."""
    parcial = destino.with_name(destino.name + ".part")
    if parcial.is_dir():
        shutil.rmtree(parcial)
    elif parcial.exists():
        parcial.unlink()
    destino.parent.mkdir(parents=True, exist_ok=True)
    try:
        escribir(parcial)
    except BaseException:
        shutil.rmtree(parcial, ignore_errors=True) if parcial.is_dir() else parcial.unlink(missing_ok=True)
        raise
    parcial.replace(destino)
    return destino


def _bbox_clave(bbox) -> str:
    return "_".join(f"{v:.4f}" for v in bbox)


def obtener(url: str, destino: Path, timeout: int = 600) -> Path:
    """Baja url a destino si todavía no está. Escribe en .part y renombra al terminar."""
    if destino.exists():
        return destino
    destino.parent.mkdir(parents=True, exist_ok=True)
    parcial = destino.with_suffix(destino.suffix + ".part")
    ultimo_error = None
    for _ in range(3):
        try:
            with requests.get(url, stream=True, timeout=timeout) as r:
                r.raise_for_status()
                with open(parcial, "wb") as f:
                    for bloque in r.iter_content(1 << 20):
                        f.write(bloque)
            parcial.replace(destino)
            return destino
        except requests.RequestException as e:
            ultimo_error = e
    raise RuntimeError(f"No se pudo bajar {url}: {ultimo_error}")


def extraer(archivo: Path, carpeta: Path) -> Path:
    """Extrae un .rar con bsdtar (libarchive) dentro de carpeta, si todavía no existe. Extrae en un temporal y renombra."""
    if carpeta.exists():
        return carpeta

    def extraer_en(tmp: Path):
        tmp.mkdir()
        subprocess.run([config.BSDTAR, "-xf", str(archivo), "-C", str(tmp)], check=True)
        if not (tmp / carpeta.name).is_dir():
            raise RuntimeError(f"{archivo.name} no trajo la carpeta {carpeta.name}")

    tmp = reemplazar_atomico(carpeta.with_name(carpeta.name + ".extraido"), extraer_en)
    (tmp / carpeta.name).replace(carpeta)
    tmp.rmdir()
    return carpeta


def nasa_power() -> pd.DataFrame:
    lat, lon = config.PUNTO_CLIMA
    inicio, fin = config.PERIODO
    url = ("https://power.larc.nasa.gov/api/temporal/daily/point?parameters=PRECTOTCORR,T2M_MAX,T2M_MIN,ALLSKY_SFC_SW_DWN"
           f"&community=AG&longitude={lon}&latitude={lat}&start={inicio}&end={fin}&format=JSON")
    archivo = obtener(url, config.CACHE / "nasa" / f"power_{lat}_{lon}_{inicio}_{fin}.json")
    p = json.loads(archivo.read_text(encoding="utf-8"))["properties"]["parameter"]
    df = pd.DataFrame(p)
    df.index = pd.to_datetime(df.index, format="%Y%m%d")
    df = df.rename(columns={"PRECTOTCORR": "lluvia", "T2M_MAX": "tmax", "T2M_MIN": "tmin", "ALLSKY_SFC_SW_DWN": "rad"})
    if (df == -999).any().any():
        raise RuntimeError("NASA POWER tiene días faltantes (-999)")
    return df[["lluvia", "tmax", "tmin", "rad"]]


def oni() -> pd.DataFrame:
    archivo = obtener("https://www.cpc.ncep.noaa.gov/data/indices/oni.ascii.txt", config.CACHE / "oni.ascii.txt")
    df = pd.read_csv(io.StringIO(archivo.read_text()), sep=r"\s+")
    df = df.rename(columns={"SEAS": "seas", "YR": "anio", "ANOM": "anom"})
    return df[["seas", "anio", "anom"]].astype({"seas": str, "anio": int, "anom": float}).reset_index(drop=True)


def soilgrids(prop: str, prof: str, bbox: tuple[float, float, float, float]) -> Path:
    """GeoTIFF EPSG:4326 de una capa de SoilGrids (WCS de maps.isric.org, 250 m)."""
    destino = config.CACHE / "soilgrids" / f"{prop}_{prof}_{_bbox_clave(bbox)}.tif"
    if destino.exists():
        return destino
    xmin, ymin, xmax, ymax = bbox
    crs = "http://www.opengis.net/def/crs/EPSG/0/4326"
    url = (f"https://maps.isric.org/mapserv?map=/map/{prop}.map&SERVICE=WCS&VERSION=2.0.1&REQUEST=GetCoverage"
           f"&COVERAGEID={prop}_{prof}_mean&FORMAT=image/tiff&SUBSET=long({xmin},{xmax})&SUBSET=lat({ymin},{ymax})"
           f"&SUBSETTINGCRS={crs}&OUTPUTCRS={crs}")
    r = requests.get(url, timeout=300)
    r.raise_for_status()
    if not r.headers.get("content-type", "").startswith("image/tiff"):
        raise RuntimeError(f"SoilGrids {prop} {prof} no devolvió un GeoTIFF: {r.text[:300]}")
    return reemplazar_atomico(destino, lambda p: p.write_bytes(r.content))


def _inta(nombre_rar: str, archivo_local: str, carpeta: str) -> Path:
    rar = obtener(config.ZENODO_INTA + quote(nombre_rar) + "?download=1", config.CACHE / "inta" / archivo_local)
    return extraer(rar, config.CACHE / "inta" / carpeta)


def inta_shapefile() -> Path:
    carpeta = _inta("1_Mapa_de_Suelos_BA_50000_V2.rar", "suelos_ba_50000.rar", "1_Mapa_de_Suelos_BA_50000_V2")
    return carpeta / "Suelos_BA_50mil_V2.shp"


def inta_fichas() -> Path:
    return _inta("3_Series de suelos y Perfiles representativos.rar", "series.rar", "Series de suelos y Perfiles representativos")
