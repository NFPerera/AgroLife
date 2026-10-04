using System.Collections.Generic;

namespace AgroLife.Sim
{
    public sealed class PresupuestoCultivo
    {
        public string CultivoId;
        public double CostoUsd, RindeEsperadoQqHa, MargenBrutoUsd, RindeIndiferenciaQqHa;
    }

    /// <summary>Presupuesto en vivo de un plan: uno o dos cultivos (doble cultivo).</summary>
    public sealed class Presupuesto
    {
        public List<PresupuestoCultivo> Cultivos = new List<PresupuestoCultivo>();
        public double CostoTotalUsd, MargenBrutoUsd;
    }

    /// <summary>Reporte de campaña de un lote.</summary>
    public sealed class ReporteLote
    {
        public List<ResultadoCultivo> Cultivos;
        public double MargenBrutoUsd, ArrendamientoUsd, ResultadoUsd;
        public Dictionary<string, double> PromedioZonaQqHa;
    }
}
