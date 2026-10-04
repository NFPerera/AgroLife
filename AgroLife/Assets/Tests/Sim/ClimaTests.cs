using System;
using System.Linq;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class ClimaTests
    {
        [Test]
        public void Hargreaves15DeEnero() => Assert.AreEqual(6.0550, Hargreaves.Et0(30, 17, 14, -33.89), 0.001);

        [Test]
        public void Hargreaves15DeJulio() => Assert.AreEqual(1.4269, Hargreaves.Et0(14, 3, 195, -33.89), 0.001);

        [Test]
        public void LluviaYTemperaturaCoincidenConLosParametros()
        {
            var d = DatosPrueba.Cargar();
            var e = new EstadoClima();
            var rng = new Pcg32(99, (ulong)Flujo.Clima);
            const int anios = 1000;
            var lluvia = new double[12]; var tmax = new double[12]; var tmin = new double[12]; var dias = new int[12];
            for (int i = 0; i < 365 * anios; i++)
            {
                var f = new Fecha(120 + i);
                var dia = GeneradorClima.GenerarDia(e, d.Clima, d.Region.Latitud, f, rng);
                int m = f.Mes - 1;
                lluvia[m] += dia.LluviaMm; tmax[m] += dia.TmaxC; tmin[m] += dia.TminC; dias[m]++;
            }
            for (int m = 0; m < 12; m++)
            {
                var p = d.Clima.Meses[m];
                double pi = p.PSecoAHumedo / (1 - p.PHumedoAHumedo + p.PSecoAHumedo);
                double lluviaEsperada = pi * Fecha.DiasDelMes(m + 1) * p.GammaForma * p.GammaEscala;
                Assert.AreEqual(lluviaEsperada, lluvia[m] / anios, lluviaEsperada * 0.07, $"lluvia mes {m + 1}");
                Assert.AreEqual(pi * p.TmaxHumedo.Media + (1 - pi) * p.TmaxSeco.Media, tmax[m] / dias[m], 0.3, $"tmax mes {m + 1}");
                Assert.AreEqual(pi * p.TminHumedo.Media + (1 - pi) * p.TminSeco.Media, tmin[m] / dias[m], 0.3, $"tmin mes {m + 1}");
            }
        }

        [Test]
        public void TminSiempreQuedaUnGradoDebajoDeTmax()
        {
            var d = DatosPrueba.Cargar();
            var e = new EstadoClima();
            var rng = new Pcg32(7, (ulong)Flujo.Clima);
            for (int i = 0; i < 365 * 100; i++)
            {
                var dia = GeneradorClima.GenerarDia(e, d.Clima, d.Region.Latitud, new Fecha(120 + i), rng);
                Assert.LessOrEqual(dia.TminC, dia.TmaxC - 1 + 1e-9);
            }
        }

        [Test]
        public void MismaSemillaMismoClima()
        {
            var d = DatosPrueba.Cargar();
            EstadoClima ea = new EstadoClima(), eb = new EstadoClima();
            Pcg32 ra = new Pcg32(5, (ulong)Flujo.Clima), rb = new Pcg32(5, (ulong)Flujo.Clima);
            for (int i = 0; i < 730; i++)
            {
                var f = new Fecha(120 + i);
                var a = GeneradorClima.GenerarDia(ea, d.Clima, d.Region.Latitud, f, ra);
                var b = GeneradorClima.GenerarDia(eb, d.Clima, d.Region.Latitud, f, rb);
                Assert.AreEqual(a.LluviaMm, b.LluviaMm); Assert.AreEqual(a.TmaxC, b.TmaxC); Assert.AreEqual(a.TminC, b.TminC);
                Assert.AreEqual(a.RadMJ, b.RadMJ); Assert.AreEqual(a.Et0Mm, b.Et0Mm);
            }
        }

        [Test]
        public void NinoLlueveMasQueNinaEnVerano() =>
            Assert.Greater(LluviaVerano(FaseEnso.Nino), LluviaVerano(FaseEnso.Nina));

        [Test]
        public void FaseYPronosticoSiguenLasFrecuencias()
        {
            var d = DatosPrueba.Cargar();
            var e = new EstadoClima();
            var rng = new Pcg32(3, (ulong)Flujo.Clima);
            const int n = 5000;
            int aciertos = 0;
            var conteo = new int[3];
            for (int i = 0; i < n; i++)
            {
                GeneradorClima.SortearFase(e, d.Clima, rng);
                conteo[(int)e.Fase]++;
                var favorecida = e.Pronostico.OrderByDescending(kv => kv.Value).First().Key;
                if (favorecida == e.Fase) aciertos++;
                var valores = e.Pronostico.Values.OrderByDescending(v => v).ToArray();
                Assert.AreEqual(new[] { 0.70, 0.15, 0.15 }, valores);
                Assert.AreEqual(1, valores.Sum(), 1e-9);
            }
            foreach (FaseEnso f in Enum.GetValues(typeof(FaseEnso)))
                Assert.AreEqual(d.Clima.Enso.Frecuencias[f], conteo[(int)f] / (double)n, 0.02, f.ToString());
            Assert.AreEqual(0.70, aciertos / (double)n, 0.02);
        }

        static double LluviaVerano(FaseEnso fase)
        {
            var d = DatosPrueba.Cargar();
            var e = new EstadoClima { Fase = fase };
            var rng = new Pcg32(21, (ulong)Flujo.Clima);
            double total = 0;
            for (int i = 0; i < 365 * 1000; i++)
            {
                var f = new Fecha(120 + i);
                var dia = GeneradorClima.GenerarDia(e, d.Clima, d.Region.Latitud, f, rng);
                if (f.Mes == 12 || f.Mes <= 2) total += dia.LluviaMm;
            }
            return total;
        }
    }
}
