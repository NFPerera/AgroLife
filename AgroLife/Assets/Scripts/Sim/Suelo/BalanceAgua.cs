using System;

namespace AgroLife.Sim
{
    public struct FlujosAgua
    {
        public double Infiltracion, Escurrimiento, EtPotencial, EtReal, AguaAntesDeDrenarMm, Drenaje;
        public bool Anegado;
    }

    /// <summary>Balde único de 150 cm. No sabe de cultivos: recibe el kc del día.</summary>
    public static class BalanceAgua
    {
        /// <param name="aguaMm">Agua útil sobre el punto de marchitez; puede superar AuMax hasta AuMax + Drenable.</param>
        public static FlujosAgua PasoDiario(ref double aguaMm, LoteDatos suelo, ModeloSuelo m, double lluviaMm, double et0Mm, double kc)
        {
            double au = suelo.AuMaxMm;
            var f = new FlujosAgua();

            // 1. Entrada de lluvia: lo que supera la capacidad de infiltración del día escurre.
            f.Infiltracion = Math.Max(0, Math.Min(lluviaMm, Math.Min(suelo.KsatMmH * m.HorasInfiltracion, au + suelo.DrenableMm - aguaMm)));
            f.Escurrimiento = lluviaMm - f.Infiltracion;
            aguaMm += f.Infiltracion;

            // 2. Consumo, reducido cuando el suelo está seco.
            f.EtPotencial = et0Mm * kc;
            double umbral = (1 - m.FraccionAgotamiento) * au;
            double ks = aguaMm >= umbral ? 1 : aguaMm / umbral;
            f.EtReal = Math.Min(aguaMm, f.EtPotencial * ks);
            aguaMm -= f.EtReal;

            // 3. Drenaje del agua por encima de capacidad de campo.
            f.AguaAntesDeDrenarMm = aguaMm;
            f.Drenaje = Math.Max(0, Math.Min(aguaMm - au, suelo.KsatMmH * m.HorasDrenaje));
            aguaMm -= f.Drenaje;

            // 4. Anegamiento en suelos de drenaje lento.
            f.Anegado = aguaMm > au && suelo.KsatMmH < m.KsatAnegamientoMmH;
            return f;
        }
    }
}
