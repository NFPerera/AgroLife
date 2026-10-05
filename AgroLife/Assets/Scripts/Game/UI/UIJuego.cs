using System.Collections.Generic;
using System.Linq;
using AgroLife.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace AgroLife.Game
{
    /// <summary>Raíz de la escena del mapa: arma la partida, cablea los paneles y repinta el mapa como mucho una vez por frame.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UIJuego : MonoBehaviour
    {
        public FuenteDatos Fuente; // con Play directo en Mapa (sin pasar por el menú), arranca una partida con semilla 1
        public MapaLotes Mapa;
        public CamaraMapa Camara;
        public ControladorTiempo Tiempo;

        public Capa CapaActual { get; private set; } = Capa.Estado;
        public Simulation Sim => Sesion.Sim;
        public int LoteSeleccionado { get; private set; }
        public VisualElement Raiz { get; private set; }

        BarraSuperior barra;
        Notificaciones notificaciones;
        FichaLote ficha;
        PanelPlanificacion planificacion;
        DialogoEvento dialogo;
        PanelMercado mercado;
        PanelReporte reporte;
        Glosario glosario;
        Label tooltip;
        VisualElement leyenda;
        readonly List<(Label etiqueta, Vector3 mundo)> etiquetas = new List<(Label, Vector3)>();
        bool pintarPendiente;
        Vector2 inicioClic;
        Camera cam;

        public Notificaciones Notificaciones => notificaciones;
        public FichaLote Ficha => ficha;
        public PanelPlanificacion Planificacion => planificacion;
        public DialogoEvento Dialogo => dialogo;
        public PanelMercado Mercado => mercado;
        public PanelReporte Reporte => reporte;
        public BarraSuperior Barra => barra;

        /// <summary>true mientras un diálogo pide una respuesta: el tiempo no se puede reanudar.</summary>
        public bool Bloqueado { get; set; }

        void Start()
        {
            cam = Camara.GetComponent<Camera>();
            if (Sesion.Sim == null)
            {
                Sesion.CargarDatos(Fuente);
                Sesion.Sim = Simulation.Nueva(Sesion.Datos, 1);
                Sesion.RutaGuardado = Guardado.RutaPara(1);
            }
            var doc = GetComponent<UIDocument>();
            doc.rootVisualElement.pickingMode = PickingMode.Ignore;
            Raiz = doc.rootVisualElement.Q("juego");

            Mapa.Construir(Sesion.Geografia);
            Camara.Limites = Mapa.Limites;
            Camara.PunteroSobreUI = PunteroSobreUI;
            Tiempo.Sim = Sim;
            Tiempo.Eventos += AlRecibirEventos;
            Tiempo.CambioVelocidad += _ => RefrescarBarra();

            barra = new BarraSuperior(Raiz, this);
            notificaciones = new Notificaciones(Raiz);
            ficha = new FichaLote(Raiz, this);
            AlRefrescar += ficha.Refrescar;
            planificacion = new PanelPlanificacion(Raiz, this);
            ficha.PedirPlanificacion += planificacion.Abrir;
            AlRefrescar += _ => planificacion.Actualizar();
            dialogo = new DialogoEvento(Raiz, this);
            mercado = new PanelMercado(Raiz, this);
            barra.Mercado += mercado.Abrir;
            AlRefrescar += mercado.Refrescar;
            reporte = new PanelReporte(Raiz, this);
            ficha.PedirReporte += reporte.Abrir;
            notificaciones.Click += AbrirReporteDeCosecha;
            glosario = Sesion.Glosario;
            tooltip = Raiz.Q<Label>("tooltip");
            glosario.Marcar(Raiz, tooltip);
            barra.Guardar += GuardarPartida;
            barra.Menu += () => dialogo.Preguntar("Volver al menú", "Lo que no guardaste se pierde. ¿Volver al menú?", "Volver",
                () => SceneManager.LoadScene("Menu"));
            leyenda = Raiz.Q("leyenda");
            CrearEtiquetas();
            ArmarLeyenda();

            AlRecibirEventos(Sim.EventosIniciales.ToList());
            if (Sim.EventosIniciales.Count == 0) dialogo.MostrarPendientes(); // partida cargada
            Refrescar();
        }

        void AlRecibirEventos(List<Evento> eventos)
        {
            foreach (var e in eventos)
            {
                if (e.Pausa) dialogo.Mostrar(e);
                else notificaciones.Agregar(e);
            }
            Refrescar();
        }

        public void GuardarPartida()
        {
            try
            {
                Guardado.Guardar(Sim, Sesion.RutaGuardado);
                notificaciones.Agregar("Partida guardada");
            }
            catch (System.Exception e)
            {
                notificaciones.Agregar("No se pudo guardar: " + e.Message); // la partida anterior queda intacta (se escribe un .tmp)
            }
        }

        /// <summary>Al hacer clic en el aviso de una cosecha del jugador, abre su reporte.</summary>
        void AbrirReporteDeCosecha(Evento e)
        {
            if (e.Tipo != TipoEvento.Cosecha || e.LoteId == 0) return;
            var r = Sim.Estado.Resultados.LastOrDefault(x => x.LoteId == e.LoteId && x.FechaCosecha == e.Fecha);
            if (r != null) reporte.Abrir(r.LoteId, r.Campania);
        }

        /// <summary>Marca con tooltip del glosario los términos (clase "termino", name "t-&lt;id&gt;") de un panel armado por código.</summary>
        public void MarcarTerminos(VisualElement panel) => glosario?.Marcar(panel, tooltip);

        public void CambiarCapa(Capa c)
        {
            CapaActual = c;
            ArmarLeyenda();
            Refrescar();
        }

        public void SeleccionarLote(int id)
        {
            LoteSeleccionado = id;
            if (id == 0) ficha.Cerrar();
            else ficha.Abrir(id);
            Refrescar();
        }

        public void Refrescar() => pintarPendiente = true;

        void RefrescarBarra() => barra.Refrescar(Sim, Tiempo.Velocidad, Bloqueado);

        /// <summary>¿El puntero (coordenadas de pantalla del Input System, origen abajo a la izquierda) está sobre algún panel?</summary>
        public bool PunteroSobreUI(Vector2 posicionPantalla)
        {
            var panel = Raiz?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(posicionPantalla.x, Screen.height - posicionPantalla.y));
            var elegido = panel.Pick(p);
            return elegido != null && elegido.pickingMode == PickingMode.Position && elegido != Raiz;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var pos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) inicioClic = pos;
            if (mouse.leftButton.wasReleasedThisFrame && (pos - inicioClic).sqrMagnitude < 25 && !PunteroSobreUI(pos))
            {
                var mundo = cam.ScreenToWorldPoint(pos);
                SeleccionarLote(Mapa.LoteEn(mundo));
            }
        }

        void LateUpdate()
        {
            if (pintarPendiente)
            {
                pintarPendiente = false;
                var s = Sim;
                Mapa.Pintar(id => CapasMapa.Color(CapaActual, id, s));
                var propios = s.Estado.Lotes.Where(l => l.Tenencia == Tenencia.Propio).Select(l => l.Id).ToList();
                var arrendados = s.Estado.Lotes.Where(l => l.Tenencia == Tenencia.Arrendado).Select(l => l.Id).ToList();
                Mapa.Resaltar(LoteSeleccionado, propios, arrendados);
                RefrescarBarra();
                AlRefrescar?.Invoke(s);
            }
            foreach (var (etiqueta, mundo) in etiquetas)
            {
                var p = RuntimePanelUtils.CameraTransformWorldToPanel(Raiz.panel, mundo, cam);
                etiqueta.style.left = p.x;
                etiqueta.style.top = p.y;
            }
        }

        /// <summary>Los paneles abiertos se refrescan acá (una vez por frame con cambios).</summary>
        public event System.Action<Simulation> AlRefrescar;

        void CrearEtiquetas()
        {
            var capa = Raiz.Q("etiquetas");
            var conAcopio = new HashSet<string>(Sesion.Geografia.Acopios.Select(a => a.X + "," + a.Y));
            foreach (var loc in Sesion.Geografia.Localidades)
            {
                var texto = (conAcopio.Contains(loc.X + "," + loc.Y) ? "▲ " : "") + loc.Nombre;
                var l = new Label(texto) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("etiqueta-localidad");
                capa.Add(l);
                var u = Escala.AUnidades(loc.X, loc.Y);
                etiquetas.Add((l, new Vector3(u.x, u.y, 0)));
            }
        }

        void ArmarLeyenda()
        {
            leyenda.Clear();
            var titulo = new Label(CapasMapa.Titulo(CapaActual));
            titulo.AddToClassList("subtitulo");
            leyenda.Add(titulo);
            foreach (var (texto, color) in CapasMapa.Leyenda(CapaActual))
            {
                var fila = new VisualElement();
                fila.AddToClassList("leyenda-fila");
                var muestra = new VisualElement();
                muestra.AddToClassList("leyenda-color");
                muestra.style.backgroundColor = (Color)color;
                fila.Add(muestra);
                fila.Add(new Label(texto));
                leyenda.Add(fila);
            }
        }
    }
}
