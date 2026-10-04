"""IP de las unidades cartográficas del INTA: promedio de los IP de sus series (fichas en PDF), ponderado por PORC."""
import re
import unicodedata
from functools import lru_cache
from pathlib import Path
from statistics import mean

import geopandas as gpd
from pypdf import PdfReader

from . import config, descargas

SERIES_SIN_IP = {"miscelaneasciudadesyopoblados"}
_PATRON_IP = re.compile(r"ndice de productividad[^:]*:\s*([0-9]+(?:[.,][0-9]+)?)", re.IGNORECASE)
_PATRON_CAPACIDAD = re.compile(r"Capacidad de uso:\s*([IVX]+)")
_CLASES = ["VIII", "VII", "VI", "IV", "V", "III", "II", "I"]  # las más largas primero


def clave(nombre: str) -> str:
    s = unicodedata.normalize("NFKD", nombre).encode("ascii", "ignore").decode().lower()
    return re.sub(r"[^a-z]", "", s)


def ip_de_ficha(texto: str) -> float | None:
    m = _PATRON_IP.search(texto)
    return float(m.group(1).replace(",", ".")) if m else None


def capacidad_de_ficha(texto: str) -> str | None:
    m = _PATRON_CAPACIDAD.search(texto)
    return clase_capacidad(m.group(1)) if m else None


def clase_capacidad(cap_uso) -> str | None:
    """Clase (I a VIII) del código de capacidad de uso del INTA, p. ej. "VIIws" → "VII"."""
    if not isinstance(cap_uso, str):
        return None
    return next((c for c in _CLASES if cap_uso.strip().startswith(c)), None)


def ip_por_capacidad(cap_uso, ip_clase: dict[str, float]) -> float | None:
    return ip_clase.get(clase_capacidad(cap_uso))


def partes(serie: str) -> list[str]:
    p = re.split(r"\s+y\s+", serie.strip())
    return p if len(p) > 1 else [serie]


def _series_de(fila: dict):
    for i in range(1, 7):
        s, p = fila.get(f"SERIE{i}"), fila.get(f"PORC{i}") or 0
        if isinstance(s, str) and s.strip() and p > 0:
            yield s.strip(), float(p)


def ip_unidad(fila: dict, ips: dict[str, float]) -> float | None:
    suma = peso = 0.0
    for serie, porc in _series_de(fila):
        if clave(serie) in ips:
            suma += ips[clave(serie)] * porc
            peso += porc
            continue
        nombres = partes(serie)
        if len(nombres) > 1:  # serie combinada: el porcentaje se reparte en partes iguales
            for n in nombres:
                if clave(n) in ips:
                    suma += ips[clave(n)] * porc / len(nombres)
                    peso += porc / len(nombres)
    return suma / peso if peso > 0 else None


@lru_cache(maxsize=None)
def _leer_ficha(pdf: Path) -> tuple[float | None, str | None]:
    texto = " ".join((pagina.extract_text() or "") for pagina in PdfReader(pdf).pages[:2])
    return ip_de_ficha(texto), capacidad_de_ficha(texto)


def _leer_ip(pdf: Path) -> float | None:
    return _leer_ficha(pdf)[0]


def ip_por_clase(fichas: Path, series: list[str]) -> dict[str, float]:
    """IP medio de las series de referencia por clase de capacidad, para las unidades sin series (complejos)."""
    archivos = {clave(p.stem): p for p in fichas.glob("*.pdf")}
    por_clase: dict[str, list[float]] = {}
    for serie in sorted(set(series)):
        for n in ([serie] if clave(serie) in archivos else partes(serie)):
            if clave(n) in archivos:
                ip, clase = _leer_ficha(archivos[clave(n)])
                if ip is not None and clase:
                    por_clase.setdefault(clase, []).append(ip)
    return {c: round(mean(v), 1) for c, v in sorted(por_clase.items())}


def tabla_ip(fichas: Path, series: list[str]) -> tuple[dict[str, float], list[str]]:
    archivos = {clave(p.stem): p for p in fichas.glob("*.pdf")}
    tabla, faltantes = {}, []
    for serie in sorted(set(series)):
        k = clave(serie)
        if k in SERIES_SIN_IP:
            continue
        nombres = [serie] if k in archivos else partes(serie)
        for n in nombres:
            ip = _leer_ip(archivos[clave(n)]) if clave(n) in archivos else None
            if ip is None:
                faltantes.append(n)
            else:
                tabla[clave(n)] = ip
    return tabla, faltantes


def unidades(shp: Path, limite_4326: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    """Unidades cartográficas del partido con su IP (None en Misceláneas y lagunas, clase VIII), en EPSG_UTM."""
    uc = gpd.read_file(shp, bbox=tuple(limite_4326.total_bounds))
    uc = gpd.clip(uc, limite_4326.to_crs(uc.crs))
    series = [s for _, fila in uc.iterrows() for s, _ in _series_de(fila)]
    tabla, faltantes = tabla_ip(descargas.inta_fichas(), series)
    if faltantes:
        raise AssertionError(f"Series del partido sin IP: {sorted(set(faltantes))}")
    ip_clase = ip_por_clase(descargas.inta_fichas(), series)
    # Complejos indiferenciados (planicies de arroyos) no tienen series: IP de su clase de capacidad.
    uc["ip"] = [ip_unidad(fila, tabla) if any(_series_de(fila)) else ip_por_capacidad(fila["CAP_USO"], ip_clase)
                for _, fila in uc.iterrows()]
    return uc[["SIMBC", "ip", "geometry"]].to_crs(config.EPSG_UTM).reset_index(drop=True)
