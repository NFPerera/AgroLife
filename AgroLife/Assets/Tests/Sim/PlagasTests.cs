using System.Linq;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class PlagasTests
    {
        DatosJuego d;
        [SetUp] public void Cargar() => d = DatosPrueba.Cargar();

        PlagaParams Plaga(string id) => d.Plagas.Single(p => p.Id == id);
        static EstadoCultivo Cultivo(string id, Etapa etapa) => new EstadoCultivo { CultivoId = id, Etapa = etapa };
        static DiaClima Clima(double tmax, double tmin) => new DiaClima { TmaxC = tmax, TminC = tmin };

        [Test]
        public void SoloApareceEnSusCultivosYEtapasUnaVez()
        {
            var chinches = Plaga("chinches");
            chinches.ProbDiaria = 1;
            var rng = new Pcg32(1, (ulong)Flujo.Plagas);
            Assert.IsNull(Plagas.Sortear(Cultivo("soja1", Etapa.Vegetativo), chinches, Clima(30, 20), 10, rng));

            var soja = Cultivo("soja1", Etapa.Llenado);
            var sev = Plagas.Sortear(soja, chinches, Clima(30, 20), 10, rng);
            Assert.IsNotNull(sev);
            Assert.That(sev.Value, Is.InRange(0.04, 0.30));
            Assert.IsNull(Plagas.Sortear(soja, chinches, Clima(30, 20), 10, rng));

            Assert.IsNull(Plagas.Sortear(Cultivo("maiz", Etapa.Llenado), chinches, Clima(30, 20), 10, rng));
        }

        [Test]
        public void FrioImpideLaAparicion()
        {
            var chinches = Plaga("chinches");
            chinches.ProbDiaria = 1;
            Assert.IsNull(Plagas.Sortear(Cultivo("soja1", Etapa.Llenado), chinches, Clima(12, 8), 10, new Pcg32(1, (ulong)Flujo.Plagas)));
        }

        [Test]
        public void LluviaMultiplicaLaProbabilidad()
        {
            var roya = Plaga("roya");
            roya.ProbDiaria = 0.1;
            var rng = new Pcg32(2, (ulong)Flujo.Plagas);
            int conLluvia = 0, sinLluvia = 0;
            for (int i = 0; i < 20_000; i++)
            {
                if (Plagas.Sortear(Cultivo("trigo", Etapa.Floracion), roya, Clima(20, 10), 0, rng) != null) conLluvia++;
                if (Plagas.Sortear(Cultivo("trigo", Etapa.Floracion), roya, Clima(20, 10), 10, rng) != null) sinLluvia++;
            }
            Assert.That(conLluvia / (double)sinLluvia, Is.InRange(2.7, 3.3));
        }
    }
}
