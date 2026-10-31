# Mobile Companion — Direction Spec (DRAFT)

> **Trạng thái:** **FROZEN 2026-10-31** (owner chốt M1–M5c và OD-M1..M4). Không có production code nào được viết từ spec này.
> **Nguồn:** thảo luận M1–M5 ngày 2026-10-31 (thread "Định hướng bản mobile").
> **Nguyên mẫu UI (đã duyệt 2026-10-31):** https://claude.ai/artifact/JNKzyosJTrEeqaw1AMgwv3
> **Thay thế:** mục *"Mobile / hybrid clients — revisit after LAN sync lands"* trong `system_roadmap.md` (deferred list).

---

## 1. Goal

Một app Android **Companion** cho phép sinh viên xem lịch hôm nay, check-in, quick-add task và chạy Focus khi rời PC, đồng bộ với desktop qua LAN, chi phí vận hành 0 đồng.

## 2. Decision record

| ID | Quyết định | Lý do chính | Phương án đã loại |
|---|---|---|---|
| **M1** | Mobile = **Companion**. ML training, SOE optimize, analytics sâu chỉ ở desktop. | Ít việc nhất, giữ giá trị E1–E3 ở desktop, ML.NET train kém trên mobile. | Full parity; Mobile-first. |
| **M2** | Sync pha 1 = **chỉ LAN** (chung Wi-Fi). | Ràng buộc: **không có ngân sách** cho server 24/7. | Relay tự host; BaaS; cloud-drive mailbox (để dành). |
| **M3** | Framework = **Avalonia**, chỉ **Android**. | Tùy biến UI cao (Skia), XAML gần WPF, dùng lại C# và merge engine E2. iOS cần Mac + 99$/năm. | .NET MAUI (tùy biến UI yếu); Flutter/RN (phải viết lại E2). |
| **M4** | Tính năng nhóm / chia việc: **hoãn**. | Cần server online, mâu thuẫn ràng buộc 0 đồng. | — |
| **M5a** | Điều hướng **Today-first, 3 tab**: Hôm nay / Deadline / Focus. FAB quick-add, vuốt để check-in. | Đúng vai trò Companion, ít màn nhất. | Mirror 5 tab desktop; Notification-first (để làm add-on). |
| **M5b** | Quick-add trên phone → **lịch tạm** trên phone (heuristic, ML tắt); desktop xếp lại khi sync. | Phản hồi ngay, desktop vẫn là nguồn lịch chính thức. | Chỉ ghi task; phone xếp lịch đầy đủ có ML. |
| **M5c** | **Token chung** (`design-tokens.json` → sinh theme WPF + Avalonia), phủ lên một thư viện component có sẵn. Font **Inter**. | Nhận diện đồng bộ, nhanh, token tái dùng nếu desktop chuyển Avalonia. | Theme mobile riêng; Figma trước. |

## 3. Scope

**In scope (MVP mobile):**
- Tab *Hôm nay*: timeline phiên học + deadline trong ngày, check-in (vuốt), trạng thái sync.
- Tab *Deadline*: danh sách task mở theo nhóm thời gian, nhãn mức độ từ `Core/Risk`.
- Tab *Focus*: bộ đếm phiên, kết thúc → ghi `StudyLog` (check-in).
- Quick-add task (FAB) + lịch tạm (M5b).
- Thông báo cục bộ: nhắc phiên sắp tới, deadline gần.
- Sync LAN với desktop qua engine E2.

**Out of scope (MVP):** xem §6 Non-goals.

## 4. Contracts và invariant

### 4.1 Cấu trúc solution (tiền đề, làm trước mọi việc mobile)

```text
SmartStudyPlanner.Core      net10.0, không tham chiếu WPF/Avalonia/ML.NET training
  Models, Core/Scheduling, Core/Risk, Core/Parsing, Sync (merge engine), Data (EF Core SQLite)
SmartStudyPlanner           WPF desktop (giữ tên hiện tại), tham chiếu Core
SmartStudyPlanner.Mobile    Avalonia Android, tham chiếu Core
tools/TokenGen              design-tokens.json → Wpf ResourceDictionary + Avalonia .axaml
```

- **INV-1:** `SmartStudyPlanner.Core` không được tham chiếu `System.Windows.*`. (Hiện có 6 ViewModel + `ThemeManager` vi phạm, phải tách trước.)
- **INV-2:** ML trong Core chỉ qua contract (`Core/ML/Contracts`). Mobile cài implementation "ML off" (heuristic fallback).

### 4.2 Sync (ràng buộc lên T2.1)

- **C-1:** T2.1 định nghĩa `ISyncTransport`; pha 1 có đúng một implementation `LanTransport`.
- **C-2:** Discovery: mDNS **và** nhập IP peer thủ công. (Nhập IP thủ công giúp LAN-over-Tailscale chạy sau này mà không cần code mới.)
- **C-3:** Lịch (schedule) là **dữ liệu dẫn xuất**, không nằm trong `SyncEntityTypes`, không đi qua merge E2. Mỗi máy tự tính lịch từ entity đã sync.
- **C-4:** Lịch tạm trên phone mang cờ `IsProvisional`, lưu cục bộ, bị thay thế khi nhận lịch/entity mới từ desktop.
- **C-5:** Phone chỉ ghi các entity đã có trong `SyncEntityTypes` (`StudyTask`, `StudyLog`, `TaskNote`...). Không thêm entity type mới cho mobile ở MVP.

### 4.3 UI

- **U-1:** Mọi màu/cỡ chữ/bo góc/khoảng cách lấy từ `design-tokens.json`. Không hard-code hex trong view.
- **U-2:** Token giữ tên ngữ nghĩa khớp key hiện có (`AppBackground`, `CardBackground`, `AccentColor`, `SubjectColor1..6`, `Severity*`) để WPF chuyển sang không vỡ binding.
- **U-3:** Vùng chạm mobile ≥ 48dp (`density.touch`); desktop `density.compact`.
- **U-4:** Lịch tạm hiển thị viền nét đứt + nhãn "Lịch tạm".
- **U-5:** Text đạt tương phản 4.5:1 ở cả Light và Dark.

## 5. Acceptance criteria (cho spec này, không phải cho code)

- [x] Owner xác nhận bảng M1–M5c.
- [x] Owner chốt các OD ở §7 (2026-10-31: M1 A, M2 A, M3 A, M4 A).
- [ ] `system_roadmap.md` cập nhật: mobile không còn "revisit", trỏ tới spec này; group feature ghi deferred.
- [ ] Prompt T2.1 (khi viết) có C-1, C-2, C-3.

## 6. Non-goals

- iOS, tablet layout, widget màn hình chính (pha sau).
- Train ML / chạy SOE / analytics sâu trên phone.
- Sync ngoài LAN (Tailscale, cloud-drive mailbox, relay) ở MVP.
- Tính năng nhóm, chia việc, nhiều người dùng.
- Chuyển desktop sang Avalonia (để ngỏ, quyết định riêng).

## 7. Open decisions (cần owner chốt trước khi freeze)

### OD-M1 — Ghép cặp và bảo mật kênh sync LAN — **CHỐT 2026-10-31: A (QR + TLS ghim cert)**

**Vấn đề.** Ở pha 1, sync chạy trên Wi-Fi, thường là mạng chung (ký túc xá, quán cà phê, trường). Nếu không có bảo mật:
- Máy lạ dò mDNS sẽ thấy app và **đọc được** lịch, task cá nhân vì dữ liệu đi dạng plaintext.
- Máy lạ **đẩy được change-set giả**. Engine E2 sẽ merge nó như dữ liệu thật, phá hỏng đúng thứ mà fence E2 đang bảo vệ.

**Phương án.**
| | Cách làm | Ưu | Nhược |
|---|---|---|---|
| **A (đề xuất)** | Desktop hiện **QR** gồm `DeviceId`, `IP:port` và fingerprint của chứng chỉ tự ký. Phone quét QR, hai bên lưu danh tính của nhau (trust-on-first-pair). Kênh truyền là **TLS dùng `SslStream` có sẵn trong .NET**, ghim fingerprint. | An toàn thật sự, không tự chế crypto, chỉ quét 1 lần. Nhập IP thủ công (C-2) có sẵn trong QR. | Cần quyền camera và thư viện quét QR (ZXing.Net). Phải làm màn "thiết bị đã ghép cặp / thu hồi". |
| B | Desktop hiện **PIN 6 số**, nhập trên phone. | Không cần camera. | PIN ngắn dễ bị brute-force nếu không dùng PAKE (SPAKE2), mà PAKE thì phải tự cài hoặc thêm thư viện ít phổ biến. Dễ làm sai. |
| C | Không bảo mật, chỉ sync trên Wi-Fi do người dùng đánh dấu "tin cậy" (SSID). | Đơn giản nhất. | Dữ liệu vẫn đi plaintext. SSID giả mạo được. Không phù hợp với ký túc xá. |

**Hệ quả lên contract nếu chọn A:**
- Mỗi `PeerChangeSet` gắn `DeviceId` của peer đã ghép cặp.
- Peer chưa ghép cặp bị từ chối **ở tầng transport**, trước khi dữ liệu tới merge engine. Merge engine không cần biết gì về bảo mật.
- Sau này dùng Tailscale vẫn giữ TLS (phòng thủ nhiều lớp).

### OD-M2 — Thư viện component nền cho Avalonia — **CHỐT 2026-10-31: A (Fluent có sẵn + token)**

**Bối cảnh.** Các control đặc thù mobile trong nguyên mẫu (bottom tab, vuốt để check-in, FAB, vòng đếm Focus) **không thư viện nào có sẵn đúng ý**, đằng nào cũng phải tự làm. Câu hỏi thật sự là: lấy nền nào cho các control cơ bản (button, textbox, list, dialog), rồi phủ token lên trên.

| | Nền | Ưu | Nhược |
|---|---|---|---|
| **A (đề xuất)** | **Fluent theme có sẵn của Avalonia** + token của mình | 0 phụ thuộc ngoài, luôn tương thích khi nâng Avalonia, token override trực tiếp. | Mặc định trông "Windows", phải tự style lại nhiều hơn để ra cảm giác Android. |
| B | **Material.Avalonia** (cộng đồng) | Cảm giác Android tự nhiên, có ripple, card, một số control mobile. | Thêm phụ thuộc cộng đồng, có thể chậm theo phiên bản Avalonia mới. Muốn override token phải hiểu cách nó tổ chức theme. Look Material có thể lệch với desktop. |
| C | **FluentAvalonia** | Control phong phú kiểu WinUI (NavigationView, InfoBar...). | Thiên về desktop, ít giá trị trên Android. Chỉ đáng dùng khi chuyển desktop sang Avalonia. |

### OD-M3 — Thông báo do máy nào tính, theo luật nào — **CHỐT 2026-10-31: A (phone tự tính)**

**Bối cảnh.** Thông báo là lý do chính để có Companion, nhưng chỉ có LAN sync: khi ở ngoài, phone không hỏi được PC.

| | Cách làm | Ưu | Nhược |
|---|---|---|---|
| **A (đề xuất)** | Phone **tự tính** nhắc nhở từ lịch và deadline đã sync, lên lịch cục bộ bằng `AlarmManager`/`WorkManager`, tính lại sau mỗi lần sync, check-in hoặc quick-add. | Chạy offline, PC tắt vẫn nhắc. Không cần entity sync mới (giữ C-5). | Nhắc dựa trên dữ liệu từ lần sync cuối. Phải xử lý giới hạn của Android (xem dưới). |
| B | **Desktop tính** danh sách reminder rồi sync xuống. | Logic nằm một chỗ. | Phải thêm entity type mới (vi phạm C-5), vẫn bị cũ như A, và phone mất khả năng tự cập nhật khi quick-add. |
| C | Chỉ nhắc khi app đang mở. | Đơn giản. | Mất phần lớn giá trị Companion. |

**Luật nhắc mặc định đề xuất (cho A, chỉnh được trong cài đặt):**
- Phiên học: 10 phút trước giờ bắt đầu.
- Deadline: 24 giờ và 2 giờ trước hạn.
- Không nhắc trong lúc đang Focus. Gộp các nhắc trùng giờ.

**Lưu ý kỹ thuật Android:**
- Từ Android 12, alarm chính xác cần quyền `SCHEDULE_EXACT_ALARM`. Đề xuất: dùng inexact cho deadline, exact chỉ cho nhắc phiên học.
- Phải đăng ký lại alarm sau khi máy khởi động lại (`BOOT_COMPLETED`).
- Từ Android 13, cần xin quyền `POST_NOTIFICATIONS`.

### OD-M4 — Thứ tự so với roadmap hiện tại — **CHỐT 2026-10-31: A (song song có kiểm soát)**

**Bối cảnh.**
- E2 đang chạy: fence Slice 5 có PR #113 và review #114 chưa merge, Slice 6 chờ OD-1.
- Việc gợi ý tiếp theo là G3-1 Phase 0.
- Backlog có sẵn mục "UI mobile-ready polish".
- P0 (tách Core) là refactor chạm rất nhiều file, nên dễ **conflict** với các PR fence đang mở.

| | Thứ tự | Ưu | Nhược |
|---|---|---|---|
| **A (đề xuất)** | Merge #113/#114 xong thì làm **P0 song song E2**, chia slice nhỏ xen giữa các slice fence. **P2 TokenGen làm bất cứ lúc nào**, vì nó độc lập và làm đẹp luôn desktop (gộp với mục UI polish). **P3 mobile shell chờ T2.1 xong.** | Mobile tiến dần mà không chặn E2. Có lợi ích cho desktop sớm (token). | Phải điều phối để P0 không đụng file mà fence đang sửa. |
| B | Tuần tự: xong E2 (T2.1–T2.5), rồi G3-1, rồi mới tới mobile. | An toàn nhất, không conflict. | Mobile bắt đầu rất muộn. |
| C | Làm mobile ngay (P0, P2, P3 với dữ liệu giả), sync tính sau. | Sớm có app để xem, tạo động lực. | UI dựng trước khi contract sync ổn định thì dễ phải làm lại. Conflict với E2 nặng nhất. |

## 8. Phân pha (theo OD-M4 = A)

Bối cảnh lúc chốt: #113/#114 đã merge; G3-1 Phase 0 đã có prompt, là recon nên không viết production code; E2 Slice 6 đang chờ OD-1.

| Pha | Nội dung | Khi nào / phụ thuộc |
|---|---|---|
| P2 | `tools/TokenGen` + chuyển theme WPF sang token sinh ra (U-1, U-2). Chỉ đụng `Themes/` và `tools/`. | **Ngay**, song song với G3-1 Phase 0. |
| P0 | Tách `SmartStudyPlanner.Core` (INV-1, INV-2), không đổi hành vi, test giữ xanh. **Một PR làm trong "cửa sổ đóng băng"**: không có PR code nào khác mở trong lúc này. **Giữ nguyên namespace**, chỉ đổi project, để git nhận diện rename. | Sau P2 và G3-1 Phase 0. **Trước** G3-1 Phase 1 và E2 Slice 6/T2.1. |
| P1 | T2.1 `ISyncTransport` + `LanTransport` + discovery (C-1, C-2) + ghép cặp QR/TLS (OD-M1). | Sau P0 và fence E2. |
| P3 | Mobile shell Avalonia (Fluent + token, OD-M2): 3 tab, đọc dữ liệu đã sync. | Sau P0, P1, P2. |
| P4 | Ghi từ phone: check-in, quick-add, Focus, lịch tạm (C-4, C-5). | Sau P3. |
| P5 | Thông báo cục bộ do phone tự tính (OD-M3). | Sau P3. |
| Sau | Tailscale, thông báo có nút bấm, widget, tính năng nhóm. | — |

**Lưu ý:** báo cáo G3-1 Phase 0 trích đường dẫn theo cấu trúc cũ, sau P0 cần map lại.
