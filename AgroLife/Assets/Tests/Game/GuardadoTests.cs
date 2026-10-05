using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using static AgroLife.Game.Tests.DatosPruebaGame;

namespace AgroLife.Game.Tests
{
    public class GuardadoTests
    {
        string carpetaOriginal;

        [SetUp] public void CarpetaTemporal()
        {
            carpetaOriginal = Guardado.Carpeta;
            Guardado.Carpeta = Path.Combine(Path.GetTempPath(), "agrolife-tests-" + Guid.NewGuid());
        }

        [TearDown] public void Limpiar()
        {
            if (Directory.Exists(Guardado.Carpeta)) Directory.Delete(Guardado.Carpeta, true);
            Guardado.Carpeta = carpetaOriginal;
        }

        [Test]
        public void GuardarYCargarDevuelveLaMismaPartida()
        {
            var s = Nueva();
            for (int i = 0; i < 30; i++) s.StepDay();
            var ruta = Guardado.RutaPara(s.Estado.Semilla);
            Guardado.Guardar(s, ruta);
            Assert.AreEqual(s.Guardar(), Guardado.Cargar(Datos(), ruta).Guardar());
            Assert.IsFalse(File.Exists(ruta + ".tmp"));
        }

        [Test]
        public void GuardarDosVecesReemplaza()
        {
            var s = Nueva();
            var ruta = Guardado.RutaPara(s.Estado.Semilla);
            Guardado.Guardar(s, ruta);
            s.StepDay();
            Guardado.Guardar(s, ruta);
            Assert.AreEqual(s.Estado.Fecha, Guardado.Cargar(Datos(), ruta).Estado.Fecha);
        }

        [Test]
        public void ArchivoDanadoApareceConSuErrorYNoRompeLaLista()
        {
            Guardado.Guardar(Nueva(), Guardado.RutaPara(1));
            File.WriteAllText(Path.Combine(Guardado.Carpeta, "partida-2.json"), "{");
            var lista = Guardado.Listar(Datos());
            Assert.AreEqual(2, lista.Count);
            StringAssert.StartsWith("La partida guardada está dañada", lista.Single(p => p.Ruta.EndsWith("partida-2.json")).Error);
            Assert.IsNull(lista.Single(p => p.Ruta.EndsWith("partida-1.json")).Error);
        }
    }
}
