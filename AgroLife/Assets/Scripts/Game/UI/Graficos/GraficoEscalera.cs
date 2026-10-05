using AgroLife.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>
    /// Gráfico en escalera del reporte (spec §4.7): rinde potencial → pérdida por cada causa, en el orden fijo del modelo → rinde real.
    /// Fila de valores arriba, barras con Painter2D en el medio, nombres abajo, y una línea punteada en el promedio de la zona.
    /// </summary>
    public sealed class GraficoEscalera : VisualElement
    {
        static readonly string[] Nombres = { "Potencial", "Fecha", "Agua", "Anegam.", "Temp.", "Nitrógeno", "Fósforo", "Plagas", "Real" };
        static readonly Color Potencial = new Color32(0x8C, 0x9A, 0x86, 255), Perdida = new Color32(0xE0, 0x7A, 0x5F, 255),
                              Real = new Color32(0x3E, 0x8E, 0x41, 255), Promedio = new Color32(0xE3, 0xC0, 0x4B, 255);

        readonly VisualElement barras;
        readonly Label[] valores = new Label[9];
        double[] escalones = new double[9];   // en qq/ha: potencial, 7 pérdidas, real
        double promedio;

        public GraficoEscalera()
        {
            style.flexGrow = 1;
            var filaValores = Fila();
            barras = new VisualElement();
            barras.style.height = 200;
            barras.generateVisualContent += Dibujar;
            Add(barras);
            var filaNombres = Fila();
            for (int i = 0; i < 9; i++)
            {
                valores[i] = Celda(filaValores, "");
                Celda(filaNombres, Nombres[i]);
            }
        }

        VisualElement Fila()
        {
            var f = new VisualElement();
            f.style.flexDirection = FlexDirection.Row;
            Add(f);
            return f;
        }

        static Label Celda(VisualElement fila, string texto)
        {
            var l = new Label(texto);
            l.style.flexGrow = 1;
            l.style.flexBasis = 0;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.fontSize = 12;
            fila.Add(l);
            return l;
        }

        public void Mostrar(Cascada k, double promedioZonaQqHa)
        {
            escalones = new[] { k.PotencialQqHa, k.FechaQq, k.AguaQq, k.AnegamientoQq, k.TemperaturaQq, k.NitrogenoQq, k.FosforoQq, k.PlagasQq, k.RealQqHa };
            promedio = promedioZonaQqHa;
            for (int i = 0; i < 9; i++)
                valores[i].text = (i is > 0 and < 8 && escalones[i] >= 0.05 ? "−" : "") + Formato.Numero(escalones[i], 1);
            barras.MarkDirtyRepaint();
        }

        void Dibujar(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = barras.contentRect.width, h = barras.contentRect.height;
            double max = System.Math.Max(escalones[0], promedio);
            if (max <= 0) return;
            float Y(double qq) => h - (float)(qq / max) * (h - 4);
            float columna = w / 9, ancho = columna * 0.6f;
            double resto = escalones[0];
            for (int i = 0; i < 9; i++)
            {
                float x = columna * i + (columna - ancho) / 2;
                double arriba, abajo;
                Color color;
                if (i == 0) { arriba = escalones[0]; abajo = 0; color = Potencial; }
                else if (i == 8) { arriba = escalones[8]; abajo = 0; color = Real; }
                else { arriba = resto; abajo = resto - escalones[i]; resto = abajo; color = Perdida; }
                float y0 = Y(arriba), y1 = Mathf.Max(Y(abajo), y0 + 1.5f); // una pérdida de 0 se ve como una raya
                p.fillColor = color;
                p.BeginPath();
                p.MoveTo(new Vector2(x, y0));
                p.LineTo(new Vector2(x + ancho, y0));
                p.LineTo(new Vector2(x + ancho, y1));
                p.LineTo(new Vector2(x, y1));
                p.ClosePath();
                p.Fill();
            }
            if (promedio <= 0) return;
            p.strokeColor = Promedio;
            p.lineWidth = 1.5f;
            float yp = Y(promedio);
            for (float x = 0; x < w; x += 12)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(x, yp));
                p.LineTo(new Vector2(Mathf.Min(x + 6, w), yp));
                p.Stroke();
            }
        }
    }
}
