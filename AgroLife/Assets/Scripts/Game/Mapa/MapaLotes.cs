using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AgroLife.Game
{
    /// <summary>
    /// Un PolygonCollider2D por lote para el clic; se dibujan todos juntos en una malla con color por vértice
    /// (spec §8: la variante combinada, porque el mapa se recolorea a diario con miles de lotes).
    /// </summary>
    public sealed class MapaLotes : MonoBehaviour
    {
        static readonly Color32 ColorBorde = new Color32(0x2A, 0x3A, 0x2E, 255);
        static readonly Color32 ColorSeleccion = new Color32(255, 255, 255, 255);
        static readonly Color32 ColorPropio = new Color32(0x4F, 0xA3, 0xFF, 255);
        static readonly Color32 ColorArrendado = new Color32(0xFF, 0x9F, 0x40, 255);

        public Material Material;

        GeografiaRegion geografia;
        MallaLotes malla;
        MeshFilter resaltados;
        readonly Dictionary<Collider2D, int> ids = new Dictionary<Collider2D, int>();

        public Rect Limites { get; private set; }

        public void Construir(GeografiaRegion g)
        {
            geografia = g;
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(transform, false);
            var mallas = new List<Mesh>(g.Lotes.Count);
            foreach (var lote in g.Lotes)
            {
                var go = new GameObject("Lote " + lote.Id);
                go.transform.SetParent(colliders, false);
                var col = go.AddComponent<PolygonCollider2D>();
                col.points = lote.Poligono.Select(p => Escala.AUnidades(p[0], p[1])).ToArray();
                mallas.Add(col.CreateMesh(false, false));
                ids[col] = lote.Id;
            }
            malla = MallaLotes.Construir(mallas);
            foreach (var m in mallas) Destroy(m);

            Superposiciones.CrearRenderer(transform, "Lotes", malla.Mesh, Material, Superposiciones.OrdenLotes);
            var bordes = Superposiciones.Lineas(g.Lotes.Select(l => Superposiciones.Cerrado(l.Poligono)), _ => ColorBorde);
            Superposiciones.CrearRenderer(transform, "Bordes", bordes, Material, Superposiciones.OrdenBordes);
            Superposiciones.Crear(transform, g, Material);
            resaltados = Superposiciones.CrearRenderer(transform, "Resaltados", new Mesh(), Material, Superposiciones.OrdenResaltados)
                .GetComponent<MeshFilter>();

            var xs = g.Contorno.Select(p => p[0] / Escala.MetrosPorUnidad).ToList();
            var ys = g.Contorno.Select(p => p[1] / Escala.MetrosPorUnidad).ToList();
            var r = Rect.MinMaxRect(xs.Min(), ys.Min(), xs.Max(), ys.Max());
            Limites = new Rect(r.x - r.width * 0.05f, r.y - r.height * 0.05f, r.width * 1.1f, r.height * 1.1f);
        }

        public int LoteEn(Vector2 mundo)
        {
            var c = Physics2D.OverlapPoint(mundo);
            return c != null && ids.TryGetValue(c, out var id) ? id : 0;
        }

        public void Pintar(Func<int, Color32> colorPorId)
        {
            for (int i = 0; i < geografia.Lotes.Count; i++) malla.Pintar(i, colorPorId(i + 1));
            malla.Aplicar();
        }

        public void Resaltar(int idSeleccionado, IReadOnlyCollection<int> propios, IReadOnlyCollection<int> arrendados)
        {
            var lineas = new List<List<int[]>>();
            var colores = new List<Color32>();
            void Agregar(int id, Color32 c)
            {
                lineas.Add(Superposiciones.Cerrado(geografia.Lotes[id - 1].Poligono));
                colores.Add(c);
            }
            foreach (var id in arrendados) Agregar(id, ColorArrendado);
            foreach (var id in propios) Agregar(id, ColorPropio);
            if (idSeleccionado > 0) Agregar(idSeleccionado, ColorSeleccion);
            var anterior = resaltados.sharedMesh;
            resaltados.sharedMesh = Superposiciones.Lineas(lineas, i => colores[i]);
            if (anterior != null) Destroy(anterior);
        }
    }
}
