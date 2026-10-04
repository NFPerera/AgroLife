using System;

namespace AgroLife.Sim
{
    public static class Hargreaves
    {
        /// <summary>Evapotranspiración de referencia (mm/día) con Ra de FAO-56; diaDelAnio 0..364.</summary>
        public static double Et0(double tmax, double tmin, int diaDelAnio, double latitudGrados)
        {
            int j = diaDelAnio + 1;
            double dr = 1 + 0.033 * Math.Cos(2 * Math.PI * j / 365);
            double decl = 0.409 * Math.Sin(2 * Math.PI * j / 365 - 1.39);
            double lat = latitudGrados * Math.PI / 180;
            double ws = Math.Acos(-Math.Tan(lat) * Math.Tan(decl));
            double ra = 24 * 60 / Math.PI * 0.0820 * dr *
                        (ws * Math.Sin(lat) * Math.Sin(decl) + Math.Cos(lat) * Math.Cos(decl) * Math.Sin(ws));
            return 0.0023 * ((tmax + tmin) / 2 + 17.8) * Math.Sqrt(tmax - tmin) * 0.408 * ra;
        }
    }
}
