using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Gráfico de líneas con Painter2D: hasta 3 series, eje con mínimo y máximo, fechas de inicio y fin.</summary>
    public sealed class GraficoLinea : VisualElement
    {
        const float MargenIzq = 56f, MargenAbajo = 22f, Margen = 8f;
        static readonly Color Ejes = new Color(0.93f, 0.90f, 0.83f, 0.35f);

        IReadOnlyList<Color> colores = new Color[0];
        IReadOnlyList<double[]> series = new double[0][];
        double min, max;
        readonly Label lMin, lMax, lInicio, lFin;
        readonly VisualElement referencias;

        public int Puntos => series.Count == 0 ? 0 : series[0].Length;

        public GraficoLinea()
        {
            style.height = 240;
            style.flexGrow = 1;
            generateVisualContent += Dibujar;
            lMax = Texto(0, null, Margen);
            lMin = Texto(0, null, null, MargenAbajo);
            lInicio = Texto(MargenIzq, null, null, 0);
            lFin = Texto(null, 0, null, 0);
            referencias = new VisualElement { pickingMode = PickingMode.Ignore };
            referencias.style.flexDirection = FlexDirection.Row;
            referencias.style.position = Position.Absolute;
            referencias.style.right = 0;
            referencias.style.top = 0;
            Add(referencias);
        }

        Label Texto(float? izq, float? der, float? arriba, float? abajo = null)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.fontSize = 11;
            l.style.color = new Color(0.73f, 0.78f, 0.71f);
            if (izq.HasValue) l.style.left = izq.Value;
            if (der.HasValue) l.style.right = der.Value;
            if (arriba.HasValue) l.style.top = arriba.Value;
            if (abajo.HasValue) l.style.bottom = abajo.Value;
            Add(l);
            return l;
        }

        public void Mostrar(IReadOnlyList<string> nombres, IReadOnlyList<Color> colores, IReadOnlyList<double[]> series, string inicio, string fin)
        {
            this.colores = colores;
            this.series = series;
            var todos = series.SelectMany(s => s).ToList();
            min = todos.Count > 0 ? todos.Min() : 0;
            max = todos.Count > 0 ? todos.Max() : 1;
            if (max - min < 1e-6) max = min + 1;
            lMin.text = Sim.Formato.Numero(min, 0);
            lMax.text = Sim.Formato.Numero(max, 0);
            lInicio.text = inicio;
            lFin.text = fin;
            referencias.Clear();
            for (int i = 0; i < nombres.Count; i++)
            {
                var r = new Label("● " + nombres[i]) { pickingMode = PickingMode.Ignore };
                r.style.color = colores[i];
                r.style.marginLeft = 10;
                r.style.fontSize = 12;
                referencias.Add(r);
            }
            MarkDirtyRepaint();
        }

        void Dibujar(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float x0 = MargenIzq, x1 = contentRect.width - Margen, y0 = contentRect.height - MargenAbajo, y1 = Margen + 16;
            p.strokeColor = Ejes;
            p.lineWidth = 1;
            p.BeginPath();
            p.MoveTo(new Vector2(x0, y1));
            p.LineTo(new Vector2(x0, y0));
            p.LineTo(new Vector2(x1, y0));
            p.Stroke();
            for (int s = 0; s < series.Count; s++)
            {
                var v = series[s];
                if (v.Length < 2) continue;
                p.strokeColor = colores[s];
                p.lineWidth = 2;
                p.BeginPath();
                for (int i = 0; i < v.Length; i++)
                {
                    var punto = new Vector2(x0 + (x1 - x0) * i / (v.Length - 1), y0 - (y0 - y1) * (float)((v[i] - min) / (max - min)));
                    if (i == 0) p.MoveTo(punto);
                    else p.LineTo(punto);
                }
                p.Stroke();
            }
        }
    }
}
