# Đặc tả gán nhãn chuẩn — kho ngữ liệu tác vụ SmartStudyPlanner

**Bản dịch tiếng Việt của `GuidelineVersion: v1`.**

> **Đây là bản dịch để đọc cho dễ, không phải bản gốc.** Bản gốc là tệp
> `01-annotation-guideline-v1.md` (tiếng Anh). Bản dịch này được tạo ra từ đúng nội dung đó và cố
> gắng bám sát từng câu. **Nếu hai bản có chỗ nào hiểu khác nhau, bản gốc tiếng Anh là bản có
> hiệu lực.** Bản dịch chưa được chủ sở hữu phê duyệt.
>
> Bản dịch **không thêm quy tắc mới, không thêm ví dụ, không làm rõ những chỗ bản gốc còn mơ hồ.**
> Chỗ nào bản gốc nói chưa rõ thì bản dịch cũng để nguyên như vậy — đó là chủ ý.

**Những chữ sau đây được giữ nguyên, không dịch**, vì chúng là *tên gọi* chứ không phải lời văn:
năm tên lớp (`BaiTapVeNha`, `KiemTraThuongXuyen`, `ThiGiuaKy`, `ThiCuoiKy`, `DoAnCuoiKy`), tên các
trường cần điền (`TaskType`, `Difficulty`, `decided_by`, `unresolved`), mã các quy tắc (`B-1`–`B-6`)
và số hiệu phiên bản `v1`. Những từ tiếng Việt đặt trong dấu nháy ngược như `giữa kỳ`, `cuối kỳ`,
`kiểm tra`, `đồ án`, `bài tập lớn`, `nhóm` là **chuỗi chữ cần tìm trong đoạn văn**, nên cũng giữ
nguyên.

---

**`GuidelineVersion: v1`** · soạn ngày 2026-09-04 ·
**Trạng thái: `đã phê duyệt để đem ra đo — đã đóng băng`. Chưa được kiểm chứng.**

> **Được phê duyệt để đem ra đo, không có nghĩa là đã được kiểm chứng.** Chủ sở hữu đã phê duyệt
> `v1` vào **2026-09-04** như là **bản đặc tả cố định mà phép thử tính tái lập sẽ chạy trên đó**.
> Phê duyệt chỉ có nghĩa là bản đặc tả đã được giữ cố định để phép thử đo được nó — **nó không có
> nghĩa là S-2 đã đạt, và chưa có con số nào của §10 tồn tại cả.** Xem §14.

Đây là sản phẩm bàn giao của S-2: **tài liệu làm cho một cái nhãn có thể tái lập được.** Nó chứa
giao ước về ngữ nghĩa, các quy tắc ranh giới, các mốc Độ khó, danh mục ví dụ khó phân định, quy
trình phân xử, các yêu cầu về nguồn gốc, và ngữ nghĩa phiên bản của chính nó.

**Nó chưa được đem ra đo.** Phép thử tính tái lập (§10) đã được đăng ký trước và đã được cho phép,
nhưng **chưa được thực hiện**. Không chỗ nào ở đây được mô tả là đã kiểm chứng, và **§14 cấm sửa
bất cứ phần nào sau khi đã nhìn thấy kết quả.**

| | |
|---|---|
| **Kế hoạch chi phối** | Data Maturation Proposal rev 3, cho phép ngày 2026-09-04 |
| **Các quyết định đã phê duyệt** | Stage Decision Outcomes — S-2.1 … S-2.12 |
| **Nguồn gốc của §4 và §5** | S-1 Limited taxonomy review, chấp nhận ngày 2026-09-04 |
| **Quy tắc chọn danh mục** | S-2 catalogue pre-registration, cam kết trước khi chọn |
| **Các dòng đã giữ riêng** | S-0 snapshot — đã niêm phong, và **bị loại khỏi mọi ví dụ bên dưới** |

---

## 1. Công bố về phần do AI soạn (S-2.12, DFD-8)

Theo S-2.12, bản đặc tả này phải nói rõ mục nào do AI soạn hoặc có AI hỗ trợ. Theo lời chủ sở hữu
nói thêm, **đây là thông tin về nguồn gốc và minh bạch — không phải điểm đánh giá chất lượng hay độ
tin cậy.**

| Mục | Nguồn gốc |
|---|---|
| §2 Hệ phân loại · §3 Trình tự quyết định · §4 Quy tắc ranh giới | **Do AI soạn.** B-1 và B-4 của §4 chép lại các quy tắc đã chốt trong S-1 §4.2; phần còn lại là soạn mới |
| §5 Các mốc Độ khó | **Do AI soạn trong S-1, chủ sở hữu chấp nhận 2026-09-04**, đưa vào đây **nguyên văn** (ghi chú §5) |
| §6 Danh mục ví dụ khó phân định | **Chọn bằng máy** theo một quy tắc đăng ký trước; phần bình luận cho từng ranh giới là **do AI soạn**. Phần chữ của mỗi dòng được **trích nguyên văn từ kho ngữ liệu, không phải do soạn ra** |
| §7 Các lỗ hổng của danh mục | **Đo được**, không phải soạn ra |
| §8 Phân xử · §9 Nguồn gốc nhãn · §11 Đánh phiên bản · §12 Mẫu bản ghi | **Do AI soạn** từ các quyết định đã phê duyệt S-2.8 / S-2.10 / S-2.11 / S-2.12 |
| §10 Phép thử tính tái lập | Chép lại **các tham số đã phê duyệt** (S-2.1–S-2.7); **chưa thực hiện** |

**Cái vòng luẩn quẩn mà công bố này tồn tại để đánh dấu:** kho ngữ liệu mà bản hướng dẫn này chi
phối là `[measured]` **do AI sinh ra — 0 dòng thật** (Data Audit Phase 0), và bản hướng dẫn này cũng
do AI soạn. Một bản hướng dẫn được viết bởi cùng loại hệ thống đã tạo ra những cái nhãn mà nó chi
phối thì không thể mặc nhiên coi là độc lập với chúng. **Phép dò tính tái lập bằng người ở §10 chính
là chốt chặn cho chuyện đó**, và đó là lý do S-2.2 chỉ cho phép AI làm phép dò bổ sung, không bao
giờ được thay thế.

---

## 2. Hệ phân loại

**Năm lớp chính thức.** Được thiết lập bởi S-1.0/S-1.1 cùng S-1 §4.1, chấp nhận ngày 2026-09-04.

| Lớp | Nghĩa là |
|---|---|
| **`BaiTapVeNha`** | **Một phần việc học duy nhất** mà sinh viên làm và nộp |
| **`KiemTraThuongXuyen`** | **Một bài đánh giá ngắn, lặp lại, làm trong giờ học** — bài ngắn, bài 15 phút, bài hằng tuần |
| **`ThiGiuaKy`** | **Kỳ thi giữa học kỳ** đã được xếp lịch |
| **`ThiCuoiKy`** | **Kỳ thi cuối học kỳ** đã được xếp lịch |
| **`DoAnCuoiKy`** | **Một sản phẩm nhiều phần, làm trong nhiều tuần** — `đồ án`, `bài tập lớn`, `BTL`, *project* |

**`OnTap` và `NhacNho` không phải là lớp chính thức** và không quay trở lại (S-1.1).

**Tính chất "là lời nhắc" là một thuộc tính suy ra và là một lớp vỏ bề mặt, không phải một lớp tác
vụ.** Một dòng có thể *là* `BaiTapVeNha` và *mang theo* một lời nhắc; lời nhắc không phải là lớp
của nó. **Điều này đã chốt — đừng đem ra bàn lại như thể đang thiếu một lớp.**

**Mỗi dòng nhận đúng một trong năm lớp.** Không có `Khac`, không có "khác", không có giá trị rỗng.
Nếu không lớp nào hợp, thì đó là một phát hiện về hệ phân loại và phải được nêu lên như một phát
hiện, chứ không được nuốt vào một lớp dự phòng.

---

## 3. Trình tự quyết định

Áp dụng theo thứ tự. Dừng lại ở nhánh đầu tiên giải quyết được.

1. **Tác vụ này là một buổi thi, hay là phần việc sinh viên làm ra và nộp?**
   Tân ngữ của câu quyết định điều này, chứ không phải những từ ngữ xung quanh nó.
2. **Nếu là một buổi thi** — **hãy tìm dấu hiệu kỳ học trước đã.**
   - Có dấu `giữa kỳ` / `GK` → **`ThiGiuaKy`**
   - Có dấu `cuối kỳ` / `CK` / kỳ thi kết thúc → **`ThiCuoiKy`**
   - **Không có dấu hiệu kỳ học nào**, và bài đánh giá đó lặp lại, ngắn hoặc thường lệ →
     **`KiemTraThuongXuyen`**

   Bước tìm dấu hiệu phải làm **trước**, theo quy tắc B-4. Đảo ngược hai bước này sẽ đẩy
   `bài kiểm tra giữa kỳ` sang `KiemTraThuongXuyen`, điều mà B-4 cấm.
3. **Nếu là phần việc làm ra** — nó có hình hài thế nào?
   - **Nhiều phần và kéo dài** — nhiều sản phẩm (báo cáo *và* demo *và* mã nguồn), hoặc nhiều tuần
     làm việc, hoặc có buổi bảo vệ → **`DoAnCuoiKy`**
   - **Một sản phẩm duy nhất**, dù lớn đến đâu → **`BaiTapVeNha`**

**Hãy gán nhãn theo tác vụ mà đoạn văn mô tả, không theo những từ khoá mà nó chứa.** Các quy tắc B-1
đến B-6 tồn tại vì câu vừa rồi không tự thi hành được.

---

## 4. Quy tắc ranh giới

**B-1 — `BaiTapVeNha` / `DoAnCuoiKy`** *(đã chốt trong S-1 §4.2)*

> **`DoAnCuoiKy`** khi phần việc là một **sản phẩm nhiều phần làm trong nhiều tuần**, thường được
> gọi là `đồ án` / `bài tập lớn` / `BTL` / *project*, và thường mang theo nhiều hơn một sản phẩm
> (báo cáo, demo, mã nguồn, buổi bảo vệ) hoặc một tập thể cùng làm. **`BaiTapVeNha`** khi đó là
> **một phần việc học duy nhất được làm và nộp**, dù lớn đến đâu. Cái để phân biệt là
> **nhiều-phần-và-kéo-dài**, không phải kích cỡ, và cũng không phải việc chữ `nhóm` có xuất hiện
> hay không.

**B-2 — `kiểm tra` dùng như một động từ** *(đã chốt trong S-1 §4.2)*

> **`kiểm tra` dùng như một động từ** (*xem lại, rà lại, xác nhận lại*) **không làm cho một dòng trở
> thành `KiemTraThuongXuyen`.** Hãy phân loại theo tác vụ mà đoạn văn mô tả, không theo sự có mặt
> của một từ khoá.

**B-3 — `cuối kỳ` gắn vào phần việc làm ra là dấu chỉ thời điểm, không phải dấu chỉ kỳ thi**

> `cuối kỳ` / `final` gắn vào **phần việc làm ra** (`đồ án cuối kỳ`, `project final`) cho biết *khi
> nào phải nộp*, chứ không phải *rằng đó là một kỳ thi*. Nó cho ra **`DoAnCuoiKy`**. `ThiCuoiKy`
> đòi hỏi phải có **một buổi thi**.

**B-4 — `giữa kỳ` so với `cuối kỳ` được quyết định theo mặt chữ**

> Khi đoạn văn mang chữ `giữa kỳ` / `GK`, lớp là **`ThiGiuaKy`**; `cuối kỳ` / `CK` gắn vào một buổi
> thi cho ra **`ThiCuoiKy`**. `bài kiểm tra giữa kỳ` là **`ThiGiuaKy`**, không phải
> `KiemTraThuongXuyen` — dấu `giữa kỳ` có thứ bậc cao hơn chữ `kiểm tra` (và xem thêm B-2).

**B-5 — làm việc theo `nhóm` không kéo theo `DoAnCuoiKy`**

> `nhóm` cho biết *ai* làm phần việc đó, không cho biết nó *là cái gì*. `bài tập nhóm` mà chỉ có một
> sản phẩm phải nộp thì là **`BaiTapVeNha`**.

**B-6 — lối viết không chuẩn không làm đổi lớp**

> Thiếu dấu, tiếng lóng, viết tắt, lỗi chính tả và chen tiếng Anh (`mon ktvm`, `ko kịp`, `BTL`) đều
> không làm thay đổi điều gì. Hãy đọc để tìm ra tác vụ; đừng hạ xuống một lớp dự phòng chỉ vì đoạn
> văn viết không trang trọng.

---

## 5. Độ khó — các mốc từ 1 đến 5

**Nhập nguyên văn từ S-1 §4.3**, chấp nhận ngày 2026-09-04. *Từ nay trở đi bản đặc tả này là văn bản
có thẩm quyền*; S-1 là bản ghi gốc. Mọi thay đổi ở một cái mốc đều là **thay đổi ngữ nghĩa, đòi hỏi
tăng số phiên bản** (§11), nên câu chữ được chép lại chứ không diễn giải lại.

Độ khó đo **khối lượng công việc × hậu quả nếu bỏ lỡ**. Nó là **thứ bậc, không phải khoảng cách
đều** — khoảng cách giữa 4 và 5 không bằng khoảng cách giữa 1 và 2.

| Mức | Cái mốc mà người gán nhãn có thể áp dụng |
|---|---|
| **1** | Một hành động đơn lẻ, một lần ngồi, **không phải chuẩn bị gì và không phải nộp gì** — nhớ, mang theo, xem lại một mốc giờ |
| **2** | Một phần việc ngắn, khoảng **dưới hai tiếng trong một lần ngồi**, không phải phối hợp với ai, trọng số nhỏ |
| **3** | **Một tác vụ có tính điểm bình thường** cần một hoặc hai buổi chuẩn bị. Đây là giá trị mặc định *chỉ khi đoạn văn không cho tín hiệu nào về khối lượng hay mức hệ trọng* |
| **4** | **Việc kéo dài nhiều buổi**, hoặc một sản phẩm gồm **nhiều thành phần** (báo cáo *và* demo *và* mã nguồn), hoặc việc đòi hỏi **phối hợp tập thể** |
| **5** | **Vừa hệ trọng cao vừa khối lượng lớn** — bài đánh giá kết thúc học kỳ mà kết quả môn học phụ thuộc vào đó, hoặc một dự án kéo dài đến buổi bảo vệ cuối |

Ba quy tắc đi kèm với chúng:

1. **Không được suy Độ khó ra từ `TaskType`.** Một dự đoán dựa trên lớp là *phương án chữa cháy khi
   thiếu dữ liệu*, không bao giờ là một quy tắc gán nhãn. `[measured]` Trong kho ngữ liệu cũ,
   `KiemTraThuongXuyen` nằm ở mức 3 trong **120 trên 121** dòng — đó chính là hình hài của việc suy
   ra như vậy.
2. **Mức 3 đòi hỏi một lý do khẳng định** khi đoạn văn có mang bất kỳ tín hiệu nào về khối lượng hay
   mức hệ trọng. "Không có tín hiệu mạnh" là căn cứ duy nhất được chấp nhận cho mức 3. `[measured]`
   Mức 3 chiếm 567/1028 rồi 579/1028 dòng.
3. **Ngưỡng của `v1` chỉ chi phối các mức 3–5.** Các mức 1–2 được đưa vào để thang đo trải hết tầm
   của nó, nhưng theo S-2.3 thì khoảng trống giữa mức 1 và mức 2 là **được tuyên bố, chưa được giải
   quyết**, và không con số nào của `v1` được trích dẫn như thể chi phối chúng.

`[measured]` **Vì sao cần có các mốc:** giữa hai lượt gán nhãn, không có quy tắc nào thay đổi, mức 5
tụt từ 184 xuống 116, mức 4 tăng từ 79 lên 147 và mức 1 tụt từ 57 xuống 30, với `5→4` (50), `3→4`
(41) và `1→3` (27) là những dịch chuyển nhiều nhất. Sự dịch chuyển có hệ thống theo một chiều là
**dấu hiệu thiếu mốc, không phải dấu hiệu cẩu thả.**

---

## 6. Danh mục ví dụ khó phân định (S-2.9)

**Sắp theo từng ranh giới**, chọn bằng máy theo một quy tắc đã đăng ký trước khi bất kỳ ví dụ nào
được rút ra: chỉ lấy các dòng trong phần chung chưa giữ riêng, sắp theo mã băm, **khử trùng lặp theo
khuôn mẫu trước**, mỗi ranh giới lấy `min(4, số ví dụ có được)`.

**Theo K1, không mục nào dưới đây là một phán quyết phân xử.** Lập danh mục không phải là phân xử;
S-2 không phân xử những dòng tranh chấp còn lại, và **không mục nào được viện dẫn như là nhãn của
dòng đó.**

> **Bản dịch này lược bỏ phần dữ liệu nguồn gốc của từng mục.** Bản gốc tiếng Anh còn in thêm, cho
> mỗi mục, mã băm `sha256` của dòng, tên tệp và số dòng nguồn, cùng những cái nhãn mà các lượt gán
> nhãn trước đây đã gán. Ba thứ đó là dữ liệu về nguồn gốc, **không phải căn cứ để quyết định** — và
> như chính mục này vừa nói, không mục nào được viện dẫn như là nhãn của dòng đó. Chúng được lược bỏ
> ở đây để bạn quyết định bằng các quy tắc chứ không bằng cách chép lại một cái nhãn cũ. **Phần chữ
> của mọi ví dụ và toàn bộ lời bình được giữ nguyên, không thiếu chỗ nào.**

**19 mục bao trùm 15 dòng khác nhau.** Bốn dòng bị tranh chấp trên *cả hai* trục — giữa các lượt và
ở lượt thứ ba — nên quy tắc chọn xếp mỗi dòng đó vào hai ranh giới và chúng xuất hiện hai lần: số
hiệu mục khác nhau, cùng một dòng. Đây là quy tắc chạy đúng như đã đăng ký, không phải trùng lặp.

**60 dòng đã giữ riêng vắng mặt khỏi danh mục này ngay từ cách dựng** — một ví dụ rút ra từ lô đã
chấm điểm sẽ huấn luyện người đọc trên chính dòng mà sau đó họ bị đem ra đo.

### `BaiTapVeNha` ↔ `DoAnCuoiKy`
*Phạm vi 64 dòng chưa giữ riêng / 39 khuôn mẫu — chọn 4, đủ.*

**E-1**

> Deadline bài tập lớn môn Kỹ thuật phần mềm sắp tới, mình đang hoàn thiện phần cuối.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-2**

> tình hình tiến độ cái đồ án tới đâu r ae

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-3**

> Mình phải nộp bài tập lớn Truyền thông đa phương tiện trong tuần này, ai rảnh review giúp mình không?

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-4**

> Deadline đồ án Kỹ thuật phần mềm đã gần, mình cần hoàn thiện phần demo.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**Những ví dụ này cho thấy gì.** Cái để phân biệt là *nhiều-phần-và-kéo-dài*, không phải kích cỡ và
không phải chữ `nhóm`. `E-1` và `E-4` nói về **cùng một môn nhưng dùng hai danh từ tác vụ khác
nhau** — `bài tập lớn` và `đồ án` — và cả hai đều cho ra `DoAnCuoiKy`. `E-2` là lối viết thân mật,
không mang danh từ tác vụ nào ngoài `đồ án`. `E-3` là một `bài tập lớn` mà **cả hai** lượt gán nhãn
cũ đều đọc thành bài tập thông thường. **Cũng để ý chỗ Độ khó lệch nhau**: `E-1` mang mức 3 còn
`E-4` mang mức 4 cho phần việc cùng tầm cỡ — một bất đồng về mốc nằm ngay bên trong một ví dụ về
`TaskType`.

### `BaiTapVeNha` ↔ `KiemTraThuongXuyen`
*Phạm vi 2 dòng chưa giữ riêng / 2 khuôn mẫu — chọn 2, **THIẾU**.*

**E-5**

> cô cho bài tập về nhà mon ktvm nhưng mình ghi ko kịp đề, ai chụp giúp mình với

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-6**

> làm bài tập ktvm sao kho wá, chắc chớt mất

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**Những ví dụ này cho thấy gì.** Cả hai rõ ràng là bài tập ở nhà (`bài tập về nhà`, `làm bài tập`)
mà lượt 1 lại gán thành `KiemTraThuongXuyen`. Cả hai đều ở lối viết không chuẩn — không dấu, tiếng
lóng, sai chính tả (`mon ktvm`, `sao kho wá`). Việc lối viết có phải là nguyên nhân gây lỗi hay
không là **một giả thuyết để phép dò tính tái lập kiểm chứng, chưa phải một kết luận**; n=2.

### `BaiTapVeNha` ↔ `ThiCuoiKy`
*Phạm vi 17 dòng chưa giữ riêng / 9 khuôn mẫu — chọn 4, đủ.*

**E-7**

> Deadline bài tập lớn môn Kỹ thuật phần mềm sắp tới, mình đang hoàn thiện phần cuối.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-8**

> Deadline đồ án Kỹ thuật phần mềm đã gần, mình cần hoàn thiện phần demo.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-9**

> Bài tập nhóm Nghiên cứu thị trường đang thiếu một vài phần, cần họp nhóm gấp.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-10**

> Nhóm mình còn thiếu phần kết luận báo cáo đồ án môn Cơ sở dữ liệu, hoàn thiện trước thứ 5.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**Những ví dụ này cho thấy gì.** Đây là "vụ đụng độ thật sự duy nhất" mà bản rà soát nêu ra. `E-9`
là trường hợp sạch cho `BaiTapVeNha` — `bài tập nhóm`, một sản phẩm duy nhất phải nộp, dù có làm
theo `nhóm` đi nữa — và lượt 3 cũng đồng ý như vậy. `E-7`, `E-8` và `E-10` thật ra không thuộc ranh
giới này: cả ba đều dừng ở `DoAnCuoiKy` một khi lớp đó tồn tại. Chúng được xếp vào đây vì các lượt
gán nhãn cũ đã tranh chấp chúng ở đây, và chúng minh hoạ **vì sao** lại có tranh chấp — lớp đúng cho
chúng lúc ấy chưa có.

### `BaiTapVeNha` ↔ `ThiGiuaKy`
*Phạm vi 0 dòng chưa giữ riêng / 0 khuôn mẫu — chọn 0, **không có bằng chứng trong kho ngữ liệu**.*

**Kho ngữ liệu không có bằng chứng nào cho ranh giới này.** Chưa lượt gán nhãn cũ nào từng tranh
chấp nó, nên không rút được dòng nào. Chuyển sang **các ví dụ soạn tay ở S-4**; ở đây không soạn gì.

### `DoAnCuoiKy` ↔ `KiemTraThuongXuyen`
*Phạm vi 0 dòng chưa giữ riêng / 0 khuôn mẫu — chọn 0, **không có bằng chứng trong kho ngữ liệu**.*

**Kho ngữ liệu không có bằng chứng nào cho ranh giới này.** Chưa lượt gán nhãn cũ nào từng tranh
chấp nó, nên không rút được dòng nào. Chuyển sang **các ví dụ soạn tay ở S-4**; ở đây không soạn gì.

### `DoAnCuoiKy` ↔ `ThiCuoiKy`
*Phạm vi 1 dòng chưa giữ riêng / 1 khuôn mẫu — chọn 1, **THIẾU**.*

**E-11**

> deadline project final là 23h59 chủ nhật nha

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**Những ví dụ này cho thấy gì.** Chỉ một dòng, và nó đúng là trường hợp khó thật: `project final`
có thể đọc thành *dự án cuối* hoặc *bài thi cuối*. Bản đặc tả giải quyết bằng tân ngữ — `project`
là phần việc làm ra, nên là `DoAnCuoiKy` — và `final` / `cuối kỳ` gắn vào phần việc làm ra là **dấu
chỉ thời điểm, không phải dấu chỉ kỳ thi** (quy tắc B-3).

### `DoAnCuoiKy` ↔ `ThiGiuaKy`
*Phạm vi 0 dòng chưa giữ riêng / 0 khuôn mẫu — chọn 0, **không có bằng chứng trong kho ngữ liệu**.*

**Kho ngữ liệu không có bằng chứng nào cho ranh giới này.** Chưa lượt gán nhãn cũ nào từng tranh
chấp nó, nên không rút được dòng nào. Chuyển sang **các ví dụ soạn tay ở S-4**; ở đây không soạn gì.

### `KiemTraThuongXuyen` ↔ `ThiCuoiKy`
*Phạm vi 6 dòng chưa giữ riêng / 3 khuôn mẫu — chọn 3, đủ.*

**E-12**

> Cô Hương nhắc cả lớp bài kiểm tra giữa kỳ môn Kế toán tài chính vào thứ 2 tuần 10.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-13**

> Thầy nhắc nộp báo cáo Hóa phân tích, mình cần kiểm tra lại format trước khi gửi.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-14**

> Kiểm tra giữa kỳ môn Hệ điều hành vào tuần 8, ôn phần quản lý tiến trình và bộ nhớ.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**Những ví dụ này cho thấy gì — và không cho thấy gì.** Chỉ `E-13` là thật sự nằm trên ranh giới
này, và nó chính là chỗ hỏng `kiểm tra`-dùng-như-động-từ (D-4): tác vụ là nộp một bản báo cáo,
`kiểm tra lại format` nghĩa là *rà lại cách trình bày*, còn lớp mà lượt 2 gán thì đến từ cái từ
khoá. `E-12` và `E-14` có chữ `giữa kỳ` và thuộc về `ThiGiuaKy`; chúng nằm trong cụm này chỉ vì
không lượt gán nhãn cũ nào có lớp đó. **Ranh giới này thiếu về thực chất dù đủ về số lượng** — được
báo cáo lại, không đem chọn lại (§7).

### `KiemTraThuongXuyen` ↔ `ThiGiuaKy`
*Phạm vi 2 dòng chưa giữ riêng / 2 khuôn mẫu — chọn 2, **THIẾU**.*

**E-15**

> Cô Hương nhắc cả lớp bài kiểm tra giữa kỳ môn Kế toán tài chính vào thứ 2 tuần 10.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-16**

> Kiểm tra giữa kỳ môn Hệ điều hành vào tuần 8, ôn phần quản lý tiến trình và bộ nhớ.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**Những ví dụ này cho thấy gì.** Cả hai đều nói rõ `giữa kỳ` và cả hai đều cho ra `ThiGiuaKy` theo
quy tắc B-2. Không cái nào thật sự khó phân định dưới hệ phân loại hiện hành; chúng bị tranh chấp
chỉ vì lúc được gán nhãn thì `ThiGiuaKy` chưa tồn tại. n=2.

### `ThiCuoiKy` ↔ `ThiGiuaKy`
*Phạm vi 12 dòng chưa giữ riêng / 3 khuôn mẫu — chọn 3, đủ.*

**E-17**

> Đợt thi giữa kỳ môn Toán rời rạc khó hơn mình dự kiến, phải tập trung hơn.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-18**

> Mình cần ôn gấp cho bài thi giữa kỳ Toán cao cấp A3, nội dung khá nhiều.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**E-19**

> Thi giữa kỳ môn Tài chính doanh nghiệp sắp diễn ra, mình đang ôn theo đề cương.

*Ví dụ minh hoạ. Không phải bản ghi phân xử. Không có nhãn Gold nào gắn với dòng này.*

**Những ví dụ này cho thấy gì.** Đây là ranh giới sạch nhất trong danh mục: `giữa kỳ` so với
`cuối kỳ` là **một dấu hiệu mặt chữ mà bản đặc tả có thể dựa vào**, và cả ba đều được giải quyết chỉ
bằng dấu hiệu đó. Chúng bị tranh chấp chỉ vì `ThiGiuaKy` ra đời sau các lượt gán nhãn ấy. Để ý
`E-17` và `E-18` mang Độ khó chuyển từ 5 xuống 4 — đúng hiện tượng trôi dạt đã ghi ở §5.

---

## 7. Các lỗ hổng của danh mục — đo được, không lấp

`[measured]` **Chỉ 4 trong 10 cặp lớp đạt mức sàn 3 ví dụ mà S-2.9 đặt ra.** Phần thiếu hụt đã được
đo và ghi lại *trước khi* việc chọn ví dụ chạy, nên nó là một tính chất của kho ngữ liệu chứ không
phải của cách chọn ví dụ.

| Tình trạng | Các ranh giới |
|---|---|
| **Đủ** (3–4 ví dụ) | `BaiTapVeNha`\|`DoAnCuoiKy` · `BaiTapVeNha`\|`ThiCuoiKy` · `KiemTraThuongXuyen`\|`ThiCuoiKy` · `ThiCuoiKy`\|`ThiGiuaKy` |
| **Thiếu** (1–2 ví dụ) | `BaiTapVeNha`\|`KiemTraThuongXuyen` (2) · `KiemTraThuongXuyen`\|`ThiGiuaKy` (2) · `DoAnCuoiKy`\|`ThiCuoiKy` (1) |
| **Kho ngữ liệu không có bằng chứng** (0) | `BaiTapVeNha`\|`ThiGiuaKy` · `DoAnCuoiKy`\|`KiemTraThuongXuyen` · `DoAnCuoiKy`\|`ThiGiuaKy` |

**Chỉ tiêu cho mỗi ranh giới đã được đăng ký trước ở mức `min(4, số ví dụ có được)`** — nằm giữa
khoảng 3–5 của S-2.9, chừa dư trên mức sàn mà không vét cạn những ranh giới ít ví dụ. Nó được ấn
định trước khi chọn và không phải là một lựa chọn đặt ra sau khi đã thấy kết quả.

**Hai trong bốn ranh giới "đủ" nằm đúng ngay trên mức sàn.** `KiemTraThuongXuyen`\|`ThiCuoiKy` có 6
dòng nhưng chỉ **3 khuôn mẫu**; `ThiCuoiKy`\|`ThiGiuaKy` có 12 dòng nhưng chỉ **3 khuôn mẫu**. Số
dòng làm cho lượng bằng chứng đằng sau cả hai trông nhiều hơn thực tế.

**`KiemTraThuongXuyen`\|`ThiCuoiKy` thiếu về thực chất dù đủ về số lượng.** Trong ba ví dụ của nó,
hai ví dụ có chữ `giữa kỳ` và thuộc về `ThiGiuaKy` theo B-4; chỉ một ví dụ thực sự nằm trên ranh
giới, và ví dụ đó chính là chỗ hỏng của B-2. Theo cam kết trước trong bản đăng ký, điều này được
**báo cáo như một phát hiện, không đem chọn lại.**

**Ba ranh giới không có bằng chứng thì không thể lập danh mục từ kho ngữ liệu này** và được chuyển
sang **các ví dụ soạn tay ở S-4**. Ở đây không soạn gì cả: viết ví dụ để lấp lỗ hổng sẽ đặt tài liệu
do AI sinh ra vào bên trong chính công cụ chi phối những cái nhãn do AI sinh ra.

`[observation]` Lỗ hổng này chỉ có một nguyên nhân. Chín trong mười cặp có `DoAnCuoiKy` hoặc
`ThiGiuaKy` ở ít nhất một vế, và **hai lớp đó chưa tồn tại trong cả hai lượt gán nhãn cũ** (S-1
§3.1). Kho ngữ liệu không thể làm bằng chứng cho những ranh giới vốn không thể chạm tới vào lúc nó
được gán nhãn.

---

## 8. Quy trình phân xử (S-2.8)

**Phân xử là gán cái nhãn đúng lấy từ toàn bộ hệ phân loại chính thức hiện hành — cả năm lớp.** Nó
**không phải** là một lựa chọn hai chiều giữa hai cái nhãn ban đầu đang tranh chấp. Phân xử và gán
nhãn là cùng một thao tác, cộng thêm một phán quyết được ghi lại.

1. Đọc dòng đó. Áp dụng §3, rồi §4.
2. Gán **một** trong năm lớp và một Độ khó theo §5. Cả hai, luôn luôn — không bao giờ có cái này mà
   thiếu cái kia.
3. Ghi lại phán quyết: cái nhãn, **quy tắc ở §4 hoặc cái mốc ở §5 đã quyết định nó**, và người gán
   nhãn.
4. Chỗ nào §4 không quyết định được dòng đó, **hãy nói ra** và nêu lên. Một dòng mà bản đặc tả không
   giải quyết được chính là bằng chứng về bản đặc tả, và giấu nó đi bằng cách chọn đại lớp gần nhất
   sẽ phá huỷ bằng chứng đó.

Theo S-4.5, việc dẫn ra quy tắc chỉ bắt buộc **ở những chỗ mà cấu trúc sẵn có chưa trả lời được câu
hỏi "quy tắc nào đã quyết định dòng này"**: phán quyết về Độ khó phải nêu cái mốc §5 đã dùng; các
dòng thuộc mẫu soạn tay phải nêu quy tắc đã xếp chúng vào đó; phán quyết `TaskType` cho các dòng
thuộc diện tranh chấp thì thừa hưởng ranh giới đã ghi sẵn và không cần thêm gì.

---

## 9. Nguồn gốc của nhãn

Mỗi cái nhãn đều mang theo, ít nhất là: **ai hoặc cái gì đã gán nó**, **khi nào**, **dưới
`GuidelineVersion` nào**, và **danh tính của dòng đó** (`sha256` + tệp nguồn + số dòng, theo
S-2.10/H1).

- **Mã băm là cái để định vị trong bản niêm phong đã giữ riêng, không phải danh tính lâu dài** (H1).
  **S-3** sẽ đưa vào `RowId` ổn định, trong đó giữ lại vĩnh viễn mã băm gốc.
- **Chữ nguồn đã sửa vẫn giữ được các tham chiếu tới mã băm cũ** thông qua một chuỗi kế thừa (H2) —
  một dòng đã sửa không âm thầm trở thành một dòng khác.
- **Trường `GuidelineVersion` ở mức từng dòng là của S-3, không phải của tài liệu này** (S-2.11).
  §11 định nghĩa ngữ nghĩa phiên bản; S-3 lưu chúng theo từng dòng.
- Theo DFD-8, **lượt gán nhãn của chủ sở hữu chính là cái nhãn**; mọi lượt khác đều là một phép đo.

---

## 10. Phép thử tính tái lập — đã đăng ký trước, **chưa thực hiện**

> **Được phép thực hiện không có nghĩa là đã thực hiện.** Không có gì trong mục này đã được chạy, và
> chưa có con số nào của nó tồn tại.

**Ai làm** (S-2.2, khép lại R-1): **chủ sở hữu** thực hiện lượt Gold/đối chiếu; **một người đọc độc
lập từ mạng lưới Q-2** thực hiện một phép dò tính tái lập trong điều kiện không biết trước. Cả hai
gán nhãn cùng 20 dòng một cách độc lập. **Về sau có thể thêm AI như một phép dò bổ sung, không bao
giờ được thay thế.**

**Trên cái gì**: **lô đã chấm điểm S-0 đã niêm phong** — 20 dòng, 12 dòng tranh chấp + 8 dòng trải
đều Độ khó. Một lô, hai phép đo (C3).

**Đo cái gì** (S-2.1): `TaskType` và `Difficulty` được đo **độc lập với nhau** (C1). **Cả hai đều
phải vượt ngưỡng của riêng mình — không lấy trung bình, không đánh đổi** (C2).

**Các ngưỡng** (S-2.7), đã đăng ký trước và **không được thay đổi sau khi đã nhìn thấy kết quả**:

| Chiều đo | Mẫu số | Ngưỡng |
|---|---|---|
| **`TaskType`** | 20 — chấm trên toàn lô | **≥ 17/20 (85%)**, khớp chính xác |
| **`Difficulty`** | 20 — chấm trên toàn lô | **≥ 18/20 (90%)**, khớp chính xác |

Mức "lệch trong vòng một bậc" có thể **được báo cáo như một chỉ dấu chẩn đoán; nó không bao giờ là
cái chốt chặn.** Nếu trượt ⇒ phải sửa bản đặc tả cộng với **lô đo lại v2** đã giữ riêng từ trước.

**Các quy tắc báo cáo ràng buộc mọi con số được tạo ra:**

- **G4 — thành phần của lô phải đi kèm mọi con số được trích dẫn. Không có phần trăm trần trụi ở
  bất cứ đâu.**
- **G3 — các tỷ lệ tiêu đề không được mô tả như là mức đồng thuận trên toàn kho ngữ liệu.**
- **G2 — bắt buộc phải tách theo từng tầng.** **C4 — bắt buộc phải có chi tiết theo từng chiều đo và
  từng ranh giới**; một con số tiêu đề đơn lẻ không phải là kết quả chấp nhận được.
- **Lưu ý về mức 1–2 phải đi kèm mọi con số Độ khó** — ngưỡng của `v1` chỉ chi phối các mức 3–5.
- **D-2 (phán quyết của chủ sở hữu, 2026-09-04) — thành phần nhãn-chung so với bị-ép-bởi-lượt-ba
  phải đi kèm mọi con số của S-2 như một chỉ dấu chẩn đoán:**

  | Lô đã giữ riêng | Tranh chấp | nhãn chung (giữa hai lượt) | bị ép bởi lượt ba |
  |---|---|---|---|
  | **Lô đã chấm điểm** | 12 | 3 | 9 |
  | Q-1 phân xử có tính giờ | 20 | 5 | 15 |
  | Lô đo lại sạch | 12 | 3 | 9 |

  Những dòng tranh chấp trên cả hai chiều được tính là nhãn chung, vì với chúng vẫn tồn tại một bất
  đồng giữa hai lượt một cách độc lập. **Đây là một chỉ dấu chẩn đoán, không bao giờ là tiêu chí
  chọn hay loại.** S-2 đo tính tái lập **so với hệ phân loại năm lớp hiện hành**, không phải mức
  trùng khớp với các nhãn cũ của những lượt trước.

**Điều kiện chặn:** phải có **một người đọc độc lập được mời**. Q-2 chỉ phê chuẩn rằng một mạng lưới
có giới hạn *tồn tại*; nó không xếp lịch cho ai cả. Chừng nào chưa mời được người đọc, phép thử này
chưa thể bắt đầu.

---

## 11. Đánh phiên bản bản hướng dẫn (S-2.11)

**Phiên bản của tài liệu này là `GuidelineVersion`, khai báo ở đầu tài liệu.** S-2 sở hữu ngữ nghĩa
phiên bản; **S-3 sở hữu trường ở mức từng dòng.**

**Tăng số phiên bản khi:** một định nghĩa lớp thay đổi · một quy tắc ranh giới giữa các lớp thay đổi
· một cái mốc Độ khó thay đổi.

**Không tăng số phiên bản với:** lỗi chính tả · định dạng · việc thêm ví dụ không làm đổi ngữ nghĩa.

**Lằn ranh chủ sở hữu đặt ra:** một ví dụ được thêm vào mà **làm thay đổi quy tắc phân loại trên
thực tế** thì là một thay đổi ngữ nghĩa và **đòi hỏi tăng số phiên bản** — kể cả một ví dụ thêm vào
danh mục §6 mà lại quyết định được một trường hợp §4 trước đó chưa quyết định được.

`[inference]` Việc đánh phiên bản là thứ chịu lực, không phải thủ tục giấy tờ. Kho ngữ liệu vốn đã
chứa những dòng được gán nhãn dưới các hệ phân loại ngầm khác nhau mà không có dấu hiệu nào phân
biệt chúng — đúng cái thất bại mà `LabelVersion` lẽ ra phải ngăn nhưng đã không ngăn được, vì nó
đánh phiên bản cho **tệp**, chứ không phải cho **bản hướng dẫn**.

---

## 12. Mẫu bản ghi gán nhãn (S-2.12)

**Đây là một hiện vật làm việc khi gán nhãn — dứt khoát *không phải* một giao ước lưu trữ của S-3.**
S-3 mới là nơi định nghĩa việc lưu trữ.

```yaml
row:
  sha256:            # đủ 64 ký tự hex của chữ đầu vào, UTF-8
  source:            # tệp:dòng cho từng lần xuất hiện
label:
  task_type:         # một trong năm lớp chính thức
  difficulty:        # 1-5
annotation:
  annotator:         # người hoặc hệ thống
  role:              # gold | probe | supplementary
  date:
  guideline_version: # ví dụ v1
ruling:
  decided_by:        # mã quy tắc (B-1..B-6) hoặc mức mốc; bắt buộc theo S-4.5
  note:              # chỉ dùng ở chỗ bản đặc tả không quyết định được dòng đó
  unresolved: false  # true khi không quy tắc nào ở mục 4 giải quyết được - hãy nêu lên, đừng đoán
```

---

## 13. Những gì bản đặc tả này **không** làm

| Không làm ở đây | Nó nằm ở đâu |
|---|---|
| Phân xử những dòng tranh chấp còn lại | Không thuộc S-2 (K1). Gold-A |
| Soạn ví dụ cho ba ranh giới không có bằng chứng | Mẫu soạn tay ở **S-4** |
| Định nghĩa trường `GuidelineVersion` ở mức từng dòng, hay bất kỳ việc lưu trữ nào | **S-3** (S-2.11) |
| Rà soát hay thay đổi những nơi đang dùng Độ khó | **FU-1**, chưa xếp lịch (S-1.4) |
| Phán quyết về chỗ hỏng trong khâu đánh giá | **S-6** (S-1.5) |
| Sửa các dòng hỏng vì B-2 trong tập hạt giống | **Không sửa.** D-4 giữ chúng lại như tài liệu quan sát được |
| Thực hiện phép thử tính tái lập | **§10** — đã đăng ký trước, chưa thực hiện |

**DFD-2 — chủ sở hữu đã phán quyết, 2026-09-04.** DFD-2 chặn việc thu thập thêm dữ liệu có nhãn
*"cho tới khi [bản đặc tả] tồn tại."* Việc *tồn tại* có gỡ được chốt chặn đó hay phải *vượt qua*
§10 mới gỡ được, thì văn bản đã phê duyệt không nói rõ. **Trong lần thực thi này, DFD-2 chỉ được
thoả mãn sau khi phép thử §10 đã đăng ký trước đạt yêu cầu** — trừ khi về sau có một quyết định
tường minh của chủ sở hữu thay đổi cách hiểu này.

**Vì vậy DFD-2 hôm nay vẫn chưa được thoả mãn.** Cả sự tồn tại của bản đặc tả này lẫn việc nó được
phê duyệt để đem ra đo đều không gỡ được chốt chặn về dữ liệu có nhãn. **Không được thu thập, nhập
vào, sinh ra hay đưa lên chính thức bất kỳ dữ liệu có nhãn nào dựa trên tài liệu này.**

---

## 14. Đóng băng (chủ sở hữu phê duyệt, 2026-09-04)

**`v1` đã đóng băng.** Nó được phê duyệt như là bản đặc tả mà phép thử §10 sẽ chạy **đối chiếu
với**, để phép thử đo được chính công cụ đó chứ không phải một mục tiêu đang di chuyển.

**Bị khoá — và khoá riêng để chống việc sửa sau khi đã thấy kết quả:**

| Bị khoá | Ở đâu |
|---|---|
| Trình tự quyết định | §3 |
| Sáu quy tắc ranh giới `B-1`–`B-6` | §4 |
| Các mốc Độ khó 1–5 và ba quy tắc đi kèm | §5 |
| Danh mục, **đúng như đã rút ra từ bằng chứng** | §6 |
| Quy trình phân xử 5 lớp | §8 |
| Các ngưỡng — `TaskType` ≥ 17/20, `Difficulty` ≥ 18/20, khớp chính xác | §10 |
| Mọi quy tắc báo cáo — G2, G3, G4, C4, lưu ý mức 1–2, chỉ dấu chẩn đoán D-2 | §10 |

**Bất biến của S-2.7 chính là lý do:** *các ngưỡng không được thay đổi sau khi đã nhìn thấy kết
quả.* §14 mở rộng đúng kỷ luật đó sang trình tự quyết định, các mốc và các quy tắc báo cáo — một bản
đặc tả được sửa sau khi đã thấy điểm của chính nó thì chẳng đo được gì cả.

**Danh mục giữ nguyên đúng như hiện tại.** Ba ranh giới không có bằng chứng **không** được lấp bằng
ví dụ soạn tay, và các ranh giới thiếu ví dụ **không** được chọn lại. Chúng đứng nguyên như những
**phát hiện về độ phủ của danh mục** (§7) và được chuyển sang **các ví dụ soạn tay ở S-4**.

**Lô 20 dòng đã chấm điểm và niêm phong giữ nguyên không đổi.** Nó **không** được giữ riêng lại lần
nữa dựa trên thành phần nhãn-chung so với bị-ép-bởi-lượt-ba của nó; thành phần đó là một **chỉ dấu
chẩn đoán bắt buộc trên mọi con số của S-2, không bao giờ là tiêu chí chọn.**

**Nếu phép thử trượt**, lối đi là lối đã được phê duyệt sẵn: sửa bản đặc tả — mà đó là một lần
**tăng `GuidelineVersion`** theo §11 — cộng với **lô đo lại v2** đã giữ riêng từ trước. Việc sửa
diễn ra *sau* một lần trượt đã được ghi nhận, không bao giờ *thay cho* việc ghi nhận đó.

### Neo đóng băng

Văn bản đã đóng băng là nội dung của tệp này tại lần commit mang mục này. Cái neo — lần commit đó
cùng mã `sha256` của tệp tại thời điểm đó — được ghi trong `2026-09-04-s2-v1-freeze-record.md`, để
việc đóng băng là **kiểm chứng được chứ không phải chỉ nói suông**: tính lại mã băm rồi đem so.

### Điều kiện tiên quyết mang tính chặn

**Phải mời được một người đọc độc lập từ mạng lưới Q-2 trước khi phép thử có thể bắt đầu.** Q-2 xác
lập rằng một mạng lưới *tồn tại*; nó không chỉ định ai cả. **Không được dùng AI thay thế** (S-2.2).
Việc mời người đọc là hành động của chủ sở hữu, và chừng nào việc đó chưa xong thì §10 chưa thể bắt
đầu.
