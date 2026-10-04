"""OpenStreetMap desde el extracto de Geofabrik (driver OSM de GDAL vía pyogrio). Cada capa leída se guarda en GeoPackage."""
from pathlib import Path

import geopandas as gpd
import pandas as pd
import pyogrio

from . import config, descargas

ARROYOS = ["river", "stream"]  # los drain/ditch son canales de drenaje: no llevan franja
USOS_URBANOS = ["residential", "commercial", "industrial", "retail"]
LUGARES = ["city", "town", "village"]


def extracto() -> Path:
    return descargas.obtener(config.GEOFABRIK_ARGENTINA, config.CACHE / "osm" / "argentina.osm.pbf", timeout=1800)


def _cacheado(archivo: str, leer) -> gpd.GeoDataFrame:
    ruta = config.CACHE / "osm" / archivo
    if ruta.exists():
        return gpd.read_file(ruta)
    gdf = leer()
    descargas.reemplazar_atomico(ruta, lambda p: gdf.to_file(p, driver="GPKG"))
    return gdf


def limite_partido() -> gpd.GeoDataFrame:
    gdf = _cacheado(f"limite_{config.OSM_PARTIDO}.gpkg", lambda: pyogrio.read_dataframe(
        extracto(), layer="multipolygons", where=f"osm_id = '{config.OSM_PARTIDO}'"))
    if len(gdf) != 1:
        raise RuntimeError(f"La relación OSM {config.OSM_PARTIDO} devolvió {len(gdf)} polígonos")
    return gdf.to_crs(4326)


def capa(nombre: str, bbox: tuple[float, float, float, float]) -> gpd.GeoDataFrame:
    """nombre ∈ {"lines", "points", "multipolygons"}."""
    return _cacheado(f"{nombre}_{descargas._bbox_clave(bbox)}.gpkg", lambda: pyogrio.read_dataframe(extracto(), layer=nombre, bbox=bbox)).to_crs(4326)


def vias(lineas: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    return lineas[lineas["highway"].isin(config.VIAS_BLOQUE)]


def arroyos(lineas: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    return lineas[lineas["waterway"].isin(ARROYOS)]


def urbano(multipoligonos: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    return multipoligonos[multipoligonos["landuse"].isin(USOS_URBANOS)]


def localidades(puntos: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    return puntos[puntos["place"].isin(LUGARES) & puntos["name"].notna()]


def silos(puntos: gpd.GeoDataFrame, multipoligonos: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    def es_silo(gdf):
        m = gdf["man_made"].eq("silo") if "man_made" in gdf else False
        if "building" in gdf:
            m = m | gdf["building"].eq("silo")
        return m | gdf["other_tags"].fillna("").str.contains('"building"=>"silo"', regex=False)

    p = puntos[es_silo(puntos)][["name", "geometry"]]
    pol = multipoligonos[es_silo(multipoligonos)][["name", "geometry"]].copy()
    pol["geometry"] = pol.geometry.representative_point()
    todos = gpd.GeoDataFrame(pd.concat([p, pol], ignore_index=True), crs=4326)
    return todos[todos["name"].notna()].reset_index(drop=True)
