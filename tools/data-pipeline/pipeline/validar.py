"""Controles de las salidas antes de escribirlas (spec §7, paso 6). Devuelve la lista de errores; vacía = ok."""
from .clima import MESES_ENSO
from .config import LOTE_HA_MAX, LOTE_HA_MIN, LOTES_MAX, LOTES_MIN


def _lote(lote: dict) -> list[str]:
    i, e = lote["id"], []
    ha = lote["superficieHa"]
    if not LOTE_HA_MIN <= ha <= LOTE_HA_MAX:
        e.append(f"lote {i}: superficie {ha:.1f} ha fuera de [{LOTE_HA_MIN}, {LOTE_HA_MAX}]")
    s = lote["arena"] + lote["limo"] + lote["arcilla"]
    if abs(s - 100) > 0.5:
        e.append(f"lote {i}: textura suma {s:.2f}")
    if not 0 < lote["ip"] <= 100:
        e.append(f"lote {i}: ip {lote['ip']} fuera de rango")
    if not lote["ccMm"] > lote["pmpMm"] >= 0:
        e.append(f"lote {i}: agua cc {lote['ccMm']} <= pmp {lote['pmpMm']}")
    if lote["corgPct"] < 0:
        e.append(f"lote {i}: corg negativo")
    if not 0 <= lote["distanciaAcopioKm"] < 100:
        e.append(f"lote {i}: distancia {lote['distanciaAcopioKm']} fuera de rango")
    if len(lote["poligono"]) < 3:
        e.append(f"lote {i}: polígono con menos de 3 vértices")
    return e


def _clima(clima: dict) -> list[str]:
    e = []
    meses = clima["meses"]
    if len(meses) != 12:
        e.append(f"clima: {len(meses)} meses")
    for m, mes in enumerate(meses, 1):
        if not (0 <= mes["pSecoAHumedo"] <= 1 and 0 <= mes["pHumedoAHumedo"] <= 1):
            e.append(f"clima: mes {m}: probabilidad fuera de [0, 1]")
        if not (mes["gammaForma"] > 0 and mes["gammaEscala"] > 0):
            e.append(f"clima: mes {m}: gamma inválida")
    s = sum(clima["enso"]["frecuencias"].values())
    if abs(s - 1) > 0.01:
        e.append(f"clima: frecuencias suman {s:.2f}")
    for fase, lista in clima["enso"]["multiplicadores"].items():
        for m, par in enumerate(lista, 1):
            if m not in MESES_ENSO and (par["frecuencia"] != 1 or par["cantidad"] != 1):
                e.append(f"clima: {fase} mes {m} debería valer 1")
    return e


def validar(region: dict, lotes: list[dict], clima: dict, geografia: dict,
            lotes_min: int = LOTES_MIN, lotes_max: int = LOTES_MAX) -> list[str]:
    e = []
    if not lotes_min <= len(lotes) <= lotes_max:
        e.append(f"cantidad de lotes {len(lotes)} fuera de [{lotes_min}, {lotes_max}]")
    for pos, lote in enumerate(lotes, 1):
        if lote["id"] != pos:
            e.append(f"ids no consecutivos en la posición {pos}")
            break
    for lote in lotes:
        e += _lote(lote)
    e += _clima(clima)
    if not region.get("acopios"):
        e.append("region: sin acopios")
    if not region.get("localidades"):
        e.append("region: sin localidades")
    if len(geografia.get("contorno", [])) < 3:
        e.append("geografia: contorno vacío")
    return e
