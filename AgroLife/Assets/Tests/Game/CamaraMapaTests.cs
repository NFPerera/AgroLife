using NUnit.Framework;
using UnityEngine;

namespace AgroLife.Game.Tests
{
    public class CamaraMapaTests
    {
        [Test]
        public void LimitarNoDejaVerFueraDelContorno()
        {
            var r = new Rect(-100, -100, 200, 200);
            Assert.AreEqual(new Vector3(90, 0, -10), CamaraMapa.Limitar(new Vector3(500, 0, -10), 10, 1, r));
        }

        [Test]
        public void VistaMasGrandeQueElContornoCentra() =>
            Assert.AreEqual(new Vector3(0, 0, -10), CamaraMapa.Limitar(new Vector3(50, 50, -10), 300, 1, new Rect(-100, -100, 200, 200)));

        [Test]
        public void ZoomRespetaLosTopes()
        {
            Assert.AreEqual(5f, CamaraMapa.ZoomHacia(5.5f, 1, 5, 300), 1e-4);
            Assert.AreEqual(11.5f, CamaraMapa.ZoomHacia(10f, -1, 5, 300), 1e-4);
        }
    }
}
