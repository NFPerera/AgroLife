using System;
using System.Collections.Generic;

namespace AgroLife.Sim
{
    /// <summary>
    /// Núcleo de la partida. Game lee <see cref="Estado"/> para dibujar y lo cambia solo a través de las acciones.
    /// </summary>
    public sealed partial class Simulation
    {
        static readonly string[] Rotacion = { "maiz", "soja1", "trigo" }; // el trigo de terceros siempre va con soja de segunda
        static readonly Flujo[] Flujos = { Flujo.Clima, Flujo.Precios, Flujo.Plagas, Flujo.Silos, Flujo.Inicial };

        public DatosJuego Datos { get; }
        public EstadoJuego Estado { get; }
        public IReadOnlyList<Evento> EventosIniciales { get; private set; } = new List<Evento>();

        Simulation(DatosJuego datos, EstadoJuego estado)
        {
            Datos = datos;
            Estado = estado;
        }

        /// <summary>Partida nueva al 1/5 Año 1, con ese día ya simulado; sus eventos quedan en EventosIniciales.</summary>
        public static Simulation Nueva(DatosJuego datos, ulong semilla)
        {
            var e = new EstadoJuego
            {
                RegionId = datos.Region.Id,
                Semilla = semilla,
                Fecha = Fecha.Crear(1, 5, 1),
                Rng = new Dictionary<Flujo, Pcg32>(),
                Clima = new EstadoClima(),
                Precios = new EstadoPrecios(),
                Lotes = new List<EstadoLote>(),
                StockT = new Dictionary<Grano, double> { [Grano.Trigo] = 0, [Grano.Maiz] = 0, [Grano.Soja] = 0 },
                Contratos = new List<Contrato>(),
                Resultados = new List<ResultadoCultivo>(),
                PromediosZona = new List<PromedioZona>(),
            };
            foreach (var f in Flujos) e.Rng[f] = new Pcg32(semilla, (ulong)f);
            Precios.Inicializar(e.Precios, datos.Economia, e.Fecha);

            var p = datos.Region.FosforoBray;
            var inicial = e.Rng[Flujo.Inicial];
            foreach (var l in datos.Lotes)
                e.Lotes.Add(new EstadoLote
                {
                    Id = l.Id,
                    Tenencia = Tenencia.Tercero,
                    AguaMm = l.AuMaxMm * datos.Suelo.AguaInicialFraccion,
                    NMineralKgHa = datos.Suelo.NInicialKgHa,
                    PBrayPpm = Math.Max(p.Minimo, p.Media + p.Desvio * inicial.NextGaussian()),
                    CultivoAnterior = UltimoCultivoDeRotacion(l.Id, 0),
                });
            e.SaldoUsd = CapitalInicial(datos);

            var s = new Simulation(datos, e);
            s.EventosIniciales = s.SimularDia();
            return s;
        }

        /// <summary>
        /// Avanza un día y devuelve sus eventos. Con la partida terminada no hace nada.
        /// Con una decisión de plaga pendiente lanza InvalidOperationException: Game tiene que resolverla antes.
        /// </summary>
        public List<Evento> StepDay()
        {
            if (Estado.Terminada) return new List<Evento>();
            if (Estado.Lotes.Exists(l => l.Decision != null)) throw new InvalidOperationException("Hay decisiones de plaga pendientes");
            Estado.Fecha = Estado.Fecha.MasDias(1);
            return SimularDia();
        }

        public double RindeEstimadoQqHa(int loteId)
        {
            var c = Estado.Lotes[loteId - 1].Cultivo;
            return c == null ? 0 : ModeloCultivo.CalcularCascada(c, Datos.Cultivos[c.CultivoId]).RealQqHa;
        }

        public double PromedioZonaQqHa(int campania, string cultivoId)
        {
            var p = Estado.PromediosZona.Find(x => x.Campania == campania && x.CultivoId == cultivoId);
            return p == null || p.SumaHa <= 0 ? 0 : p.SumaQqPorHa / p.SumaHa;
        }

        public double FondosDisponiblesUsd() => Estado.SaldoUsd + Datos.Economia.Cuenta.LimiteDescubiertoUsd;

        /// <summary>Siembras ya planificadas en lotes del jugador que todavía no se cobraron (incluye la soja de segunda).</summary>
        public double CostoComprometidoUsd()
        {
            double total = 0;
            foreach (var l in Estado.Lotes)
            {
                if (l.Tenencia == Tenencia.Tercero) continue;
                double ha = Datos.Lote(l.Id).SuperficieHa;
                if (l.Plan != null) total += CostoSiembraUsdHa(l.Plan) * ha + (l.Plan.Soja2 != null ? CostoSiembraUsdHa(l.Plan.Soja2) * ha : 0);
                if (l.Cultivo?.SegundoCultivo != null) total += CostoSiembraUsdHa(l.Cultivo.SegundoCultivo) * ha;
            }
            return total;
        }

        /// <summary>Lo que queda para gastar sin pisar las siembras comprometidas.</summary>
        double FondosLibresUsd() => FondosDisponiblesUsd() - CostoComprometidoUsd();

        double CostoSiembraUsdHa(PlanCultivo p) => Economia.CostosDirectosSiembraUsdHa(p.CultivoId, p.NKgHa, p.PKgHa, Datos.Economia);

        public const int VersionGuardado = 1;

        /// <summary>Partida en JSON. Escribirla a disco (temporal + reemplazo) es tarea de Game.</summary>
        public string Guardar() => JsonSim.Serializar(new Partida { Version = VersionGuardado, Estado = Estado });

        public static Simulation Cargar(DatosJuego datos, string json)
        {
            Partida p;
            try
            {
                // La versión se lee primero: una partida de otra versión puede no tener la forma actual.
                int version = JsonSim.Deserializar<Partida.Cabecera>(json)?.Version ?? 0;
                if (version != VersionGuardado)
                    throw new PartidaIncompatibleException($"La partida guardada es de una versión incompatible (v{version}; se esperaba v{VersionGuardado})");
                p = JsonSim.Deserializar<Partida>(json);
            }
            catch (PartidaIncompatibleException) { throw; }
            catch (Exception ex) { throw new PartidaIncompatibleException("La partida guardada está dañada: " + ex.Message); }

            if (p?.Estado?.Lotes == null) throw new PartidaIncompatibleException("La partida guardada está dañada: no tiene estado");
            if (p.Estado.RegionId != datos.Region.Id)
                throw new PartidaIncompatibleException($"La partida guardada es de otra región ({p.Estado.RegionId})");
            if (p.Estado.Lotes.Count != datos.Lotes.Count)
                throw new PartidaIncompatibleException("La partida guardada no coincide con los lotes de la región");
            return new Simulation(datos, p.Estado);
        }

        /// <summary>Lo que cuesta arrendar 250 ha de IP promedio y sembrarlas con soja de primera, a precios de referencia.</summary>
        static double CapitalInicial(DatosJuego d)
        {
            double ha = 0, ipPorHa = 0;
            foreach (var l in d.Lotes) { ha += l.SuperficieHa; ipPorHa += l.Ip * l.SuperficieHa; }
            var ci = d.Economia.CapitalInicial;
            var plan = d.Cultivos[ci.Cultivo].PlanTipico;
            double arriendo = d.Region.ArrendamientoBaseQqHa * (ipPorHa / ha) / 100 * d.Economia.Granos[Grano.Soja].ReferenciaUsdT / 10;
            return ci.Hectareas * (arriendo + Economia.CostosDirectosSiembraUsdHa(ci.Cultivo, plan.NKgHa, plan.PKgHa, d.Economia));
        }

        static string UltimoCultivoDeRotacion(int loteId, int campania)
        {
            var c = Rotacion[(loteId + campania) % 3];
            return c == "trigo" ? "soja2" : c;
        }

        Pcg32 Rng(Flujo f) => Estado.Rng[f];

        Evento NuevoEvento(TipoEvento tipo, int loteId, string mensaje, bool pausa = false) =>
            new Evento { Tipo = tipo, Fecha = Estado.Fecha, LoteId = loteId, Mensaje = mensaje, Pausa = pausa };
    }

    public sealed class Partida
    {
        public int Version;
        public EstadoJuego Estado;

        internal sealed class Cabecera { public int Version; }
    }

    public sealed class PartidaIncompatibleException : Exception
    {
        public PartidaIncompatibleException(string mensaje) : base(mensaje) { }
    }
}
