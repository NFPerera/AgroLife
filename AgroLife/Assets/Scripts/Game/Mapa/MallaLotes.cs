using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AgroLife.Game
{
    /// <summary>Todos los lotes en una sola malla, con color por vértice: recolorear el mapa es escribir un arreglo.</summary>
    public sealed class MallaLotes
    {
        public Mesh Mesh { get; }
        readonly int[] inicio, cantidad;
        readonly Color32[] colores;

        MallaLotes(Mesh mesh, int[] inicio, int[] cantidad, Color32[] colores)
        {
            Mesh = mesh;
            this.inicio = inicio;
            this.cantidad = cantidad;
            this.colores = colores;
        }

        public static MallaLotes Construir(IReadOnlyList<Mesh> mallasPorLote)
        {
            var vertices = new List<Vector3>();
            var triangulos = new List<int>();
            var inicio = new int[mallasPorLote.Count];
            var cantidad = new int[mallasPorLote.Count];
            for (int k = 0; k < mallasPorLote.Count; k++)
            {
                var m = mallasPorLote[k];
                inicio[k] = vertices.Count;
                cantidad[k] = m.vertexCount;
                foreach (var i in m.triangles) triangulos.Add(i + inicio[k]);
                vertices.AddRange(m.vertices);
            }
            var colores = new Color32[vertices.Count];
            for (int i = 0; i < colores.Length; i++) colores[i] = new Color32(255, 255, 255, 255);
            var mesh = new Mesh { name = "Lotes", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangulos, 0);
            mesh.SetColors(colores);
            mesh.RecalculateBounds();
            return new MallaLotes(mesh, inicio, cantidad, colores);
        }

        public (int inicio, int cantidad) Rango(int indiceLote) => (inicio[indiceLote], cantidad[indiceLote]);

        public void Pintar(int indiceLote, Color32 color)
        {
            int fin = inicio[indiceLote] + cantidad[indiceLote];
            for (int i = inicio[indiceLote]; i < fin; i++) colores[i] = color;
        }

        public void Aplicar() => Mesh.SetColors(colores);
    }
}
