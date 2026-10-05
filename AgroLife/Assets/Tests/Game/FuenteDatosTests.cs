using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AgroLife.Game.Tests
{
    public class FuenteDatosTests
    {
        [Test]
        public void FuenteDatosCargaPergamino()
        {
            var f = AssetDatabase.LoadAssetAtPath<FuenteDatos>("Assets/Data/FuenteDatos.asset");
            var d = f.CargarDatos();
            var g = f.CargarGeografia();
            Assert.AreEqual(d.Lotes.Count, g.Lotes.Count);
            Assert.That(g.Lotes.Count, Is.InRange(3000, 5000));
            Assert.AreEqual(1, g.Lotes[0].Id);
            Assert.GreaterOrEqual(g.Lotes[0].Poligono.Count, 3);
            Assert.GreaterOrEqual(g.Contorno.Count, 3);
            Assert.Greater(g.Localidades.Count, 0);
            Assert.AreEqual(g.Localidades.Count, g.Acopios.Count);
        }

        [Test]
        public void EscalaPasaMetrosAUnidades() => Assert.AreEqual(new Vector2(12.34f, -5f), Escala.AUnidades(1234, -500));
    }
}
