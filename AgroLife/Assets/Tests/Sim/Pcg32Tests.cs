using System;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class Pcg32Tests
    {
        [Test]
        public void ReproduceElVectorDeReferencia()
        {
            // pcg32-demo de O'Neill: semilla 42, secuencia 54
            var r = new Pcg32(42, 54);
            foreach (var e in new uint[] { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e })
                Assert.AreEqual(e, r.NextUInt());
        }

        [Test]
        public void FlujosDistintosDanSecuenciasDistintas() =>
            Assert.AreNotEqual(new Pcg32(7, (ulong)Flujo.Clima).NextUInt(), new Pcg32(7, (ulong)Flujo.Precios).NextUInt());

        [Test]
        public void GaussianaTieneMedia0YDesvio1()
        {
            var r = new Pcg32(1, 1);
            const int n = 200_000;
            double suma = 0, suma2 = 0;
            for (int i = 0; i < n; i++) { double x = r.NextGaussian(); suma += x; suma2 += x * x; }
            double media = suma / n, desvio = Math.Sqrt(suma2 / n - media * media);
            Assert.Less(Math.Abs(media), 0.01);
            Assert.Less(Math.Abs(desvio - 1), 0.01);
        }

        [Test]
        public void GammaTieneMediaYVarianzaCorrectas()
        {
            var (media, varianza) = Momentos(new Pcg32(1, 2), 0.75, 14);
            Assert.AreEqual(10.5, media, 10.5 * 0.01);
            Assert.AreEqual(147, varianza, 147 * 0.03);
            var (media2, _) = Momentos(new Pcg32(1, 3), 2.5, 3);
            Assert.AreEqual(7.5, media2, 7.5 * 0.01);
        }

        [Test]
        public void SobreviveAGuardarYCargar()
        {
            var r = new Pcg32(42, 54);
            r.NextUInt(); r.NextUInt(); r.NextUInt();
            var copia = JsonSim.Deserializar<Pcg32>(JsonSim.Serializar(r));
            for (int i = 0; i < 3; i++) Assert.AreEqual(r.NextUInt(), copia.NextUInt());
        }

        static (double media, double varianza) Momentos(Pcg32 r, double forma, double escala)
        {
            const int n = 200_000;
            double suma = 0, suma2 = 0;
            for (int i = 0; i < n; i++) { double x = r.NextGamma(forma, escala); suma += x; suma2 += x * x; }
            double media = suma / n;
            return (media, suma2 / n - media * media);
        }
    }
}
