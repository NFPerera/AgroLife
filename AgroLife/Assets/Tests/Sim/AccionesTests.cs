using System.Linq;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class AccionesTests
    {
        static PlanCultivo Maiz(Fecha f) => new PlanCultivo { CultivoId = "maiz", FechaSiembra = f, Genetica = Genetica.Largo, NKgHa = 120, PKgHa = 20 };

        [Test]
        public void ArrendarCobraQuintalesDeSojaAlPrecioDelDia()
        {
            var s = DatosPrueba.Nueva();
            double saldo = s.Estado.SaldoUsd, precio = s.Estado.Precios.PrecioUsdT[Grano.Soja];
            Assert.IsTrue(s.ArrendarLote(1).Ok);
            Assert.AreEqual(saldo - 17 * 0.9 * 100 * precio / 10, s.Estado.SaldoUsd, 1e-6);
            Assert.AreEqual(Tenencia.Arrendado, s.Estado.Lotes[0].Tenencia);
            Assert.AreEqual(Fecha.Crear(30, 4, 2), s.Estado.Lotes[0].Contrato.Vence);
            Assert.IsNull(s.Estado.Lotes[0].Plan);
        }

        [Test]
        public void ArrendarOComprarLoteOcupadoSeRechaza()
        {
            var s = DatosPrueba.Nueva();
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(1, 7, 1));
            s.Estado.SaldoUsd = 5_000_000;
            Assert.AreEqual("El lote 1 está ocupado con trigo hasta la cosecha", s.ArrendarLote(1).Motivo);
            Assert.AreEqual("El lote 1 está ocupado con trigo hasta la cosecha", s.ComprarLote(1).Motivo);
            Assert.AreEqual("trigo", s.Estado.Lotes[0].Cultivo.CultivoId);
        }

        [Test]
        public void ArrendarSinSaldoSeRechaza()
        {
            var s = DatosPrueba.Nueva();
            s.Estado.SaldoUsd = -49000;
            StringAssert.StartsWith("Saldo insuficiente: hacen falta US$ ", s.ArrendarLote(1).Motivo);
        }

        [Test]
        public void ComprarCobraPrecioSegunIp()
        {
            var s = DatosPrueba.Nueva();
            s.Estado.SaldoUsd = 2_000_000;
            Assert.IsTrue(s.ComprarLote(1).Ok);
            Assert.AreEqual(740_000, s.Estado.SaldoUsd, 1e-6);
            Assert.AreEqual(Tenencia.Propio, s.Estado.Lotes[0].Tenencia);
        }

        [Test]
        public void PlanificarFueraDeVentanaSeRechazaConMotivo()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(2);
            Assert.AreEqual("Fuera de la ventana de siembra de maíz (15/9–31/12)", s.PlanificarCultivo(2, Maiz(Fecha.Crear(1, 9, 1))).Motivo);
        }

        [Test]
        public void PlanificarEnLoteAjenoSeRechaza() =>
            Assert.AreEqual("El lote 3 no es tuyo", DatosPrueba.Nueva().PlanificarCultivo(3, Maiz(Fecha.Crear(1, 10, 1))).Motivo);

        [Test]
        public void PlanificarSinSaldoSeRechaza()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(2);
            s.Estado.SaldoUsd = -20000;
            StringAssert.StartsWith("Saldo insuficiente", s.PlanificarCultivo(2, Maiz(Fecha.Crear(1, 10, 1))).Motivo);
        }

        [Test]
        public void SiembraCobraYCosechaDejaResultadoConsistente()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(2);
            Assert.IsTrue(s.PlanificarCultivo(2, Maiz(Fecha.Crear(1, 10, 1))).Ok);
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(30, 9, 1));
            double antes = s.Estado.SaldoUsd;
            s.StepDay();
            Assert.AreEqual(antes - 46_560, s.Estado.SaldoUsd, 1e-6); // 80 ha · 582

            DatosPrueba.AvanzarHasta(s, Fecha.Crear(1, 6, 2));
            var r = s.Estado.Resultados.Single();
            var k = r.Cascada;
            Assert.AreEqual(k.PotencialQqHa, k.FechaQq + k.AguaQq + k.AnegamientoQq + k.TemperaturaQq + k.NitrogenoQq + k.FosforoQq + k.PlagasQq + k.RealQqHa, 1e-6);
            Assert.AreEqual(k.RealQqHa * 8, r.ToneladasCosechadas, 1e-6);
            Assert.AreEqual(r.Margen.IngresoBrutoUsd - r.Margen.ComercializacionUsd - r.Margen.CostosDirectosUsd - r.Margen.CosechaUsd, r.Margen.MargenBrutoUsd, 1e-6);

            var rep = s.ReporteLote(2, 1);
            Assert.AreEqual(rep.MargenBrutoUsd - rep.ArrendamientoUsd, rep.ResultadoUsd, 1e-6);
            Assert.Greater(rep.PromedioZonaQqHa["maiz"], 0);
        }

        [Test]
        public void DobleCultivoSiembraSoja2AlDiaSiguienteDeLaCosecha()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(1);
            Assert.IsTrue(s.PlanificarCultivo(1, new PlanCultivo
            {
                CultivoId = "trigo", FechaSiembra = Fecha.Crear(15, 6, 1), Genetica = Genetica.Largo, NKgHa = 90, PKgHa = 15,
                Soja2 = new PlanCultivo { CultivoId = "soja2", Genetica = Genetica.Largo, PKgHa = 10 },
            }).Ok);
            for (int i = 0; i < 400 && s.Estado.Lotes[0].Cultivo?.CultivoId != "soja2"; i++) s.StepDay();
            var soja = s.Estado.Lotes[0].Cultivo;
            Assert.AreEqual("soja2", soja?.CultivoId);
            var trigo = s.Estado.Resultados.Single(x => x.CultivoId == "trigo");
            Assert.AreEqual(trigo.FechaCosecha.MasDias(1), soja.FechaSiembra);
        }

        [Test]
        public void PresupuestoDeMaiz()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(2);
            var p = s.CalcularPresupuesto(2, Maiz(Fecha.Crear(1, 10, 1)));
            var c = p.Cultivos.Single();
            Assert.AreEqual(46_560, p.CostoTotalUsd, 1e-6);
            double neto = Economia.PrecioNetoUsdT(s.Estado.Precios.PrecioUsdT[Grano.Maiz], s.Datos.Economia, 25);
            Assert.AreEqual(Economia.RindeIndiferenciaQqHa(582, neto, 0.08), c.RindeIndiferenciaQqHa, 1e-9);
            Assert.That(c.RindeEsperadoQqHa, Is.GreaterThan(0).And.LessThan(135 * (0.3 + 0.7 * 0.8)));
        }

        [Test]
        public void PlanesComprometidosCuentanEnElSaldo()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(2);
            s.ArrendarLote(4);
            s.Estado.SaldoUsd = 50_000; // fondos 100.000
            Assert.IsTrue(s.PlanificarCultivo(2, Maiz(Fecha.Crear(1, 10, 1))).Ok);  // 80 ha · 582 = 46.560
            Assert.AreEqual(46_560, s.CostoComprometidoUsd(), 1e-6);
            StringAssert.StartsWith("Saldo insuficiente", s.PlanificarCultivo(4, Maiz(Fecha.Crear(1, 10, 1))).Motivo); // + 120 ha · 582
        }

        [Test]
        public void ElPlanGuardadoNoCambiaSiElQueLlamaLoEdita()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(2);
            var plan = Maiz(Fecha.Crear(1, 10, 1));
            Assert.IsTrue(s.PlanificarCultivo(2, plan).Ok);
            plan.FechaSiembra = Fecha.Crear(1, 9, 1);
            plan.NKgHa = 999;
            Assert.AreEqual(Fecha.Crear(1, 10, 1), s.Estado.Lotes[1].Plan.FechaSiembra);
            Assert.AreEqual(120, s.Estado.Lotes[1].Plan.NKgHa);
        }

        [Test]
        public void ArrendarEl30DeAbrilCuentaParaLaCampaniaSiguiente()
        {
            var s = DatosPrueba.Nueva();
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(30, 4, 2));
            Assert.IsTrue(s.ArrendarLote(2).Ok);
            var c = s.Estado.Lotes[1].Contrato;
            Assert.AreEqual(2, c.Campania);
            Assert.AreEqual(Fecha.Crear(30, 4, 3), c.Vence);
        }

        [Test]
        public void VencimientoArrendamientoSigueLaRegla()
        {
            var s = DatosPrueba.Nueva();
            Assert.AreEqual(Fecha.Crear(30, 4, 2), s.VencimientoArrendamiento());
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(30, 4, 2));
            Assert.AreEqual(Fecha.Crear(30, 4, 3), s.VencimientoArrendamiento());
        }

        [Test]
        public void CancelarPlanAntesDeSembrar()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(2);
            s.PlanificarCultivo(2, Maiz(Fecha.Crear(1, 10, 1)));
            double saldo = s.Estado.SaldoUsd;
            Assert.IsTrue(s.CancelarPlan(2).Ok);
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(2, 10, 1));
            Assert.IsNull(s.Estado.Lotes[1].Cultivo);
            Assert.AreEqual(saldo, s.Estado.SaldoUsd, 1e-6);
        }

        [Test]
        public void ArrendamientoVencidoConCultivoSeExtiendeHastaLaCosecha()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(2);
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(28, 4, 2));
            var lote = s.Estado.Lotes[1];
            lote.Cultivo = ModeloCultivo.Sembrar(s.Datos.Cultivos["maiz"], Genetica.Largo, s.Estado.Fecha, 0, 0, s.Datos.Lote(2), lote.PBrayPpm, s.Datos.Suelo);

            var eventos = DatosPrueba.AvanzarHasta(s, Fecha.Crear(2, 5, 2));
            Assert.AreEqual(Tenencia.Arrendado, lote.Tenencia);
            Assert.IsTrue(eventos.Any(e => e.Tipo == TipoEvento.VencimientoArrendamiento && e.LoteId == 2 && e.Mensaje.Contains("sigue hasta la cosecha")));

            lote.Cultivo.GradosDia = 5000;
            lote.Cultivo.DiasEnMadurez = 100;
            s.StepDay();
            Assert.IsTrue(s.Estado.Resultados.Any(r => r.LoteId == 2));
            Assert.AreEqual(Tenencia.Tercero, lote.Tenencia);
        }

        [Test]
        public void Soja2SeCancelaSiElTrigoSeCosechaDespuesDel15DeEnero()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(1);
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(14, 1, 2));
            var lote = s.Estado.Lotes[0];
            lote.Cultivo = ModeloCultivo.Sembrar(s.Datos.Cultivos["trigo"], Genetica.Largo, s.Estado.Fecha, 0, 0, s.Datos.Lote(1), lote.PBrayPpm, s.Datos.Suelo);
            lote.Cultivo.SegundoCultivo = new PlanCultivo { CultivoId = "soja2", Genetica = Genetica.Largo, PKgHa = 10 };
            lote.Cultivo.GradosDia = 5000;
            lote.Cultivo.DiasEnMadurez = 100;

            var eventos = s.StepDay(); // cosecha el 15/1: el 16/1 queda fuera de la ventana
            Assert.IsTrue(eventos.Any(e => e.Tipo == TipoEvento.SiembraCancelada && e.LoteId == 1));
            Assert.IsNull(lote.Plan);
            double saldo = s.Estado.SaldoUsd;
            s.StepDay();
            Assert.AreEqual(saldo, s.Estado.SaldoUsd, 1e-6);
            Assert.IsNull(lote.Cultivo);
        }
    }
}
