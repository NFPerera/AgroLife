using System.Collections.Generic;
using System.Linq;
using AgroLife.Sim;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Reporte de campaña de un lote (spec §4.7): escalera de pérdidas, margen bruto, resultado y comparación con la zona.</summary>
    public sealed class PanelReporte
    {
        readonly UIJuego ui;
        readonly VisualElement raiz, tabla;
        readonly Label titulo, promedio;
        readonly DropdownField cultivo;
        readonly GraficoEscalera escalera = new GraficoEscalera();
        List<ResultadoCultivo> resultados = new List<ResultadoCultivo>();
        int loteId, campania;

        public bool Abierto => !raiz.ClassListContains("oculto");
        public VisualElement Tabla => tabla;

        public PanelReporte(VisualElement r, UIJuego ui)
        {
            this.ui = ui;
            raiz = r.Q("reporte");
            titulo = raiz.Q<Label>("reporte-titulo");
            promedio = raiz.Q<Label>("reporte-promedio");
            tabla = raiz.Q("reporte-tabla");
            cultivo = raiz.Q<DropdownField>("reporte-cultivo");
            raiz.Q("reporte-escalera").Add(escalera);
            raiz.Q<Button>("reporte-cerrar").clicked += Cerrar;
            cultivo.RegisterValueChangedCallback(_ => Mostrar());
        }

        public void Abrir(int lote, int camp)
        {
            var s = ui.Sim;
            loteId = lote;
            campania = camp;
            resultados = s.Estado.Resultados.Where(x => x.LoteId == lote && x.Campania == camp).ToList();
            if (resultados.Count == 0) return;
            titulo.text = $"Lote {lote} · Campaña {camp}";
            cultivo.choices = resultados.Select(x => FichaLote.NombreCultivo(s, x.CultivoId)).ToList();
            cultivo.SetValueWithoutNotify(cultivo.choices[0]);
            cultivo.style.display = resultados.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            raiz.RemoveFromClassList("oculto");
            Mostrar();
        }

        public void Cerrar() => raiz.AddToClassList("oculto");

        void Fila(string etiqueta, string valor, string termino = null, bool fuerte = false)
        {
            var f = new VisualElement();
            f.AddToClassList("fila");
            var l = new Label(etiqueta);
            l.AddToClassList("etiqueta");
            if (termino != null)
            {
                l.name = "t-" + termino;
                l.AddToClassList("termino");
            }
            var v = new Label(valor);
            v.AddToClassList("valor");
            if (fuerte) v.style.color = (UnityEngine.Color)new UnityEngine.Color32(0xE3, 0xC0, 0x4B, 255);
            f.Add(l);
            f.Add(v);
            tabla.Add(f);
        }

        void Mostrar()
        {
            var s = ui.Sim;
            var r = resultados[System.Math.Max(0, cultivo.index)];
            var rep = s.ReporteLote(loteId, campania);
            double zona = rep.PromedioZonaQqHa.TryGetValue(r.CultivoId, out var z) ? z : 0;
            escalera.Mostrar(r.Cascada, zona);
            promedio.text = zona > 0 ? $"- - - Promedio de la zona para {FichaLote.NombreCultivo(s, r.CultivoId).ToLowerInvariant()}: {Formato.Numero(zona, 1)} qq/ha" : "";

            var m = r.Margen;
            tabla.Clear();
            Fila("Rinde real", $"{Formato.Numero(r.Cascada.RealQqHa, 1)} qq/ha");
            Fila("Toneladas cosechadas", $"{Formato.Numero(r.ToneladasCosechadas, 1)} t");
            Fila("Precio a cosecha", $"{Formato.Usd(r.PrecioCosechaUsdT)}/t");
            Fila("Ingreso bruto", Formato.Usd(m.IngresoBrutoUsd));
            Fila("Comercialización (flete y comisión)", Formato.Usd(-m.ComercializacionUsd));
            Fila("Costos directos (semilla, insumos, labores, tratamientos)", Formato.Usd(-m.CostosDirectosUsd));
            Fila("Cosecha (contratista)", Formato.Usd(-m.CosechaUsd));
            Fila("Margen bruto", $"{Formato.Usd(m.MargenBrutoUsd)} ({Formato.Usd(m.MargenBrutoUsd / r.SuperficieHa)}/ha)", "margen_bruto", true);
            Fila("Rinde de indiferencia", $"{Formato.Numero(r.RindeIndiferenciaQqHa, 1)} qq/ha (real {Formato.Numero(r.Cascada.RealQqHa, 1)})", "rinde_indiferencia");
            var campaniaTitulo = new Label($"Campaña completa del lote");
            campaniaTitulo.AddToClassList("subtitulo");
            tabla.Add(campaniaTitulo);
            Fila("Margen bruto de la campaña", Formato.Usd(rep.MargenBrutoUsd), "margen_bruto");
            Fila("Arrendamiento", Formato.Usd(-rep.ArrendamientoUsd), "arrendamiento");
            Fila("Resultado", Formato.Usd(rep.ResultadoUsd), "resultado", true);
            ui.MarcarTerminos(raiz);
        }
    }
}
