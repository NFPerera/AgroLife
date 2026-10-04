using System.IO;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    /// <summary>Contrato entre el pipeline de Python y Sim: los datos reales de Pergamino cargan y simulan un año.</summary>
    public class PergaminoTests
    {
        static string Leer(string ruta) => File.ReadAllText(ruta);

        [Test]
        public void DatosRealesDePergaminoCorrenUnAnio()
        {
            const string R = "Assets/Data/Regions/pergamino/";
            var d = DatosJuego.Desde(new ArchivosDatos
            {
                Crops = Leer("Assets/Data/crops.json"), Pests = Leer("Assets/Data/pests.json"),
                Economy = Leer("Assets/Data/economy.json"), Soils = Leer("Assets/Data/soils.json"),
                Region = Leer(R + "region.json"), Lotes = Leer(R + "lotes.json"), Clima = Leer(R + "clima.json"),
            });
            Assert.That(d.Lotes.Count, Is.InRange(3000, 5000));
            var s = Simulation.Nueva(d, 1);
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(1, 6, 2));
            foreach (var c in new[] { "trigo", "maiz", "soja1", "soja2" })
            {
                double r = s.PromedioZonaQqHa(1, c);
                TestContext.WriteLine($"{c}: {r:F1} qq/ha");
                Assert.Greater(r, 0, c);
            }
        }
    }
}
