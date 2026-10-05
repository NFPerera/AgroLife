using System;
using AgroLife.Sim;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Avisos que no frenan el juego (cosecha, helada, vencimientos...): los últimos 6, se van solos a los 8 s.</summary>
    public sealed class Notificaciones
    {
        const int Maximo = 6;
        const long DuracionMs = 8000;
        readonly VisualElement raiz;

        public event Action<Evento> Click;

        public Notificaciones(VisualElement raiz) => this.raiz = raiz.Q("notificaciones");

        public void Agregar(Evento e) => Agregar($"{e.Fecha}: {e.Mensaje}", e);

        public void Agregar(string texto, Evento e = null)
        {
            var l = new Label(texto);
            l.AddToClassList("notificacion");
            if (e != null && Click != null)
            {
                l.pickingMode = PickingMode.Position;
                l.RegisterCallback<ClickEvent>(_ => Click?.Invoke(e));
            }
            raiz.Insert(0, l);
            while (raiz.childCount > Maximo) raiz.RemoveAt(raiz.childCount - 1);
            l.schedule.Execute(() => l.RemoveFromHierarchy()).StartingIn(DuracionMs);
        }
    }
}
