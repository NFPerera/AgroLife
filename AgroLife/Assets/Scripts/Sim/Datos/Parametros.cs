using System.Collections.Generic;

namespace AgroLife.Sim
{
    // Espejo de los JSON de Assets/Data y de Data/Regions/<región>. Newtonsoft no distingue mayúsculas al leer.

    public enum Etapa { Siembra, Emergencia, Vegetativo, Floracion, Llenado, Madurez }
    public enum Genetica { Corto, Largo }
    public enum Grano { Trigo, Maiz, Soja }
    public enum FaseEnso { Nino, Neutro, Nina }

    // crops.json
    public sealed class CultivosArchivo { public List<CultivoParams> Cultivos; }
    public sealed class CultivoParams
    {
        public string Id, Nombre;
        public Grano Grano;
        public double TemperaturaBase;
        public Ventana VentanaSiembra;
        public Dictionary<Genetica, GeneticaParams> Geneticas;
        public FactorIp FactorIp;
        public List<PuntoCurva> CurvaFecha;
        public Dictionary<Etapa, double> Kc, SensibilidadAgua, SensibilidadAnegamiento; // etapa ausente = 0
        public EventoTermico Helada, Calor;
        public NitrogenoParams Nitrogeno; // null en soja
        public FosforoParams Fosforo;
        public int DiasACosecha;
        public double FactorClimaEsperado;
        public PlanTipico PlanTipico;
        public Fotoperiodo Fotoperiodo; // null salvo soja
    }
    public sealed class Ventana { public string Desde, Hasta; }
    public sealed class GeneticaParams { public double RpotQqHa; public UmbralesGd Gd; }
    public sealed class UmbralesGd { public double Emergencia, Vegetativo, Floracion, Llenado, Madurez; }
    public sealed class FactorIp { public double A, B; }
    public sealed class PuntoCurva { public string Fecha; public double Perdida; }
    public sealed class EventoTermico { public double Umbral; public Dictionary<Etapa, double> Perdida; }
    public sealed class NitrogenoParams { public double KgPorQq, MinRelativo, MineralizacionEsperadaKgHa; }
    public sealed class FosforoParams { public double PCriticoPpm, MinRelativo, ExportacionKgPorQq; }
    public sealed class PlanTipico { public string Siembra; public Genetica Genetica; public double NKgHa, PKgHa; }
    public sealed class Fotoperiodo { public string FechaReferencia; public double AcortamientoPorDia, FactorMinimo; }

    // pests.json
    public sealed class PlagasArchivo { public List<PlagaParams> Plagas; }
    public sealed class PlagaParams
    {
        public string Id, Nombre, Unidad;
        public List<string> Cultivos;
        public List<Etapa> Etapas;
        public double ProbDiaria, TempMinima, MultiplicadorLluvia, Umbral, Eficacia, ProductoUsdHa, PulverizacionUsdHa, EscalaNivel;
        public Rango Severidad;
    }
    public sealed class Rango { public double Min, Max; }

    // economy.json
    public sealed class EconomiaParams
    {
        public Dictionary<Grano, GranoParams> Granos;
        public Dictionary<string, CostosCultivo> Costos;
        public Fertilizantes Fertilizantes;
        public double CosechaPorcentaje, ComisionVenta;
        public Flete Flete;
        public Silobolsa Silobolsa;
        public Cuenta Cuenta;
        public CapitalInicial CapitalInicial;
    }
    public sealed class GranoParams { public double ReferenciaUsdT, VolatilidadDiaria, VidaMediaDias; public double[] Estacional; }
    public sealed class CostosCultivo { public double SemillaUsdHa, AgroquimicosUsdHa, SiembraUsdHa, PulverizacionesUsdHa; }
    public sealed class Fertilizantes { public double NitrogenoUsdKg, FosforoUsdKg, AplicacionUsdHa; }
    public sealed class Flete { public double FijoUsdT, UsdTKm; }
    public sealed class Silobolsa { public double EmbolsadoUsdT, RiesgoMensual, PerdidaMin, PerdidaMax; }
    public sealed class Cuenta { public double LimiteDescubiertoUsd, TasaAnualDescubierto, AlertaFraccion; }
    public sealed class CapitalInicial { public double Hectareas; public string Cultivo; }

    // soils.json
    public sealed class SuelosArchivo { public ModeloSuelo Modelo; }
    public sealed class ModeloSuelo
    {
        public double HorasInfiltracion, HorasDrenaje, KsatAnegamientoMmH, FraccionAgotamiento, KcSueloDesnudo,
            AguaInicialFraccion, NInicialKgHa, KMineralizacion, PpmPorKgP, PBrayMinimo;
    }

    // region.json
    public sealed class RegionParams
    {
        public string Id, Nombre;
        public double Latitud, Longitud, PrecioBaseTierraUsdHa, ArrendamientoBaseQqHa;
        public DistribucionP FosforoBray;
    }
    public sealed class DistribucionP { public double Media, Desvio, Minimo; }

    // lotes.json
    public sealed class LotesArchivo { public List<LoteDatos> Lotes; }
    public sealed class LoteDatos
    {
        public int Id;
        public double SuperficieHa, Arena, Limo, Arcilla, CorgPct, CcMm, PmpMm, Ip, DistanciaAcopioKm;
        public string UnidadSuelo;

        // Derivados (CalcularDerivados)
        public ClaseTextural Clase;
        public double AuMaxMm, KsatMmH, DrenableMm;

        public void CalcularDerivados()
        {
            double mo = CorgPct * 1.724;
            Clase = Textura.Clasificar(Arena, Limo, Arcilla);
            AuMaxMm = CcMm - PmpMm;
            KsatMmH = Textura.KsatMmH(Arena, Arcilla, mo);
            DrenableMm = Textura.DrenableMm(Arena, Arcilla, mo);
        }
    }

    // clima.json
    public sealed class ClimaParams { public List<MesClima> Meses; public Autocorrelacion Autocorrelacion; public EnsoParams Enso; }
    public sealed class MesClima
    {
        public double PSecoAHumedo, PHumedoAHumedo, GammaForma, GammaEscala;
        public MediaDesvio TmaxSeco, TmaxHumedo, TminSeco, TminHumedo, RadSeco, RadHumedo;
    }
    public sealed class MediaDesvio { public double Media, Desvio; }
    public sealed class Autocorrelacion { public double Tmax, Tmin, Rad; }
    public sealed class EnsoParams
    {
        public Dictionary<FaseEnso, double> Frecuencias;
        public Dictionary<FaseEnso, List<MultiplicadorMes>> Multiplicadores;
    }
    public sealed class MultiplicadorMes { public double Frecuencia, Cantidad; }
}
