using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class TexturaTests
    {
        [TestCase(92, 5, 3, ClaseTextural.Arenoso)]
        [TestCase(82, 12, 6, ClaseTextural.ArenosoFranco)]
        [TestCase(65, 25, 10, ClaseTextural.FrancoArenoso)]
        [TestCase(40, 40, 20, ClaseTextural.Franco)]
        [TestCase(20, 65, 15, ClaseTextural.FrancoLimoso)]
        [TestCase(5, 88, 7, ClaseTextural.Limoso)]
        [TestCase(60, 13, 27, ClaseTextural.FrancoArcillosoArenoso)]
        [TestCase(35, 33, 32, ClaseTextural.FrancoArcilloso)]
        [TestCase(10, 58, 32, ClaseTextural.FrancoArcillosoLimoso)]
        [TestCase(50, 10, 40, ClaseTextural.ArcillosoArenoso)]
        [TestCase(8, 47, 45, ClaseTextural.ArcillosoLimoso)]
        [TestCase(20, 20, 60, ClaseTextural.Arcilloso)]
        public void ClasificaSegunUsda(double a, double l, double c, ClaseTextural esperada) =>
            Assert.AreEqual(esperada, Textura.Clasificar(a, l, c));

        [Test]
        public void SaxtonRawlsFrancoLimoso()
        {
            Assert.AreEqual(19.9009, Textura.KsatMmH(20, 15, 1.8 * 1.724), 0.001);
            Assert.AreEqual(281.555, Textura.DrenableMm(20, 15, 1.8 * 1.724), 0.01);
        }

        [Test]
        public void SaxtonRawlsArenosoDrenaMasQueArcilloso()
        {
            Assert.AreEqual(84.4856, Textura.KsatMmH(82, 6, 0.6 * 1.724), 0.001);
            Assert.AreEqual(9.2073, Textura.KsatMmH(10, 32, 2.0 * 1.724), 0.001);
        }
    }
}
