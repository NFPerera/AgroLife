using System;
using Newtonsoft.Json;

namespace AgroLife.Sim
{
    /// <summary>
    /// Día del calendario de la partida: 365 días, sin bisiestos. El año cambia el 1/1;
    /// la campaña N va del 1/5 del Año N al 30/4 del Año N+1.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public readonly struct Fecha : IEquatable<Fecha>, IComparable<Fecha>
    {
        static readonly int[] DiasPorMes = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        static readonly int[] DiaInicioMes = Acumulados();
        const int DiaDel1DeMayo = 120;

        /// <summary>Días desde el 1/1 del Año 1 (0 = 1/1 Año 1).</summary>
        [JsonProperty] public readonly int Absoluto;

        [JsonConstructor]
        public Fecha(int absoluto) { Absoluto = absoluto; }

        public static Fecha Crear(int dia, int mes, int anio) =>
            new Fecha((anio - 1) * 365 + DiaInicioMes[mes - 1] + dia - 1);

        public static Fecha EnCampania(int campania, int dia, int mes) =>
            Crear(1, 5, campania).MasDias(DiaDeCampaniaDe(dia, mes));

        public static int DiaDeCampaniaDe(int dia, int mes) =>
            (DiaInicioMes[mes - 1] + dia - 1 - DiaDel1DeMayo + 365) % 365;

        public static int DiasDelMes(int mes) => DiasPorMes[mes - 1];

        public static (int dia, int mes) ParseDiaMes(string ddmm)
        {
            var partes = ddmm.Split('/');
            return (int.Parse(partes[0]), int.Parse(partes[1]));
        }

        public int Anio => Absoluto / 365 + 1;
        public int DiaDelAnio => Absoluto % 365;
        public int Mes
        {
            get
            {
                int m = 11;
                while (DiaInicioMes[m] > DiaDelAnio) m--;
                return m + 1;
            }
        }
        public int Dia => DiaDelAnio - DiaInicioMes[Mes - 1] + 1;
        public int Campania => DiaDelAnio >= DiaDel1DeMayo ? Anio : Anio - 1;
        public int DiaDeCampania => (DiaDelAnio - DiaDel1DeMayo + 365) % 365;

        public Fecha MasDias(int n) => new Fecha(Absoluto + n);

        public override string ToString() => $"{Dia}/{Mes} Año {Anio}";

        public bool Equals(Fecha otra) => Absoluto == otra.Absoluto;
        public override bool Equals(object obj) => obj is Fecha f && Equals(f);
        public override int GetHashCode() => Absoluto;
        public int CompareTo(Fecha otra) => Absoluto.CompareTo(otra.Absoluto);

        public static bool operator ==(Fecha a, Fecha b) => a.Absoluto == b.Absoluto;
        public static bool operator !=(Fecha a, Fecha b) => a.Absoluto != b.Absoluto;
        public static bool operator <(Fecha a, Fecha b) => a.Absoluto < b.Absoluto;
        public static bool operator >(Fecha a, Fecha b) => a.Absoluto > b.Absoluto;
        public static bool operator <=(Fecha a, Fecha b) => a.Absoluto <= b.Absoluto;
        public static bool operator >=(Fecha a, Fecha b) => a.Absoluto >= b.Absoluto;

        static int[] Acumulados()
        {
            var r = new int[12];
            for (int i = 1; i < 12; i++) r[i] = r[i - 1] + DiasPorMes[i - 1];
            return r;
        }
    }
}
