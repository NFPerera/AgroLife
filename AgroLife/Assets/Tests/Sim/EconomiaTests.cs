using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace AgroLife.Sim.Tests
{
    public class EconomiaTests
    {
        EconomiaParams p;
        [SetUp] public void Cargar() => p = DatosPrueba.Cargar().Economia;

        [Test]
        public void PrecioNetoDescuentaComisionYFlete() => Assert.AreEqual(284, Economia.PrecioNetoUsdT(300, p, 20), 1e-9);

        [Test]
        public void MargenEsCeroEnElRindeDeIndiferencia()
        {
            double ri = Economia.RindeIndiferenciaQqHa(300, 284, 0.08);
            Assert.AreEqual(11.4819, ri, 0.0001); // 300 / (28,4 · 0,92)
            Assert.AreEqual(0, Economia.Margen(ri / 10, 300, 20, 300, p).MargenBrutoUsd, 1e-6);
        }

        [Test]
        public void CostosDeSiembra()
        {
            Assert.AreEqual(582, Economia.CostosDirectosSiembraUsdHa("maiz", 120, 20, p), 1e-9);
            Assert.AreEqual(236, Economia.CostosDirectosSiembraUsdHa("soja1", 0, 0, p), 1e-9);
        }

        [Test]
        public void ArrendamientoYCompraSegunIp()
        {
            var l = new LoteDatos { SuperficieHa = 100, Ip = 80 };
            var r = new RegionParams { ArrendamientoBaseQqHa = 17, PrecioBaseTierraUsdHa = 14000 };
            Assert.AreEqual(40800, Economia.ArrendamientoUsd(l, r, 300), 1e-6);
            Assert.AreEqual(1120000, Economia.PrecioCompraUsd(l, r), 1e-6);
        }

        [Test]
        public void InteresDelDescubierto()
        {
            Assert.AreEqual(-4.10959, Economia.InteresDiario(-10000, 0.15), 1e-5);
            Assert.AreEqual(0, Economia.InteresDiario(5000, 0.15));
        }

        [Test]
        public void PreciosVuelvenALaMedia()
        {
            var xs = Recorrido(out _);
            double media = 0;
            foreach (var x in xs) media += x;
            media /= xs.Count;
            double var = 0;
            foreach (var x in xs) var += (x - media) * (x - media);
            double desvio = Math.Sqrt(var / xs.Count);
            var soja = p.Granos[Grano.Soja];
            double phi = Math.Exp(-Math.Log(2) / soja.VidaMediaDias);
            double esperado = soja.VolatilidadDiaria / Math.Sqrt(1 - phi * phi);
            Assert.Less(Math.Abs(media), 0.03);
            Assert.AreEqual(esperado, desvio, esperado * 0.15);
        }

        [Test]
        public void PrecioSigueLaEstacionalidad()
        {
            Recorrido(out var porMes);
            Assert.AreEqual(0.95 / 1.01, porMes[5] / porMes[9], 0.95 / 1.01 * 0.04);
        }

        [Test]
        public void HistorialDePreciosGuardaLosUltimos730Dias()
        {
            var s = DatosPrueba.Nueva();
            for (int i = 0; i < 800; i++) s.StepDay();
            var h = s.Estado.Precios.Historial;
            Assert.AreEqual(730, h.Count);
            Assert.AreEqual(s.Estado.Fecha, h[h.Count - 1].Fecha);
            Assert.AreEqual(s.Estado.Precios.PrecioUsdT[Grano.Soja], h[h.Count - 1].SojaUsdT);
            Assert.AreEqual(h[0].Fecha.MasDias(729), h[729].Fecha);
        }

        /// <summary>100 años de precios: devuelve X de soja por día y el precio medio de soja por mes (1..12).</summary>
        List<double> Recorrido(out double[] precioMedioPorMes)
        {
            var e = new EstadoPrecios();
            var rng = new Pcg32(8, (ulong)Flujo.Precios);
            var inicio = Fecha.Crear(1, 5, 1);
            Precios.Inicializar(e, p, inicio);
            var xs = new List<double>();
            var suma = new double[13];
            var n = new int[13];
            for (int i = 1; i <= 365 * 100; i++)
            {
                var f = inicio.MasDias(i);
                Precios.AvanzarDia(e, p, f, rng);
                xs.Add(e.X[Grano.Soja]);
                suma[f.Mes] += e.PrecioUsdT[Grano.Soja];
                n[f.Mes]++;
            }
            precioMedioPorMes = new double[13];
            for (int m = 1; m <= 12; m++) precioMedioPorMes[m] = suma[m] / n[m];
            return xs;
        }
    }
}
