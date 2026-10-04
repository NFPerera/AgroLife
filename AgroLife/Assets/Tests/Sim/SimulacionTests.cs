using System.Linq;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class SimulacionTests
    {
        [Test]
        public void NuevaArrancaEl1DeMayoConElCapitalInicial()
        {
            var s = DatosPrueba.Nueva();
            Assert.AreEqual(Fecha.Crear(1, 5, 1), s.Estado.Fecha);
            Assert.AreEqual(173325, s.Estado.SaldoUsd, 0.01); // 250 · (17·0.78·300/10 + 295.5)
            var e = s.EventosIniciales.Single(x => x.Tipo == TipoEvento.InicioCampania);
            Assert.IsTrue(e.Pausa);
            StringAssert.StartsWith("Inicio de la campaña 1. Pronóstico:", e.Mensaje);
        }

        [Test]
        public void LotesArrancanConAguaNitrogenoFosforoYRotacion()
        {
            var s = DatosPrueba.Nueva();
            var l = s.Estado.Lotes;
            var hoy = s.Estado.Clima.Hoy; // Nueva ya simuló el 1/5: el agua inicial (300·0.6) cambió un día
            Assert.That(l[0].AguaMm, Is.InRange(180 - hoy.Et0Mm, 180 + hoy.LluviaMm));
            Assert.That(l[0].NMineralKgHa, Is.InRange(39, 41));
            Assert.GreaterOrEqual(l[0].PBrayPpm, 4);
            Assert.AreEqual(Tenencia.Tercero, l[0].Tenencia);
            Assert.AreEqual("soja1", l[0].CultivoAnterior);
            Assert.AreEqual("soja2", l[1].CultivoAnterior);
            Assert.AreEqual("maiz", l[2].CultivoAnterior);
        }

        [Test]
        public void TercerosPlanificanSegunLaRotacion()
        {
            var l = DatosPrueba.Nueva().Estado.Lotes;
            Assert.AreEqual("trigo", l[0].Plan.CultivoId);
            Assert.IsNotNull(l[0].Plan.Soja2);
            Assert.AreEqual(Fecha.Crear(15, 6, 1), l[0].Plan.FechaSiembra);
            Assert.AreEqual("maiz", l[1].Plan.CultivoId);
            Assert.AreEqual(Fecha.Crear(1, 10, 1), l[1].Plan.FechaSiembra);
            Assert.AreEqual("soja1", l[2].Plan.CultivoId);
        }

        [Test]
        public void StepDayAvanzaUnDiaConClimaYPrecios()
        {
            var s = DatosPrueba.Nueva();
            s.StepDay();
            Assert.AreEqual(Fecha.Crear(2, 5, 1), s.Estado.Fecha);
            Assert.Greater(s.Estado.Clima.Hoy.Et0Mm, 0);
            foreach (var g in new[] { Grano.Trigo, Grano.Maiz, Grano.Soja })
                Assert.Greater(s.Estado.Precios.PrecioUsdT[g], 0);
        }

        [Test]
        public void TercerosCosechanTodaLaRotacionConRindesCreibles()
        {
            var s = DatosPrueba.Nueva();
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(1, 6, 2));
            foreach (var (cultivo, rpot) in new[] { ("trigo", 62.0), ("maiz", 135.0), ("soja1", 50.0), ("soja2", 40.0) })
            {
                double r = s.PromedioZonaQqHa(1, cultivo);
                Assert.That(r, Is.InRange(5, rpot), cultivo);
                TestContext.WriteLine($"{cultivo}: {r:F1} qq/ha");
            }
        }

        [Test]
        public void RindeEstimadoDuranteElCultivo()
        {
            var s = DatosPrueba.Nueva();
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(1, 12, 1));
            Assert.That(s.RindeEstimadoQqHa(2), Is.GreaterThan(0).And.LessThanOrEqualTo(135 * (0.3 + 0.7 * 0.8) + 1e-9));
        }
    }
}
