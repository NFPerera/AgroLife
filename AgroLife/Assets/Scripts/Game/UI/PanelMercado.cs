using System.Linq;
using AgroLife.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Mercado (spec §8): precios de hoy, evolución del último año, stock en silobolsa y venta.</summary>
    public sealed class PanelMercado
    {
        static readonly Grano[] Granos = { Grano.Trigo, Grano.Maiz, Grano.Soja };
        static readonly string[] Nombres = { "Trigo", "Maíz", "Soja" };
        static readonly Color[] Colores = { new Color32(0xE3, 0xC0, 0x4B, 255), new Color32(0x8F, 0xD1, 0x8A, 255), new Color32(0x4F, 0xA3, 0xD9, 255) };

        readonly UIJuego ui;
        readonly VisualElement raiz;
        readonly Label[] precios, stocks;
        readonly GraficoLinea grafico = new GraficoLinea();
        readonly DropdownField grano;
        readonly FloatField toneladas;
        readonly Label motivo;

        public bool Abierto => !raiz.ClassListContains("oculto");
        public GraficoLinea Grafico => grafico;

        public PanelMercado(VisualElement r, UIJuego ui)
        {
            this.ui = ui;
            raiz = r.Q("mercado");
            precios = new[] { raiz.Q<Label>("precio-trigo"), raiz.Q<Label>("precio-maiz"), raiz.Q<Label>("precio-soja") };
            stocks = new[] { raiz.Q<Label>("stock-trigo"), raiz.Q<Label>("stock-maiz"), raiz.Q<Label>("stock-soja") };
            raiz.Q("mercado-grafico").Add(grafico);
            grano = raiz.Q<DropdownField>("venta-grano");
            grano.choices = Nombres.ToList();
            grano.index = 2;
            toneladas = raiz.Q<FloatField>("venta-t");
            motivo = raiz.Q<Label>("mercado-motivo");
            raiz.Q<Button>("mercado-cerrar").clicked += Cerrar;
            raiz.Q<Button>("venta-btn").clicked += () => Vender(toneladas.value);
            raiz.Q<Button>("venta-todo").clicked += () => Vender(ui.Sim.Estado.StockT[Granos[grano.index]]);
        }

        public void Abrir()
        {
            motivo.text = "";
            raiz.RemoveFromClassList("oculto");
            Refrescar(ui.Sim);
        }

        public void Cerrar() => raiz.AddToClassList("oculto");

        public void Refrescar(Simulation s)
        {
            if (!Abierto) return;
            var comision = s.Datos.Economia.ComisionVenta;
            for (int i = 0; i < Granos.Length; i++)
            {
                double precio = s.Estado.Precios.PrecioUsdT[Granos[i]];
                precios[i].text = $"{Formato.Usd(precio)}/t · {Formato.Usd(precio / 10)}/qq";
                double t = s.Estado.StockT[Granos[i]];
                stocks[i].text = $"{Formato.Numero(t, 1)} t · vale {Formato.Usd(t * precio * (1 - comision))}";
            }
            var h = s.Estado.Precios.Historial;
            var ultimos = h.Skip(System.Math.Max(0, h.Count - 365)).ToList();
            if (ultimos.Count == 0) return;
            grafico.Mostrar(Nombres, Colores, new[]
            {
                ultimos.Select(d => d.TrigoUsdT).ToArray(),
                ultimos.Select(d => d.MaizUsdT).ToArray(),
                ultimos.Select(d => d.SojaUsdT).ToArray(),
            }, ultimos[0].Fecha.ToString(), ultimos[ultimos.Count - 1].Fecha.ToString());
        }

        public void Vender(double t)
        {
            var r = ui.Sim.VenderGrano(Granos[grano.index], t);
            motivo.text = r.Ok ? "" : r.Motivo;
            ui.Refrescar();
            Refrescar(ui.Sim);
        }
    }
}
