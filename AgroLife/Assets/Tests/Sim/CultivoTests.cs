using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class CultivoTests
    {
        DatosJuego d;
        [SetUp] public void Cargar() => d = DatosPrueba.Cargar();

        static DiaClima Clima(double tmax, double tmin) => new DiaClima { TmaxC = tmax, TminC = tmin };

        [Test]
        public void EtapasLleganElDiaEsperadoConTemperaturaFija()
        {
            var c = d.Cultivos["maiz"];
            c.TemperaturaBase = 10;
            c.DiasACosecha = 7;
            c.Geneticas[Genetica.Largo].Gd = new UmbralesGd { Emergencia = 50, Vegetativo = 100, Floracion = 300, Llenado = 400, Madurez = 600 };
            var e = ModeloCultivo.Sembrar(c, Genetica.Largo, Fecha.Crear(1, 10, 1), 0, 0, d.Lote(1), 20, d.Suelo);

            var primeraVez = new System.Collections.Generic.Dictionary<Etapa, int>();
            int listo = 0;
            double n = 0;
            for (int llamada = 1; llamada <= 80; llamada++)
            {
                ModeloCultivo.AvanzarDia(e, c, Clima(30, 10), new FlujosAgua(), ref n);
                if (!primeraVez.ContainsKey(e.Etapa)) primeraVez[e.Etapa] = llamada;
                if (listo == 0 && ModeloCultivo.ListoParaCosechar(e, c)) listo = llamada;
            }
            Assert.AreEqual(5, primeraVez[Etapa.Emergencia]);
            Assert.AreEqual(10, primeraVez[Etapa.Vegetativo]);
            Assert.AreEqual(30, primeraVez[Etapa.Floracion]);
            Assert.AreEqual(40, primeraVez[Etapa.Llenado]);
            Assert.AreEqual(60, primeraVez[Etapa.Madurez]);
            Assert.AreEqual(66, listo);
        }

        [Test]
        public void SojaTardiaAcortaElCiclo()
        {
            var soja2 = d.Cultivos["soja2"];
            var e = ModeloCultivo.Sembrar(soja2, Genetica.Largo, Fecha.Crear(10, 12, 1), 0, 10, d.Lote(1), 14, d.Suelo);
            Assert.AreEqual(0.925, e.FactorFotoperiodo, 1e-9);
            Assert.AreEqual(462.5, ModeloCultivo.Umbrales(e, soja2).Floracion, 1e-9);
            Assert.AreEqual(80, ModeloCultivo.Umbrales(e, soja2).Emergencia, 1e-9);
            Assert.AreEqual(1, ModeloCultivo.Sembrar(d.Cultivos["soja1"], Genetica.Largo, Fecha.Crear(1, 11, 1), 0, 10, d.Lote(1), 14, d.Suelo).FactorFotoperiodo, 1e-9);
        }

        [Test]
        public void PerdidaPorFechaInterpola()
        {
            var maiz = d.Cultivos["maiz"];
            Assert.AreEqual(0, ModeloCultivo.PerdidaPorFecha(maiz, Fecha.DiaDeCampaniaDe(1, 10)), 1e-9);
            Assert.AreEqual(0.04, ModeloCultivo.PerdidaPorFecha(maiz, Fecha.DiaDeCampaniaDe(10, 9)), 1e-9);
            Assert.AreEqual(0.05, ModeloCultivo.PerdidaPorFecha(maiz, 185), 1e-9);
        }

        [Test]
        public void EstresHidricoPesaSegunLaEtapa()
        {
            var maiz = d.Cultivos["maiz"];
            var agua = new FlujosAgua { EtPotencial = 4, EtReal = 2 };
            double n = 0;
            var e = ModeloCultivo.Sembrar(maiz, Genetica.Largo, Fecha.Crear(1, 10, 1), 0, 0, d.Lote(1), 20, d.Suelo);
            e.GradosDia = 800;
            ModeloCultivo.AvanzarDia(e, maiz, Clima(5, 5), agua, ref n);
            Assert.AreEqual(0.0125, e.SumaEstresAgua, 1e-12);

            var v = ModeloCultivo.Sembrar(maiz, Genetica.Largo, Fecha.Crear(1, 10, 1), 0, 0, d.Lote(1), 20, d.Suelo);
            v.GradosDia = 300;
            ModeloCultivo.AvanzarDia(v, maiz, Clima(5, 5), agua, ref n);
            Assert.AreEqual(0.0015, v.SumaEstresAgua, 1e-12);
        }

        [Test]
        public void HeladaEnFloracionDeTrigo()
        {
            var trigo = d.Cultivos["trigo"];
            var e = ModeloCultivo.Sembrar(trigo, Genetica.Largo, Fecha.Crear(15, 6, 1), 0, 0, d.Lote(1), 20, d.Suelo);
            e.GradosDia = 1550;
            double n = 0;
            ModeloCultivo.AvanzarDia(e, trigo, Clima(10, -3), new FlujosAgua(), ref n);
            Assert.AreEqual(0.08, e.LTemp, 1e-12);
        }

        [Test]
        public void NitrogenoSeAbsorbeHastaLaDemanda()
        {
            var maiz = d.Cultivos["maiz"];
            var e = ModeloCultivo.Sembrar(maiz, Genetica.Largo, Fecha.Crear(1, 10, 1), 0, 0, d.Lote(1), 20, d.Suelo);
            e.GradosDia = 120;
            e.NDemandaTotalKg = 178;
            double n = 0.4;
            ModeloCultivo.AvanzarDia(e, maiz, Clima(28, 8), new FlujosAgua(), ref n);
            Assert.AreEqual(0.4, e.NAbsorbidoKg, 1e-9);
            Assert.AreEqual(0, n, 1e-9);
            Assert.AreEqual(1.0, e.NDemandaHastaHoyKg, 1e-9);
            n = 50;
            ModeloCultivo.AvanzarDia(e, maiz, Clima(28, 8), new FlujosAgua(), ref n);
            Assert.AreEqual(1.4, e.NAbsorbidoKg, 1e-9);
            Assert.AreEqual(49, n, 1e-9);
        }

        [Test]
        public void SojaNoTienePerdidaPorNitrogeno()
        {
            var e = new EstadoCultivo { CultivoId = "soja1", RindeAlcanzableQqHa = 50 };
            Assert.AreEqual(0, ModeloCultivo.PerdidasActuales(e, d.Cultivos["soja1"]).Nitrogeno);
        }

        [Test]
        public void CascadaRepartePerdidasEnOrden()
        {
            var e = new EstadoCultivo
            {
                CultivoId = "maiz", RindeAlcanzableQqHa = 100, LFecha = 0.1, SumaEstresAgua = 0.1, SumaAnegamiento = 0.1,
                LTemp = 0.1, NDemandaHastaHoyKg = 120, NAbsorbidoKg = 100, LP = 0.1, LPlagas = 0.1,
            };
            var k = ModeloCultivo.CalcularCascada(e, d.Cultivos["maiz"]);
            Assert.AreEqual(100, k.PotencialQqHa, 1e-9);
            Assert.AreEqual(10, k.FechaQq, 1e-9);
            Assert.AreEqual(9, k.AguaQq, 1e-9);
            Assert.AreEqual(8.1, k.AnegamientoQq, 1e-9);
            Assert.AreEqual(7.29, k.TemperaturaQq, 1e-9);
            Assert.AreEqual(6.561, k.NitrogenoQq, 1e-9);
            Assert.AreEqual(5.9049, k.FosforoQq, 1e-9);
            Assert.AreEqual(5.31441, k.PlagasQq, 1e-9);
            Assert.AreEqual(47.82969, k.RealQqHa, 1e-9);
        }
    }
}
