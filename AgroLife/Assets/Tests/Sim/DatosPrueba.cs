using System.Collections.Generic;
using System.IO;

namespace AgroLife.Sim.Tests
{
    /// <summary>Lee las copias congeladas de los datos (Tests/Sim/Datos) y la región de prueba.</summary>
    static class DatosPrueba
    {
        public static string Leer(string archivo) => File.ReadAllText(Path.Combine("Assets", "Tests", "Sim", "Datos", archivo));

        public static ArchivosDatos Archivos() => new ArchivosDatos
        {
            Crops = Leer("crops.json"), Pests = Leer("pests.json"), Economy = Leer("economy.json"), Soils = Leer("soils.json"),
            Region = Leer("region.json"), Lotes = Leer("lotes.json"), Clima = Leer("clima.json"),
        };

        public static DatosJuego Cargar() => DatosJuego.Desde(Archivos());

        /// <summary>Nueva partida con la región de prueba; sin plagas salvo que se pidan, así no hay decisiones que bloqueen.</summary>
        public static Simulation Nueva(ulong semilla = 1234, bool plagas = false)
        {
            var d = Cargar();
            if (!plagas) foreach (var p in d.Plagas) p.ProbDiaria = 0;
            return Simulation.Nueva(d, semilla);
        }

        public static List<Evento> AvanzarHasta(Simulation s, Fecha f)
        {
            var eventos = new List<Evento>();
            while (s.Estado.Fecha < f) eventos.AddRange(s.StepDay());
            return eventos;
        }
    }
}
