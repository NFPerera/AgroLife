using System.IO;
using System.Linq;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class DatosTests
    {
        [Test]
        public void DatosRealesSonValidos()
        {
            var a = DatosPrueba.Archivos();
            a.Crops = File.ReadAllText("Assets/Data/crops.json");
            a.Pests = File.ReadAllText("Assets/Data/pests.json");
            a.Economy = File.ReadAllText("Assets/Data/economy.json");
            a.Soils = File.ReadAllText("Assets/Data/soils.json");
            Assert.AreEqual(4, DatosJuego.Desde(a).Cultivos.Count);
        }

        [Test]
        public void CalculaDerivadosDelLote()
        {
            var l = DatosPrueba.Cargar().Lote(1);
            Assert.AreEqual(ClaseTextural.FrancoLimoso, l.Clase);
            Assert.AreEqual(300, l.AuMaxMm, 1e-9);
            Assert.AreEqual(19.9009, l.KsatMmH, 0.001);
        }

        [Test]
        public void TexturaQueNoSuma100SeRechaza()
        {
            var a = DatosPrueba.Archivos();
            a.Lotes = a.Lotes.Replace("\"arcilla\": 15,", "\"arcilla\": 30,");
            StringAssert.Contains("lotes.json: lote 1: arena + limo + arcilla = 115",
                Assert.Throws<DatosInvalidosException>(() => DatosJuego.Desde(a)).Message);
        }

        [Test]
        public void ClimaConOnceMesesSeRechaza()
        {
            var a = DatosPrueba.Archivos();
            var c = JsonSim.Deserializar<ClimaParams>(a.Clima);
            c.Meses.RemoveAt(11);
            a.Clima = JsonSim.Serializar(c);
            StringAssert.Contains("clima.json: se esperaban 12 meses y hay 11",
                Assert.Throws<DatosInvalidosException>(() => DatosJuego.Desde(a)).Message);
        }

        [Test]
        public void FaltaUnCultivoSeRechaza()
        {
            var a = DatosPrueba.Archivos();
            var c = JsonSim.Deserializar<CultivosArchivo>(a.Crops);
            c.Cultivos.RemoveAll(x => x.Id == "soja2");
            a.Crops = JsonSim.Serializar(c);
            StringAssert.Contains("crops.json: falta el cultivo soja2",
                Assert.Throws<DatosInvalidosException>(() => DatosJuego.Desde(a)).Message);
        }

        [Test]
        public void BloquesFaltantesYFechasMalEscritasSeRechazan()
        {
            var a = DatosPrueba.Archivos();
            var c = JsonSim.Deserializar<CultivosArchivo>(a.Crops);
            var trigo = c.Cultivos.Single(x => x.Id == "trigo");
            trigo.Helada = null;
            trigo.VentanaSiembra.Desde = "20-5";
            c.Cultivos.Single(x => x.Id == "maiz").Nitrogeno = null;
            a.Crops = JsonSim.Serializar(c);
            var r = JsonSim.Deserializar<RegionParams>(a.Region);
            r.FosforoBray = null;
            a.Region = JsonSim.Serializar(r);
            var cl = JsonSim.Deserializar<ClimaParams>(a.Clima);
            cl.Autocorrelacion = null;
            a.Clima = JsonSim.Serializar(cl);
            var p = JsonSim.Deserializar<PlagasArchivo>(a.Pests);
            p.Plagas.Single(x => x.Id == "chinches").Etapas.Add(Etapa.Madurez);
            a.Pests = JsonSim.Serializar(p);

            var msg = Assert.Throws<DatosInvalidosException>(() => DatosJuego.Desde(a)).Message;
            StringAssert.Contains("crops.json: trigo: falta helada", msg);
            StringAssert.Contains("crops.json: trigo: fecha mal escrita \"20-5\"", msg);
            StringAssert.Contains("crops.json: maiz: falta nitrogeno", msg);
            StringAssert.Contains("region.json: falta fosforoBray", msg);
            StringAssert.Contains("clima.json: falta autocorrelacion", msg);
            StringAssert.Contains("pests.json: chinches: no puede aparecer en Madurez", msg);
        }

        [Test]
        public void JsonMalFormadoNombraElArchivo()
        {
            var a = DatosPrueba.Archivos();
            a.Region = "{";
            StringAssert.StartsWith("Datos de región inválidos:\nregion.json:",
                Assert.Throws<DatosInvalidosException>(() => DatosJuego.Desde(a)).Message);
        }
    }
}
