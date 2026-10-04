using System;

namespace AgroLife.Sim
{
    public static class Nitrogeno
    {
        /// <summary>kg N/ha mineralizados en el día, según carbono orgánico, temperatura y humedad.</summary>
        public static double Mineralizacion(double corgPct, double tmediaC, double aguaMm, double auMaxMm, ModeloSuelo m)
        {
            double fT = tmediaC <= 0 ? 0 : Math.Pow(2, (tmediaC - 20) / 10);
            double fW = Math.Max(0, Math.Min(1, aguaMm / auMaxMm));
            return m.KMineralizacion * corgPct * fT * fW;
        }

        /// <summary>Se va la misma fracción del nitrato que la del agua del perfil que drenó.</summary>
        public static double Lavado(double nMineralKgHa, double drenajeMm, double aguaAntesDeDrenarMm, double pmpMm) =>
            drenajeMm <= 0 ? 0 : nMineralKgHa * drenajeMm / (aguaAntesDeDrenarMm + pmpMm);
    }

    public static class Fosforo
    {
        /// <summary>LP: pérdida de rinde por fósforo, con curva lineal-plateau.</summary>
        public static double Perdida(double pBrayPpm, double pKgHa, FosforoParams f, ModeloSuelo m)
        {
            double disponible = pBrayPpm + pKgHa * m.PpmPorKgP;
            return 1 - Math.Min(1, f.MinRelativo + (1 - f.MinRelativo) * disponible / f.PCriticoPpm);
        }

        /// <summary>Balance al cosechar: fósforo aplicado menos exportado en el grano.</summary>
        public static double Actualizar(double pBrayPpm, double pAplicadoKgHa, double rindeQqHa, FosforoParams f, ModeloSuelo m) =>
            Math.Max(m.PBrayMinimo, pBrayPpm + (pAplicadoKgHa - rindeQqHa * f.ExportacionKgPorQq) * m.PpmPorKgP);
    }
}
