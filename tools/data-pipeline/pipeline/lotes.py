"""Lotes: cuadras rurales armadas con los caminos, sin zonas urbanas ni franjas de arroyos, divididas en franjas."""
import math

from shapely.affinity import rotate
from shapely.geometry import LineString, MultiPolygon, Polygon, box
from shapely.geometry.base import BaseGeometry
from shapely.ops import polygonize, unary_union


def _poligonos(g: BaseGeometry) -> list[Polygon]:
    if g.is_empty:
        return []
    if isinstance(g, Polygon):
        return [g]
    if isinstance(g, MultiPolygon):
        return list(g.geoms)
    return [p for p in getattr(g, "geoms", []) if isinstance(p, Polygon) and not p.is_empty]


def bloques(lineas: list[LineString], limite: Polygon) -> list[Polygon]:
    caras = polygonize(unary_union(list(lineas) + [limite.boundary]))
    return [p for p in caras if limite.contains(p.representative_point())]


def excluir(bloques: list[Polygon], zonas: BaseGeometry) -> list[Polygon]:
    return [p for b in bloques for p in _poligonos(b.difference(zonas))]


def _angulo_lado_largo(p: Polygon) -> float:
    c = list(p.minimum_rotated_rectangle.exterior.coords)
    (x0, y0), (x1, y1), (x2, y2) = c[0], c[1], c[2]
    if math.hypot(x1 - x0, y1 - y0) >= math.hypot(x2 - x1, y2 - y1):
        return math.degrees(math.atan2(y1 - y0, x1 - x0))
    return math.degrees(math.atan2(y2 - y1, x2 - x1))


def dividir(bloque: Polygon, ha_obj: float = 80, ha_min: float = 30, ha_max: float = 150,
            largo_max: float = 2000) -> list[Polygon]:
    """Corta el bloque en franjas paralelas a su lado largo; descarta lo chico y vuelve a cortar lo grande.

    Un bloque más largo que largo_max (los que deja OSM cuando faltan caminos) se parte antes en tramos
    transversales, que se tratan como cuadras: así ningún lote es una tira de kilómetros.
    """
    ha = bloque.area / 1e4
    if ha < ha_min:
        return []
    theta = _angulo_lado_largo(bloque)
    origen = bloque.centroid
    r = rotate(bloque, -theta, origin=origen)
    minx, miny, maxx, maxy = r.bounds
    tramos = math.ceil((maxx - minx) / largo_max)
    if tramos > 1:
        largo = (maxx - minx) / tramos
        resultado = []
        for k in range(tramos):
            corte = box(minx + k * largo, miny - 1, minx + (k + 1) * largo, maxy + 1)
            for p in _poligonos(r.intersection(corte)):
                resultado += dividir(rotate(p, theta, origin=origen), ha_obj, ha_min, ha_max, largo_max)
        return resultado
    n = max(1, round(ha / ha_obj))
    while ha / n > ha_max:
        n += 1
    while n > 1 and ha / n < ha_min:
        n -= 1

    alto = (maxy - miny) / n
    piezas = []
    for k in range(n):
        franja = box(minx - 1, miny + k * alto, maxx + 1, miny + (k + 1) * alto)
        piezas += [rotate(p, theta, origin=origen) for p in _poligonos(r.intersection(franja))]

    resultado = []
    for p in piezas:
        ha_p = p.area / 1e4
        if ha_p < ha_min:
            continue
        resultado += dividir(p, ha_obj, ha_min, ha_max, largo_max) if ha_p > ha_max else [p]
    return resultado


def ordenar(lotes: list[Polygon]) -> list[Polygon]:
    """Norte a sur y oeste a este, por el punto representativo redondeado al metro: ids estables."""
    def clave(p):
        pt = p.representative_point()
        return (-round(pt.y), round(pt.x))
    return sorted(lotes, key=clave)


def a_local(p: Polygon, x0: int, y0: int) -> list[list[int]]:
    anillo = p.simplify(2, preserve_topology=True).exterior.coords[:-1]
    return [[round(x - x0), round(y - y0)] for x, y in anillo]
