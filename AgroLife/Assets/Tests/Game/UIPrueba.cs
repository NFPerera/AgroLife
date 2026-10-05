using AgroLife.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgroLife.Game.Tests
{
    /// <summary>Arma la interfaz real (Juego.uxml) con un UIJuego sin entrar en Play, para probar los paneles.</summary>
    sealed class UIPrueba : System.IDisposable
    {
        readonly GameObject go;
        readonly Simulation simAnterior;
        public readonly UIJuego Ui;
        public readonly VisualElement Raiz;

        public UIPrueba(Simulation s)
        {
            simAnterior = Sesion.Sim;
            Sesion.Sim = s;
            Raiz = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Juego.uxml").Instantiate().Q("juego");
            go = new GameObject("ui-prueba");
            Ui = go.AddComponent<UIJuego>();
            Ui.Tiempo = go.AddComponent<ControladorTiempo>();
            Ui.Tiempo.Sim = s;
        }

        public void Dispose()
        {
            Object.DestroyImmediate(go);
            Sesion.Sim = simAnterior;
        }
    }
}
