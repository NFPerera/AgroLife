import pytest
from shapely.geometry import LineString, Polygon, box

from pipeline import lotes


def test_bloques_salen_de_los_caminos():
    b = lotes.bloques([LineString([(500, -10), (500, 1010)]), LineString([(-10, 500), (1010, 500)])], box(0, 0, 1000, 1000))
    assert len(b) == 4 and all(p.area == pytest.approx(250_000) for p in b)


def test_excluir_franja_de_arroyo():
    partes = lotes.excluir([box(0, 0, 1000, 1000)], LineString([(500, -10), (500, 1010)]).buffer(100, cap_style="flat"))
    assert sorted(round(p.area) for p in partes) == [400_000, 400_000]


def test_rectangulo_en_franjas_paralelas_al_lado_largo():
    piezas = lotes.dividir(box(0, 0, 2000, 1000))
    assert len(piezas) == 2
    assert all(p.area / 1e4 == pytest.approx(100, abs=0.1) for p in piezas)
    assert all(p.bounds[2] - p.bounds[0] == pytest.approx(2000, abs=1) for p in piezas)


def test_bloque_largo_se_corta_en_tramos_antes_de_las_franjas():
    piezas = lotes.dividir(box(0, 0, 15000, 500))  # 750 ha, como los bloques gigantes de huellas sin salida
    assert len(piezas) == 8
    assert all(p.bounds[2] - p.bounds[0] <= 2000 + 1 for p in piezas)
    assert all(30 <= p.area / 1e4 <= 150 for p in piezas)


def test_piezas_fuera_de_rango():
    assert lotes.dividir(box(0, 0, 400, 500)) == []  # 20 ha
    assert len(lotes.dividir(box(0, 0, 600, 600))) == 1  # 36 ha
    assert all(30 <= p.area / 1e4 <= 150 for p in lotes.dividir(Polygon([(0, 0), (5000, 0), (5000, 300), (0, 2500)])))


def test_orden_estable_norte_a_sur_oeste_a_este():
    a, b, c = box(0, 100, 10, 110), box(100, 100, 110, 110), box(0, 0, 10, 10)
    assert lotes.ordenar([c, b, a]) == [a, b, c]


def test_coordenadas_locales():
    assert lotes.a_local(box(1000, 2000, 1100, 2100), 1000, 2000) == [[100, 0], [100, 100], [0, 100], [0, 0]]
