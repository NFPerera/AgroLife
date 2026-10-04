using System;
using System.Collections.Generic;

namespace AgroLife.Sim
{
    public struct DiaClima
    {
        public double LluviaMm, TmaxC, TminC, RadMJ, Et0Mm;
        public double Tmedia() => (TmaxC + TminC) / 2;
    }

    public sealed class EstadoClima
    {
        public bool AyerLlovio;
        public double ZTmax, ZTmin, ZRad;
        public int DiasDesdeLluvia = 99;
        public FaseEnso Fase = FaseEnso.Neutro;
        public Dictionary<FaseEnso, double> Pronostico = new Dictionary<FaseEnso, double>();
        public DiaClima Hoy;
    }

    /// <summary>Generador estadístico tipo WGEN con fase de El Niño / La Niña sorteada al inicio de cada campaña.</summary>
    public static class GeneradorClima
    {
        static readonly FaseEnso[] Fases = { FaseEnso.Nino, FaseEnso.Neutro, FaseEnso.Nina };

        public static DiaClima GenerarDia(EstadoClima e, ClimaParams p, double latitud, Fecha fecha, Pcg32 rng)
        {
            var m = p.Meses[fecha.Mes - 1];
            var k = p.Enso.Multiplicadores[e.Fase][fecha.Mes - 1];

            double pLluvia = Math.Min(0.95, (e.AyerLlovio ? m.PHumedoAHumedo : m.PSecoAHumedo) * k.Frecuencia);
            bool llueve = rng.NextDouble() < pLluvia;
            double lluvia = llueve ? rng.NextGamma(m.GammaForma, m.GammaEscala * k.Cantidad) : 0;

            e.ZTmax = Ar1(e.ZTmax, p.Autocorrelacion.Tmax, rng);
            e.ZTmin = Ar1(e.ZTmin, p.Autocorrelacion.Tmin, rng);
            e.ZRad = Ar1(e.ZRad, p.Autocorrelacion.Rad, rng);

            var tx = llueve ? m.TmaxHumedo : m.TmaxSeco;
            var tn = llueve ? m.TminHumedo : m.TminSeco;
            var rd = llueve ? m.RadHumedo : m.RadSeco;
            double tmax = tx.Media + tx.Desvio * e.ZTmax;
            double tmin = tn.Media + tn.Desvio * e.ZTmin;
            double rad = Math.Max(1, rd.Media + rd.Desvio * e.ZRad);
            if (tmin > tmax - 1) tmin = tmax - 1;

            var dia = new DiaClima
            {
                LluviaMm = lluvia, TmaxC = tmax, TminC = tmin, RadMJ = rad,
                Et0Mm = Hargreaves.Et0(tmax, tmin, fecha.DiaDelAnio, latitud),
            };
            e.AyerLlovio = llueve;
            e.DiasDesdeLluvia = llueve ? 0 : e.DiasDesdeLluvia + 1;
            e.Hoy = dia;
            return dia;
        }

        /// <summary>Sortea la fase real y el pronóstico: favorece la fase real con probabilidad 0,7.</summary>
        public static void SortearFase(EstadoClima e, ClimaParams p, Pcg32 rng)
        {
            double u = rng.NextDouble(), acumulada = 0;
            e.Fase = Fases[Fases.Length - 1];
            foreach (var f in Fases)
            {
                acumulada += p.Enso.Frecuencias[f];
                if (u < acumulada) { e.Fase = f; break; }
            }

            var favorecida = e.Fase;
            if (rng.NextDouble() >= 0.7)
            {
                var otras = Array.FindAll(Fases, f => f != e.Fase);
                favorecida = otras[rng.NextDouble() < 0.5 ? 0 : 1];
            }

            e.Pronostico = new Dictionary<FaseEnso, double>();
            foreach (var f in Fases) e.Pronostico[f] = f == favorecida ? 0.70 : 0.15;
        }

        static double Ar1(double z, double rho, Pcg32 rng) => rho * z + Math.Sqrt(1 - rho * rho) * rng.NextGaussian();
    }
}
