using System.Collections.Generic;
using AgroLife.Sim;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    public sealed class Termino { public string Id, Nombre, Definicion; }

    /// <summary>
    /// Glosario (spec §8): a cada elemento con la clase "termino" y name "t-&lt;id&gt;" le agrega una ayuda emergente.
    /// Los tooltips nativos de UI Toolkit solo andan en el Editor; este es propio.
    /// </summary>
    public sealed class Glosario
    {
        const string Marcado = "glosario-marcado";
        readonly Dictionary<string, Termino> terminos = new Dictionary<string, Termino>();

        sealed class Archivo { public List<Termino> Terminos; }

        public static Glosario Desde(string json)
        {
            var g = new Glosario();
            foreach (var t in JsonSim.Deserializar<Archivo>(json).Terminos) g.terminos[t.Id] = t;
            return g;
        }

        public Termino Buscar(string id) => id != null && terminos.TryGetValue(id, out var t) ? t : null;

        public void Marcar(VisualElement raiz, Label tooltip)
        {
            raiz.Query<VisualElement>(className: "termino").ForEach(e =>
            {
                if (e.ClassListContains(Marcado) || e.name == null || !e.name.StartsWith("t-")) return;
                var t = Buscar(e.name.Substring(2));
                if (t == null) return;
                e.AddToClassList(Marcado);
                e.pickingMode = PickingMode.Position;
                e.RegisterCallback<PointerEnterEvent>(ev =>
                {
                    tooltip.text = $"{t.Nombre}: {t.Definicion}";
                    tooltip.RemoveFromClassList("oculto");
                    Ubicar(tooltip, ev.position);
                });
                e.RegisterCallback<PointerMoveEvent>(ev => Ubicar(tooltip, ev.position));
                e.RegisterCallback<PointerLeaveEvent>(_ => tooltip.AddToClassList("oculto"));
            });
        }

        static void Ubicar(Label tooltip, UnityEngine.Vector2 posicionPanel)
        {
            tooltip.style.left = posicionPanel.x + 16;
            tooltip.style.top = posicionPanel.y + 16;
        }
    }
}
