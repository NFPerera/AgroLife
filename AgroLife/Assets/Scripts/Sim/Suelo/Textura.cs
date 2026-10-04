using System;

namespace AgroLife.Sim
{
    public enum ClaseTextural
    {
        Arenoso, ArenosoFranco, FrancoArenoso, Franco, FrancoLimoso, Limoso,
        FrancoArcillosoArenoso, FrancoArcilloso, FrancoArcillosoLimoso, ArcillosoArenoso, ArcillosoLimoso, Arcilloso
    }

    public static class Textura
    {
        /// <summary>Triángulo textural USDA; arena, limo y arcilla en %.</summary>
        public static ClaseTextural Clasificar(double arena, double limo, double arcilla)
        {
            double s = arena, l = limo, c = arcilla;
            if (l + 1.5 * c < 15) return ClaseTextural.Arenoso;
            if (l + 2 * c < 30) return ClaseTextural.ArenosoFranco;
            if ((c >= 7 && c < 20 && s > 52) || (c < 7 && l < 50)) return ClaseTextural.FrancoArenoso;
            if (c >= 7 && c < 27 && l >= 28 && l < 50 && s <= 52) return ClaseTextural.Franco;
            if ((l >= 50 && c >= 12 && c < 27) || (l >= 50 && l < 80 && c < 12)) return ClaseTextural.FrancoLimoso;
            if (l >= 80 && c < 12) return ClaseTextural.Limoso;
            if (c >= 20 && c < 35 && l < 28 && s > 45) return ClaseTextural.FrancoArcillosoArenoso;
            if (c >= 27 && c < 40 && s > 20 && s <= 45) return ClaseTextural.FrancoArcilloso;
            if (c >= 27 && c < 40 && s <= 20) return ClaseTextural.FrancoArcillosoLimoso;
            if (c >= 35 && s > 45) return ClaseTextural.ArcillosoArenoso;
            if (c >= 40 && l >= 40) return ClaseTextural.ArcillosoLimoso;
            return ClaseTextural.Arcilloso;
        }

        /// <summary>Conductividad saturada de Saxton &amp; Rawls (2006), en mm/h.</summary>
        public static double KsatMmH(double arena, double arcilla, double materiaOrganicaPct)
        {
            var (t1500, t33, ts) = Contenidos(arena, arcilla, materiaOrganicaPct);
            double lambda = (Math.Log(t33) - Math.Log(t1500)) / (Math.Log(1500) - Math.Log(33));
            return 1930 * Math.Pow(ts - t33, 3 - lambda);
        }

        /// <summary>Agua entre capacidad de campo y saturación en el perfil de 150 cm, en mm.</summary>
        public static double DrenableMm(double arena, double arcilla, double materiaOrganicaPct)
        {
            var (_, t33, ts) = Contenidos(arena, arcilla, materiaOrganicaPct);
            return (ts - t33) * 1500;
        }

        static (double t1500, double t33, double ts) Contenidos(double arena, double arcilla, double mo)
        {
            double S = arena / 100, C = arcilla / 100;
            double t1500t = -0.024 * S + 0.487 * C + 0.006 * mo + 0.005 * S * mo - 0.013 * C * mo + 0.068 * S * C + 0.031;
            double t1500 = t1500t + (0.14 * t1500t - 0.02);
            double t33t = -0.251 * S + 0.195 * C + 0.011 * mo + 0.006 * S * mo - 0.027 * C * mo + 0.452 * S * C + 0.299;
            double t33 = t33t + (1.283 * t33t * t33t - 0.374 * t33t - 0.015);
            double ts33t = 0.278 * S + 0.034 * C + 0.022 * mo - 0.018 * S * mo - 0.027 * C * mo - 0.584 * S * C + 0.078;
            double ts33 = ts33t + (0.636 * ts33t - 0.107);
            double ts = t33 + ts33 - 0.097 * S + 0.043;
            return (t1500, t33, ts);
        }
    }
}
