using System;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class SueloTests
    {
        [Test]
        public void BalanceDeAguaCierra()
        {
            var d = DatosPrueba.Cargar();
            foreach (var lote in d.Lotes)
            {
                var rng = new Pcg32(11, (ulong)Flujo.Clima);
                double agua = lote.AuMaxMm * 0.6, inicial = agua, lluvias = 0, et = 0, dren = 0, esc = 0;
                for (int i = 0; i < 730; i++)
                {
                    double lluvia = rng.Bernoulli(0.25) ? rng.NextGamma(0.8, 18) : 0;
                    var f = BalanceAgua.PasoDiario(ref agua, lote, d.Suelo, lluvia, rng.Uniforme(1, 7), rng.Uniforme(0.25, 1.2));
                    lluvias += lluvia; et += f.EtReal; dren += f.Drenaje; esc += f.Escurrimiento;
                }
                Assert.AreEqual(lluvias, agua - inicial + et + dren + esc, 1e-6, $"lote {lote.Id}");
            }
        }

        [Test]
        public void ArenosoDrenaYArcillosoEscurre()
        {
            var (fa, fc) = LluviaDe100();
            Assert.AreEqual(100, fa.Drenaje, 1e-6);
            Assert.AreEqual(44.7562, fc.Escurrimiento, 0.001);
            Assert.AreEqual(36.8292, fc.Drenaje, 0.001);
            Assert.IsTrue(fc.Anegado);
            Assert.IsFalse(fa.Anegado);
        }

        [Test]
        public void ArenosoLavaMasNitrogeno()
        {
            var (fa, fc) = LluviaDe100();
            Assert.AreEqual(14.7059, Nitrogeno.Lavado(50, fa.Drenaje, fa.AguaAntesDeDrenarMm, 90), 0.001);
            Assert.AreEqual(2.8103, Nitrogeno.Lavado(50, fc.Drenaje, fc.AguaAntesDeDrenarMm, 330), 0.001);
        }

        [Test]
        public void SueloSecoReduceElConsumo()
        {
            var d = DatosPrueba.Cargar();
            var lote = d.Lote(1);
            double agua = 0.25 * lote.AuMaxMm;
            Assert.AreEqual(2.5, BalanceAgua.PasoDiario(ref agua, lote, d.Suelo, 0, 5, 1).EtReal, 1e-9);
        }

        [Test]
        public void MineralizacionSegunTemperaturaYHumedad()
        {
            var m = DatosPrueba.Cargar().Suelo;
            Assert.AreEqual(0.36, Nitrogeno.Mineralizacion(2, 20, 300, 300, m), 1e-9);
            Assert.AreEqual(0.18, Nitrogeno.Mineralizacion(2, 10, 300, 300, m), 1e-9);
            Assert.AreEqual(0, Nitrogeno.Mineralizacion(2, 0, 300, 300, m), 1e-9);
            Assert.AreEqual(0.18, Nitrogeno.Mineralizacion(2, 20, 150, 300, m), 1e-9);
        }

        [Test]
        public void FosforoRespuestaYBalance()
        {
            var f = new FosforoParams { PCriticoPpm = 18, MinRelativo = 0.7, ExportacionKgPorQq = 0.55 };
            var m = DatosPrueba.Cargar().Suelo;
            Assert.AreEqual(0.1, Fosforo.Perdida(9, 10, f, m), 1e-9);
            Assert.AreEqual(0, Fosforo.Perdida(20, 0, f, m), 1e-9);
            Assert.AreEqual(13.4, Fosforo.Actualizar(14, 20, 40, f, m), 1e-9);
            Assert.AreEqual(3, Fosforo.Actualizar(3.2, 0, 50, f, m), 1e-9);
        }

        static (FlujosAgua arenoso, FlujosAgua arcilloso) LluviaDe100()
        {
            var d = DatosPrueba.Cargar();
            LoteDatos arenoso = d.Lote(5), arcilloso = d.Lote(4);
            double a = arenoso.AuMaxMm, c = arcilloso.AuMaxMm;
            return (BalanceAgua.PasoDiario(ref a, arenoso, d.Suelo, 100, 0, 1),
                    BalanceAgua.PasoDiario(ref c, arcilloso, d.Suelo, 100, 0, 1));
        }
    }
}
