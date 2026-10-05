using System;
using System.Linq;
using AgroLife.Sim;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Ficha lateral del lote (spec §8): datos, triángulo textural, agua útil, cultivo y las acciones según el estado.</summary>
    public sealed class FichaLote
    {
        readonly UIJuego ui;
        readonly VisualElement raiz, acciones, aguaRelleno, filaRinde;
        readonly Label titulo, motivo, tenencia, unidad, clase, ip, fosforo, anterior, distancia, agua, cultivo, rinde;
        readonly TrianguloTextural triangulo = new TrianguloTextural();
        string confirmando; // "arrendar", "comprar" o null
        string firma;       // estado con el que se armaron las acciones
        readonly System.Collections.Generic.List<(TextElement elemento, Func<string> texto)> dinamicos =
            new System.Collections.Generic.List<(TextElement, Func<string>)>();

        public int LoteId { get; private set; }
        public event Action<int> PedirPlanificacion;
        public event Action<int, int> PedirReporte;

        public FichaLote(VisualElement r, UIJuego ui)
        {
            this.ui = ui;
            raiz = r.Q("ficha");
            titulo = raiz.Q<Label>("ficha-titulo");
            motivo = raiz.Q<Label>("ficha-motivo");
            tenencia = raiz.Q<Label>("f-tenencia");
            unidad = raiz.Q<Label>("f-unidad");
            clase = raiz.Q<Label>("f-clase");
            ip = raiz.Q<Label>("f-ip");
            fosforo = raiz.Q<Label>("f-fosforo");
            anterior = raiz.Q<Label>("f-anterior");
            distancia = raiz.Q<Label>("f-distancia");
            agua = raiz.Q<Label>("f-agua");
            aguaRelleno = raiz.Q("f-agua-relleno");
            cultivo = raiz.Q<Label>("f-cultivo");
            rinde = raiz.Q<Label>("f-rinde");
            filaRinde = raiz.Q("f-fila-rinde");
            acciones = raiz.Q("ficha-acciones");
            raiz.Q("ficha-triangulo").Add(triangulo);
            raiz.Q<Button>("ficha-cerrar").clicked += () => ui.SeleccionarLote(0);
        }

        public void Abrir(int loteId)
        {
            if (loteId != LoteId)
            {
                motivo.text = "";
                confirmando = null;
            }
            LoteId = loteId;
            raiz.RemoveFromClassList("oculto");
            Refrescar(ui.Sim);
        }

        public void Cerrar()
        {
            LoteId = 0;
            raiz.AddToClassList("oculto");
        }

        public static string NombreCultivo(Simulation s, string id) => id == null ? "—" : s.Datos.Cultivos[id].Nombre;

        public static string NombreEtapa(Etapa e)
        {
            switch (e)
            {
                case Etapa.Floracion: return "floración";
                case Etapa.Llenado: return "llenado de granos";
                default: return e.ToString().ToLowerInvariant();
            }
        }

        public void Refrescar(Simulation s)
        {
            if (LoteId == 0) return;
            var l = s.Estado.Lotes[LoteId - 1];
            var d = s.Datos.Lote(LoteId);
            titulo.text = $"Lote {LoteId} · {Formato.Numero(d.SuperficieHa, 1)} ha";
            tenencia.text = l.Tenencia == Tenencia.Tercero ? "De terceros" : l.Tenencia == Tenencia.Propio ? "Propio" : $"Arrendado hasta {l.Contrato.Vence}";
            unidad.text = d.UnidadSuelo;
            clase.text = CapasMapa.NombreClase(d.Clase);
            ip.text = Formato.Numero(d.Ip, 1);
            fosforo.text = $"{Formato.Numero(l.PBrayPpm, 1)} ppm";
            anterior.text = NombreCultivo(s, l.CultivoAnterior);
            distancia.text = $"{Formato.Numero(d.DistanciaAcopioKm, 1)} km";
            triangulo.Mostrar(d.Arena, d.Limo, d.Arcilla);
            double fraccion = l.AguaMm / d.AuMaxMm;
            agua.text = $"{Formato.Numero(fraccion * 100, 0)} % ({Formato.Numero(l.AguaMm, 0)} de {Formato.Numero(d.AuMaxMm, 0)} mm)";
            aguaRelleno.style.width = Length.Percent((float)Math.Min(100, fraccion * 100));

            var c = l.Cultivo;
            if (c != null)
            {
                cultivo.text = $"{NombreCultivo(s, c.CultivoId)} · ciclo {(c.Genetica == Genetica.Corto ? "corto" : "largo")} · {NombreEtapa(c.Etapa)} · sembrado el {c.FechaSiembra}"
                               + (c.SegundoCultivo != null ? " · después, soja de segunda" : "");
                rinde.text = $"{Formato.Numero(s.RindeEstimadoQqHa(LoteId), 1)} qq/ha";
            }
            else if (l.Plan != null)
            {
                cultivo.text = $"Siembra planificada: {NombreCultivo(s, l.Plan.CultivoId).ToLowerInvariant()} el {l.Plan.FechaSiembra}"
                               + (l.Plan.Soja2 != null ? ", y soja de segunda después del trigo" : "");
            }
            else cultivo.text = "Sin cultivo";
            filaRinde.style.display = c != null ? DisplayStyle.Flex : DisplayStyle.None;
            ArmarAcciones(s, l);
        }

        /// <summary>Un botón cuyo texto se recalcula en cada refresco (por ejemplo, el precio del arrendamiento, que cambia a diario).</summary>
        Button Boton(Func<string> texto, Action alHacerClic, bool principal = false)
        {
            var b = new Button(alHacerClic) { text = texto() };
            b.AddToClassList("boton");
            if (principal) b.AddToClassList("boton-principal");
            acciones.Add(b);
            dinamicos.Add((b, texto));
            return b;
        }

        Button Boton(string texto, Action alHacerClic, bool principal = false) => Boton(() => texto, alHacerClic, principal);

        /// <summary>
        /// Las acciones se rearman solo si cambió el estado del lote: con el tiempo corriendo hay un refresco por frame,
        /// y un botón recreado entre el PointerDown y el PointerUp pierde el clic.
        /// </summary>
        void ArmarAcciones(Simulation s, EstadoLote l)
        {
            var nueva = string.Join("|", LoteId, l.Tenencia, l.Cultivo?.CultivoId, l.Plan != null, l.Cultivo?.SegundoCultivo != null, confirmando,
                s.Estado.Resultados.Count(r => r.LoteId == LoteId), s.Estado.Terminada);
            if (nueva == firma)
            {
                foreach (var (elemento, texto) in dinamicos) elemento.text = texto();
                return;
            }
            firma = nueva;
            dinamicos.Clear();
            acciones.Clear();
            if (confirmando != null)
            {
                bool arrendar = confirmando == "arrendar";
                Func<string> texto = () => arrendar
                    ? $"¿Arrendar el lote {LoteId} por {Formato.Usd(s.PrecioArrendamientoUsd(LoteId))}? Se paga hoy y vence el {s.VencimientoArrendamiento()}."
                    : $"¿Comprar el lote {LoteId} por {Formato.Usd(s.PrecioCompraUsd(LoteId))}?";
                var pregunta = new Label(texto());
                pregunta.AddToClassList("texto");
                acciones.Add(pregunta);
                dinamicos.Add((pregunta, texto));
                Boton("Confirmar", Confirmar, true);
                Boton("Cancelar", () => { confirmando = null; Refrescar(s); });
                return;
            }
            if (l.Tenencia == Tenencia.Tercero)
            {
                if (l.Cultivo != null)
                {
                    var ocupado = new Label($"Ocupado con {NombreCultivo(s, l.Cultivo.CultivoId).ToLowerInvariant()} hasta la cosecha");
                    ocupado.AddToClassList("texto");
                    acciones.Add(ocupado);
                }
                else
                {
                    Boton(() => $"Arrendar ({Formato.Usd(s.PrecioArrendamientoUsd(LoteId))}, vence {s.VencimientoArrendamiento()})", Arrendar, true);
                    Boton(() => $"Comprar ({Formato.Usd(s.PrecioCompraUsd(LoteId))})", Comprar);
                }
            }
            else
            {
                if (l.Cultivo == null && l.Plan == null) Boton("Planificar", () => PedirPlanificacion?.Invoke(LoteId), true);
                if (l.Plan != null || l.Cultivo?.SegundoCultivo != null) Boton("Cancelar plan", () => Resultado(s.CancelarPlan(LoteId)));
                if (l.Tenencia == Tenencia.Arrendado) Boton(() => $"Comprar ({Formato.Usd(s.PrecioCompraUsd(LoteId))})", Comprar);
            }
            foreach (var campania in s.Estado.Resultados.Where(r => r.LoteId == LoteId).Select(r => r.Campania).Distinct().OrderBy(c => c))
            {
                int camp = campania;
                Boton($"Reporte campaña {camp}", () => PedirReporte?.Invoke(LoteId, camp));
            }
        }

        public void Arrendar() { confirmando = "arrendar"; Refrescar(ui.Sim); }
        public void Comprar() { confirmando = "comprar"; Refrescar(ui.Sim); }

        public void Confirmar()
        {
            var s = ui.Sim;
            var r = confirmando == "arrendar" ? s.ArrendarLote(LoteId) : s.ComprarLote(LoteId);
            confirmando = null;
            Resultado(r);
        }

        void Resultado(Resultado r)
        {
            motivo.text = r.Ok ? "" : r.Motivo;
            ui.Refrescar();
            Refrescar(ui.Sim);
        }
    }
}
