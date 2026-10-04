namespace AgroLife.Sim
{
    public static class Plagas
    {
        /// <summary>
        /// Sorteo diario de aparición. Devuelve la severidad si aparece hoy (y la marca como ocurrida:
        /// una vez por cultivo), o null si no corresponde o no apareció.
        /// </summary>
        public static double? Sortear(EstadoCultivo e, PlagaParams p, DiaClima clima, int diasDesdeLluvia, Pcg32 rng)
        {
            if (!p.Cultivos.Contains(e.CultivoId) || !p.Etapas.Contains(e.Etapa) || e.PlagasOcurridas.Contains(p.Id))
                return null;

            double prob = p.ProbDiaria
                          * (diasDesdeLluvia <= 3 ? p.MultiplicadorLluvia : 1)
                          * (clima.Tmedia() >= p.TempMinima ? 1 : 0);
            if (!rng.Bernoulli(prob)) return null;

            e.PlagasOcurridas.Add(p.Id);
            return rng.Uniforme(p.Severidad.Min, p.Severidad.Max);
        }
    }
}
