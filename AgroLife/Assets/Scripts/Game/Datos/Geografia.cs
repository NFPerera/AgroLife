using System.Collections.Generic;
using AgroLife.Sim;
using UnityEngine;

namespace AgroLife.Game
{
    /// <summary>1 unidad de Unity = 100 m. Los datos vienen en metros locales del origen UTM de region.json.</summary>
    public static class Escala
    {
        public const float MetrosPorUnidad = 100f;
        public static Vector2 AUnidades(int xMetros, int yMetros) => new Vector2(xMetros / MetrosPorUnidad, yMetros / MetrosPorUnidad);
    }

    public sealed class LoteForma { public int Id; public List<int[]> Poligono; }
    public sealed class Localidad { public string Nombre, Tipo; public int X, Y; }

    /// <summary>Lo que el mapa necesita y Sim no: polígonos de lotes, contorno, rutas, caminos, arroyos, localidades y acopios.</summary>
    public sealed class GeografiaRegion
    {
        public List<LoteForma> Lotes;   // índice = id − 1
        public List<int[]> Contorno;
        public List<List<int[]>> Rutas, Caminos, Arroyos;
        public List<Localidad> Localidades, Acopios;

        sealed class ArchivoLotes { public List<LoteForma> Lotes; }
        sealed class ArchivoGeografia { public List<int[]> Contorno; public List<List<int[]>> Rutas, Caminos, Arroyos; }
        sealed class ArchivoRegion { public List<Localidad> Localidades, Acopios; }

        public static GeografiaRegion Desde(string lotesJson, string geografiaJson, string regionJson)
        {
            var g = JsonSim.Deserializar<ArchivoGeografia>(geografiaJson);
            var r = JsonSim.Deserializar<ArchivoRegion>(regionJson);
            return new GeografiaRegion
            {
                Lotes = JsonSim.Deserializar<ArchivoLotes>(lotesJson).Lotes,
                Contorno = g.Contorno, Rutas = g.Rutas, Caminos = g.Caminos, Arroyos = g.Arroyos,
                Localidades = r.Localidades, Acopios = r.Acopios,
            };
        }
    }
}
