using System;
using System.Collections.Generic;

namespace AgroLife.Sim
{
    public sealed partial class Simulation
    {
        public double PrecioArrendamientoUsd(int loteId) =>
            Economia.ArrendamientoUsd(Datos.Lote(loteId), Datos.Region, Estado.Precios.PrecioUsdT[Grano.Soja]);

        public double PrecioCompraUsd(int loteId) => Economia.PrecioCompraUsd(Datos.Lote(loteId), Datos.Region);

        public Resultado ArrendarLote(int loteId)
        {
            var r = ValidarComun(loteId);
            if (!r.Ok) return r;
            var lote = Lote(loteId);
            if (lote.Tenencia != Tenencia.Tercero) return Resultado.Rechazo($"El lote {loteId} ya es tuyo");
            if (lote.Cultivo != null) return Ocupado(lote);
            double costo = PrecioArrendamientoUsd(loteId);
            if (costo > FondosLibresUsd()) return SaldoInsuficiente(costo);

            // Firmado el 30/4 ("vence el 30 de abril siguiente"), cuenta para la campaña que empieza mañana.
            var hoy = Estado.Fecha;
            int campania = hoy.DiaDeCampania == 364 ? hoy.Campania + 1 : hoy.Campania;
            var contrato = new Contrato
            {
                LoteId = loteId, Campania = campania, MontoUsd = costo, Vence = Fecha.EnCampania(campania, 30, 4),
            };
            lote.Plan = null; // se descarta el plan de los terceros
            lote.Tenencia = Tenencia.Arrendado;
            lote.Contrato = contrato;
            Estado.Contratos.Add(contrato);
            Estado.SaldoUsd -= costo;
            return Resultado.Exito;
        }

        public Resultado ComprarLote(int loteId)
        {
            var r = ValidarComun(loteId);
            if (!r.Ok) return r;
            var lote = Lote(loteId);
            if (lote.Tenencia == Tenencia.Propio) return Resultado.Rechazo($"El lote {loteId} ya es tuyo");
            if (lote.Tenencia == Tenencia.Tercero && lote.Cultivo != null) return Ocupado(lote);
            double costo = PrecioCompraUsd(loteId);
            if (costo > FondosLibresUsd()) return SaldoInsuficiente(costo);

            if (lote.Tenencia == Tenencia.Tercero) lote.Plan = null;
            lote.Tenencia = Tenencia.Propio;
            lote.Contrato = null;
            Estado.SaldoUsd -= costo;
            return Resultado.Exito;
        }

        public Resultado PlanificarCultivo(int loteId, PlanCultivo plan)
        {
            var r = ValidarComun(loteId);
            if (!r.Ok) return r;
            var lote = Lote(loteId);
            if (lote.Tenencia == Tenencia.Tercero) return NoEsTuyo(loteId);
            if (lote.Cultivo != null || lote.Plan != null) return Resultado.Rechazo($"El lote {loteId} ya tiene un cultivo planificado o sembrado");
            if (plan?.CultivoId == null || !Datos.Cultivos.TryGetValue(plan.CultivoId, out var c))
                return Resultado.Rechazo($"Cultivo desconocido: {plan?.CultivoId}");
            if (plan.CultivoId == "soja2") return Resultado.Rechazo("La soja de segunda se planifica junto con el trigo");
            if (plan.Soja2 != null && plan.CultivoId != "trigo") return Resultado.Rechazo("La soja de segunda solo va después de trigo");
            if (plan.Soja2 != null && plan.Soja2.CultivoId != "soja2") return Resultado.Rechazo($"Cultivo desconocido: {plan.Soja2.CultivoId}");

            r = ValidarDosis(plan, c);
            if (!r.Ok) return r;
            if (plan.Soja2 != null)
            {
                r = ValidarDosis(plan.Soja2, Datos.Cultivos["soja2"]);
                if (!r.Ok) return r;
            }

            var hoy = Estado.Fecha;
            if (plan.FechaSiembra <= hoy || plan.FechaSiembra > hoy.MasDias(365))
                return Resultado.Rechazo("La fecha de siembra tiene que ser posterior a hoy y dentro del próximo año");
            var (dd, md) = Fecha.ParseDiaMes(c.VentanaSiembra.Desde);
            var (dh, mh) = Fecha.ParseDiaMes(c.VentanaSiembra.Hasta);
            int dia = plan.FechaSiembra.DiaDeCampania;
            if (dia < Fecha.DiaDeCampaniaDe(dd, md) || dia > Fecha.DiaDeCampaniaDe(dh, mh))
                return Resultado.Rechazo($"Fuera de la ventana de siembra de {NombreDe(c)} ({c.VentanaSiembra.Desde}–{c.VentanaSiembra.Hasta})");
            if (lote.Tenencia == Tenencia.Arrendado && plan.FechaSiembra > lote.Contrato.Vence)
                return Resultado.Rechazo($"El arrendamiento del lote {loteId} vence el {lote.Contrato.Vence}");

            double costo = CalcularPresupuesto(loteId, plan).CostoTotalUsd;
            if (costo > FondosLibresUsd()) return SaldoInsuficiente(costo);

            lote.Plan = plan.Copia(); // el que llama puede seguir editando su objeto
            return Resultado.Exito;
        }

        public Resultado CancelarPlan(int loteId)
        {
            var r = ValidarComun(loteId);
            if (!r.Ok) return r;
            var lote = Lote(loteId);
            if (lote.Tenencia == Tenencia.Tercero) return NoEsTuyo(loteId);
            if (lote.Plan != null) { lote.Plan = null; return Resultado.Exito; }
            if (lote.Cultivo?.SegundoCultivo != null) { lote.Cultivo.SegundoCultivo = null; return Resultado.Exito; }
            return Resultado.Rechazo($"No hay un plan pendiente en el lote {loteId}");
        }

        public Resultado VenderGrano(Grano grano, double toneladas)
        {
            if (Estado.Terminada) return Resultado.Rechazo("La partida terminó por quiebra");
            if (!(toneladas > 0)) return Resultado.Rechazo("La cantidad tiene que ser mayor que cero"); // también rechaza NaN
            if (toneladas > Estado.StockT[grano] + 1e-9) return Resultado.Rechazo($"No hay suficiente {NombreGrano(grano)} en silobolsa");
            Estado.StockT[grano] = Math.Max(0, Estado.StockT[grano] - toneladas);
            Estado.SaldoUsd += toneladas * Estado.Precios.PrecioUsdT[grano] * (1 - Datos.Economia.ComisionVenta);
            return Resultado.Exito;
        }

        public Resultado DecidirTratamiento(int loteId, bool aplicar)
        {
            var r = ValidarComun(loteId);
            if (!r.Ok) return r;
            var lote = Lote(loteId);
            var d = lote.Decision;
            if (d == null) return Resultado.Rechazo($"No hay una decisión pendiente en el lote {loteId}");
            if (aplicar)
            {
                if (d.CostoUsd > FondosLibresUsd()) return SaldoInsuficiente(d.CostoUsd);
                var plaga = Datos.Plagas.Find(p => p.Id == d.PlagaId);
                Estado.SaldoUsd -= d.CostoUsd;
                lote.Cultivo.CostosDirectosUsd += d.CostoUsd;
                lote.Cultivo.LPlagas += d.Severidad * (1 - plaga.Eficacia);
            }
            else
            {
                lote.Cultivo.LPlagas += d.Severidad;
            }
            lote.Decision = null;
            return Resultado.Exito;
        }

        public Presupuesto CalcularPresupuesto(int loteId, PlanCultivo plan)
        {
            var p = new Presupuesto();
            AgregarAlPresupuesto(p, loteId, plan, plan.FechaSiembra.DiaDeCampania);
            if (plan.Soja2 != null)
            {
                var (d, m) = Fecha.ParseDiaMes(Datos.Cultivos["soja2"].PlanTipico.Siembra);
                AgregarAlPresupuesto(p, loteId, plan.Soja2, Fecha.DiaDeCampaniaDe(d, m));
            }
            return p;
        }

        public ReporteLote ReporteLote(int loteId, int campania)
        {
            var rep = new ReporteLote
            {
                Cultivos = Estado.Resultados.FindAll(x => x.LoteId == loteId && x.Campania == campania),
                PromedioZonaQqHa = new Dictionary<string, double>(),
            };
            foreach (var c in rep.Cultivos)
            {
                rep.MargenBrutoUsd += c.Margen.MargenBrutoUsd;
                rep.PromedioZonaQqHa[c.CultivoId] = PromedioZonaQqHa(campania, c.CultivoId);
            }
            foreach (var k in Estado.Contratos)
                if (k.LoteId == loteId && k.Campania == campania) rep.ArrendamientoUsd += k.MontoUsd;
            rep.ResultadoUsd = rep.MargenBrutoUsd - rep.ArrendamientoUsd;
            return rep;
        }

        void AgregarAlPresupuesto(Presupuesto p, int loteId, PlanCultivo plan, int diaDeCampania)
        {
            var c = Datos.Cultivos[plan.CultivoId];
            var datos = Datos.Lote(loteId);
            var lote = Lote(loteId);
            var eco = Datos.Economia;
            double ha = datos.SuperficieHa;
            double costoHa = Economia.CostosDirectosSiembraUsdHa(c.Id, plan.NKgHa, plan.PKgHa, eco);

            double alcanzable = ModeloCultivo.RindeAlcanzable(c, plan.Genetica, datos.Ip);
            double lf = ModeloCultivo.PerdidaPorFecha(c, diaDeCampania);
            double lp = Fosforo.Perdida(lote.PBrayPpm, plan.PKgHa, c.Fosforo, Datos.Suelo);
            double ln = 0;
            if (c.Nitrogeno != null)
            {
                double demanda = alcanzable * (1 - lf) * c.Nitrogeno.KgPorQq;
                double oferta = lote.NMineralKgHa + plan.NKgHa + c.Nitrogeno.MineralizacionEsperadaKgHa;
                ln = ModeloCultivo.PerdidaPorNitrogeno(c.Nitrogeno, oferta / demanda);
            }
            double esperado = alcanzable * (1 - lf) * (1 - lp) * (1 - ln) * c.FactorClimaEsperado;

            double precio = Estado.Precios.PrecioUsdT[c.Grano];
            double km = datos.DistanciaAcopioKm;
            var pc = new PresupuestoCultivo
            {
                CultivoId = c.Id,
                CostoUsd = costoHa * ha,
                RindeEsperadoQqHa = esperado,
                MargenBrutoUsd = Economia.Margen(esperado * ha / 10, precio, km, costoHa * ha, eco).MargenBrutoUsd,
                RindeIndiferenciaQqHa = Economia.RindeIndiferenciaQqHa(costoHa, Economia.PrecioNetoUsdT(precio, eco, km), eco.CosechaPorcentaje),
            };
            p.Cultivos.Add(pc);
            p.CostoTotalUsd += pc.CostoUsd;
            p.MargenBrutoUsd += pc.MargenBrutoUsd;
        }

        Resultado ValidarDosis(PlanCultivo plan, CultivoParams c)
        {
            if (plan.NKgHa > 0 && c.Nitrogeno == null) return Resultado.Rechazo($"El cultivo {NombreDe(c)} no lleva nitrógeno");
            if (!(plan.NKgHa >= 0 && plan.NKgHa <= 400 && plan.PKgHa >= 0 && plan.PKgHa <= 100)) // también rechaza NaN
                return Resultado.Rechazo("Dosis fuera de rango (N 0–400, P 0–100 kg/ha)");
            return Resultado.Exito;
        }

        Resultado ValidarComun(int loteId)
        {
            if (Estado.Terminada) return Resultado.Rechazo("La partida terminó por quiebra");
            if (loteId < 1 || loteId > Estado.Lotes.Count) return Resultado.Rechazo($"No existe el lote {loteId}");
            return Resultado.Exito;
        }

        EstadoLote Lote(int id) => Estado.Lotes[id - 1];

        static string NombreDe(CultivoParams c) => c.Nombre.ToLowerInvariant();

        static string NombreGrano(Grano g) => g == Grano.Trigo ? "trigo" : g == Grano.Maiz ? "maíz" : "soja";

        Resultado Ocupado(EstadoLote lote) =>
            Resultado.Rechazo($"El lote {lote.Id} está ocupado con {NombreDe(Datos.Cultivos[lote.Cultivo.CultivoId])} hasta la cosecha");

        static Resultado NoEsTuyo(int loteId) => Resultado.Rechazo($"El lote {loteId} no es tuyo");

        Resultado SaldoInsuficiente(double costo) =>
            Resultado.Rechazo($"Saldo insuficiente: hacen falta {Formato.Usd(costo)} y tenés {Formato.Usd(FondosLibresUsd())} disponibles");
    }
}
