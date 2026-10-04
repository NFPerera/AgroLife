"""Distancia por caminos desde cada lote al acopio más cercano (metros en EPSG_UTM)."""
import math
from collections import Counter

import geopandas as gpd
import networkx as nx
import numpy as np
from scipy.spatial import cKDTree
from shapely.geometry import LineString


def grafo_vial(lineas: list[LineString]) -> nx.Graph:
    """Un nodo por vértice (las vías de OSM comparten vértices en los cruces). Queda la componente conexa más grande."""
    G = nx.Graph()
    for linea in lineas:
        partes = getattr(linea, "geoms", [linea])
        for parte in partes:
            c = [(round(x, 2), round(y, 2)) for x, y in parte.coords]
            for a, b in zip(c, c[1:]):
                if a != b:
                    G.add_edge(a, b, length=math.dist(a, b))
    if G.number_of_nodes() == 0:
        return G
    return G.subgraph(max(nx.connected_components(G), key=len)).copy()


def nodos_cercanos(G: nx.Graph, puntos: list[tuple[float, float]]) -> list:
    nodos = list(G.nodes)
    _, idx = cKDTree(np.array(nodos)).query(np.array(puntos))
    return [nodos[i] for i in np.atleast_1d(idx)]


def distancias_km(G: nx.Graph, fuentes: list, destinos: list) -> list[float]:
    largo = nx.multi_source_dijkstra_path_length(G, set(fuentes), weight="length")
    return [largo.get(d, math.inf) / 1000 for d in destinos]


def acopios(silos: gpd.GeoDataFrame, localidades: gpd.GeoDataFrame, radio_m: float = 1000, minimo: int = 3) -> list[dict]:
    """Silos con nombre agrupados por cercanía; con menos de `minimo` grupos, un acopio por localidad (spec §7)."""
    grupos: list[list] = []
    if "name" in silos.columns:
        for nombre, pt in zip(silos["name"], silos.geometry):
            if not isinstance(nombre, str) or not nombre.strip():
                continue
            grupo = next((g for g in grupos if any(pt.distance(q) < radio_m for _, q in g)), None)
            if grupo is None:
                grupos.append([(nombre, pt)])
            else:
                grupo.append((nombre, pt))
    if len(grupos) >= minimo:
        return [{"nombre": Counter(n for n, _ in g).most_common(1)[0][0],
                 "x": float(np.mean([q.x for _, q in g])), "y": float(np.mean([q.y for _, q in g]))} for g in grupos]
    return [{"nombre": f"Acopio {n}", "x": float(pt.x), "y": float(pt.y)} for n, pt in zip(localidades["name"], localidades.geometry)]
