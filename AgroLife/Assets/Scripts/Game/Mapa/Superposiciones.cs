using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AgroLife.Game
{
    /// <summary>Líneas del mapa (bordes, rutas, caminos, arroyos, contorno) como mallas de topología Lines.</summary>
    public static class Superposiciones
    {
        public const int OrdenLotes = 0, OrdenBordes = 1, OrdenCaminos = 2, OrdenRutas = 3, OrdenArroyos = 4, OrdenContorno = 5, OrdenResaltados = 10;

        /// <summary>Una polilínea por lista de puntos (metros locales); colorDeLinea(i) colorea la i-ésima (blanco si es null).</summary>
        public static Mesh Lineas(IEnumerable<List<int[]>> lineas, Func<int, Color32> colorDeLinea = null)
        {
            var vertices = new List<Vector3>();
            var colores = new List<Color32>();
            var indices = new List<int>();
            int n = 0;
            foreach (var linea in lineas)
            {
                var color = colorDeLinea != null ? colorDeLinea(n) : new Color32(255, 255, 255, 255);
                int primero = vertices.Count;
                foreach (var p in linea)
                {
                    vertices.Add(Escala.AUnidades(p[0], p[1]));
                    colores.Add(color);
                }
                for (int i = primero; i < vertices.Count - 1; i++)
                {
                    indices.Add(i);
                    indices.Add(i + 1);
                }
                n++;
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colores);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static List<int[]> Cerrado(List<int[]> anillo)
        {
            var r = new List<int[]>(anillo) { anillo[0] };
            return r;
        }

        public static MeshRenderer CrearRenderer(Transform padre, string nombre, Mesh mesh, Material material, int orden)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.sortingOrder = orden;
            return r;
        }

        public static void Crear(Transform padre, GeografiaRegion g, Material m)
        {
            CrearRenderer(padre, "Caminos", Lineas(g.Caminos, _ => new Color32(0x6E, 0x6A, 0x5E, 255)), m, OrdenCaminos);
            CrearRenderer(padre, "Rutas", Lineas(g.Rutas, _ => new Color32(0xD8, 0xD2, 0xC4, 255)), m, OrdenRutas);
            CrearRenderer(padre, "Arroyos", Lineas(g.Arroyos, _ => new Color32(0x4F, 0xA3, 0xD9, 255)), m, OrdenArroyos);
            CrearRenderer(padre, "Contorno", Lineas(new[] { Cerrado(g.Contorno) }, _ => new Color32(0xF2, 0xE6, 0xC9, 255)), m, OrdenContorno);
        }
    }
}
