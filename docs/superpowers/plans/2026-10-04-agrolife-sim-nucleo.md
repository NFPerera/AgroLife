# AgroLife v1 — Plan 1: núcleo de simulación (AgroLife.Sim)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir `AgroLife.Sim`, el núcleo en C# puro que simula día a día clima, suelo, cultivos, plagas, economía y acciones del jugador, con guardado y determinismo, testeado en EditMode con una región de prueba.

**Architecture:** El estado es un árbol de POCOs serializables (solo campos públicos). La lógica vive en clases estáticas por subsistema que reciben estado + parámetros. `Simulation` orquesta y expone las acciones; está partida en tres archivos (`partial`). Los parámetros son JSON (`Assets/Data`) que `DatosJuego` carga y valida. La aleatoriedad sale de flujos PCG32 independientes.

**Tech Stack:** Unity 6000.3.14f1, C# 9, asmdef con `noEngineReferences`, Newtonsoft Json (`com.unity.nuget.newtonsoft-json` 3.2.2), Unity Test Framework 1.6 (NUnit, EditMode), MCP for Unity para compilar y correr tests.

**Spec:** [docs/superpowers/specs/2026-10-04-agrolife-v1-design.md](../specs/2026-10-04-agrolife-v1-design.md)

### Hoja de ruta: este es el plan 1 de 4
1. **Núcleo de simulación** (este plan): todo `AgroLife.Sim`, los datos editados a mano (`crops`, `pests`, `economy`, `soils`) y una región de prueba.
2. **Pipeline de datos de Pergamino** (Python): genera `Data/Regions/pergamino/{region,lotes,geografia,clima}.json` con el contrato de la Tarea 4, más la tabla `respaldoIP` de `soils.json`.
3. **Juego en Unity** (`AgroLife.Game`): renderer URP 2D (hoy el proyecto tiene el Universal Renderer 3D), mapa, cámara, capas, UI Toolkit, controlador de tiempo, guardado en disco (archivo temporal + reemplazo), menú y `glosario.json`.
4. **Calibración**: tests `[Category("Calibration")]` de clima contra NASA POWER y de rindes contra el Ministerio, y ajuste de `Assets/Data/*.json`.

## Global Constraints

- Unity 6000.3.14f1 con el C# 9 de Unity: no usar `record` ni `init`.
- `AgroLife.Sim` es un asmdef con `"noEngineReferences": true`. Ningún archivo de Sim usa `using UnityEngine`.
- `com.unity.nuget.newtonsoft-json` va declarado de forma explícita en `Packages/manifest.json`. Hoy solo llega como dependencia de unity-mcp.
- Toda la aleatoriedad de Sim sale de `Pcg32`, con un flujo por subsistema (`Flujo.Clima`, `Precios`, `Plagas`, `Silos`, `Inicial`), sembrado desde la semilla maestra.
  - Prohibido usar `System.Random`, `UnityEngine.Random`, `DateTime.Now`, `Guid` e iterar un `HashSet`.
- Calendario de 365 días, sin bisiestos. La partida arranca el **1/5 Año 1**. La campaña N va del 1/5 Año N al 30/4 Año N+1.
- Moneda: dólares, sin inflación, sin retenciones y sin brecha.
- Clases de estado serializables:
  - Solo campos públicos.
  - Nada calculado como propiedad, porque Newtonsoft lo serializaría.
  - Única excepción: `Fecha`, con `[JsonObject(MemberSerialization.OptIn)]`.
- Nombres de dominio y mensajes en español rioplatense, con voseo ("tenés").
- Los números de los mensajes se formatean con `Formato` (miles con ".", decimales con ","). Nunca con la cultura del sistema.
- Rutas de Unity relativas a `AgroLife/Assets/`. Los `.meta` los genera Unity: no escribirlos a mano.
- Con el Editor abierto se usa MCP for Unity. Después de crear o modificar scripts: `refresh_unity` y `read_console`.
- **No commitear** (CLAUDE.md). Este plan no tiene pasos de commit; el usuario decide cuándo.

### Cómo correr tests

Todos los pasos "Correr" siguen esta secuencia:
1. `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true)`.
2. `read_console(action="get", types=["error"], count="20")`.
   - En los pasos "debe fallar" se espera un error de compilación CS0246 o CS0103 que nombre el tipo que falta.
   - En los demás pasos no tiene que haber errores.
3. `run_tests(mode="EditMode", assembly_names=["AgroLife.Sim.Tests"], group_names=["<Clase>"])`.
4. `get_test_job(job_id, wait_timeout=60, include_failed_tests=true)`.

## Review Focus

1. **Arrendamiento que vence el 30/4 con un cultivo en pie.** El lote sigue siendo del jugador hasta la cosecha, el jugador cobra esa cosecha y después el lote vuelve a terceros. → Tarea 11.
2. **Doble cultivo con el trigo cosechado después del 15/1.** La soja de segunda no se siembra fuera de la ventana ni se cobra, y sale un aviso `SiembraCancelada`. → Tarea 11.
3. **Arrendar o comprar un lote de terceros con cultivo en pie.** Se rechaza con el motivo "ocupado" y no se pisa el cultivo. → Tarea 11.
4. **Una siembra programada deja el saldo por debajo del límite.** Se siembra igual y la partida termina por quiebra. Después, `StepDay` no avanza y toda acción se rechaza. → Tarea 12.
5. **Guardar con una decisión de plaga pendiente.** Al cargar sigue pendiente, y `StepDay` sigue bloqueado hasta que se decida. → Tarea 13.

---

### Task 1: Paquete, asmdefs y generador PCG32

**Files:**
- Modify: `AgroLife/Packages/manifest.json`
- Create: `AgroLife/Assets/Scripts/Sim/AgroLife.Sim.asmdef`
- Create: `AgroLife/Assets/Scripts/Sim/Azar/Pcg32.cs`
- Create: `AgroLife/Assets/Scripts/Sim/JsonSim.cs`
- Create: `AgroLife/Assets/Tests/Sim/AgroLife.Sim.Tests.asmdef`
- Test: `AgroLife/Assets/Tests/Sim/Pcg32Tests.cs`

**Interfaces:**
- Produces:
```csharp
namespace AgroLife.Sim
public enum Flujo : ulong { Clima = 1, Precios = 2, Plagas = 3, Silos = 4, Inicial = 5 }
public sealed class Pcg32 {
    public ulong State, Inc;
    public Pcg32() { }                          // para Newtonsoft
    public Pcg32(ulong semilla, ulong flujo);
    public uint NextUInt();
    public double NextDouble();                 // (NextUInt() + 0.5) / 2^32: nunca 0 ni 1
    public double NextGaussian();               // Box–Muller, sin cache (no agrega estado)
    public double NextGamma(double forma, double escala); // Marsaglia–Tsang; forma < 1: Gamma(forma+1)·U^(1/forma)
    public bool Bernoulli(double p);            // NextDouble() < p
    public double Uniforme(double min, double max);
}
public static class JsonSim {
    public static readonly JsonSerializerSettings Ajustes; // StringEnumConverter, MissingMemberHandling.Ignore, Formatting.None
    public static string Serializar(object o);
    public static T Deserializar<T>(string json);
}
```

- [ ] **Step 1: Agregar Newtonsoft de forma explícita**

  Llamar a `manage_packages(action="add_package", package="com.unity.nuget.newtonsoft-json@3.2.2")`. Hacer polling con `status` hasta que termine. Verificar que `Packages/manifest.json` tenga `"com.unity.nuget.newtonsoft-json": "3.2.2"`.

- [ ] **Step 2: Crear los asmdefs**

`Scripts/Sim/AgroLife.Sim.asmdef` (Newtonsoft.Json.dll se referencia sola: su meta tiene `isExplicitlyReferenced: 0`):
```json
{ "name": "AgroLife.Sim", "rootNamespace": "AgroLife.Sim", "references": [], "autoReferenced": true, "noEngineReferences": true }
```
`Tests/Sim/AgroLife.Sim.Tests.asmdef`:
```json
{
  "name": "AgroLife.Sim.Tests", "rootNamespace": "AgroLife.Sim.Tests",
  "references": ["AgroLife.Sim", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"], "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"], "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

- [ ] **Step 3: Escribir los tests que fallan** (`Pcg32Tests`)

```csharp
[Test] public void ReproduceElVectorDeReferencia() {           // pcg32-demo de O'Neill, semilla 42, secuencia 54
    var r = new Pcg32(42, 54);
    foreach (var e in new uint[] { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e })
        Assert.AreEqual(e, r.NextUInt());
}
[Test] public void FlujosDistintosDanSecuenciasDistintas() =>
    Assert.AreNotEqual(new Pcg32(7, (ulong)Flujo.Clima).NextUInt(), new Pcg32(7, (ulong)Flujo.Precios).NextUInt());
[Test] public void GaussianaTieneMedia0YDesvio1()   // 200.000 muestras, semilla 1: |media| < 0.01, |desvío − 1| < 0.01
[Test] public void GammaTieneMediaYVarianzaCorrectas()
    // forma 0.75, escala 14: media 10.5 ±1 %, varianza 147 ±3 %; forma 2.5, escala 3: media 7.5 ±1 % (200.000 muestras)
[Test] public void SobreviveAGuardarYCargar() {
    var r = new Pcg32(42, 54); r.NextUInt(); r.NextUInt(); r.NextUInt();
    var copia = JsonSim.Deserializar<Pcg32>(JsonSim.Serializar(r));
    for (int i = 0; i < 3; i++) Assert.AreEqual(r.NextUInt(), copia.NextUInt());
}
```

- [ ] **Step 4: Correr `Pcg32Tests`.** Esperado: falla la compilación porque no existe `Pcg32`.

- [ ] **Step 5: Implementar `Pcg32` y `JsonSim`.**
  - PCG32 XSH-RR de O'Neill.
  - Siembra: `State = 0; Inc = (flujo << 1) | 1; NextUInt(); State += semilla; NextUInt();`.

- [ ] **Step 6: Correr `Pcg32Tests`.** Esperado: 5/5 PASS y consola sin errores.

---

### Task 2: Calendario y formato de números

**Files:**
- Create: `AgroLife/Assets/Scripts/Sim/Tiempo/Fecha.cs`
- Create: `AgroLife/Assets/Scripts/Sim/Formato.cs`
- Test: `AgroLife/Assets/Tests/Sim/FechaTests.cs`

**Interfaces:**
- Produces:
```csharp
[JsonObject(MemberSerialization.OptIn)]
public readonly struct Fecha : IEquatable<Fecha>, IComparable<Fecha> {
    [JsonProperty] public readonly int Absoluto;            // días desde el 1/1 del Año 1 (0 = 1/1 Año 1)
    [JsonConstructor] public Fecha(int absoluto);
    public static Fecha Crear(int dia, int mes, int anio);
    public static Fecha EnCampania(int campania, int dia, int mes); // Crear(1,5,campania).MasDias(DiaDeCampaniaDe(dia,mes))
    public static int DiaDeCampaniaDe(int dia, int mes);    // 1/5 → 0 … 30/4 → 364
    public static int DiasDelMes(int mes);                  // {31,28,31,30,31,30,31,31,30,31,30,31}
    public static (int dia, int mes) ParseDiaMes(string ddmm); // "15/9" → (15, 9)
    public int Anio { get; }          // Absoluto / 365 + 1 (el año cambia el 1/1)
    public int DiaDelAnio { get; }    // 0..364
    public int Mes { get; }           // 1..12
    public int Dia { get; }           // 1..31
    public int Campania { get; }      // DiaDelAnio >= 120 ? Anio : Anio - 1
    public int DiaDeCampania { get; } // (DiaDelAnio - 120 + 365) % 365
    public Fecha MasDias(int n);
    public override string ToString(); // "1/5 Año 1"
    // ==, !=, <, >, <=, >=, Equals, GetHashCode, CompareTo por Absoluto
}
public static class Formato {
    public static string Numero(double valor, int decimales); // NumberFormatInfo propio: grupo ".", decimal ","
    public static string Usd(double valor);                    // "US$ " + Numero(valor, 0)
}
```

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
[Test] public void ElUnoDeMayoDelAnio1EsElDia120() { var f = Fecha.Crear(1, 5, 1); Assert.AreEqual(120, f.Absoluto); Assert.AreEqual("1/5 Año 1", f.ToString()); Assert.AreEqual(1, f.Campania); Assert.AreEqual(0, f.DiaDeCampania); }
[Test] public void FinDeAnioYFebreroSinBisiesto() { Assert.AreEqual(Fecha.Crear(1, 1, 2), Fecha.Crear(31, 12, 1).MasDias(1)); Assert.AreEqual(Fecha.Crear(1, 3, 1), Fecha.Crear(28, 2, 1).MasDias(1)); }
[Test] public void EneroPerteneceALaCampaniaAnterior() { Assert.AreEqual(1, Fecha.Crear(15, 1, 2).Campania); Assert.AreEqual(2, Fecha.Crear(1, 5, 2).Campania); }
[Test] public void DiaDeCampania() { Assert.AreEqual(259, Fecha.DiaDeCampaniaDe(15, 1)); Assert.AreEqual(364, Fecha.DiaDeCampaniaDe(30, 4)); Assert.AreEqual(137, Fecha.DiaDeCampaniaDe(15, 9)); Assert.AreEqual(Fecha.Crear(15, 1, 3), Fecha.EnCampania(2, 15, 1)); }
[Test] public void ParseaDiaMes() => Assert.AreEqual((15, 9), Fecha.ParseDiaMes("15/9"));
[Test] public void FormatoRioplatense() { Assert.AreEqual("1.234.567,89", Formato.Numero(1234567.891, 2)); Assert.AreEqual("US$ -4.500", Formato.Usd(-4500)); }
```

- [ ] **Step 2: Correr `FechaTests`.** Esperado: falla la compilación porque no existe `Fecha`.
- [ ] **Step 3: Implementar `Fecha` y `Formato`.**
- [ ] **Step 4: Correr `FechaTests`.** Esperado: 6/6 PASS.

---

### Task 3: Textura (triángulo USDA) y Saxton & Rawls

**Files:**
- Create: `AgroLife/Assets/Scripts/Sim/Suelo/Textura.cs`
- Test: `AgroLife/Assets/Tests/Sim/TexturaTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum ClaseTextural { Arenoso, ArenosoFranco, FrancoArenoso, Franco, FrancoLimoso, Limoso,
    FrancoArcillosoArenoso, FrancoArcilloso, FrancoArcillosoLimoso, ArcillosoArenoso, ArcillosoLimoso, Arcilloso }
public static class Textura {
    public static ClaseTextural Clasificar(double arena, double limo, double arcilla);       // en %
    public static double KsatMmH(double arena, double arcilla, double materiaOrganicaPct);   // Saxton & Rawls 2006, mm/h
    public static double DrenableMm(double arena, double arcilla, double materiaOrganicaPct); // (θS − θ33) × 1500 mm
}
```

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
[TestCase(92, 5, 3, ClaseTextural.Arenoso)]        [TestCase(82, 12, 6, ClaseTextural.ArenosoFranco)]
[TestCase(65, 25, 10, ClaseTextural.FrancoArenoso)] [TestCase(40, 40, 20, ClaseTextural.Franco)]
[TestCase(20, 65, 15, ClaseTextural.FrancoLimoso)]  [TestCase(5, 88, 7, ClaseTextural.Limoso)]
[TestCase(60, 13, 27, ClaseTextural.FrancoArcillosoArenoso)] [TestCase(35, 33, 32, ClaseTextural.FrancoArcilloso)]
[TestCase(10, 58, 32, ClaseTextural.FrancoArcillosoLimoso)]  [TestCase(50, 10, 40, ClaseTextural.ArcillosoArenoso)]
[TestCase(8, 47, 45, ClaseTextural.ArcillosoLimoso)] [TestCase(20, 20, 60, ClaseTextural.Arcilloso)]
public void ClasificaSegunUsda(double a, double l, double c, ClaseTextural esperada) => Assert.AreEqual(esperada, Textura.Clasificar(a, l, c));

[Test] public void SaxtonRawlsFrancoLimoso() {   // arena 20, arcilla 15, MO = 1.8 × 1.724
    Assert.AreEqual(19.9009, Textura.KsatMmH(20, 15, 1.8 * 1.724), 0.001);
    Assert.AreEqual(281.555, Textura.DrenableMm(20, 15, 1.8 * 1.724), 0.01);
}
[Test] public void SaxtonRawlsArenosoDrenaMasQueArcilloso() {
    Assert.AreEqual(84.4856, Textura.KsatMmH(82, 6, 0.6 * 1.724), 0.001);
    Assert.AreEqual(9.2073, Textura.KsatMmH(10, 32, 2.0 * 1.724), 0.001);
}
```

- [ ] **Step 2: Correr `TexturaTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar `Textura`.**

`Clasificar`: con `s = arena`, `l = limo`, `c = arcilla`, se evalúan las reglas en este orden y gana la primera que se cumple:
```
Arenoso:                l + 1.5c < 15
ArenosoFranco:          l + 2c < 30
FrancoArenoso:          (c >= 7 && c < 20 && s > 52) || (c < 7 && l < 50)
Franco:                 c >= 7 && c < 27 && l >= 28 && l < 50 && s <= 52
FrancoLimoso:           (l >= 50 && c >= 12 && c < 27) || (l >= 50 && l < 80 && c < 12)
Limoso:                 l >= 80 && c < 12
FrancoArcillosoArenoso: c >= 20 && c < 35 && l < 28 && s > 45
FrancoArcilloso:        c >= 27 && c < 40 && s > 20 && s <= 45
FrancoArcillosoLimoso:  c >= 27 && c < 40 && s <= 20
ArcillosoArenoso:       c >= 35 && s > 45
ArcillosoLimoso:        c >= 40 && l >= 40
Arcilloso:              resto
```

Saxton & Rawls (2006). S y C van como fracción (0–1); MO va en %:
```
θ1500t = −0.024S + 0.487C + 0.006MO + 0.005·S·MO − 0.013·C·MO + 0.068·S·C + 0.031 ;  θ1500 = θ1500t + (0.14θ1500t − 0.02)
θ33t   = −0.251S + 0.195C + 0.011MO + 0.006·S·MO − 0.027·C·MO + 0.452·S·C + 0.299 ;  θ33 = θ33t + (1.283θ33t² − 0.374θ33t − 0.015)
θs33t  =  0.278S + 0.034C + 0.022MO − 0.018·S·MO − 0.027·C·MO − 0.584·S·C + 0.078 ;  θs33 = θs33t + (0.636θs33t − 0.107)
θS = θ33 + θs33 − 0.097S + 0.043 ;  B = (ln 1500 − ln 33)/(ln θ33 − ln θ1500) ;  λ = 1/B
Ksat = 1930 (θS − θ33)^(3 − λ)   [mm/h]
```

- [ ] **Step 4: Correr `TexturaTests`.** Esperado: 14/14 PASS.

---

### Task 4: Datos del juego, contrato de archivos y validación

**Files:**
- Create: `AgroLife/Assets/Data/crops.json`, `pests.json`, `economy.json`, `soils.json`
- Create: `AgroLife/Assets/Tests/Sim/Datos/` con copias exactas de esos cuatro archivos, más `region.json`, `lotes.json` y `clima.json` de la región de prueba.
- Create: `AgroLife/Assets/Scripts/Sim/Datos/Parametros.cs`
- Create: `AgroLife/Assets/Scripts/Sim/Datos/DatosJuego.cs`
- Create: `AgroLife/Assets/Tests/Sim/DatosPrueba.cs` (helper)
- Test: `AgroLife/Assets/Tests/Sim/DatosTests.cs`

Los tests usan **copias congeladas** en `Tests/Sim/Datos/`. Así la calibración (plan 4) puede ajustar `Assets/Data` sin romper los tests unitarios. Solo `DatosRealesSonValidos` lee `Assets/Data`.

**Interfaces:**
- Consumes: `Textura` (Tarea 3), `JsonSim` (Tarea 1), `Fecha.ParseDiaMes` (Tarea 2).
- Produces. Los nombres de campo son los de los JSON en PascalCase; Newtonsoft no distingue mayúsculas al leer.
```csharp
public enum Etapa { Siembra, Emergencia, Vegetativo, Floracion, Llenado, Madurez }
public enum Genetica { Corto, Largo }
public enum Grano { Trigo, Maiz, Soja }
public enum FaseEnso { Nino, Neutro, Nina }

public sealed class CultivosArchivo { public List<CultivoParams> Cultivos; }
public sealed class CultivoParams {
    public string Id, Nombre; public Grano Grano; public double TemperaturaBase; public Ventana VentanaSiembra;
    public Dictionary<Genetica, GeneticaParams> Geneticas; public FactorIp FactorIp; public List<PuntoCurva> CurvaFecha;
    public Dictionary<Etapa, double> Kc, SensibilidadAgua, SensibilidadAnegamiento;   // etapa ausente = 0
    public EventoTermico Helada, Calor; public NitrogenoParams Nitrogeno /* null en soja */; public FosforoParams Fosforo;
    public int DiasACosecha; public double FactorClimaEsperado; public PlanTipico PlanTipico; public Fotoperiodo Fotoperiodo /* null salvo soja */;
}
public sealed class Ventana { public string Desde, Hasta; }
public sealed class GeneticaParams { public double RpotQqHa; public UmbralesGd Gd; }
public sealed class UmbralesGd { public double Emergencia, Vegetativo, Floracion, Llenado, Madurez; }
public sealed class FactorIp { public double A, B; }                     // f(IP) = A + B·IP/100
public sealed class PuntoCurva { public string Fecha; public double Perdida; }
public sealed class EventoTermico { public double Umbral; public Dictionary<Etapa, double> Perdida; }
public sealed class NitrogenoParams { public double KgPorQq, MinRelativo, MineralizacionEsperadaKgHa; }
public sealed class FosforoParams { public double PCriticoPpm, MinRelativo, ExportacionKgPorQq; }
public sealed class PlanTipico { public string Siembra; public Genetica Genetica; public double NKgHa, PKgHa; }
public sealed class Fotoperiodo { public string FechaReferencia; public double AcortamientoPorDia, FactorMinimo; }
public sealed class PlagasArchivo { public List<PlagaParams> Plagas; }
public sealed class PlagaParams { public string Id, Nombre, Unidad; public List<string> Cultivos; public List<Etapa> Etapas;
    public double ProbDiaria, TempMinima, MultiplicadorLluvia, Umbral, Eficacia, ProductoUsdHa, PulverizacionUsdHa, EscalaNivel; public Rango Severidad; }
public sealed class Rango { public double Min, Max; }
public sealed class EconomiaParams { public Dictionary<Grano, GranoParams> Granos; public Dictionary<string, CostosCultivo> Costos;
    public Fertilizantes Fertilizantes; public double CosechaPorcentaje, ComisionVenta; public Flete Flete; public Silobolsa Silobolsa;
    public Cuenta Cuenta; public CapitalInicial CapitalInicial; }
public sealed class GranoParams { public double ReferenciaUsdT, VolatilidadDiaria, VidaMediaDias; public double[] Estacional; }
public sealed class CostosCultivo { public double SemillaUsdHa, AgroquimicosUsdHa, SiembraUsdHa, PulverizacionesUsdHa; }
public sealed class Fertilizantes { public double NitrogenoUsdKg, FosforoUsdKg, AplicacionUsdHa; }
public sealed class Flete { public double FijoUsdT, UsdTKm; }
public sealed class Silobolsa { public double EmbolsadoUsdT, RiesgoMensual, PerdidaMin, PerdidaMax; }
public sealed class Cuenta { public double LimiteDescubiertoUsd, TasaAnualDescubierto, AlertaFraccion; }
public sealed class CapitalInicial { public double Hectareas; public string Cultivo; }
public sealed class SuelosArchivo { public ModeloSuelo Modelo; }
public sealed class ModeloSuelo { public double HorasInfiltracion, HorasDrenaje, KsatAnegamientoMmH, FraccionAgotamiento, KcSueloDesnudo,
    AguaInicialFraccion, NInicialKgHa, KMineralizacion, PpmPorKgP, PBrayMinimo; }
public sealed class RegionParams { public string Id, Nombre; public double Latitud, Longitud, PrecioBaseTierraUsdHa, ArrendamientoBaseQqHa; public DistribucionP FosforoBray; }
public sealed class DistribucionP { public double Media, Desvio, Minimo; }
public sealed class LotesArchivo { public List<LoteDatos> Lotes; }
public sealed class LoteDatos {
    public int Id; public double SuperficieHa, Arena, Limo, Arcilla, CorgPct, CcMm, PmpMm, Ip, DistanciaAcopioKm; public string UnidadSuelo;
    public ClaseTextural Clase; public double AuMaxMm, KsatMmH, DrenableMm;  // derivados
    public void CalcularDerivados(); // AuMax = Cc − Pmp; MO = CorgPct × 1.724; Ksat y Drenable con Textura
}
public sealed class ClimaParams { public List<MesClima> Meses; public Autocorrelacion Autocorrelacion; public EnsoParams Enso; }
public sealed class MesClima { public double PSecoAHumedo, PHumedoAHumedo, GammaForma, GammaEscala;
    public MediaDesvio TmaxSeco, TmaxHumedo, TminSeco, TminHumedo, RadSeco, RadHumedo; }
public sealed class MediaDesvio { public double Media, Desvio; }
public sealed class Autocorrelacion { public double Tmax, Tmin, Rad; }
public sealed class EnsoParams { public Dictionary<FaseEnso, double> Frecuencias; public Dictionary<FaseEnso, List<MultiplicadorMes>> Multiplicadores; }
public sealed class MultiplicadorMes { public double Frecuencia, Cantidad; }

public sealed class ArchivosDatos { public string Crops, Pests, Economy, Soils, Region, Lotes, Clima; }
public sealed class DatosInvalidosException : Exception { public DatosInvalidosException(string mensaje) : base(mensaje) { } }
public sealed class DatosJuego {
    public Dictionary<string, CultivoParams> Cultivos; public List<PlagaParams> Plagas; public EconomiaParams Economia;
    public ModeloSuelo Suelo; public RegionParams Region; public List<LoteDatos> Lotes; public ClimaParams Clima;
    public static DatosJuego Desde(ArchivosDatos a);  // parsea, calcula derivados, valida; lanza DatosInvalidosException
    public LoteDatos Lote(int id);                    // Lotes[id - 1]
}
```
- Produces (tests): `DatosPrueba.Archivos()`, `DatosPrueba.Cargar()` y `DatosPrueba.Leer(string archivo)`. Leen de `Path.Combine("Assets","Tests","Sim","Datos", archivo)`; en el Editor, el directorio actual es la raíz del proyecto.

- [ ] **Step 1: Crear `Assets/Data/crops.json`**

```json
{ "cultivos": [
  { "id": "trigo", "nombre": "Trigo", "grano": "Trigo", "temperaturaBase": 0, "ventanaSiembra": { "desde": "20/5", "hasta": "20/7" },
    "geneticas": { "Corto": { "rpotQqHa": 58, "gd": { "emergencia": 150, "vegetativo": 250, "floracion": 1350, "llenado": 1500, "madurez": 2050 } },
                   "Largo": { "rpotQqHa": 62, "gd": { "emergencia": 150, "vegetativo": 250, "floracion": 1500, "llenado": 1650, "madurez": 2200 } } },
    "factorIp": { "a": 0.3, "b": 0.7 },
    "curvaFecha": [ { "fecha": "20/5", "perdida": 0.06 }, { "fecha": "10/6", "perdida": 0 }, { "fecha": "5/7", "perdida": 0 }, { "fecha": "20/7", "perdida": 0.12 } ],
    "kc": { "Siembra": 0.3, "Emergencia": 0.4, "Vegetativo": 0.8, "Floracion": 1.15, "Llenado": 1.0, "Madurez": 0.3 },
    "sensibilidadAgua": { "Vegetativo": 0.002, "Floracion": 0.03, "Llenado": 0.012 },
    "sensibilidadAnegamiento": { "Emergencia": 0.01, "Vegetativo": 0.004, "Floracion": 0.01, "Llenado": 0.006 },
    "helada": { "umbral": -2, "perdida": { "Floracion": 0.08, "Llenado": 0.04 } },
    "calor": { "umbral": 32, "perdida": { "Floracion": 0.03, "Llenado": 0.015 } },
    "nitrogeno": { "kgPorQq": 3.0, "minRelativo": 0.45, "mineralizacionEsperadaKgHa": 40 },
    "fosforo": { "pCriticoPpm": 18, "minRelativo": 0.7, "exportacionKgPorQq": 0.35 },
    "diasACosecha": 7, "factorClimaEsperado": 0.8,
    "planTipico": { "siembra": "15/6", "genetica": "Largo", "nKgHa": 90, "pKgHa": 15 }, "fotoperiodo": null },
  { "id": "maiz", "nombre": "Maíz", "grano": "Maiz", "temperaturaBase": 8, "ventanaSiembra": { "desde": "15/9", "hasta": "31/12" },
    "geneticas": { "Corto": { "rpotQqHa": 120, "gd": { "emergencia": 70, "vegetativo": 120, "floracion": 620, "llenado": 1000, "madurez": 1650 } },
                   "Largo": { "rpotQqHa": 135, "gd": { "emergencia": 70, "vegetativo": 120, "floracion": 700, "llenado": 1100, "madurez": 1850 } } },
    "factorIp": { "a": 0.3, "b": 0.7 },
    "curvaFecha": [ { "fecha": "15/9", "perdida": 0.04 }, { "fecha": "1/10", "perdida": 0 }, { "fecha": "20/10", "perdida": 0 },
                    { "fecha": "15/11", "perdida": 0.10 }, { "fecha": "10/12", "perdida": 0.12 }, { "fecha": "31/12", "perdida": 0.20 } ],
    "kc": { "Siembra": 0.3, "Emergencia": 0.35, "Vegetativo": 0.8, "Floracion": 1.2, "Llenado": 1.0, "Madurez": 0.35 },
    "sensibilidadAgua": { "Vegetativo": 0.003, "Floracion": 0.025, "Llenado": 0.008 },
    "sensibilidadAnegamiento": { "Emergencia": 0.02, "Vegetativo": 0.006, "Floracion": 0.008, "Llenado": 0.004 },
    "helada": { "umbral": -1, "perdida": { "Emergencia": 0.05, "Vegetativo": 0.03, "Llenado": 0.08 } },
    "calor": { "umbral": 35, "perdida": { "Floracion": 0.02, "Llenado": 0.008 } },
    "nitrogeno": { "kgPorQq": 2.2, "minRelativo": 0.4, "mineralizacionEsperadaKgHa": 70 },
    "fosforo": { "pCriticoPpm": 16, "minRelativo": 0.7, "exportacionKgPorQq": 0.30 },
    "diasACosecha": 15, "factorClimaEsperado": 0.75,
    "planTipico": { "siembra": "1/10", "genetica": "Largo", "nKgHa": 120, "pKgHa": 20 }, "fotoperiodo": null },
  { "id": "soja1", "nombre": "Soja de primera", "grano": "Soja", "temperaturaBase": 10, "ventanaSiembra": { "desde": "15/10", "hasta": "15/12" },
    "geneticas": { "Corto": { "rpotQqHa": 47, "gd": { "emergencia": 80, "vegetativo": 130, "floracion": 450, "llenado": 680, "madurez": 1300 } },
                   "Largo": { "rpotQqHa": 50, "gd": { "emergencia": 80, "vegetativo": 130, "floracion": 500, "llenado": 750, "madurez": 1450 } } },
    "factorIp": { "a": 0.3, "b": 0.7 },
    "curvaFecha": [ { "fecha": "15/10", "perdida": 0.04 }, { "fecha": "1/11", "perdida": 0 }, { "fecha": "25/11", "perdida": 0 }, { "fecha": "15/12", "perdida": 0.12 } ],
    "kc": { "Siembra": 0.3, "Emergencia": 0.35, "Vegetativo": 0.75, "Floracion": 1.05, "Llenado": 1.1, "Madurez": 0.4 },
    "sensibilidadAgua": { "Vegetativo": 0.002, "Floracion": 0.006, "Llenado": 0.015 },
    "sensibilidadAnegamiento": { "Emergencia": 0.02, "Vegetativo": 0.005, "Floracion": 0.006, "Llenado": 0.005 },
    "helada": { "umbral": -1, "perdida": { "Emergencia": 0.05, "Vegetativo": 0.03, "Llenado": 0.10 } },
    "calor": { "umbral": 35, "perdida": { "Floracion": 0.01, "Llenado": 0.01 } },
    "nitrogeno": null,
    "fosforo": { "pCriticoPpm": 14, "minRelativo": 0.7, "exportacionKgPorQq": 0.55 },
    "diasACosecha": 10, "factorClimaEsperado": 0.8,
    "planTipico": { "siembra": "15/11", "genetica": "Largo", "nKgHa": 0, "pKgHa": 15 },
    "fotoperiodo": { "fechaReferencia": "15/11", "acortamientoPorDia": 0.003, "factorMinimo": 0.75 } },
  { "id": "soja2", "nombre": "Soja de segunda", "grano": "Soja", "temperaturaBase": 10, "ventanaSiembra": { "desde": "15/11", "hasta": "15/1" },
    "geneticas": { "Corto": { "rpotQqHa": 38, "gd": { "emergencia": 80, "vegetativo": 130, "floracion": 450, "llenado": 680, "madurez": 1300 } },
                   "Largo": { "rpotQqHa": 40, "gd": { "emergencia": 80, "vegetativo": 130, "floracion": 500, "llenado": 750, "madurez": 1450 } } },
    "factorIp": { "a": 0.3, "b": 0.7 },
    "curvaFecha": [ { "fecha": "15/11", "perdida": 0 }, { "fecha": "10/12", "perdida": 0.03 }, { "fecha": "31/12", "perdida": 0.12 }, { "fecha": "15/1", "perdida": 0.25 } ],
    "kc": { "Siembra": 0.3, "Emergencia": 0.35, "Vegetativo": 0.75, "Floracion": 1.05, "Llenado": 1.1, "Madurez": 0.4 },
    "sensibilidadAgua": { "Vegetativo": 0.002, "Floracion": 0.006, "Llenado": 0.015 },
    "sensibilidadAnegamiento": { "Emergencia": 0.02, "Vegetativo": 0.005, "Floracion": 0.006, "Llenado": 0.005 },
    "helada": { "umbral": -1, "perdida": { "Emergencia": 0.05, "Vegetativo": 0.03, "Llenado": 0.10 } },
    "calor": { "umbral": 35, "perdida": { "Floracion": 0.01, "Llenado": 0.01 } },
    "nitrogeno": null,
    "fosforo": { "pCriticoPpm": 14, "minRelativo": 0.7, "exportacionKgPorQq": 0.55 },
    "diasACosecha": 10, "factorClimaEsperado": 0.75,
    "planTipico": { "siembra": "5/12", "genetica": "Largo", "nKgHa": 0, "pKgHa": 10 },
    "fotoperiodo": { "fechaReferencia": "15/11", "acortamientoPorDia": 0.003, "factorMinimo": 0.75 } }
] }
```

- [ ] **Step 2: Crear `pests.json`, `economy.json` y `soils.json` en `Assets/Data`**

`pests.json`:
```json
{ "plagas": [
  { "id": "chinches", "nombre": "Chinches", "cultivos": ["soja1", "soja2"], "etapas": ["Llenado"], "probDiaria": 0.012, "tempMinima": 15,
    "multiplicadorLluvia": 1.0, "severidad": { "min": 0.04, "max": 0.30 }, "umbral": 0.10, "eficacia": 0.85,
    "productoUsdHa": 12, "pulverizacionUsdHa": 9, "unidad": "chinches/m", "escalaNivel": 10 },
  { "id": "isoca_cogollera", "nombre": "Isoca cogollera", "cultivos": ["maiz"], "etapas": ["Vegetativo"], "probDiaria": 0.010, "tempMinima": 18,
    "multiplicadorLluvia": 1.0, "severidad": { "min": 0.03, "max": 0.20 }, "umbral": 0.08, "eficacia": 0.80,
    "productoUsdHa": 18, "pulverizacionUsdHa": 9, "unidad": "% de plantas dañadas", "escalaNivel": 250 },
  { "id": "roya", "nombre": "Roya", "cultivos": ["trigo"], "etapas": ["Vegetativo", "Floracion", "Llenado"], "probDiaria": 0.004, "tempMinima": 10,
    "multiplicadorLluvia": 3.0, "severidad": { "min": 0.03, "max": 0.25 }, "umbral": 0.05, "eficacia": 0.85,
    "productoUsdHa": 22, "pulverizacionUsdHa": 9, "unidad": "% de severidad foliar", "escalaNivel": 200 }
] }
```
`economy.json` (los meses de `estacional` van de enero a diciembre):
```json
{ "granos": {
    "Trigo": { "referenciaUsdT": 200, "estacional": [0.95,0.97,0.99,1.0,1.01,1.02,1.03,1.04,1.04,1.02,0.98,0.95], "volatilidadDiaria": 0.012, "vidaMediaDias": 60 },
    "Maiz":  { "referenciaUsdT": 170, "estacional": [1.03,1.02,0.98,0.96,0.95,0.96,0.98,1.0,1.02,1.03,1.04,1.03], "volatilidadDiaria": 0.012, "vidaMediaDias": 60 },
    "Soja":  { "referenciaUsdT": 300, "estacional": [1.02,1.03,1.0,0.96,0.95,0.97,0.99,1.0,1.01,1.02,1.02,1.03], "volatilidadDiaria": 0.011, "vidaMediaDias": 60 } },
  "costos": {
    "trigo": { "semillaUsdHa": 72,  "agroquimicosUsdHa": 40, "siembraUsdHa": 65, "pulverizacionesUsdHa": 27 },
    "maiz":  { "semillaUsdHa": 190, "agroquimicosUsdHa": 80, "siembraUsdHa": 65, "pulverizacionesUsdHa": 27 },
    "soja1": { "semillaUsdHa": 50,  "agroquimicosUsdHa": 85, "siembraUsdHa": 65, "pulverizacionesUsdHa": 36 },
    "soja2": { "semillaUsdHa": 55,  "agroquimicosUsdHa": 70, "siembraUsdHa": 65, "pulverizacionesUsdHa": 27 } },
  "fertilizantes": { "nitrogenoUsdKg": 1.2, "fosforoUsdKg": 3.3, "aplicacionUsdHa": 10 },
  "cosechaPorcentaje": 0.08, "comisionVenta": 0.02,
  "flete": { "fijoUsdT": 5, "usdTKm": 0.25 },
  "silobolsa": { "embolsadoUsdT": 4, "riesgoMensual": 0.03, "perdidaMin": 0.05, "perdidaMax": 0.25 },
  "cuenta": { "limiteDescubiertoUsd": 50000, "tasaAnualDescubierto": 0.15, "alertaFraccion": 0.8 },
  "capitalInicial": { "hectareas": 250, "cultivo": "soja1" } }
```
`soils.json` (el plan 2 le agrega `respaldoIP`):
```json
{ "modelo": { "horasInfiltracion": 6, "horasDrenaje": 4, "ksatAnegamientoMmH": 12, "fraccionAgotamiento": 0.5, "kcSueloDesnudo": 0.25,
              "aguaInicialFraccion": 0.6, "nInicialKgHa": 40, "kMineralizacion": 0.18, "ppmPorKgP": 0.3, "pBrayMinimo": 3 } }
```

- [ ] **Step 3: Crear la región de prueba en `Assets/Tests/Sim/Datos/`** y copiar sin cambios los cuatro archivos del paso 1 y 2.

`region.json`:
```json
{ "id": "prueba", "nombre": "Región de prueba", "latitud": -33.89, "longitud": -60.57,
  "precioBaseTierraUsdHa": 14000, "arrendamientoBaseQqHa": 17, "fosforoBray": { "media": 14, "desvio": 5, "minimo": 4 } }
```
`lotes.json`: es el contrato que el plan 2 tiene que respetar, más `poligono`, que Sim ignora. El IP promedio ponderado por superficie da 78.
```json
{ "lotes": [
  { "id": 1, "superficieHa": 100, "arena": 20, "limo": 65, "arcilla": 15, "corgPct": 1.8, "ccMm": 560, "pmpMm": 260, "ip": 90,  "unidadSuelo": "P1", "distanciaAcopioKm": 15 },
  { "id": 2, "superficieHa": 80,  "arena": 40, "limo": 40, "arcilla": 20, "corgPct": 1.5, "ccMm": 500, "pmpMm": 250, "ip": 80,  "unidadSuelo": "P2", "distanciaAcopioKm": 25 },
  { "id": 3, "superficieHa": 60,  "arena": 65, "limo": 25, "arcilla": 10, "corgPct": 1.0, "ccMm": 380, "pmpMm": 170, "ip": 60,  "unidadSuelo": "P3", "distanciaAcopioKm": 30 },
  { "id": 4, "superficieHa": 120, "arena": 10, "limo": 58, "arcilla": 32, "corgPct": 2.0, "ccMm": 600, "pmpMm": 330, "ip": 70,  "unidadSuelo": "P4", "distanciaAcopioKm": 10 },
  { "id": 5, "superficieHa": 40,  "arena": 82, "limo": 12, "arcilla": 6,  "corgPct": 0.6, "ccMm": 240, "pmpMm": 90,  "ip": 40,  "unidadSuelo": "P5", "distanciaAcopioKm": 40 },
  { "id": 6, "superficieHa": 100, "arena": 18, "limo": 64, "arcilla": 18, "corgPct": 1.7, "ccMm": 570, "pmpMm": 270, "ip": 100, "unidadSuelo": "P6", "distanciaAcopioKm": 20 }
] }
```
`clima.json`:
- Valores aproximados de Pergamino, no calibrados.
- Los multiplicadores de Neutro son todos 1. Los de Niño y Niña valen 1 de abril a julio; de agosto a marzo, Niño usa 1.1 y Niña 0.9.
- Las listas van de enero a diciembre.
```json
{ "meses": [
    {"pSecoAHumedo": 0.209, "pHumedoAHumedo": 0.4, "gammaForma": 0.8, "gammaEscala": 17.19, "tmaxSeco": {"media": 31.0, "desvio": 3.5}, "tmaxHumedo": {"media": 28.5, "desvio": 3.5}, "tminSeco": {"media": 17.1, "desvio": 3.0}, "tminHumedo": {"media": 19.0, "desvio": 3.0}, "radSeco": {"media": 27.5, "desvio": 3.8}, "radHumedo": {"media": 15.0, "desvio": 3.8}},
    {"pSecoAHumedo": 0.2, "pHumedoAHumedo": 0.4, "gammaForma": 0.8, "gammaEscala": 18.75, "tmaxSeco": {"media": 29.5, "desvio": 3.5}, "tmaxHumedo": {"media": 27.0, "desvio": 3.5}, "tminSeco": {"media": 16.4, "desvio": 3.0}, "tminHumedo": {"media": 18.3, "desvio": 3.0}, "radSeco": {"media": 24.2, "desvio": 3.3}, "radHumedo": {"media": 13.2, "desvio": 3.3}},
    {"pSecoAHumedo": 0.209, "pHumedoAHumedo": 0.4, "gammaForma": 0.8, "gammaEscala": 19.53, "tmaxSeco": {"media": 27.3, "desvio": 3.5}, "tmaxHumedo": {"media": 24.8, "desvio": 3.5}, "tminSeco": {"media": 14.4, "desvio": 3.0}, "tminHumedo": {"media": 16.3, "desvio": 3.0}, "radSeco": {"media": 19.8, "desvio": 2.7}, "radHumedo": {"media": 10.8, "desvio": 2.7}},
    {"pSecoAHumedo": 0.183, "pHumedoAHumedo": 0.4, "gammaForma": 0.8, "gammaEscala": 16.96, "tmaxSeco": {"media": 23.3, "desvio": 3.5}, "tmaxHumedo": {"media": 20.8, "desvio": 3.5}, "tminSeco": {"media": 10.1, "desvio": 3.0}, "tminHumedo": {"media": 12.0, "desvio": 3.0}, "radSeco": {"media": 14.3, "desvio": 1.9}, "radHumedo": {"media": 7.8, "desvio": 1.9}},
    {"pSecoAHumedo": 0.125, "pHumedoAHumedo": 0.35, "gammaForma": 0.8, "gammaEscala": 13.75, "tmaxSeco": {"media": 19.3, "desvio": 4.0}, "tmaxHumedo": {"media": 16.8, "desvio": 4.0}, "tminSeco": {"media": 6.6, "desvio": 3.8}, "tminHumedo": {"media": 8.5, "desvio": 3.8}, "radSeco": {"media": 11.0, "desvio": 1.5}, "radHumedo": {"media": 6.0, "desvio": 1.5}},
    {"pSecoAHumedo": 0.1, "pHumedoAHumedo": 0.35, "gammaForma": 0.8, "gammaEscala": 9.38, "tmaxSeco": {"media": 15.7, "desvio": 4.0}, "tmaxHumedo": {"media": 13.2, "desvio": 4.0}, "tminSeco": {"media": 3.6, "desvio": 3.8}, "tminHumedo": {"media": 5.5, "desvio": 3.8}, "radSeco": {"media": 8.8, "desvio": 1.2}, "radHumedo": {"media": 4.8, "desvio": 1.2}},
    {"pSecoAHumedo": 0.096, "pHumedoAHumedo": 0.35, "gammaForma": 0.8, "gammaEscala": 9.38, "tmaxSeco": {"media": 15.0, "desvio": 4.0}, "tmaxHumedo": {"media": 12.5, "desvio": 4.0}, "tminSeco": {"media": 2.6, "desvio": 3.8}, "tminHumedo": {"media": 4.5, "desvio": 3.8}, "radSeco": {"media": 9.9, "desvio": 1.3}, "radHumedo": {"media": 5.4, "desvio": 1.3}},
    {"pSecoAHumedo": 0.096, "pHumedoAHumedo": 0.35, "gammaForma": 0.8, "gammaEscala": 10.94, "tmaxSeco": {"media": 17.5, "desvio": 4.0}, "tmaxHumedo": {"media": 15.0, "desvio": 4.0}, "tminSeco": {"media": 3.6, "desvio": 3.8}, "tminHumedo": {"media": 5.5, "desvio": 3.8}, "radSeco": {"media": 13.2, "desvio": 1.8}, "radHumedo": {"media": 7.2, "desvio": 1.8}},
    {"pSecoAHumedo": 0.163, "pHumedoAHumedo": 0.35, "gammaForma": 0.8, "gammaEscala": 11.46, "tmaxSeco": {"media": 20.0, "desvio": 4.0}, "tmaxHumedo": {"media": 17.5, "desvio": 4.0}, "tminSeco": {"media": 5.6, "desvio": 3.8}, "tminHumedo": {"media": 7.5, "desvio": 3.8}, "radSeco": {"media": 17.6, "desvio": 2.4}, "radHumedo": {"media": 9.6, "desvio": 2.4}},
    {"pSecoAHumedo": 0.209, "pHumedoAHumedo": 0.4, "gammaForma": 0.8, "gammaEscala": 17.19, "tmaxSeco": {"media": 23.3, "desvio": 3.5}, "tmaxHumedo": {"media": 20.8, "desvio": 3.5}, "tminSeco": {"media": 9.1, "desvio": 3.0}, "tminHumedo": {"media": 11.0, "desvio": 3.0}, "radSeco": {"media": 22.0, "desvio": 3.0}, "radHumedo": {"media": 12.0, "desvio": 3.0}},
    {"pSecoAHumedo": 0.218, "pHumedoAHumedo": 0.4, "gammaForma": 0.8, "gammaEscala": 16.41, "tmaxSeco": {"media": 27.0, "desvio": 3.5}, "tmaxHumedo": {"media": 24.5, "desvio": 3.5}, "tminSeco": {"media": 12.4, "desvio": 3.0}, "tminHumedo": {"media": 14.3, "desvio": 3.0}, "radSeco": {"media": 26.4, "desvio": 3.6}, "radHumedo": {"media": 14.4, "desvio": 3.6}},
    {"pSecoAHumedo": 0.209, "pHumedoAHumedo": 0.4, "gammaForma": 0.8, "gammaEscala": 17.97, "tmaxSeco": {"media": 30.0, "desvio": 3.5}, "tmaxHumedo": {"media": 27.5, "desvio": 3.5}, "tminSeco": {"media": 15.4, "desvio": 3.0}, "tminHumedo": {"media": 17.3, "desvio": 3.0}, "radSeco": {"media": 28.6, "desvio": 3.9}, "radHumedo": {"media": 15.6, "desvio": 3.9}}
  ],
  "autocorrelacion": { "tmax": 0.65, "tmin": 0.6, "rad": 0.4 },
  "enso": { "frecuencias": { "Nino": 0.25, "Neutro": 0.45, "Nina": 0.30 },
    "multiplicadores": {
      "Nino":   [ {"frecuencia":1.1,"cantidad":1.1}, {"frecuencia":1.1,"cantidad":1.1}, {"frecuencia":1.1,"cantidad":1.1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1.1,"cantidad":1.1}, {"frecuencia":1.1,"cantidad":1.1}, {"frecuencia":1.1,"cantidad":1.1}, {"frecuencia":1.1,"cantidad":1.1}, {"frecuencia":1.1,"cantidad":1.1} ],
      "Neutro": [ {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1} ],
      "Nina":   [ {"frecuencia":0.9,"cantidad":0.9}, {"frecuencia":0.9,"cantidad":0.9}, {"frecuencia":0.9,"cantidad":0.9}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":1,"cantidad":1}, {"frecuencia":0.9,"cantidad":0.9}, {"frecuencia":0.9,"cantidad":0.9}, {"frecuencia":0.9,"cantidad":0.9}, {"frecuencia":0.9,"cantidad":0.9}, {"frecuencia":0.9,"cantidad":0.9} ] } } }
```

- [ ] **Step 4: Escribir los tests que fallan** (`DatosTests`, más el helper `DatosPrueba`)

```csharp
[Test] public void DatosRealesSonValidos() {
    var a = DatosPrueba.Archivos();
    a.Crops = File.ReadAllText("Assets/Data/crops.json"); a.Pests = File.ReadAllText("Assets/Data/pests.json");
    a.Economy = File.ReadAllText("Assets/Data/economy.json"); a.Soils = File.ReadAllText("Assets/Data/soils.json");
    Assert.AreEqual(4, DatosJuego.Desde(a).Cultivos.Count);
}
[Test] public void CalculaDerivadosDelLote() { var l = DatosPrueba.Cargar().Lote(1);
    Assert.AreEqual(ClaseTextural.FrancoLimoso, l.Clase); Assert.AreEqual(300, l.AuMaxMm, 1e-9); Assert.AreEqual(19.9009, l.KsatMmH, 0.001); }
[Test] public void TexturaQueNoSuma100SeRechaza() { var a = DatosPrueba.Archivos(); a.Lotes = a.Lotes.Replace("\"arcilla\": 15,", "\"arcilla\": 30,");
    StringAssert.Contains("lotes.json: lote 1: arena + limo + arcilla = 115", Assert.Throws<DatosInvalidosException>(() => DatosJuego.Desde(a)).Message); }
[Test] public void ClimaConOnceMesesSeRechaza() { var a = DatosPrueba.Archivos(); var c = JsonSim.Deserializar<ClimaParams>(a.Clima); c.Meses.RemoveAt(11); a.Clima = JsonSim.Serializar(c);
    StringAssert.Contains("clima.json: se esperaban 12 meses y hay 11", Assert.Throws<DatosInvalidosException>(() => DatosJuego.Desde(a)).Message); }
[Test] public void FaltaUnCultivoSeRechaza() { var a = DatosPrueba.Archivos(); var c = JsonSim.Deserializar<CultivosArchivo>(a.Crops); c.Cultivos.RemoveAll(x => x.Id == "soja2"); a.Crops = JsonSim.Serializar(c);
    StringAssert.Contains("crops.json: falta el cultivo soja2", Assert.Throws<DatosInvalidosException>(() => DatosJuego.Desde(a)).Message); }
[Test] public void JsonMalFormadoNombraElArchivo() { var a = DatosPrueba.Archivos(); a.Region = "{";
    StringAssert.StartsWith("Datos de región inválidos:\nregion.json:", Assert.Throws<DatosInvalidosException>(() => DatosJuego.Desde(a)).Message); }
```

- [ ] **Step 5: Correr `DatosTests`.** Esperado: falla la compilación.

- [ ] **Step 6: Implementar `Parametros.cs` y `DatosJuego.cs`.**

`Desde` hace todo esto y después lanza **un solo** `DatosInvalidosException` con `"Datos de región inválidos:\n" + string.Join("\n", errores)`:
1. Parsear cada archivo con `JsonSim`. Un error de parseo agrega `"{archivo}: {ex.Message}"`, y se saltean las validaciones que dependen de ese archivo (nunca un `NullReferenceException`).
2. Llamar a `CalcularDerivados()` en cada lote.
3. Validar y juntar todos los errores. Formatos exactos:
   - `crops.json: falta el cultivo {id}` (para trigo, maiz, soja1 y soja2)
   - `crops.json: {id}: falta la genética {g}`
   - `crops.json: {id}: los umbrales de grados-día de {g} tienen que ser crecientes`
   - `pests.json: {id}: cultivo desconocido {c}`
   - `economy.json: falta el grano {g}`
   - `economy.json: {g}: estacional tiene que tener 12 valores`
   - `economy.json: faltan los costos de {id}`
   - `clima.json: se esperaban 12 meses y hay {n}`
   - `clima.json: mes {m}: probabilidad fuera de [0, 1]`
   - `clima.json: las frecuencias de fase tienen que sumar 1` (tolerancia ±0.01)
   - `clima.json: {fase}: se esperaban 12 multiplicadores`
   - `lotes.json: no hay lotes`
   - `lotes.json: los ids tienen que ser 1..N consecutivos (posición {i}, id {id})`
   - `lotes.json: lote {id}: arena + limo + arcilla = {Formato.Numero(suma,0)}` (cuando |suma − 100| > 2)
   - `lotes.json: lote {id}: superficie, IP, agua o distancia fuera de rango`. Aplica cuando superficie ≤ 0, IP fuera de (0, 100], cc ≤ pmp, pmp < 0, corg < 0 o distancia < 0.

- [ ] **Step 7: Correr `DatosTests`.** Esperado: 6/6 PASS.

---

### Task 5: Clima generado (WGEN, Hargreaves, El Niño / La Niña)

**Files:**
- Create: `AgroLife/Assets/Scripts/Sim/Clima/Hargreaves.cs`
- Create: `AgroLife/Assets/Scripts/Sim/Clima/GeneradorClima.cs` (incluye `DiaClima` y `EstadoClima`)
- Test: `AgroLife/Assets/Tests/Sim/ClimaTests.cs`

**Interfaces:**
- Consumes: `Pcg32`, `Fecha`, `ClimaParams`, `FaseEnso`.
- Produces:
```csharp
public struct DiaClima { public double LluviaMm, TmaxC, TminC, RadMJ, Et0Mm; public double Tmedia() => (TmaxC + TminC) / 2; }
public sealed class EstadoClima {
    public bool AyerLlovio; public double ZTmax, ZTmin, ZRad; public int DiasDesdeLluvia = 99;
    public FaseEnso Fase = FaseEnso.Neutro; public Dictionary<FaseEnso, double> Pronostico = new(); public DiaClima Hoy;
}
public static class Hargreaves { public static double Et0(double tmax, double tmin, int diaDelAnio, double latitudGrados); } // FAO-56, Ra con J = diaDelAnio + 1
public static class GeneradorClima {
    public static DiaClima GenerarDia(EstadoClima e, ClimaParams p, double latitud, Fecha fecha, Pcg32 rng); // actualiza e (incluye e.Hoy)
    public static void SortearFase(EstadoClima e, ClimaParams p, Pcg32 rng);
}
```

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
[Test] public void Hargreaves15DeEnero() => Assert.AreEqual(6.0550, Hargreaves.Et0(30, 17, 14, -33.89), 0.001);
[Test] public void Hargreaves15DeJulio() => Assert.AreEqual(1.4269, Hargreaves.Et0(14, 3, 195, -33.89), 0.001);
[Test] public void LluviaYTemperaturaCoincidenConLosParametros()
    // 1000 años desde new Fecha(120), EstadoClima nuevo (fase Neutro), rng (99, Clima). Por mes, con π = pSH/(1 − pHH + pSH):
    // lluvia media mensual = π·DiasDelMes·forma·escala ±7 %; Tmax media = π·TmaxHumedo + (1−π)·TmaxSeco ±0.3 °C (ídem Tmin).
[Test] public void TminSiempreQuedaUnGradoDebajoDeTmax()      // 100 años: TminC <= TmaxC − 1 todos los días
[Test] public void MismaSemillaMismoClima()                   // dos generadores (5, Clima), 730 días: DiaClima idénticos campo a campo
[Test] public void NinoLlueveMasQueNinaEnVerano()             // 1000 años con e.Fase fija en Nino y en Nina: lluvia dic–feb Niño > Niña
[Test] public void FaseYPronosticoSiguenLasFrecuencias() {    // 5000 sorteos con un rng (3, Clima)
    // frecuencia de cada fase = la de clima.json ±0.02
    // la fase con la mayor probabilidad en el pronóstico coincide con la real en 0.70 ±0.02 de los casos
    // valores del pronóstico, ordenados: {0.70, 0.15, 0.15}, y suman 1
}
```

- [ ] **Step 2: Correr `ClimaTests`.** Esperado: falla la compilación.

- [ ] **Step 3: Implementar.**

`GenerarDia` tiene que respetar este orden de sorteos (el determinismo depende de él):
```
m = p.Meses[fecha.Mes-1]; k = p.Enso.Multiplicadores[e.Fase][fecha.Mes-1]
pLluvia = min(0.95, (e.AyerLlovio ? m.PHumedoAHumedo : m.PSecoAHumedo) * k.Frecuencia)
llueve  = rng.NextDouble() < pLluvia
lluvia  = llueve ? rng.NextGamma(m.GammaForma, m.GammaEscala * k.Cantidad) : 0
Z de Tmax, luego Tmin, luego Rad:  z = ρ·z + sqrt(1 − ρ²)·rng.NextGaussian()
tmax = (llueve ? TmaxHumedo : TmaxSeco).Media + Desvio·zTmax   (ídem tmin; rad = max(1, …))
if (tmin > tmax − 1) tmin = tmax − 1
et0 = Hargreaves.Et0(tmax, tmin, fecha.DiaDelAnio, latitud)
e.AyerLlovio = llueve; e.DiasDesdeLluvia = llueve ? 0 : e.DiasDesdeLluvia + 1; e.Hoy = día
```
`SortearFase`:
1. Sortear la fase real con un `NextDouble()` contra las frecuencias acumuladas, en el orden Nino, Neutro, Nina.
2. Con otro `NextDouble()`: si es menor que 0.7, la fase favorecida es la real. Si no, un tercer `NextDouble()` elige con igual probabilidad entre las otras dos.
3. El pronóstico queda en 0.70 para la favorecida y 0.15 para cada una de las otras.

- [ ] **Step 4: Correr `ClimaTests`.** Esperado: 7/7 PASS.

---

### Task 6: Balance de agua, nitrógeno y fósforo

**Files:**
- Create: `AgroLife/Assets/Scripts/Sim/Suelo/BalanceAgua.cs`
- Create: `AgroLife/Assets/Scripts/Sim/Suelo/Nutrientes.cs`
- Test: `AgroLife/Assets/Tests/Sim/SueloTests.cs`

**Interfaces:**
- Consumes: `LoteDatos` (con sus derivados), `ModeloSuelo`, `FosforoParams`.
- Produces:
```csharp
public struct FlujosAgua { public double Infiltracion, Escurrimiento, EtPotencial, EtReal, AguaAntesDeDrenarMm, Drenaje; public bool Anegado; }
public static class BalanceAgua {
    // aguaMm = agua útil sobre PMP; puede superar AuMax hasta AuMax + Drenable
    public static FlujosAgua PasoDiario(ref double aguaMm, LoteDatos suelo, ModeloSuelo m, double lluviaMm, double et0Mm, double kc);
}
public static class Nitrogeno {
    public static double Mineralizacion(double corgPct, double tmediaC, double aguaMm, double auMaxMm, ModeloSuelo m);
    public static double Lavado(double nMineralKgHa, double drenajeMm, double aguaAntesDeDrenarMm, double pmpMm);
}
public static class Fosforo {
    public static double Perdida(double pBrayPpm, double pKgHa, FosforoParams f, ModeloSuelo m);  // LP
    public static double Actualizar(double pBrayPpm, double pAplicadoKgHa, double rindeQqHa, FosforoParams f, ModeloSuelo m);
}
```

- [ ] **Step 1: Escribir los tests que fallan.** Usar los lotes de prueba: lote 5 arenoso franco, lote 4 franco arcilloso limoso.

```csharp
[Test] public void BalanceDeAguaCierra()
    // por cada lote: 730 días con rng (11, Clima): lluvia = Bernoulli(0.25) ? Gamma(0.8, 18) : 0, et0 = Uniforme(1, 7), kc = Uniforme(0.25, 1.2)
    // |Σlluvia − (aguaFinal − aguaInicial + ΣEtReal + ΣDrenaje + ΣEscurrimiento)| < 1e-6
[Test] public void ArenosoDrenaYArcillosoEscurre() {        // agua = AuMax, lluvia 100, et0 0
    var d = DatosPrueba.Cargar(); var arenoso = d.Lote(5); var arcilloso = d.Lote(4);
    double a = arenoso.AuMaxMm, c = arcilloso.AuMaxMm;
    var fa = BalanceAgua.PasoDiario(ref a, arenoso, d.Suelo, 100, 0, 1); var fc = BalanceAgua.PasoDiario(ref c, arcilloso, d.Suelo, 100, 0, 1);
    Assert.AreEqual(100, fa.Drenaje, 1e-6); Assert.AreEqual(44.7562, fc.Escurrimiento, 0.001); Assert.AreEqual(36.8292, fc.Drenaje, 0.001);
    Assert.IsTrue(fc.Anegado); Assert.IsFalse(fa.Anegado);
}
[Test] public void ArenosoLavaMasNitrogeno() {               // mismos flujos del test anterior, 50 kg N
    // Lavado(50, fa.Drenaje, fa.AguaAntesDeDrenarMm, 90) == 14.7059 ±0.001 ; Lavado(50, fc.Drenaje, fc.AguaAntesDeDrenarMm, 330) == 2.8103 ±0.001
}
[Test] public void SueloSecoReduceElConsumo()                // agua = 0.25·AuMax, et0 5, kc 1, lluvia 0 → EtReal == 2.5 ±1e-9
[Test] public void MineralizacionSegunTemperaturaYHumedad()
    // corg 2, agua = AuMax: T 20 → 0.36; T 10 → 0.18; T 0 → 0; agua = AuMax/2 y T 20 → 0.18 (todos ±1e-9)
[Test] public void FosforoRespuestaYBalance() {
    var f = new FosforoParams { PCriticoPpm = 18, MinRelativo = 0.7, ExportacionKgPorQq = 0.55 }; var m = DatosPrueba.Cargar().Suelo;
    Assert.AreEqual(0.1, Fosforo.Perdida(9, 10, f, m), 1e-9);        // pDisp 12 → rel 0.9
    Assert.AreEqual(0, Fosforo.Perdida(20, 0, f, m), 1e-9);
    Assert.AreEqual(13.4, Fosforo.Actualizar(14, 20, 40, f, m), 1e-9); // 14 + (20 − 22)·0.3
    Assert.AreEqual(3, Fosforo.Actualizar(3.2, 0, 50, f, m), 1e-9);    // piso PBrayMinimo
}
```

- [ ] **Step 2: Correr `SueloTests`.** Esperado: falla la compilación.

- [ ] **Step 3: Implementar.**

`PasoDiario` sigue el orden fijo del spec (`au = AuMaxMm`, `p = FraccionAgotamiento`):
```
inf = clamp(min(lluvia, Ksat·HorasInfiltracion, au + Drenable − agua), 0, lluvia); esc = lluvia − inf; agua += inf
etp = et0·kc; ks = agua >= (1−p)·au ? 1 : agua / ((1−p)·au); etr = min(agua, etp·ks); agua −= etr
antes = agua; dren = max(0, min(agua − au, Ksat·HorasDrenaje)); agua −= dren
anegado = agua > au && Ksat < KsatAnegamientoMmH
```
Las fórmulas de nutrientes:
- **Mineralización** = `KMineralizacion · corg · fT · fW`, donde:
  - `fT = T <= 0 ? 0 : 2^((T − 20)/10)`,
  - `fW = clamp(agua/au, 0, 1)`.
- **Lavado** = `N · dren / (antes + pmp)`.
- **Pérdida por fósforo:** `1 − min(1, MinRelativo + (1 − MinRelativo)·(pBray + pKgHa·PpmPorKgP)/PCritico)`.
- **Actualizar fósforo:** `max(PBrayMinimo, pBray + (aplicado − rinde·Exportacion)·PpmPorKgP)`.

- [ ] **Step 4: Correr `SueloTests`.** Esperado: 6/6 PASS.

---

### Task 7: Modelo de cultivo (fenología, estrés, rinde y cascada)

**Files:**
- Create: `AgroLife/Assets/Scripts/Sim/Cultivo/EstadoCultivo.cs` (`EstadoCultivo`, `PlanCultivo`, `Perdidas`, `Cascada`)
- Create: `AgroLife/Assets/Scripts/Sim/Cultivo/ModeloCultivo.cs`
- Test: `AgroLife/Assets/Tests/Sim/CultivoTests.cs`

**Interfaces:**
- Consumes: `CultivoParams`, `DiaClima`, `FlujosAgua`, `Fosforo.Perdida`, `Fecha`, `LoteDatos`, `ModeloSuelo`.
- Produces:
```csharp
public sealed class PlanCultivo { public string CultivoId; public Fecha FechaSiembra; public Genetica Genetica; public double NKgHa, PKgHa;
    public bool VenderAlCosechar = true; public PlanCultivo Soja2; }   // Soja2 solo con trigo; su FechaSiembra se fija al cosechar el trigo
public sealed class EstadoCultivo {
    public string CultivoId; public Genetica Genetica; public Fecha FechaSiembra; public int Campania;
    public double GradosDia, FactorFotoperiodo = 1; public Etapa Etapa; public int DiasEnMadurez;
    public double RindeAlcanzableQqHa, LFecha, LP;                       // fijos desde la siembra
    public double SumaEstresAgua, SumaAnegamiento, LTemp, LPlagas;
    public double NDemandaTotalKg, NDemandaHastaHoyKg, NAbsorbidoKg, NAplicadoKgHa, PAplicadoKgHa;
    public double CostosDirectosUsd; public bool VenderAlCosechar = true;
    public List<string> PlagasOcurridas = new(); public PlanCultivo SegundoCultivo;
}
public struct Perdidas { public double Fecha, Agua, Anegamiento, Temperatura, Nitrogeno, Fosforo, Plagas; }  // fracciones 0..1
public struct Cascada { public double PotencialQqHa, FechaQq, AguaQq, AnegamientoQq, TemperaturaQq, NitrogenoQq, FosforoQq, PlagasQq, RealQqHa; }
public static class ModeloCultivo {
    public static EstadoCultivo Sembrar(CultivoParams c, Genetica g, Fecha fecha, double nKgHa, double pKgHa, LoteDatos lote, double pBrayPpm, ModeloSuelo m);
    public static UmbralesGd Umbrales(EstadoCultivo e, CultivoParams c);   // todos × FactorFotoperiodo, salvo Emergencia
    public static double Kc(EstadoCultivo e, CultivoParams c);
    public static void AvanzarDia(EstadoCultivo e, CultivoParams c, DiaClima clima, FlujosAgua agua, ref double nMineralKgHa);
    public static bool ListoParaCosechar(EstadoCultivo e, CultivoParams c);  // Etapa == Madurez && DiasEnMadurez >= DiasACosecha
    public static double PerdidaPorFecha(CultivoParams c, int diaDeCampania); // interpolación lineal por día de campaña; plana fuera de los extremos
    public static Perdidas PerdidasActuales(EstadoCultivo e, CultivoParams c);
    public static Cascada CalcularCascada(EstadoCultivo e, CultivoParams c);
}
```

- [ ] **Step 1: Escribir los tests que fallan.** Usan los cultivos de `DatosPrueba.Cargar()` y se pueden modificar.

```csharp
[Test] public void EtapasLleganElDiaEsperadoConTemperaturaFija() {
    var d = DatosPrueba.Cargar(); var c = d.Cultivos["maiz"]; c.TemperaturaBase = 10; c.DiasACosecha = 7;
    c.Geneticas[Genetica.Largo].Gd = new UmbralesGd { Emergencia = 50, Vegetativo = 100, Floracion = 300, Llenado = 400, Madurez = 600 };
    var e = ModeloCultivo.Sembrar(c, Genetica.Largo, Fecha.Crear(1, 10, 1), 0, 0, d.Lote(1), 20, d.Suelo);
    // clima Tmax 30 / Tmin 10 (10 GD por día), FlujosAgua por defecto; contar llamadas (1-based) hasta la primera vez que aparece cada etapa:
    // Emergencia 5, Vegetativo 10, Floracion 30, Llenado 40, Madurez 60; ListoParaCosechar pasa a true en la llamada 66
}
[Test] public void SojaTardiaAcortaElCiclo() {     // soja2 Largo: 10/12 → 25 días de atraso respecto del 15/11
    var e = ModeloCultivo.Sembrar(soja2, Genetica.Largo, Fecha.Crear(10, 12, 1), 0, 10, lote1, 14, suelo);
    Assert.AreEqual(0.925, e.FactorFotoperiodo, 1e-9); Assert.AreEqual(462.5, ModeloCultivo.Umbrales(e, soja2).Floracion, 1e-9);
    Assert.AreEqual(80, ModeloCultivo.Umbrales(e, soja2).Emergencia, 1e-9);
    Assert.AreEqual(1, ModeloCultivo.Sembrar(soja1, Genetica.Largo, Fecha.Crear(1, 11, 1), 0, 10, lote1, 14, suelo).FactorFotoperiodo, 1e-9);
}
[Test] public void PerdidaPorFechaInterpola() {     // maíz
    Assert.AreEqual(0, ModeloCultivo.PerdidaPorFecha(maiz, Fecha.DiaDeCampaniaDe(1, 10)), 1e-9);
    Assert.AreEqual(0.04, ModeloCultivo.PerdidaPorFecha(maiz, Fecha.DiaDeCampaniaDe(10, 9)), 1e-9);
    Assert.AreEqual(0.05, ModeloCultivo.PerdidaPorFecha(maiz, 185), 1e-9);   // mitad entre 20/10 (172) y 15/11 (198)
}
[Test] public void EstresHidricoPesaSegunLaEtapa()
    // maíz Largo, e.GradosDia = 800 (Floracion), clima Tmax = Tmin = 5 (0 GD), agua EtPotencial 4, EtReal 2 → SumaEstresAgua == 0.0125
    // con e.GradosDia = 300 (Vegetativo) → 0.0015
[Test] public void HeladaEnFloracionDeTrigo()      // trigo Largo, GradosDia 1550, Tmax 10, Tmin −3 → LTemp == 0.08
[Test] public void NitrogenoSeAbsorbeHastaLaDemanda()
    // maíz Largo, GradosDia 120, NDemandaTotalKg 178 (0.1 kg/GD sobre 70..1850), Tmax 28 / Tmin 8 (10 GD):
    // nMineral 0.4 → NAbsorbido 0.4, nMineral 0, NDemandaHastaHoy 1.0; un segundo día con nMineral 50 → NAbsorbido 1.4, nMineral 49
[Test] public void SojaNoTienePerdidaPorNitrogeno()  // soja1 con NAbsorbido 0 y NDemandaHastaHoy 0 → PerdidasActuales.Nitrogeno == 0
[Test] public void CascadaRepartePerdidasEnOrden() {
    var e = new EstadoCultivo { CultivoId = "maiz", RindeAlcanzableQqHa = 100, LFecha = 0.1, SumaEstresAgua = 0.1, SumaAnegamiento = 0.1,
        LTemp = 0.1, NDemandaHastaHoyKg = 120, NAbsorbidoKg = 100, LP = 0.1, LPlagas = 0.1 };   // N: rel 0.4 + 0.6·(100/120) = 0.9
    var k = ModeloCultivo.CalcularCascada(e, maiz);
    Assert.AreEqual(10, k.FechaQq, 1e-9); Assert.AreEqual(9, k.AguaQq, 1e-9); Assert.AreEqual(8.1, k.AnegamientoQq, 1e-9);
    Assert.AreEqual(7.29, k.TemperaturaQq, 1e-9); Assert.AreEqual(6.561, k.NitrogenoQq, 1e-9); Assert.AreEqual(5.9049, k.FosforoQq, 1e-9);
    Assert.AreEqual(5.31441, k.PlagasQq, 1e-9); Assert.AreEqual(47.82969, k.RealQqHa, 1e-9);
}
```

- [ ] **Step 2: Correr `CultivoTests`.** Esperado: falla la compilación.

- [ ] **Step 3: Implementar.**

`Sembrar`:
- `RindeAlcanzable = Rpot(g) · (A + B·IP/100)`.
- `LFecha = PerdidaPorFecha(c, fecha.DiaDeCampania)`.
- `LP = Fosforo.Perdida(pBray, pKgHa, c.Fosforo, m)`.
- `FactorFotoperiodo = Fotoperiodo == null ? 1 : max(FactorMinimo, 1 − AcortamientoPorDia·max(0, díaCampaña − díaCampaña(FechaReferencia)))`.
- `NDemandaTotal = Nitrogeno == null ? 0 : Alcanzable·(1 − LFecha)·KgPorQq`.
- `Campania = fecha.Campania`.
- No toca el lote: el N del fertilizante lo suma quien llama.

`AvanzarDia`, en este orden:
1. Sumar los grados-día: `gdHoy = max(0, Tmedia − Tbase)`.
2. Fijar la etapa: la última cuyo umbral (de `Umbrales`) es ≤ GD. Si queda en Madurez, `DiasEnMadurez++`.
3. Acumular el estrés hídrico si `EtPotencial > 0`: `SumaEstresAgua += (1 − EtReal/EtPotencial) · SensibilidadAgua[etapa]`.
4. Acumular el anegamiento si `Anegado`: `SumaAnegamiento += SensibilidadAnegamiento[etapa]`.
5. Acumular la temperatura:
   - si `Tmin < Helada.Umbral`, sumar `Helada.Perdida[etapa]` a `LTemp`,
   - si `Tmax > Calor.Umbral`, sumar `Calor.Perdida[etapa]`.
6. Repartir la demanda de N proporcional a los GD que caen entre Emergencia y Madurez:
   - `demandaHoy = NDemandaTotal · (min(GD, mad) − max(GDanterior, emer))⁺ / (mad − emer)`,
   - `NDemandaHastaHoy += demandaHoy`,
   - `abs = min(nMineral, demandaHoy)`; restarlo de `nMineral` y sumarlo a `NAbsorbido`.

`PerdidasActuales`:
- Agua, Anegamiento, Temperatura y Plagas = la suma acumulada, con tope en 1.
- Nitrógeno = `Nitrogeno == null || NDemandaHastaHoy == 0 ? 0 : 1 − min(1, MinRel + (1 − MinRel)·NAbsorbido/NDemandaHastaHoy)`.
- Fecha y Fósforo salen de `LFecha` y `LP`.

`CalcularCascada`: arranca de `Potencial = RindeAlcanzable` y aplica cada pérdida sobre lo que queda, en el orden fecha → agua → anegamiento → temperatura → N → P → plagas.

- [ ] **Step 4: Correr `CultivoTests`.** Esperado: 8/8 PASS.

---

### Task 8: Plagas y enfermedades

**Files:**
- Create: `AgroLife/Assets/Scripts/Sim/Cultivo/Plagas.cs`
- Test: `AgroLife/Assets/Tests/Sim/PlagasTests.cs`

**Interfaces:**
- Consumes: `EstadoCultivo`, `PlagaParams`, `DiaClima`, `Pcg32`.
- Produces:
```csharp
public static class Plagas {
    // null si hoy no aparece. Si aparece: agrega p.Id a e.PlagasOcurridas (una vez por cultivo) y devuelve la severidad.
    public static double? Sortear(EstadoCultivo e, PlagaParams p, DiaClima clima, int diasDesdeLluvia, Pcg32 rng);
}
```

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
[Test] public void SoloApareceEnSusCultivosYEtapasUnaVez()
    // chinches con ProbDiaria 1, clima Tmax 30 / Tmin 20: soja1 en Vegetativo → null; soja1 en Llenado → severidad en [0.04, 0.30];
    // la segunda llamada sobre el mismo cultivo → null; maíz en Llenado → null
[Test] public void FrioImpideLaAparicion()               // chinches ProbDiaria 1, Tmax 12 / Tmin 8 (Tmedia 10 < 15) → null
[Test] public void LluviaMultiplicaLaProbabilidad()
    // roya con ProbDiaria 0.1 (mult 3), trigo en Floracion, Tmedia 15; 20.000 cultivos nuevos con diasDesdeLluvia 0 y 20.000 con 10:
    // apariciones con lluvia / sin lluvia en [2.7, 3.3]
```

- [ ] **Step 2: Correr `PlagasTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar.**
  - Corresponde sortear si el cultivo está en `p.Cultivos`, la etapa está en `p.Etapas` y la plaga no ocurrió todavía en ese cultivo.
  - `prob = ProbDiaria · (diasDesdeLluvia <= 3 ? MultiplicadorLluvia : 1) · (Tmedia >= TempMinima ? 1 : 0)`.
  - Siempre se consume un `Bernoulli(prob)`. Si aparece, la severidad sale de `Uniforme(Severidad.Min, Severidad.Max)`.
- [ ] **Step 4: Correr `PlagasTests`.** Esperado: 3/3 PASS.

---

### Task 9: Precios y cálculos económicos

**Files:**
- Create: `AgroLife/Assets/Scripts/Sim/Economia/Precios.cs` (`EstadoPrecios`, `Precios`)
- Create: `AgroLife/Assets/Scripts/Sim/Economia/Economia.cs` (`Economia`, `DesgloseMargen`)
- Test: `AgroLife/Assets/Tests/Sim/EconomiaTests.cs`

**Interfaces:**
- Consumes: `EconomiaParams`, `RegionParams`, `LoteDatos`, `Pcg32`, `Fecha`.
- Produces:
```csharp
public sealed class EstadoPrecios { public Dictionary<Grano, double> X = new(), PrecioUsdT = new(); }
public static class Precios {
    public static void Inicializar(EstadoPrecios e, EconomiaParams p, Fecha fecha);          // X = 0
    public static void AvanzarDia(EstadoPrecios e, EconomiaParams p, Fecha fecha, Pcg32 rng); // granos en orden Trigo, Maiz, Soja
}
public struct DesgloseMargen { public double IngresoBrutoUsd, ComercializacionUsd, CosechaUsd, CostosDirectosUsd, MargenBrutoUsd; }
public static class Economia {
    public static double PrecioNetoUsdT(double precioUsdT, EconomiaParams p, double distanciaKm);   // precio·(1 − comisión) − (FijoUsdT + UsdTKm·km)
    public static double CostosDirectosSiembraUsdHa(string cultivoId, double nKgHa, double pKgHa, EconomiaParams p);
        // semilla + agroquímicos + siembra + pulverizaciones + N·NitrogenoUsdKg + P·FosforoUsdKg + (N + P > 0 ? AplicacionUsdHa : 0)
    public static double RindeIndiferenciaQqHa(double costosDirectosUsdHa, double precioNetoUsdT, double cosechaPct); // costos / (neto/10 · (1 − cosecha))
    public static DesgloseMargen Margen(double toneladas, double precioUsdT, double distanciaKm, double costosDirectosUsd, EconomiaParams p);
        // IB = t·precio; Com = t·(precio·comisión + flete); Cosecha = cosechaPct·t·PrecioNeto; MB = IB − Com − CD − Cosecha
    public static double ArrendamientoUsd(LoteDatos l, RegionParams r, double precioSojaUsdT); // base·IP/100·ha·precio/10
    public static double PrecioCompraUsd(LoteDatos l, RegionParams r);                         // base·IP/100·ha
    public static double InteresDiario(double saldoUsd, double tasaAnual);                     // saldo < 0 ? saldo·tasa/365 : 0
}
```
La cosecha se cobra sobre el valor neto (precio menos comisión y flete). Así, el margen bruto en el rinde de indiferencia da exactamente 0, y la fórmula queda igual a la del spec.

- [ ] **Step 1: Escribir los tests que fallan** (con `p = DatosPrueba.Cargar().Economia`)

```csharp
[Test] public void PrecioNetoDescuentaComisionYFlete() => Assert.AreEqual(284, Economia.PrecioNetoUsdT(300, p, 20), 1e-9);
[Test] public void MargenEsCeroEnElRindeDeIndiferencia() {
    double ri = Economia.RindeIndiferenciaQqHa(300, 284, 0.08);
    Assert.AreEqual(11.4818, ri, 0.0001);
    Assert.AreEqual(0, Economia.Margen(ri / 10, 300, 20, 300, p).MargenBrutoUsd, 1e-6);
}
[Test] public void CostosDeSiembra() { Assert.AreEqual(582, Economia.CostosDirectosSiembraUsdHa("maiz", 120, 20, p), 1e-9);
    Assert.AreEqual(236, Economia.CostosDirectosSiembraUsdHa("soja1", 0, 0, p), 1e-9); }
[Test] public void ArrendamientoYCompraSegunIp() { var l = new LoteDatos { SuperficieHa = 100, Ip = 80 }; var r = new RegionParams { ArrendamientoBaseQqHa = 17, PrecioBaseTierraUsdHa = 14000 };
    Assert.AreEqual(40800, Economia.ArrendamientoUsd(l, r, 300), 1e-6); Assert.AreEqual(1120000, Economia.PrecioCompraUsd(l, r), 1e-6); }
[Test] public void InteresDelDescubierto() { Assert.AreEqual(-4.10959, Economia.InteresDiario(-10000, 0.15), 1e-5); Assert.AreEqual(0, Economia.InteresDiario(5000, 0.15)); }
[Test] public void PreciosVuelvenALaMedia()
    // 100 años de AvanzarDia con rng (8, Precios): |media de X[Soja]| < 0.03; desvío de X = σ/sqrt(1 − φ²) ±15 %, con φ = exp(−ln2/VidaMediaDias)
[Test] public void PrecioSigueLaEstacionalidad()
    // mismo recorrido: (precio medio de soja en mayo / en septiembre) == 0.95/1.01 ±4 %
```

- [ ] **Step 2: Correr `EconomiaTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar.**
  - Proceso del precio: `φ = exp(−ln 2 / VidaMediaDias)`; `X = φ·X + VolatilidadDiaria·NextGaussian()`.
  - `Precio = ReferenciaUsdT · Estacional[mes−1] · exp(X)`.
- [ ] **Step 4: Correr `EconomiaTests`.** Esperado: 7/7 PASS.

---

### Task 10: `Simulation`: estado, nueva partida y día simulado (terceros)

**Files:**
- Create: `AgroLife/Assets/Scripts/Sim/Estado.cs`
- Create: `AgroLife/Assets/Scripts/Sim/Eventos.cs` (`Evento`, `TipoEvento`, `Resultado`)
- Create: `AgroLife/Assets/Scripts/Sim/Simulation.cs` (API pública y consultas)
- Create: `AgroLife/Assets/Scripts/Sim/Simulation.Dia.cs` (`partial`: `SimularDia` y siembra/cosecha)
- Test: `AgroLife/Assets/Tests/Sim/SimulacionTests.cs`; ampliar `DatosPrueba` con `Nueva` y `AvanzarHasta`

**Interfaces:**
- Consumes: todo lo anterior.
- Produces:
```csharp
public enum Tenencia { Tercero, Arrendado, Propio }
public sealed class EstadoJuego {
    public string RegionId; public ulong Semilla; public Fecha Fecha;   // Fecha = hoy, ya simulado
    public Dictionary<Flujo, Pcg32> Rng; public EstadoClima Clima; public EstadoPrecios Precios;
    public List<EstadoLote> Lotes;                                       // índice = id − 1
    public double SaldoUsd; public Dictionary<Grano, double> StockT;     // stock en silobolsa
    public List<Contrato> Contratos; public List<ResultadoCultivo> Resultados; public List<PromedioZona> PromediosZona;
    public bool AlertaSaldoEmitida, Terminada;
}
public sealed class EstadoLote { public int Id; public Tenencia Tenencia; public Contrato Contrato; public double AguaMm, NMineralKgHa, PBrayPpm;
    public string CultivoAnterior; public PlanCultivo Plan; public EstadoCultivo Cultivo; public DecisionPlaga Decision; }
public sealed class Contrato { public int LoteId, Campania; public double MontoUsd; public Fecha Vence; }
public sealed class DecisionPlaga { public string PlagaId; public double Severidad, Nivel, Umbral, CostoUsd, PerdidaEsperadaQq, PerdidaEsperadaUsd; }
public sealed class PromedioZona { public int Campania; public string CultivoId; public double SumaQqPorHa, SumaHa; }
public sealed class ResultadoCultivo { public int LoteId, Campania; public string CultivoId; public double SuperficieHa; public Fecha FechaCosecha;
    public Cascada Cascada; public double ToneladasCosechadas, PrecioCosechaUsdT, RindeIndiferenciaQqHa; public DesgloseMargen Margen; }
public enum TipoEvento { InicioCampania, Plaga, SaldoCercaDelLimite, Quiebra, Siembra, Cosecha, SiembraCancelada, Helada, VencimientoArrendamiento, PerdidaSilobolsa }
public sealed class Evento { public TipoEvento Tipo; public Fecha Fecha; public int LoteId; /* 0 = ninguno */ public string Mensaje; public bool Pausa; }
public readonly struct Resultado { public readonly bool Ok; public readonly string Motivo; public static readonly Resultado Exito; public static Resultado Rechazo(string motivo); }
public sealed partial class Simulation {
    public DatosJuego Datos { get; } public EstadoJuego Estado { get; } public IReadOnlyList<Evento> EventosIniciales { get; }
    public static Simulation Nueva(DatosJuego datos, ulong semilla);  // estado al 1/5 Año 1, ya simulado; eventos de ese día en EventosIniciales
    public List<Evento> StepDay();                                    // Fecha += 1 y simula ese día; si Terminada devuelve una lista vacía y no avanza
    public double RindeEstimadoQqHa(int loteId);                      // CalcularCascada(...).RealQqHa del cultivo en pie, o 0
    public double PromedioZonaQqHa(int campania, string cultivoId);   // SumaQqPorHa / SumaHa, o 0
    public double FondosDisponiblesUsd();                             // Saldo + LimiteDescubiertoUsd
}
```
- Produces (tests):
  - `DatosPrueba.Nueva(ulong semilla = 1234, bool plagas = false)`: si `plagas` es false, pone `ProbDiaria = 0` en todas las plagas antes de `Nueva`.
  - `DatosPrueba.AvanzarHasta(Simulation s, Fecha f)`: llama a `StepDay` mientras `Fecha < f` y devuelve todos los eventos.

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
[Test] public void NuevaArrancaEl1DeMayoConElCapitalInicial() {
    var s = DatosPrueba.Nueva();
    Assert.AreEqual(Fecha.Crear(1, 5, 1), s.Estado.Fecha);
    Assert.AreEqual(173325, s.Estado.SaldoUsd, 0.01);   // 250 · (17·0.78·300/10 + 295.5)
    var e = s.EventosIniciales.Single(x => x.Tipo == TipoEvento.InicioCampania);
    Assert.IsTrue(e.Pausa); StringAssert.StartsWith("Inicio de la campaña 1. Pronóstico:", e.Mensaje);
}
[Test] public void LotesArrancanConAguaNitrogenoFosforoYRotacion() {
    var l = DatosPrueba.Nueva().Estado.Lotes;
    Assert.AreEqual(180, l[0].AguaMm, 1e-9); Assert.AreEqual(40, l[0].NMineralKgHa, 1e-9); Assert.GreaterOrEqual(l[0].PBrayPpm, 4);
    Assert.AreEqual(Tenencia.Tercero, l[0].Tenencia);
    Assert.AreEqual("soja1", l[0].CultivoAnterior); Assert.AreEqual("soja2", l[1].CultivoAnterior); Assert.AreEqual("maiz", l[2].CultivoAnterior);
}
[Test] public void TercerosPlanificanSegunLaRotacion() {
    var l = DatosPrueba.Nueva().Estado.Lotes;
    Assert.AreEqual("trigo", l[0].Plan.CultivoId); Assert.IsNotNull(l[0].Plan.Soja2); Assert.AreEqual(Fecha.Crear(15, 6, 1), l[0].Plan.FechaSiembra);
    Assert.AreEqual("maiz", l[1].Plan.CultivoId); Assert.AreEqual(Fecha.Crear(1, 10, 1), l[1].Plan.FechaSiembra);
    Assert.AreEqual("soja1", l[2].Plan.CultivoId);
}
[Test] public void StepDayAvanzaUnDiaConClimaYPrecios()
    // StepDay → Fecha == 2/5 Año 1; Clima.Hoy.Et0Mm > 0; los tres precios > 0
[Test] public void TercerosCosechanTodaLaRotacionConRindesCreibles()
    // AvanzarHasta(1/6 Año 2); PromedioZonaQqHa(1, c) en (5, rpot Largo) para trigo (62), maiz (135), soja1 (50) y soja2 (40)
[Test] public void RindeEstimadoDuranteElCultivo()
    // AvanzarHasta(1/12 Año 1): 0 < RindeEstimadoQqHa(2) <= 135·(0.3 + 0.7·0.8)
```

- [ ] **Step 2: Correr `SimulacionTests`.** Esperado: falla la compilación.

- [ ] **Step 3: Implementar `Nueva`**
  - Crear un `Pcg32(semilla, (ulong)flujo)` por cada `Flujo`.
  - Fijar `Fecha = 1/5 Año 1`, `Precios.Inicializar` y los `StockT` en 0.
  - Inicializar los lotes en orden de id:
    - `Agua = AuMax · AguaInicialFraccion`,
    - `NMineral = NInicialKgHa`,
    - `PBray = max(Minimo, Media + Desvio·Gaussiana(Inicial))`,
    - `CultivoAnterior` = el último cultivo de la rotación de la campaña 0.
  - Saldo = `Hectareas · (ArrendamientoBase · IPprom/100 · ReferenciaSoja/10 + CostosDirectosSiembraUsdHa(cultivoCapital, planTipico N, P))`, donde IPprom es el IP promedio ponderado por superficie.
  - Por último: `EventosIniciales = SimularDia()`.

  Rotación de terceros: `{"maiz", "soja1", "trigo"}` indexada por `(id + campaña) % 3`. El trigo va siempre con la soja de segunda en `Plan.Soja2`.

- [ ] **Step 4: Implementar `SimularDia` en este orden fijo** (los pasos marcados con una tarea posterior se agregan ahí)
```
1. Si Fecha es 1/5:
   (a) vencimientos de arrendamiento                                     [Tarea 11]
   (b) GeneradorClima.SortearFase (rng Clima)
   (c) los lotes Tercero sin Cultivo ni Plan reciben su plan:
       PlanTipico con FechaSiembra = Fecha.EnCampania(campaña, dd/mm)
   (d) evento InicioCampania con Pausa:
       "Inicio de la campaña {N}. Pronóstico: Niño {p}% · Neutro {p}% · Niña {p}%" (p sin decimales)
2. GeneradorClima.GenerarDia (rng Clima).
3. Precios.AvanzarDia (rng Precios).
4. Si Fecha.Dia == 1: riesgo de silobolsa (rng Silos)                    [Tarea 12]
5. Por cada lote, en orden de id:
   a. Si Plan != null y Plan.FechaSiembra == Fecha, Sembrar:
      - Cultivo = ModeloCultivo.Sembrar(...); NMineral += Plan.NKgHa;
      - Cultivo.SegundoCultivo = Plan.Soja2; Cultivo.VenderAlCosechar = Plan.VenderAlCosechar; Plan = null.
      - Lote del jugador: cobro de los costos y evento Siembra           [Tarea 11]
   b. kc = Cultivo != null ? ModeloCultivo.Kc : KcSueloDesnudo; BalanceAgua.PasoDiario.
   c. NMineral += Mineralizacion; NMineral −= Lavado (con piso en 0).
   d. Si hay Cultivo:
      - AvanzarDia.
      - Por cada plaga en el orden de pests.json, Plagas.Sortear (rng Plagas).
        Si aparece: LPlagas += sev > Umbral ? sev·(1 − Eficacia) : sev.
        (En la Tarea 12 la rama del jugador pasa a ser una decisión.)
      - Si ListoParaCosechar → Cosechar.
6. Cuenta: interés, quiebra y alerta                                     [Tarea 12]
7. Si Tmin < 0: evento Helada "Helada: {Formato.Numero(tmin, 1)} °C".
```

  `Cosechar(lote)`:
  - `k = CalcularCascada`.
  - Si el lote es Tercero, sumar `k.RealQqHa · ha` y `ha` a `PromedioZona(campaña del cultivo, cultivo)`.
  - `PBray = Fosforo.Actualizar(PBray, PAplicado, k.RealQqHa, ...)`.
  - `CultivoAnterior = cultivo`.
  - Si `SegundoCultivo != null`:
    - Si `Fecha.MasDias(1)` cae en la ventana de soja2 (día de campaña ≤ el de `hasta`): `Plan = SegundoCultivo` con `FechaSiembra = Fecha.MasDias(1)`.
    - Si no: evento `SiembraCancelada` (Tarea 11).
  - `Cultivo = null`.
  - Lote del jugador: economía y resultado (Tarea 11).

- [ ] **Step 5: Correr `SimulacionTests`.** Esperado: 6/6 PASS. Si `TercerosCosechanTodaLaRotacion` falla por rindes fuera de rango, mirar la cascada de un lote antes de tocar parámetros: los rangos son amplios a propósito.

---

### Task 11: Tierra, planificación, siembra y cosecha del jugador

**Files:**
- Create: `AgroLife/Assets/Scripts/Sim/Simulation.Acciones.cs` (`partial`)
- Create: `AgroLife/Assets/Scripts/Sim/Reportes.cs` (`Presupuesto`, `PresupuestoCultivo`, `ReporteLote`)
- Modify: `AgroLife/Assets/Scripts/Sim/Simulation.Dia.cs` (los pasos 1a y 5a, y `Cosechar`)
- Test: `AgroLife/Assets/Tests/Sim/AccionesTests.cs`

**Interfaces:**
- Consumes: Tarea 10 y `Economia`.
- Produces:
```csharp
public sealed class PresupuestoCultivo { public string CultivoId; public double CostoUsd, RindeEsperadoQqHa, MargenBrutoUsd, RindeIndiferenciaQqHa; }
public sealed class Presupuesto { public List<PresupuestoCultivo> Cultivos = new(); public double CostoTotalUsd, MargenBrutoUsd; }
public sealed class ReporteLote { public List<ResultadoCultivo> Cultivos; public double MargenBrutoUsd, ArrendamientoUsd, ResultadoUsd;
    public Dictionary<string, double> PromedioZonaQqHa; }
// en Simulation:
public double PrecioArrendamientoUsd(int loteId);   // Economia.ArrendamientoUsd al precio de soja de hoy
public double PrecioCompraUsd(int loteId);
public Resultado ArrendarLote(int loteId);
public Resultado ComprarLote(int loteId);
public Resultado PlanificarCultivo(int loteId, PlanCultivo plan);
public Resultado CancelarPlan(int loteId);           // borra Plan; si no hay Plan pero sí Cultivo.SegundoCultivo, borra ese
public Presupuesto CalcularPresupuesto(int loteId, PlanCultivo plan);
public ReporteLote ReporteLote(int loteId, int campania);
```

**Validaciones.** Se devuelve el primer motivo que aplica, en este orden y con este texto exacto:

| Acción | Orden de chequeos → motivo |
|---|---|
| todas | `Terminada` → "La partida terminó por quiebra"; id fuera de rango → "No existe el lote {id}" |
| Arrendar | ya Arrendado/Propio → "El lote {id} ya es tuyo"; Cultivo en pie → "El lote {id} está ocupado con {nombre en minúscula} hasta la cosecha"; costo > fondos → saldo insuficiente |
| Comprar | ya Propio → "El lote {id} ya es tuyo"; Tercero con Cultivo → ocupado (mismo texto); costo > fondos → saldo insuficiente |
| Planificar | Tercero → "El lote {id} no es tuyo"; Cultivo o Plan → "El lote {id} ya tiene un cultivo planificado o sembrado"; id desconocido → "Cultivo desconocido: {id}"; soja2 como principal → "La soja de segunda se planifica junto con el trigo"; Soja2 sin trigo → "La soja de segunda solo va después de trigo"; N > 0 en un cultivo sin `Nitrogeno` → "El cultivo {nombre en minúscula} no lleva nitrógeno"; N fuera de [0, 400] o P fuera de [0, 100] (también en Soja2) → "Dosis fuera de rango (N 0–400, P 0–100 kg/ha)"; FechaSiembra ≤ hoy o > hoy+365 → "La fecha de siembra tiene que ser posterior a hoy y dentro del próximo año"; fuera de ventana → "Fuera de la ventana de siembra de {nombre en minúscula} ({desde}–{hasta})"; Arrendado y FechaSiembra > Vence → "El arrendamiento del lote {id} vence el {Vence}"; CostoTotal del presupuesto > fondos → saldo insuficiente |
| Cancelar | Tercero → no es tuyo; sin nada que cancelar → "No hay un plan pendiente en el lote {id}" |

- Saldo insuficiente: `"Saldo insuficiente: hacen falta {Usd(costo)} y tenés {Usd(fondos)} disponibles"`.
- Ventana: se compara `DiaDeCampania` contra los de `desde` y `hasta`. Ninguna ventana cruza el 30/4.

**Efectos.**
- **Arrendar:**
  - Borra el `Plan` de terceros y pasa a `Tenencia = Arrendado`.
  - Crea `Contrato { Campania = hoy, MontoUsd, Vence }`, donde `Vence = EnCampania(hoy.DiaDeCampania == 364 ? campaña + 1 : campaña, 30, 4)`.
  - Lo agrega a `Estado.Contratos` y resta el monto del saldo.
- **Comprar:** borra el `Plan` si el lote era de terceros, pasa a `Tenencia = Propio` y `Contrato = null`, y resta el precio del saldo.
- **Planificar:** guarda el plan. No cobra nada hasta la siembra.

**Siembra del jugador (paso 5a):**
- `CostosDirectosUsd = CostosDirectosSiembraUsdHa(...) · ha`; se resta del saldo.
- Evento Siembra: "Se sembró {nombre en minúscula} en el lote {id}".

**Cosecha del jugador:**
- `t = Real·ha/10`, `precio` del grano hoy, `m = Economia.Margen(t, precio, km, CostosDirectosUsd)`.
- Movimientos de saldo:
  - siempre se restan `m.CosechaUsd` y el flete `t·(FijoUsdT + UsdTKm·km)`;
  - con `VenderAlCosechar` se suma `t·precio·(1 − comisión)`;
  - si no, se resta `t·EmbolsadoUsdT` y se suma `t` a `StockT[grano]`.
- Se agrega un `ResultadoCultivo` con `RindeIndiferencia = RindeIndiferenciaQqHa(CostosDirectosUsd/ha, PrecioNeto, cosecha)`.
- Evento Cosecha: "Cosecha de {nombre en minúscula} en el lote {id}: {Numero(real,1)} qq/ha".
- Si el lote es Arrendado, `Vence < hoy` y no queda `Plan`, vuelve a Tercero.
- Soja de segunda fuera de ventana: evento `SiembraCancelada` con "No se sembró soja de segunda en el lote {id}: el trigo se cosechó después del 15/1".

**Vencimientos (paso 1a):**
- Aplica a cada lote Arrendado con `Vence < Fecha`.
- Evento: "Venció el arrendamiento del lote {id}", más "; sigue hasta la cosecha" si hay `Cultivo` o `Plan`.
- Si no hay `Cultivo` ni `Plan`, el lote pasa a `Tenencia = Tercero` y `Contrato = null`.

**Presupuesto:** por cada cultivo del plan (trigo y, si corresponde, soja2 con la fecha de su `PlanTipico`):
- `CostoUsd = CostosDirectosSiembraUsdHa · ha`.
- `RindeEsperado = Alcanzable·(1 − LFecha)·(1 − LP)·(1 − LNesp)·FactorClimaEsperado`, donde LNesp sale de la misma curva de N con `ratio = (NMineral + N + MineralizacionEsperada)/(Alcanzable·(1 − LFecha)·KgPorQq)`.
- `MargenBruto = Margen(RindeEsperado·ha/10, precio de hoy, km, CostoUsd)`.
- `RindeIndiferencia` con el precio neto de hoy.

**Reporte:**
- Junta los `Resultados` del lote en esa campaña.
- `ArrendamientoUsd` = la suma de los `Contratos` del lote en esa campaña.
- `ResultadoUsd = MB − Arrendamiento`.
- `PromedioZonaQqHa[c] = PromedioZonaQqHa(campaña, c)` para cada cultivo del reporte.

- [ ] **Step 1: Escribir los tests que fallan.** Usan `DatosPrueba.Nueva()` sin plagas. Rotación en la campaña 1: lote 1 trigo/soja2, lote 2 maíz, lote 3 soja1.

```csharp
[Test] public void ArrendarCobraQuintalesDeSojaAlPrecioDelDia() {
    var s = DatosPrueba.Nueva(); double saldo = s.Estado.SaldoUsd, precio = s.Estado.Precios.PrecioUsdT[Grano.Soja];
    Assert.IsTrue(s.ArrendarLote(1).Ok);
    Assert.AreEqual(saldo - 17 * 0.9 * 100 * precio / 10, s.Estado.SaldoUsd, 1e-6);
    Assert.AreEqual(Tenencia.Arrendado, s.Estado.Lotes[0].Tenencia); Assert.AreEqual(Fecha.Crear(30, 4, 2), s.Estado.Lotes[0].Contrato.Vence);
    Assert.IsNull(s.Estado.Lotes[0].Plan);
}
[Test] public void ArrendarOComprarLoteOcupadoSeRechaza() {      // Review Focus 3
    var s = DatosPrueba.Nueva(); DatosPrueba.AvanzarHasta(s, Fecha.Crear(1, 7, 1)); s.Estado.SaldoUsd = 5_000_000;
    Assert.AreEqual("El lote 1 está ocupado con trigo hasta la cosecha", s.ArrendarLote(1).Motivo);
    Assert.AreEqual("El lote 1 está ocupado con trigo hasta la cosecha", s.ComprarLote(1).Motivo);
    Assert.AreEqual("trigo", s.Estado.Lotes[0].Cultivo.CultivoId);
}
[Test] public void ArrendarSinSaldoSeRechaza() { var s = DatosPrueba.Nueva(); s.Estado.SaldoUsd = -49000;
    StringAssert.StartsWith("Saldo insuficiente: hacen falta US$ ", s.ArrendarLote(1).Motivo); }
[Test] public void ComprarCobraPrecioSegunIp() { var s = DatosPrueba.Nueva(); s.Estado.SaldoUsd = 2_000_000;
    Assert.IsTrue(s.ComprarLote(1).Ok); Assert.AreEqual(740_000, s.Estado.SaldoUsd, 1e-6); Assert.AreEqual(Tenencia.Propio, s.Estado.Lotes[0].Tenencia); }
[Test] public void PlanificarFueraDeVentanaSeRechazaConMotivo() { var s = DatosPrueba.Nueva(); s.ArrendarLote(2);
    Assert.AreEqual("Fuera de la ventana de siembra de maíz (15/9–31/12)", s.PlanificarCultivo(2, Maiz(Fecha.Crear(1, 9, 1))).Motivo); }
[Test] public void PlanificarEnLoteAjenoSeRechaza() => Assert.AreEqual("El lote 3 no es tuyo", DatosPrueba.Nueva().PlanificarCultivo(3, Maiz(Fecha.Crear(1, 10, 1))).Motivo);
[Test] public void PlanificarSinSaldoSeRechaza() { var s = DatosPrueba.Nueva(); s.ArrendarLote(2); s.Estado.SaldoUsd = -20000;
    StringAssert.StartsWith("Saldo insuficiente", s.PlanificarCultivo(2, Maiz(Fecha.Crear(1, 10, 1))).Motivo); }
[Test] public void SiembraCobraYCosechaDejaResultadoConsistente() {
    var s = DatosPrueba.Nueva(); s.ArrendarLote(2); Assert.IsTrue(s.PlanificarCultivo(2, Maiz(Fecha.Crear(1, 10, 1))).Ok);
    DatosPrueba.AvanzarHasta(s, Fecha.Crear(30, 9, 1)); double antes = s.Estado.SaldoUsd; s.StepDay();
    Assert.AreEqual(antes - 46_560, s.Estado.SaldoUsd, 1e-6);                // 80 ha · 582
    DatosPrueba.AvanzarHasta(s, Fecha.Crear(1, 6, 2)); var r = s.Estado.Resultados.Single();
    var k = r.Cascada; Assert.AreEqual(k.PotencialQqHa, k.FechaQq + k.AguaQq + k.AnegamientoQq + k.TemperaturaQq + k.NitrogenoQq + k.FosforoQq + k.PlagasQq + k.RealQqHa, 1e-6);
    Assert.AreEqual(k.RealQqHa * 8, r.ToneladasCosechadas, 1e-6);
    Assert.AreEqual(r.Margen.IngresoBrutoUsd - r.Margen.ComercializacionUsd - r.Margen.CostosDirectosUsd - r.Margen.CosechaUsd, r.Margen.MargenBrutoUsd, 1e-6);
    var rep = s.ReporteLote(2, 1); Assert.AreEqual(rep.MargenBrutoUsd - rep.ArrendamientoUsd, rep.ResultadoUsd, 1e-6); Assert.Greater(rep.PromedioZonaQqHa["maiz"], 0);
}
[Test] public void DobleCultivoSiembraSoja2AlDiaSiguienteDeLaCosecha()
    // arrendar 1; trigo 15/6 Largo N90 P15 con Soja2 {soja2, Largo, P10}; avanzar (tope 400 días) hasta Cultivo?.CultivoId == "soja2";
    // FechaSiembra de la soja == FechaCosecha del ResultadoCultivo del trigo + 1
[Test] public void PresupuestoDeMaiz() {
    // arrendar 2; p = CalcularPresupuesto(2, Maiz(1/10)): CostoTotalUsd == 46_560; RindeIndiferenciaQqHa == RindeIndiferenciaQqHa(582, PrecioNetoUsdT(precio maíz, 25), 0.08);
    // 0 < RindeEsperadoQqHa < 135·(0.3 + 0.7·0.8)
}
[Test] public void CancelarPlanAntesDeSembrar()    // arrendar 2, planificar maíz 1/10, CancelarPlan(2).Ok; AvanzarHasta(2/10): Cultivo == null y sin cobro de siembra
[Test] public void ArrendamientoVencidoConCultivoSeExtiendeHastaLaCosecha() {   // Review Focus 1
    // arrendar 2; AvanzarHasta(28/4 Año 2); forzar lote.Cultivo = ModeloCultivo.Sembrar(maiz, Largo, hoy, 0, 0, lote2, PBray, suelo);
    // AvanzarHasta(2/5 Año 2): Tenencia sigue Arrendado; hay un evento VencimientoArrendamiento del lote 2 que contiene "sigue hasta la cosecha";
    // forzar Cultivo.GradosDia = 5000 y DiasEnMadurez = 100; StepDay: hay un ResultadoCultivo del lote 2 y Tenencia == Tercero
}
[Test] public void Soja2SeCancelaSiElTrigoSeCosechaDespuesDel15DeEnero() {      // Review Focus 2
    // arrendar 1; AvanzarHasta(14/1 Año 2); forzar Cultivo = Sembrar(trigo, ...) con SegundoCultivo = {soja2, Largo, P10}, GradosDia 5000, DiasEnMadurez 100;
    // StepDay (cosecha el 15/1): evento SiembraCancelada del lote 1, Plan == null; saldo tras ese día == saldo tras un StepDay más
}
static PlanCultivo Maiz(Fecha f) => new PlanCultivo { CultivoId = "maiz", FechaSiembra = f, Genetica = Genetica.Largo, NKgHa = 120, PKgHa = 20 };
```

- [ ] **Step 2: Correr `AccionesTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar las acciones, el presupuesto, el reporte y los cambios en `Simulation.Dia.cs`** descritos arriba.
- [ ] **Step 4: Correr `AccionesTests` y `SimulacionTests`.** Esperado: 13/13 y 6/6 PASS.

---

### Task 12: Mercado, plagas en lotes propios y cuenta

**Files:**
- Modify: `AgroLife/Assets/Scripts/Sim/Simulation.Acciones.cs`, `Simulation.Dia.cs`, `Simulation.cs`
- Test: `AgroLife/Assets/Tests/Sim/MercadoYCuentaTests.cs`

**Interfaces:**
- Produces:
```csharp
public Resultado VenderGrano(Grano grano, double toneladas);
public Resultado DecidirTratamiento(int loteId, bool aplicar);
// StepDay lanza InvalidOperationException("Hay decisiones de plaga pendientes") si algún lote tiene Decision != null
```

**Comportamiento.**
- **VenderGrano:**
  - Rechazos, en orden: `Terminada`; `t <= 0` → "La cantidad tiene que ser mayor que cero"; `t > stock + 1e-9` → "No hay suficiente {trigo|maíz|soja} en silobolsa".
  - Efecto: `stock −= t`; `saldo += t·precio·(1 − comisión)`.
- **Plaga en un lote del jugador con `sev > Umbral` (paso 5d):**
  - Se guarda `Decision`:
    - `Nivel = sev·EscalaNivel`, `Umbral = Umbral·EscalaNivel`,
    - `CostoUsd = (Producto + Pulverización)·ha`,
    - `PerdidaEsperadaQq = sev·RindeEstimado·ha`,
    - `PerdidaEsperadaUsd = qq·PrecioNeto/10`.
  - Evento `Plaga` con `Pausa = true`: "{Nombre} en el lote {id}: nivel {Numero(nivel,1)} {unidad} (umbral {Numero(umbral,1)} {unidad})".
  - Si `sev <= Umbral`: `LPlagas += sev`.
  - Los lotes de terceros siguen como en la Tarea 10.
- **DecidirTratamiento:**
  - Rechazos: `Terminada`; id inválido; sin `Decision` → "No hay una decisión pendiente en el lote {id}"; `aplicar` con costo > fondos → saldo insuficiente (la decisión sigue pendiente).
  - Si se aplica: se cobra y `LPlagas += sev·(1 − Eficacia)`. Si no: `LPlagas += sev`. En los dos casos, `Decision = null`.
- **Silobolsa (paso 4):** por cada grano con stock > 0, en orden Trigo, Maiz, Soja:
  - Si sale `Bernoulli(RiesgoMensual)`: `frac = Uniforme(PerdidaMin, PerdidaMax)` y `stock −= stock·frac`.
  - Evento: "Se perdió el {Numero(frac·100,0)} % del {grano} en silobolsa".
- **Cuenta (paso 6):**
  - `saldo += InteresDiario`.
  - Si `saldo < −Limite`: `Terminada = true`; evento Quiebra con Pausa: "Quiebra: el saldo bajó del límite de descubierto. La partida terminó."
  - Si no, si `saldo < −Limite·AlertaFraccion` y la alerta no estaba emitida: evento SaldoCercaDelLimite con Pausa ("Tu saldo ({Usd}) está cerca del límite de descubierto ({Usd(−Limite)})") y se marca la alerta como emitida.
  - Si el saldo vuelve a estar por encima de ese umbral, se resetea la alerta.

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
[Test] public void VenderCobraPrecioMenosComision() { var s = DatosPrueba.Nueva(); s.Estado.StockT[Grano.Soja] = 100;
    double saldo = s.Estado.SaldoUsd, precio = s.Estado.Precios.PrecioUsdT[Grano.Soja];
    Assert.IsTrue(s.VenderGrano(Grano.Soja, 40).Ok); Assert.AreEqual(saldo + 40 * precio * 0.98, s.Estado.SaldoUsd, 1e-6); Assert.AreEqual(60, s.Estado.StockT[Grano.Soja], 1e-9); }
[Test] public void VentasInvalidasSeRechazan() { var s = DatosPrueba.Nueva();
    Assert.AreEqual("La cantidad tiene que ser mayor que cero", s.VenderGrano(Grano.Soja, 0).Motivo);
    Assert.AreEqual("No hay suficiente maíz en silobolsa", s.VenderGrano(Grano.Maiz, 1).Motivo); }
[Test] public void EmbolsarCobraYSumaStock()
    // RiesgoMensual = 0; arrendar 2; maíz 1/10 con VenderAlCosechar = false; avanzar de a un día hasta que Resultados.Count == 1:
    // StockT[Maiz] == r.ToneladasCosechadas; saldoDespués − saldoAntes == −(r.Margen.CosechaUsd + t·(5 + 0.25·25) + t·4) ±1e-6
[Test] public void SilobolsaPierdeUnaParte()        // RiesgoMensual = 1; StockT[Soja] = 100; AvanzarHasta(1/6/1): stock en [75, 95], hay un evento PerdidaSilobolsa
[Test] public void PlagaSobreUmbralPausaYBloqueaElDia() {
    // plagas: chinches ProbDiaria 1, Severidad {0.2, 0.3}; isoca y roya en 0; arrendar 3; soja1 15/11 Largo P15;
    // avanzar (tope 300 días) hasta un evento Plaga del lote 3: Pausa == true; Decision != null; Assert.Throws<InvalidOperationException>(() => s.StepDay());
    // sev = Decision.Severidad; DecidirTratamiento(3, true).Ok; saldo bajó (12 + 9)·60 = 1260; LPlagas == sev·0.15; Decision == null
}
[Test] public void NoAplicarSumaLaSeveridadCompleta()  // mismo armado; DecidirTratamiento(3, false): LPlagas == sev; saldo sin cambios
[Test] public void InteresDiarioEnDescubierto() { var s = DatosPrueba.Nueva(); s.Estado.SaldoUsd = -10000; s.StepDay();
    Assert.AreEqual(-10000 * (1 + 0.15 / 365), s.Estado.SaldoUsd, 1e-6); }
[Test] public void AlertaDeSaldoUnaSolaVez()           // SaldoUsd = −41.000; el primer StepDay trae SaldoCercaDelLimite con Pausa; el segundo no
[Test] public void SiembraQueRompeElLimiteTerminaLaPartida() {   // Review Focus 4
    var s = DatosPrueba.Nueva(); s.ArrendarLote(2); s.PlanificarCultivo(2, new PlanCultivo { CultivoId = "maiz", FechaSiembra = Fecha.Crear(1, 10, 1), Genetica = Genetica.Largo, NKgHa = 120, PKgHa = 20 });
    DatosPrueba.AvanzarHasta(s, Fecha.Crear(30, 9, 1)); s.Estado.SaldoUsd = -10000;
    Assert.IsTrue(s.StepDay().Any(e => e.Tipo == TipoEvento.Quiebra && e.Pausa)); Assert.IsTrue(s.Estado.Terminada);
    Assert.IsEmpty(s.StepDay()); Assert.AreEqual(Fecha.Crear(1, 10, 1), s.Estado.Fecha);
    Assert.AreEqual("La partida terminó por quiebra", s.ArrendarLote(3).Motivo);
}
```

- [ ] **Step 2: Correr `MercadoYCuentaTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Correr `MercadoYCuentaTests`, `AccionesTests` y `SimulacionTests`.** Esperado: todo PASS.

---

### Task 13: Guardado, carga y determinismo

**Files:**
- Modify: `AgroLife/Assets/Scripts/Sim/Simulation.cs`
- Test: `AgroLife/Assets/Tests/Sim/DeterminismoTests.cs`

**Interfaces:**
- Produces:
```csharp
public const int VersionGuardado = 1;
public sealed class Partida { public int Version; public EstadoJuego Estado; }
public sealed class PartidaIncompatibleException : Exception { public PartidaIncompatibleException(string m) : base(m) { } }
public string Guardar();                                         // JsonSim.Serializar(new Partida { Version = 1, Estado })
public static Simulation Cargar(DatosJuego datos, string json);  // EventosIniciales vacío
```

**Mensajes de `Cargar`** (todos lanzan `PartidaIncompatibleException`):
- JSON inválido → "La partida guardada está dañada: {msg}"
- versión distinta → "La partida guardada es de una versión incompatible (v{v}; se esperaba v1)"
- otra región → "La partida guardada es de otra región ({RegionId})"
- distinta cantidad de lotes → "La partida guardada no coincide con los lotes de la región"

El archivo en disco y su reemplazo atómico son de Game (plan 3).

- [ ] **Step 1: Escribir los tests que fallan.** Todos usan `DatosPrueba.Nueva(semilla, plagas: true)` y este guion:

```csharp
static void Guion(Simulation s, int dias) {
    for (int i = 0; i < dias && !s.Estado.Terminada; i++) {
        foreach (var l in s.Estado.Lotes) if (l.Decision != null && !s.DecidirTratamiento(l.Id, true).Ok) s.DecidirTratamiento(l.Id, false);
        var f = s.Estado.Fecha;
        if (f.Dia == 1 && f.Mes == 5) {
            s.ArrendarLote(2); s.PlanificarCultivo(2, new PlanCultivo { CultivoId = "maiz", FechaSiembra = Fecha.EnCampania(f.Campania, 1, 10), Genetica = Genetica.Largo, NKgHa = 120, PKgHa = 20 });
            s.ArrendarLote(1); s.PlanificarCultivo(1, new PlanCultivo { CultivoId = "trigo", FechaSiembra = Fecha.EnCampania(f.Campania, 15, 6), Genetica = Genetica.Largo, NKgHa = 90, PKgHa = 15,
                VenderAlCosechar = false, Soja2 = new PlanCultivo { CultivoId = "soja2", Genetica = Genetica.Largo, PKgHa = 10, VenderAlCosechar = false } });
        }
        if (f.Dia == 1 && f.Mes == 7) foreach (var g in new[] { Grano.Trigo, Grano.Maiz, Grano.Soja }) if (s.Estado.StockT[g] > 0) s.VenderGrano(g, s.Estado.StockT[g]);
        s.StepDay();
    }
}
[Test] public void MismaSemillaDiezAniosEstadoIdentico()     // dos simulaciones (42), Guion 3650 días cada una → Guardar() idénticos
[Test] public void DecisionesDistintasNoCambianClimaNiPrecios()
    // A y B con semilla 42; día por día durante 3 años, A con Guion(1) y B solo StepDay;
    // cada día: los campos de Clima.Hoy y los tres precios son iguales en A y B
[Test] public void GuardarYCargarDaElMismoEstado() {
    var a = DatosPrueba.Nueva(42, true); Guion(a, 500); var json = a.Guardar();
    var b = Simulation.Cargar(DatosPrueba.Cargar(), json); Assert.AreEqual(json, b.Guardar());
    Guion(a, 400); Guion(b, 400); Assert.AreEqual(a.Guardar(), b.Guardar());
}
[Test] public void GuardarConDecisionPendienteLaConserva() {  // Review Focus 5
    // plagas: chinches ProbDiaria 1, Severidad {0.2, 0.3}; arrendar 3; soja1 15/11; avanzar hasta que haya Decision en el lote 3;
    // b = Cargar(datos con las mismas modificaciones, a.Guardar()): b.Estado.Lotes[2].Decision.Severidad == la de a; Assert.Throws<InvalidOperationException>(() => b.StepDay())
}
[Test] public void VersionIncompatibleSeRechazaConMensaje() {
    var json = DatosPrueba.Nueva().Guardar().Replace("\"Version\":1", "\"Version\":99");
    Assert.AreEqual("La partida guardada es de una versión incompatible (v99; se esperaba v1)",
        Assert.Throws<PartidaIncompatibleException>(() => Simulation.Cargar(DatosPrueba.Cargar(), json)).Message);
}
[Test] public void PartidaDaniadaSeRechaza()                 // Cargar(datos, "{") → PartidaIncompatibleException con mensaje que empieza en "La partida guardada está dañada:"
```

- [ ] **Step 2: Correr `DeterminismoTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar `Guardar` y `Cargar`.** Si `MismaSemillaDiezAnios` o `GuardarYCargar` fallan, la causa suele ser una de estas:
  - un `Dictionary` armado en distinto orden,
  - una propiedad calculada que se serializa,
  - un sorteo que depende de algo que no está en el estado.

  Diagnosticar con el primer carácter distinto entre los dos JSON.
- [ ] **Step 4: Correr `DeterminismoTests`.** Esperado: 6/6 PASS.
- [ ] **Step 5: Verificación final.** Correr todo el assembly: `run_tests(mode="EditMode", assembly_names=["AgroLife.Sim.Tests"])`.
  - Esperado: todos PASS y `read_console(types=["error","warning"])` sin errores ni warnings de AgroLife.
  - Verificar que ningún archivo de `Scripts/Sim` contenga `UnityEngine`: `grep -r "UnityEngine" AgroLife/Assets/Scripts/Sim` no tiene que devolver nada.
