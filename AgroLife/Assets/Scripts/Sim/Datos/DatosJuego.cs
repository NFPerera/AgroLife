using System;
using System.Collections.Generic;
using System.Linq;

namespace AgroLife.Sim
{
    public sealed class ArchivosDatos { public string Crops, Pests, Economy, Soils, Region, Lotes, Clima; }

    public sealed class DatosInvalidosException : Exception
    {
        public DatosInvalidosException(string mensaje) : base(mensaje) { }
    }

    /// <summary>Todos los parámetros de una partida, parseados y validados. El juego no arranca con datos parciales.</summary>
    public sealed class DatosJuego
    {
        static readonly string[] CultivosRequeridos = { "trigo", "maiz", "soja1", "soja2" };

        public Dictionary<string, CultivoParams> Cultivos;
        public List<PlagaParams> Plagas;
        public EconomiaParams Economia;
        public ModeloSuelo Suelo;
        public RegionParams Region;
        public List<LoteDatos> Lotes;
        public ClimaParams Clima;

        public LoteDatos Lote(int id) => Lotes[id - 1];

        public static DatosJuego Desde(ArchivosDatos a)
        {
            var errores = new List<string>();
            var crops = Parsear<CultivosArchivo>(a.Crops, "crops.json", errores);
            var pests = Parsear<PlagasArchivo>(a.Pests, "pests.json", errores);
            var economia = Parsear<EconomiaParams>(a.Economy, "economy.json", errores);
            var suelos = Parsear<SuelosArchivo>(a.Soils, "soils.json", errores);
            var region = Parsear<RegionParams>(a.Region, "region.json", errores);
            var lotes = Parsear<LotesArchivo>(a.Lotes, "lotes.json", errores);
            var clima = Parsear<ClimaParams>(a.Clima, "clima.json", errores);

            if (crops != null) ValidarCultivos(crops, errores);
            if (pests != null && crops != null) ValidarPlagas(pests, crops, errores);
            if (economia != null) ValidarEconomia(economia, errores);
            if (clima != null) ValidarClima(clima, errores);
            if (suelos != null && suelos.Modelo == null) errores.Add("soils.json: falta el bloque modelo");
            if (region != null && region.FosforoBray == null) errores.Add("region.json: falta fosforoBray");
            if (lotes != null) ValidarLotes(lotes, errores);

            if (errores.Count > 0)
                throw new DatosInvalidosException("Datos de región inválidos:\n" + string.Join("\n", errores));

            return new DatosJuego
            {
                Cultivos = crops.Cultivos.ToDictionary(c => c.Id),
                Plagas = pests.Plagas,
                Economia = economia,
                Suelo = suelos.Modelo,
                Region = region,
                Lotes = lotes.Lotes,
                Clima = clima,
            };
        }

        static T Parsear<T>(string json, string archivo, List<string> errores) where T : class
        {
            try
            {
                var r = JsonSim.Deserializar<T>(json);
                if (r == null) errores.Add($"{archivo}: está vacío");
                return r;
            }
            catch (Exception ex)
            {
                errores.Add($"{archivo}: {ex.Message}");
                return null;
            }
        }

        static void ValidarCultivos(CultivosArchivo crops, List<string> errores)
        {
            var lista = crops.Cultivos ?? new List<CultivoParams>();
            foreach (var id in CultivosRequeridos)
                if (!lista.Any(c => c.Id == id)) errores.Add($"crops.json: falta el cultivo {id}");
            foreach (var c in lista) ValidarCultivo(c, errores);
        }

        static void ValidarCultivo(CultivoParams c, List<string> errores)
        {
            foreach (Genetica g in Enum.GetValues(typeof(Genetica)))
            {
                if (c.Geneticas == null || !c.Geneticas.TryGetValue(g, out var gp) || gp.Gd == null)
                {
                    errores.Add($"crops.json: {c.Id}: falta la genética {g}");
                    continue;
                }
                var u = gp.Gd;
                if (!(u.Emergencia < u.Vegetativo && u.Vegetativo < u.Floracion && u.Floracion < u.Llenado && u.Llenado < u.Madurez))
                    errores.Add($"crops.json: {c.Id}: los umbrales de grados-día de {g} tienen que ser crecientes");
            }

            void Falta(string bloque) => errores.Add($"crops.json: {c.Id}: falta {bloque}");
            void ValidarFecha(string ddmm) { if (!DiaMesValido(ddmm)) errores.Add($"crops.json: {c.Id}: fecha mal escrita \"{ddmm}\""); }

            if (c.VentanaSiembra == null) Falta("ventanaSiembra");
            else { ValidarFecha(c.VentanaSiembra.Desde); ValidarFecha(c.VentanaSiembra.Hasta); }
            if (c.CurvaFecha == null || c.CurvaFecha.Count == 0) Falta("curvaFecha");
            else foreach (var p in c.CurvaFecha) ValidarFecha(p.Fecha);
            if (c.PlanTipico == null) Falta("planTipico");
            else ValidarFecha(c.PlanTipico.Siembra);
            if (c.Fotoperiodo != null) ValidarFecha(c.Fotoperiodo.FechaReferencia);
            if (c.FactorIp == null) Falta("factorIp");
            if (c.Kc == null) Falta("kc");
            if (c.Helada == null) Falta("helada");
            if (c.Calor == null) Falta("calor");
            if (c.Fosforo == null) Falta("fosforo");
            if (c.Nitrogeno == null && c.Grano != Grano.Soja) Falta("nitrogeno"); // solo la soja fija su propio N
        }

        static bool DiaMesValido(string ddmm)
        {
            var p = ddmm?.Split('/');
            return p != null && p.Length == 2 && int.TryParse(p[0], out int d) && int.TryParse(p[1], out int m)
                   && m >= 1 && m <= 12 && d >= 1 && d <= Fecha.DiasDelMes(m);
        }

        static void ValidarPlagas(PlagasArchivo pests, CultivosArchivo crops, List<string> errores)
        {
            var ids = new List<string>((crops.Cultivos ?? new List<CultivoParams>()).Select(c => c.Id));
            foreach (var p in pests.Plagas ?? new List<PlagaParams>())
            {
                if (p.Cultivos == null) errores.Add($"pests.json: {p.Id}: falta cultivos");
                foreach (var c in p.Cultivos ?? new List<string>())
                    if (!ids.Contains(c)) errores.Add($"pests.json: {p.Id}: cultivo desconocido {c}");
                if (p.Etapas == null) errores.Add($"pests.json: {p.Id}: falta etapas");
                else if (p.Etapas.Contains(Etapa.Madurez)) errores.Add($"pests.json: {p.Id}: no puede aparecer en Madurez"); // se cosecharía con la decisión pendiente
                if (p.Severidad == null) errores.Add($"pests.json: {p.Id}: falta severidad");
            }
        }

        static void ValidarEconomia(EconomiaParams e, List<string> errores)
        {
            foreach (Grano g in Enum.GetValues(typeof(Grano)))
            {
                if (e.Granos == null || !e.Granos.TryGetValue(g, out var gp)) { errores.Add($"economy.json: falta el grano {g}"); continue; }
                if (gp.Estacional == null || gp.Estacional.Length != 12) errores.Add($"economy.json: {g}: estacional tiene que tener 12 valores");
            }
            foreach (var id in CultivosRequeridos)
                if (e.Costos == null || !e.Costos.ContainsKey(id)) errores.Add($"economy.json: faltan los costos de {id}");
            if (e.Fertilizantes == null) errores.Add("economy.json: falta fertilizantes");
            if (e.Flete == null) errores.Add("economy.json: falta flete");
            if (e.Silobolsa == null) errores.Add("economy.json: falta silobolsa");
            if (e.Cuenta == null) errores.Add("economy.json: falta cuenta");
            if (e.CapitalInicial == null) errores.Add("economy.json: falta capitalInicial");
            else if (Array.IndexOf(CultivosRequeridos, e.CapitalInicial.Cultivo) < 0)
                errores.Add($"economy.json: capitalInicial: cultivo desconocido {e.CapitalInicial.Cultivo}");
        }

        static void ValidarClima(ClimaParams c, List<string> errores)
        {
            int n = c.Meses?.Count ?? 0;
            if (n != 12) errores.Add($"clima.json: se esperaban 12 meses y hay {n}");
            for (int i = 0; i < n; i++)
            {
                var m = c.Meses[i];
                if (m.PSecoAHumedo < 0 || m.PSecoAHumedo > 1 || m.PHumedoAHumedo < 0 || m.PHumedoAHumedo > 1)
                    errores.Add($"clima.json: mes {i + 1}: probabilidad fuera de [0, 1]");
                if (m.TmaxSeco == null || m.TmaxHumedo == null || m.TminSeco == null || m.TminHumedo == null || m.RadSeco == null || m.RadHumedo == null)
                    errores.Add($"clima.json: mes {i + 1}: faltan temperaturas o radiación");
            }
            if (c.Autocorrelacion == null) errores.Add("clima.json: falta autocorrelacion");
            if (c.Enso?.Frecuencias == null || Math.Abs(c.Enso.Frecuencias.Values.Sum() - 1) > 0.01)
                errores.Add("clima.json: las frecuencias de fase tienen que sumar 1");
            foreach (FaseEnso f in Enum.GetValues(typeof(FaseEnso)))
                if (c.Enso?.Multiplicadores == null || !c.Enso.Multiplicadores.TryGetValue(f, out var ms) || ms.Count != 12)
                    errores.Add($"clima.json: {f}: se esperaban 12 multiplicadores");
        }

        static void ValidarLotes(LotesArchivo lotes, List<string> errores)
        {
            var lista = lotes.Lotes ?? new List<LoteDatos>();
            if (lista.Count == 0) errores.Add("lotes.json: no hay lotes");
            for (int i = 0; i < lista.Count; i++)
            {
                var l = lista[i];
                if (l.Id != i + 1) errores.Add($"lotes.json: los ids tienen que ser 1..N consecutivos (posición {i + 1}, id {l.Id})");
                double suma = l.Arena + l.Limo + l.Arcilla;
                if (Math.Abs(suma - 100) > 2) errores.Add($"lotes.json: lote {l.Id}: arena + limo + arcilla = {Formato.Numero(suma, 0)}");
                if (l.SuperficieHa <= 0 || l.Ip <= 0 || l.Ip > 100 || l.CcMm <= l.PmpMm || l.PmpMm < 0 || l.CorgPct < 0 || l.DistanciaAcopioKm < 0)
                    errores.Add($"lotes.json: lote {l.Id}: superficie, IP, agua o distancia fuera de rango");
                else
                    l.CalcularDerivados();
            }
        }
    }
}
