using System;
using System.Collections.Generic;

namespace AgroLife.Sim
{
    public static class ModeloCultivo
    {
        public static EstadoCultivo Sembrar(CultivoParams c, Genetica g, Fecha fecha, double nKgHa, double pKgHa,
            LoteDatos lote, double pBrayPpm, ModeloSuelo m)
        {
            var e = new EstadoCultivo
            {
                CultivoId = c.Id,
                Genetica = g,
                FechaSiembra = fecha,
                Campania = fecha.Campania,
                Etapa = Etapa.Siembra,
                RindeAlcanzableQqHa = RindeAlcanzable(c, g, lote.Ip),
                LFecha = PerdidaPorFecha(c, fecha.DiaDeCampania),
                LP = Fosforo.Perdida(pBrayPpm, pKgHa, c.Fosforo, m),
                NAplicadoKgHa = nKgHa,
                PAplicadoKgHa = pKgHa,
            };
            if (c.Fotoperiodo != null)
            {
                var (dia, mes) = Fecha.ParseDiaMes(c.Fotoperiodo.FechaReferencia);
                int atraso = Math.Max(0, fecha.DiaDeCampania - Fecha.DiaDeCampaniaDe(dia, mes));
                e.FactorFotoperiodo = Math.Max(c.Fotoperiodo.FactorMinimo, 1 - c.Fotoperiodo.AcortamientoPorDia * atraso);
            }
            if (c.Nitrogeno != null)
                e.NDemandaTotalKg = e.RindeAlcanzableQqHa * (1 - e.LFecha) * c.Nitrogeno.KgPorQq;
            return e;
        }

        /// <summary>Rpot de la genética × f(IP).</summary>
        public static double RindeAlcanzable(CultivoParams c, Genetica g, double ip) =>
            c.Geneticas[g].RpotQqHa * (c.FactorIp.A + c.FactorIp.B * ip / 100);

        /// <summary>LN según la relación oferta/demanda de nitrógeno (curva lineal-plateau).</summary>
        public static double PerdidaPorNitrogeno(NitrogenoParams n, double ofertaSobreDemanda) =>
            1 - Math.Min(1, n.MinRelativo + (1 - n.MinRelativo) * ofertaSobreDemanda);

        /// <summary>Umbrales de la genética, acortados por el fotoperíodo (salvo la emergencia).</summary>
        public static UmbralesGd Umbrales(EstadoCultivo e, CultivoParams c)
        {
            var u = c.Geneticas[e.Genetica].Gd;
            double f = e.FactorFotoperiodo;
            return new UmbralesGd
            {
                Emergencia = u.Emergencia, Vegetativo = u.Vegetativo * f, Floracion = u.Floracion * f,
                Llenado = u.Llenado * f, Madurez = u.Madurez * f,
            };
        }

        public static double Kc(EstadoCultivo e, CultivoParams c) => Valor(c.Kc, e.Etapa);

        public static void AvanzarDia(EstadoCultivo e, CultivoParams c, DiaClima clima, FlujosAgua agua, ref double nMineralKgHa)
        {
            var u = Umbrales(e, c);
            double gdAntes = e.GradosDia;
            e.GradosDia += Math.Max(0, clima.Tmedia() - c.TemperaturaBase);

            e.Etapa = EtapaPara(e.GradosDia, u);
            if (e.Etapa == Etapa.Madurez) e.DiasEnMadurez++;

            if (agua.EtPotencial > 0)
                e.SumaEstresAgua += (1 - agua.EtReal / agua.EtPotencial) * Valor(c.SensibilidadAgua, e.Etapa);
            if (agua.Anegado)
                e.SumaAnegamiento += Valor(c.SensibilidadAnegamiento, e.Etapa);
            if (clima.TminC < c.Helada.Umbral) e.LTemp += Valor(c.Helada.Perdida, e.Etapa);
            if (clima.TmaxC > c.Calor.Umbral) e.LTemp += Valor(c.Calor.Perdida, e.Etapa);

            // La demanda de N se reparte según los grados-día entre emergencia y madurez.
            double tramo = Math.Max(0, Math.Min(e.GradosDia, u.Madurez) - Math.Max(gdAntes, u.Emergencia));
            double demandaHoy = e.NDemandaTotalKg * tramo / (u.Madurez - u.Emergencia);
            e.NDemandaHastaHoyKg += demandaHoy;
            double absorbido = Math.Min(nMineralKgHa, demandaHoy);
            nMineralKgHa -= absorbido;
            e.NAbsorbidoKg += absorbido;
        }

        public static bool ListoParaCosechar(EstadoCultivo e, CultivoParams c) =>
            e.Etapa == Etapa.Madurez && e.DiasEnMadurez >= c.DiasACosecha;

        /// <summary>Interpolación lineal de la curva por día de campaña; plana fuera de los extremos.</summary>
        public static double PerdidaPorFecha(CultivoParams c, int diaDeCampania)
        {
            var puntos = c.CurvaFecha;
            double DiaDe(PuntoCurva p) { var (d, m) = Fecha.ParseDiaMes(p.Fecha); return Fecha.DiaDeCampaniaDe(d, m); }

            if (diaDeCampania <= DiaDe(puntos[0])) return puntos[0].Perdida;
            for (int i = 1; i < puntos.Count; i++)
            {
                double x0 = DiaDe(puntos[i - 1]), x1 = DiaDe(puntos[i]);
                if (diaDeCampania <= x1)
                    return puntos[i - 1].Perdida + (puntos[i].Perdida - puntos[i - 1].Perdida) * (diaDeCampania - x0) / (x1 - x0);
            }
            return puntos[puntos.Count - 1].Perdida;
        }

        public static Perdidas PerdidasActuales(EstadoCultivo e, CultivoParams c)
        {
            double ln = 0;
            if (c.Nitrogeno != null && e.NDemandaHastaHoyKg > 0)
                ln = PerdidaPorNitrogeno(c.Nitrogeno, e.NAbsorbidoKg / e.NDemandaHastaHoyKg);
            return new Perdidas
            {
                Fecha = e.LFecha,
                Agua = Math.Min(1, e.SumaEstresAgua),
                Anegamiento = Math.Min(1, e.SumaAnegamiento),
                Temperatura = Math.Min(1, e.LTemp),
                Nitrogeno = ln,
                Fosforo = e.LP,
                Plagas = Math.Min(1, e.LPlagas),
            };
        }

        public static Cascada CalcularCascada(EstadoCultivo e, CultivoParams c)
        {
            var p = PerdidasActuales(e, c);
            var k = new Cascada { PotencialQqHa = e.RindeAlcanzableQqHa };
            double resto = k.PotencialQqHa;
            double Quitar(double fraccion) { double q = resto * fraccion; resto -= q; return q; }
            k.FechaQq = Quitar(p.Fecha);
            k.AguaQq = Quitar(p.Agua);
            k.AnegamientoQq = Quitar(p.Anegamiento);
            k.TemperaturaQq = Quitar(p.Temperatura);
            k.NitrogenoQq = Quitar(p.Nitrogeno);
            k.FosforoQq = Quitar(p.Fosforo);
            k.PlagasQq = Quitar(p.Plagas);
            k.RealQqHa = resto;
            return k;
        }

        static Etapa EtapaPara(double gd, UmbralesGd u)
        {
            if (gd >= u.Madurez) return Etapa.Madurez;
            if (gd >= u.Llenado) return Etapa.Llenado;
            if (gd >= u.Floracion) return Etapa.Floracion;
            if (gd >= u.Vegetativo) return Etapa.Vegetativo;
            if (gd >= u.Emergencia) return Etapa.Emergencia;
            return Etapa.Siembra;
        }

        static double Valor(Dictionary<Etapa, double> porEtapa, Etapa etapa) =>
            porEtapa != null && porEtapa.TryGetValue(etapa, out var v) ? v : 0;
    }
}
