using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AgroLife.Game.Tests
{
    public class MallaLotesTests
    {
        static Mesh Cuadrado(float x, float y, float lado)
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(x, y), new Vector3(x + lado, y), new Vector3(x + lado, y + lado), new Vector3(x, y + lado) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            return m;
        }

        [Test]
        public void ConstruyeLaMallaConUnRangoPorLote()
        {
            var m = MallaLotes.Construir(new[] { Cuadrado(0, 0, 1), Cuadrado(5, 0, 2) });
            Assert.AreEqual(8, m.Mesh.vertexCount);
            Assert.AreEqual((4, 4), m.Rango(1));
            m.Pintar(1, new Color32(255, 0, 0, 255));
            m.Aplicar();
            Assert.AreEqual(new Color32(255, 0, 0, 255), m.Mesh.colors32[5]);
            Assert.AreEqual(12, m.Mesh.triangles.Length);
        }

        [Test]
        public void LineasUsaTopologiaDeLineas()
        {
            var mesh = Superposiciones.Lineas(new[] { new List<int[]> { new[] { 0, 0 }, new[] { 100, 0 }, new[] { 100, 100 } } });
            Assert.AreEqual(MeshTopology.Lines, mesh.GetTopology(0));
            Assert.AreEqual(4, mesh.GetIndices(0).Length); // 2 segmentos
        }
    }
}
