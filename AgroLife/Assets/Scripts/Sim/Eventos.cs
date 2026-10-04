namespace AgroLife.Sim
{
    public enum TipoEvento
    {
        InicioCampania, Plaga, SaldoCercaDelLimite, Quiebra, Siembra, Cosecha, SiembraCancelada, Helada, VencimientoArrendamiento, PerdidaSilobolsa
    }

    public sealed class Evento
    {
        public TipoEvento Tipo;
        public Fecha Fecha;
        public int LoteId; // 0 = ninguno
        public string Mensaje;
        public bool Pausa; // pide una decisión: el juego se frena
    }

    /// <summary>Respuesta de una acción del jugador: ok, o el motivo del rechazo.</summary>
    public readonly struct Resultado
    {
        public readonly bool Ok;
        public readonly string Motivo;

        Resultado(bool ok, string motivo) { Ok = ok; Motivo = motivo; }

        public static readonly Resultado Exito = new Resultado(true, null);
        public static Resultado Rechazo(string motivo) => new Resultado(false, motivo);
    }
}
