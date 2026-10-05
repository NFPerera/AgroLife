using System;
using AgroLife.Sim;
using NUnit.Framework;
using UnityEngine;
using static AgroLife.Game.Tests.DatosPruebaGame;

namespace AgroLife.Game.Tests
{
    public class CapasMapaTests
    {
        [Test]
        public void EstadoSinCultivoEsSueloDesnudo() =>
            Assert.AreEqual(CapasMapa.SueloDesnudo, CapasMapa.Color(Capa.Estado, 1, Nueva()));

        [Test]
        public void EstadoEnMadurezEsAmarillo()
        {
            var s = Nueva();
            var l = s.Estado.Lotes[0];
            l.Cultivo = ModeloCultivo.Sembrar(s.Datos.Cultivos["maiz"], Genetica.Largo, s.Estado.Fecha, 0, 0, s.Datos.Lote(1), 20, s.Datos.Suelo);
            l.Cultivo.Etapa = Etapa.Madurez;
            Assert.AreEqual(new Color32(0xE3, 0xC0, 0x4B, 255), CapasMapa.Color(Capa.Estado, 1, s));
        }

        [Test]
        public void GradienteDeTresParadas()
        {
            Color32 r = new Color32(0xB5, 0x45, 0x2F, 255), a = new Color32(0xE3, 0xC0, 0x4B, 255), v = new Color32(0x3E, 0x8E, 0x41, 255);
            Assert.AreEqual(r, CapasMapa.Gradiente(0f, r, a, v));
            Assert.AreEqual(a, CapasMapa.Gradiente(0.5f, r, a, v));
            Assert.AreEqual(v, CapasMapa.Gradiente(1.7f, r, a, v)); // fuera de rango se limita
        }

        [Test]
        public void LeyendaDeCadaCapaNoEstaVacia()
        {
            foreach (Capa c in Enum.GetValues(typeof(Capa))) Assert.Greater(CapasMapa.Leyenda(c).Count, 1, c.ToString());
        }
    }
}
