using System;

namespace AgroLife.Sim
{
    /// <summary>Flujos aleatorios independientes, uno por subsistema, todos sembrados con la semilla maestra.</summary>
    public enum Flujo : ulong { Clima = 1, Precios = 2, Plagas = 3, Silos = 4, Inicial = 5 }

    /// <summary>PCG32 (XSH-RR) de O'Neill. Todo su estado son dos campos, así se guarda con la partida.</summary>
    public sealed class Pcg32
    {
        public ulong State, Inc;

        public Pcg32() { }

        public Pcg32(ulong semilla, ulong flujo)
        {
            State = 0;
            Inc = (flujo << 1) | 1;
            NextUInt();
            State += semilla;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong viejo = State;
            State = unchecked(viejo * 6364136223846793005UL + Inc);
            uint xorshifted = (uint)(((viejo >> 18) ^ viejo) >> 27);
            int rot = (int)(viejo >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        /// <summary>Uniforme en (0, 1): nunca 0 ni 1, así se puede tomar el logaritmo.</summary>
        public double NextDouble() => (NextUInt() + 0.5) / 4294967296.0;

        public double NextGaussian() =>
            Math.Sqrt(-2.0 * Math.Log(NextDouble())) * Math.Cos(2.0 * Math.PI * NextDouble());

        /// <summary>Marsaglia–Tsang. Para forma &lt; 1 usa Gamma(forma + 1)·U^(1/forma).</summary>
        public double NextGamma(double forma, double escala)
        {
            if (forma < 1)
                return NextGamma(forma + 1, escala) * Math.Pow(NextDouble(), 1.0 / forma);

            double d = forma - 1.0 / 3.0, c = 1.0 / Math.Sqrt(9.0 * d);
            while (true)
            {
                double x = NextGaussian(), v = 1 + c * x;
                if (v <= 0) continue;
                v = v * v * v;
                double u = NextDouble();
                if (u < 1 - 0.0331 * x * x * x * x || Math.Log(u) < 0.5 * x * x + d * (1 - v + Math.Log(v)))
                    return d * v * escala;
            }
        }

        public bool Bernoulli(double p) => NextDouble() < p;

        public double Uniforme(double min, double max) => min + (max - min) * NextDouble();
    }
}
