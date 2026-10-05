using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AgroLife.Sim;
using UnityEngine;

namespace AgroLife.Game
{
    public sealed class PartidaGuardada
    {
        public string Ruta, Descripcion;
        public DateTime Modificada;
        public string Error; // distinto de null: no se puede cargar
    }

    /// <summary>Partidas en saves/partida-&lt;semilla&gt;.json (spec §10): se escribe un .tmp y después se reemplaza.</summary>
    public static class Guardado
    {
        static string carpeta;

        public static string Carpeta
        {
            get => carpeta ?? (carpeta = Path.Combine(Application.persistentDataPath, "saves"));
            set => carpeta = value;
        }

        public static string RutaPara(ulong semilla) => Path.Combine(Carpeta, $"partida-{semilla}.json");

        public static void Guardar(Simulation s, string ruta)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            var tmp = ruta + ".tmp";
            File.WriteAllText(tmp, s.Guardar(), new UTF8Encoding(false));
            if (File.Exists(ruta)) File.Replace(tmp, ruta, null);
            else File.Move(tmp, ruta);
        }

        /// <summary>Deja pasar PartidaIncompatibleException (versión, región o archivo dañado).</summary>
        public static Simulation Cargar(DatosJuego datos, string ruta) => Simulation.Cargar(datos, File.ReadAllText(ruta));

        public static List<PartidaGuardada> Listar(DatosJuego datos)
        {
            var r = new List<PartidaGuardada>();
            if (!Directory.Exists(Carpeta)) return r;
            foreach (var ruta in Directory.GetFiles(Carpeta, "partida-*.json"))
            {
                var p = new PartidaGuardada { Ruta = ruta, Modificada = File.GetLastWriteTime(ruta) };
                try
                {
                    var s = Cargar(datos, ruta);
                    p.Descripcion = $"{datos.Region.Nombre} · {s.Estado.Fecha} · {Formato.Usd(s.Estado.SaldoUsd)}";
                }
                catch (Exception e)
                {
                    p.Error = e.Message;
                    p.Descripcion = Path.GetFileName(ruta);
                }
                r.Add(p);
            }
            return r.OrderByDescending(p => p.Modificada).ThenBy(p => p.Ruta, StringComparer.Ordinal).ToList();
        }
    }
}
