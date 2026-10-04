"""Parámetros del generador de clima tipo WGEN y de El Niño / La Niña (spec §6.1), desde NASA POWER y el ONI."""
import numpy as np
import pandas as pd
import scipy.stats

MESES_ENSO = [8, 9, 10, 11, 12, 1, 2, 3]  # agosto a marzo
FASES = ["Nino", "Neutro", "Nina"]
VARIABLES = ["tmax", "tmin", "rad"]
TEMPORADA_ONI = ["JAS", "ASO", "SON", "OND", "NDJ"]


def campania(fecha: pd.Timestamp) -> int:
    return fecha.year if fecha.month >= 5 else fecha.year - 1


def markov(humedo: pd.Series) -> pd.DataFrame:
    """Probabilidad de día húmedo según el día anterior; la transición t−1→t cuenta en el mes de t."""
    d = pd.DataFrame({"hoy": humedo.astype(bool), "ayer": humedo.astype(bool).shift(1)}).iloc[1:]
    d["ayer"] = d["ayer"].astype(bool)
    d["mes"] = d.index.month
    p = d.groupby(["mes", "ayer"])["hoy"].mean().unstack()
    return pd.DataFrame({"pSecoAHumedo": p[False], "pHumedoAHumedo": p[True]})


def gamma_mensual(lluvia: pd.Series, humedo: pd.Series) -> pd.DataFrame:
    filas = {}
    for mes, x in lluvia[humedo].groupby(lluvia[humedo].index.month):
        forma, _, escala = scipy.stats.gamma.fit(x.to_numpy(), floc=0)
        filas[mes] = {"forma": forma, "escala": escala}
    return pd.DataFrame.from_dict(filas, orient="index")


def medias_por_estado(df: pd.DataFrame, humedo: pd.Series) -> dict:
    r = {}
    for mes in sorted(set(df.index.month)):
        en_mes = df.index.month == mes
        r[mes] = {}
        for var in VARIABLES:
            for h, sufijo in [(False, "Seco"), (True, "Humedo")]:
                x = df.loc[en_mes & (humedo.to_numpy() == h), var]
                r[mes][f"{var}{sufijo}"] = {"media": float(x.mean()), "desvio": float(x.std(ddof=1))}
    return r


def autocorrelacion(df: pd.DataFrame, humedo: pd.Series) -> dict:
    """Correlación lag-1 de los residuos estandarizados por mes y estado (húmedo/seco)."""
    grupos = [df.index.month, humedo.to_numpy()]
    r = {}
    for var in VARIABLES:
        g = df[var].groupby(grupos)
        z = ((df[var] - g.transform("mean")) / g.transform("std")).to_numpy()
        r[var] = float(np.corrcoef(z[1:], z[:-1])[0, 1])
    return r


def fase_por_campania(oni: pd.DataFrame, campanias: range) -> dict[int, str]:
    r = {}
    for c in campanias:
        x = oni.loc[(oni["anio"] == c) & oni["seas"].isin(TEMPORADA_ONI), "anom"]
        media = x.mean() if len(x) else 0.0
        r[c] = "Nino" if media >= 0.5 else "Nina" if media <= -0.5 else "Neutro"
    return r


def multiplicadores(lluvia: pd.Series, humedo: pd.Series, fases: dict[int, str]) -> dict[str, list[dict]]:
    """Un par (frecuencia, cantidad) por fase, juntando agosto–marzo, relativo a todas las campañas."""
    camp = np.array([campania(d) for d in lluvia.index])
    temporada = np.isin(lluvia.index.month, MESES_ENSO) & np.isin(camp, list(fases))

    def estadisticos(mascara):
        h = humedo.to_numpy()[mascara]
        return h.mean(), lluvia.to_numpy()[mascara][h].mean()

    frac, cant = estadisticos(temporada)
    r = {}
    for fase in FASES:
        mascara = temporada & np.isin(camp, [c for c, f in fases.items() if f == fase])
        if mascara.any():
            fr, ca = estadisticos(mascara)
            par = {"frecuencia": round(float(fr / frac), 3), "cantidad": round(float(ca / cant), 3)}
        else:
            par = {"frecuencia": 1, "cantidad": 1}
        r[fase] = [dict(par) if m in MESES_ENSO else {"frecuencia": 1, "cantidad": 1} for m in range(1, 13)]
    return r


def armar_clima(df: pd.DataFrame, oni: pd.DataFrame, umbral_mm: float) -> dict:
    humedo = df["lluvia"] >= umbral_mm
    mk = markov(humedo)
    gm = gamma_mensual(df["lluvia"], humedo)
    me = medias_por_estado(df, humedo)
    ac = autocorrelacion(df, humedo)

    inicio, fin = df.index.min(), df.index.max()
    primera = inicio.year if inicio <= pd.Timestamp(inicio.year, 8, 1) else inicio.year + 1
    ultima = fin.year - 1 if fin >= pd.Timestamp(fin.year, 3, 31) else fin.year - 2
    fases = fase_por_campania(oni, range(primera, ultima + 1))

    def md(d):
        return {"media": round(d["media"], 1), "desvio": round(d["desvio"], 1)}

    meses = []
    for m in range(1, 13):
        meses.append({
            "pSecoAHumedo": round(float(mk.loc[m, "pSecoAHumedo"]), 3),
            "pHumedoAHumedo": round(float(mk.loc[m, "pHumedoAHumedo"]), 3),
            "gammaForma": round(float(gm.loc[m, "forma"]), 2),
            "gammaEscala": round(float(gm.loc[m, "escala"]), 2),
            **{clave: md(valor) for clave, valor in me[m].items()},
        })
    n = len(fases)
    return {
        "meses": meses,
        "autocorrelacion": {k: round(v, 2) for k, v in ac.items()},
        "enso": {
            "frecuencias": {f: round(sum(1 for x in fases.values() if x == f) / n, 3) for f in FASES},
            "multiplicadores": multiplicadores(df["lluvia"], humedo, fases),
        },
    }
