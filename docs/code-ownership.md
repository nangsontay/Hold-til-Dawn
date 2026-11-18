# Ai sửa file nào

Ngày cập nhật: 18/11/2026. Đây là sơ đồ **đang có thật trong repo** sau lần tách file (thẻ I01). Kế hoạch dài hạn vẫn ở [team-boundaries.md](team-boundaries.md).

Quy tắc: **mỗi file chỉ có một người sửa chính**. Cần đổi file của người khác thì nhắn người đó hoặc gửi patch để họ nhận. Trước khi gộp code, chạy `powershell -File tools/Test.ps1 -Godot <Godot-.NET-console.exe>`.

## Cách tách

`GameSim` và `Main` vẫn là **một class**, chỉ chia thành nhiều file bằng `partial class` của C#. Hàm ở file này gọi được hàm ở file kia như cũ, không cần import hay adapter. Hành vi game không đổi: toàn bộ test (geometry, simulation, network) vẫn đạt sau khi tách.

Hằng số `static` khai báo trong file nào thì chỉ được dựa vào hằng số **cùng file** hoặc `T` (`const`). C# không đảm bảo thứ tự khởi tạo giữa các file của một partial class. Ví dụ `DOOR_POS` dùng `HALF` nên cả hai nằm chung trong `src/world/GameSim.World.cs`.

## Pham Minh Hieu — luật chơi, trận đấu, HUD gameplay

| File | Nội dung |
|---|---|
| `GameSim.cs` | API server dùng cho mạng (`AddPlayer`, `RemovePlayer`, `Input`, `Action`, `Tick`, `Snapshot`), hằng số chung (HP, vũ khí, công thức chế tạo), trạng thái trận, bỏ phiếu, chế tạo |
| `src/match/GameSim.Phases.cs` | Ngày / chạng vạng / đêm, thắng thua, trực thăng đêm 5 |
| `src/gameplay/GameSim.Players.cs` | Người chơi: đánh, nhặt, sửa, hồi sinh, hồi máu, vào/ra căn cứ |
| `src/gameplay/GameSim.Towers.cs` | Trụ phòng thủ: đặt, bắn, chiếu sáng |
| `ui/gameplay/Main.GameplayHud.cs` | Bảng tài nguyên, dòng gợi ý thao tác, bảng workbench, phím gameplay (Q, T, V, C, số, F9, chuột phải) |
| `config/gameplay/` | `gameplay.tres`: thời gian tối thiểu trước khi vote, chạng vạng |
| `tests/SimCheck.cs` | Test luật chơi (runner chung, gọi cả phần test bot) |

## Vu Duc Viet Anh — AI, boss, bot, đảo

| File | Nội dung |
|---|---|
| `src/ai/GameSim.Enemies.cs` | Điều phối đợt quái, máy trạng thái quái, boss, đạn Spitter |
| `src/ai/GameSim.Roamers.cs` | Quái lang thang ban ngày |
| `src/ai/GameSim.Drops.cs` | Đồ rơi từ quái |
| `src/ai/Bots.cs` | Bot đồng đội |
| `src/ai/view/Main.EnemyView.cs` | Cách vẽ quái và boss trên client: sprite, màu, thanh máu, thanh máu boss |
| `src/world/GameSim.World.cs` | Kích thước căn cứ, cửa, va chạm tường, tìm đường, vị trí tài nguyên |
| `src/world/WorldGeometry.cs` | Cảnh vật, collider, A*, di chuyển |
| `maps/`, `art/nature/`, `tools/BakeNature.cs` | Bản đồ và cây/đá |
| `config/ai/`, `config/world/` | `ai.tres`: tốc độ quái, nhịp đợt, các hệ số; `world.tres`: số mỏ tài nguyên |
| `tests/ai/SimCheck.Bots.cs`, `tests/GeometryCheck.cs` | Test bot và test hình học |

## Mai Thien Ngon — mạng, giao diện, đồ họa nhân vật

| File | Nội dung |
|---|---|
| `Main.cs` | Menu, phòng chờ, camera, dự đoán di chuyển, vẽ thế giới, khung HUD (gọi các widget ở trên) |
| `src/network/Main.Network.cs` | ENet host/join, RPC, vòng tick server, gửi snapshot |
| `main.tscn`, `project.godot`, `tools/Test.ps1`, `src/GodotInterop.cs`, `src/VariantCollections.cs` | Khung project, runner test, lớp chuyển kiểu Godot |
| `art/chars/`, `tools/PixelChars.cs`, `art/props/`… theo team-boundaries | Hình nhân vật |
| `config/display/` | `display.tres`: vùng chết và độ mượt camera |
| `tests/NetworkCheck.cs` | Test mạng thật giữa hai tiến trình |

## Ranh giới giữa các phần

- **Mạng ↔ luật chơi:** mạng chỉ gọi 6 hàm API trong `GameSim.cs`. Thao tác mới (ví dụ `"craft"`, `"drop"`) chỉ cần thêm trong `GameSim.Action` và phím ở `ui/gameplay`; RPC `CAction` chuyển mọi tên thao tác như nhau.
- **Snapshot:** do Hieu tạo trong `GameSim.Snapshot()`, Ngon và Viet Anh đọc để vẽ. Snapshot hiện là mảng theo vị trí (`p[11]`, `e[5]`): **chỉ thêm phần tử vào cuối mảng** và báo hai người còn lại. Đổi sang trường có tên là việc của thẻ H02.
- **AI ↔ người chơi:** AI chọn mục tiêu và di chuyển. Gây sát thương cho người chơi thì gọi `Hurt()` (của Hieu), không tự trừ HP.
- **Khung HUD ↔ widget:** `Main.DrawHud()` (Ngon) gọi `DrawInventoryPanel`, `DrawInteractionHint`, `DrawCraftPanel` (Hieu) và `DrawBossBar` (Viet Anh). Muốn thêm widget mới thì viết hàm trong file của mình, rồi nhờ Ngon thêm một dòng gọi trong `DrawHud()`.
