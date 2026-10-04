import numpy as np
import pandas as pd
import pytest
import scipy.stats

from pipeline import clima


def serie(fechas, valores):
    return pd.Series(valores, index=pd.DatetimeIndex(fechas))


def test_markov_cuenta_transiciones_en_el_mes_del_dia():
    lluvia = serie(pd.date_range("2000-01-01", periods=10), [0, 2, 3, 0, 0, 1.5, 0, 4, 0, 0])
    m = clima.markov(lluvia >= 1.0)
    assert m.loc[1, "pSecoAHumedo"] == pytest.approx(0.6)
    assert m.loc[1, "pHumedoAHumedo"] == pytest.approx(0.25)


def test_gamma_recupera_los_parametros():
    x = scipy.stats.gamma.rvs(0.8, scale=15, size=20000, random_state=1)
    lluvia = serie(pd.date_range("2000-01-01", periods=20000), x)
    g = clima.gamma_mensual(lluvia, lluvia > 0)
    assert g.loc[1, "forma"] == pytest.approx(0.8, abs=0.04)
    assert g.loc[1, "escala"] == pytest.approx(15, rel=0.06)


def test_medias_por_estado():
    idx = pd.date_range("2000-01-01", periods=4)
    df = pd.DataFrame({"tmax": [20, 30, 22, 34], "tmin": [10, 10, 12, 12], "rad": [5, 20, 7, 22]}, index=idx)
    r = clima.medias_por_estado(df, pd.Series([True, False, True, False], index=idx))
    assert r[1]["tmaxHumedo"]["media"] == 21
    assert r[1]["tmaxSeco"]["media"] == 32
    assert r[1]["tmaxSeco"]["desvio"] == pytest.approx(2.828, abs=0.001)


def test_autocorrelacion_de_un_ar1():
    rng = np.random.default_rng(2)
    z = np.zeros(20000)
    for i in range(1, len(z)):
        z[i] = 0.6 * z[i - 1] + 0.8 * rng.standard_normal()
    idx = pd.date_range("2000-01-01", periods=len(z))
    df = pd.DataFrame({"tmax": 25 + 3 * z, "tmin": 12 + 2 * z, "rad": 18 + 4 * z}, index=idx)
    assert clima.autocorrelacion(df, pd.Series(False, index=idx))["tmax"] == pytest.approx(0.6, abs=0.03)


def test_fase_por_campania():
    filas = [(s, a, v) for a, v in [(2015, 2.0), (2010, -1.2), (2012, 0.3)] for s in ["JAS", "ASO", "SON", "OND", "NDJ"]]
    oni = pd.DataFrame(filas, columns=["seas", "anio", "anom"])
    assert clima.fase_por_campania(oni, range(2010, 2016)) == {
        2010: "Nina", 2011: "Neutro", 2012: "Neutro", 2013: "Neutro", 2014: "Neutro", 2015: "Nino"}


def test_multiplicadores_comparan_contra_todas_las_campanias():
    idx = pd.date_range("2000-05-01", "2002-04-30")
    agosto_marzo = np.array([d.month in clima.MESES_ENSO for d in idx])
    camp = np.array([clima.campania(d) for d in idx])
    alterna = np.cumsum(agosto_marzo & (camp == 2001)) % 2 == 1  # 2001: día por medio, arrancando húmedo
    lluvia = pd.Series(np.where(agosto_marzo & ((camp == 2000) | alterna), 10.0, 0.0), index=idx)
    m = clima.multiplicadores(lluvia, lluvia >= 1, {2000: "Nino", 2001: "Nina"})
    assert m["Nino"][7]["frecuencia"] == pytest.approx(486 / 365, abs=0.001)  # agosto
    assert m["Nina"][0]["frecuencia"] == pytest.approx(244 / 365, abs=0.001)  # enero
    assert m["Nino"][7]["cantidad"] == 1
    assert m["Nino"][4] == {"frecuencia": 1, "cantidad": 1}  # mayo
    assert all(x == {"frecuencia": 1, "cantidad": 1} for x in m["Neutro"])
