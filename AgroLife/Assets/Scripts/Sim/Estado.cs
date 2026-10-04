using System.Collections.Generic;

namespace AgroLife.Sim
{
    // Estado completo y serializable de una partida. Solo campos públicos: es lo que se guarda.

    public enum Tenencia { Tercero, Arrendado, Propio }

    public sealed class EstadoJuego
    {
        public string RegionId;
        public ulong Semilla;
        public Fecha Fecha; // hoy, ya simulado
        public Dictionary<Flujo, Pcg32> Rng;
        public EstadoClima Clima;
        public EstadoPrecios Precios;
        public List<EstadoLote> Lotes; // índice = id − 1
        public double SaldoUsd;
        public Dictionary<Grano, double> StockT; // en silobolsa
        public List<Contrato> Contratos;
        public List<ResultadoCultivo> Resultados;
        public List<PromedioZona> PromediosZona;
        public bool AlertaSaldoEmitida, Terminada;
    }

    public sealed class EstadoLote
    {
        public int Id;
        public Tenencia Tenencia;
        public Contrato Contrato;
        public double AguaMm, NMineralKgHa, PBrayPpm;
        public string CultivoAnterior;
        public PlanCultivo Plan;          // planificado, todavía sin sembrar
        public EstadoCultivo Cultivo;     // en pie
        public DecisionPlaga Decision;    // pendiente
    }

    public sealed class Contrato
    {
        public int LoteId, Campania;
        public double MontoUsd;
        public Fecha Vence;
    }

    public sealed class DecisionPlaga
    {
        public string PlagaId;
        public double Severidad, Nivel, Umbral, CostoUsd, PerdidaEsperadaQq, PerdidaEsperadaUsd;
    }

    public sealed class PromedioZona
    {
        public int Campania;
        public string CultivoId;
        public double SumaQqPorHa, SumaHa;
    }

    public sealed class ResultadoCultivo
    {
        public int LoteId, Campania;
        public string CultivoId;
        public double SuperficieHa;
        public Fecha FechaCosecha;
        public Cascada Cascada;
        public double ToneladasCosechadas, PrecioCosechaUsdT, RindeIndiferenciaQqHa;
        public DesgloseMargen Margen;
    }
}
