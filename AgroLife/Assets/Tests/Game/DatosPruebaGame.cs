using System.IO;
using AgroLife.Sim;

namespace AgroLife.Game.Tests
{
    /// <summary>La región de prueba de Sim (Assets/Tests/Sim/Datos), leída desde los tests de Game.</summary>
    static class DatosPruebaGame
    {
        static string Leer(string archivo) => File.ReadAllText(Path.Combine("Assets", "Tests", "Sim", "Datos", archivo));

        public static DatosJuego Datos() => DatosJuego.Desde(new ArchivosDatos
        {
            Crops = Leer("crops.json"), Pests = Leer("pests.json"), Economy = Leer("economy.json"), Soils = Leer("soils.json"),
            Region = Leer("region.json"), Lotes = Leer("lotes.json"), Clima = Leer("clima.json"),
        });

        public static Simulation Nueva(ulong semilla = 1234)
        {
            var d = Datos();
            foreach (var p in d.Plagas) p.ProbDiaria = 0;
            return Simulation.Nueva(d, semilla);
        }
    }
}
