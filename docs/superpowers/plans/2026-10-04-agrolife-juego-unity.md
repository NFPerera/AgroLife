# AgroLife v1 — Plan 3: el juego en Unity (AgroLife.Game)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** El juego jugable en Unity: menú, mapa de Pergamino con sus 3.477 lotes, tiempo que corre, y toda la interfaz del spec §8 (ficha del lote, planificación con presupuesto en vivo, decisiones ante eventos, mercado, reporte de campaña, glosario), con guardado y carga.

**Architecture:**
- Asmdef `AgroLife.Game`, que referencia `AgroLife.Sim` y `Unity.InputSystem`.
  - Lee el estado de Sim para dibujar y lo cambia **solo** con las acciones de `Simulation`.
  - Un `ControladorTiempo` llama a `StepDay`.
- El mapa es:
  - un `PolygonCollider2D` por lote, para el clic;
  - **una sola malla combinada** con color por vértice, armada con `Collider2D.CreateMesh`, para dibujar.
- La interfaz es un único UXML por escena. Cada panel tiene una clase C# propia, sin MonoBehaviour, que la cablea un `UIJuego` raíz.
- Los gráficos se dibujan con `Painter2D`.

**Tech Stack:** Unity 6000.3.14f1, URP con el **Renderer 2D**, UI Toolkit (runtime), Input System (el proyecto ya tiene `activeInputHandler: 1`) y Unity Test Framework (EditMode) para la lógica de Game.

**Spec:** [docs/superpowers/specs/2026-10-04-agrolife-v1-design.md](../specs/2026-10-04-agrolife-v1-design.md), §4, §5 (AgroLife.Game), §8 y §10. APIs de Sim: [Plan 1](2026-10-04-agrolife-sim-nucleo.md), Tareas 10–13. Datos: [Plan 2](2026-10-04-agrolife-pipeline-pergamino.md) (`region.json`, `lotes.json` con `poligono`, y `geografia.json`).

### Estado de partida (verificado el 2026-10-04)

- **Render:** `GraphicsSettings` y las dos calidades (Mobile y PC) usan `PC_RPAsset`/`Mobile_RPAsset`, con **Universal Renderer 3D**. El spec pide URP 2D.
- **Input:** solo el Input System.
- **Build Settings:** solo `SampleScene` (con el cubo "Carlos" de una prueba vieja).
- **`execute_code` de MCP** compila con **CodeDom (C# 6)**. Los fragmentos de verificación de este plan no usan sintaxis más nueva: nada de tuplas, `is not`, records ni `new()`.
- **Minors heredados que este plan resuelve:**
  - Mostrar el vencimiento del arrendamiento antes de confirmar.
  - Mostrar solo el `Pronostico` y nunca `Estado.Clima.Fase`.
  - No llamar `CalcularPresupuesto` con un plan incompleto.
  - Si al cargar hay una decisión pendiente, abrirla enseguida.

## Global Constraints

- **Game nunca escribe en `Simulation.Estado`.** Todo cambio pasa por una acción (`ArrendarLote`, `PlanificarCultivo`, `DecidirTratamiento`, `VenderGrano`, ...) y su `Resultado`. Si la acción se rechaza, se muestra `Motivo`.
- **Nada de uGUI ni de librerías externas.** UI Toolkit para la interfaz y `Painter2D` para los gráficos. Sin sprites: colores planos.
- **Input:** `UnityEngine.InputSystem` (`Mouse.current`, `Keyboard.current`). Prohibido usar `UnityEngine.Input`.
- **Escala:** 1 unidad de Unity = 100 m (`Escala.MetrosPorUnidad = 100`). Los datos vienen en metros locales del origen UTM de `region.json`.
- **Tiempo:**
  - x1 = 6 días/s, x2 = 12, x4 = 24.
  - Como máximo **4 días por frame**.
  - Se pausa solo ante eventos con `Pausa` o decisiones pendientes (spec §4).
- **Textos** en español rioplatense. **Números** con `Formato.Numero` y `Formato.Usd` de Sim.
- **Guardado:**
  - `Application.persistentDataPath/saves/partida-<semilla>.json`, con el JSON de `Simulation.Guardar()`.
  - Se escribe un `.tmp` y después se reemplaza.
  - Si la versión es incompatible, se avisa y no se rompe (spec §10).
- **Datos:** `Assets/Data/*.json` y `Regions/pergamino/*.json` se referencian como `TextAsset` desde el asset `FuenteDatos`. Con datos inválidos, el error se muestra en el menú y no se puede jugar.
- **Plataforma:** PC. No se usa nada que bloquee WebGL: ni hilos ni plugins nativos.
- **Herramientas:**
  - Con el Editor abierto: escenas y assets por MCP, scripts como archivos.
  - Después de cada script, `read_console`.
  - Los tests se corren como en el plan 1, con el assembly `AgroLife.Game.Tests` además de `AgroLife.Sim.Tests`.
- **Tests:** según el spec §9, la interfaz de Game se prueba a mano. Acá se testea en EditMode **solo la lógica de Game**: guardado, pasos de tiempo, colores, malla, cámara y carga de datos. Cada tarea de interfaz termina con una verificación en Play mode por MCP y una lista de pruebas manuales.
- **No commitear** (CLAUDE.md).

## Review Focus

1. **Clic sobre un panel de la interfaz que cae encima del mapa.** No tiene que seleccionar el lote de abajo. → Tareas 8 y 9.
2. **A x4 con un frame lento, o un evento con pausa.** No se avanzan más de 4 días por frame. Al haber evento con `Pausa` o una decisión, se frena **ese mismo día**: ni un día de más. → Tarea 4.
3. **Cargar una partida con una decisión de plaga pendiente.** El diálogo se abre enseguida y el tiempo queda en pausa. → Tareas 11 y 15.
4. **Guardado cortado o archivo de partida corrupto.** La partida anterior queda intacta. En el menú, un archivo dañado se muestra con su mensaje y no hace caer el juego. → Tareas 3 y 15.
5. **Presupuesto en vivo con controles que no aplican** (N en soja, fecha de la soja de segunda). Esos controles se deshabilitan, y el presupuesto nunca lanza una excepción mientras se edita. → Tarea 10.

---

### Task 1: URP 2D, escenas, asmdefs y carga de datos

**Files:**
- Create (por MCP/`execute_code`): `Assets/Settings/URP2D_Renderer.asset` (Renderer2DData), `Assets/Settings/URP2D_RPAsset.asset`
- Create: `Assets/Materials/ColorVertice.mat`, con el shader `Universal Render Pipeline/2D/Sprite-Unlit-Default`: color por vértice y sin textura.
- Create: `Assets/Scenes/Menu.unity` y `Assets/Scenes/Mapa.unity`. Build Settings: Menu (0) y Mapa (1). `SampleScene` sale de la lista, pero el archivo queda.
- Create: `Assets/Scripts/Game/AgroLife.Game.asmdef`, `Assets/Scripts/Game/Datos/FuenteDatos.cs`, `Assets/Scripts/Game/Datos/Geografia.cs`
- Create: `Assets/Data/FuenteDatos.asset`
- Create: `Assets/Tests/Game/AgroLife.Game.Tests.asmdef`
- Test: `Assets/Tests/Game/FuenteDatosTests.cs`

**Interfaces:**
- Produces:
```csharp
namespace AgroLife.Game
public static class Escala { public const float MetrosPorUnidad = 100f; public static Vector2 AUnidades(int xMetros, int yMetros); }
[CreateAssetMenu] public sealed class FuenteDatos : ScriptableObject {
    public TextAsset Crops, Pests, Economy, Soils, Region, Lotes, Clima, Geografia, Glosario;
    public DatosJuego CargarDatos();          // DatosJuego.Desde(...); deja pasar DatosInvalidosException
    public GeografiaRegion CargarGeografia(); // ver abajo
}
public sealed class LoteForma { public int Id; public List<int[]> Poligono; }             // de lotes.json (solo id y poligono)
public sealed class Localidad { public string Nombre, Tipo; public int X, Y; }
public sealed class GeografiaRegion {
    public List<LoteForma> Lotes;                 // índice = id − 1
    public List<int[]> Contorno; public List<List<int[]>> Rutas, Caminos, Arroyos;
    public List<Localidad> Localidades, Acopios;  // de region.json (los acopios usan Nombre, X, Y)
}
```
- `CargarGeografia` parsea con `JsonSim.Deserializar` sobre DTOs internos (`{"lotes":[{id,poligono}]}`, `geografia.json` y `region.json`). Game no referencia Newtonsoft.
- `AgroLife.Game.asmdef`: `references: ["AgroLife.Sim", "Unity.InputSystem"]`, `autoReferenced: true`.
- `AgroLife.Game.Tests.asmdef`: igual que el de los tests de Sim, más `AgroLife.Game` y `Unity.InputSystem`.

- [ ] **Step 1: Configurar URP 2D por `execute_code`**

  Asignarlo como pipeline por defecto y en **todas** las calidades:
```csharp
var datos = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.Renderer2DData>();
AssetDatabase.CreateAsset(datos, "Assets/Settings/URP2D_Renderer.asset");
var rp = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.Create(datos);
AssetDatabase.CreateAsset(rp, "Assets/Settings/URP2D_RPAsset.asset");
UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = rp;
int original = QualitySettings.GetQualityLevel();
for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = rp; }
QualitySettings.SetQualityLevel(original, false);
AssetDatabase.SaveAssets();
```
  Verificar con `manage_graphics(action="pipeline_get_info")`: el renderer por defecto tiene que ser `Renderer2DData`. Los assets 3D viejos quedan en el proyecto, sin usar.

- [ ] **Step 2: Crear el material, las escenas y el Build Settings**
  - Escena `Mapa`: `Main Camera` ortográfica en (0, 0, −10), fondo `#1E2A22`, `Size` 300.
  - Escena `Menu`: una cámara vacía.
  - Agregar las dos al Build Settings con `manage_build`.

- [ ] **Step 3: Escribir el test que falla** (`FuenteDatosTests`)

```csharp
[Test] public void FuenteDatosCargaPergamino() {
    var f = AssetDatabase.LoadAssetAtPath<FuenteDatos>("Assets/Data/FuenteDatos.asset");
    var d = f.CargarDatos();
    var g = f.CargarGeografia();
    Assert.AreEqual(d.Lotes.Count, g.Lotes.Count);
    Assert.That(g.Lotes.Count, Is.InRange(3000, 5000));
    Assert.AreEqual(1, g.Lotes[0].Id);
    Assert.GreaterOrEqual(g.Lotes[0].Poligono.Count, 3);
    Assert.GreaterOrEqual(g.Contorno.Count, 3);
    Assert.Greater(g.Localidades.Count, 0);
    Assert.AreEqual(g.Localidades.Count, g.Acopios.Count);
}
[Test] public void EscalaPasaMetrosAUnidades() => Assert.AreEqual(new Vector2(12.34f, -5f), Escala.AUnidades(1234, -500));
```

- [ ] **Step 4: Correr `FuenteDatosTests`.** Esperado: falla la compilación porque no existe `FuenteDatos`.
- [ ] **Step 5: Implementar `FuenteDatos.cs` y `Geografia.cs`.**
  - Crear `Assets/Data/FuenteDatos.asset` con `execute_code`, asignando los 9 `TextAsset`.
  - `Glosario` apunta a `Assets/Data/glosario.json`, que se crea vacío (`{"terminos":[]}`) y se llena en la Tarea 14.
- [ ] **Step 6: Correr `FuenteDatosTests`.** Esperado: 2/2 PASS, y la suite de Sim sigue en 102/102.

---

### Task 2: Sim guarda el historial de precios

El gráfico del mercado (spec §8) necesita la evolución del precio, y tiene que sobrevivir a guardar y cargar. Es el único cambio en Sim de este plan.

**Files:**
- Modify: `Assets/Scripts/Sim/Economia/Precios.cs` (agrega `PrecioDia` y `EstadoPrecios.Historial`)
- Modify: `Assets/Scripts/Sim/Simulation.Dia.cs` (registra el día después de `Precios.AvanzarDia`)
- Test: `Assets/Tests/Sim/EconomiaTests.cs` (agrega 1 test)

**Interfaces:**
- Produces:
```csharp
public sealed class PrecioDia { public Fecha Fecha; public double TrigoUsdT, MaizUsdT, SojaUsdT; }
// EstadoPrecios:
public List<PrecioDia> Historial = new List<PrecioDia>();   // los últimos 730 días simulados, en orden
```

- [ ] **Step 1: Escribir el test que falla**

```csharp
[Test] public void HistorialDePreciosGuardaLosUltimos730Dias() {
    var s = DatosPrueba.Nueva();
    for (int i = 0; i < 800; i++) s.StepDay();
    var h = s.Estado.Precios.Historial;
    Assert.AreEqual(730, h.Count);
    Assert.AreEqual(s.Estado.Fecha, h[h.Count - 1].Fecha);
    Assert.AreEqual(s.Estado.Precios.PrecioUsdT[Grano.Soja], h[h.Count - 1].SojaUsdT);
    Assert.AreEqual(h[0].Fecha.MasDias(729), h[729].Fecha);
}
```

- [ ] **Step 2: Correr `EconomiaTests`.** Esperado: falla la compilación porque no existe `Historial`.
- [ ] **Step 3: Implementar.** Al final del paso 3 de `SimularDia`, se agrega un `PrecioDia` y se quita el primero si pasan de 730.
- [ ] **Step 4: Correr `EconomiaTests` y `DeterminismoTests`.** Esperado: PASS. El guardado incluye el historial, y la repetibilidad y el guardar/cargar siguen idénticos.

---

### Task 3: Guardado y carga en disco

**Files:**
- Create: `Assets/Scripts/Game/Guardado.cs`
- Test: `Assets/Tests/Game/GuardadoTests.cs`

**Interfaces:**
- Produces:
```csharp
public sealed class PartidaGuardada { public string Ruta, Descripcion; public DateTime Modificada; public string Error; } // Error != null: no se puede cargar
public static class Guardado {
    public static string Carpeta { get; set; }                     // por defecto Path.Combine(Application.persistentDataPath, "saves")
    public static string RutaPara(ulong semilla);                  // Carpeta/partida-<semilla>.json
    public static void Guardar(Simulation s, string ruta);         // escribe ruta + ".tmp"; File.Replace si existe, si no File.Move
    public static List<PartidaGuardada> Listar(DatosJuego datos);  // más nueva primero
    public static Simulation Cargar(DatosJuego datos, string ruta);// deja pasar PartidaIncompatibleException
}
```
- `Descripcion` dice, por ejemplo, `"Pergamino · 15/9 Año 3 · US$ 120.345"`.
- En `Listar`, si un archivo falla con `Cargar`, `Error` lleva el mensaje de la excepción y `Descripcion` el nombre del archivo.

- [ ] **Step 1: Escribir los tests que fallan.** Usan `Guardado.Carpeta = Path.Combine(Path.GetTempPath(), "agrolife-tests-" + Guid.NewGuid())` y la región de prueba (`AgroLife.Sim.Tests` no se puede referenciar, así que el test lee los mismos archivos de `Assets/Tests/Sim/Datos/` con un helper propio).

```csharp
[Test] public void GuardarYCargarDevuelveLaMismaPartida() {
    var s = Nueva(); for (int i = 0; i < 30; i++) s.StepDay();
    var ruta = Guardado.RutaPara(s.Estado.Semilla); Guardado.Guardar(s, ruta);
    Assert.AreEqual(s.Guardar(), Guardado.Cargar(Datos(), ruta).Guardar());
    Assert.IsFalse(File.Exists(ruta + ".tmp"));
}
[Test] public void GuardarDosVecesReemplaza() {
    var s = Nueva(); var ruta = Guardado.RutaPara(s.Estado.Semilla);
    Guardado.Guardar(s, ruta); s.StepDay(); Guardado.Guardar(s, ruta);
    Assert.AreEqual(s.Estado.Fecha, Guardado.Cargar(Datos(), ruta).Estado.Fecha);
}
[Test] public void ArchivoDanadoApareceConSuErrorYNoRompeLaLista() {
    var s = Nueva(); Guardado.Guardar(s, Guardado.RutaPara(1));
    Directory.CreateDirectory(Guardado.Carpeta); File.WriteAllText(Path.Combine(Guardado.Carpeta, "partida-2.json"), "{");
    var lista = Guardado.Listar(Datos());
    Assert.AreEqual(2, lista.Count);
    StringAssert.StartsWith("La partida guardada está dañada", lista.Single(p => p.Ruta.EndsWith("partida-2.json")).Error);
    Assert.IsNull(lista.Single(p => p.Ruta.EndsWith("partida-1.json")).Error);
}
```

- [ ] **Step 2: Correr `GuardadoTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar `Guardado.cs`.**
- [ ] **Step 4: Correr `GuardadoTests`.** Esperado: 3/3 PASS.

---

### Task 4: Controlador de tiempo

**Files:**
- Create: `Assets/Scripts/Game/ControladorTiempo.cs`
- Test: `Assets/Tests/Game/ControladorTiempoTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum Velocidad { Pausa = 0, X1 = 6, X2 = 12, X4 = 24 }   // días por segundo
public static class Pasos {
    public const int MaxDiasPorFrame = 4;
    public static int DiasAAvanzar(ref double acumulado, double dt, Velocidad v);  // suma dt·v; devuelve hasta MaxDiasPorFrame días enteros; el sobrante > 1 día se descarta
}
public sealed class ControladorTiempo : MonoBehaviour {
    public Simulation Sim;                         // lo asigna UIJuego
    public Velocidad Velocidad { get; }
    public event Action<List<Evento>> Eventos;     // uno por día avanzado
    public event Action<Velocidad> CambioVelocidad;
    public void CambiarVelocidad(Velocidad v);     // se rechaza (queda en Pausa) si hay decisión pendiente o la partida terminó
    public static bool HayDecisiones(Simulation s);// algún lote con Decision != null
}
```
`Update`:
```
n = Pasos.DiasAAvanzar(ref acumulado, Time.unscaledDeltaTime, Velocidad)
por cada día: ev = Sim.StepDay(); Eventos?.Invoke(ev)
    si algún ev.Pausa, o HayDecisiones(Sim), o Sim.Estado.Terminada: CambiarVelocidad(Pausa); acumulado = 0; cortar el bucle
```

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
[Test] public void X1AvanzaSeisDiasPorSegundo() {
    double a = 0; int total = 0;
    for (int i = 0; i < 60; i++) total += Pasos.DiasAAvanzar(ref a, 1.0 / 60, Velocidad.X1);
    Assert.AreEqual(6, total);
}
[Test] public void UnFrameLentoNoAvanzaMasDeCuatroDias() {
    double a = 0;
    Assert.AreEqual(4, Pasos.DiasAAvanzar(ref a, 2.0, Velocidad.X4));   // 48 días pedidos
    Assert.Less(a, 1.0);
}
[Test] public void EnPausaNoAvanza() { double a = 0.9; Assert.AreEqual(0, Pasos.DiasAAvanzar(ref a, 1, Velocidad.Pausa)); }
```

- [ ] **Step 2: Correr `ControladorTiempoTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar `ControladorTiempo.cs`.**
- [ ] **Step 4: Correr `ControladorTiempoTests`.** Esperado: 3/3 PASS.

---

### Task 5: Malla de lotes y superposiciones

**Files:**
- Create: `Assets/Scripts/Game/Mapa/MallaLotes.cs`
- Create: `Assets/Scripts/Game/Mapa/MapaLotes.cs`
- Create: `Assets/Scripts/Game/Mapa/Superposiciones.cs`
- Test: `Assets/Tests/Game/MallaLotesTests.cs`

**Interfaces:**
- Produces:
```csharp
public sealed class MallaLotes {   // una malla con todos los lotes; colores por lote
    public Mesh Mesh { get; }                                     // IndexFormat.UInt32
    public static MallaLotes Construir(IReadOnlyList<Mesh> mallasPorLote);  // concatena; recuerda el rango de vértices de cada lote
    public (int inicio, int cantidad) Rango(int indiceLote);
    public void Pintar(int indiceLote, Color32 color);            // escribe el buffer de colores
    public void Aplicar();                                        // Mesh.SetColors una vez por recoloreo
}
public sealed class MapaLotes : MonoBehaviour {
    public Material Material;                                     // ColorVertice.mat
    public void Construir(GeografiaRegion g);                     // un GameObject hijo por lote con PolygonCollider2D (name = "Lote <id>") + la malla combinada + bordes
    public int LoteEn(Vector2 mundo);                             // Physics2D.OverlapPoint → id, o 0
    public void Pintar(Func<int, Color32> colorPorId);            // recolorea todo y aplica
    public void Resaltar(int idSeleccionado, IReadOnlyCollection<int> propios, IReadOnlyCollection<int> arrendados); // contornos: selección blanco, propios #4FA3FF, arrendados #FF9F40
}
public static class Superposiciones {
    public static Mesh Lineas(IEnumerable<List<int[]>> lineas);   // MeshTopology.Lines, UInt32, coordenadas en unidades
    public static void Crear(Transform padre, GeografiaRegion g, Material m);  // rutas #D8D2C4, caminos #6E6A5E, arroyos #4FA3D9, contorno #F2E6C9, en ese orden de dibujo
}
```
- La malla de cada lote sale de `PolygonCollider2D.CreateMesh(false, false)`, después de asignar `points` con `Escala.AUnidades`.
- Los bordes de todos los lotes van en una malla de líneas `#2A3A2E`.
- Los contornos resaltados van en una malla de líneas aparte, que se rearma en `Resaltar`.
- Orden de dibujo: el `sortingOrder` sube de abajo hacia arriba: lotes, bordes, superposiciones, resaltados.

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
[Test] public void ConstruyeLaMallaConUnRangoPorLote() {
    var a = Cuadrado(0, 0, 1); var b = Cuadrado(5, 0, 2);     // helpers: Mesh de 4 vértices y 2 triángulos
    var m = MallaLotes.Construir(new[] { a, b });
    Assert.AreEqual(8, m.Mesh.vertexCount);
    Assert.AreEqual((4, 4), m.Rango(1));
    m.Pintar(1, new Color32(255, 0, 0, 255)); m.Aplicar();
    Assert.AreEqual(new Color32(255, 0, 0, 255), m.Mesh.colors32[5]);
    Assert.AreEqual(12, m.Mesh.triangles.Length);
}
[Test] public void LineasUsaTopologiaDeLineas() {
    var mesh = Superposiciones.Lineas(new[] { new List<int[]> { new[] { 0, 0 }, new[] { 100, 0 }, new[] { 100, 100 } } });
    Assert.AreEqual(MeshTopology.Lines, mesh.GetTopology(0));
    Assert.AreEqual(4, mesh.GetIndices(0).Length);   // 2 segmentos
}
```

- [ ] **Step 2: Correr `MallaLotesTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar los tres archivos.**
- [ ] **Step 4: Correr `MallaLotesTests`.** Esperado: 2/2 PASS.
- [ ] **Step 5: Verificar en Play mode.**
  - Agregar a `Mapa.unity` un GameObject `Mapa` con `MapaLotes` y un `Arranque` temporal que haga `Construir(fuente.CargarGeografia())` y pinte todo de verde. Se reemplaza en la Tarea 8.
  - `manage_editor(action="play")`, después `manage_camera(action="screenshot")`, después `read_console`.
  - Esperado: el partido completo, con lotes, bordes, rutas, arroyos y contorno, y la consola sin errores.
  - Medir con `manage_graphics(action="stats_get")`: menos de 50 batches.

---

### Task 6: Capas de color

**Files:**
- Create: `Assets/Scripts/Game/Mapa/CapasMapa.cs`
- Test: `Assets/Tests/Game/CapasMapaTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum Capa { Estado, ClaseTextural, Ip, Agua, RindeEstimado }
public static class CapasMapa {
    public static readonly Color32 SueloDesnudo;              // #8C7A5B
    public static Color32 Color(Capa capa, int loteId, Simulation s);
    public static string Titulo(Capa capa);                   // "Estado", "Clase textural", "IP", "Agua útil (%)", "Rinde estimado"
    public static IReadOnlyList<(string texto, Color32 color)> Leyenda(Capa capa);  // para la leyenda de la UI
}
```
Reglas de color:
- **Estado:**
  - Sin cultivo: `SueloDesnudo`.
  - Con cultivo: `Lerp(claro, oscuro, progreso)`, donde `progreso = GradosDia / Umbrales.Madurez` (con tope en 1).
    - Trigo: claro `#B9D67A`, oscuro `#4E7A2A`.
    - Maíz: claro `#8FD18A`, oscuro `#1F6B2E`.
    - Soja 1ª y 2ª: claro `#A8E0B0`, oscuro `#2F7F4F`.
  - En `Madurez`: `#E3C04B`.
  - La tenencia no cambia el relleno: se ve en los contornos de la Tarea 5.
- **Clase textural:** 12 colores fijos, uno por valor de `ClaseTextural`, en una paleta del arena (amarillos) a la arcilla (rojizos) que se define en el archivo.
- **IP:** gradiente `#B5452F` (0) → `#E3C04B` (50) → `#3E8E41` (100).
- **Agua:** con `% = AguaMm / AuMaxMm`, gradiente `#8C5A2B` (0) → `#E8E1C4` (50 %) → `#2C6FB5` (100 % o más).
- **Rinde estimado:** sin cultivo, `#4A4A4A`. Con cultivo, `RindeEstimadoQqHa / RindeAlcanzableQqHa` en el gradiente de IP.

- [ ] **Step 1: Escribir los tests que fallan.** Usan una partida nueva de la región de prueba y un lote sembrado a mano en el estado, como en el plan 1 (Tarea 11).

```csharp
[Test] public void EstadoSinCultivoEsSueloDesnudo() => Assert.AreEqual(CapasMapa.SueloDesnudo, CapasMapa.Color(Capa.Estado, 1, Nueva()));
[Test] public void EstadoEnMadurezEsAmarillo() {
    var s = Nueva(); var l = s.Estado.Lotes[0];
    l.Cultivo = ModeloCultivo.Sembrar(s.Datos.Cultivos["maiz"], Genetica.Largo, s.Estado.Fecha, 0, 0, s.Datos.Lote(1), 20, s.Datos.Suelo);
    l.Cultivo.Etapa = Etapa.Madurez;
    Assert.AreEqual(new Color32(0xE3, 0xC0, 0x4B, 255), CapasMapa.Color(Capa.Estado, 1, s));
}
[Test] public void GradienteDeTresParadas() {
    Color32 r = new Color32(0xB5, 0x45, 0x2F, 255), a = new Color32(0xE3, 0xC0, 0x4B, 255), v = new Color32(0x3E, 0x8E, 0x41, 255);
    Assert.AreEqual(r, CapasMapa.Gradiente(0f, r, a, v));
    Assert.AreEqual(a, CapasMapa.Gradiente(0.5f, r, a, v));
    Assert.AreEqual(v, CapasMapa.Gradiente(1.7f, r, a, v));   // fuera de rango se limita
}
[Test] public void LeyendaDeCadaCapaNoEstaVacia() { foreach (Capa c in Enum.GetValues(typeof(Capa))) Assert.Greater(CapasMapa.Leyenda(c).Count, 1); }
```
`Gradiente(float t, params Color32[] paradas)` es pública: reparte las paradas en partes iguales y limita `t` a [0, 1].

- [ ] **Step 2: Correr `CapasMapaTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar `CapasMapa.cs`.**
- [ ] **Step 4: Correr `CapasMapaTests`.** Esperado: 4/4 PASS.

---

### Task 7: Cámara del mapa

**Files:**
- Create: `Assets/Scripts/Game/Mapa/CamaraMapa.cs`
- Test: `Assets/Tests/Game/CamaraMapaTests.cs`

**Interfaces:**
- Produces:
```csharp
public sealed class CamaraMapa : MonoBehaviour {
    public Rect Limites;                                        // bounds del contorno en unidades, +10 %
    public float TamanioMin = 5f;                               // 500 m de medio alto
    public Func<Vector2, bool> PunteroSobreUI;                  // lo asigna UIJuego; si es true, no hay zoom ni arrastre
    public static Vector3 Limitar(Vector3 pos, float tamanio, float aspecto, Rect limites);  // el centro no deja ver fuera de los límites; si la vista es más grande que los límites, centra
    public static float ZoomHacia(float tamanio, float rueda, float min, float max);       // cada muesca ×/÷ 1,15
}
```
- Entrada:
  - rueda: zoom hacia el cursor (el punto bajo el cursor queda fijo);
  - botón derecho o del medio arrastrado: desplazamiento;
  - WASD o flechas: desplazamiento a `tamaño × 1,5` unidades/s.
- El tamaño máximo es el que muestra todos los límites.

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
[Test] public void LimitarNoDejaVerFueraDelContorno() {
    var r = new Rect(-100, -100, 200, 200);
    Assert.AreEqual(new Vector3(90, 0, -10), CamaraMapa.Limitar(new Vector3(500, 0, -10), 10, 1, r));
}
[Test] public void VistaMasGrandeQueElContornoCentra() =>
    Assert.AreEqual(new Vector3(0, 0, -10), CamaraMapa.Limitar(new Vector3(50, 50, -10), 300, 1, new Rect(-100, -100, 200, 200)));
[Test] public void ZoomRespetaLosTopes() {
    Assert.AreEqual(5f, CamaraMapa.ZoomHacia(5.5f, 1, 5, 300), 1e-4);
    Assert.AreEqual(11.5f, CamaraMapa.ZoomHacia(10f, -1, 5, 300), 1e-4);
}
```

- [ ] **Step 2: Correr `CamaraMapaTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Implementar `CamaraMapa.cs`.**
- [ ] **Step 4: Correr `CamaraMapaTests`.** Esperado: 3/3 PASS.

---

### Task 8: Interfaz base: `UIJuego`, barra superior, notificaciones y leyenda

**Files:**
- Create: `Assets/UI/PanelSettings.asset` (por `manage_ui create_panel_settings`, `ScaleWithScreenSize` 1920×1080, con el tema runtime por defecto)
- Create: `Assets/UI/Estilos.uss`, `Assets/UI/Juego.uxml`
- Create: `Assets/Scripts/Game/Sesion.cs`, `Assets/Scripts/Game/UI/UIJuego.cs`, `Assets/Scripts/Game/UI/BarraSuperior.cs`, `Assets/Scripts/Game/UI/Notificaciones.cs`
- Modify: `Mapa.unity`. Se saca el `Arranque` temporal. Queda un GameObject `Juego` con `UIDocument` (Juego.uxml, PanelSettings), `UIJuego`, `ControladorTiempo`; más `Mapa` (`MapaLotes`) y la cámara con `CamaraMapa`.

**Interfaces:**
- Produces:
```csharp
public static class Sesion {                 // pasa la partida del menú al mapa
    public static FuenteDatos Fuente; public static DatosJuego Datos; public static GeografiaRegion Geografia;
    public static Simulation Sim; public static string RutaGuardado; public static string MensajeMenu;  // el menú lo muestra al volver
}
public sealed class UIJuego : MonoBehaviour {
    public FuenteDatos Fuente;               // si Sesion.Sim == null (Play directo en Mapa), arranca una partida nueva con semilla 1
    public MapaLotes Mapa; public CamaraMapa Camara; public ControladorTiempo Tiempo;
    public Capa CapaActual { get; }
    public void CambiarCapa(Capa c);
    public void SeleccionarLote(int id);     // 0 = ninguno; abre o cierra la ficha
    public bool PunteroSobreUI(Vector2 posicionPantalla);   // panel.Pick(...) != null y no es un contenedor con picking-mode="Ignore"
    public void Refrescar();                 // barra, ficha abierta, contornos y repintado de la capa
}
public sealed class BarraSuperior { public BarraSuperior(VisualElement raiz, UIJuego ui); public void Refrescar(Simulation s); }
public sealed class Notificaciones { public Notificaciones(VisualElement raiz); public void Agregar(Evento e); }  // las últimas 6; se desvanecen a los 8 s
```
**Estructura de `Juego.uxml`.** La raíz `#juego` ocupa toda la pantalla con `picking-mode="Ignore"`. Los nombres son los que usan las tareas siguientes:
- `#barra`:
  - `#fecha` (Label);
  - `#vel-pausa`, `#vel-x1`, `#vel-x2`, `#vel-x4` (Button; la activa lleva la clase `activo`);
  - `#saldo`;
  - `#pronostico` ("Niño 70 % · Neutro 15 % · Niña 15 %", desde `Estado.Clima.Pronostico`);
  - `#capa` (DropdownField con `CapasMapa.Titulo`);
  - `#btn-mercado`, `#btn-guardar`, `#btn-menu`.
- `#notificaciones` (abajo a la izquierda, `picking-mode="Ignore"`).
- `#leyenda` (abajo a la derecha).
- `#etiquetas` (absoluto, `picking-mode="Ignore"`): nombres de localidades y "▲" de acopios, que se reposicionan en `LateUpdate` con `RuntimePanelUtils.CameraTransformWorldToPanel`.
- Contenedores vacíos que llenan las tareas siguientes: `#ficha` (Tarea 9), `#planificacion` (10), `#dialogo` (11), `#mercado` (12), `#reporte` (13), `#tooltip` (14). Todos arrancan con la clase `oculto` (`display: none`).
- `Estilos.uss`: tema oscuro.
  - Fondo de paneles `rgba(20,28,22,0.92)`, texto `#EDE6D3`, acento `#E3C04B`.
  - Clases `panel`, `fila`, `etiqueta`, `valor`, `boton`, `activo`, `oculto`, `termino` (con subrayado punteado), `motivo` (texto rojizo `#E07A5F`).

`UIJuego.Start`:
1. Si `Sesion.Sim` es null, lo carga (`Fuente`, partida nueva con semilla 1).
2. `Mapa.Construir(Sesion.Geografia)`.
3. `Camara.Limites` = bounds del contorno; `Camara.PunteroSobreUI = PunteroSobreUI`.
4. `Tiempo.Sim = Sesion.Sim`, y se suscribe a `Tiempo.Eventos`.
5. Muestra `Sesion.Sim.EventosIniciales` (Tarea 11).
6. `Refrescar()`.

En cada lote de eventos: notificaciones, diálogo si corresponde, y `Refrescar()`. El repintado del mapa se hace **una vez por frame** como máximo, no una vez por día.

Clic en el mapa (`Update`): botón izquierdo liberado, sin arrastre de más de 5 px y `!PunteroSobreUI` → `SeleccionarLote(Mapa.LoteEn(mundo))`.

- [ ] **Step 1: Crear el `PanelSettings`, el UXML y el USS por `manage_ui`.** Cablear la escena por MCP.
- [ ] **Step 2: Implementar los cuatro scripts.** `read_console` sin errores.
- [ ] **Step 3: Verificar en Play mode.**
  - `play`, después `execute_code`: `var ui = Object.FindFirstObjectByType<AgroLife.Game.UIJuego>(); ui.Tiempo.CambiarVelocidad(AgroLife.Game.Velocidad.X4); return ui.Tiempo.Sim.Estado.Fecha.ToString();`.
  - Esperar 2 s y repetir. Esperado: la fecha avanzó y `#fecha` muestra lo mismo (`manage_ui get_visual_tree`).
  - `manage_camera screenshot`: barra visible, mapa con capa Estado y leyenda.
  - Probar `execute_code` con `ui.PunteroSobreUI(new Vector2(960, 1060))` sobre la barra: tiene que dar `true`. Esto es el Review Focus 1.
- [ ] **Step 4: Pruebas manuales** (para el usuario, en el reporte final):
  - Los botones de velocidad cambian el ritmo.
  - El selector de capa repinta.
  - Arrastrar y hacer zoom no se dispara sobre la barra.

---

### Task 9: Ficha lateral del lote y triángulo textural

**Files:**
- Create: `Assets/Scripts/Game/UI/FichaLote.cs`
- Create: `Assets/Scripts/Game/UI/Graficos/TrianguloTextural.cs`
- Modify: `Assets/UI/Juego.uxml` (contenido de `#ficha`) y `Estilos.uss`

**Interfaces:**
- Produces:
```csharp
public sealed class TrianguloTextural : VisualElement {      // Painter2D: triángulo, grilla cada 20 %, punto del lote
    public void Mostrar(double arena, double limo, double arcilla);
}
public sealed class FichaLote {
    public FichaLote(VisualElement raiz, UIJuego ui);
    public int LoteId { get; }
    public void Abrir(int loteId); public void Cerrar(); public void Refrescar(Simulation s);
    public event Action<int> PedirPlanificacion;           // lo atiende PanelPlanificacion (Tarea 10)
    public event Action<int, int> PedirReporte;            // (lote, campaña) → PanelReporte (Tarea 13)
}
```
**Contenido de `#ficha`** (panel a la derecha, 380 px):
- `#ficha-titulo`: "Lote 123 · 84,2 ha".
- `#ficha-datos`: una fila por dato, con etiqueta y valor:
  - Tenencia ("De terceros", "Arrendado hasta 30/4 Año 2", "Propio").
  - Unidad de suelo.
  - **Clase textural** (término del glosario).
  - **IP**.
  - Fósforo (ppm).
  - Cultivo anterior.
  - Distancia al acopio (km).
- `#ficha-triangulo`: un `TrianguloTextural` de 200 × 175 px.
- `#ficha-agua`: barra de **agua útil**: `AguaMm` sobre `AuMaxMm`, con el porcentaje y los mm.
- `#ficha-cultivo`:
  - cultivo, genética, etapa, fecha de siembra;
  - **rinde estimado** (`RindeEstimadoQqHa`, 1 decimal);
  - si hay plan pendiente: "Siembra planificada: maíz el 1/10".
- `#ficha-acciones`, según el estado:

| Estado del lote | Acciones |
|---|---|
| Tercero sin cultivo | "Arrendar (US$ X, vence 30/4 Año N)" y "Comprar (US$ Y)". Cada botón pide una confirmación en el mismo panel que repite precio y vencimiento, y recién ahí llama a la acción. |
| Tercero con cultivo | Texto: "Ocupado con maíz hasta la cosecha". |
| Del jugador, sin cultivo ni plan | "Planificar" (abre la Tarea 10). |
| Del jugador, con plan | "Cancelar plan" (`CancelarPlan`). |
| Arrendado y no comprado | Además, "Comprar". |
| Con resultados de alguna campaña | "Reporte campaña N", uno por campaña (`PedirReporte`). |

- `#ficha-motivo` (clase `motivo`): el `Motivo` del último rechazo. Se borra al cambiar de lote.
- `#ficha-cerrar`.

El vencimiento que se muestra sale de la misma regla que usa `ArrendarLote`: el 30/4 de la campaña de hoy, o de la siguiente si hoy es 30/4. Para no duplicarla, se agrega a Sim `public Fecha VencimientoArrendamiento()` en `Simulation.Acciones.cs`, que `ArrendarLote` también usa, con un test en `AccionesTests`: si hoy es 30/4 Año 2, devuelve 30/4 Año 3. Es un cambio chico en Sim, con test.

- [ ] **Step 1: Escribir el test de Sim que falla** (`VencimientoArrendamientoSigueLaRegla`) y verlo fallar.
- [ ] **Step 2: Implementar `VencimientoArrendamiento()` y usarlo en `ArrendarLote`.** Correr `AccionesTests`: todo PASS.
- [ ] **Step 3: Implementar `TrianguloTextural` y `FichaLote`.** Agregar el UXML. `read_console` sin errores.
- [ ] **Step 4: Verificar en Play mode.**
  - `execute_code` con `ui.SeleccionarLote(2)`: el árbol (`get_visual_tree`) muestra `#ficha` visible con "Lote 2" y el botón "Arrendar".
  - `execute_code` invocando la acción de arrendar desde la ficha (método público `Arrendar()` para la verificación): `Sesion.Sim.Estado.Lotes[1].Tenencia == Arrendado`, y el contorno naranja se ve en una captura.
  - Lote ocupado: avanzar al 1/7 y seleccionar el lote 1. Esperado: "Ocupado con trigo hasta la cosecha".
- [ ] **Step 5: Pruebas manuales.**
  - Clic en lotes, cerrar la ficha.
  - Un clic sobre la ficha que cae encima del mapa no cambia de lote.

---

### Task 10: Planificación con presupuesto en vivo

**Files:**
- Create: `Assets/Scripts/Game/UI/PanelPlanificacion.cs`
- Modify: `Juego.uxml` (`#planificacion`) y `Estilos.uss`

**Interfaces:**
- Produces:
```csharp
public sealed class PanelPlanificacion {
    public PanelPlanificacion(VisualElement raiz, UIJuego ui);
    public void Abrir(int loteId); public void Cerrar();
    public PlanCultivo PlanActual();                     // el plan que arman los controles; siempre completo
}
```
**Controles de `#planificacion`** (panel modal centrado):
- `#plan-cultivo` (DropdownField): "Trigo", "Maíz", "Soja de primera", "Trigo / soja de segunda".
- `#plan-fecha` (SliderInt): días de la ventana del cultivo que caen después de hoy y dentro de los próximos 365.
  - `#plan-fecha-texto` muestra la fecha ("15/10 Año 1").
  - Si ningún día de la ventana queda disponible: "La ventana de siembra de maíz ya pasó este año" y `#plan-confirmar` deshabilitado.
- `#plan-genetica` (RadioButtonGroup "Ciclo corto" / "Ciclo largo").
- `#plan-n` (SliderInt 0–250, paso 10, kg N/ha). **Se deshabilita y queda en 0** en soja.
- `#plan-p` (SliderInt 0–60, paso 5, kg P/ha).
- `#plan-destino` (RadioButtonGroup "Vender al cosechar" / "Embolsar en silobolsa").
- `#plan-soja2`: solo visible en doble cultivo, con `#plan-s2-genetica` y `#plan-s2-p`. Explica: "Se siembra sola al día siguiente de cosechar el trigo (hasta el 15/1)".
- Presupuesto, recalculado en cada `RegisterValueChangedCallback` con `CalcularPresupuesto(lote, PlanActual())`:
  - `#pres-costo` "Costo total: US$ X";
  - `#pres-rinde` "**Rinde esperado**: N qq/ha" (uno por cultivo en doble);
  - `#pres-mb` "**Margen bruto** proyectado: US$ X";
  - `#pres-ri` "**Rinde de indiferencia**: N qq/ha".
- `#plan-motivo` (clase `motivo`), y los botones `#plan-confirmar` y `#plan-cancelar`.
  - Confirmar → `PlanificarCultivo`. Si sale `Ok`, se cierra y se refresca. Si no, se muestra el `Motivo`.
- Valores iniciales: el `PlanTipico` del cultivo (fecha, genética, N y P). Si la fecha típica ya pasó, se usa el primer día disponible.

- [ ] **Step 1: Implementar `PanelPlanificacion`** y conectar `FichaLote.PedirPlanificacion`. `read_console` sin errores.
- [ ] **Step 2: Verificar en Play mode** (Review Focus 5). En una partida nueva:
  - arrendar el lote 2;
  - `execute_code`: abrir la planificación del lote 2;
  - poner por código el dropdown en "Soja de primera" (`#plan-n` tiene que quedar `enabledSelf == false` con valor 0);
  - después en "Trigo / soja de segunda" (`#plan-soja2` visible);
  - leer `#pres-costo` y `#pres-ri`: no vacíos, y la consola sin excepciones;
  - confirmar el maíz del 1/10: `Sesion.Sim.Estado.Lotes[1].Plan.CultivoId == "maiz"`.
- [ ] **Step 3: Pruebas manuales.** Mover los controles y ver que el presupuesto cambia; probar una fecha que no conviene (pérdida por fecha) y un rechazo por saldo.

---

### Task 11: Diálogos de eventos

**Files:**
- Create: `Assets/Scripts/Game/UI/DialogoEvento.cs`
- Modify: `Juego.uxml` (`#dialogo`) y `UIJuego.cs` (cola de diálogos)

**Interfaces:**
- Produces:
```csharp
public sealed class DialogoEvento {
    public DialogoEvento(VisualElement raiz, UIJuego ui);
    public bool Abierto { get; }
    public void Mostrar(Evento e);          // encola si hay uno abierto
    public void MostrarDecisionPendiente(); // si algún lote tiene Decision, abre la de menor id
}
```
**Contenido según el evento** (`#dialogo-titulo`, `#dialogo-texto`, `#dialogo-detalle`, `#dialogo-si`, `#dialogo-no`, `#dialogo-motivo`):

| Evento | Contenido |
|---|---|
| `InicioCampania` | Título "Campaña N". Texto con el pronóstico en barras simples (tres filas con su %) y una línea educativa: "El Niño suele traer más lluvia de primavera a otoño en la región pampeana; La Niña, menos". Botón "Seguir". |
| `Plaga` | Título "Chinches en el lote 7". Detalle con los datos de `Decision`: nivel frente al **umbral de daño económico**, costo de aplicar (US$), pérdida esperada en qq y US$. Botones "Aplicar" y "No aplicar" llaman a `DecidirTratamiento`; si sale rechazo, se muestra `Motivo` y el diálogo sigue abierto. |
| `SaldoCercaDelLimite` | Mensaje, con el saldo y el límite. Botón "Entendido". |
| `Quiebra` | Mensaje. Botón "Volver al menú": pone `Sesion.MensajeMenu = "La partida terminó por quiebra"` y carga `Menu`. |

- Mientras un diálogo está abierto, el tiempo queda en pausa y los botones de velocidad se deshabilitan.
- Al cerrar el último diálogo, la velocidad queda en Pausa: el jugador decide cuándo seguir.
- Los eventos sin `Pausa` van solo a `Notificaciones`.
- Si el evento no trae decisión pero hay decisiones pendientes (por ejemplo, al cargar), `UIJuego.Start` llama a `MostrarDecisionPendiente()` (Review Focus 3).

- [ ] **Step 1: Implementar `DialogoEvento`** y la cola en `UIJuego`. `read_console` sin errores.
- [ ] **Step 2: Verificar en Play mode.**
  - Partida nueva: el diálogo "Campaña 1" está abierto (`get_visual_tree`) y la velocidad está en Pausa.
  - Plaga forzada:
    - `execute_code` arrienda el lote 3 y planifica soja para el 15/11;
    - pone `ProbDiaria = 1` y `Severidad` {0,2; 0,3} a chinches en `Sesion.Datos.Plagas` (solo para la verificación);
    - pasa a x4.
  - Esperado: se frena en el día del evento, el diálogo de la plaga queda abierto y `ControladorTiempo.HayDecisiones` es true.
  - Llamar al botón "Aplicar" por código: decisión resuelta y saldo descontado.
- [ ] **Step 3: Pruebas manuales.** Leer el diálogo de campaña y probar "No aplicar".

---

### Task 12: Mercado

**Files:**
- Create: `Assets/Scripts/Game/UI/PanelMercado.cs`
- Create: `Assets/Scripts/Game/UI/Graficos/GraficoLinea.cs`
- Modify: `Juego.uxml` (`#mercado`)

**Interfaces:**
- Produces:
```csharp
public sealed class GraficoLinea : VisualElement {   // Painter2D: hasta 3 series, ejes con mínimo/máximo, etiquetas de inicio y fin
    public void Mostrar(IReadOnlyList<string> nombres, IReadOnlyList<Color> colores, IReadOnlyList<double[]> series, string inicio, string fin);
}
public sealed class PanelMercado { public PanelMercado(VisualElement raiz, UIJuego ui); public void Abrir(); public void Cerrar(); public void Refrescar(Simulation s); }
```
**Contenido de `#mercado`:**
- Precio de hoy por grano: `#precio-trigo`, `#precio-maiz`, `#precio-soja`, en US$/t y US$/qq.
- `#mercado-grafico`: `GraficoLinea` con los últimos 365 días de `Estado.Precios.Historial`, o menos si no hay. Colores: trigo `#E3C04B`, maíz `#8FD18A`, soja `#4FA3D9`.
- Stock en silobolsa: `#stock-trigo`, `#stock-maiz`, `#stock-soja` (t), cada uno con su valor al precio de hoy menos la comisión.
- Venta: `#venta-grano` (Dropdown), `#venta-t` (FloatField), `#venta-btn`, `#venta-todo` (vende todo el stock de ese grano).
  - Llaman a `VenderGrano`. Si sale rechazo, se muestra `Motivo` en `#mercado-motivo`.
- `#mercado-cerrar`.
- Se abre con `#btn-mercado`. Mientras está abierto, se refresca en cada lote de eventos (el tiempo puede seguir corriendo).

- [ ] **Step 1: Implementar `GraficoLinea` y `PanelMercado`.** `read_console` sin errores.
- [ ] **Step 2: Verificar en Play mode.**
  - Avanzar 60 días a x4 y abrir el mercado por código: el gráfico tiene 3 series con 61 puntos (método público `Puntos` para la verificación).
  - Cargar stock de soja y vender: `#stock-soja` muestra 60.
- [ ] **Step 3: Pruebas manuales.** Ver cómo el precio baja en cosecha (estacionalidad) y vender todo.

---

### Task 13: Reporte de campaña

**Files:**
- Create: `Assets/Scripts/Game/UI/PanelReporte.cs`
- Create: `Assets/Scripts/Game/UI/Graficos/GraficoEscalera.cs`
- Modify: `Juego.uxml` (`#reporte`)

**Interfaces:**
- Produces:
```csharp
public sealed class GraficoEscalera : VisualElement {   // Painter2D: barra de potencial, un escalón descendente por pérdida, barra del rinde real
    public void Mostrar(Cascada k, double promedioZonaQqHa);  // línea horizontal punteada en el promedio de la zona
}
public sealed class PanelReporte { public PanelReporte(VisualElement raiz, UIJuego ui); public void Abrir(int loteId, int campania); public void Cerrar(); }
```
**Contenido de `#reporte`:**
- `#reporte-titulo`: "Lote 12 · Campaña 1".
- `#reporte-cultivo`: un selector, si la campaña tuvo doble cultivo.
- `#reporte-escalera`:
  - `GraficoEscalera` de 640 × 260 px;
  - etiquetas abajo: Potencial, Fecha, Agua, Anegamiento, Temperatura, Nitrógeno, Fósforo, Plagas, Real;
  - cada escalón con su valor en qq/ha (1 decimal);
  - las pérdidas de 0 se dibujan finitas pero con su etiqueta.
- `#reporte-tabla`:
  - toneladas;
  - precio a cosecha;
  - ingreso bruto, comercialización, costos directos, cosecha;
  - **margen bruto** (US$ y US$/ha);
  - arrendamiento;
  - **resultado**;
  - **rinde de indiferencia** frente al rinde real;
  - promedio de la zona.

  Todo sale de `ReporteLote(lote, campaña)`.
- `#reporte-cerrar`.
- Al cosechar un lote del jugador, la notificación de `Cosecha` es clickeable y abre su reporte.

- [ ] **Step 1: Implementar `GraficoEscalera` y `PanelReporte`.** `read_console` sin errores.
- [ ] **Step 2: Verificar en Play mode.**
  - Con `execute_code`: arrendar el lote 2, planificar maíz para el 1/10 y avanzar con `Sim.StepDay()` en un bucle hasta que haya un `ResultadoCultivo`.
  - Abrir el reporte. Esperado: `#reporte-tabla` con el margen bruto igual a `ReporteLote(2,1).MargenBrutoUsd` formateado, y la escalera visible en una captura.
- [ ] **Step 3: Pruebas manuales.** Leer el reporte y compararlo con el promedio de la zona.

---

### Task 14: Glosario

**Files:**
- Modify: `Assets/Data/glosario.json`
- Create: `Assets/Scripts/Game/UI/Glosario.cs`
- Test: `Assets/Tests/Game/GlosarioTests.cs`

**Interfaces:**
- Produces:
```csharp
public sealed class Termino { public string Id, Nombre, Definicion; }
public sealed class Glosario {
    public static Glosario Desde(string json);                // {"terminos":[{id, nombre, definicion}]}
    public Termino Buscar(string id);
    public void Marcar(VisualElement raiz, Label tooltip);    // a cada elemento con la clase "termino" y userData/name "t-<id>" le agrega hover → tooltip
}
```
- `glosario.json` tiene 16 términos, con definiciones de 1 a 3 oraciones:
  - `ip`
  - `clase_textural`
  - `agua_util`
  - `margen_bruto`
  - `rinde_indiferencia`
  - `resultado`
  - `rinde_potencial`
  - `rinde_esperado`
  - `umbral_danio`
  - `arrendamiento`
  - `silobolsa`
  - `campania`
  - `enso`
  - `grados_dia`
  - `genetica_ciclo`
  - `fosforo_bray`
- En el UXML, cada término marcado es un `Label` con la clase `termino` y `name="t-<id>"`.
- El tooltip (`#tooltip`) sigue al puntero mientras dura el hover. Los tooltips nativos de UITK solo funcionan en el Editor; por eso se implementa así.

- [ ] **Step 1: Escribir el test que falla**

```csharp
[Test] public void GlosarioRealTieneLosTerminosQueUsaLaInterfaz() {
    var g = Glosario.Desde(File.ReadAllText("Assets/Data/glosario.json"));
    foreach (var id in new[] { "ip", "clase_textural", "agua_util", "margen_bruto", "rinde_indiferencia", "umbral_danio" })
        Assert.IsNotEmpty(g.Buscar(id)?.Definicion, id);
    var uxml = File.ReadAllText("Assets/UI/Juego.uxml");
    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(uxml, "name=\"t-([a-z_]+)\""))
        Assert.IsNotNull(g.Buscar(m.Groups[1].Value), m.Groups[1].Value);   // ningún término marcado sin definición
}
```

- [ ] **Step 2: Correr `GlosarioTests`.** Esperado: falla la compilación.
- [ ] **Step 3: Escribir `glosario.json`, implementar `Glosario`** y llamar a `Marcar` en `UIJuego.Start`, también para los paneles que se arman por código.
- [ ] **Step 4: Correr `GlosarioTests`.** Esperado: PASS.
- [ ] **Step 5: Prueba manual.** Pasar el mouse sobre "IP" en la ficha y sobre "Rinde de indiferencia" en la planificación.

---

### Task 15: Menú principal, flujo entre escenas y verificación de punta a punta

**Files:**
- Create: `Assets/UI/Menu.uxml`
- Create: `Assets/Scripts/Game/Menu/MenuPrincipal.cs`
- Modify: `Menu.unity` (UIDocument con `Menu.uxml` y `MenuPrincipal`, con `Fuente` asignada) y `UIJuego.cs` (guardar y volver al menú)

**Interfaces:**
- Produces:
```csharp
public sealed class MenuPrincipal : MonoBehaviour {
    public FuenteDatos Fuente;
    // Start: Sesion.Fuente = Fuente; carga Datos y Geografia en try/catch (DatosInvalidosException → #menu-error y botones deshabilitados)
}
```
**Contenido de `Menu.uxml`:**
- Título "AgroLife" y subtítulo "Pergamino, zona núcleo".
- `#menu-nueva`: semilla = `(ulong)DateTime.Now.Ticks`.
- `#menu-partidas`: una fila por `Guardado.Listar`. Las filas con `Error` van deshabilitadas y muestran el error.
- `#menu-mensaje`: `Sesion.MensajeMenu`, que se limpia después de mostrarse.
- `#menu-error`.
- `#menu-salir`: `Application.Quit()`.

**Flujo:**
- **Nueva partida:** `Sesion.Sim = Simulation.Nueva(...)`, `Sesion.RutaGuardado = Guardado.RutaPara(semilla)`, y carga `Mapa`.
- **Cargar:** `Guardado.Cargar` dentro de un try/catch. `PartidaIncompatibleException` → `#menu-mensaje` con su texto, sin cambiar de escena.
- **En el mapa:**
  - `#btn-guardar` llama a `Guardado.Guardar(Sesion.Sim, Sesion.RutaGuardado)` y avisa por notificación: "Partida guardada".
  - `#btn-menu` pide confirmación en `#dialogo` ("Lo que no guardaste se pierde. ¿Volver al menú?") y carga `Menu`.

- [ ] **Step 1: Implementar el menú y el flujo.** `read_console` sin errores.
- [ ] **Step 2: Verificar de punta a punta en Play mode, desde `Menu`.**
  1. El menú muestra "Nueva partida".
  2. `execute_code` dispara "Nueva partida": la escena activa es `Mapa` y el diálogo de campaña está abierto.
  3. Cerrarlo, arrendar el lote 2, planificar maíz y guardar: el archivo existe en `Guardado.Carpeta`.
  4. Volver al menú: la partida aparece listada con su descripción.
  5. Cargarla: el lote 2 sigue arrendado y con el plan.
  6. Escribir un archivo de partida corrupto en la carpeta y volver al menú: la fila aparece deshabilitada con el error y el menú no se cae (Review Focus 4).
  7. `read_console`: sin errores ni excepciones durante todo el recorrido.
- [ ] **Step 3: Correr todas las suites** (`AgroLife.Sim.Tests` y `AgroLife.Game.Tests`). Esperado: todo PASS.
- [ ] **Step 4: Lista de pruebas manuales para el usuario.** Se escribe en el reporte final y junta las de las Tareas 8 a 14, más una partida libre de dos campañas: arrendar, planificar, cosechar, vender, leer el reporte, guardar y cargar.
