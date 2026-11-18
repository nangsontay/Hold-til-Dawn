# Hold ’til Dawn — Backlog Kanban theo GDD v1.0

Ngày lập: 18/11/2026. Nguồn: `.tmp/GDD/ver1.pdf`, bản baseline cập nhật 11/11/2026, 88 trang; đặc biệt bảng 7.3–7.8, MEC-01–29, BAL-01–48, FML-01–16 và các bảng art/audio.

Đây là backlog từ **repo hiện tại đến nghiệm thu GDD**, không yêu cầu viết lại mọi thứ đã có. Mỗi card bao gồm bổ sung/sửa phần thiếu và kiểm chứng phần đang chạy. Chưa thay đổi gameplay hoặc tạo card trên dịch vụ Kanban.

## Cách đưa lên board

- Cột đề xuất: **Backlog → Ready → In progress → Review/Test → Done**; thêm nhãn **Blocked** khi thiếu phụ thuộc.
- Đặt title card là `[ID] Tên task`; copy mục GDD, phụ thuộc và điều kiện hoàn thành vào description.
- Phân công đã điều chỉnh theo yêu cầu người dùng: **Ngon ưu tiên tạo server, multiplayer và enhance đồ họa nhân vật**; Hieu tiếp nhận gameplay/physics/audio; Viet Anh giữ AI/boss/map và chuẩn bị PNG gốc/prompt. Phân công này thay đề xuất ban đầu theo bảng 7.6; yêu cầu nghiệm thu GDD giữ nguyên. Mỗi card có một owner; card Q cần cả nhóm.
- Giữ nguyên ID N/A/H/Q để không hỏng card/liên kết đã copy. Tiền tố là nhóm công việc lịch sử, **không còn là ký hiệu tên người**; đọc trường Owner. G01 là task gói bàn giao ảnh/prompt mới.
- **P0:** nền tảng, lỗi luật quan trọng hoặc phụ thuộc chặn task khác. **P1:** vẫn là phần bắt buộc để nghiệm thu MVP/nộp bài, không phải optional.
- Mỗi card tách **Phụ thuộc triển khai** (thực sự phải có để viết module) và **Ghép tích hợp / nghiệm thu** (các feature thật phải hoạt động cùng nhau trước khi Done). Sau contract H02 dùng fixture/mock để làm song song; test module đạt chưa đồng nghĩa acceptance cuối đạt. Đường tích hợp không phải lệnh bắt hai người chờ nhau trước khi viết code.
- WIP đề xuất: mỗi người một card đang làm; chia nhỏ card lớn thành checklist/subtask nếu cần. Đếm card không phải ước lượng công sức; riêng boss/online cần nhiều thời gian hơn một card sửa luật.
- Có thể gắn nhãn **Module ready** khi code/test cục bộ đã qua để owner làm card tiếp. Chỉ kéo **Done** sau review và nghiệm thu tích hợp trên feature thật; mock không được dùng để thay phần production còn thiếu.

## Phân công tổng thể

**Quy tắc mới: một file chỉ có một người giữ quyền sửa chính.** Module được chia theo [team-boundaries.md](team-boundaries.md). Đây là kế hoạch tách code; lượt cập nhật backlog này chưa refactor source.

| Người | Phạm vi chính | Số card |
|---|---|---:|
| Mai Thien Ngon | Server/multiplayer, transport, UI/presentation và enhance nhân vật | 26 |
| Vu Duc Viet Anh | AI/boss/bot, world/navigation, gói PNG gốc + prompt | 26 |
| Pham Minh Hieu | Luật gameplay/match/extraction, inventory/build, audio và tài liệu | 31 |

**Tổng: 83 card**: giữ 81 ID cũ, thêm I01 (tách file một lần) và M01 (luật match tách khỏi network). H17 chuyển cho Hieu vì là luật extraction trong mô phỏng. Không cắt yêu cầu GDD.

Quan hệ chờ khác owner trong backlog cũ: **94 cạnh**; sau tách: **48 cạnh triển khai** (có cả QA). Đây là số liên kết card, không phải thước đo mức độc lập của code hay % công việc đã xong. Các phụ thuộc tích hợp thực vẫn được giữ trong từng card; source hiện tại chỉ bớt va chạm sau I01.

## Điểm đụng code đã xác nhận

| Hiện tại | Nguy cơ | Cách chia mới |
|---|---|---|
| GameSim.cs gộp player/inventory/craft/repair + AI/waves + phases/extraction | Hieu, Viet Anh và Ngon cùng sửa một file | Hieu giữ router và match/gameplay; AI gọi qua port riêng; Ngon chỉ transport/bootstrap |
| Main.cs gộp network/UI/draw/input | Cả ba dễ sửa renderer/HUD chung | Ngon giữ main/presentation; Hieu làm widget gameplay riêng; Viet Anh gửi state/descriptor |
| WorldGeometry.cs có collision/nav/cảnh vật | Hieu build/player và Viet Anh nav sửa chéo | Viet Anh sở hữu world query; Hieu gửi dynamic obstacle/footprint qua API |
| config/playtest.* và tests/SimCheck.cs là file chung | Tuning/test của ai cũng chạm | Resource con và thư mục test riêng theo owner, aggregate/runner có một owner |
| art/chars/chars.json, PNG và state animation | Art/AI/gameplay cùng sửa metadata/ảnh | Ngon sở hữu output+descriptor; Viet Anh chỉ input/prompt và AI mapping, Hieu player mapping |

## Thứ tự bắt đầu để ít chờ nhau

1. **H02:** Ngon chốt contract/fixture v1 cùng hai người còn lại; H01 tài liệu baseline làm song song, không giữ tất cả task chờ viết xong tài liệu.
2. **I01:** một PR tách file cơ học do Ngon điều phối, không đổi luật. Chốt root owner sau merge. Trước đó ai viết module mới/fixture thì làm trong thư mục riêng, không cùng sửa Main.cs/GameSim.cs.
3. **Ngon:** H04/H05/H06/H09 với FakeSimulation; UI H12/H14/H15 với snapshot mẫu; enhance N17/A15/A22 từ từng batch G01.
4. **Viet Anh:** G01/A16/A21 bắt đầu ngay; A01/A03/A04/A07 với FakeCombat/FakeWorld/StructureState, không chờ inventory/trụ/vote thật.
5. **Hieu:** N19/N18/H18 chuẩn bị ngay; N03/N07/N10/N12/N24/M01 với FakeNetwork/FakeAI/FakeWorld, không chờ ENet, boss hoặc nav hoàn tất.
6. **Ghép theo lát nhỏ:** một player + một Grunt + một cửa trước; sau đó inventory/structure/bot/vision và nhiều phòng. Owner root nhận adapter, không để ba người merge trực tiếp vào monolith.

Khi giao việc trên board, ghi cả đường dẫn module và owner. Giữ ID cũ dù người giữ card thay đổi. Q01–Q06 vẫn cần nghiệm thu trên hệ thống thật, không bỏ vì muốn giảm dependency.

## Những gì hiện có để kế thừa

Đã có mô phỏng ngày/đêm, ba role, combat cận chiến, resource nodes/crafting một phần, cửa/base/trụ, quái thường/roamers, boss slam thử nghiệm, downed/revive, logic extraction, menu/lobby/HUD, ENet một match, collision/pathfinding tự viết và cấu hình demo. Không tạo task “làm mới” các phần này khi chỉ cần hoàn thiện/kiểm chứng.

Lần audit gần nhất: `tests/GeometryCheck.cs` đạt 20/20; `tests/SimCheck.cs` đạt 87/88, còn fail bot farm ban ngày. Kiểm tra riêng xác nhận lỗi hai Engineer repair cộng dồn, vừa đánh vừa revive, sửa base ban đêm và Revival kit luôn về A. Các test này là baseline của repo, **không chứng minh mọi acceptance GDD đã đạt**.

## G01 — Quy ước gói asset giao cho Ngon

Đây là **task chuẩn bị gói bàn giao**, chưa phải yêu cầu chạy AI hoặc thay ảnh trong game ngay. **Viet Anh chuẩn bị PNG/prompt → Ngon enhance → tích hợp cùng owner gameplay/AI.** Có thể bàn giao từng batch.

```text
assets/ai-log/
  README.md                     # manifest: mã GDD, nguồn, ưu tiên, trạng thái
  characters/
    engineer/
      original.png              # bản sao ảnh gốc, giữ nguyên
      prompt.txt                # yêu cầu sửa riêng Engineer
    scout/
      original.png
      prompt.txt
    healer/
      original.png
      prompt.txt
  enemies/
    grunt/
      original.png
      prompt.txt
    spitter/
      original.png
      prompt.txt
    wrecker/
      original.png
      prompt.txt
  bosses/
    juggernaut/
      original.png              # có thể là tham chiếu/placeholder được ghi rõ
      prompt.txt
    ...                         # đủ bảy boss
```

- Mỗi asset một folder riêng, tối thiểu **PNG gốc + TXT prompt**, nằm trong vùng Git theo dõi; không dùng `.tmp` làm gói bàn giao chính.
- PNG gốc là ảnh thật, không phải `.png.import`; không sửa đè. Với boss chưa có hình riêng, ghi rõ ảnh tham chiếu/placeholder và nguồn thực tế, không gọi đó là concept đã duyệt.
- Prompt riêng từng asset, không chỉ “enhance this image”. Ghi vai trò, silhouette, trang bị, màu và đặc điểm phân biệt bảy boss.
- Sau khi Ngon chạy AI, thêm `enhanced.png` và log tool/version/ngày/prompt thực dùng trong cùng folder. Chỉ đưa vào `art/` sau review. Output không phải điều kiện **G01** Done; nó thuộc N17/A15/A22.
- Đây là spritesheet, không chỉ một portrait. Ghi rõ frame size, cột/dòng, hướng, animation, transparency và anchor. Đổi layout/kích thước phải có mô tả layout đích và bàn giao cập nhật metadata/code.
- Hieu cung cấp nhu cầu player/animation, Viet Anh cung cấp nhu cầu enemy/boss. Ngon là owner enhance và chất lượng đồ họa nhân vật.

Mẫu `characters/engineer/prompt.txt` để người chuẩn bị điều chỉnh:

```text
Asset: Engineer — CHR-03 / ART-01
Nguồn PNG: res://art/chars/engineer.png
Nguồn/licence/quy trình: ghi theo art/chars/CREDITS.txt; không tự gán CC0.

Mục tiêu:
Nâng độ rõ và chi tiết của kỹ sư theo phong cách pixel art hiện tại.
Áo hi-vis/mũ bảo hộ cam #FF7A1A; dáng nặng nhất trong ba role.
Búa/công cụ và túi đồ rõ; outline tối, shading giới hạn, theo palette GDD.
Không thêm chữ, watermark hoặc nền đặc.

Ràng buộc đầu vào:
Spritesheet 192x128 px, 6 cột x 4 dòng; mỗi frame 32x32 px.
Dòng: right, down, left, up. Cột: idle 0–1, walk 2–5.
Anchor trong frame (16, 29); nền alpha trong suốt.
Batch chỉ enhance: giữ kích thước, vị trí và số frame;
màu/trang bị nhất quán giữa các frame và hướng.

Nếu bổ sung attack/downed/death:
Ghi riêng layout đích, kích thước, số frame/hướng cho từng action.
Phối hợp người tích hợp để cập nhật chars.json/code; không tự đổi layout.

Đầu ra:
Lưu enhanced.png riêng, không ghi đè original.png.
Review ở kích thước pixel gốc và trong game trước khi nhận.
```

## Mai Thien Ngon — Server / Multiplayer / Presentation / Enhance

| ID | Ưu tiên | Task | Owner |
|---|---|---|---|
| [H02](#h02) | P0 | Chốt contract v1 và fixture chung cho ba module | Mai Thien Ngon |
| [I01](#i01) | P0 | Tách monolith và giao file về một owner trước khi sửa song song | Mai Thien Ngon |
| [A15](#a15) | P1 | Enhance bảy bộ hình và animation boss | Mai Thien Ngon |
| [A22](#a22) | P1 | Enhance sprite quái thường và feedback | Mai Thien Ngon |
| [H03](#h03) | P0 | Network bootstrap, client lifecycle và nối adapter mô phỏng | Mai Thien Ngon |
| [H04](#h04) | P0 | Dedicated server: chế độ lobby và room | Mai Thien Ngon |
| [H05](#h05) | P0 | Room lifecycle và giới hạn ba phòng | Mai Thien Ngon |
| [H06](#h06) | P0 | Tự kết nối lobby bằng DNS và hỗ trợ LAN override | Mai Thien Ngon |
| [H07](#h07) | P0 | Giao diện danh sách phòng và create/join | Mai Thien Ngon |
| [H08](#h08) | P0 | Lobby Ready và điều kiện start | Mai Thien Ngon |
| [H09](#h09) | P0 | Replication và action authority theo TECH-NET | Mai Thien Ngon |
| [H10](#h10) | P0 | Technical timeout và state machine bỏ phiếu | Mai Thien Ngon |
| [H11](#h11) | P1 | UI technical timeout và bot takeover | Mai Thien Ngon |
| [H12](#h12) | P1 | Main menu, Settings, Credits và lưu tùy chọn | Mai Thien Ngon |
| [H13](#h13) | P1 | Pause overlay và leave flow | Mai Thien Ngon |
| [H14](#h14) | P1 | Hoàn thiện 13 thành phần HUD | Mai Thien Ngon |
| [H15](#h15) | P0 | Tầm nhìn đêm thực sự giấu quái | Mai Thien Ngon |
| [H16](#h16) | P1 | Spectator camera cho người đã chết | Mai Thien Ngon |
| [H19](#h19) | P1 | Dawn briefing, boss card và tutorial ngày 1 | Mai Thien Ngon |
| [H20](#h20) | P1 | Result screen và quay về đúng room lobby | Mai Thien Ngon |
| [H21](#h21) | P0 | Export presets và build liên nền tảng | Mai Thien Ngon |
| [H22](#h22) | P1 | Triển khai lobby/rooms trên máy chủ và DNS | Mai Thien Ngon |
| [H24](#h24) | P0 | Telemetry một log cho mỗi match | Mai Thien Ngon |
| [H26](#h26) | P1 | Chuẩn hóa hiển thị và hiệu năng client | Mai Thien Ngon |
| [N17](#n17) | P1 | Enhance đồ họa và hoàn thiện sprite ba vai trò | Mai Thien Ngon |

<a id="h02"></a>

### H02 — Chốt contract v1 và fixture chung cho ba module

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-ENG, TECH-NET, bảng 7.2
- **File / module sở hữu:** src/contracts/**; tests/contracts/**; docs/contracts/** — Ngon giữ contract v1.
- **Phụ thuộc triển khai:** —
- **Ghép tích hợp / nghiệm thu:** H01
- **Điều kiện hoàn thành:** Chốt schema/API nhỏ theo docs/team-boundaries.md: PlayerState/EnemyState/StructureState/RoomState, command/event/snapshot, SimulationPort/CombatPort/WorldQuery/BotReplacementPort, animation descriptor và version. Kèm fixture/FakeSimulation/FakeCombat/FakeWorld tối thiểu và test contract để từng người chạy module offline. Ghi chủ sở hữu field, nội dung tick/event và thứ tự xử lý; không yêu cầu hoàn thành H01 hoặc toàn bộ hệ thống thật. Các bên review contract một lần; thay đổi phá vỡ tương thích cần cập nhật fixture/adapter cùng PR.

<a id="i01"></a>

### I01 — Tách monolith và giao file về một owner trước khi sửa song song

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — chưa triển khai
- **GDD:** Giảm xung đột triển khai TECH-ENG/TECH-PHY/TECH-AI/TECH-NET
- **File / module sở hữu:** Một lần: Main.cs, GameSim.cs và adapter/runner/khung module; sau merge giao về owner trong team-boundaries.md.
- **Phụ thuộc triển khai:** H02
- **Ghép tích hợp / nghiệm thu:** H02
- **Điều kiện hoàn thành:** Một PR cơ học theo docs/team-boundaries.md: trích gameplay/match, AI/world, network/UI ra thư mục riêng và giữ API/behavior baseline. Chuẩn bị adapter/fixture/test runner và resource con có owner. Ngon điều phối lần tách chung này, Hieu/Viet Anh review; không trộn feature mới. Sau merge, GameSim.cs về Hieu, Main.cs/project.godot về Ngon, WorldGeometry.cs/Bots.cs về Viet Anh. Các task có thể viết file mới trên fixture trước, nhưng không cùng sửa monolith trong lúc PR tách file đang chạy. Regression baseline được giữ và lỗi bot farm đã biết tiếp tục được ghi nhận, không làm biến mất test fail.

<a id="a15"></a>

### A15 — Enhance bảy bộ hình và animation boss

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** ART-03, CHR-08, bảng 6.1
- **File / module sở hữu:** art/chars/**; art/chars/chars.json; tools/PixelChars.cs; assets/ai-log/**/enhanced.png + output-log.*; src/presentation/characters/**.
- **Phụ thuộc triển khai:** G01
- **Ghép tích hợp / nghiệm thu:** H01, A07, G01, I01
- **Điều kiện hoàn thành:** Mỗi archetype có silhouette khác nhau và idle/move/telegraph/signature/death, kích thước 48–64 px theo hướng dẫn. Tích hợp vào A08–A14, marker khớp hitbox/thời gian; palette/anchor/licence/AI disclosure đầy đủ, không dùng cùng Wrecker tô màu thay tất cả. Ngon dùng AI đồ họa trên gói original.png + prompt.txt của G01; giữ nguyên PNG gốc, lưu enhanced.png và thông tin tool/version/ngày/prompt thực dùng, review từng frame rồi mới tích hợp. Hieu phối hợp animation player; Viet Anh phối hợp state/hitbox enemy và boss. Không chờ A07/AI boss chạy xong: dùng animation descriptor và preview state từ H02. Viet Anh chỉ tích hợp mapping state→animation trong src/ai; không sửa PNG/chars.json của Ngon.

<a id="a22"></a>

### A22 — Enhance sprite quái thường và feedback

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** ART-02, CHR-05–07, ART-07
- **File / module sở hữu:** art/chars/**; art/chars/chars.json; tools/PixelChars.cs; assets/ai-log/**/enhanced.png + output-log.*; src/presentation/characters/**.
- **Phụ thuộc triển khai:** G01
- **Ghép tích hợp / nghiệm thu:** A03, H01, G01, I01
- **Điều kiện hoàn thành:** Ba sheet nhận ra rõ Grunt/Spitter/Wrecker; Wrecker 1.25 ô, Spitter cue acid, palette NOX và chuyển động bốn hướng. Hit/death/telegraph/projectile đúng hướng dẫn, không gore; bàn giao event âm thanh cho N22, không để animation che marker. Ngon dùng AI đồ họa trên gói original.png + prompt.txt của G01; giữ nguyên PNG gốc, lưu enhanced.png và thông tin tool/version/ngày/prompt thực dùng, review từng frame rồi mới tích hợp. Hieu phối hợp animation player; Viet Anh phối hợp state/hitbox enemy và boss. Không chờ A03 stats/AI hoàn tất: dùng descriptor và fixture state. Ngon sở hữu file art đầu ra, Viet Anh sở hữu code enemy state/attack.

<a id="h03"></a>

### H03 — Network bootstrap, client lifecycle và nối adapter mô phỏng

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-ENG, bảng 7.2
- **File / module sở hữu:** src/network/bootstrap/**; Main.cs/main.tscn/project.godot — chỉ Ngon.
- **Phụ thuộc triển khai:** H02, I01
- **Ghép tích hợp / nghiệm thu:** H02, I01, M01
- **Điều kiện hoàn thành:** Chỉ triển khai bootstrap network/server/client, lifecycle kết nối/room, signals và nối SimulationPort qua adapter. Ngon không viết luật ngày/đêm, unlock hoặc extraction trong card này: các luật đó thuộc M01/H17 do Hieu phụ trách. Dùng FakeSimulation để phát triển/test server mà chưa cần gameplay hoàn chỉnh; kết nối Simulation thật do owner GameSim.cs nhận adapter trong bước tích hợp.

<a id="h04"></a>

### H04 — Dedicated server: chế độ lobby và room

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-ENG, TECH-NET, BAL-48
- **File / module sở hữu:** src/network/**; src/server/**; config/network/**; tests/network/**.
- **Phụ thuộc triển khai:** H03
- **Ghép tích hợp / nghiệm thu:** H03, I01
- **Điều kiện hoàn thành:** Headless chạy theo launch option, lobby UDP 7777, room nhận port 7778–7780 do lobby cấp. Lobby khởi tạo/theo dõi/đóng room servers, không phụ thuộc một player host; cùng source hỗ trợ server Windows/macOS/Linux; lỗi process/port có thông báo và cleanup.

<a id="h05"></a>

### H05 — Room lifecycle và giới hạn ba phòng

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-29, BAL-48
- **File / module sở hữu:** src/network/**; src/server/**; config/network/**; tests/network/**.
- **Phụ thuộc triển khai:** H04
- **Ghép tích hợp / nghiệm thu:** H04, I01
- **Điều kiện hoàn thành:** Create/join/status/leave đầy đủ; tên room từ leader tối đa 16 ký tự, tối đa ba room; từ chối room thứ tư, full hoặc đang chơi không cho join. Room list cập nhật ≤1 s khi đổi trạng thái; room rỗng đóng sau 60 s; sau trận vẫn giữ room/lobby.

<a id="h06"></a>

### H06 — Tự kết nối lobby bằng DNS và hỗ trợ LAN override

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-29, BAL-48, TECH-NET
- **File / module sở hữu:** src/network/**; src/server/**; config/network/**; tests/network/**.
- **Phụ thuộc triển khai:** H04
- **Ghép tích hợp / nghiệm thu:** H04, I01
- **Điều kiện hoàn thành:** Play dùng DNS built-in, không yêu cầu người chơi nhập IP; có --server= cho test LAN, xử lý port/timeout đúng. Room list xuất hiện trong ba giây theo acceptance; lỗi sau năm giây cho Server offline + Retry; không làm lộ thông tin truy cập máy chủ.

<a id="h07"></a>

### H07 — Giao diện danh sách phòng và create/join

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-29, FTR-03, hình 6.4
- **File / module sở hữu:** ui/{menu,lobby,settings,timeout,results}/** theo card; tests/presentation/** — Ngon.
- **Phụ thuộc triển khai:** H05, H06
- **Ghép tích hợp / nghiệm thu:** H05, H06, I01
- **Điều kiện hoàn thành:** Có tên room, số người, mode, state, Join/Create/Retry; refresh ≤1 s; nút full/playing/cap bị vô hiệu hóa đúng lý do. Vào room và Leave room quay lại list; thao tác bàn phím/chuột được, xử lý room biến mất trong lúc join.

<a id="h08"></a>

### H08 — Lobby Ready và điều kiện start

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-24, BAL-01, BAL-45
- **File / module sở hữu:** src/network/**; src/server/**; config/network/**; tests/network/**.
- **Phụ thuộc triển khai:** H05, H09
- **Ghép tích hợp / nghiệm thu:** H05, H09, I01
- **Điều kiện hoàn thành:** Mỗi người có Ready; đổi role hoặc mode xóa mọi Ready; chỉ leader được đổi mode/start. Start cần 3–5 người, tất cả Ready và ≥1 Engineer; role được trùng. Leader rời thì chuyển cho người tiếp theo; UI nêu đúng lý do bị khóa, server vẫn kiểm tra độc lập.

<a id="h09"></a>

### H09 — Replication và action authority theo TECH-NET

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-NET, TECH-ENG
- **File / module sở hữu:** src/network/**; src/server/**; config/network/**; tests/network/**.
- **Phụ thuộc triển khai:** H02, H03
- **Ghép tích hợp / nghiệm thu:** H02, H03, I01
- **Điều kiện hoàn thành:** Tích hợp MultiplayerSpawner/MultiplayerSynchronizer theo GDD, snapshot/interpolation 20 Hz; server giữ damage, inventory, cửa, quái, timer. Đồng bộ seed/config/entity lifecycle, RPC có kiểm tra điều kiện, không duplicate action/drop. Kế thừa prediction/ack đang có và chứng minh 5 client nhất quán.

<a id="h10"></a>

### H10 — Technical timeout và state machine bỏ phiếu

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-27, BAL-46
- **File / module sở hữu:** src/network/**; src/server/**; config/network/**; tests/network/**.
- **Phụ thuộc triển khai:** H05, H09
- **Ghép tích hợp / nghiệm thu:** H05, H09, I01
- **Điều kiện hoàn thành:** Một người bất kỳ rời, kể cả leader: trong một giây đóng băng timer/quái/projectile/channel; bước Continue/End ≤30 s, rồi Bot/No bot ≤30 s. Đa số phiếu đã cast, tie/no vote → Continue/No bot, resume countdown 3 s. Gộp nhiều leaver cùng timeout; chuyển leader, End không thắng/thua; không bot thì drop inventory/remove, không còn người thì kết thúc.

<a id="h11"></a>

### H11 — UI technical timeout và bot takeover

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-27, FTR-03
- **File / module sở hữu:** ui/{menu,lobby,settings,timeout,results}/** theo card; tests/presentation/** — Ngon.
- **Phụ thuộc triển khai:** H10
- **Ghép tích hợp / nghiệm thu:** H10, A19, N05, I01
- **Điều kiện hoàn thành:** Panel hiện tên leaver, bước/phiếu/thời gian, đổi vote; màn hình frozen đúng, cảnh báo No Engineer left, BOT tag và resume countdown. Đồng bộ cho mọi client, người rời giữa timeout không làm vote treo; không cho thao tác gameplay trong lúc freeze.

<a id="h12"></a>

### H12 — Main menu, Settings, Credits và lưu tùy chọn

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** FTR-03, mục 6.2–6.3
- **File / module sở hữu:** ui/{menu,lobby,settings,timeout,results}/** theo card; tests/presentation/** — Ngon.
- **Phụ thuộc triển khai:** H03
- **Ghép tích hợp / nghiệm thu:** H03, N20, I01
- **Điều kiện hoàn thành:** Menu Play/Settings/Credits/Quit; tên player, master/music/effects volume, window mode, tắt camera shake; lưu local qua lần mở game. Mọi menu dùng được chuột/bàn phím/trackpad; style, font và pixel scale theo baseline hoặc CHG đã chốt. Dùng AudioSettingsPort/FakeAudio cho UI; không chờ file nhạc hay AudioManager thật N20 để triển khai menu.

<a id="h13"></a>

### H13 — Pause overlay và leave flow

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** FTR-03, bảng 2.1, hình 6.2
- **File / module sở hữu:** ui/{menu,lobby,settings,timeout,results}/** theo card; tests/presentation/** — Ngon.
- **Phụ thuộc triển khai:** H10, H12
- **Ghép tích hợp / nghiệm thu:** H10, H12, I01
- **Điều kiện hoàn thành:** Esc mở overlay, không tự rời ngay và không pause trận online; có reminder the night goes on, settings/leave/đóng overlay. Leave đi qua flow timeout cho người còn lại; không thêm NOX file viewer thành điều kiện MVP.

<a id="h14"></a>

### H14 — Hoàn thiện 13 thành phần HUD

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** Bảng 6.2, MEC-19, MEC-21–22
- **File / module sở hữu:** src/presentation/**; ui/hud/**; config/display/**; tests/presentation/**. Không sửa widget ui/gameplay của Hieu.
- **Phụ thuộc triển khai:** H02
- **Ghép tích hợp / nghiệm thu:** H02, N04, N10, N15, H17, I01
- **Điều kiện hoàn thành:** Đủ 13 mục: pha/timer/next wave/vote, generator, door+state+tower, radio, boss, own status/equipped, team/downed/BOT, hotbar/count/flask, resources, interaction progress, edge arrows kèm distance/timer, pad occupancy/hold, centre warnings/tutorial. UI phản ánh snapshot và vẫn đọc được khi mục tiêu ngoài tầm nhìn. Ngon dựng HUD từ fixture/snapshot schema; inventory/hotbar/storage/build widgets do Hieu viết trong ui/gameplay, Ngon chỉ mount qua slot/signal, không sửa nội bộ widgets. Không chờ các luật N04/N10/N15/H17 hoàn tất mới dựng/test UI.

<a id="h15"></a>

### H15 — Tầm nhìn đêm thực sự giấu quái

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-21, BAL-12, FML-12
- **File / module sở hữu:** src/presentation/**; ui/hud/**; config/display/**; tests/presentation/**. Không sửa widget ui/gameplay của Hieu.
- **Phụ thuộc triển khai:** H02
- **Ghép tích hợp / nghiệm thu:** H09, N15, N24, I01
- **Điều kiện hoàn thành:** Ban ngày không fog; đêm circle 4 ô, Scout 5.2, cộng vùng trụ đứng radius 6 cho mọi người. Quái ngoài các vùng được phép không được vẽ; tắt trụ thì quái ngoài vùng còn lại biến mất cùng frame. Dusk đóng trong 15 s, dawn mở trong 15 s theo profile GDD; HUD/telegraph cần thiết đúng thiết kế. Dùng PlayerViewState/TowerLightState fixtures; vision không đọc trực tiếp class build/simulation của Hieu và không cần N15/N24 hoàn tất để test render.

<a id="h16"></a>

### H16 — Spectator camera cho người đã chết

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-07, mục 3.3
- **File / module sở hữu:** src/presentation/**; ui/hud/**; config/display/**; tests/presentation/**. Không sửa widget ui/gameplay của Hieu.
- **Phụ thuộc triển khai:** H09
- **Ghép tích hợp / nghiệm thu:** N11, H09, I01
- **Điều kiện hoàn thành:** Người chết theo dõi đồng đội còn sống và chuyển mục tiêu được, không điều khiển/nhìn bằng vị trí xác chết. Camera/vision theo quyền spectator đã chốt, không gửi input người sống; hồi sinh trả camera và control đúng nhân vật. Dùng fixture dead/alive để test camera, không chờ Revival kit thật; ghép luồng N11 khi nghiệm thu.

<a id="h19"></a>

### H19 — Dawn briefing, boss card và tutorial ngày 1

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-19–20, FML-05, CHR-04, mục 5.3
- **File / module sở hữu:** src/presentation/**; ui/hud/**; config/display/**; tests/presentation/**. Không sửa widget ui/gameplay của Hieu.
- **Phụ thuộc triển khai:** H14
- **Ghép tích hợp / nghiệm thu:** A01, A07, H14, I01
- **Điều kiện hoàn thành:** Dawn báo cửa và đúng boss đã bốc; radio panel đầy đủ 8 s rồi một dòng. Boss arrival card code/name 2 s, HP bar đúng archetype; hints ngày 1 chỉ node/workbench và các story beat theo bảng 5.4, tất cả English, có event radio/boss audio. Dựng từ BossDescriptor/DawnBriefing fixtures; không cần director/boss framework thật hoàn thành trước.

<a id="h20"></a>

### H20 — Result screen và quay về đúng room lobby

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** Mục 2.5, FTR-03, hình 6.4
- **File / module sở hữu:** ui/{menu,lobby,settings,timeout,results}/** theo card; tests/presentation/** — Ngon.
- **Phụ thuộc triển khai:** H05, H10
- **Ghép tích hợp / nghiệm thu:** H05, H10, H17, I01
- **Điều kiện hoàn thành:** Win/Loss/Ended by vote và nguyên nhân rõ (gồm cửa gây mất base), đêm đạt, kills/revives/damage taken từng người. Mỗi người có Back to lobby; bỏ tự thoát sau 10 s và không instant restart. Giữ room, đổi role/mode được; match mới reset mọi tiến trình và có seed mới.

<a id="h21"></a>

### H21 — Export presets và build liên nền tảng

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-ENG, bảng 7.1
- **File / module sở hữu:** deploy/**; export_presets.cfg; project.godot; cấu hình build/CI — Ngon.
- **Phụ thuộc triển khai:** H03, H04
- **Ghép tích hợp / nghiệm thu:** H03, H04, I01
- **Điều kiện hoàn thành:** Có preset client Windows x64 .exe+.pck, macOS 12+ universal .app Intel/Apple Silicon ad-hoc signed, và headless server Windows/macOS/Linux. Build sạch đủ tài nguyên/fonts/config; macOS không phụ thuộc D3D12, input/trackpad/scaling chạy được; chưa tính Linux client/web là MVP. Ngon chịu trách nhiệm build server/multiplayer; Hieu phối hợp kiểm tra dữ liệu gameplay/audio và tài liệu chạy.

<a id="h22"></a>

### H22 — Triển khai lobby/rooms trên máy chủ và DNS

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-NET, mục 7.3, DEC-15
- **File / module sở hữu:** deploy/**; export_presets.cfg; project.godot; cấu hình build/CI — Ngon.
- **Phụ thuộc triển khai:** H04, H05, H06, H21
- **Ghép tích hợp / nghiệm thu:** H04, H05, H06, H21, I01
- **Điều kiện hoàn thành:** Chuẩn bị VM mục tiêu 4 vCPU/4 GB RAM tại Singapore theo GDD, UDP 7777–7780, startup/shutdown và tự cập nhật DNS khi IP đổi. Có bằng chứng client ngoài LAN thấy/join room; vẫn có phương án LAN dùng cùng lobby server theo README. Ngon là owner tạo, cấu hình và vận hành server.

<a id="h24"></a>

### H24 — Telemetry một log cho mỗi match

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** Mục 7.8, H1–H16, FML-11
- **File / module sở hữu:** src/telemetry/**; tests/telemetry/** — collector; event phát ở module do owner module đó sửa.
- **Phụ thuộc triển khai:** H02
- **Ghép tích hợp / nghiệm thu:** H02, H09, A01, I01
- **Điều kiện hoàn thành:** Log match seed/build/profile/mode/team size và các mốc pha; base/door HP theo đêm, downed time, boss kill time/archetype, tài nguyên theo nguồn, chết ban ngày, tower uptime/kills, bot/leaver và kết quả đêm 5. Dữ liệu đủ tái hiện/thống kê hypotheses, không chỉ console ok/FAIL của unit test. Collector chỉ nhận DomainEvent/schema đã chốt, test bằng stream giả; từng module phát event trong code do owner của nó sửa. Không cấy logging trực tiếp vào gameplay/AI file của người khác.

<a id="h26"></a>

### H26 — Chuẩn hóa hiển thị và hiệu năng client

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** Bảng 6.1, bảng 7.1, TECH-ENG
- **File / module sở hữu:** src/presentation/**; ui/hud/**; config/display/**; tests/presentation/**. Không sửa widget ui/gameplay của Hieu.
- **Phụ thuộc triển khai:** H14, H15, H21
- **Ghép tích hợp / nghiệm thu:** H01, H14, H15, H21, I01
- **Điều kiện hoàn thành:** Profile GDD render 960×540 scale lên 1920×1080, tile 32 và camera ~30×17 ô/fixed zoom; font/kích cỡ/readability/camera shake theo baseline hoặc CHG đã duyệt. Đo 60 FPS trên Windows RTX2050 và Mac M1 bằng build thực; xử lý bottleneck có số đo, không chỉ benchmark simulation.

<a id="n17"></a>

### N17 — Enhance đồ họa và hoàn thiện sprite ba vai trò

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** ART-01, CHR-01–03, bảng 6.1
- **File / module sở hữu:** art/chars/**; art/chars/chars.json; tools/PixelChars.cs; assets/ai-log/**/enhanced.png + output-log.*; src/presentation/characters/**.
- **Phụ thuộc triển khai:** G01
- **Ghép tích hợp / nghiệm thu:** N09, H01, G01, I01
- **Điều kiện hoàn thành:** Ba sheet 4 hướng có idle/walk/attack/downed/death; đúng anchor, pixel grid và màu vai trò. Scout có cung, Healer dấu cộng xanh/đồ y tế, Engineer công cụ rõ; left/right được mirror. Tích hợp animation với state thực, ghi nguồn/quy trình và licence. Ngon dùng AI đồ họa trên gói original.png + prompt.txt của G01; giữ nguyên PNG gốc, lưu enhanced.png và thông tin tool/version/ngày/prompt thực dùng, review từng frame rồi mới tích hợp. Hieu phối hợp animation player; Viet Anh phối hợp state/hitbox enemy và boss. Phạm vi Ngon là PNG/animation descriptor/preview; Hieu bind action gameplay qua descriptor đã chốt, không cần N09 làm xong mới enhance. Ngon dùng scene preview với state giả để review từng frame.

## Vu Duc Viet Anh — AI / Boss / World / Gói asset-prompt

| ID | Ưu tiên | Task | Owner |
|---|---|---|---|
| [G01](#g01) | P0 | Chuẩn bị thư mục asset PNG gốc và prompt TXT cho Ngon enhance | Vu Duc Viet Anh |
| [A01](#a01) | P0 | Match seed, bốc cửa và bốc boss tái lập được | Vu Duc Viet Anh |
| [A02](#a02) | P0 | Wave director đúng budget, nhịp và phân bổ | Vu Duc Viet Anh |
| [A03](#a03) | P1 | Chuẩn hóa ba quái thường và projectile Spitter | Vu Duc Viet Anh |
| [A04](#a04) | P0 | Sửa FSM, threat window và lock-on cửa/trụ | Vu Duc Viet Anh |
| [A05](#a05) | P0 | Navigation theo tilemap và công trình động | Vu Duc Viet Anh |
| [A06](#a06) | P1 | Hoàn thiện daytime roamers | Vu Duc Viet Anh |
| [A07](#a07) | P0 | Framework boss dùng chung và luật công bằng | Vu Duc Viet Anh |
| [A08](#a08) | P1 | Boss Juggernaut hoàn chỉnh | Vu Duc Viet Anh |
| [A09](#a09) | P1 | Boss Broodmother hoàn chỉnh | Vu Duc Viet Anh |
| [A10](#a10) | P1 | Boss Bio-Cannon hoàn chỉnh | Vu Duc Viet Anh |
| [A11](#a11) | P1 | Boss Burrower hoàn chỉnh | Vu Duc Viet Anh |
| [A12](#a12) | P1 | Boss Gatebreaker hoàn chỉnh | Vu Duc Viet Anh |
| [A13](#a13) | P1 | Boss Phantom hoàn chỉnh | Vu Duc Viet Anh |
| [A14](#a14) | P1 | Boss Ironhide hoàn chỉnh | Vu Duc Viet Anh |
| [A16](#a16) | P0 | Hoàn thiện đảo và tilemap baseline | Vu Duc Viet Anh |
| [A17](#a17) | P1 | Tài nguyên, metal sites và respawn đúng GDD | Vu Duc Viet Anh |
| [A18](#a18) | P1 | Drops, capacity và phần thưởng boss | Vu Duc Viet Anh |
| [A19](#a19) | P0 | Bot tiếp quản người rời thay vì sinh teammate từ lobby | Vu Duc Viet Anh |
| [A20](#a20) | P1 | Bot theo đúng danh sách hành vi BAL-47 | Vu Duc Viet Anh |
| [A21](#a21) | P0 | Sửa và kiểm chứng lỗi bot farm không ổn định | Vu Duc Viet Anh |
| [A23](#a23) | P1 | Kiểm thử boss, director và randomness | Vu Duc Viet Anh |
| [A24](#a24) | P1 | Phân tích cân bằng và đề xuất tuning có bằng chứng | Vu Duc Viet Anh |

<a id="g01"></a>

### G01 — Chuẩn bị thư mục asset PNG gốc và prompt TXT cho Ngon enhance

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** ART-01–03, CHR-01–08, bảng 6.1, phụ lục B; quy trình bổ sung theo yêu cầu người dùng
- **File / module sở hữu:** assets/ai-log/**/original.png; assets/ai-log/**/prompt.txt; input-manifest.* — chỉ đầu vào, không sửa enhanced.png.
- **Phụ thuộc triển khai:** —
- **Ghép tích hợp / nghiệm thu:** H01
- **Điều kiện hoàn thành:** Có folder assets/ai-log/ chia theo từng asset; mỗi asset tối thiểu có original.png là bản sao ảnh nguồn và prompt.txt UTF-8 mô tả yêu cầu chỉnh riêng. Chuẩn bị trước ba player và ba enemy từ art/chars; với bảy boss, dùng ảnh tham chiếu/placeholder có nguồn và ghi nhãn rõ, không giả là concept gốc đã duyệt. Prompt ghi nguồn/licence/mã GDD, hình dáng/màu/trang bị cần sửa, kích thước sheet/frame, layout/hướng/animation/anchor, alpha và đầu ra; nêu rõ giữ layout hay đổi có chủ đích. Có README/manifest chỉ nguồn, thứ tự ưu tiên, trạng thái, người chuẩn bị và task nhận N17/A15/A22. Không dùng .png.import thay PNG, không ghi đè art đang chạy. Ngon review gói trước khi chạy AI. Output enhanced.png thuộc task enhance tương ứng, không phải điều kiện G01 Done. Bắt đầu từ PNG và style GDD đang có, không phải chờ server, gameplay hoặc H01 đóng card. Bàn giao từng asset/batch có version; chỉ sửa original.png/prompt.txt/manifest đầu vào, không sửa enhanced.png/descriptor đầu ra của Ngon.

<a id="a01"></a>

### A01 — Match seed, bốc cửa và bốc boss tái lập được

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-19, FML-05
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** H02
- **Ghép tích hợp / nghiệm thu:** H02, N24, I01
- **Điều kiện hoàn thành:** Một seed 32-bit server tạo/log/gửi; mọi random thiết kế dùng RNG của match, không shuffle RNG toàn cục. Bốc min(3,k) cửa và boss ngay bình minh; đêm 2 một trong 7, đêm 4 một trong 6 còn lại. Cùng seed ra cùng kết quả; kiểm tra phân bố 1000 seed theo T-FML05.

<a id="a02"></a>

### A02 — Wave director đúng budget, nhịp và phân bổ

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-19, BAL-04, BAL-33–34, FML-04–05
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A01
- **Ghép tích hợp / nghiệm thu:** A01, N24, I01
- **Điều kiện hoàn thành:** Profile GDD dùng budget và carry theo công thức, đúng loại mở khóa, độ khó, hệ số boss/đêm 5; gán cửa round-robin và spawn ngoài tầm nhìn 25–35 ô trước cửa, không phía nam. Áp dụng cap chung cho wave/roamer/boss/summon; dùng case T-FML04 và kiểm tra không spawn lộ trong camera.

<a id="a03"></a>

### A03 — Chuẩn hóa ba quái thường và projectile Spitter

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** AGT-01–04, BAL-29–32, FML-01, TECH-PHY
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** H02, I01
- **Ghép tích hợp / nghiệm thu:** N24, N02, I01
- **Điều kiện hoàn thành:** Stats và làm tròn theo đêm/độ khó; Grunt 2, Spitter 2, Wrecker 1.6 ô/s lúc đêm 1, cap 3. Spitter chỉ nhắm người, giữ tầm/range theo BAL-30, projectile theo TECH-PHY và first-hit; Wrecker ×2 công trình và không retaliate. Chốt xung đột collider/tầm đánh bằng acceptance check của GDD.

<a id="a04"></a>

### A04 — Sửa FSM, threat window và lock-on cửa/trụ

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** FML-09, MEC-28, BAL-35
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** H02
- **Ghép tích hợp / nghiệm thu:** H02, N15, I01
- **Điều kiện hoàn thành:** Threat chỉ tính damage trong 5 s, đổi khi >1.2×, đánh giá 0.25 s; leash 12 ô/5 s không damage, boss miễn tái aggro 10 s sau break. Trụ thu hút trong vùng sáng nhưng lock chỉ sau hit đầu; quái lock bỏ qua người, trụ vỡ đi tiếp tới cửa. Wrecker/Spitter đúng ngoại lệ; test các chuyển state hình 7.2. Unit test dùng FakeStructureState/FakeCombat; không phải chờ trụ N15 chạy thật. AI chỉ phát damage intent, không tự trừ HP trong module của Hieu.

<a id="a05"></a>

### A05 — Navigation theo tilemap và công trình động

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-AI, TECH-PHY
- **File / module sở hữu:** src/world/**; config/world/**; maps/**; WorldGeometry.cs; art/nature/**; tests/world/**.
- **Phụ thuộc triển khai:** H02, A16, I01
- **Ghép tích hợp / nghiệm thu:** H02, A16, N01, I01
- **Điều kiện hoàn thành:** NavigationAgent2D/navmesh theo TECH-AI, cập nhật đường 0.5 s có stagger; không xuyên cây/đá/cửa/công trình. Sau build/phá/tái sinh tài nguyên, đường cập nhật; mục tiêu bị bọc bởi collider vẫn tiếp cận được. Quái không bypass wall xây, bot không kẹt cửa; không mất server authority. World/nav chỉ nhận footprint/radius/layer và danh sách dynamic obstacles theo WorldQuery; dùng FakeBody/FakeObstacle để không phải chờ controller N01 hoặc build N14.

<a id="a06"></a>

### A06 — Hoàn thiện daytime roamers

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-26, BAL-44, FML-16
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A01, A03, A05
- **Ghép tích hợp / nghiệm thu:** A01, A03, A05, I01
- **Điều kiện hoàn thành:** Kiểm tra mỗi 15 s, xác suất 0 ở 20 ô tới 30% ở 60; nhóm hai, tối đa tám. Spawn ngoài camera mọi người và phía xa base, dùng camera thực tế. Ban ngày wander/chase không đánh base; dusk chuyển sang cửa active gần nhất; test xác suất, spawn bounds và cap.

<a id="a07"></a>

### A07 — Framework boss dùng chung và luật công bằng

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-20, BAL-36–39, BAL-43, FML-02–03
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A01, A03, A04
- **Ghép tích hợp / nghiệm thu:** A01, A03, A04, N02, I01
- **Điều kiện hoàn thành:** Spawn 300 s đêm 2/4, HP theo n/ub/mode; có telegraph→act→recover và slam chung. Mọi damaging move telegraph ≥1 s; đêm 4 damage ×1.21, cooldown ×0.9, telegraph giữ nguyên. Không reset leash khi tunnel/teleport; summon tuân cap, không drop và xóa khi boss chết. Khung hỗ trợ bảy module và kill kết thúc đêm.

<a id="a08"></a>

### A08 — Boss Juggernaut hoàn chỉnh

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** AGT-05, bảng 4.2
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A07
- **Ghép tích hợp / nghiệm thu:** A07, I01
- **Điều kiện hoàn thành:** Charge mỗi 8 s khi target cách 3–10 ô; line telegraph 1 s/rộng 1.5 ô; dash tối đa 10 ô ở 9 ô/s, 35 damage và knockback, không bẻ hướng bám người đã né; kết thúc bằng slam và recovery 2 s không đánh. Đủ bản đêm 2/4 và dữ liệu art cho A15.

<a id="a09"></a>

### A09 — Boss Broodmother hoàn chỉnh

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** AGT-06, bảng 4.2
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A07
- **Ghép tích hợp / nghiệm thu:** A07, I01
- **Điều kiện hoàn thành:** Mỗi 12 s glow 1 s rồi sinh hai Grunt, tối đa bốn còn sống; không drop, tính cap chung, xóa cùng boss. Tốc độ ×0.75, ub=0.6 và HP tương ứng; slam/door damage/night-4 scaling đúng; giai đoạn wind-up đọc được.

<a id="a10"></a>

### A10 — Boss Bio-Cannon hoàn chỉnh

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** AGT-07, bảng 4.2
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A07
- **Ghép tích hợp / nghiệm thu:** A07, I01
- **Điều kiện hoàn thành:** Mỗi 5 s telegraph 1 s rồi tám đạn theo vòng 45°, 12 damage/viên, 4 ô/s, range 8; giữ khoảng năm ô, recovery 2 s sau volley. ub=0.45, collision/first-hit và phiên bản đêm 4 đúng.

<a id="a11"></a>

### A11 — Boss Burrower hoàn chỉnh

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** AGT-08, bảng 4.2
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A07
- **Ghép tích hợp / nghiệm thu:** A07, I01
- **Điều kiện hoàn thành:** Mỗi 10 s burrow tối đa 3 s; không bị đánh và không đánh cửa khi ngầm. Trồi cách target ≥1.5 ô với marker 1 s/radius 2, damage 35; climb-out 1.5 s không đánh. ub=0.4; không reset leash/aggro immunity; night-4 scaling đúng.

<a id="a12"></a>

### A12 — Boss Gatebreaker hoàn chỉnh

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** AGT-09, bảng 4.2
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A07
- **Ghép tích hợp / nghiệm thu:** A07, N12, I01
- **Điều kiện hoàn thành:** Door/tower blow 120 damage mỗi 8 s sau telegraph 1 s, không cộng thêm hit thường; trung bình đúng 15 DPS trước night-4 scaling. Đánh người chỉ dùng slam; sửa được giữa các đòn, đúng pause; ub=0.55.

<a id="a13"></a>

### A13 — Boss Phantom hoàn chỉnh

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** AGT-10, bảng 4.2
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A07
- **Ghép tích hợp / nghiệm thu:** A07, H15, I01
- **Điều kiện hoàn thành:** Mỗi 12 s cloak tối đa 4 s nhưng vẫn bị đánh; nhận hit hoặc bắt đầu telegraph thì lộ. Ambush arc 1.5 ô, telegraph 1 s, damage 35; không đánh cửa khi cloak, hiện tối thiểu 3 s sau ambush; ub=0.4. Phân biệt cloak với fog nhìn của người chơi.

<a id="a14"></a>

### A14 — Boss Ironhide hoàn chỉnh

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** AGT-11, bảng 4.2
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A07
- **Ghép tích hợp / nghiệm thu:** A07, N02, I01
- **Điều kiện hoàn thành:** Đòn từ phía trước 120° chỉ gây 30% damage; phía sau full. Sau slam stagger 3 s, mọi hướng full damage; ub=0.35. Có test góc biên, đánh từ nhiều người và cả damage ranged/tower.

<a id="a16"></a>

### A16 — Hoàn thiện đảo và tilemap baseline

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** ART-04, mục 4.3, hình 4.1, BAL-21
- **File / module sở hữu:** src/world/**; config/world/**; maps/**; WorldGeometry.cs; art/nature/**; tests/world/**.
- **Phụ thuộc triển khai:** —
- **Ghép tích hợp / nghiệm thu:** H01, N24, I01
- **Điều kiện hoàn thành:** TileMapLayer cho đảo khoảng 150×150 ô, base 15×10, cửa A tây/B bắc/C đông, không cửa nam, ba pad. Coast 70–76 ô, cát 4–6 ô, biển không đi qua. Safe zone mở 20 ô, outer có rừng/đá/wrecks/ruins và hành lang cửa rộng ba ô; spawn/path hợp lý và ba cửa được kiểm tra công bằng.

<a id="a17"></a>

### A17 — Tài nguyên, metal sites và respawn đúng GDD

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-08, ITM-14–15, BAL-17, FML-11
- **File / module sở hữu:** src/world/**; config/world/**; maps/**; WorldGeometry.cs; art/nature/**; tests/world/**.
- **Phụ thuộc triển khai:** A16
- **Ghép tích hợp / nghiệm thu:** A16, N03, N24, I01
- **Điều kiện hoàn thành:** Node từng loại 4+3n, yield 5, rate 0.35/s và respawn dawn; không block cửa hoặc tạo node không thể farm. Metal chỉ ngoài 20 ô, có hình wreck/ruins đúng thiết kế. Thu thập hủy do di chuyển/hit; log lượng và nguồn tài nguyên cho economy test.

<a id="a18"></a>

### A18 — Drops, capacity và phần thưởng boss

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-23, BAL-41, FML-15
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A01
- **Ghép tích hợp / nghiệm thu:** N03, N05, A01, N24, I01
- **Điều kiện hoàn thành:** Grunt 10%/1–2 wood-stone-herb; Spitter 15%/1 bandage; Wrecker 20%/2 metal; boss luôn 10 metal+3 bandage. Nhặt do overlap khi đủ chỗ, không từ trong base; TTL 30 s, blink 5 s cuối; summon không drop; test 10.000 kill và ownership.

<a id="a19"></a>

### A19 — Bot tiếp quản người rời thay vì sinh teammate từ lobby

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-27, AGT-13, BAL-47
- **File / module sở hữu:** src/ai/bots/**; Bots.cs; tests/ai/**; không sửa inventory/repair/timeout thật.
- **Phụ thuộc triển khai:** H02
- **Ghép tích hợp / nghiệm thu:** H10, N03, I01
- **Điều kiện hoàn thành:** Sau vote Bot, giữ đúng role/HP/pos/inventory của leaver, một bot mỗi người rời; bot không vote, được tính n khi wave/boss spawn. Không tự thêm bot lúc start ở profile GDD; demo giữ riêng. Không tạo lại character có đồ/HP mới. Phát triển qua BotReplacementPort với event leaver và PlayerState/inventory giả; không chờ màn vote H10 hay inventory N03 hoàn tất. Server adapter H10 gọi port này khi vote thật kết thúc.

<a id="a20"></a>

### A20 — Bot theo đúng danh sách hành vi BAL-47

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** AGT-13, BAL-47, TECH-AI
- **File / module sở hữu:** src/ai/bots/**; Bots.cs; tests/ai/**; không sửa inventory/repair/timeout thật.
- **Phụ thuộc triển khai:** A19, A05
- **Ghép tích hợp / nghiệm thu:** A19, A05, N06, N12, I01
- **Điều kiện hoàn thành:** Ban ngày chỉ farm safe zone và gửi kho khi đầy; không craft/build/đi xa quá 20 ô/vào base ban đêm. Đêm giữ cửa active ít người nhất, đánh trong sáu ô; bandage <40% HP; Healer ưu tiên revive trong tám ô, Engineer sửa cửa bị hư nhiều nhất. Không dùng bot planner/crafting demo trong GDD profile. Bot phát cùng GameplayCommand như người chơi; dùng FakeInventory/FakeRepair/FakeStorage để test chính sách, không tự sửa implementation kho/repair.

<a id="a21"></a>

### A21 — Sửa và kiểm chứng lỗi bot farm không ổn định

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** Kiểm thử hiện tại, AGT-13, TECH-AI
- **File / module sở hữu:** src/ai/bots/**; Bots.cs; tests/ai/**; không sửa inventory/repair/timeout thật.
- **Phụ thuộc triển khai:** —
- **Ghép tích hợp / nghiệm thu:** A05, A17, I01
- **Điều kiện hoàn thành:** Điều tra case hiện tại bots gather resources by day thất bại; có test seed cố định, node bị che/khó tới, đứng sát collider và replan khi target cạn/không tới được. Phân biệt lỗi điều hướng với ngưỡng thời gian test; sửa nguyên nhân, không chỉ bỏ assertion; kiểm tra nhiều seed. Điều tra ngay trên baseline hiện có, không chờ toàn bộ nav/tilemap/node mới. Regression test thuộc tests/ai; bổ sung case tích hợp khi ghép A05/A17.

<a id="a23"></a>

### A23 — Kiểm thử boss, director và randomness

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** FTR-02, FML-01–05, FML-09, BAL-43
- **File / module sở hữu:** src/ai/**; src/ai/config/**; scenes/ai/**; tests/ai/**. Boss state chỉ đọc animation descriptor, không sửa PNG/chars.json.
- **Phụ thuộc triển khai:** A02, A04, A08, A09, A10, A11, A12, A13, A14
- **Ghép tích hợp / nghiệm thu:** A02, A04, A08, A09, A10, A11, A12, A13, A14, I01
- **Điều kiện hoàn thành:** Có test từng signature và bản đêm 4, summon cap/no-drop, cloak/burrow reveal, leash/immunity, doors lock, same-seed/no-repeat, round-robin và scaling n=3/4/5. Kiểm tra biên không nhận n/k ngoài miền; chạy tất cả geometry regression.

<a id="a24"></a>

### A24 — Phân tích cân bằng và đề xuất tuning có bằng chứng

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** H1–H16, FML-03, FML-11, FML-15, bảng 3.2–3.3
- **File / module sở hữu:** reports/balance/**; src/ai/config/**; đề xuất CHG cho chủ resource ngoài AI.
- **Phụ thuộc triển khai:** Q04, H24, N24
- **Ghép tích hợp / nghiệm thu:** Q04, H24, N24, I01
- **Điều kiện hoàn thành:** Đối chiếu log với target GDD: boss TTK 65/78 s, thu/chi ngày 3 ~0.8, drop 5–15%, win-rate theo mode, độ tương đương boss/cửa và hiệu quả bot/trụ. Ghi measured result, đề xuất thay đổi và CHG; không coi số test demo là kết quả cân bằng baseline.

## Pham Minh Hieu — Gameplay / Match / Audio / Tài liệu

| ID | Ưu tiên | Task | Owner |
|---|---|---|---|
| [M01](#m01) | P0 | Match simulation: ngày/đêm, vote, unlock và vòng đời trận | Pham Minh Hieu |
| [H01](#h01) | P0 | Chốt baseline, phạm vi và log thay đổi GDD | Pham Minh Hieu |
| [H17](#h17) | P0 | Nghiệm thu logic giải cứu và thứ tự thắng/thua | Pham Minh Hieu |
| [H18](#h18) | P1 | Intro và kết thúc trực quan | Pham Minh Hieu |
| [H23](#h23) | P1 | Credits, giấy phép và AI disclosure thực tế | Pham Minh Hieu |
| [H25](#h25) | P1 | README chạy từ máy sạch, LAN và cloud | Pham Minh Hieu |
| [N01](#n01) | P0 | Hoàn thiện scene và physics của người chơi | Pham Minh Hieu |
| [N02](#n02) | P0 | Hoàn thiện cận chiến, sát thương và knockback | Pham Minh Hieu |
| [N03](#n03) | P0 | Làm inventory có giới hạn ô và stack | Pham Minh Hieu |
| [N04](#n04) | P1 | Làm hotbar và đủ thao tác inventory | Pham Minh Hieu |
| [N05](#n05) | P1 | Cho/thả/nhặt vật phẩm do người chơi thả | Pham Minh Hieu |
| [N06](#n06) | P1 | Kho chung tại căn cứ | Pham Minh Hieu |
| [N07](#n07) | P0 | Sửa crafting thành thao tác tại workbench mất 3 giây | Pham Minh Hieu |
| [N08](#n08) | P1 | Cung Scout, cung chế tạo và tên | Pham Minh Hieu |
| [N09](#n09) | P1 | Hoàn thiện bộ đồ khởi đầu và hồi máu | Pham Minh Hieu |
| [N10](#n10) | P0 | Sửa luật downed và hồi sinh đồng đội | Pham Minh Hieu |
| [N11](#n11) | P1 | Hoàn thiện chết và Revival kit | Pham Minh Hieu |
| [N12](#n12) | P0 | Sửa cửa/trụ chỉ cho một Engineer sửa mỗi mục tiêu | Pham Minh Hieu |
| [N13](#n13) | P0 | Sửa chữa HP căn cứ đúng ngày và đúng tốc độ | Pham Minh Hieu |
| [N14](#n14) | P1 | Build menu và tường gỗ/đá | Pham Minh Hieu |
| [N15](#n15) | P1 | Hoàn thiện xây và sửa trụ phòng thủ | Pham Minh Hieu |
| [N16](#n16) | P0 | Hoàn thiện vào/ra căn cứ và kết thúc breach | Pham Minh Hieu |
| [N18](#n18) | P1 | Hoàn thiện ảnh công trình, đồ vật và icon | Pham Minh Hieu |
| [N19](#n19) | P1 | Nhập đủ tài nguyên nhạc và SFX kèm nguồn | Pham Minh Hieu |
| [N20](#n20) | P1 | Audio manager và mixer | Pham Minh Hieu |
| [N21](#n21) | P1 | Gắn 8 cue nhạc vào đúng pha | Pham Minh Hieu |
| [N22](#n22) | P1 | Gắn đủ 25 cue SFX vào gameplay và UI | Pham Minh Hieu |
| [N23](#n23) | P1 | Kiểm thử gameplay theo MEC/FML và sửa các regression | Pham Minh Hieu |
| [N24](#n24) | P0 | Profile Demo/GDD và resource gameplay có ranh giới rõ | Pham Minh Hieu |

<a id="m01"></a>

### M01 — Match simulation: ngày/đêm, vote, unlock và vòng đời trận

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — chưa triển khai
- **GDD:** MEC-14, MEC-18, MEC-19, MEC-25; TECH-ENG; phần luật chuyển ra từ H03
- **File / module sở hữu:** src/match/**; tests/match/**; GameSim.cs — chỉ Hieu.
- **Phụ thuộc triển khai:** H02, I01, N24
- **Ghép tích hợp / nghiệm thu:** H02, I01, N24
- **Điều kiện hoàn thành:** Hieu sở hữu src/match và GameSim.cs: state machine match/day/dusk/night/result, clock, vote sau 180 s với ceil(0.6×người thật), rút vote, tự hết ngày 360 s, bot không vote; unlock stone sau đêm 1, metal/tower sau boss đầu; reset match/new seed đúng. H17 cung cấp outcome/extraction cùng module. Network chỉ yêu cầu begin/apply_action/step/set_frozen và đọc snapshot/events. Dùng FakeAI/FakeWorld để test progression không cần server/AI thật; UI/network state khác room/connection do Ngon giữ.

<a id="h01"></a>

### H01 — Chốt baseline, phạm vi và log thay đổi GDD

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** M1, mục 7.5, phụ lục A–C
- **File / module sở hữu:** docs/**; README.md; manifest/credits tổng hợp — Hieu, không sửa log đầu ra art của Ngon.
- **Phụ thuộc triển khai:** —
- **Ghép tích hợp / nghiệm thu:** Không có; review gói đầu vào/contract theo card.
- **Điều kiện hoàn thành:** Cả ba chốt phiên bản GDD và một ma trận deviation: số liệu demo, base 11×7, font/zoom, kiến trúc tự viết, bot lobby. Đưa bản GDD dùng nghiệm thu vào tài liệu được version-control, kiểm tra tag baseline. Giữ 7 boss/online rooms/audio trong MVP trừ khi nhóm ghi CHG theo fallback; stretch không lẫn MVP.

<a id="h17"></a>

### H17 — Nghiệm thu logic giải cứu và thứ tự thắng/thua

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-22, FML-13, mục 2.5
- **File / module sở hữu:** src/match/**; tests/match/**; GameSim.cs — chỉ Hieu.
- **Phụ thuộc triển khai:** H02, I01, N24, M01
- **Ghép tích hợp / nghiệm thu:** A01, H09, N24, I01
- **Điều kiện hoàn thành:** Ba pad radius 4, đêm 5 countdown 600 s; hết giờ có pad sạch thì chọn pad nhiều người nhất, tie dùng seeded random. Không pad sạch chỉ extension 60 s khi base>500, giữ sạch liên tục 10 s; enemy vào reset, vẫn spawn. Base chết/team wipe/boss timer/failed extraction ưu tiên đúng, landing không lấn loss cùng tick; freeze thì không check.

<a id="h18"></a>

### H18 — Intro và kết thúc trực quan

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** ART-09, mục 5.3, bảng 5.4
- **File / module sở hữu:** ui/story/**; art/story/**; tests/presentation/story/** — Hieu.
- **Phụ thuộc triển khai:** —
- **Ghép tích hợp / nghiệm thu:** H03, H17, H01, I01
- **Điều kiện hoàn thành:** Bộ sáu still theo ART-09: opening 3–4 panel một lần trước ngày 1 có skip và hai ending; có cảnh helicopter rời đi khi thắng, đèn tắt khi thua, caption English. Không chặn combat của match đang chạy; tích hợp helicopter/pad feedback, bàn giao audio event N22.

<a id="h23"></a>

### H23 — Credits, giấy phép và AI disclosure thực tế

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** Mục 6.1/6.4/7.4, phụ lục B
- **File / module sở hữu:** docs/**; README.md; manifest/credits tổng hợp — Hieu, không sửa log đầu ra art của Ngon.
- **Phụ thuộc triển khai:** —
- **Ghép tích hợp / nghiệm thu:** N17, N18, N19, A15, A22, H18, I01
- **Điều kiện hoàn thành:** Credits trong game/README và asset manifest khớp tài nguyên thực; ghi authors/URLs/licence/nguồn font. AI-assisted edits có log source/tool/date/prompt/raw/cleanup theo chính sách GDD; art tạo bằng script được khai báo trung thực, không gán nhầm là CC0 pack edit. Nhận log enhance từ Ngon và manifest nguồn/prompt từ Viet Anh (G01).

<a id="h25"></a>

### H25 — README chạy từ máy sạch, LAN và cloud

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-ENG, TECH-NET, M5
- **File / module sở hữu:** docs/**; README.md; manifest/credits tổng hợp — Hieu, không sửa log đầu ra art của Ngon.
- **Phụ thuộc triển khai:** H23
- **Ghép tích hợp / nghiệm thu:** H21, H22, H23, I01
- **Điều kiện hoàn thành:** Hướng dẫn Windows/macOS cho client và server, mở lần đầu app macOS, lobby start tự spawn rooms, --server= LAN, ports, controls, settings và xử lý server offline. Ghim Godot 4.7.2, ghi scope/version/feedback; người chưa có môi trường dev làm theo chạy được.

<a id="n01"></a>

### N01 — Hoàn thiện scene và physics của người chơi

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-ENG, TECH-PHY, MEC-01
- **File / module sở hữu:** src/gameplay/player/**; scenes/gameplay/player/**; tests/gameplay/**.
- **Phụ thuộc triển khai:** H02, I01
- **Ghép tích hợp / nghiệm thu:** H02, H03, I01
- **Điều kiện hoàn thành:** Player có scene riêng và CharacterBody2D/move_and_slide theo kế hoạch kỹ thuật; đi 5 ô/s, Scout ×1.2; đi chéo không nhanh hơn; va chạm với thế giới, cửa, công trình, quái và bờ biển; chạy physics 60 Hz. Giữ phần mô phỏng server và prediction hoạt động khi chuyển kiến trúc.

<a id="n02"></a>

### N02 — Hoàn thiện cận chiến, sát thương và knockback

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-02, MEC-04, BAL-08, BAL-11, BAL-13, FML-06, TECH-PHY
- **File / module sở hữu:** src/gameplay/combat/**; scenes/gameplay/projectiles/**; tests/gameplay/**.
- **Phụ thuộc triển khai:** N01, H02
- **Ghép tích hợp / nghiệm thu:** N01, H02, I01
- **Điều kiện hoàn thành:** Cận chiến dùng Area2D/collision layers theo TECH-PHY, cung 90°, tầm 1.5 ô; một đòn trúng mỗi quái một lần, không friendly fire. Áo giáp, làm tròn sát thương tối thiểu 1, miễn thương 1 s và knockback 4 ô/s hoạt động; boss đi vào người đẩy người ra. Có kiểm thử biên tầm đánh theo TECH-PHY.

<a id="n03"></a>

### N03 — Làm inventory có giới hạn ô và stack

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-09, BAL-18
- **File / module sở hữu:** src/gameplay/inventory/**; ui/gameplay/{inventory,hotbar,storage}/**; tests/gameplay/**.
- **Phụ thuộc triển khai:** H02
- **Ghép tích hợp / nghiệm thu:** H02, I01
- **Điều kiện hoàn thành:** Mỗi người có 12 ô, tối đa 20 đơn vị/stack; thêm/bớt/chia/gộp và kiểm tra đầy do server quyết định. Không mất/nhân đôi đồ khi nhặt, chế tạo, nhận đồ hoặc nhận RPC lặp; trạng thái được UI đọc được.

<a id="n04"></a>

### N04 — Làm hotbar và đủ thao tác inventory

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-09, bảng 2.1, BAL-18
- **File / module sở hữu:** src/gameplay/inventory/**; ui/gameplay/{inventory,hotbar,storage}/**; tests/gameplay/**.
- **Phụ thuộc triển khai:** N03, H02
- **Ghép tích hợp / nghiệm thu:** N03, H02, I01
- **Điều kiện hoàn thành:** Bốn ô đầu là hotbar; 1–4 và bánh xe chọn ô; Tab mở inventory; kéo thả hoán đổi đồ với hotbar; có highlight ô chọn và số lượng. Bánh xe ở profile GDD không bị thao tác zoom demo chiếm mất; bàn phím và trackpad dùng được.

<a id="n05"></a>

### N05 — Cho/thả/nhặt vật phẩm do người chơi thả

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-09, BAL-18
- **File / module sở hữu:** src/gameplay/inventory/**; ui/gameplay/{inventory,hotbar,storage}/**; tests/gameplay/**.
- **Phụ thuộc triển khai:** N03, N04
- **Ghép tích hợp / nghiệm thu:** N03, N04, H09, I01
- **Điều kiện hoàn thành:** G đưa vật phẩm đang chọn cho đồng đội gần nhất trong 1.5 ô, không có thì thả; đồ người chơi thả tồn tại tới khi nhặt, khác TTL đồ quái. Kiểm tra sức chứa, quyền sở hữu và đồng bộ; có marker/feedback và hỗ trợ trường hợp leaver không thay bằng bot. Core transfer/drop là hàm gameplay, test local và phát DomainEvent; ENet serialization đi qua adapter của Ngon, không phải chờ H09 để viết core.

<a id="n06"></a>

### N06 — Kho chung tại căn cứ

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-09, MEC-15, BAL-18
- **File / module sở hữu:** src/gameplay/inventory/**; ui/gameplay/{inventory,hotbar,storage}/**; tests/gameplay/**.
- **Phụ thuộc triển khai:** N03
- **Ghép tích hợp / nghiệm thu:** N03, H09, I01
- **Điều kiện hoàn thành:** Có storage chung 40 ô và giao diện chuyển đồ; ban ngày truy cập trong căn cứ, ban đêm chỉ Engineer được phép theo luật. Server kiểm tra quyền và xử lý hai người cùng thao tác không mất/nhân đôi đồ; nội dung kho không lộ cho vai trò không được phép. Phát triển kho và UI gameplay offline với hai fake clients/commands; adapter network chỉ vận chuyển request/result, không nhúng rule kho trong network module.

<a id="n07"></a>

### N07 — Sửa crafting thành thao tác tại workbench mất 3 giây

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-10, BAL-19, mục 3.2
- **File / module sở hữu:** src/gameplay/** theo hệ thống tương ứng; ui/gameplay/build/**; tests/gameplay/**; GameSim.cs chỉ làm router của Hieu.
- **Phụ thuộc triển khai:** N03, H02, N24
- **Ghép tích hợp / nghiệm thu:** N03, H02, N24, I01
- **Điều kiện hoàn thành:** Kiểm tra khoảng cách tương tác với workbench, vai trò, pha và unlock; chế tạo 3 s có tiến độ, trừ đúng chi phí và kiểm tra chỗ chứa. Ban ngày ai cũng chế tạo được; ban đêm chỉ Engineer đã vào căn cứ. Recipe và xử lý hủy/ngắt được thống nhất với luật tương tác của GDD, không còn mua tức thì từ mọi vị trí trong căn cứ.

<a id="n08"></a>

### N08 — Cung Scout, cung chế tạo và tên

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-03, ITM-02, ITM-09, BAL-10, BAL-42
- **File / module sở hữu:** src/gameplay/combat/**; scenes/gameplay/projectiles/**; tests/gameplay/**.
- **Phụ thuộc triển khai:** N02, N03, N04, N07
- **Ghép tích hợp / nghiệm thu:** N02, N03, N04, N07, I01
- **Điều kiện hoàn thành:** Scout có starter bow 8 damage, 0.75 phát/s, tầm 7, không tốn tên. Cung chế tạo 12 damage, 1 phát/s, tầm 8; recipe 6 wood + 2 metal kèm 5 tên, 1 wood làm 5 tên. Projectile theo TECH-PHY, trúng mục tiêu đầu tiên, dừng ở tường/tầm tối đa; hết tên không bắn; có trail và số tên.

<a id="n09"></a>

### N09 — Hoàn thiện bộ đồ khởi đầu và hồi máu

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-05, BAL-14, BAL-42, ITM-03, ITM-04
- **File / module sở hữu:** src/gameplay/** theo hệ thống tương ứng; ui/gameplay/build/**; tests/gameplay/**; GameSim.cs chỉ làm router của Hieu.
- **Phụ thuộc triển khai:** N03, N04
- **Ghép tích hợp / nghiệm thu:** N03, N04, I01
- **Điều kiện hoàn thành:** Mỗi vai trò có basic weapon và đồ nghề đúng GDD; hammer là công cụ build/repair và vũ khí riêng 12 damage, 1.2 đòn/s. Bandage +30 HP sau 2 s, Healer ×1.5; bị đánh hoặc tấn công hủy và giữ băng. Flask +40 HP sau 0.5 s cho mình/đồng đội trong 3 ô, 3 lần, nạp lại lúc bình minh, không chế tạo.

<a id="n10"></a>

### N10 — Sửa luật downed và hồi sinh đồng đội

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-06, BAL-15, FML-10
- **File / module sở hữu:** src/gameplay/** theo hệ thống tương ứng; ui/gameplay/build/**; tests/gameplay/**; GameSim.cs chỉ làm router của Hieu.
- **Phụ thuộc triển khai:** N02
- **Ghép tích hợp / nghiệm thu:** N02, H09, I01
- **Điều kiện hoàn thành:** Downed không di chuyển/đánh/dùng đồ và không nhận damage; bleed-out 30 s. Giữ E trong 1.5 ô để revive 20 s, Healer 10 s; reviver không được đánh đồng thời. Bị đánh, rời tầm hoặc ngừng tương tác reset đúng; đứng dậy 30% HP và miễn thương 1 s. Unit test state/commands offline; không phụ thuộc replication H09 khi viết luật.

<a id="n11"></a>

### N11 — Hoàn thiện chết và Revival kit

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-07, BAL-16, ITM-18
- **File / module sở hữu:** src/gameplay/** theo hệ thống tương ứng; ui/gameplay/build/**; tests/gameplay/**; GameSim.cs chỉ làm router của Hieu.
- **Phụ thuộc triển khai:** N07, N10
- **Ghép tích hợp / nghiệm thu:** N07, N10, I01
- **Điều kiện hoàn thành:** Chết giữ inventory, không tự hồi sinh khi sang ngày; kit 4 herb + 4 metal dùng ở workbench đưa đúng đồng đội về cửa gần nhất với 50% HP. Dữ liệu chết/sống và chọn mục tiêu đủ cho camera spectator H16; không cố định hồi sinh ở cửa A.

<a id="n12"></a>

### N12 — Sửa cửa/trụ chỉ cho một Engineer sửa mỗi mục tiêu

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-12, BAL-23, BAL-24, FML-08
- **File / module sở hữu:** src/gameplay/** theo hệ thống tương ứng; ui/gameplay/build/**; tests/gameplay/**; GameSim.cs chỉ làm router của Hieu.
- **Phụ thuộc triển khai:** H02, I01
- **Ghép tích hợp / nghiệm thu:** H02, I01
- **Điều kiện hoàn thành:** Một mục tiêu chỉ nhận một luồng repair: 15 HP/s, 1 wood/10 HP, trong 1.5 ô và từ ngoài; mỗi hit ngắt 2 s. Hai Engineer không làm tốc độ tăng thành 30 HP/s. Cửa vỡ chỉ sửa khi không có quái trong căn cứ và tối đa 150 HP; trụ vỡ chỉ sửa khi vùng sáng sạch và tối đa 90 HP; chi phí không âm.

<a id="n13"></a>

### N13 — Sửa chữa HP căn cứ đúng ngày và đúng tốc độ

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-13, BAL-20
- **File / module sở hữu:** src/gameplay/** theo hệ thống tương ứng; ui/gameplay/build/**; tests/gameplay/**; GameSim.cs chỉ làm router của Hieu.
- **Phụ thuộc triển khai:** N07
- **Ghép tích hợp / nghiệm thu:** N07, I01
- **Điều kiện hoàn thành:** Chỉ Engineer tại workbench ban ngày sửa được; 20 HP/s, 1 stone/20 HP, cap 1000, không tự hồi. Ban đêm tuyệt đối không dùng recipe sửa base; bỏ hành vi tăng tức thì 100 HP bằng 5 stone.

<a id="n14"></a>

### N14 — Build menu và tường gỗ/đá

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-11, BAL-27, ITM-07, ITM-11
- **File / module sở hữu:** src/gameplay/** theo hệ thống tương ứng; ui/gameplay/build/**; tests/gameplay/**; GameSim.cs chỉ làm router của Hieu.
- **Phụ thuộc triển khai:** N03, N24, H02
- **Ghép tích hợp / nghiệm thu:** N03, N24, H02, A05, I01
- **Điều kiện hoàn thành:** B mở build menu, preview xanh/đỏ, trái đặt/phải hủy; chỉ Engineer và đủ vật liệu mới đặt. Wood wall 5 wood/200 HP; stone wall 5 stone/400 HP sau đêm 1; cấm trong 3 ô quanh cửa và chồng vật cản. Tường có collision/HP/network và quái đánh công trình chắn đường. Phát StructureChanged với footprint qua WorldQuery; test placement bằng FakeWorld, không đợi A05 hoàn chỉnh. Khi ghép nav, Viet Anh nhận obstacle và giữ quyền sửa world_geometry.

<a id="n15"></a>

### N15 — Hoàn thiện xây và sửa trụ phòng thủ

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-11, MEC-28, BAL-28, AGT-12
- **File / module sở hữu:** src/gameplay/** theo hệ thống tương ứng; ui/gameplay/build/**; tests/gameplay/**; GameSim.cs chỉ làm router của Hieu.
- **Phụ thuộc triển khai:** N14, N12
- **Ghép tích hợp / nghiệm thu:** N14, N12, I01
- **Điều kiện hoàn thành:** Dùng build menu thống nhất; band 3–8 ô trước cửa, tối đa một/cửa và ba tổng cộng; kiểm tra đất trống. Trụ đầu miễn phí ngày 1 hoặc tự đặt khi dusk; trụ sau boss đầu tốn 10 wood + 5 metal; 300 HP, xác trụ và repair đúng luật. Cung cấp trạng thái cho AI A04 và ánh sáng H15. Trụ bắn mục tiêu gần nhất trong sáu ô ở 8 DPS, tôn trọng vật cản; event shot/destroy/repair và đèn flicker dưới 30% được đồng bộ.

<a id="n16"></a>

### N16 — Hoàn thiện vào/ra căn cứ và kết thúc breach

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MEC-14, MEC-15, MEC-16, BAL-25, BAL-26
- **File / module sở hữu:** src/gameplay/** theo hệ thống tương ứng; ui/gameplay/build/**; tests/gameplay/**; GameSim.cs chỉ làm router của Hieu.
- **Phụ thuộc triển khai:** N01, N12
- **Ghép tích hợp / nghiệm thu:** N01, N12, I01
- **Điều kiện hoàn thành:** Đêm đóng cửa, đẩy người còn trong ra cửa gần nhất; Engineer giữ F đứng yên 2 s để vào/ra, hủy khi di chuyển/bị hit/thả phím, ra được miễn thương 0.5 s. Khi breach cho phép vào và chiến đấu đúng luật; khi hết breach cảnh báo 5 s rồi đẩy người không được ở trong ra ngoài. Không giữ miễn thương/khóa hành động sai sau khi trạng thái cửa thay đổi.

<a id="n18"></a>

### N18 — Hoàn thiện ảnh công trình, đồ vật và icon

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** ART-05, ART-06
- **File / module sở hữu:** art/props/**; art/items/**; tools/PixelProps.cs; scene preview riêng.
- **Phụ thuộc triển khai:** —
- **Ghép tích hợp / nghiệm thu:** N03, N14, N15, H01, I01
- **Điều kiện hoàn thành:** Hoàn thiện bộ công trình của ART-05: lighthouse, wall, ba trạng thái cửa, tường gỗ/đá, trụ đứng/vỡ và pad; đủ icon MVP của ART-06. Door không chỉ đổi màu: có dấu nứt/gap, scale/anchor/collider nhất quán; không biến NOX files stretch thành blocker MVP. Icon/props có thể tạo và preview theo catalogue GDD trước khi inventory/build xong; Hieu sở hữu art/props và art/items, không sửa art/chars của Ngon.

<a id="n19"></a>

### N19 — Nhập đủ tài nguyên nhạc và SFX kèm nguồn

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** FTR-04, MUS-01–08, SFX-01–25, mục 6.4
- **File / module sở hữu:** src/audio/**; audio/**; tests/audio/**. AudioSettingsPort trong contracts không tự đổi.
- **Phụ thuộc triển khai:** —
- **Ghép tích hợp / nghiệm thu:** H01, I01
- **Điều kiện hoàn thành:** Có danh mục 8 cue nhạc và 25 cue SFX, file dùng được, nguồn/licence chính xác theo bảng 6.3 hoặc thay đổi đã ghi CHG. Phân biệt CC0/CC-BY, không dùng CC-BY-SA/chiptune; bàn giao thông tin ghi credit cho H23.

<a id="n20"></a>

### N20 — Audio manager và mixer

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** FTR-04, mục 6.4
- **File / module sở hữu:** src/audio/**; audio/**; tests/audio/**. AudioSettingsPort trong contracts không tự đổi.
- **Phụ thuộc triển khai:** H02
- **Ghép tích hợp / nghiệm thu:** N19, H02, I01
- **Điều kiện hoàn thành:** Bốn bus Music/Effects/UI/Radio; world sound positional với panning/falloff, tối đa tám enemy voices; crossfade nhạc 2 s. Danger cue duck nhạc 6 dB trong 1 s; kết nối volume Settings và lưu lựa chọn. AudioManager test bằng clip mẫu và fake events; không chờ thu đủ N19 mới viết mixer/duck/crossfade. Chỉ đóng phần nghiệm thu cue thật khi N19/N21/N22 đã ghép.

<a id="n21"></a>

### N21 — Gắn 8 cue nhạc vào đúng pha

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** MUS-01–08
- **File / module sở hữu:** src/audio/**; audio/**; tests/audio/**. AudioSettingsPort trong contracts không tự đổi.
- **Phụ thuộc triển khai:** N20
- **Ghép tích hợp / nghiệm thu:** N20, H03, I01
- **Điều kiện hoàn thành:** Có nhạc menu/lobby/credits, ngày, đêm 1–2, đêm 3–4, boss, đêm 5/extension, win và loss; chuyển đúng trigger, loop/one-shot đúng bảng 6.3, không chồng nhiều track sau đổi pha hoặc trở lại lobby.

<a id="n22"></a>

### N22 — Gắn đủ 25 cue SFX vào gameplay và UI

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** SFX-01–25
- **File / module sở hữu:** src/audio/**; audio/**; tests/audio/**. AudioSettingsPort trong contracts không tự đổi.
- **Phụ thuộc triển khai:** N20, H02
- **Ghép tích hợp / nghiệm thu:** N20, H02, I01
- **Điều kiện hoàn thành:** Checklist từng SFX-01 tới SFX-25 đạt trigger của bảng 6.3, gồm đánh/cung/hit/downed/revive/heal/farm/craft/build/cửa/base/dusk/radio/entry/quái/boss/drop/vote/helicopter/kết quả/UI/trụ. Cue quan trọng có visual pair, không phát trùng vì snapshot.

<a id="n23"></a>

### N23 — Kiểm thử gameplay theo MEC/FML và sửa các regression

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** FTR-01, TECH-PHY, FML-06–10
- **File / module sở hữu:** tests/gameplay/**; tests/SimCheck.cs (legacy runner của Hieu).
- **Phụ thuộc triển khai:** N02, N07, N10, N12, N13, N16
- **Ghép tích hợp / nghiệm thu:** N02, N07, N10, N12, N13, N16, I01
- **Điều kiện hoàn thành:** Có test biên sát thương, va chạm, immunity, channel, repair, crafting và revive/respawn; có case hai Engineer, vừa đánh vừa revive, sửa base ban đêm. Test phản ánh GDD thay vì chỉ xác nhận hành vi demo; không làm mất test geometry đang hữu ích. Kiểm tra sát thương qua cửa nguyên 30% tăng tới 60% khi hư, breach đánh base trực tiếp, base không tự hồi; đủ nhánh FML-07 và thứ tự channel.

<a id="n24"></a>

### N24 — Profile Demo/GDD và resource gameplay có ranh giới rõ

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** BAL-01–48, mục 3.4
- **File / module sở hữu:** config/gameplay/**; config/profiles/gameplay/**; balance aggregate/loader (Hieu); không sửa resource con AI/network/world.
- **Phụ thuộc triển khai:** H02, I01
- **Ghép tích hợp / nghiệm thu:** H01, H02, I01
- **Điều kiện hoàn thành:** Hieu làm profile loader/resource tổng hợp và chỉ sửa config gameplay/match/player/craft/repair do mình sở hữu. Resource con AI/waves/boss/world do Viet Anh sở hữu; ports/rooms/network/display do Ngon sở hữu, có schema/default mẫu từ H02/I01. Không bắt các bên chờ một file BAL chung được viết xong. Bản Demo giữ chỉnh sửa đã yêu cầu; profile GDD có đủ giá trị chuẩn sau khi ghép resource con, không trùng hai nguồn định nghĩa cùng một thông số. Log profile thực tế, không sửa default demo ngoài chủ ý.

## Nghiệm thu chung — dùng hệ thống thật

| ID | Ưu tiên | Task | Owner |
|---|---|---|---|
| [Q01](#q01) | P0 | Nghiệm thu vòng chơi qua mạng liên nền tảng | Pham Minh Hieu |
| [Q02](#q02) | P1 | Nghiệm thu toàn bộ nội dung và năm đêm | Vu Duc Viet Anh |
| [Q03](#q03) | P1 | Nghiệm thu network, disconnect và nhiều phòng | Mai Thien Ngon |
| [Q04](#q04) | P1 | Playtest bên ngoài và thu đủ H1–H16 | Vu Duc Viet Anh |
| [Q05](#q05) | P1 | Nghiệm thu tải server và client trên phần cứng mục tiêu | Vu Duc Viet Anh |
| [Q06](#q06) | P1 | Chốt nghiệm thu MVP và gói nộp/demo | Pham Minh Hieu |

<a id="q01"></a>

### Q01 — Nghiệm thu vòng chơi qua mạng liên nền tảng

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P0
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** FTR-01, M2
- **File / module sở hữu:** Bằng chứng trong reports/qa/<ID>/; test bổ sung đặt trong thư mục theo owner. Gửi lỗi cho owner, không sửa chéo source.
- **Phụ thuộc triển khai:** H08, H09, H21, N23, A02
- **Ghép tích hợp / nghiệm thu:** H08, H09, H21, N23, A02, I01
- **Điều kiện hoàn thành:** Chạy theo acceptance M2: hai người LAN gồm Windows/macOS; thêm phiên ba người để nghiệm thu FTR-01. Người thật farm, craft light armour, bảo vệ cửa, sửa và sang ngày 2; lưu build/seed/kết quả/video hoặc log, không dùng debug skip thay cả lượt chơi.

<a id="q02"></a>

### Q02 — Nghiệm thu toàn bộ nội dung và năm đêm

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** FTR-02, MEC-01–29, LVL-01–03
- **File / module sở hữu:** Bằng chứng trong reports/qa/<ID>/; test bổ sung đặt trong thư mục theo owner. Gửi lỗi cho owner, không sửa chéo source.
- **Phụ thuộc triển khai:** A23, A20, N08, N14, H17, H20
- **Ghép tích hợp / nghiệm thu:** A23, A20, N08, N14, H17, H20, I01
- **Điều kiện hoàn thành:** Có ma trận thắng/thua ở ba mode, các đường mất base/team wipe/boss còn sống/extraction thất bại, đêm 2/4 không trùng boss, đêm 5 cứu cả dead/downed. Tất cả item MVP và tower/bot có case; ghi rõ điều gì chạy debug, điều gì chơi đủ trận.

<a id="q03"></a>

### Q03 — Nghiệm thu network, disconnect và nhiều phòng

- **Owner:** Mai Thien Ngon
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-NET, BAL-46, BAL-48
- **File / module sở hữu:** Bằng chứng trong reports/qa/<ID>/; test bổ sung đặt trong thư mục theo owner. Gửi lỗi cho owner, không sửa chéo source.
- **Phụ thuộc triển khai:** H07, H11, H22, A19
- **Ghép tích hợp / nghiệm thu:** H07, H11, H22, A19, I01
- **Điều kiện hoàn thành:** Năm client LAN: base/cửa nhất quán trong 0.2 s, enemy sai khác <0.5 ô; client ngoài LAN mở list ≤3 s, room update ≤1 s. Ba phòng chạy đồng thời, room thứ tư bị từ chối; leader/client rời freeze ≤1 s, vote/resume đúng; server dừng về menu ≤5 s; test leaver liên tiếp và hết người.

<a id="q04"></a>

### Q04 — Playtest bên ngoài và thu đủ H1–H16

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** M4, mục 7.8, bảng 3.2–3.3
- **File / module sở hữu:** Bằng chứng trong reports/qa/<ID>/; test bổ sung đặt trong thư mục theo owner. Gửi lỗi cho owner, không sửa chéo source.
- **Phụ thuộc triển khai:** Q01, Q02, Q03, H24
- **Ghép tích hợp / nghiệm thu:** Q01, Q02, Q03, H24, I01
- **Điều kiện hoàn thành:** Ít nhất sáu người ngoài team, nhóm 3–5, test khả năng hiểu mục tiêu và full-match Medium rồi Easy/Hard. Thực hiện sample/target từng H trong checklist cuối file, ghi observations/metrics/result và issue; chưa đủ sample ghi Pending, không ghi Pass bằng cảm giác.

<a id="q05"></a>

### Q05 — Nghiệm thu tải server và client trên phần cứng mục tiêu

- **Owner:** Vu Duc Viet Anh
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** TECH-ENG, TECH-AI, TECH-NET, bảng 7.1
- **File / module sở hữu:** Bằng chứng trong reports/qa/<ID>/; test bổ sung đặt trong thư mục theo owner. Gửi lỗi cho owner, không sửa chéo source.
- **Phụ thuộc triển khai:** A05, A23, H22, H26
- **Ghép tích hợp / nghiệm thu:** A05, A23, H22, H26, I01
- **Điều kiện hoàn thành:** Đo ba match đồng thời trên VM 4 vCPU/4 GB; mỗi match giữ 60 physics ticks/s với kịch bản tải của bảng 7.1, đồng thời tuân cap thực thể đã chốt. Client Windows i5-13420H/16GB/RTX2050 và Mac M1/8GB giữ 60 FPS. Log tick/frame time, CPU/RAM; nếu dùng fallback cap40 hoặc một room phải ghi CHG và nghiệm thu lại.

<a id="q06"></a>

### Q06 — Chốt nghiệm thu MVP và gói nộp/demo

- **Owner:** Pham Minh Hieu
- **Ưu tiên:** P1
- **Trạng thái khởi tạo:** Backlog — cần bổ sung/sửa hoặc nghiệm thu từ code hiện có
- **GDD:** M3, M4, M5, FTR-01–04, TECH
- **File / module sở hữu:** Bằng chứng trong reports/qa/<ID>/; test bổ sung đặt trong thư mục theo owner. Gửi lỗi cho owner, không sửa chéo source.
- **Phụ thuộc triển khai:** N21, N22, H18, H20, H23, H25, Q01, Q02, Q03, Q05, A24
- **Ghép tích hợp / nghiệm thu:** N21, N22, H18, H20, H23, H25, Q01, Q02, Q03, Q05, A24, I01
- **Điều kiện hoàn thành:** Mỗi acceptance trong bảng 7.5/7.3 có bằng chứng hoặc CHG được nhóm chấp thuận; lỗi playtest đã sửa/ghi nhận, không giấu test fail. Đủ build/README/credits/asset log/báo cáo cập nhật và Git tag phiên bản demo/nộp; máy Windows/macOS sạch chạy được.

## Checklist chi tiết của card Q04 / A24: H1–H16

Mỗi mục cần có số đo, sample thực tế, kết quả Pass/Fail/Pending và link issue/CHG nếu khác target. Không cần 16 card độc lập ngay từ đầu; tách thành subtask khi tổ chức playtest.

| ID  | Nội dung                                | Sample và điều kiện trong GDD                                                                                                                                                                             |
| --- | ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| H1  | Team size công bằng                    | 5 session n=3 và 5 session n=5; HP base mất đêm 3–5 của đội ba người ≤1.15× đội năm người.                                                                                                   |
| H2  | Chuẩn bị có tác dụng                | Tất cả boss nights: đội nâng cấp giết trước lúc cửa vỡ; đội không nâng cấp không đạt. Kèm target bảng 3.2: đêm 2 trước breach ≥70% khi có repair, đêm 4 kill ≥60% lượt thử. |
| H3  | Bảy boss tương đương               | 14 cấu hình (7 boss × 2 đêm), mỗi cấu hình 3 lượt; chênh TTK ≤15% và win-rate ≤15 điểm phần trăm.                                                                                           |
| H4  | Kinh tế buộc lựa chọn                | 10 session; thu/chi đến ngày 3 trong 0.75–0.85; không recipe nào được mọi người craft trong >70% session.                                                                                         |
| H5  | Sống sót trước Grunt                 | Telemetry: Grunt cần 8–10 hit để down một player theo giả thuyết.                                                                                                                                      |
| H6  | Thời gian downed hợp lý               | Telemetry 10 đêm: downed time <15% thời lượng đêm.                                                                                                                                                     |
| H7  | Base không thành đường thoát hiểm | Quan sát: không có ca né chết nhờ lạm dụng entry vào base.                                                                                                                                           |
| H8  | Breach còn cứu được                 | 10 session: có breach vẫn thắng đêm ≥30%; breach đêm 1–2 trong ≤20% session.                                                                                                                        |
| H9  | Tập trung một pad có ích             | Mọi extension: đội tập trung thắng ≥50%, đội tản mỏng ≤20%.                                                                                                                                        |
| H10 | Ba cửa công bằng                      | 15 lượt night 1: HP base mất giữa các cửa lệch ≤15%.                                                                                                                                                  |
| H11 | Kiting có giới hạn                    | Boss bị kéo vẫn tới cửa trong ≤1.5× thời gian đi thẳng.                                                                                                                                             |
| H12 | Drop là phần thưởng phụ             | Telemetry 10 session: drops chiếm 5–15% tổng tài nguyên.                                                                                                                                                 |
| H13 | Rủi ro ban ngày hợp lý               | 10 session: downed ≤20% chuyến outer zone; chết ban ngày ≤20% session; metal item đầu có trước/ngày 3 trong ≥80% session.                                                                         |
| H14 | Các mode khác biệt                    | 5 session/mode: Easy win ≥75%, Medium 50–70%, Hard 25–40%.                                                                                                                                                 |
| H15 | Bot bù được người rời             | 5 session có một người rời: với một bot từ đêm 2 ở Medium, tới night 5 ≥50%; không có phản ánh bot chặn cửa/lãng phí tài nguyên.                                                       |
| H16 | Ánh sáng trụ đáng bảo vệ          | 10 session: đêm 3–5 có ít nhất một trụ đứng ≥50% đêm trong ≥60% session; trụ làm 10–25% kills đêm.                                                                                         |

## Checklist cue cho N19 / N21 / N22

Mỗi cue có: file/nguồn/licence, event trigger, âm lượng/bus, visual pair nếu là cue quan trọng, và kết quả test trong build. Trigger/nguồn chi tiết đối chiếu bảng 6.3.

| ID     | Trigger                                |
| ------ | -------------------------------------- |
| MUS-01 | Menu, lobby, credits                   |
| MUS-02 | Ban ngày                              |
| MUS-03 | Đêm 1–2                             |
| MUS-04 | Đêm 3–4                             |
| MUS-05 | Boss còn sống                        |
| MUS-06 | Đêm 5 và extension                  |
| MUS-07 | Loss, không loop                      |
| MUS-08 | Win, không loop                       |
| SFX-01 | Melee swing/hit                        |
| SFX-02 | Bow shot/arrow hit                     |
| SFX-03 | Player nhận damage                    |
| SFX-04 | Downed và bleed-out 10 giây cuối    |
| SFX-05 | Revive đang tiến hành/hoàn tất    |
| SFX-06 | Bandage/flask                          |
| SFX-07 | Farm wood/stone/metal/herb             |
| SFX-08 | Craft/build hoàn thành               |
| SFX-09 | Cửa bị đánh: intact/damaged        |
| SFX-10 | Cửa vỡ/breach                        |
| SFX-11 | Repair cửa/trụ và ngắt repair      |
| SFX-12 | Generator bị đánh                   |
| SFX-13 | Dusk siren                             |
| SFX-14 | Dawn radio crackle                     |
| SFX-15 | Engineer vào/ra cửa                  |
| SFX-16 | Grunt growl/Spitter hiss/Wrecker steps |
| SFX-17 | Quái bị hạ                          |
| SFX-18 | Boss arrival                           |
| SFX-19 | Boss telegraph 1 giây                 |
| SFX-20 | Drop xuất hiện/nhặt                 |
| SFX-21 | Bỏ phiếu skip                        |
| SFX-22 | Helicopter tiếp cận/hạ cánh        |
| SFX-23 | Loss: đèn tắt/radio static          |
| SFX-24 | UI hover/click                         |
| SFX-25 | Trụ bắn/bị phá/đèn tắt          |

## Đối chiếu yêu cầu chính với card

| Yêu cầu                                  | Card chính                                           |
| ------------------------------------------ | ----------------------------------------------------- |
| MEC-01–05: movement/combat/role kits/heal | N01, N02, N08, N09                                    |
| MEC-06–07: downed/dead/revive             | N10, N11, H16                                         |
| MEC-08–10: gather/inventory/craft         | A17, N03–N07                                         |
| MEC-11–13: build/repair                   | N12–N15                                              |
| MEC-14–17: dusk/entry/breach/door damage  | H03, N16, N23                                         |
| MEC-18: day skip                           | H03, N24                                              |
| MEC-19–20: director/boss briefings        | A01–A14, H19                                         |
| MEC-21: vision                             | H15                                                   |
| MEC-22: extraction                         | H17, H18                                              |
| MEC-23: drops                              | A18, N03                                              |
| MEC-24: lobby gates                        | H08                                                   |
| MEC-25: unlocks                            | H03, N07                                              |
| MEC-26: roamers                            | A06                                                   |
| MEC-27: timeout/bot takeover               | H10, H11, A19, A20, N05                               |
| MEC-28: towers                             | N12, N15, A04, H14, H15                               |
| MEC-29: finding rooms                      | H04–H08, H22                                         |
| FML-01–05                                 | A01–A03, A07–A14, A23, A24                          |
| FML-06–10                                 | N02, N10, N12, N23, A04                               |
| FML-11                                     | A17, A24, H24, Q04                                    |
| FML-12–14                                 | H15, H17, H03                                         |
| FML-15–16                                 | A18, A06                                              |
| ART-01–03                                 | G01 (Viet Anh chuẩn bị), N17/A22/A15 (Ngon enhance) |
| ART-04–06                                 | A16, N18                                              |
| ART-07–08                                 | N02, N08, N17, A07–A15, H12, H14, H15                |
| ART-09 / narrative                         | H18, H19                                              |
| FTR-01–04 và TECH                        | Q01–Q06, N19–N23, H01–H26, A23                     |
| H1–H16 / acceptance evidence              | H24, Q04, A24                                         |

## Stretch goals — tách khỏi 83 card bắt buộc

Chỉ đưa vào board khi MVP đã được nghiệm thu hoặc nhóm chủ động đổi phạm vi:

- NOX files / ITM-17 và màn đọc lore.
- Rejoin match đang diễn ra.
- Automatic matchmaking.
- Private room có mật khẩu.
- Linux client (khác với headless Linux server đã nằm trong GDD).
- Web build.
- Gamepad support.

## Lưu ý các thay đổi hiện tại so với GDD

- Giữ các chỉnh sửa demo đã được yêu cầu (skip ngay, dusk 5 s, quái chậm/đến sớm/spawn quanh đảo) trong profile Demo. N24 chuẩn bị baseline riêng để nghiệm thu, không tự xóa sở thích thử nghiệm của người dùng.
- Một số phần đang dùng dictionary/custom draw/custom collision/AStarGrid thay cho scene entity/CharacterBody2D/Area2D/NavigationAgent2D/Spawner/Synchronizer như TECH yêu cầu. H01/H02 chốt cách tuân thủ hoặc CHG trước khi ba người cùng sửa; nếu chỉ giữ bản tự viết mà không cập nhật cam kết, chưa thể đánh dấu TECH Done theo bản GDD gốc.
- Hiện repo chưa có bằng chứng nghiệm thu Windows–macOS/Internet/ba room/target hardware hoặc kết quả H1–H16. Không giả định chúng đã hoàn thành chỉ vì chạy được ở máy phát triển.
- Phụ thuộc và độ ưu tiên là đề xuất kỹ thuật của backlog này; yêu cầu/giá trị nghiệm thu lấy từ GDD. Khi task quá lớn, chia nhỏ bằng subtask giữ nguyên ID GDD, không bỏ phần acceptance.
