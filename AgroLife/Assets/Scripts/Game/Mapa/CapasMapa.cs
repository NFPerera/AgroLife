using System.Collections.Generic;
using System.Text.RegularExpressions;
using AgroLife.Sim;
using UnityEngine;

namespace AgroLife.Game
{
    public enum Capa { Estado, ClaseTextural, Ip, Agua, RindeEstimado }

    /// <summary>Color de cada lote según la capa del mapa (spec §8): estilo plano, sin sprites.</summary>
    public static class CapasMapa
    {
        public static readonly Color32 SueloDesnudo = new Color32(0x8C, 0x7A, 0x5B, 255);
        static readonly Color32 Maduro = new Color32(0xE3, 0xC0, 0x4B, 255);
        static readonly Color32 SinDato = new Color32(0x4A, 0x4A, 0x4A, 255);
        static readonly Color32 Rojo = new Color32(0xB5, 0x45, 0x2F, 255), Amarillo = new Color32(0xE3, 0xC0, 0x4B, 255), Verde = new Color32(0x3E, 0x8E, 0x41, 255);
        static readonly Color32 Seco = new Color32(0x8C, 0x5A, 0x2B, 255), Medio = new Color32(0xE8, 0xE1, 0xC4, 255), Humedo = new Color32(0x2C, 0x6F, 0xB5, 255);

        static readonly Dictionary<string, (Color32 claro, Color32 oscuro)> PorCultivo = new Dictionary<string, (Color32, Color32)>
        {
            ["trigo"] = (new Color32(0xB9, 0xD6, 0x7A, 255), new Color32(0x4E, 0x7A, 0x2A, 255)),
            ["maiz"] = (new Color32(0x8F, 0xD1, 0x8A, 255), new Color32(0x1F, 0x6B, 0x2E, 255)),
            ["soja1"] = (new Color32(0xA8, 0xE0, 0xB0, 255), new Color32(0x2F, 0x7F, 0x4F, 255)),
            ["soja2"] = (new Color32(0xA8, 0xE0, 0xB0, 255), new Color32(0x2F, 0x7F, 0x4F, 255)),
        };

        // En el orden de ClaseTextural: de la arena (amarillos) a la arcilla (rojizos).
        static readonly Color32[] PorClase =
        {
            new Color32(0xF2, 0xE3, 0x94, 255), new Color32(0xE8, 0xD2, 0x7A, 255), new Color32(0xD9, 0xC0, 0x6A, 255), new Color32(0xC9, 0xA5, 0x5E, 255),
            new Color32(0xB7, 0xB2, 0x6A, 255), new Color32(0xA6, 0xB8, 0x7A, 255), new Color32(0xC9, 0x8B, 0x5A, 255), new Color32(0xB9, 0x77, 0x4F, 255),
            new Color32(0xA8, 0x6E, 0x55, 255), new Color32(0xB5, 0x60, 0x4A, 255), new Color32(0x9E, 0x50, 0x48, 255), new Color32(0x8A, 0x3E, 0x3A, 255),
        };

        public static Color32 Gradiente(float t, params Color32[] paradas)
        {
            t = Mathf.Clamp01(t);
            float tramo = t * (paradas.Length - 1);
            int i = Mathf.Min((int)tramo, paradas.Length - 2);
            return Color32.Lerp(paradas[i], paradas[i + 1], tramo - i);
        }

        public static string NombreClase(ClaseTextural c) =>
            Regex.Replace(c.ToString(), "(?<=[a-z])([A-Z])", m => " " + m.Value.ToLowerInvariant());

        public static Color32 Color(Capa capa, int loteId, Simulation s)
        {
            var lote = s.Estado.Lotes[loteId - 1];
            var datos = s.Datos.Lote(loteId);
            var cultivo = lote.Cultivo;
            switch (capa)
            {
                case Capa.Estado:
                    if (cultivo == null) return SueloDesnudo;
                    if (cultivo.Etapa == Etapa.Madurez) return Maduro;
                    var c = s.Datos.Cultivos[cultivo.CultivoId];
                    float progreso = (float)(cultivo.GradosDia / ModeloCultivo.Umbrales(cultivo, c).Madurez);
                    var par = PorCultivo[cultivo.CultivoId];
                    return Color32.Lerp(par.claro, par.oscuro, Mathf.Clamp01(progreso));
                case Capa.ClaseTextural:
                    return PorClase[(int)datos.Clase];
                case Capa.Ip:
                    return Gradiente((float)(datos.Ip / 100), Rojo, Amarillo, Verde);
                case Capa.Agua:
                    return Gradiente((float)(lote.AguaMm / datos.AuMaxMm), Seco, Medio, Humedo);
                default: // RindeEstimado
                    if (cultivo == null || cultivo.RindeAlcanzableQqHa <= 0) return SinDato;
                    return Gradiente((float)(s.RindeEstimadoQqHa(loteId) / cultivo.RindeAlcanzableQqHa), Rojo, Amarillo, Verde);
            }
        }

        public static string Titulo(Capa capa)
        {
            switch (capa)
            {
                case Capa.Estado: return "Estado";
                case Capa.ClaseTextural: return "Clase textural";
                case Capa.Ip: return "IP";
                case Capa.Agua: return "Agua útil (%)";
                default: return "Rinde estimado";
            }
        }

        public static IReadOnlyList<(string texto, Color32 color)> Leyenda(Capa capa)
        {
            switch (capa)
            {
                case Capa.Estado:
                    return new[] { ("Suelo desnudo", SueloDesnudo), ("Trigo", PorCultivo["trigo"].oscuro), ("Maíz", PorCultivo["maiz"].oscuro),
                                   ("Soja", PorCultivo["soja1"].oscuro), ("Madurez", Maduro) };
                case Capa.ClaseTextural:
                    var r = new List<(string, Color32)>();
                    for (int i = 0; i < PorClase.Length; i++) r.Add((NombreClase((ClaseTextural)i), PorClase[i]));
                    return r;
                case Capa.Ip:
                    return new[] { ("0", Rojo), ("50", Amarillo), ("100", Verde) };
                case Capa.Agua:
                    return new[] { ("0 %", Seco), ("50 %", Medio), ("100 % o más", Humedo) };
                default:
                    return new[] { ("Sin cultivo", SinDato), ("0 % del alcanzable", Rojo), ("50 %", Amarillo), ("100 %", Verde) };
            }
        }
    }
}
