using System;
using AgroLife.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Menú (spec §8): nueva partida o cargar una guardada. Con datos inválidos, muestra el error y no deja jugar (spec §10).</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuPrincipal : MonoBehaviour
    {
        public FuenteDatos Fuente;

        Label mensaje, error;
        ScrollView partidas;
        Button nueva;

        void Start()
        {
            var raiz = GetComponent<UIDocument>().rootVisualElement.Q("menu");
            mensaje = raiz.Q<Label>("menu-mensaje");
            error = raiz.Q<Label>("menu-error");
            partidas = raiz.Q<ScrollView>("menu-partidas");
            nueva = raiz.Q<Button>("menu-nueva");
            nueva.clicked += NuevaPartida;
            raiz.Q<Button>("menu-salir").clicked += Application.Quit;
            mensaje.text = Sesion.MensajeMenu ?? "";
            Sesion.MensajeMenu = null;
            try
            {
                Sesion.CargarDatos(Fuente);
            }
            catch (Exception e) // DatosInvalidosException con el detalle, o cualquier otro problema al leer los datos
            {
                error.text = e.Message;
                nueva.SetEnabled(false);
                return;
            }
            ListarPartidas();
        }

        public void NuevaPartida()
        {
            ulong semilla = (ulong)DateTime.Now.Ticks;
            Sesion.Sim = Simulation.Nueva(Sesion.Datos, semilla);
            Sesion.RutaGuardado = Guardado.RutaPara(semilla);
            SceneManager.LoadScene("Mapa");
        }

        public void Cargar(string ruta)
        {
            try
            {
                Sesion.Sim = Guardado.Cargar(Sesion.Datos, ruta);
                Sesion.RutaGuardado = ruta;
                SceneManager.LoadScene("Mapa");
            }
            catch (Exception e)
            {
                mensaje.text = e.Message; // versión incompatible o archivo dañado: se avisa y no se cambia de escena
            }
        }

        void ListarPartidas()
        {
            partidas.Clear();
            var lista = Guardado.Listar(Sesion.Datos);
            if (lista.Count == 0) partidas.Add(new Label("No hay partidas guardadas"));
            foreach (var p in lista)
            {
                var fila = new VisualElement();
                fila.AddToClassList("partida");
                var texto = new Label(p.Error == null ? $"{p.Descripcion} · guardada {p.Modificada:dd/MM HH:mm}" : $"{p.Descripcion}: {p.Error}");
                texto.AddToClassList("partida-texto");
                if (p.Error != null) texto.AddToClassList("motivo");
                fila.Add(texto);
                var ruta = p.Ruta;
                var boton = new Button(() => Cargar(ruta)) { text = "Cargar" };
                boton.AddToClassList("boton");
                boton.SetEnabled(p.Error == null);
                fila.Add(boton);
                partidas.Add(fila);
            }
        }
    }
}
