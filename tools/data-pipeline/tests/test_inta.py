import pytest

from pipeline import inta

IPS = {inta.clave(k): v for k, v in {"Pergamino": 85.5, "Ramallo": 65, "Wheelwright": 31.5, "El Recuerdo": 22.5}.items()}


def uc(*pares):
    fila = {f"SERIE{i}": "" for i in range(1, 7)} | {f"PORC{i}": 0 for i in range(1, 7)}
    for i, (s, p) in enumerate(pares, 1):
        fila[f"SERIE{i}"], fila[f"PORC{i}"] = s, p
    return fila


def test_clave_ignora_acentos_y_guiones():
    assert inta.clave("Santa_Lucía") == inta.clave("Santa Lucia") == "santalucia"


def test_ip_de_la_ficha():
    assert inta.ip_de_ficha("Índice de productividad según la región climática:\n85,50 (A)") == 85.5
    assert inta.ip_de_ficha("Indice de productividad: 90") == 90
    assert inta.ip_de_ficha("sin dato") is None


def test_ip_de_la_unidad_pondera_por_porcentaje():
    assert inta.ip_unidad(uc(("Pergamino", 60), ("Ramallo", 40)), IPS) == pytest.approx(77.3)
    assert inta.ip_unidad(uc(("Pergamino", 70), ("Desconocida", 30)), IPS) == pytest.approx(85.5)


def test_serie_combinada_se_reparte():
    assert inta.ip_unidad(uc(("Wheelwright y el Recuerdo", 100)), IPS) == pytest.approx(27.0)


def test_miscelaneas_no_tiene_ip():
    assert inta.ip_unidad(uc(("Miscelaneas ciudades y o poblados", 100)), IPS) is None


def test_clase_de_capacidad():
    assert inta.clase_capacidad("VIIws") == "VII"
    assert inta.clase_capacidad("I-1/IIw") == "I"
    assert inta.clase_capacidad("IIIe") == "III"
    assert inta.clase_capacidad(None) is None


def test_capacidad_de_la_ficha():
    assert inta.capacidad_de_ficha("Capacidad de uso: I-1/2\nLimitaciones") == "I"
    assert inta.capacidad_de_ficha("Capacidad de uso:  VI\n") == "VI"


def test_unidad_sin_series_usa_su_clase_de_capacidad():
    ref = {"VI": 20.1, "VII": 5.7}
    assert inta.ip_por_capacidad("VIws", ref) == pytest.approx(20.1)
    assert inta.ip_por_capacidad("VIII", ref) is None
