using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class FechaTests
    {
        [Test]
        public void ElUnoDeMayoDelAnio1EsElDia120()
        {
            var f = Fecha.Crear(1, 5, 1);
            Assert.AreEqual(120, f.Absoluto);
            Assert.AreEqual("1/5 Año 1", f.ToString());
            Assert.AreEqual(1, f.Campania);
            Assert.AreEqual(0, f.DiaDeCampania);
        }

        [Test]
        public void FinDeAnioYFebreroSinBisiesto()
        {
            Assert.AreEqual(Fecha.Crear(1, 1, 2), Fecha.Crear(31, 12, 1).MasDias(1));
            Assert.AreEqual(Fecha.Crear(1, 3, 1), Fecha.Crear(28, 2, 1).MasDias(1));
        }

        [Test]
        public void EneroPerteneceALaCampaniaAnterior()
        {
            Assert.AreEqual(1, Fecha.Crear(15, 1, 2).Campania);
            Assert.AreEqual(2, Fecha.Crear(1, 5, 2).Campania);
        }

        [Test]
        public void DiaDeCampania()
        {
            Assert.AreEqual(259, Fecha.DiaDeCampaniaDe(15, 1));
            Assert.AreEqual(364, Fecha.DiaDeCampaniaDe(30, 4));
            Assert.AreEqual(137, Fecha.DiaDeCampaniaDe(15, 9));
            Assert.AreEqual(Fecha.Crear(15, 1, 3), Fecha.EnCampania(2, 15, 1));
        }

        [Test]
        public void ParseaDiaMes() => Assert.AreEqual((15, 9), Fecha.ParseDiaMes("15/9"));

        [Test]
        public void FormatoRioplatense()
        {
            Assert.AreEqual("1.234.567,89", Formato.Numero(1234567.891, 2));
            Assert.AreEqual("US$ -4.500", Formato.Usd(-4500));
        }
    }
}
