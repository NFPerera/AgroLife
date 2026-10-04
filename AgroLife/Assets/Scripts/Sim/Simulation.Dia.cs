using System;
using System.Collections.Generic;

namespace AgroLife.Sim
{
    public sealed partial class Simulation
    {
        // El orden de este método es parte del determinismo: el clima y los precios se sortean
        // siempre igual, hagan lo que hagan los lotes.
        List<Evento> SimularDia()
        {
            var eventos = new List<Evento>();
            var hoy = Estado.Fecha;

            if (hoy.Dia == 1 && hoy.Mes == 5) IniciarCampania(eventos);

            var clima = GeneradorClima.GenerarDia(Estado.Clima, Datos.Clima, Datos.Region.Latitud, hoy, Rng(Flujo.Clima));
            Precios.AvanzarDia(Estado.Precios, Datos.Economia, hoy, Rng(Flujo.Precios));
            if (hoy.Dia == 1) RiesgoSilobolsa(eventos);

            foreach (var lote in Estado.Lotes) SimularLote(lote, clima, eventos);

            ActualizarCuenta(eventos);
            if (clima.TminC < 0) eventos.Add(NuevoEvento(TipoEvento.Helada, 0, $"Helada: {Formato.Numero(clima.TminC, 1)} °C"));
            return eventos;
        }

        void IniciarCampania(List<Evento> eventos)
        {
            int campania = Estado.Fecha.Campania;

            foreach (var lote in Estado.Lotes)
            {
                if (lote.Tenencia != Tenencia.Arrendado || lote.Contrato.Vence >= Estado.Fecha) continue;
                bool ocupado = lote.Cultivo != null || lote.Plan != null;
                eventos.Add(NuevoEvento(TipoEvento.VencimientoArrendamiento, lote.Id,
                    $"Venció el arrendamiento del lote {lote.Id}" + (ocupado ? "; sigue hasta la cosecha" : "")));
                if (!ocupado) DevolverATerceros(lote);
            }

            GeneradorClima.SortearFase(Estado.Clima, Datos.Clima, Rng(Flujo.Clima));

            foreach (var lote in Estado.Lotes)
                if (lote.Tenencia == Tenencia.Tercero && lote.Cultivo == null && lote.Plan == null)
                    lote.Plan = PlanDeTerceros(lote.Id, campania);

            var p = Estado.Clima.Pronostico;
            string Pct(FaseEnso f) => Formato.Numero(p[f] * 100, 0);
            eventos.Add(NuevoEvento(TipoEvento.InicioCampania, 0,
                $"Inicio de la campaña {campania}. Pronóstico: Niño {Pct(FaseEnso.Nino)}% · Neutro {Pct(FaseEnso.Neutro)}% · Niña {Pct(FaseEnso.Nina)}%",
                pausa: true));
        }

        PlanCultivo PlanDeTerceros(int loteId, int campania)
        {
            var id = Rotacion[(loteId + campania) % 3];
            var plan = PlanTipico(id, campania);
            if (id == "trigo") plan.Soja2 = PlanTipico("soja2", campania);
            return plan;
        }

        PlanCultivo PlanTipico(string cultivoId, int campania)
        {
            var pt = Datos.Cultivos[cultivoId].PlanTipico;
            var (dia, mes) = Fecha.ParseDiaMes(pt.Siembra);
            return new PlanCultivo
            {
                CultivoId = cultivoId, FechaSiembra = Fecha.EnCampania(campania, dia, mes),
                Genetica = pt.Genetica, NKgHa = pt.NKgHa, PKgHa = pt.PKgHa,
            };
        }

        void SimularLote(EstadoLote lote, DiaClima clima, List<Evento> eventos)
        {
            var datos = Datos.Lote(lote.Id);
            if (lote.Plan != null && lote.Plan.FechaSiembra == Estado.Fecha) Sembrar(lote, datos, eventos);

            var c = lote.Cultivo != null ? Datos.Cultivos[lote.Cultivo.CultivoId] : null;
            double kc = c != null ? ModeloCultivo.Kc(lote.Cultivo, c) : Datos.Suelo.KcSueloDesnudo;
            double agua = lote.AguaMm;
            var flujos = BalanceAgua.PasoDiario(ref agua, datos, Datos.Suelo, clima.LluviaMm, clima.Et0Mm, kc);
            lote.AguaMm = agua;

            lote.NMineralKgHa += Nitrogeno.Mineralizacion(datos.CorgPct, clima.Tmedia(), lote.AguaMm, datos.AuMaxMm, Datos.Suelo);
            lote.NMineralKgHa = Math.Max(0, lote.NMineralKgHa - Nitrogeno.Lavado(lote.NMineralKgHa, flujos.Drenaje, flujos.AguaAntesDeDrenarMm, datos.PmpMm));

            if (lote.Cultivo == null) return;
            double n = lote.NMineralKgHa;
            ModeloCultivo.AvanzarDia(lote.Cultivo, c, clima, flujos, ref n);
            lote.NMineralKgHa = n;

            foreach (var plaga in Datos.Plagas)
            {
                var sev = Plagas.Sortear(lote.Cultivo, plaga, clima, Estado.Clima.DiasDesdeLluvia, Rng(Flujo.Plagas));
                if (sev != null) AplicarPlaga(lote, plaga, sev.Value, eventos);
            }

            if (ModeloCultivo.ListoParaCosechar(lote.Cultivo, c)) Cosechar(lote, datos, c, eventos);
        }

        void AplicarPlaga(EstadoLote lote, PlagaParams plaga, double sev, List<Evento> eventos)
        {
            if (sev <= plaga.Umbral) { lote.Cultivo.LPlagas += sev; return; }
            if (lote.Tenencia == Tenencia.Tercero) { lote.Cultivo.LPlagas += sev * (1 - plaga.Eficacia); return; } // los terceros siempre tratan

            // ponytail: una sola decisión por lote y día; alcanza mientras pests.json tenga una plaga por cultivo.
            var datos = Datos.Lote(lote.Id);
            var c = Datos.Cultivos[lote.Cultivo.CultivoId];
            double ha = datos.SuperficieHa;
            double qq = sev * ModeloCultivo.CalcularCascada(lote.Cultivo, c).RealQqHa * ha;
            double neto = Economia.PrecioNetoUsdT(Estado.Precios.PrecioUsdT[c.Grano], Datos.Economia, datos.DistanciaAcopioKm);
            lote.Decision = new DecisionPlaga
            {
                PlagaId = plaga.Id, Severidad = sev, Nivel = sev * plaga.EscalaNivel, Umbral = plaga.Umbral * plaga.EscalaNivel,
                CostoUsd = (plaga.ProductoUsdHa + plaga.PulverizacionUsdHa) * ha,
                PerdidaEsperadaQq = qq, PerdidaEsperadaUsd = qq * neto / 10,
            };
            eventos.Add(NuevoEvento(TipoEvento.Plaga, lote.Id,
                $"{plaga.Nombre} en el lote {lote.Id}: nivel {Formato.Numero(lote.Decision.Nivel, 1)} {plaga.Unidad} " +
                $"(umbral {Formato.Numero(lote.Decision.Umbral, 1)} {plaga.Unidad})", pausa: true));
        }

        void RiesgoSilobolsa(List<Evento> eventos)
        {
            var sb = Datos.Economia.Silobolsa;
            foreach (var g in new[] { Grano.Trigo, Grano.Maiz, Grano.Soja })
            {
                if (Estado.StockT[g] <= 0 || !Rng(Flujo.Silos).Bernoulli(sb.RiesgoMensual)) continue;
                double frac = Rng(Flujo.Silos).Uniforme(sb.PerdidaMin, sb.PerdidaMax);
                Estado.StockT[g] -= Estado.StockT[g] * frac;
                eventos.Add(NuevoEvento(TipoEvento.PerdidaSilobolsa, 0,
                    $"Se perdió el {Formato.Numero(frac * 100, 0)} % del {NombreGrano(g)} en silobolsa"));
            }
        }

        void ActualizarCuenta(List<Evento> eventos)
        {
            var cta = Datos.Economia.Cuenta;
            Estado.SaldoUsd += Economia.InteresDiario(Estado.SaldoUsd, cta.TasaAnualDescubierto);
            if (Estado.SaldoUsd < -cta.LimiteDescubiertoUsd)
            {
                Estado.Terminada = true;
                eventos.Add(NuevoEvento(TipoEvento.Quiebra, 0, "Quiebra: el saldo bajó del límite de descubierto. La partida terminó.", pausa: true));
            }
            else if (Estado.SaldoUsd < -cta.LimiteDescubiertoUsd * cta.AlertaFraccion)
            {
                if (Estado.AlertaSaldoEmitida) return;
                Estado.AlertaSaldoEmitida = true;
                eventos.Add(NuevoEvento(TipoEvento.SaldoCercaDelLimite, 0,
                    $"Tu saldo ({Formato.Usd(Estado.SaldoUsd)}) está cerca del límite de descubierto ({Formato.Usd(-cta.LimiteDescubiertoUsd)})", pausa: true));
            }
            else
            {
                Estado.AlertaSaldoEmitida = false;
            }
        }

        void Sembrar(EstadoLote lote, LoteDatos datos, List<Evento> eventos)
        {
            var plan = lote.Plan;
            var c = Datos.Cultivos[plan.CultivoId];
            lote.Cultivo = ModeloCultivo.Sembrar(c, plan.Genetica, Estado.Fecha, plan.NKgHa, plan.PKgHa, datos, lote.PBrayPpm, Datos.Suelo);
            lote.Cultivo.SegundoCultivo = plan.Soja2;
            lote.Cultivo.VenderAlCosechar = plan.VenderAlCosechar;
            lote.NMineralKgHa += plan.NKgHa;
            lote.Plan = null;

            if (lote.Tenencia == Tenencia.Tercero) return;
            double costo = Economia.CostosDirectosSiembraUsdHa(c.Id, plan.NKgHa, plan.PKgHa, Datos.Economia) * datos.SuperficieHa;
            lote.Cultivo.CostosDirectosUsd = costo;
            Estado.SaldoUsd -= costo;
            eventos.Add(NuevoEvento(TipoEvento.Siembra, lote.Id, $"Se sembró {NombreDe(c)} en el lote {lote.Id}"));
        }

        void Cosechar(EstadoLote lote, LoteDatos datos, CultivoParams c, List<Evento> eventos)
        {
            var cultivo = lote.Cultivo;
            var k = ModeloCultivo.CalcularCascada(cultivo, c);
            if (lote.Tenencia == Tenencia.Tercero) SumarPromedioZona(cultivo.Campania, cultivo.CultivoId, k.RealQqHa, datos.SuperficieHa);

            lote.PBrayPpm = Fosforo.Actualizar(lote.PBrayPpm, cultivo.PAplicadoKgHa, k.RealQqHa, c.Fosforo, Datos.Suelo);
            lote.CultivoAnterior = cultivo.CultivoId;
            lote.Cultivo = null;

            bool delJugador = lote.Tenencia != Tenencia.Tercero;
            if (cultivo.SegundoCultivo != null)
            {
                var manana = Estado.Fecha.MasDias(1);
                var (dia, mes) = Fecha.ParseDiaMes(Datos.Cultivos["soja2"].VentanaSiembra.Hasta);
                if (manana.Campania == cultivo.Campania && manana.DiaDeCampania <= Fecha.DiaDeCampaniaDe(dia, mes))
                {
                    cultivo.SegundoCultivo.FechaSiembra = manana;
                    lote.Plan = cultivo.SegundoCultivo;
                }
                else if (delJugador)
                {
                    eventos.Add(NuevoEvento(TipoEvento.SiembraCancelada, lote.Id,
                        $"No se sembró soja de segunda en el lote {lote.Id}: el trigo se cosechó después del 15/1"));
                }
            }

            if (!delJugador) return;
            CobrarCosecha(lote, datos, c, cultivo, k);
            eventos.Add(NuevoEvento(TipoEvento.Cosecha, lote.Id,
                $"Cosecha de {NombreDe(c)} en el lote {lote.Id}: {Formato.Numero(k.RealQqHa, 1)} qq/ha"));
            if (lote.Tenencia == Tenencia.Arrendado && lote.Contrato.Vence < Estado.Fecha && lote.Plan == null)
                DevolverATerceros(lote);
        }

        void CobrarCosecha(EstadoLote lote, LoteDatos datos, CultivoParams c, EstadoCultivo cultivo, Cascada k)
        {
            var eco = Datos.Economia;
            double ha = datos.SuperficieHa, km = datos.DistanciaAcopioKm;
            double t = k.RealQqHa * ha / 10;
            double precio = Estado.Precios.PrecioUsdT[c.Grano];
            var m = Economia.Margen(t, precio, km, cultivo.CostosDirectosUsd, eco);

            Estado.SaldoUsd -= m.CosechaUsd + t * Economia.FleteUsdT(eco, km);
            if (cultivo.VenderAlCosechar)
            {
                Estado.SaldoUsd += t * precio * (1 - eco.ComisionVenta);
            }
            else
            {
                Estado.SaldoUsd -= t * eco.Silobolsa.EmbolsadoUsdT;
                Estado.StockT[c.Grano] += t;
            }

            Estado.Resultados.Add(new ResultadoCultivo
            {
                LoteId = lote.Id, Campania = cultivo.Campania, CultivoId = cultivo.CultivoId, SuperficieHa = ha,
                FechaCosecha = Estado.Fecha, Cascada = k, ToneladasCosechadas = t, PrecioCosechaUsdT = precio, Margen = m,
                RindeIndiferenciaQqHa = Economia.RindeIndiferenciaQqHa(cultivo.CostosDirectosUsd / ha,
                    Economia.PrecioNetoUsdT(precio, eco, km), eco.CosechaPorcentaje),
            });
        }

        static void DevolverATerceros(EstadoLote lote)
        {
            lote.Tenencia = Tenencia.Tercero;
            lote.Contrato = null;
        }

        void SumarPromedioZona(int campania, string cultivoId, double rindeQqHa, double ha)
        {
            var p = Estado.PromediosZona.Find(x => x.Campania == campania && x.CultivoId == cultivoId);
            if (p == null)
            {
                p = new PromedioZona { Campania = campania, CultivoId = cultivoId };
                Estado.PromediosZona.Add(p);
            }
            p.SumaQqPorHa += rindeQqHa * ha;
            p.SumaHa += ha;
        }
    }
}
