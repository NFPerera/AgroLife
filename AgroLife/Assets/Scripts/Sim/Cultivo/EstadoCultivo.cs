using System.Collections.Generic;

namespace AgroLife.Sim
{
    public sealed class PlanCultivo
    {
        public string CultivoId;
        public Fecha FechaSiembra;
        public Genetica Genetica;
        public double NKgHa, PKgHa;
        public bool VenderAlCosechar = true;
        /// <summary>Solo con trigo (doble cultivo). Su FechaSiembra se fija al cosechar el trigo.</summary>
        public PlanCultivo Soja2;

        public PlanCultivo Copia()
        {
            var c = (PlanCultivo)MemberwiseClone();
            c.Soja2 = Soja2?.Copia();
            return c;
        }
    }

    public sealed class EstadoCultivo
    {
        public string CultivoId;
        public Genetica Genetica;
        public Fecha FechaSiembra;
        public int Campania;
        public double GradosDia, FactorFotoperiodo = 1;
        public Etapa Etapa;
        public int DiasEnMadurez;
        public double RindeAlcanzableQqHa, LFecha, LP; // fijos desde la siembra
        public double SumaEstresAgua, SumaAnegamiento, LTemp, LPlagas;
        public double NDemandaTotalKg, NDemandaHastaHoyKg, NAbsorbidoKg, NAplicadoKgHa, PAplicadoKgHa;
        public double CostosDirectosUsd;
        public bool VenderAlCosechar = true;
        public List<string> PlagasOcurridas = new List<string>();
        public PlanCultivo SegundoCultivo;
    }

    /// <summary>Fracciones de pérdida (0..1), en el orden fijo de atribución del reporte.</summary>
    public struct Perdidas { public double Fecha, Agua, Anegamiento, Temperatura, Nitrogeno, Fosforo, Plagas; }

    /// <summary>Rinde potencial → pérdida por cada causa → rinde real, todo en qq/ha.</summary>
    public struct Cascada
    {
        public double PotencialQqHa, FechaQq, AguaQq, AnegamientoQq, TemperaturaQq, NitrogenoQq, FosforoQq, PlagasQq, RealQqHa;
    }
}
