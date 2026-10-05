using NUnit.Framework;

namespace AgroLife.Game.Tests
{
    public class ControladorTiempoTests
    {
        [Test]
        public void X1AvanzaSeisDiasPorSegundo()
        {
            double a = 0;
            int total = 0;
            for (int i = 0; i < 60; i++) total += Pasos.DiasAAvanzar(ref a, 1.0 / 60, Velocidad.X1);
            Assert.AreEqual(6, total);
        }

        [Test]
        public void UnFrameLentoNoAvanzaMasDeCuatroDias()
        {
            double a = 0;
            Assert.AreEqual(4, Pasos.DiasAAvanzar(ref a, 2.0, Velocidad.X4)); // 48 días pedidos
            Assert.Less(a, 1.0);
        }

        [Test]
        public void EnPausaNoAvanza()
        {
            double a = 0.9;
            Assert.AreEqual(0, Pasos.DiasAAvanzar(ref a, 1, Velocidad.Pausa));
        }
    }
}
