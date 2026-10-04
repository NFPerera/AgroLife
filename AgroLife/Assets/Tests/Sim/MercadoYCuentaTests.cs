using System;
using System.Linq;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class MercadoYCuentaTests
    {
        static PlanCultivo Maiz(bool vender = true) => new PlanCultivo
            { CultivoId = "maiz", FechaSiembra = Fecha.Crear(1, 10, 1), Genetica = Genetica.Largo, NKgHa = 120, PKgHa = 20, VenderAlCosechar = vender };

        /// <summary>Partida con chinches seguras y fuertes en soja de primera sobre el lote 3 (60 ha); devuelve el evento de la plaga.</summary>
        static (Simulation s, Evento plaga) PlagaEnLote3()
        {
            var s = DatosPrueba.Nueva(plagas: true);
            foreach (var p in s.Datos.Plagas) p.ProbDiaria = p.Id == "chinches" ? 1 : 0;
            s.Datos.Plagas.Single(p => p.Id == "chinches").Severidad = new Rango { Min = 0.2, Max = 0.3 };
            Assert.IsTrue(s.ArrendarLote(3).Ok);
            Assert.IsTrue(s.PlanificarCultivo(3, new PlanCultivo { CultivoId = "soja1", FechaSiembra = Fecha.Crear(15, 11, 1), Genetica = Genetica.Largo, PKgHa = 15 }).Ok);
            for (int i = 0; i < 300; i++)
            {
                var e = s.StepDay().FirstOrDefault(x => x.Tipo == TipoEvento.Plaga && x.LoteId == 3);
                if (e != null) return (s, e);
            }
            Assert.Fail("no apareció la plaga en el lote 3");
            return default;
        }

        [Test]
        public void VenderCobraPrecioMenosComision()
        {
            var s = DatosPrueba.Nueva();
            s.Estado.StockT[Grano.Soja] = 100;
            double saldo = s.Estado.SaldoUsd, precio = s.Estado.Precios.PrecioUsdT[Grano.Soja];
            Assert.IsTrue(s.VenderGrano(Grano.Soja, 40).Ok);
            Assert.AreEqual(saldo + 40 * precio * 0.98, s.Estado.SaldoUsd, 1e-6);
            Assert.AreEqual(60, s.Estado.StockT[Grano.Soja], 1e-9);
        }

        [Test]
        public void VentasInvalidasSeRechazan()
        {
            var s = DatosPrueba.Nueva();
            Assert.AreEqual("La cantidad tiene que ser mayor que cero", s.VenderGrano(Grano.Soja, 0).Motivo);
            Assert.AreEqual("No hay suficiente maíz en silobolsa", s.VenderGrano(Grano.Maiz, 1).Motivo);
        }

        [Test]
        public void NaNSeRechaza()
        {
            var s = DatosPrueba.Nueva();
            s.Estado.StockT[Grano.Soja] = 10;
            Assert.IsFalse(s.VenderGrano(Grano.Soja, double.NaN).Ok);
            Assert.AreEqual(10, s.Estado.StockT[Grano.Soja]);
            s.ArrendarLote(2);
            var plan = Maiz();
            plan.NKgHa = double.NaN;
            Assert.IsFalse(s.PlanificarCultivo(2, plan).Ok);
        }

        [Test]
        public void EmbolsarCobraYSumaStock()
        {
            var s = DatosPrueba.Nueva();
            s.Datos.Economia.Silobolsa.RiesgoMensual = 0;
            s.ArrendarLote(2);
            Assert.IsTrue(s.PlanificarCultivo(2, Maiz(vender: false)).Ok);
            double antes = 0;
            for (int i = 0; i < 400 && s.Estado.Resultados.Count == 0; i++) { antes = s.Estado.SaldoUsd; s.StepDay(); }
            var r = s.Estado.Resultados.Single();
            double t = r.ToneladasCosechadas;
            Assert.AreEqual(t, s.Estado.StockT[Grano.Maiz], 1e-9);
            Assert.AreEqual(-(r.Margen.CosechaUsd + t * (5 + 0.25 * 25) + t * 4), s.Estado.SaldoUsd - antes, 1e-6);
        }

        [Test]
        public void SilobolsaPierdeUnaParte()
        {
            var s = DatosPrueba.Nueva();
            s.Datos.Economia.Silobolsa.RiesgoMensual = 1;
            s.Estado.StockT[Grano.Soja] = 100;
            var eventos = DatosPrueba.AvanzarHasta(s, Fecha.Crear(1, 6, 1));
            Assert.That(s.Estado.StockT[Grano.Soja], Is.InRange(75, 95));
            Assert.IsTrue(eventos.Any(e => e.Tipo == TipoEvento.PerdidaSilobolsa));
        }

        [Test]
        public void PlagaSobreUmbralPausaYBloqueaElDia()
        {
            var (s, plaga) = PlagaEnLote3();
            var lote = s.Estado.Lotes[2];
            Assert.IsTrue(plaga.Pausa);
            Assert.IsNotNull(lote.Decision);
            Assert.Throws<InvalidOperationException>(() => s.StepDay());

            double sev = lote.Decision.Severidad, saldo = s.Estado.SaldoUsd, lAntes = lote.Cultivo.LPlagas;
            Assert.IsTrue(s.DecidirTratamiento(3, true).Ok);
            Assert.AreEqual(saldo - (12 + 9) * 60, s.Estado.SaldoUsd, 1e-6);
            Assert.AreEqual(lAntes + sev * 0.15, lote.Cultivo.LPlagas, 1e-9);
            Assert.IsNull(lote.Decision);
        }

        [Test]
        public void NoAplicarSumaLaSeveridadCompleta()
        {
            var (s, _) = PlagaEnLote3();
            var lote = s.Estado.Lotes[2];
            double sev = lote.Decision.Severidad, saldo = s.Estado.SaldoUsd, lAntes = lote.Cultivo.LPlagas;
            Assert.IsTrue(s.DecidirTratamiento(3, false).Ok);
            Assert.AreEqual(lAntes + sev, lote.Cultivo.LPlagas, 1e-9);
            Assert.AreEqual(saldo, s.Estado.SaldoUsd, 1e-9);
        }

        [Test]
        public void InteresDiarioEnDescubierto()
        {
            var s = DatosPrueba.Nueva();
            s.Estado.SaldoUsd = -10000;
            s.StepDay();
            Assert.AreEqual(-10000 * (1 + 0.15 / 365), s.Estado.SaldoUsd, 1e-6);
        }

        [Test]
        public void AlertaDeSaldoUnaSolaVez()
        {
            var s = DatosPrueba.Nueva();
            s.Estado.SaldoUsd = -41000;
            Assert.IsTrue(s.StepDay().Any(e => e.Tipo == TipoEvento.SaldoCercaDelLimite && e.Pausa));
            Assert.IsFalse(s.StepDay().Any(e => e.Tipo == TipoEvento.SaldoCercaDelLimite));
        }

        [Test]
        public void SiembraQueRompeElLimiteTerminaLaPartida()
        {
            var s = DatosPrueba.Nueva();
            s.ArrendarLote(2);
            s.PlanificarCultivo(2, Maiz());
            DatosPrueba.AvanzarHasta(s, Fecha.Crear(30, 9, 1));
            s.Estado.SaldoUsd = -10000;
            Assert.IsTrue(s.StepDay().Any(e => e.Tipo == TipoEvento.Quiebra && e.Pausa));
            Assert.IsTrue(s.Estado.Terminada);
            Assert.IsEmpty(s.StepDay());
            Assert.AreEqual(Fecha.Crear(1, 10, 1), s.Estado.Fecha);
            Assert.AreEqual("La partida terminó por quiebra", s.ArrendarLote(3).Motivo);
        }
    }
}
