# GEMINI CLI Unity Integration Guide

This guide outlines best practices and common pitfalls when using Gemini CLI to interact with Unity Editor via `unity-cli`. These guidelines are designed to be generic and applicable across various Unity projects.

## 왜 만들었나 (Why it was made)
터미널에서 Unity를 제어하고 싶었습니다. 기존 MCP 기반 연동은 Python 런타임, WebSocket 릴레이, JSON-RPC 프로토콜 레이어, 설정 파일, 켜고 꺼야 하는 서버 프로세스, 도구 등록 절차, 수만 줄의 과잉 설계된 코드를 요구했습니다. 단순한 명령 하나 보내는 데 이 모든 게 필요했습니다.

게다가 AI 에이전트마다 MCP 설정과 연동을 따로 해줘야 했습니다. CLI는 그런 게 없습니다 — 셸 명령어를 실행할 수 있는 에이전트라면 바로 쓸 수 있습니다.

이상하다고 느꼈습니다. `curl`로 URL 하나 쏠 수 있는데, 왜 그 모든 게 필요한가?

그래서 정반대로 만들었습니다. Unity에 HTTP로 직접 통신하는 바이너리 하나. 서버를 띄울 필요 없이 — Unity 패키지가 자동으로 수신합니다. 설정 파일 없이 — Unity 인스턴스를 알아서 찾습니다. 도구 등록 없이 — 이름으로 바로 호출합니다. 캐싱도, 프로토콜 레이어도, 절차도 없습니다.

CLI 전체가 Go ~800줄(+ help text ~300줄), Unity 커넥터가 C# ~2,300줄입니다. 셸에서 Unity를 다루게 해주는 아주 얇은 레이어 — 그 본분에 충실합니다. 바이너리 설치하고, Unity 패키지 추가하면 끝입니다.

## Installation

### Linux / macOS

```bash
curl -fsSL https://raw.githubusercontent.com/youngwoocho02/unity-cli/master/install.sh | sh
```

### Windows (PowerShell)

```powershell
irm https://raw.githubusercontent.com/youngwoocho02/unity-cli/master/install.ps1 | iex
```

### Other Methods

```bash
# Go install (Go가 설치된 모든 플랫폼)
go install github.com/youngwoocho02/unity-cli@latest

# 수동 다운로드 (플랫폼 선택)
# Linux amd64 / Linux arm64 / macOS amd64 / macOS arm64 / Windows amd64
curl -fsSL https://github.com/youngwoocho02/unity-cli/releases/latest/download/unity-cli-linux-amd64 -o unity-cli
chmod +x unity-cli && sudo mv unity-cli /usr/local/bin/
```

Supported Platforms: Linux (amd64, arm64), macOS (Intel, Apple Silicon), Windows (amd64).

### Update

```bash
# 최신 버전으로 자동 업데이트
unity-cli update

# 새 버전 확인만
unity-cli update --check
```

## Unity Setup

**Package Manager → Add package from git URL**에서 추가:

```
https://github.com/youngwoocho02/unity-cli.git?path=unity-connector
```

또는 `Packages/manifest.json`에 직접 추가:
```json
"com.youngwoocho02.unity-cli-connector": "https://github.com/youngwoocho02/unity-cli.git?path=unity-connector"
```

To pin a specific version, add a tag to the URL (e.g., `#v0.2.21`).

After adding, open Unity and the connector will start automatically. No separate setup required.

### Recommendation: Disable Editor Throttling

By default, Unity throttles Editor updates when the window loses focus. In this case, CLI commands may not run until Unity is clicked again.

In **Edit → Preferences → General → Interaction Mode**, set to **No Throttling**.

This ensures that CLI commands are processed immediately, even if Unity is in the background.

## Quick Start

```bash
# Check Unity connection
unity-cli status

# Enter play mode and wait
unity-cli editor play --wait

# Execute C# code within Unity
unity-cli exec "return Application.dataPath;"

# Read console logs
unity-cli console --type error,warning,log
```

## How It Works

```
터미널                                Unity Editor
──────                                ────────────
$ unity-cli editor play --wait
    │
    ├─ ~/.unity-cli/instances/*.json 스캔
    │  → Unity가 포트 8090에 있음을 확인
    │
    ├─ POST http://127.0.0.1:8090/command
    │  { "command": "manage_editor",
    │    "params": { "action": "play",
    │                "wait_for_completion": true }}
    │                                      │
    │                                  HttpServer 수신
    │                                      │
    │                                  CommandRouter 디스패치
    │                                      │
    │                                  ManageEditor.HandleCommand()
    │                                  → EditorApplication.isPlaying = true
    │                                  → PlayModeStateChange 대기
    │                                      │
    ├─ JSON 응답 수신  ←──────────────────┘
    │  { "success": true,
    │    "message": "Entered play mode (confirmed)." }
    │
    └─ 출력: Entered play mode (confirmed).
```

Unity Connector Operation:
1. When the Editor starts, it opens an HTTP server on `localhost:8090`.
2. Records project-specific instance files in `~/.unity-cli/instances/` to allow CLI connection.
3. Updates the current status in the instance file every 0.5 seconds (heartbeat).
4. Detects `[UnityCliTool]` classes using reflection on each request.
5. Routes received commands to the corresponding handler on the main thread.
6. Persists even during domain reload (script recompile).

Before compilation or reload, the status (`compiling`, `reloading`) is recorded in the instance file. If the main thread stops, timestamp updates cease, and the CLI waits until a new timestamp is recorded before sending commands.

## Built-in Commands

| Command | Description |
|---|---|
| `editor` | Controls Unity Editor play/stop/pause/refresh |
| `console` | Reads, filters, and clears console logs |
| `exec` | Executes arbitrary C# code within Unity |
| `test` | Runs EditMode/PlayMode tests |
| `menu` | Executes Unity menu items by path |
| `reserialize` | Reserializes assets via Unity serializer |
| `screenshot` | Captures Scene/Game view as PNG |
| `profiler` | Reads profiler hierarchy, controls recording |
| `list` | Displays all available tools and parameter schemas |
| `status` | Checks Unity Editor connection status |
| `update` | Automatically updates CLI binary |

### Detailed Command Usage

#### Editor Control

```bash
# Enter play mode
unity-cli editor play

# Enter play mode and wait until fully loaded
unity-cli editor play --wait

# Exit play mode
unity-cli editor stop

# Toggle pause (only works in play mode)
unity-cli editor pause

# Refresh assets
unity-cli editor refresh

# Refresh + compile scripts (wait until compilation completes)
unity-cli editor refresh --compile
```

#### Console Logs

```bash
# Read error and warning logs (default)
unity-cli console

# Read recent 20 logs of all types
unity-cli console --lines 20 --filter error,warning,log

# Read only errors
unity-cli console --type error

# Include stack trace (user: user code only, full: as is)
unity-cli console --stacktrace user

# Clear console
unity-cli console --clear
```

#### C# Code Execution (`exec`)

This is the most powerful command. It executes arbitrary C# code directly within the Unity Editor runtime. It can access UnityEngine, UnityEditor, ECS, and all loaded assemblies. There is no need to create custom tools for one-off queries or modifications.

It receives results via `return`. Major namespaces are included by default. Add project-specific types (e.g., `Unity.Entities`) with `--usings`.

```bash
unity-cli exec "return Application.dataPath;"
unity-cli exec "return EditorSceneManager.GetActiveScene().name;"
unity-cli exec "return World.All.Count;" --usings Unity.Entities

# Piping via stdin avoids shell escaping issues
echo 'Debug.Log("hello"); return null;' | unity-cli exec
echo 'var go = new GameObject("Marker"); go.tag = "EditorOnly"; return go.name;' | unity-cli exec
```

`exec` actually compiles and executes C#, so it can do anything a custom tool can — inspect ECS entities, modify assets, call internal APIs, run editor utilities. For AI agents, this means **immediate access to the entire Unity runtime without writing a single line of tool code**. Using stdin piping avoids shell escaping issues with complex code.

#### Menu Items (`menu`)

```bash
# Execute Unity menu item by path
unity-cli menu "File/Save Project"
unity-cli menu "Assets/Refresh"
unity-cli menu "Window/General/Console"
```

For safety, `File/Quit` is blocked.

#### Asset Reserialization (`reserialize`)

AI agents (and humans) can directly modify Unity asset files — `.prefab`, `.unity`, `.asset`, `.mat` — as text YAML. However, Unity's YAML serializer is strict: a missing field, incorrect indentation, or an outdated `fileID` can silently break an asset.

`reserialize` solves this. After text modification, execute it and Unity will load the asset into memory and then rewrite it using its own serializer. The result will be a clean, valid YAML file, identical to one modified in the Inspector.

```bash
# Reserialize entire project (without arguments)
unity-cli reserialize

# After modifying Transform values of a prefab as text
unity-cli reserialize Assets/Prefabs/Player.prefab

# After batch modifying multiple scenes
unity-cli reserialize Assets/Scenes/Main.unity Assets/Scenes/Lobby.unity

# After modifying material properties
unity-cli reserialize Assets/Materials/Character.mat
```

This is key to making text-based asset modification safe. Without it, a single misplaced YAML field can lead to prefab corruption that only becomes apparent at runtime. With it, **AI agents can confidently modify any Unity asset as text** — adding components to prefabs, changing scene hierarchy, adjusting material properties — while ensuring the results load correctly.

#### Profiler

```bash
# Read profiler hierarchy (last frame, top level)
unity-cli profiler hierarchy

# Recursive drilldown
unity-cli profiler hierarchy --depth 3

# Specify root by name (substring match) — focus on specific systems
unity-cli profiler hierarchy --root SimulationSystem --depth 3

# Drill down by specific item ID
unity-cli profiler hierarchy --parent 4 --depth 2

# Average over last 30 frames
unity-cli profiler hierarchy --frames 30 --min 0.5

# Average over specific frame range
unity-cli profiler hierarchy --from 100 --to 200

# Filter and sort
unity-cli profiler hierarchy --min 0.5 --sort self --max 10

# Enable/disable profiler recording
unity-cli profiler enable
unity-cli profiler disable

# Check profiler status
unity-cli profiler status

# Clear captured frames
unity-cli profiler clear
```

#### Test Execution

Runs EditMode/PlayMode tests via Unity Test Framework.

```bash
# Run EditMode tests (default)
unity-cli test

# Run PlayMode tests
unity-cli test --mode PlayMode

# Filter by test name (substring match)
unity-cli test --filter MyTestClass
```

Unity Test Framework package is required. PlayMode tests trigger domain reloads, and the CLI automatically polls for results.

## Global Options

| Flag | Description | Default |
|---|---|---|
| `--port <N>` | Directly specifies Unity instance port (skips auto-detection) | auto |
| `--project <path>` | Selects Unity instance by project path | latest |
| `--timeout <ms>` | HTTP request timeout | 120000 |

```bash
# Connect to specific Unity instance
unity-cli --port 8091 editor play

# Select by project path among multiple Unity instances
unity-cli --project MyGame editor stop
```

Use `--help` with any command for detailed usage:

```bash
unity-cli editor --help
unity-cli exec --help
unity-cli profiler --help
```

## Creating Custom Tools

Create a static class with the `[UnityCliTool]` attribute in an Editor assembly. It will be automatically detected upon domain reload.

```csharp
using UnityCliConnector;
using Newtonsoft.Json.Linq;
using UnityEngine;

[UnityCliTool(Name = "spawn", Description = "지정 위치에 적 스폰", Group = "gameplay")]
public static class SpawnEnemy
{
    public class Parameters
    {
        [ToolParameter("X 월드 좌표", Required = true)]
        public float X { get; set; }

        [ToolParameter("Y 월드 좌표", Required = true)]
        public float Y { get; set; }

        [ToolParameter("Z 월드 좌표", Required = true)]
        public float Z { get; set; }

        [ToolParameter("Resources 폴더 내 프리팹 이름", DefaultValue = "Enemy")]
        public string Prefab { get; set; }
    }

    public static object HandleCommand(JObject parameters)
    {
        var p = new ToolParams(parameters);
        float x = p.GetFloat("x", 0);
        float y = p.GetFloat("y", 0);
        float z = p.GetFloat("z", 0);
        string prefabName = p.Get("prefab", "Enemy");

        var prefab = Resources.Load<GameObject>(prefabName);
        var instance = Object.Instantiate(prefab, new Vector3(x, y, z), Quaternion.identity);

        return new SuccessResponse("Enemy spawned", new
        {
            name = instance.name,
            position = new { x, y, z }
        });
    }
}
```

Call using flags or JSON:

```bash
unity-cli spawn --x 1 --y 0 --z 5 --prefab Goblin
unity-cli spawn --params '{"x":1,"y":0,"z":5,"prefab":"Goblin"}'
```

**Key Points:**

- **Name**: If `Name` is not provided, it is automatically generated from the class name (`SpawnEnemy` → `spawn_enemy`, `UITree` → `ui_tree`). If `Name = "spawn"`, it is called as `unity-cli spawn`.
- **Parameters Class**: Optional but recommended. `unity-cli list` displays parameter names, types, descriptions, and required status — allowing AI assistants to understand tool usage without source code.
- **ToolParams**: Consistent parameter reading with `p.Get()`, `p.GetInt()`, `p.GetFloat()`, `p.GetBool()`, `p.GetRaw()`.
- **Detection**: Built-in tools (`group: "built-in"`) are displayed first in `unity-cli list`, followed by custom tools (`group: "custom"`) from the connected project.

**Attribute Reference:**

| Attribute | Property | Description |
|---|---|---|
| `[UnityCliTool]` | `Name` | Overrides command name (default: class name → snake_case) |
| | `Description` | Tool description displayed in `list` |
| | `Group` | Group name for classification |
| `[ToolParameter]` | `Description` | Parameter description (constructor argument) |
| | `Required` | 필수 여부 (기본: `false`) |
| | `Name` | 파라미터 이름 오버라이드 |
| | `DefaultValue` | 기본값 힌트 |

### Rules

- Class must be `static`.
- `public static object HandleCommand(JObject parameters)` or `async Task<object>` variation is required.
- Return `SuccessResponse(message, data)` or `ErrorResponse(message)`.
- Adding `[ToolParameter]` attributes to a `Parameters` nested class provides automatic documentation.
- Class name is automatically converted to snake_case command name.
- Name can be overridden with `[UnityCliTool(Name = "my_name")]`.
- Executed on Unity's main thread, so all Unity APIs can be safely called.
- Automatically detected on Editor startup and after script recompile.
- Duplicate tool names are detected and logged as errors — only the first found handler is used.

## Multiple Unity Instances

If multiple Unity Editors are open, each registers on a different port (8090, 8091, ...):

```bash
# Check all running instances
ls ~/.unity-cli/instances/

# Select by project path
unity-cli --project MyGame editor play

# Select by port
unity-cli --port 8091 editor play

# Default: Use the most recently registered instance
unity-cli editor play
```

## MCP와 비교 (Comparison with MCP)

| | MCP | unity-cli |
|---|-----|-----------|
| **설치** | Python + uv + FastMCP + config JSON | 바이너리 하나 |
| **의존성** | Python 런타임, WebSocket 릴레이 | 없음 |
| **프로토콜** | JSON-RPC 2.0 over stdio + WebSocket | 직접 HTTP POST |
| **설정** | MCP 설정 생성, AI 도구 재시작 | Unity 패키지 추가, 끝 |
| **재연결** | 복잡한 도메인 리로드 재연결 로직 | 요청별 무상태 |
| **호환성** | MCP 호환 클라이언트만 | 셸이 있는 모든 것 |
| **커스텀 도구** | 동일한 `[Attribute]` + `HandleCommand` 패턴 | 동일 |

## 만든 사람 (Creator)

**DevBookOfArray**

[![YouTube](https://img.shields.io/badge/YouTube-DevBookOfArray-red?logo=youtube&logoColor=white)](https://www.youtube.com/@DevBookOfArray)
[![GitHub](https://img.shields.io/badge/GitHub-youngwoocho02-181717?logo=github)](https://github.com/youngwoocho02)

## 라이선스 (License)

MIT
