# NEXUS 3D – Architecture Overview (Stage 01 Foundation)

Tài liệu này mô tả thiết kế kiến trúc của Giai đoạn 01 – Foundation:

1. **Presentation Layer (`NEXUS3D.App`)**:
   - Giao diện WPF sử dụng mẫu MVVM với `CommunityToolkit.Mvvm`.
   - Khởi tạo DI và quản lý vòng đời ứng dụng trong `Bootstrap/AppBootstrapper.cs`, `Bootstrap/ServiceRegistration.cs`, và `Bootstrap/DependencyConfig.cs`.
2. **Application Layer (`NEXUS3D.Application`)**:
   - Điều phối các Use Case (`DashboardService`, `ConverterPreparationService`, `FileManagerService`, `SettingsService`, `NotificationService`).
3. **Domain Layer (`NEXUS3D.Domain`)**:
   - Thực thể cốt lõi, Value Objects, quy tắc nghiệp vụ (`ConversionDomainRules`) và các Repository Interfaces.
4. **Infrastructure Layer (`NEXUS3D.Infrastructure`)**:
   - `Logging/`: Ghi log ra file theo ngày (`LogFileProvider`, `AppLogger`, `LogConfiguration`, `ExceptionLogger`).
   - `Persistence/`: Quản lý kết nối SQLite (`SqliteConnectionFactory`), nâng cấp schema (`DatabaseSchemaMigrator`), khởi tạo dữ liệu mặc định (`DatabaseInitializer`) và các SQLite Repositories.

