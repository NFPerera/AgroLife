using AgroLife.Sim;
using UnityEngine;

namespace AgroLife.Game
{
    /// <summary>Los JSON de Assets/Data como TextAsset (spec §5): de acá salen los datos de Sim y la geografía del mapa.</summary>
    [CreateAssetMenu(menuName = "AgroLife/Fuente de datos")]
    public sealed class FuenteDatos : ScriptableObject
    {
        public TextAsset Crops, Pests, Economy, Soils, Region, Lotes, Clima, Geografia, Glosario;

        /// <summary>Lanza DatosInvalidosException si algo no valida: el juego no arranca con datos parciales.</summary>
        public DatosJuego CargarDatos() => DatosJuego.Desde(new ArchivosDatos
        {
            Crops = Crops.text, Pests = Pests.text, Economy = Economy.text, Soils = Soils.text,
            Region = Region.text, Lotes = Lotes.text, Clima = Clima.text,
        });

        public GeografiaRegion CargarGeografia() => GeografiaRegion.Desde(Lotes.text, Geografia.text, Region.text);
    }
}
