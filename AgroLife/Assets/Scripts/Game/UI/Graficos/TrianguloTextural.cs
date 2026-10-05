using UnityEngine;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Triángulo textural (arcilla arriba, arena abajo a la izquierda, limo abajo a la derecha) con la grilla cada 20 % y el punto del lote.</summary>
    public sealed class TrianguloTextural : VisualElement
    {
        const float Margen = 16f;
        static readonly Color Linea = new Color(0.93f, 0.90f, 0.83f, 0.9f);
        static readonly Color Grilla = new Color(0.93f, 0.90f, 0.83f, 0.18f);
        static readonly Color Punto = new Color32(0xE3, 0xC0, 0x4B, 255);

        double arena, limo, arcilla;
        bool hayDato;

        public TrianguloTextural()
        {
            style.width = 220;
            style.height = 200;
            generateVisualContent += Dibujar;
            Etiqueta("Arcilla", 0.5f, 0f, -0.5f);
            Etiqueta("Arena", 0f, 1f, 0f);
            Etiqueta("Limo", 1f, 1f, -1f);
        }

        void Etiqueta(string texto, float x, float y, float trasladoX)
        {
            var l = new Label(texto) { pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.left = Length.Percent(x * 100);
            l.style.top = Length.Percent(y * 100);
            l.style.translate = new Translate(Length.Percent(trasladoX * 100), Length.Percent(y > 0.5f ? -100 : 0));
            l.style.fontSize = 11;
            l.style.color = new Color(0.73f, 0.78f, 0.71f);
            Add(l);
        }

        public void Mostrar(double arena, double limo, double arcilla)
        {
            this.arena = arena;
            this.limo = limo;
            this.arcilla = arcilla;
            hayDato = true;
            MarkDirtyRepaint();
        }

        Vector2 Posicion(double s, double l, double c)
        {
            float w = contentRect.width - 2 * Margen, h = contentRect.height - 2 * Margen;
            var a = new Vector2(Margen, Margen + h);       // 100 % arena
            var b = new Vector2(Margen + w, Margen + h);   // 100 % limo
            var top = new Vector2(Margen + w / 2, Margen); // 100 % arcilla
            return a * (float)(s / 100) + b * (float)(l / 100) + top * (float)(c / 100);
        }

        void Segmento(Painter2D p, Vector2 desde, Vector2 hasta)
        {
            p.BeginPath();
            p.MoveTo(desde);
            p.LineTo(hasta);
            p.Stroke();
        }

        void Dibujar(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineWidth = 1f;
            p.strokeColor = Grilla;
            for (int k = 20; k < 100; k += 20)
            {
                Segmento(p, Posicion(100 - k, 0, k), Posicion(0, 100 - k, k));   // arcilla constante
                Segmento(p, Posicion(k, 100 - k, 0), Posicion(k, 0, 100 - k));   // arena constante
                Segmento(p, Posicion(100 - k, k, 0), Posicion(0, k, 100 - k));   // limo constante
            }
            p.lineWidth = 1.5f;
            p.strokeColor = Linea;
            p.BeginPath();
            p.MoveTo(Posicion(100, 0, 0));
            p.LineTo(Posicion(0, 100, 0));
            p.LineTo(Posicion(0, 0, 100));
            p.ClosePath();
            p.Stroke();
            if (!hayDato) return;
            p.fillColor = Punto;
            p.BeginPath();
            p.Arc(Posicion(arena, limo, arcilla), 5f, Angle.Degrees(0), Angle.Degrees(360));
            p.Fill();
        }
    }
}
