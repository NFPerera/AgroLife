using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AgroLife.Game.Tests
{
    public class GlosarioTests
    {
        [Test]
        public void GlosarioRealTieneLosTerminosQueUsaLaInterfaz()
        {
            var g = Glosario.Desde(File.ReadAllText("Assets/Data/glosario.json"));
            foreach (var id in new[] { "ip", "clase_textural", "agua_util", "margen_bruto", "rinde_indiferencia", "umbral_danio", "arrendamiento", "resultado" })
                Assert.IsNotEmpty(g.Buscar(id)?.Definicion, id);
            var uxml = File.ReadAllText("Assets/UI/Juego.uxml");
            foreach (Match m in Regex.Matches(uxml, "name=\"t-([a-z_]+)\""))
                Assert.IsNotNull(g.Buscar(m.Groups[1].Value), m.Groups[1].Value); // ningún término marcado sin definición
        }
    }
}
