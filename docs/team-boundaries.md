# Ranh giới code để ba người làm song song

Ngày cập nhật: 18/11/2026. Phân công theo yêu cầu: **Ngon làm server/multiplayer và enhance nhân vật**, **Viet Anh làm AI/world**, **Hieu làm gameplay/audio**.

Đây là kế hoạch tổ chức source và backlog. **Cập nhật 18/11/2026:** lần tách file cơ học (I01) đã làm xong bằng `partial class`; sơ đồ file thật nằm ở [code-ownership.md](code-ownership.md). Các thư mục `scenes/`, `reports/`, contract/port ở mục 4 vẫn là đích đề xuất, chưa tạo.

## 1. Kết quả kiểm tra source hiện tại

| File hiện có | Những việc đang gộp | Task dễ đụng nhau |
|---|---|---|
| `GameSim.cs` | Player/combat/inventory/craft/repair; enemy/waves/boss/drops; ngày/đêm, snapshot và extraction | N01–N16/N24 của Hieu, A01–A20 của Viet Anh, H03/H09/H17/H24 trước đây của Ngon |
| `Main.cs` | ENet, input/prediction, menu/lobby/HUD, camera/vision, vẽ nhân vật/cảnh vật | Ngon network/UI/art, Hieu inventory/hotbar/build, Viet Anh enemy/boss/world |
| `WorldGeometry.cs` | Tạo scenery, collider, query, di chuyển, A* | Hieu cần physics/build; Viet Anh cần map/nav; Ngon cần render |
| `config/PlaytestSettings.cs`, `config/playtest.tres` | Thông số gameplay lẫn enemy/wave/camera | Cả ba có thể sửa một resource |
| `tests/SimCheck.cs` | Test gameplay lẫn director/AI/bot/tower | Mọi feature thêm test vào một file |
| `art/chars/chars.json`, PNG, generator | Metadata và hình cho player lẫn enemy/boss | Art Ngon và logic/animation của hai người còn lại |

**Đổi owner trên Kanban không tự tách các file này.** Cần một PR tách module I01, rồi mới giảm được nguy cơ sửa cùng dòng code. Giữ simulation và behavior baseline trong lần tách; feature mới đi bằng PR sau đó.

## 2. Một owner cho mỗi vùng source

| Owner | Đường dẫn sau khi tách | Phạm vi được sửa |
|---|---|---|
| Ngon | `src/network/**`, `src/server/**` | Lobby/room servers, kết nối, replication, timeout/votes, transport RPC |
| Ngon | `src/presentation/**`, `ui/menu/**`, `ui/lobby/**`, `ui/settings/**`, `ui/timeout/**`, `ui/results/**`, `ui/hud/**` | Trình bày từ snapshot, menu, HUD shell, camera/vision, kết quả; không chứa luật craft/damage |
| Ngon | `src/contracts/**`, `tests/contracts/**`, `docs/contracts/**` | API/schema v1 và fixture tham chiếu; hai người còn lại review khi chốt |
| Ngon | `src/telemetry/**`, `config/network/**`, `config/display/**` | Collector event, thông số mạng và hiển thị |
| Ngon | `art/chars/**`, `tools/PixelChars.cs`, output art/log dưới `assets/ai-log/**` | PNG enhanced, spritesheet metadata/descriptor, character preview |
| Ngon | `Main.cs`, `main.tscn`, `project.godot`, `export_presets.cfg`, `deploy/**`, runner/CI tổng | Composition của client/server, đăng ký scene/autoload, export và vận hành |
| Hieu | `src/gameplay/**`, `scenes/gameplay/**`, `src/match/**`, `GameSim.cs` | Player, inventory, craft/build/repair, damage/HP, vòng ngày/đêm, unlock, extraction/outcome |
| Hieu | `ui/gameplay/**` | Inventory/hotbar/storage/build widgets; cắm vào HUD shell qua slot/signal |
| Hieu | `src/audio/**`, `audio/**` | AudioManager, bus, music/SFX triggers, volume adapter |
| Hieu | `config/gameplay/**`, `config/profiles/gameplay/**`, balance aggregate/loader | Gameplay config và tham chiếu tới resource con; không ghi đè giá trị AI/network |
| Hieu | `art/props/**`, `art/items/**`, `art/story/**`, `tools/PixelProps.cs`, `ui/story/**` | Công trình, icon/item, intro/endings |
| Hieu | `README.md`, tài liệu quản lý chung `docs/**` trừ `docs/contracts/**` | Backlog, hướng dẫn, manifest/credits tổng hợp; không sửa log output art của Ngon |
| Viet Anh | `src/ai/**`, `scenes/ai/**`, `Bots.cs` | Director, enemy/boss FSM, bot policy, enemy/boss movement state |
| Viet Anh | `src/world/**`, `WorldGeometry.cs`, `maps/**`, `art/nature/**`, `tools/BakeNature.cs` | Tilemap/scenery, geometry/query/nav và art thiên nhiên |
| Viet Anh | `src/ai/config/**`, `config/world/**` | Enemy/waves/boss/drop/roamer/world parameters |
| Viet Anh | `assets/ai-log/README.md`, `assets/ai-log/**/original.png`, `assets/ai-log/**/prompt.txt`, `assets/ai-log/input-manifest.*` | Gói nguồn + prompt G01; không sửa `enhanced.png` và output descriptor |
| Theo module | `tests/network/**`, `tests/presentation/**`, `tests/ai/**`, `tests/world/**`, `tests/gameplay/**`, `tests/match/**`, `tests/audio/**` | Test có owner tương ứng; không cùng chèn test vào legacy runner |

Ngoại lệ một lần: I01 do Ngon điều phối có thể di chuyển code trong monolith và tạo adapter. Sau khi merge giao root file về owner như trên. Không để cả ba refactor cùng một root trên ba nhánh.

`tests/SimCheck.cs` legacy do Hieu giữ, `tests/GeometryCheck.cs` legacy do Viet Anh giữ; runner tổng chỉ gọi các suite. Test mới đặt trong thư mục module, không thêm vào hai legacy file theo thói quen.

## 3. Ranh giới trách nhiệm quan trọng

### Network không quyết định luật gameplay

Ngon nhận/gửi command và snapshot, kiểm tra connection/peer/quyền gửi và điều phối room/timeout. Hieu quyết định craft có hợp lệ, mất bao nhiêu HP, ai được repair, có thắng trận chưa. Đổi rule craft không bắt Ngon sửa RPC riêng cho từng recipe.

H03 chỉ còn bootstrap/network lifecycle. Luật ngày/đêm/vote/unlock chuyển sang **M01 của Hieu**; **H17 extraction cũng thuộc Hieu**. UI của Ngon đọc outcome qua snapshot, không tự tính lại thắng/thua.

### AI không trực tiếp sửa implementation HP/inventory

Viet Anh sở hữu quyết định target, state, hướng đi và signature boss. Khi cần gây damage, nhặt đồ, revive hay repair, AI phát intent/GameplayCommand và gọi Combat/Gameplay port; Hieu áp dụng rule/chi phí/HP và phát kết quả. Enemy state/pos/threat do AI quản lý; HP và item transaction có một đường xử lý authoritative.

Điều này không bắt Viet Anh chờ hệ thống combat/inventory thật: FakeCombat/FakeGameplay ghi nhận intent và trả kết quả mẫu để test policy trước.

### World query không biết nội bộ PlayerController hoặc BuildSystem

Viet Anh cung cấp footprint/collision/nav/query và revision. Hieu gửi dynamic obstacle thay đổi khi build/phá/respawn. Ngon lấy scenery/view data để vẽ. Không có việc cả ba cùng thêm một collider vào `WorldGeometry.cs`.

Chốt rõ trong H02 pipeline physics authoritative: ai chạy movement, query trả gì, step order ra sao. Không vừa chạy custom solver vừa chạy physics body lần thứ hai cho cùng một bước mà không kiểm chứng.

### Art không phải chờ gameplay chạy xong

Viet Anh bàn giao từng `original.png` + `prompt.txt`; Ngon làm enhanced art và descriptor. Với player, Hieu gắn action gameplay vào animation; với enemy/boss, Viet Anh gắn AI state vào animation. Renderer của Ngon dùng cùng descriptor, test bằng preview scene với state giả.

- Đầu vào G01 do Viet Anh sửa, đầu ra do Ngon sửa: hai người không chỉnh cùng file.
- Source đã bàn giao được giữ nguyên theo batch/version; đổi prompt đang dùng thì tạo revision/batch tiếp theo, không ghi đè bản mà Ngon đang xử lý.
- File `art/chars/chars.json` chỉ Ngon sửa. Thiếu animation thì bên tích hợp gửi yêu cầu/mapping, không tự đổi số cột/frame trong metadata.
- Boss chưa có code vẫn vẽ/preview theo bảng hành động GDD được. Ngược lại, code boss dùng descriptor/placeholder nên không phải chờ ảnh hoàn thiện.

## 4. Contract tối thiểu cần chốt ở H02

Tên dưới đây là **API đề xuất**, không phải API đã tồn tại trong repo. H02 chọn chữ ký cụ thể và khóa v1 trước khi tích hợp. Tránh thiết kế framework tổng quát vượt nhu cầu game.

| Port / dữ liệu | Bên cung cấp thật | Bên dùng | Mock dùng để làm độc lập |
|---|---|---|---|
| `SimulationPort.begin / apply_command / step / set_frozen / snapshot / drain_events` | Hieu | Server adapter Ngon | Ngon dùng FakeSimulation với snapshot fixture |
| `CommandEnvelope` gồm request ID, actor ID, loại action, payload, schema version | Ngon giữ schema, Hieu áp dụng rule | UI, transport, gameplay và bot | FakeNetwork gửi command vào local simulation |
| `MatchSnapshot` dùng ID/field có tên, schema version | Hieu tổng hợp theo contract | Network/presentation Ngon | JSON/dictionary fixture ngày/đêm/boss/dead/build |
| `CombatPort.apply_hit` và hit result | Hieu | Enemy/boss/tower policy | Viet Anh dùng FakeCombat kiểm tra intent/target/damage |
| `WorldQuery` movement/query/path + `DynamicObstacleChanged` | Viet Anh | Player/build/AI/presentation | Hieu dùng FakeWorld, Viet Anh dùng FakeBody/Obstacle |
| `AIService.prepare_dawn / tick / spawn / export_view` | Viet Anh | Match driver Hieu | Hieu dùng FakeAI không spawn hoặc spawn fixture |
| `BotReplacementPort.take_over(player_state)` | Viet Anh | Timeout adapter Ngon | Ngon dùng FakeBotReplacement; Viet Anh bơm event leaver giả |
| `PlayerState`, `StructureState`, `InventoryState`, `RoomState`, `BossDescriptor` | Mỗi domain giữ dữ liệu của mình, schema do H02 chốt | Các adapter và view | Fixture đủ case, không import class concrete của owner khác |
| `CharacterAnimationDescriptor` | Ngon | Player/AI mapping, renderer | Scene preview với idle/walk/attack/downed/death/telegraph |
| `GameplayWidgetsPort` / các slot inventory-hotbar-storage-build và UI signals | Hieu làm widget, Ngon làm HUD shell | Client presentation | Widget/demo provider giả để mount shell trước, không sửa `Main.cs` từ nhánh gameplay |
| `AudioSettingsPort` + `DomainEvent` | Hieu làm audio adapter, Ngon làm telemetry collector | Settings UI và các module | FakeAudio và event stream giả |

Contract phải chốt ít nhất: tên field/đơn vị, owner được mutate, optional/default, tick/event order, request ID chống lặp, schema version và hành vi khi data thiếu. Không thêm field index tự phát vào mảng snapshot từ ba nhánh.

Không truyền cả object `GameSim` vào mọi module để nó tự sửa dictionary của nhau. Adapter chỉ nhận dữ liệu/context/port đủ dùng; đó mới là giảm coupling code, không chỉ chuyển hàm sang file mới.

## 5. Những phụ thuộc được chuyển thành ghép tích hợp

| Card | Trước đây phải chờ | Cách triển khai trước |
|---|---|---|
| N05/N06/N10 gameplay | H09 replication | Local command/transaction tests, fake transport |
| N14 build | A05 navigation | FakeWorld, xuất footprint/obstacle event đúng schema |
| A04 tower AI | N15 trụ thật | Fake StructureState + CombatPort |
| A07/A12/A14 boss | N02/N12 combat/repair | FakeCombat và scripted attack/repair inputs |
| A13 cloak | H15 fog | Test cloak flag/reveal/telegraph với view fixture; renderer ghép sau |
| A19/A20 bot | H10 vote, N03/N06/N12 | PlayerState và fake leave/inventory/storage/repair |
| H12 Settings | N20 audio | FakeAudioSettingsPort |
| H14/H15/H16 HUD/vision/spectator | N04/N10/N11/N15/N24/H17 | Snapshot/view fixtures |
| N17/A15/A22 enhance | N09/A03/A07 | PNG/prompt và animation descriptor, không cần gameplay/boss hoàn chỉnh |
| H18/H19 story/briefing | H17/A01/A07 | Outcome/DawnBriefing/BossDescriptor fixture |
| H24 telemetry | A01 random director | Stream DomainEvent mẫu, module thật bổ sung emitter khi tích hợp |

Các phần trên vẫn phải ghép thật trước khi nghiệm thu cuối. Fixture chỉ giúp bỏ chờ ở giai đoạn phát triển, không xóa dependency về dữ liệu/chức năng của sản phẩm.

## 6. Cách bàn giao và merge

1. H02 chốt contract/fixture; I01 tách file trong một PR cơ học. Ba người có thể chuẩn bị file mới/asset/mock song song, nhưng tránh sửa monolith cùng lúc.
2. Mỗi owner làm branch/task riêng và chỉ sửa vùng mình giữ. Kiểm tra danh sách file trong diff trước khi đưa review.
3. Khi cần đổi root/contract/file của người khác, bàn giao patch hoặc yêu cầu nhỏ cho owner nhận. Không để ba người cùng sửa `Main.cs`, `GameSim.cs`, `project.godot` hoặc `chars.json` trên ba PR.
4. Code module chạy được offline với fixture riêng, có case normal/edge; đánh dấu **Module ready** để chuyển sang card tiếp, không phải ngồi đợi network/art.
5. Ghép từng lát nhỏ: player + Grunt + một cửa; rồi inventory/structure/bot/vision/rooms. Ngon ghép transport/client; Hieu ghép simulation driver; Viet Anh giữ adapter AI/world của mình.
6. Chỉ đánh dấu **Done** sau acceptance feature thật và tích hợp đã qua. Lỗi liên module được gửi về owner đúng vùng, giữ test tái hiện ở suite liên quan.

Điểm phụ thuộc không thể bỏ: contract chung, batch ảnh đầu vào, và QA end-to-end của multiplayer/gameplay. Mục tiêu là một lần chốt giao diện rồi phát triển song song, không phải ba hệ thống tách rời không tích hợp được.
