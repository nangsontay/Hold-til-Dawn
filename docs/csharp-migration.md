# Chuyển dự án sang C#

Dự án dùng **Godot 4.7.2 .NET** và **.NET 8 SDK**. Mở `HoldTilDawn.sln` trong IDE, chạy `dotnet build`, rồi mở `project.godot` bằng Godot bản .NET. Xem [hướng dẫn C# chính thức](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html) để thiết lập máy mới.

## Các file đã chuyển

| GDScript trước đây | Source C# hiện tại |
|---|---|
| `main.gd` | `Main.cs` + partial files (see [code-ownership.md](code-ownership.md)) |
| `game.gd` | `GameSim.cs` + partial files in `src/match`, `src/gameplay`, `src/ai`, `src/world` |
| `bots.gd` | `src/ai/Bots.cs` |
| `world_geometry.gd` | `src/world/WorldGeometry.cs` |
| `config/playtest.gd` | `config/{gameplay,ai,world,display}/*Settings.cs` |
| `maps/map_prop.gd` | `maps/MapProp.cs` |
| `tests/sim_check.gd` | `tests/SimCheck.cs` |
| `tests/geometry_check.gd` | `tests/GeometryCheck.cs` |
| `tools/pixel_chars.gd` | `tools/PixelChars.cs` |
| `tools/pixel_props.gd` | `tools/PixelProps.cs` |
| `tools/bake_nature.gd` | `tools/BakeNature.cs` |

Scene và resource đã trỏ sang script C#. Thông số chỉnh trong Inspector được chia thành bốn resource theo người phụ trách: `config/gameplay`, `config/ai`, `config/world`, `config/display`. `MapProp` vẫn chạy trong editor, căn sprite theo anchor và cung cấp collider cho simulation.

## Quy ước khi phát triển tiếp

- Simulation vẫn chạy trên server; client giữ input prediction, camera, UI và rendering.
- Method/property C# dùng PascalCase. `TimeLeft` là biến thời gian pha trước đây tên `t`, để phân biệt với hằng `T` (pixel/tile).
- `GArray`, `GDict` và adapter trong `src/GodotInterop.cs` giữ cấu trúc record và snapshot hiện tại. Các record dùng `dynamic`; có thể chuyển từng nhóm sang model có kiểu trong các PR tiếp theo. Adapter chỉ chuyển kiểu dữ liệu và gọi API Godot, không chạy GDScript.
- RPC dùng kiểu Godot hợp lệ và chuyển record sang `Variant` ở ranh giới mạng. Tên method RPC đã đổi theo C#; mọi peer cần chạy cùng bản C# này.
- Chỉnh source `.cs` trực tiếp. Công cụ build không cần trình chuyển đổi hay Python.

## Kiểm tra

Chạy `powershell -File tools/Test.ps1 -Godot <Godot-.NET-console.exe>`. Runner build C#, kiểm tra geometry, simulation và giao thức mạng thật giữa hai tiến trình localhost qua UDP 17777. Có thể truyền `-Dotnet <dotnet.exe>` hoặc `-SkipBuild`.

Các suite riêng:

```text
godot --headless --path . tests/GeometryCheck.tscn
godot --headless --path . tests/SimCheck.tscn
```

Simulation có thể chạy riêng hai nhóm bằng `-- --progression-only` hoặc `-- --mechanics-only`; mặc định chạy cả hai. Các bài test tiến trình trận mô phỏng hàng trăm giây ở bước 1/60 giây nên cần vài phút.

Chạy công cụ art bằng scene `.tscn`, không dùng cờ `-s` dành cho runner GDScript. Lệnh cụ thể có trong README. PNG/JSON đã có trong `art/` tiếp tục được dùng trực tiếp khi chơi.
