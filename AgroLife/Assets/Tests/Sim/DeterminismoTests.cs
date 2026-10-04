using System;
using System.Linq;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class DeterminismoTests
    {
        /// <summary>Un jugador simple: arrienda los lotes 1 y 2 cada campaña, siembra maíz y trigo/soja de segunda, trata las plagas y vende en julio.</summary>
        static void Guion(Simulation s, int dias)
        {
            for (int i = 0; i < dias && !s.Estado.Terminada; i++)
            {
                foreach (var l in s.Estado.Lotes)
                    if (l.Decision != null && !s.DecidirTratamiento(l.Id, true).Ok) s.DecidirTratamiento(l.Id, false);
                var f = s.Estado.Fecha;
                if (f.Dia == 1 && f.Mes == 5)
                {
                    s.ArrendarLote(2);
                    s.PlanificarCultivo(2, new PlanCultivo { CultivoId = "maiz", FechaSiembra = Fecha.EnCampania(f.Campania, 1, 10), Genetica = Genetica.Largo, NKgHa = 120, PKgHa = 20 });
                    s.ArrendarLote(1);
                    s.PlanificarCultivo(1, new PlanCultivo
                    {
                        CultivoId = "trigo", FechaSiembra = Fecha.EnCampania(f.Campania, 15, 6), Genetica = Genetica.Largo, NKgHa = 90, PKgHa = 15,
                        VenderAlCosechar = false, Soja2 = new PlanCultivo { CultivoId = "soja2", Genetica = Genetica.Largo, PKgHa = 10, VenderAlCosechar = false },
                    });
                }
                if (f.Dia == 1 && f.Mes == 7)
                    foreach (var g in new[] { Grano.Trigo, Grano.Maiz, Grano.Soja })
                        if (s.Estado.StockT[g] > 0) s.VenderGrano(g, s.Estado.StockT[g]);
                s.StepDay();
            }
        }

        [Test]
        public void MismaSemillaDiezAniosEstadoIdentico()
        {
            var a = DatosPrueba.Nueva(42, plagas: true);
            var b = DatosPrueba.Nueva(42, plagas: true);
            // Saldo holgado: sin calibrar, el guion quiebra antes de los 10 años y el test cubriría menos.
            a.Estado.SaldoUsd = b.Estado.SaldoUsd = 5_000_000;
            Guion(a, 3650);
            Guion(b, 3650);
            Assert.IsFalse(a.Estado.Terminada);
            Assert.AreEqual(Fecha.Crear(1, 5, 11), a.Estado.Fecha);
            Assert.AreEqual(a.Guardar(), b.Guardar());
        }

        [Test]
        public void DecisionesDistintasNoCambianClimaNiPrecios()
        {
            var a = DatosPrueba.Nueva(42, plagas: true);
            var b = DatosPrueba.Nueva(42, plagas: true);
            for (int i = 0; i < 365 * 3; i++)
            {
                Guion(a, 1);
                b.StepDay();
                DiaClima ca = a.Estado.Clima.Hoy, cb = b.Estado.Clima.Hoy;
                Assert.AreEqual(cb.LluviaMm, ca.LluviaMm); Assert.AreEqual(cb.TmaxC, ca.TmaxC); Assert.AreEqual(cb.TminC, ca.TminC);
                Assert.AreEqual(cb.RadMJ, ca.RadMJ); Assert.AreEqual(cb.Et0Mm, ca.Et0Mm);
                foreach (var g in new[] { Grano.Trigo, Grano.Maiz, Grano.Soja })
                    Assert.AreEqual(b.Estado.Precios.PrecioUsdT[g], a.Estado.Precios.PrecioUsdT[g]);
            }
            Assert.IsFalse(a.Estado.Terminada);
            Assert.Greater(a.Estado.Resultados.Count, 0);
        }

        [Test]
        public void GuardarYCargarDaElMismoEstado()
        {
            var a = DatosPrueba.Nueva(42, plagas: true);
            Guion(a, 500);
            var json = a.Guardar();
            var b = Simulation.Cargar(DatosPrueba.Cargar(), json);
            Assert.AreEqual(json, b.Guardar());
            Guion(a, 400);
            Guion(b, 400);
            Assert.AreEqual(a.Guardar(), b.Guardar());
        }

        [Test]
        public void GuardarConDecisionPendienteLaConserva()
        {
            DatosJuego Datos()
            {
                var d = DatosPrueba.Cargar();
                foreach (var p in d.Plagas) p.ProbDiaria = p.Id == "chinches" ? 1 : 0;
                d.Plagas.Single(p => p.Id == "chinches").Severidad = new Rango { Min = 0.2, Max = 0.3 };
                return d;
            }
            var a = Simulation.Nueva(Datos(), 1234);
            a.ArrendarLote(3);
            a.PlanificarCultivo(3, new PlanCultivo { CultivoId = "soja1", FechaSiembra = Fecha.Crear(15, 11, 1), Genetica = Genetica.Largo, PKgHa = 15 });
            for (int i = 0; i < 300 && a.Estado.Lotes[2].Decision == null; i++) a.StepDay();
            Assert.IsNotNull(a.Estado.Lotes[2].Decision);

            var b = Simulation.Cargar(Datos(), a.Guardar());
            Assert.AreEqual(a.Estado.Lotes[2].Decision.Severidad, b.Estado.Lotes[2].Decision.Severidad);
            Assert.Throws<InvalidOperationException>(() => b.StepDay());
        }

        [Test]
        public void VersionIncompatibleSeRechazaConMensaje()
        {
            var json = DatosPrueba.Nueva().Guardar().Replace("\"Version\":1", "\"Version\":99");
            Assert.AreEqual("La partida guardada es de una versión incompatible (v99; se esperaba v1)",
                Assert.Throws<PartidaIncompatibleException>(() => Simulation.Cargar(DatosPrueba.Cargar(), json)).Message);
        }

        [Test]
        public void PartidaDaniadaSeRechaza() =>
            StringAssert.StartsWith("La partida guardada está dañada:",
                Assert.Throws<PartidaIncompatibleException>(() => Simulation.Cargar(DatosPrueba.Cargar(), "{")).Message);
    }
}
