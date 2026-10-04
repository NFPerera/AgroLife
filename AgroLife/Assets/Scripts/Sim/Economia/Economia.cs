namespace AgroLife.Sim
{
    public struct DesgloseMargen
    {
        public double IngresoBrutoUsd, ComercializacionUsd, CosechaUsd, CostosDirectosUsd, MargenBrutoUsd;
    }

    /// <summary>Economía ideal en dólares. La cosecha se cobra sobre el valor neto, así el margen es 0 en el rinde de indiferencia.</summary>
    public static class Economia
    {
        public static double FleteUsdT(EconomiaParams p, double distanciaKm) => p.Flete.FijoUsdT + p.Flete.UsdTKm * distanciaKm;

        public static double PrecioNetoUsdT(double precioUsdT, EconomiaParams p, double distanciaKm) =>
            precioUsdT * (1 - p.ComisionVenta) - FleteUsdT(p, distanciaKm);

        public static double CostosDirectosSiembraUsdHa(string cultivoId, double nKgHa, double pKgHa, EconomiaParams p)
        {
            var c = p.Costos[cultivoId];
            var f = p.Fertilizantes;
            return c.SemillaUsdHa + c.AgroquimicosUsdHa + c.SiembraUsdHa + c.PulverizacionesUsdHa
                   + nKgHa * f.NitrogenoUsdKg + pKgHa * f.FosforoUsdKg + (nKgHa + pKgHa > 0 ? f.AplicacionUsdHa : 0);
        }

        public static double RindeIndiferenciaQqHa(double costosDirectosUsdHa, double precioNetoUsdT, double cosechaPct) =>
            costosDirectosUsdHa / (precioNetoUsdT / 10 * (1 - cosechaPct));

        public static DesgloseMargen Margen(double toneladas, double precioUsdT, double distanciaKm, double costosDirectosUsd, EconomiaParams p)
        {
            var m = new DesgloseMargen
            {
                IngresoBrutoUsd = toneladas * precioUsdT,
                ComercializacionUsd = toneladas * (precioUsdT * p.ComisionVenta + FleteUsdT(p, distanciaKm)),
                CosechaUsd = p.CosechaPorcentaje * toneladas * PrecioNetoUsdT(precioUsdT, p, distanciaKm),
                CostosDirectosUsd = costosDirectosUsd,
            };
            m.MargenBrutoUsd = m.IngresoBrutoUsd - m.ComercializacionUsd - m.CostosDirectosUsd - m.CosechaUsd;
            return m;
        }

        public static double ArrendamientoUsd(LoteDatos l, RegionParams r, double precioSojaUsdT) =>
            r.ArrendamientoBaseQqHa * l.Ip / 100 * l.SuperficieHa * precioSojaUsdT / 10;

        public static double PrecioCompraUsd(LoteDatos l, RegionParams r) => r.PrecioBaseTierraUsdHa * l.Ip / 100 * l.SuperficieHa;

        public static double InteresDiario(double saldoUsd, double tasaAnual) => saldoUsd < 0 ? saldoUsd * tasaAnual / 365 : 0;
    }
}
