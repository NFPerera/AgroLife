"""Suelo de cada lote desde SoilGrids 250 m: textura y carbono de 0–30 cm, y agua retenida hasta 150 cm."""
import numpy as np
import rasterio

from . import descargas

PROF_TEXTURA = ["0-5cm", "5-15cm", "15-30cm"]
ESPESOR_TEXTURA = [5, 10, 15]
PROF_AGUA = ["0-5cm", "5-15cm", "15-30cm", "30-60cm", "60-100cm", "100-200cm"]
ESPESOR_AGUA_MM = [50, 100, 150, 300, 400, 500]  # el último tramo es 100–150 cm de la capa 100–200


def _ponderado(valores, pesos):
    return float(np.dot(valores, pesos) / sum(pesos))


def textura_0_30(arena: list[float], limo: list[float], arcilla: list[float]) -> tuple[float, float, float]:
    """g/kg por profundidad → % ponderado por espesor, normalizado a 100 (SoilGrids predice cada fracción por separado)."""
    a, l, c = (_ponderado(x, ESPESOR_TEXTURA) for x in (arena, limo, arcilla))
    total = a + l + c
    return a * 100 / total, l * 100 / total, c * 100 / total


def corg_0_30(soc: list[float]) -> float:
    """dg/kg → % de carbono orgánico, ponderado por espesor."""
    return _ponderado(soc, ESPESOR_TEXTURA) / 100


def agua_150(wv: list[float]) -> float:
    """Contenido volumétrico (10^-3 cm³/cm³) por profundidad → mm de agua hasta 150 cm."""
    return float(np.dot(np.asarray(wv) / 1000, ESPESOR_AGUA_MM))


def leer_pixel(banda: np.ndarray, fila: int, col: int, radio: int = 5) -> float | None:
    """Valor de la celda, o el de la celda válida (> 0) más cercana dentro del radio; 0 es enmascarado."""
    f0, f1 = max(0, fila - radio), min(banda.shape[0], fila + radio + 1)
    c0, c1 = max(0, col - radio), min(banda.shape[1], col + radio + 1)
    ventana = banda[f0:f1, c0:c1]
    filas, cols = np.nonzero(ventana > 0)
    if len(filas) == 0:
        return None
    d2 = (filas + f0 - fila) ** 2 + (cols + c0 - col) ** 2
    i = int(np.argmin(d2))
    if d2[i] > radio * radio:
        return None
    return float(ventana[filas[i], cols[i]])


class Rasters:
    """Las 24 capas de SoilGrids del partido, en memoria, muestreadas por punto."""

    def __init__(self, bbox: tuple[float, float, float, float]):
        self.capas = {}
        pedidos = [(p, d) for p in ("sand", "silt", "clay", "soc") for d in PROF_TEXTURA]
        pedidos += [(p, d) for p in ("wv0033", "wv1500") for d in PROF_AGUA]
        for prop, prof in pedidos:
            with rasterio.open(descargas.soilgrids(prop, prof, bbox)) as ds:
                self.capas[(prop, prof)] = (ds.read(1), ds.transform)

    def _valores(self, prop, profundidades, lon, lat):
        r = []
        for prof in profundidades:
            banda, transform = self.capas[(prop, prof)]
            fila, col = rasterio.transform.rowcol(transform, lon, lat)
            v = leer_pixel(banda, fila, col)
            if v is None:
                return None
            r.append(v)
        return r

    def suelo(self, lon: float, lat: float) -> dict | None:
        datos = {p: self._valores(p, PROF_TEXTURA, lon, lat) for p in ("sand", "silt", "clay", "soc")}
        datos |= {p: self._valores(p, PROF_AGUA, lon, lat) for p in ("wv0033", "wv1500")}
        if any(v is None for v in datos.values()):
            return None
        arena, limo, arcilla = textura_0_30(datos["sand"], datos["silt"], datos["clay"])
        return {"arena": arena, "limo": limo, "arcilla": arcilla, "corgPct": corg_0_30(datos["soc"]),
                "ccMm": agua_150(datos["wv0033"]), "pmpMm": agua_150(datos["wv1500"])}
