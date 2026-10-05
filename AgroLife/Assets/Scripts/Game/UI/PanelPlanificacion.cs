using System.Collections.Generic;
using System.Linq;
using AgroLife.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Formulario de planificación con presupuesto en vivo (spec §4.3): costo, rinde esperado, margen bruto y rinde de indiferencia.</summary>
    public sealed class PanelPlanificacion
    {
        static readonly string[] Opciones = { "trigo", "maiz", "soja1", "doble" };
        static readonly string[] Textos = { "Trigo", "Maíz", "Soja de primera", "Trigo / soja de segunda" };

        readonly UIJuego ui;
        readonly VisualElement raiz, grupoSoja2;
        readonly DropdownField cultivo;
        readonly SliderInt fecha, n, p, p2;
        readonly RadioButtonGroup genetica, destino, genetica2;
        readonly Label titulo, fechaTexto, nTexto, pTexto, p2Texto, costo, rinde, mb, ri, motivo;
        readonly Button confirmar;
        List<Fecha> fechas = new List<Fecha>();
        int loteId;

        public bool Abierto => !raiz.ClassListContains("oculto");

        public PanelPlanificacion(VisualElement r, UIJuego ui)
        {
            this.ui = ui;
            raiz = r.Q("planificacion");
            titulo = raiz.Q<Label>("plan-titulo");
            cultivo = raiz.Q<DropdownField>("plan-cultivo");
            fecha = raiz.Q<SliderInt>("plan-fecha");
            fechaTexto = raiz.Q<Label>("plan-fecha-texto");
            genetica = raiz.Q<RadioButtonGroup>("plan-genetica");
            n = raiz.Q<SliderInt>("plan-n");
            nTexto = raiz.Q<Label>("plan-n-texto");
            p = raiz.Q<SliderInt>("plan-p");
            pTexto = raiz.Q<Label>("plan-p-texto");
            destino = raiz.Q<RadioButtonGroup>("plan-destino");
            grupoSoja2 = raiz.Q("plan-soja2");
            genetica2 = raiz.Q<RadioButtonGroup>("plan-s2-genetica");
            p2 = raiz.Q<SliderInt>("plan-s2-p");
            p2Texto = raiz.Q<Label>("plan-s2-p-texto");
            costo = raiz.Q<Label>("pres-costo");
            rinde = raiz.Q<Label>("pres-rinde");
            mb = raiz.Q<Label>("pres-mb");
            ri = raiz.Q<Label>("pres-ri");
            motivo = raiz.Q<Label>("plan-motivo");
            confirmar = raiz.Q<Button>("plan-confirmar");

            cultivo.choices = Textos.ToList();
            cultivo.RegisterValueChangedCallback(_ => AplicarCultivo());
            fecha.RegisterValueChangedCallback(_ => Actualizar());
            genetica.RegisterValueChangedCallback(_ => Actualizar());
            destino.RegisterValueChangedCallback(_ => Actualizar());
            genetica2.RegisterValueChangedCallback(_ => Actualizar());
            n.RegisterValueChangedCallback(e => Redondear(n, e.newValue, 10));
            p.RegisterValueChangedCallback(e => Redondear(p, e.newValue, 5));
            p2.RegisterValueChangedCallback(e => Redondear(p2, e.newValue, 5));
            confirmar.clicked += Confirmar;
            raiz.Q<Button>("plan-cancelar").clicked += Cerrar;
        }

        string OpcionActual => Opciones[Mathf.Max(0, cultivo.index)];
        string CultivoPrincipal => OpcionActual == "doble" ? "trigo" : OpcionActual;

        void Redondear(SliderInt s, int valor, int paso)
        {
            int v = Mathf.RoundToInt(valor / (float)paso) * paso;
            if (v != valor) s.SetValueWithoutNotify(v);
            Actualizar();
        }

        public void Abrir(int lote)
        {
            loteId = lote;
            titulo.text = $"Planificar el lote {lote}";
            motivo.text = "";
            raiz.RemoveFromClassList("oculto");
            // Arranca en el primer cultivo con fechas disponibles.
            int inicial = 0;
            for (int i = 0; i < Opciones.Length; i++)
                if (FechasValidas(Opciones[i] == "doble" ? "trigo" : Opciones[i]).Count > 0) { inicial = i; break; }
            cultivo.index = inicial;
            AplicarCultivo();
        }

        public void Cerrar() => raiz.AddToClassList("oculto");

        List<Fecha> FechasValidas(string cultivoId)
        {
            var s = ui.Sim;
            var c = s.Datos.Cultivos[cultivoId];
            var (dd, md) = Fecha.ParseDiaMes(c.VentanaSiembra.Desde);
            var (dh, mh) = Fecha.ParseDiaMes(c.VentanaSiembra.Hasta);
            int desde = Fecha.DiaDeCampaniaDe(dd, md), hasta = Fecha.DiaDeCampaniaDe(dh, mh);
            var lote = s.Estado.Lotes[loteId - 1];
            var r = new List<Fecha>();
            for (int i = 1; i <= 365; i++)
            {
                var f = s.Estado.Fecha.MasDias(i);
                if (lote.Tenencia == Tenencia.Arrendado && f > lote.Contrato.Vence) break;
                if (f.DiaDeCampania >= desde && f.DiaDeCampania <= hasta) r.Add(f);
            }
            return r;
        }

        void AplicarCultivo()
        {
            var s = ui.Sim;
            var c = s.Datos.Cultivos[CultivoPrincipal];
            fechas = FechasValidas(c.Id);
            var tipico = c.PlanTipico;
            var (dia, mes) = Fecha.ParseDiaMes(tipico.Siembra);
            int indice = Mathf.Max(0, fechas.FindIndex(f => f.Dia == dia && f.Mes == mes));
            fecha.highValue = Mathf.Max(0, fechas.Count - 1);
            fecha.SetValueWithoutNotify(indice);
            fecha.SetEnabled(fechas.Count > 1);
            genetica.SetValueWithoutNotify(tipico.Genetica == Genetica.Corto ? 0 : 1);
            bool llevaN = c.Nitrogeno != null;
            n.SetEnabled(llevaN);
            n.SetValueWithoutNotify(llevaN ? (int)tipico.NKgHa : 0);
            p.SetValueWithoutNotify((int)tipico.PKgHa);
            destino.SetValueWithoutNotify(0);
            bool doble = OpcionActual == "doble";
            grupoSoja2.EnableInClassList("oculto", !doble);
            var s2 = s.Datos.Cultivos["soja2"].PlanTipico;
            genetica2.SetValueWithoutNotify(s2.Genetica == Genetica.Corto ? 0 : 1);
            p2.SetValueWithoutNotify((int)s2.PKgHa);
            Actualizar();
        }

        /// <summary>El plan que arman los controles. Siempre completo, así el presupuesto nunca recibe un plan a medias.</summary>
        public PlanCultivo PlanActual()
        {
            var plan = new PlanCultivo
            {
                CultivoId = CultivoPrincipal,
                FechaSiembra = fechas.Count > 0 ? fechas[Mathf.Clamp(fecha.value, 0, fechas.Count - 1)] : ui.Sim.Estado.Fecha.MasDias(1),
                Genetica = genetica.value == 0 ? Genetica.Corto : Genetica.Largo,
                NKgHa = n.enabledSelf ? n.value : 0,
                PKgHa = p.value,
                VenderAlCosechar = destino.value != 1,
            };
            if (OpcionActual == "doble")
                plan.Soja2 = new PlanCultivo
                {
                    CultivoId = "soja2", Genetica = genetica2.value == 0 ? Genetica.Corto : Genetica.Largo,
                    PKgHa = p2.value, VenderAlCosechar = plan.VenderAlCosechar,
                };
            return plan;
        }

        public void Actualizar()
        {
            if (!Abierto) return;
            var s = ui.Sim;
            nTexto.text = n.enabledSelf ? $"{n.value} kg N/ha" : "La soja fija su propio nitrógeno";
            pTexto.text = $"{p.value} kg P/ha";
            p2Texto.text = $"{p2.value} kg P/ha";
            if (fechas.Count == 0)
            {
                fechaTexto.text = $"La ventana de siembra de {FichaLote.NombreCultivo(s, CultivoPrincipal).ToLowerInvariant()} ya pasó este año";
                confirmar.SetEnabled(false);
                costo.text = rinde.text = mb.text = ri.text = "—";
                return;
            }
            confirmar.SetEnabled(true);
            var plan = PlanActual();
            fechaTexto.text = plan.FechaSiembra.ToString();
            var pres = s.CalcularPresupuesto(loteId, plan);
            costo.text = Formato.Usd(pres.CostoTotalUsd);
            mb.text = Formato.Usd(pres.MargenBrutoUsd);
            rinde.text = string.Join(" · ", pres.Cultivos.Select(c => $"{Corto(s, c.CultivoId)} {Formato.Numero(c.RindeEsperadoQqHa, 1)} qq/ha"));
            ri.text = string.Join(" · ", pres.Cultivos.Select(c => $"{Corto(s, c.CultivoId)} {Formato.Numero(c.RindeIndiferenciaQqHa, 1)} qq/ha"));
        }

        static string Corto(Simulation s, string id) => id == "soja2" ? "Soja 2ª" : id == "soja1" ? "Soja" : s.Datos.Cultivos[id].Nombre;

        public void Confirmar()
        {
            var r = ui.Sim.PlanificarCultivo(loteId, PlanActual());
            if (!r.Ok)
            {
                motivo.text = r.Motivo;
                return;
            }
            Cerrar();
            ui.Refrescar();
        }
    }
}
