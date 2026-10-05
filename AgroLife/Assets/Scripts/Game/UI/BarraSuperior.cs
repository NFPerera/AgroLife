using System;
using System.Linq;
using AgroLife.Sim;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Fecha, velocidad, saldo, pronóstico de El Niño / La Niña, capa del mapa y accesos (spec §8).</summary>
    public sealed class BarraSuperior
    {
        readonly Label fecha, saldo, pronostico;
        readonly Button[] velocidades;
        readonly Button guardar;
        static readonly Velocidad[] Orden = { Velocidad.Pausa, Velocidad.X1, Velocidad.X2, Velocidad.X4 };

        public event Action Mercado, Guardar, Menu;

        public BarraSuperior(VisualElement raiz, UIJuego ui)
        {
            fecha = raiz.Q<Label>("fecha");
            saldo = raiz.Q<Label>("saldo");
            pronostico = raiz.Q<Label>("pronostico");
            velocidades = new[] { raiz.Q<Button>("vel-pausa"), raiz.Q<Button>("vel-x1"), raiz.Q<Button>("vel-x2"), raiz.Q<Button>("vel-x4") };
            for (int i = 0; i < velocidades.Length; i++)
            {
                var v = Orden[i];
                velocidades[i].clicked += () => ui.Tiempo.CambiarVelocidad(v);
            }
            var capa = raiz.Q<DropdownField>("capa");
            capa.choices = Enum.GetValues(typeof(Capa)).Cast<Capa>().Select(CapasMapa.Titulo).ToList();
            capa.index = (int)ui.CapaActual;
            capa.RegisterValueChangedCallback(_ => ui.CambiarCapa((Capa)capa.index));
            raiz.Q<Button>("btn-mercado").clicked += () => Mercado?.Invoke();
            guardar = raiz.Q<Button>("btn-guardar");
            guardar.clicked += () => Guardar?.Invoke();
            raiz.Q<Button>("btn-menu").clicked += () => Menu?.Invoke();
        }

        /// <summary>Con un diálogo abierto no se cambia la velocidad ni se guarda; una partida terminada no se guarda (pisaría la buena).</summary>
        public void Refrescar(Simulation s, Velocidad actual, bool bloqueada)
        {
            fecha.text = s.Estado.Fecha.ToString();
            saldo.text = Formato.Usd(s.Estado.SaldoUsd);
            var p = s.Estado.Clima.Pronostico; // nunca la fase real
            pronostico.text = p.Count == 0 ? "" :
                $"Pronóstico: Niño {Formato.Numero(p[FaseEnso.Nino] * 100, 0)} % · Neutro {Formato.Numero(p[FaseEnso.Neutro] * 100, 0)} % · Niña {Formato.Numero(p[FaseEnso.Nina] * 100, 0)} %";
            for (int i = 0; i < velocidades.Length; i++)
            {
                velocidades[i].EnableInClassList("activo", Orden[i] == actual);
                velocidades[i].SetEnabled(!bloqueada && !s.Estado.Terminada);
            }
            guardar.SetEnabled(!bloqueada && !s.Estado.Terminada);
        }
    }
}
