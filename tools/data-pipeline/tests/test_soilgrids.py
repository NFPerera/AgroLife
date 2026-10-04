import numpy as np
import pytest

from pipeline import soilgrids


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
    b = np.zeros((5, 5))
    b[2, 4] = 7
    b[0, 0] = 9
    assert soilgrids.leer_pixel(b, 2, 2) == 7
    assert soilgrids.leer_pixel(np.zeros((5, 5)), 2, 2) is None
