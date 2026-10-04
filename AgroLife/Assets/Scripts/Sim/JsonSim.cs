using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AgroLife.Sim
{
    /// <summary>Única configuración de JSON de Sim: datos de región y partidas guardadas.</summary>
    public static class JsonSim
    {
        public static readonly JsonSerializerSettings Ajustes = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Formatting = Formatting.None,
        };

        public static string Serializar(object o) => JsonConvert.SerializeObject(o, Ajustes);

        public static T Deserializar<T>(string json) => JsonConvert.DeserializeObject<T>(json, Ajustes);
    }
}
