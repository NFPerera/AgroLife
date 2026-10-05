using System;
using System.Collections.Generic;

namespace AgroLife.Sim
{
    public sealed class PrecioDia { public Fecha Fecha; public double TrigoUsdT, MaizUsdT, SojaUsdT; }

    public sealed class EstadoPrecios
    {
        public const int DiasDeHistorial = 730;

        public Dictionary<Grano, double> X = new Dictionary<Grano, double>();
        public Dictionary<Grano, double> PrecioUsdT = new Dictionary<Grano, double>();
        /// <summary>Los últimos DiasDeHistorial días simulados, en orden (para el gráfico del mercado).</summary>
        public List<PrecioDia> Historial = new List<PrecioDia>();
    }

    /// <summary>Precio diario = referencia × estacionalidad del mes × exp(x), con x que tiende a volver a cero.</summary>
    public static class Precios
    {
        static readonly Grano[] Granos = { Grano.Trigo, Grano.Maiz, Grano.Soja };

        public static void Inicializar(EstadoPrecios e, EconomiaParams p, Fecha fecha)
        {
            foreach (var g in Granos)
            {
                e.X[g] = 0;
                e.PrecioUsdT[g] = Precio(p.Granos[g], fecha, 0);
            }
        }

        public static void AvanzarDia(EstadoPrecios e, EconomiaParams p, Fecha fecha, Pcg32 rng)
        {
            foreach (var g in Granos)
            {
                var gp = p.Granos[g];
                double phi = Math.Exp(-Math.Log(2) / gp.VidaMediaDias);
                double x = phi * e.X[g] + gp.VolatilidadDiaria * rng.NextGaussian();
                e.X[g] = x;
                e.PrecioUsdT[g] = Precio(gp, fecha, x);
            }
        }

        static double Precio(GranoParams gp, Fecha fecha, double x) => gp.ReferenciaUsdT * gp.Estacional[fecha.Mes - 1] * Math.Exp(x);
    }
}
