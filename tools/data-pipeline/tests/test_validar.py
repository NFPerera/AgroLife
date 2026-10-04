import copy
import json
from pathlib import Path

from pipeline import validar

CLIMA = json.loads((Path(__file__).resolve().parents[3] / "AgroLife/Assets/Tests/Sim/Datos/clima.json").read_text(encoding="utf-8"))
REGION = {"id": "pergamino", "nombre": "Pergamino", "acopios": [{"nombre": "Acopio Pergamino", "x": 0, "y": 0}],
          "localidades": [{"nombre": "Pergamino", "tipo": "city", "x": 0, "y": 0}]}
LOTE = {"id": 1, "superficieHa": 80.0, "arena": 20.0, "limo": 65.0, "arcilla": 15.0, "corgPct": 1.8, "ccMm": 560, "pmpMm": 260,
        "ip": 85.0, "unidadSuelo": "Pe1", "distanciaAcopioKm": 15.0, "poligono": [[0, 0], [1000, 0], [1000, 800], [0, 800]]}
GEO = {"contorno": [[0, 0], [5000, 0], [5000, 5000]], "rutas": [], "caminos": [], "arroyos": []}


def test_caso_valido_no_tiene_errores():
    assert validar.validar(REGION, [LOTE], CLIMA, GEO, lotes_min=1, lotes_max=10) == []


def test_errores_de_lote():
    malo = LOTE | {"superficieHa": 20.0, "arena": 10}
    e = validar.validar(REGION, [malo], CLIMA, GEO, lotes_min=1, lotes_max=10)
    assert "lote 1: superficie 20.0 ha fuera de [30, 150]" in e
    assert "lote 1: textura suma 90.00" in e


def test_cantidad_de_lotes():
    assert "cantidad de lotes 1 fuera de [3000, 5000]" in validar.validar(REGION, [LOTE], CLIMA, GEO)


def test_multiplicador_fuera_de_temporada():
    clima = copy.deepcopy(CLIMA)
    clima["enso"]["multiplicadores"]["Nino"][5]["frecuencia"] = 1.2
    assert "clima: Nino mes 6 debería valer 1" in validar.validar(REGION, [LOTE], clima, GEO, lotes_min=1, lotes_max=10)
