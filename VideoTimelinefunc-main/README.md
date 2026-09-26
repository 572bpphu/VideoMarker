# Video Timeline Marker (Công Cụ Phân Tích Thao Tác Video)

Hệ thống ứng dụng web chuyên dụng hỗ trợ đánh dấu, phân tích thao tác và quản lý mốc thời gian trong video công việc / dây chuyền sản xuất (phục vụ nghiên cứu thời gian Time Study, Lean & Industrial Engineering), được xây dựng trên nền tảng ASP.NET MVC 5, Entity Framework 6 và SQL Server LocalDB.

---

## 1. Giới thiệu tổng quan

Ứng dụng cho phép người dùng tải lên video công việc, hệ thống tự động nhận diện thời lượng và tạo trục thời gian tương ứng. Người dùng có thể đánh dấu các khoảng thời gian (thao tác), phân loại giữa thao tác **Đúng** và thao tác **Sai / Dư thừa**, gắn tên thao tác, theo dõi thời gian thực tế và xuất dữ liệu báo cáo thống kê dưới định dạng JSON.

Toàn bộ dữ liệu video và các mốc thao tác được lưu trữ bền vững trong cơ sở dữ liệu SQL Server LocalDB.

---

## 2. Yêu cầu hệ thống

- **Hệ điều hành**: Windows 10 hoặc Windows 11.
- **Môi trường phát triển**: Visual Studio 2019, 2022 hoặc mới hơn (đã cài workload *ASP.NET and web development*).
- **.NET Framework**: Phiên bản 4.7.2 trở lên (mặc định có sẵn trên Windows).
- **Cơ sở dữ liệu**: SQL Server LocalDB (đi kèm sẵn khi cài đặt Visual Studio).

---

## 3. Hướng dẫn cài đặt và khởi chạy ứng dụng

### Cách 1: Chạy trực tiếp bằng Visual Studio

1. Mở tập tin `VideoTimelineApp.sln` bằng Visual Studio.
2. Nhấp chuột phải vào Solution trong cửa sổ Solution Explorer, chọn **Restore NuGet Packages** để tải các thư viện cần thiết.
3. Nhấn phím **F5** (hoặc tổ hợp **Ctrl + F5** để chạy không debug).
4. Hệ thống sẽ tự động khởi tạo cơ sở dữ liệu `VideoTimelineDb` trong SQL Server LocalDB ở lần chạy đầu tiên.

### Cách 2: Khôi phục gói thư viện bằng PowerShell

1. Mở PowerShell tại thư mục gốc của dự án.
2. Chạy lệnh:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\setup.ps1
   ```
3. Mở `VideoTimelineApp.sln` trong Visual Studio và nhấn **F5** để bắt đầu sử dụng.

---

## 4. Hướng dẫn sử dụng chi tiết

### 4.1. Quản lý thư viện video (Trang chủ)

- **Tải lên video mới**:
  - Nhập tên video vào ô *Tên video* (nếu để trống, hệ thống sẽ tự động dùng tên tập tin gốc).
  - Kéo thả tập tin video vào khung nhận tập tin hoặc nhấp chuột vào *bấm chọn file*.
  - Các định dạng hỗ trợ: `MP4`, `MOV`, `MKV`, `AVI`, `WEBM` (dung lượng tối đa 500 MB).
  - Nhấn nút **Upload & Mở Timeline** để tiến hành tải lên máy chủ.
- **Xem danh sách video**:
  - Các video đã tải lên sẽ hiển thị dưới dạng thẻ thông tin trong mục *Thư viện video*.
  - Thông tin hiển thị bao gồm: thời lượng, dung lượng tập tin, số thao tác đã đánh dấu, thời điểm tải lên và các nhãn đánh dấu xem trước.
- **Mở giao diện đánh dấu**: Nhấn nút **Mở Timeline** trên thẻ video tương ứng.
- **Xoá video**: Nhấn nút biểu tượng thùng rác để xoá tập tin video vật lý trên máy chủ và toàn bộ dữ liệu mốc thời gian liên quan trong cơ sở dữ liệu.

---

### 4.2. Giao diện đánh dấu Timeline (Trang Player)

Giao diện trang Player được thiết kế tối ưu trên màn hình rộng (hỗ trợ hiển thị lên đến 1500px), khung video lớn và bảng quản lý thao tác rộng rãi không bị tràn thanh cuộn ngang.

#### 1. Điều khiển video và thanh trục thời gian:
- Sử dụng thanh điều khiển video mặc định hoặc bấm nút **Play/Pause** trên thanh công cụ.
- Nhấp chuột trực tiếp vào bất kỳ vị trí nào trên thanh thời gian (Timeline) để tua video đến giây tương ứng.
- Khi rê chuột trên thanh Timeline, khung hiển thị nhỏ sẽ hiển thị chính xác thời gian tại vị trí con trỏ (độ chính xác đến hàng phần trăm giây `mm:ss.SS`).

#### 2. Giao diện Timeline 2 Làn (Dual-Track Timeline):
Trục thời gian được phân chia thành 2 làn độc lập để trực quan hóa toàn diện quy trình:
- **Làn trên**: Hiển thị các thao tác **ĐÚNG** (màu sắc phân biệt sinh động, dải xanh pastel).
- **Làn dưới**: Hiển thị các thao tác **SAI** (màu đỏ kèm hoạ tiết sọc chéo).
- Hai làn tách biệt giúp nhìn rõ toàn bộ quy trình làm việc, không bị che khuất khi có thao tác lồng nhau.

#### 3. Quy tắc đè mốc thời gian linh hoạt (Overlap Rules):
- **Cho phép Đúng và Sai đè lên nhau**: Người dùng có thể đánh dấu một thao tác Sai nằm lọt bên trong hoặc giao với thao tác Đúng (phục vụ trường hợp xem lại phát hiện thao tác thừa/lỗi phát sinh giữa một công đoạn chuẩn).
- **Chặn đè giữa các thao tác cùng loại**: Hệ thống ngăn không cho tạo 2 thao tác Đúng đè lên nhau hoặc 2 thao tác Sai đè lên nhau, đảm bảo dữ liệu không bị trùng lặp nhầm lẫn.

#### 4. Tính toán "Tổng thời gian đúng" (Khấu trừ thời gian dư thừa):
- Chỉ tiêu **Tổng thời gian đúng** hiển thị tổng thời gian thực tế công nhân làm đúng quy trình.
- Khi có thao tác Sai nằm lồng bên trong một thao tác Đúng, hệ thống **tự động khấu trừ phần thời gian thao tác Sai** ra khỏi thời gian của thao tác Đúng:
  $$\text{Tổng thời gian Đúng thực tế} = \sum (\text{Độ dài các thao tác Đúng}) - \sum (\text{Phần thời gian thao tác Sai lồng bên trong})$$

#### 5. Chỉnh sửa và quản lý thao tác trực tiếp trên bảng:
- **Sửa trực tiếp tên thao tác**: Nhập lại tên trong ô input *Tên thao tác*, hệ thống tự động lưu vào cơ sở dữ liệu.
- **Sửa mốc thời gian trực tiếp**: Nhập lại thời gian bắt đầu hoặc kết thúc ngay trên bảng (hỗ trợ định dạng `mm:ss`, `mm:ss.SS` hoặc số giây).
- **Phát thử từng thao tác**: Bấm nút **▶** ở mỗi dòng để tua nhanh và phát đúng đoạn video của thao tác đó.
- **Xoá từng thao tác**: Bấm nút **Xóa** ở cuối dòng tương ứng.
- **Hoàn tác (Undo)**: Bấm nút **↩ Undo** trên thanh công cụ để xoá nhanh thao tác vừa tạo gần nhất.
- **Xoá tất cả**: Bấm nút **🗑 Xoá tất cả** trên thanh công cụ để làm mới toàn bộ trục thời gian.

#### 6. Xuất dữ liệu báo cáo (Export JSON):
- Nhấn nút **📋 Xuất JSON** trên thanh công cụ.
- Hệ thống tải xuống tập tin `.json` chứa đầy đủ các chỉ số:
  - `total_correct_net_sec`: Thời gian thuần Đúng (đã trừ thời gian thao tác Sai lồng bên trong).
  - `total_correct_gross_sec`: Tổng thời gian Đúng danh nghĩa (chưa trừ).
  - `total_incident_sec`: Tổng thời gian thao tác Sai / lãng phí.
  - `coverage_pct`: Tỷ lệ phần trăm thời gian có gắn mốc so với độ dài video.
  - Danh sách chi tiết từng thao tác với độ chính xác đến hàng mili-giây.

---

### 4.3. Danh sách phím tắt điều khiển

Hệ thống hỗ trợ phím tắt 1 chạm chuyên nghiệp, giúp người dùng vừa xem video vừa đánh dấu mốc thời gian thực mà không cần dùng chuột:

| Phím tắt | Chức năng | Mô tả chi tiết |
|---|---|---|
| **`Q`** hoặc **`1`** | **Đánh dấu thao tác ĐÚNG** | • **Bấm lần 1**: Đặt điểm đầu thao tác Đúng ngay giây video đang phát.<br>• **Bấm lần 2**: Chốt điểm cuối và tự động lưu thao tác Đúng. |
| **`W`** hoặc **`2`** | **Đánh dấu thao tác SAI** | • **Bấm lần 1**: Đặt điểm đầu thao tác Sai.<br>• **Bấm lần 2**: Chốt điểm cuối và tự động lưu thao tác Sai. |
| **Chuyển tiếp liền mạch** | **Đổi loại tức thì** | Khi đang mở mốc Đúng dở mà phát hiện lỗi, bấm ngay **`W`** (hoặc `2`) $\rightarrow$ hệ thống tự động chốt thao tác Đúng tại giây đó và lập tức mở điểm đầu thao tác Sai ngay tại cùng giây, không bị trễ nhịp. |
| **`Space`** | **Play / Pause** | Tạm dừng hoặc tiếp tục phát video. |
| **`Esc`** | **Hủy mốc đang chọn** | Hủy điểm bắt đầu đang chờ nếu lỡ bấm nhầm. |
| **`S`** | **Đặt điểm đầu** | Đặt điểm bắt đầu theo cách truyền thống. |
| **`E`** | **Đặt điểm cuối** | Đặt điểm kết thúc và lưu theo cách truyền thống. |
| **`←`** / **`→`** | **Tua ±5 giây** | Tua lùi hoặc tua tiến 5 giây. |

*(Lưu ý: Khi con trỏ chuột đang nằm trong ô nhập chữ như ô Tên thao tác, các phím tắt chữ cái sẽ tạm thời bị vô hiệu hoá để tránh gõ nhầm).*

---

## 5. Cấu trúc mã nguồn dự án

- **`App_Start/`**:
  - `RouteConfig.cs`: Cấu hình điều hướng ASP.NET MVC.
  - `WebApiConfig.cs`: Cấu hình REST API và định dạng JSON serialization (camelCase).
  - `FilterConfig.cs`: Cấu hình bộ lọc lỗi toàn cục.
- **`Controllers/`**:
  - `HomeController.cs`: Quản lý danh sách video, xử lý upload và xoá video.
  - `VideoController.cs`: Nạp dữ liệu ban đầu và hiển thị trang phát video.
  - `SegmentsApiController.cs`: Cung cấp các điểm cuối RESTful API cho thao tác (`GET`, `POST`, `PUT`, `DELETE`). Đã tích hợp bộ lọc chống đè mốc cùng loại.
- **`Models/`**:
  - `VideoSession.cs`: Thực thể lưu trữ thông tin video đã tải lên.
  - `Segment.cs`: Thực thể lưu trữ các đoạn mốc thao tác (StartTime, EndTime, Type, Label, Color).
- **`Data/`**:
  - `AppDbContext.cs`: Lớp DbContext của Entity Framework 6 với quan hệ xoá theo tầng (Cascade Delete).
- **`Views/`**:
  - `Home/Index.cshtml`: Giao diện thư viện video và form tải lên.
  - `Video/Player.cshtml`: Giao diện phát video, thanh timeline 2 làn, bảng thao tác và bảng hướng dẫn phím tắt.
  - `Shared/_Layout.cshtml`: Layout nền tối (Dark Theme) hiện đại.
- **`Content/Site.css`**: Định kiểu toàn bộ ứng dụng, hỗ trợ giao diện đáp ứng (responsive), độ rộng khung hình tối ưu 1500px, styling thanh timeline 2 làn và các nhãn phím tắt `<kbd>`.
- **`Scripts/timeline.js`**: Kịch bản chính phía client điều khiển timeline, vẽ 2 làn, lắng nghe phím tắt real-time, giao tiếp REST API, tính toán thời gian khấu trừ và xuất tệp JSON.
- **`Services/CloudinaryService.cs`**: Xử lý upload trực tiếp video lên máy chủ đám mây Cloudinary theo luồng dữ liệu (chunked streaming).
- **`uploads/videos/`**: Thư mục lưu trữ video cục bộ khi chạy ở môi trường máy cá nhân.

---

## 6. Danh sách các điểm cuối REST API

- **`GET /api/segments/{videoId}`**: Lấy danh sách toàn bộ các thao tác đã đánh dấu thuộc về một video (sắp xếp theo `StartTime`).
- **`POST /api/segments`**: Tạo một thao tác mới (có kiểm tra chống đè mốc cùng loại từ phía máy chủ).
- **`PUT /api/segments/{id}`**: Cập nhật tên thao tác hoặc mốc thời gian bắt đầu/kết thúc (có kiểm tra chống đè mốc cùng loại).
- **`DELETE /api/segments/{id}`**: Xoá thao tác khỏi cơ sở dữ liệu theo ID.
- **`PUT /api/segments/video/{videoId}/duration`**: Cập nhật thời lượng chính xác của video sau khi trình duyệt đọc được metadata từ video.

---

## 7. Cấu hình lưu trữ media cục bộ (Local Storage)

Ứng dụng lưu trữ video trực tiếp vào thư mục chuyên dụng ngoài root dự án (hoặc ổ cứng/ổ mạng NAS tùy chỉnh) và hỗ trợ HTTP Range Requests (206 Partial Content) để tua timeline mượt mà.

- **Đường dẫn mặc định**: `D:\VideoTimelineData\Videos`
- **Cách tùy chỉnh đường dẫn lưu trữ**:
  1. Mở tập tin [Web.config](file:///d:/VideoTimelinefunc-main/VideoTimelinefunc-main/Web.config).
  2. Thay đổi giá trị key `LocalStoragePath` sang thư mục bạn mong muốn (ví dụ: `E:\MediaStorage\Videos` hoặc `\\NAS-SERVER\SharedVideos`):
     ```xml
     <add key="LocalStoragePath" value="D:\VideoTimelineData\Videos" />
     <add key="MaxFileSizeMB" value="500" />
     ```
  3. Ứng dụng sẽ tự động khởi tạo thư mục nếu chưa tồn tại.
  4. Video được phát qua endpoint `/Video/Stream/{id}` có hỗ trợ phân đoạn byte giúp việc tua timeline tức thì, không giật lag.
