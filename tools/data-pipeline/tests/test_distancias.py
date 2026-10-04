import geopandas as gpd
import networkx as nx
import pytest
from shapely.geometry import LineString, Point

from pipeline import distancias


def test_grafo_une_vias_en_vertices_compartidos():
    G = distancias.grafo_vial([LineString([(0, 0), (1000, 0)]), LineString([(1000, 0), (1000, 500)]), LineString([(0, 0), (0, 300)])])
    assert G.number_of_nodes() == 4
    assert nx.shortest_path_length(G, (0.0, 0.0), (1000.0, 500.0), weight="length") == pytest.approx(1500)


def test_nodo_mas_cercano():
    G = distancias.grafo_vial([LineString([(0, 0), (1000, 0)])])
    assert distancias.nodos_cercanos(G, [(990, 10), (-5, 3)]) == [(1000.0, 0.0), (0.0, 0.0)]


def test_distancia_al_acopio_mas_cercano():
    G = nx.Graph()
    G.add_edge("a", "b", length=1000)
    G.add_edge("b", "c", length=2000)
    G.add_edge("c", "d", length=500)
    assert distancias.distancias_km(G, ["a", "d"], ["b", "c"]) == [pytest.approx(1.0), pytest.approx(0.5)]


def puntos(xy, **cols):
    return gpd.GeoDataFrame(cols, geometry=[Point(p) for p in xy], crs=32720)


def test_sin_silos_hay_un_acopio_por_localidad():
    a = distancias.acopios(puntos([]), puntos([(0, 0), (9000, 0)], name=["Pergamino", "Manuel Ocampo"]))
    assert [x["nombre"] for x in a] == ["Acopio Pergamino", "Acopio Manuel Ocampo"]


def test_silos_cercanos_forman_un_acopio():
    s = puntos([(0, 0), (300, 0), (5000, 0), (5200, 100), (20000, 0)], name=["Coop A", "Coop A", "ACA", "ACA", "Silos X"])
    a = distancias.acopios(s, puntos([(0, 0)], name=["Pergamino"]))
    assert [x["nombre"] for x in a] == ["Coop A", "ACA", "Silos X"] and a[0]["x"] == pytest.approx(150)
