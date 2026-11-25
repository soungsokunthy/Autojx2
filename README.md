# AutoJx2 - Game Automation Tool

Tool tự động hóa cho game JX2 được xây dựng bằng C# + WPF (.NET 8) + FlaUI + DPAPI/Credential Manager.

## 🚀 Tính năng

### Core Features
- **Process Management**: Tự động phát hiện và kết nối với process game
- **UI Automation**: Sử dụng FlaUI để tự động hóa giao diện game
- **Secure Storage**: Lưu trữ thông tin đăng nhập an toàn với DPAPI và Windows Credential Manager
- **Logging System**: Hệ thống logging chi tiết với Serilog

### Automation Features
- 🌾 **Auto Farming**: Tự động farming
- 📜 **Auto Quest**: Tự động nhận và hoàn thành quest
- ⚔️ **Auto Combat**: Tự động chiến đấu
- 💊 **Auto Heal**: Tự động hồi HP/MP khi thấp

### Security Features
- 🔐 **DPAPI Encryption**: Mã hóa mật khẩu với Windows Data Protection API
- 🗝️ **Credential Manager**: Lưu trữ thông tin đăng nhập trong Windows Credential Manager
- 🔒 **Secure Storage**: Không lưu mật khẩu dưới dạng plain text

## 🛠️ Yêu cầu hệ thống
- Windows 10/11
- .NET 8.0 Runtime
- Visual Studio 2022 hoặc .NET CLI
- Quyền admin để sử dụng Windows Credential Manager

## 📦 Cài đặt

### 1. Clone repository
```bash
git clone https://github.com/voidwalker2312/AutoJx2.git
cd AutoJx2
```

### 2. Restore packages
```bash
dotnet restore
```

### 3. Build project
```bash
dotnet build
```

### 4. Run application
```bash
dotnet run --project AutoJx2
```

## 🎮 Cách sử dụng

### 1. Khởi động game
- Chạy game JX2
- Đảm bảo game đang chạy và hiển thị trên màn hình

### 2. Kết nối với game
- Mở AutoJx2
- Chọn process game từ danh sách
- Nhấn "Attach" để kết nối

### 3. Cấu hình automation
- Chọn các tính năng automation cần thiết
- Thiết lập các thông số như delay, ngưỡng HP/MP
- Lưu cài đặt

### 4. Bắt đầu automation
- Nhấn F12 hoặc sử dụng hotkey đã cài đặt
- Theo dõi trạng thái trong tab Logs

## ⚙️ Cấu hình

### Automation Settings
- **Delay**: Thời gian delay giữa các hành động (ms)
- **Max HP %**: Ngưỡng HP để tự động heal
- **Max MP %**: Ngưỡng MP để tự động heal

### Logging
- **Log Level**: Mức độ chi tiết của log
- **Log Files**: Lưu trong thư mục `logs/`
- **Log Rotation**: Tự động xoay vòng log files

## 🔧 Phát triển

### Project Structure
```
AutoJx2/
├── Interfaces/          # Interface definitions
├── Models/              # Data models
├── Services/            # Business logic services
├── App.xaml             # Application entry point
├── MainWindow.xaml      # Main UI
└── AutoJx2.csproj       # Project file
```

### Key Components
- **CredentialManager**: Quản lý thông tin đăng nhập
- **GameAutomation**: Logic tự động hóa game
- **MainWindow**: Giao diện người dùng chính

### Dependencies
- **FlaUI**: UI automation framework
- **Serilog**: Logging framework
- **Microsoft.Extensions.DependencyInjection**: Dependency injection
- **System.Security.Cryptography**: DPAPI encryption

## 🚨 Lưu ý quan trọng

### Bảo mật
- Tool sử dụng Windows Credential Manager để lưu trữ thông tin đăng nhập
- Mật khẩu được mã hóa với DPAPI (Data Protection API)
- Không lưu thông tin nhạy cảm vào file cấu hình

### Sử dụng
- Chỉ sử dụng tool นี้សម្រាប់គោលបំណងសិក្សា និងស្រាវជ្រាវ
- Tuân thủ Terms of Service của game
- Sử dụng có trách nhiệm và không lạm dụng

### Hỗ trợ
- Tool được thiết kế cho game JX2
- Có thể cần điều chỉnh cho các game khác
- Các placeholder methods cần được implement theo UI cụ thể của game

## 📝 TODO
- [ ] Implement global hotkey registration
- [ ] Add more automation features
- [ ] Improve error handling
- [ ] Add unit tests
- [ ] Create installer package
- [ ] Add configuration file support
- [ ] Implement plugin system

## 🤝 Đóng góp
Mọi đóng góp đều được chào đón! Hãy:
1. Fork project
2. Tạo feature branch
3. Commit changes
4. Push to branch
5. Tạo Pull Request

## 📄 License
Project này được phân phối dưới MIT License. Xem file `LICENSE` để biết thêm chi tiết.

## ⚠️ Disclaimer
Tool này được cung cấp "nguyên trạng" và chỉ dành cho mục đích giáo dục. Tác giả không chịu trách nhiệm về việc sử dụng tool này. Hãy sử dụng có trách nhiệm và tuân thủ các quy định của game.

## 📞 Liên hệ
- GitHub: [@voidwalker2312](https://github.com/voidwalker2312)
- Email: lqdinh210@gmail.com

---
**Happy Gaming! 🎮**
