using System;
using AgroLife.Sim;

namespace AgroLife.Game
{
    /// <summary>Lo que el menú le pasa a la escena del mapa (y lo que el mapa le devuelve al menú).</summary>
    public static class Sesion
    {
        public static FuenteDatos Fuente;
        public static DatosJuego Datos;
        public static GeografiaRegion Geografia;
        public static Glosario Glosario;
        public static Simulation Sim;
        public static string RutaGuardado;
        public static string MensajeMenu; // el menú lo muestra al volver y lo limpia

        /// <summary>
        /// Carga y valida datos de Sim, geografía del mapa y glosario una sola vez por ejecución. Cualquier error sale como
        /// DatosInvalidosException (spec §10: el juego no arranca con datos parciales) y no deja la sesión a medias.
        /// </summary>
        public static void CargarDatos(FuenteDatos fuente)
        {
            if (Datos != null && Fuente == fuente) return;
            var datos = fuente.CargarDatos();
            GeografiaRegion geografia;
            Glosario glosario;
            try
            {
                geografia = fuente.CargarGeografia();
            }
            catch (Exception e)
            {
                throw new DatosInvalidosException("Datos de región inválidos:\nmapa (lotes.json, geografia.json, region.json): " + e.Message);
            }
            if (geografia.Lotes == null || geografia.Contorno == null || geografia.Localidades == null || geografia.Acopios == null
                || geografia.Rutas == null || geografia.Caminos == null || geografia.Arroyos == null)
                throw new DatosInvalidosException("Datos de región inválidos:\nmapa: faltan lotes, contorno, caminos, arroyos, localidades o acopios");
            if (geografia.Lotes.Count != datos.Lotes.Count)
                throw new DatosInvalidosException($"Datos de región inválidos:\nmapa: {geografia.Lotes.Count} polígonos para {datos.Lotes.Count} lotes");
            try
            {
                glosario = Glosario.Desde(fuente.Glosario.text);
            }
            catch (Exception e)
            {
                throw new DatosInvalidosException("Datos de región inválidos:\nglosario.json: " + e.Message);
            }
            Fuente = fuente;
            Datos = datos;
            Geografia = geografia;
            Glosario = glosario;
        }

        public static void Olvidar()
        {
            Fuente = null;
            Datos = null;
            Geografia = null;
            Glosario = null;
            Sim = null;
            RutaGuardado = null;
        }
    }
}
