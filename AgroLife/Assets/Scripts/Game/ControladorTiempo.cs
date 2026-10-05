using System;
using System.Collections.Generic;
using AgroLife.Sim;
using UnityEngine;

namespace AgroLife.Game
{
    /// <summary>Días simulados por segundo real (spec §4: x1 = 6 días/s).</summary>
    public enum Velocidad { Pausa = 0, X1 = 6, X2 = 12, X4 = 24 }

    public static class Pasos
    {
        public const int MaxDiasPorFrame = 4;

        /// <summary>Acumula dt·velocidad y devuelve los días enteros a simular, como máximo MaxDiasPorFrame; el sobrante de más de un día se descarta.</summary>
        public static int DiasAAvanzar(ref double acumulado, double dt, Velocidad v)
        {
            if (v == Velocidad.Pausa) return 0;
            acumulado += dt * (int)v;
            int n = Math.Min(MaxDiasPorFrame, (int)Math.Floor(acumulado + 1e-9)); // 1e-9: sumar 0,1 sesenta veces da 5,999…
            acumulado -= n;
            if (acumulado >= 1) acumulado -= Math.Floor(acumulado);
            return n;
        }
    }

    /// <summary>Llama a StepDay según la velocidad y se frena ante eventos con pausa, decisiones pendientes o quiebra.</summary>
    public sealed class ControladorTiempo : MonoBehaviour
    {
        public Simulation Sim;
        public Velocidad Velocidad { get; private set; } = Velocidad.Pausa;
        public event Action<List<Evento>> Eventos;
        public event Action<Velocidad> CambioVelocidad;

        double acumulado;

        public static bool HayDecisiones(Simulation s) => s.Estado.Lotes.Exists(l => l.Decision != null);

        public void CambiarVelocidad(Velocidad v)
        {
            if (v != Velocidad.Pausa && (Sim == null || Sim.Estado.Terminada || HayDecisiones(Sim))) v = Velocidad.Pausa;
            if (v == Velocidad) return;
            Velocidad = v;
            acumulado = 0;
            CambioVelocidad?.Invoke(v);
        }

        void Update()
        {
            if (Sim == null || Velocidad == Velocidad.Pausa) return;
            int dias = Pasos.DiasAAvanzar(ref acumulado, Time.unscaledDeltaTime, Velocidad);
            for (int i = 0; i < dias; i++)
            {
                var eventos = Sim.StepDay();
                Eventos?.Invoke(eventos);
                if (eventos.Exists(e => e.Pausa) || HayDecisiones(Sim) || Sim.Estado.Terminada)
                {
                    CambiarVelocidad(Velocidad.Pausa); // se frena ese mismo día
                    break;
                }
            }
        }
    }
}
