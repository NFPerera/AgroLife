using AgroLife.Sim;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static AgroLife.Game.Tests.DatosPruebaGame;

namespace AgroLife.Game.Tests
{
    public class PanelesTests
    {
        [Test]
        public void FichaNoRecreaSusBotonesSiNoCambioElEstado()
        {
            var s = Nueva();
            using (var p = new UIPrueba(s))
            {
                var ficha = new FichaLote(p.Raiz, p.Ui);
                ficha.Abrir(2);
                var arrendar = p.Raiz.Q("ficha-acciones").Q<Button>();
                var antes = arrendar.text;
                for (int i = 0; i < 30; i++) s.StepDay();
                ficha.Refrescar(s);
                Assert.AreSame(arrendar, p.Raiz.Q("ficha-acciones").Q<Button>(), "un clic en curso no se pierde");
                Assert.AreNotEqual(antes, arrendar.text, "el precio del arrendamiento se actualiza");
                s.ArrendarLote(2);
                ficha.Refrescar(s);
                Assert.AreNotSame(arrendar, p.Raiz.Q("ficha-acciones").Q<Button>(), "con otro estado cambian las acciones");
            }
        }

        [Test]
        public void DosDecisionesPendientesSeMuestranUnaTrasOtra()
        {
            var s = Nueva();
            foreach (var id in new[] { 1, 2 })
            {
                var lote = s.Estado.Lotes[id - 1];
                lote.Cultivo = ModeloCultivo.Sembrar(s.Datos.Cultivos["soja1"], Genetica.Largo, s.Estado.Fecha, 0, 15, s.Datos.Lote(id), 14, s.Datos.Suelo);
                lote.Decision = new DecisionPlaga { PlagaId = "chinches", Severidad = 0.2, Nivel = 2, Umbral = 1, CostoUsd = 100, PerdidaEsperadaQq = 10, PerdidaEsperadaUsd = 300 };
            }
            using (var p = new UIPrueba(s))
            {
                var dialogo = new DialogoEvento(p.Raiz, p.Ui);
                dialogo.MostrarPendientes();
                Assert.IsTrue(dialogo.Abierto);
                dialogo.Decidir(1, false);
                Assert.IsTrue(dialogo.Abierto, "falta la del lote 2");
                StringAssert.Contains("lote 2", p.Raiz.Q<Label>("dialogo-titulo").text);
                dialogo.Decidir(2, false);
                Assert.IsFalse(dialogo.Abierto);
            }
        }

        [Test]
        public void UnaPartidaCargadaYaTerminadaMuestraLaQuiebra()
        {
            var s = Nueva();
            s.Estado.Terminada = true;
            using (var p = new UIPrueba(s))
            {
                var dialogo = new DialogoEvento(p.Raiz, p.Ui);
                dialogo.MostrarPendientes();
                Assert.IsTrue(dialogo.Abierto, "sin explicación el juego queda frenado");
                Assert.AreEqual("Quiebra", p.Raiz.Q<Label>("dialogo-titulo").text);
            }
        }

        [Test]
        public void ConLaPartidaTerminadaODialogoAbiertoNoSePuedeGuardar()
        {
            var s = Nueva();
            using (var p = new UIPrueba(s))
            {
                var barra = new BarraSuperior(p.Raiz, p.Ui);
                var guardar = p.Raiz.Q<Button>("btn-guardar");
                barra.Refrescar(s, Velocidad.Pausa, false);
                Assert.IsTrue(guardar.enabledSelf);
                barra.Refrescar(s, Velocidad.Pausa, true);
                Assert.IsFalse(guardar.enabledSelf, "con un diálogo abierto");
                s.Estado.Terminada = true;
                barra.Refrescar(s, Velocidad.Pausa, false);
                Assert.IsFalse(guardar.enabledSelf, "una partida terminada no pisa la guardada");
            }
        }

        [Test]
        public void DatosDeMapaOGlosarioInvalidosSeInformanSinDejarLaSesionAMedias()
        {
            var real = AssetDatabase.LoadAssetAtPath<FuenteDatos>("Assets/Data/FuenteDatos.asset");
            foreach (var romper in new System.Action<FuenteDatos>[] { f => f.Geografia = new TextAsset("{"), f => f.Glosario = new TextAsset("{") })
            {
                var fuente = Object.Instantiate(real);
                romper(fuente);
                Sesion.Olvidar();
                Assert.Throws<DatosInvalidosException>(() => Sesion.CargarDatos(fuente));
                Assert.IsNull(Sesion.Datos);
                Assert.IsNull(Sesion.Geografia);
                Object.DestroyImmediate(fuente);
            }
            Sesion.Olvidar();
        }
    }
}
