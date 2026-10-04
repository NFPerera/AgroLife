# AgroLife

Juego y simulador agropecuario en Unity 2D, con fines educativos. Hablar en español rioplatense.

## Estado
- El diseño de la v1 está aprobado: [docs/superpowers/specs/2026-10-04-agrolife-v1-design.md](docs/superpowers/specs/2026-10-04-agrolife-v1-design.md). Leerlo antes de cualquier cosa.
- **Siguiente paso:** escribir el plan de implementación con el skill `superpowers:writing-plans`, a partir del spec.
- **No commitear nada** salvo que el usuario lo pida explícitamente, aunque un skill indique commitear.

## Estructura
- `AgroLife/`: proyecto de Unity 6.3 LTS (6000.3.14f1), URP. Las rutas de Unity son relativas a `AgroLife/Assets/`.
- `docs/`: specs y planes.
- `tools/data-pipeline/`: scripts Python del pipeline de datos (todavía no existe).

## Control del Editor de Unity (MCP for Unity)
El proyecto tiene el paquete `com.coplaydev.unity-mcp`. Su servidor está registrado en Claude como `UnityMCP` (HTTP, `http://127.0.0.1:8080/mcp`).

- **Para usarlo hacen falta dos cosas:**
  1. Que el Editor esté abierto.
  2. Que el usuario haya arrancado el servidor desde **Window → MCP for Unity → Start Server**.
- **Si no aparecen herramientas `mcp__UnityMCP__*`** en la sesión, el servidor no estaba corriendo cuando arrancó la sesión. Pedirle al usuario que lo arranque y reconecte `UnityMCP` desde `/mcp`.
- **Alternativa sin herramientas cargadas:** hablarle al endpoint por JSON-RPC sobre HTTP.
  - Pasos: `initialize` → guardar el header `mcp-session-id` → `notifications/initialized` → `tools/call`.
  - Las respuestas llegan como líneas SSE `data: {...}`.
  - En Windows, usar `PYTHONIOENCODING=utf-8` para imprimir la salida.
  - Ejemplo probado: `tools/call` con `{"name":"manage_gameobject","arguments":{"action":"create","name":"Carlos","primitive_type":"Cube"}}`.
- **Preferir siempre el MCP** antes que editar a mano los YAML de `.unity`, `.prefab` o `.asset` mientras el Editor está abierto.
- Después de crear o modificar scripts, revisar errores de compilación con `read_console`.
