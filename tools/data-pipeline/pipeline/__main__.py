"""Pipeline de datos de Pergamino: `uv run python -m pipeline`. Escribe las salidas solo si la validación pasa."""
import json
import math
import shutil
import sys
from collections import Counter
from pathlib import Path

import geopandas as gpd
from shapely.ops import unary_union

from . import clima, config, descargas, distancias, inta, lotes, osm, soilgrids, validar

INDENTADOS = {"region.json", "clima.json"}
FUENTES = [
    {"nombre": "NASA POWER, clima diario 1991–2025", "licencia": "Pública"},
    {"nombre": "NOAA CPC, índice ONI", "licencia": "Pública"},
    {"nombre": "SoilGrids 250 m v2.0 (ISRIC)", "licencia": "CC BY 4.0"},
    {"nombre": "INTA, Cartas de Suelos de la Provincia de Buenos Aires 1:50.000", "licencia": "CC BY 4.0"},
    {"nombre": "© colaboradores de OpenStreetMap (extracto de Geofabrik)", "licencia": "ODbL"},
]


def escribir(carpeta: Path, archivos: dict[str, object]) -> None:
    """Escribe todo en <carpeta>.tmp y después reemplaza archivo por archivo."""
    tmp = carpeta.with_name(carpeta.name + ".tmp")
    if tmp.exists():
        shutil.rmtree(tmp)
    tmp.mkdir(parents=True)
    for nombre, datos in archivos.items():
        if nombre in INDENTADOS:
            texto = json.dumps(datos, ensure_ascii=False, indent=2) + "\n"
        else:
            texto = json.dumps(datos, ensure_ascii=False, separators=(",", ":"))
        (tmp / nombre).write_text(texto, encoding="utf-8")
    carpeta.mkdir(parents=True, exist_ok=True)
    for f in sorted(tmp.iterdir()):
        f.replace(carpeta / f.name)
    tmp.rmdir()


def _lineas_locales(gdf: gpd.GeoDataFrame, x0: int, y0: int) -> list[list[list[int]]]:
    r = []
    for g in gdf.geometry.simplify(10):
        for parte in getattr(g, "geoms", [g]):
            if parte.is_empty or parte.geom_type != "LineString":
                continue
            c = [[round(x - x0), round(y - y0)] for x, y in parte.coords]
            if len(c) >= 2:
                r.append(c)
    return r


def main() -> int:
    utm = config.EPSG_UTM
    limite = osm.limite_partido()
    lim = limite.to_crs(utm).geometry.iloc[0]
    if lim.geom_type == "MultiPolygon":
        lim = max(lim.geoms, key=lambda p: p.area)
    c = lim.centroid
    x0, y0 = round(c.x), round(c.y)
    c_ll = gpd.GeoSeries([c], crs=utm).to_crs(4326).iloc[0]
    bbox = tuple(limite.total_bounds + [-0.05, -0.05, 0.05, 0.05])
    print("OSM…")
    lineas, puntos, poligonos = (osm.capa(n, bbox) for n in ("lines", "points", "multipolygons"))

    print("Clima…")
    cl = clima.armar_clima(descargas.nasa_power(), descargas.oni(), config.UMBRAL_LLUVIA_MM)

    print("Suelos INTA…")
    uc = inta.unidades(descargas.inta_shapefile(), limite)
    uc_ip, uc_sin = uc[uc["ip"].notna()], uc[uc["ip"].isna()]

    print("Lotes…")
    vias = osm.vias(lineas).to_crs(utm)
    zonas = unary_union(list(osm.urbano(poligonos).to_crs(utm).buffer(config.FRANJA_URBANA_M))
                        + list(uc_sin.buffer(config.FRANJA_URBANA_M))
                        + list(osm.arroyos(lineas).to_crs(utm).buffer(config.FRANJA_ARROYO_M)))
    bloques = lotes.bloques(list(vias.clip(lim.buffer(10)).geometry), lim)
    partes = lotes.excluir(bloques, zonas)
    candidatos = lotes.ordenar([p for parte in partes for p in lotes.dividir(
        parte, config.LOTE_HA_OBJETIVO, config.LOTE_HA_MIN, config.LOTE_HA_MAX, config.LOTE_LARGO_MAX_M)])

    print("Suelo de cada lote…")
    pts = gpd.GeoDataFrame({"i": range(len(candidatos))}, geometry=[p.representative_point() for p in candidatos], crs=utm)
    unidad = gpd.sjoin(pts, uc_ip, how="left", predicate="within").drop_duplicates("i").set_index("i")
    faltan = unidad.index[unidad["ip"].isna()]
    if len(faltan):
        cerca = gpd.sjoin_nearest(pts.set_index("i").loc[faltan], uc_ip, how="left", max_distance=500)
        cerca = cerca[~cerca.index.duplicated()]
        unidad.loc[faltan, ["ip", "SIMBC"]] = cerca[["ip", "SIMBC"]]
    pts_ll = pts.to_crs(4326)
    rasters = soilgrids.Rasters(tuple(limite.total_bounds + [-0.02, -0.02, 0.02, 0.02]))

    descartes = Counter()
    elegidos = []  # (polígono, punto, suelo, ip, simbc)
    for i, (poly, pt, pt_ll) in enumerate(zip(candidatos, pts.geometry, pts_ll.geometry)):
        suelo = rasters.suelo(pt_ll.x, pt_ll.y)
        ip = unidad.at[i, "ip"]
        if suelo is None:
            descartes["sin SoilGrids"] += 1
        elif ip is None or ip != ip:
            descartes["sin unidad INTA con IP"] += 1
        else:
            elegidos.append((poly, pt, suelo, float(ip), unidad.at[i, "SIMBC"]))

    print("Distancias…")
    locs = osm.localidades(puntos).to_crs(utm)
    locs = locs[locs.within(lim)].sort_values("name")
    silos = osm.silos(puntos, poligonos).to_crs(utm)
    acopios = distancias.acopios(silos[silos.within(lim)], locs)
    G = distancias.grafo_vial(list(vias.clip(lim.buffer(2000)).geometry))
    nodos_lote = distancias.nodos_cercanos(G, [(pt.x, pt.y) for _, pt, *_ in elegidos])
    nodos_acopio = distancias.nodos_cercanos(G, [(a["x"], a["y"]) for a in acopios])
    km = distancias.distancias_km(G, nodos_acopio, nodos_lote)

    lista = []
    for n, ((poly, pt, s, ip, simbc), nodo, d) in enumerate(zip(elegidos, nodos_lote, km), 1):
        lista.append({
            "id": n, "superficieHa": round(poly.area / 1e4, 1),
            "arena": round(s["arena"], 1), "limo": round(s["limo"], 1), "arcilla": round(s["arcilla"], 1),
            "corgPct": round(s["corgPct"], 2), "ccMm": round(s["ccMm"]), "pmpMm": round(s["pmpMm"]),
            "ip": round(ip, 1), "unidadSuelo": simbc,
            "distanciaAcopioKm": round(d + math.dist((pt.x, pt.y), nodo) / 1000, 1),
            "poligono": lotes.a_local(poly, x0, y0),
        })
    # Redondear por separado puede dejar la textura en 99,9 o 100,1: el resto va a la arcilla.
    for lote in lista:
        lote["arcilla"] = round(100 - lote["arena"] - lote["limo"], 1)

    region = {
        "id": config.REGION_ID, "nombre": config.REGION_NOMBRE,
        "latitud": round(c_ll.y, 4), "longitud": round(c_ll.x, 4),
        "precioBaseTierraUsdHa": config.PRECIO_BASE_TIERRA_USD_HA,
        "arrendamientoBaseQqHa": config.ARRENDAMIENTO_BASE_QQ_HA,
        "fosforoBray": config.FOSFORO_BRAY,
        "origenUtm": {"epsg": utm, "x": x0, "y": y0},
        "localidades": [{"nombre": r["name"], "tipo": r["place"], "x": round(r.geometry.x - x0), "y": round(r.geometry.y - y0)}
                        for _, r in locs.iterrows()],
        "acopios": [{"nombre": a["nombre"], "x": round(a["x"] - x0), "y": round(a["y"] - y0)} for a in acopios],
        "fuentes": FUENTES,
    }
    vias_lim = vias.clip(lim)
    geografia = {
        "contorno": [[round(x - x0), round(y - y0)] for x, y in lim.simplify(10).exterior.coords[:-1]],
        "rutas": _lineas_locales(vias_lim[vias_lim["highway"].isin(config.VIAS_RUTA)], x0, y0),
        "caminos": _lineas_locales(vias_lim[~vias_lim["highway"].isin(config.VIAS_RUTA)], x0, y0),
        "arroyos": _lineas_locales(osm.arroyos(lineas).to_crs(utm).clip(lim), x0, y0),
    }

    errores = validar.validar(region, lista, cl, geografia)
    ha = sum(l["superficieHa"] for l in lista)
    print(f"\nLotes: {len(lista)} ({ha:,.0f} ha). Descartados: {dict(descartes)}. Candidatos: {len(candidatos)}")
    if lista:
        print(f"IP medio ponderado: {sum(l['ip'] * l['superficieHa'] for l in lista) / ha:.1f}. "
              f"Agua útil media: {sum(l['ccMm'] - l['pmpMm'] for l in lista) / len(lista):.0f} mm. "
              f"Distancia media al acopio: {sum(l['distanciaAcopioKm'] for l in lista) / len(lista):.1f} km")
    lluvia = 0
    for m, mes in enumerate(cl["meses"], 1):
        pi = mes["pSecoAHumedo"] / (1 - mes["pHumedoAHumedo"] + mes["pSecoAHumedo"])
        lluvia += pi * [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][m - 1] * mes["gammaForma"] * mes["gammaEscala"]
    print(f"Lluvia anual esperada del generador: {lluvia:.0f} mm. Fases: {cl['enso']['frecuencias']}. "
          f"Acopios: {len(acopios)}. Localidades: {len(locs)}")
    if errores:
        print(f"\n{len(errores)} errores de validación (no se escribió nada):")
        for e in errores[:30]:
            print("  " + e)
        return 1
    escribir(config.SALIDA, {"region.json": region, "lotes.json": {"lotes": lista}, "geografia.json": geografia, "clima.json": cl})
    print(f"Salidas escritas en {config.SALIDA}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
