using System;
using System.Collections.Generic;
using System.Linq;
using AgroLife.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Diálogos que frenan el juego (spec §4): inicio de campaña con pronóstico, plaga sobre el umbral, saldo cerca del límite y quiebra.</summary>
    public sealed class DialogoEvento
    {
        readonly UIJuego ui;
        readonly VisualElement raiz, detalle, terminoUmbral;
        readonly Label titulo, texto, motivo;
        readonly Button si, no;
        readonly Queue<Action> cola = new Queue<Action>();
        Action alSi, alNo;

        public bool Abierto { get; private set; }

        public DialogoEvento(VisualElement r, UIJuego ui)
        {
            this.ui = ui;
            raiz = r.Q("dialogo");
            titulo = raiz.Q<Label>("dialogo-titulo");
            texto = raiz.Q<Label>("dialogo-texto");
            detalle = raiz.Q("dialogo-detalle");
            motivo = raiz.Q<Label>("dialogo-motivo");
            si = raiz.Q<Button>("dialogo-si");
            no = raiz.Q<Button>("dialogo-no");
            terminoUmbral = raiz.Q("t-umbral_danio");
            si.clicked += () => alSi?.Invoke();
            no.clicked += () => alNo?.Invoke();
        }

        public void Mostrar(Evento e) => Encolar(() => Abrir(e));

        /// <summary>Pregunta genérica (por ejemplo, confirmar la vuelta al menú).</summary>
        public void Preguntar(string tituloTexto, string cuerpo, string textoSi, Action accionSi, string textoNo = "Cancelar") =>
            Encolar(() => Armar(tituloTexto, cuerpo, textoSi, () => { Siguiente(); accionSi(); }, textoNo, Siguiente));

        /// <summary>Al cargar una partida: si ya había terminado, la quiebra; si no, todas las decisiones de plaga pendientes, de menor a mayor id.</summary>
        public void MostrarPendientes()
        {
            if (ui.Sim.Estado.Terminada)
            {
                Mostrar(new Evento { Tipo = TipoEvento.Quiebra, Fecha = ui.Sim.Estado.Fecha, Pausa = true,
                    Mensaje = "Quiebra: el saldo bajó del límite de descubierto. La partida terminó." });
                return;
            }
            EncolarDecisiones();
            if (!Abierto) Siguiente();
        }

        void EncolarDecisiones()
        {
            foreach (var lote in ui.Sim.Estado.Lotes.Where(l => l.Decision != null))
            {
                var e = new Evento { Tipo = TipoEvento.Plaga, LoteId = lote.Id, Fecha = ui.Sim.Estado.Fecha, Pausa = true };
                cola.Enqueue(() => Abrir(e));
            }
        }

        void Encolar(Action abrir)
        {
            cola.Enqueue(abrir);
            if (!Abierto) Siguiente();
        }

        void Siguiente()
        {
            motivo.text = "";
            // Nunca se cierra con decisiones sin resolver: el tiempo quedaría frenado sin ningún diálogo.
            if (cola.Count == 0 && ui.Sim.Estado.Lotes.Exists(l => l.Decision != null)) EncolarDecisiones();
            if (cola.Count == 0)
            {
                Abierto = false;
                ui.Bloqueado = false;
                raiz.AddToClassList("oculto");
                ui.Refrescar();
                return;
            }
            Abierto = true;
            ui.Bloqueado = true;
            ui.Tiempo.CambiarVelocidad(Velocidad.Pausa);
            raiz.RemoveFromClassList("oculto");
            cola.Dequeue()();
            ui.Refrescar();
        }

        void Armar(string t, string cuerpo, string textoSi, Action accionSi, string textoNo = null, Action accionNo = null)
        {
            titulo.text = t;
            texto.text = cuerpo;
            detalle.Clear();
            terminoUmbral.AddToClassList("oculto");
            si.text = textoSi;
            alSi = accionSi;
            no.style.display = textoNo != null ? DisplayStyle.Flex : DisplayStyle.None;
            no.text = textoNo ?? "";
            alNo = accionNo;
        }

        void Fila(string etiqueta, string valor, VisualElement etiquetaElemento = null)
        {
            var fila = new VisualElement();
            fila.AddToClassList("fila");
            if (etiquetaElemento != null) fila.Add(etiquetaElemento);
            else
            {
                var l = new Label(etiqueta);
                l.AddToClassList("etiqueta");
                fila.Add(l);
            }
            var v = new Label(valor);
            v.AddToClassList("valor");
            fila.Add(v);
            detalle.Add(fila);
        }

        void Abrir(Evento e)
        {
            var s = ui.Sim;
            switch (e.Tipo)
            {
                case TipoEvento.InicioCampania:
                    Armar($"Campaña {e.Fecha.Campania}",
                        "Pronóstico de El Niño / La Niña para esta campaña. El Niño suele traer más lluvia de primavera a otoño en la región pampeana; La Niña, menos.",
                        "Seguir", Siguiente);
                    foreach (var (fase, nombre) in new[] { (FaseEnso.Nino, "El Niño"), (FaseEnso.Neutro, "Neutro"), (FaseEnso.Nina, "La Niña") })
                        Fila(nombre, $"{Formato.Numero(s.Estado.Clima.Pronostico[fase] * 100, 0)} %");
                    break;

                case TipoEvento.Plaga:
                    var lote = s.Estado.Lotes[e.LoteId - 1];
                    var d = lote.Decision;
                    if (d == null) { Siguiente(); return; } // ya se decidió
                    var plaga = s.Datos.Plagas.First(p => p.Id == d.PlagaId);
                    Armar($"{plaga.Nombre} en el lote {e.LoteId}",
                        $"La plaga superó el umbral en {FichaLote.NombreCultivo(s, lote.Cultivo.CultivoId).ToLowerInvariant()}. ¿Aplicás un tratamiento?",
                        "Aplicar", () => Decidir(e.LoteId, true), "No aplicar", () => Decidir(e.LoteId, false));
                    Fila("Nivel", $"{Formato.Numero(d.Nivel, 1)} {plaga.Unidad}");
                    terminoUmbral.RemoveFromClassList("oculto");
                    Fila(null, $"{Formato.Numero(d.Umbral, 1)} {plaga.Unidad}", terminoUmbral);
                    Fila("Costo de aplicar", Formato.Usd(d.CostoUsd));
                    Fila("Pérdida esperada si no se aplica", $"{Formato.Numero(d.PerdidaEsperadaQq, 0)} qq ({Formato.Usd(d.PerdidaEsperadaUsd)})");
                    break;

                case TipoEvento.Quiebra:
                    Armar("Quiebra", e.Mensaje, "Volver al menú", () =>
                    {
                        Sesion.MensajeMenu = "La partida terminó por quiebra";
                        SceneManager.LoadScene("Menu");
                    });
                    break;

                case TipoEvento.SaldoCercaDelLimite:
                    Armar("Saldo cerca del límite", e.Mensaje, "Entendido", Siguiente);
                    break;

                default:
                    Armar(e.Tipo.ToString(), e.Mensaje, "Entendido", Siguiente);
                    break;
            }
        }

        public void Decidir(int loteId, bool aplicar)
        {
            var r = ui.Sim.DecidirTratamiento(loteId, aplicar);
            if (!r.Ok)
            {
                motivo.text = r.Motivo; // el diálogo sigue abierto
                return;
            }
            Siguiente();
        }

        public void Aceptar() => alSi?.Invoke(); // para verificar desde el Editor
    }
}
