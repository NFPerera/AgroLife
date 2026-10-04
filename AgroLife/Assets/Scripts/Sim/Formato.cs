using System.Globalization;

namespace AgroLife.Sim
{
    /// <summary>Números para mensajes al jugador: miles con ".", decimales con ",", sin depender de la cultura del sistema.</summary>
    public static class Formato
    {
        static readonly NumberFormatInfo Rioplatense = new NumberFormatInfo
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ",",
            NegativeSign = "-",
        };

        public static string Numero(double valor, int decimales) => valor.ToString("N" + decimales, Rioplatense);

        public static string Usd(double valor) => "US$ " + Numero(valor, 0);
    }
}
