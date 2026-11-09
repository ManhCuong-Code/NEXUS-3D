# NEXUS 3D – Universal 3D Model Converter & Editor

[![.NET 10 LTS](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20WPF-0078D4)](#)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20Architecture%20%2B%20MVVM-10B981)](#)
[![Stage](https://img.shields.io/badge/Stage-01%20Foundation%20Completed-3B82F6)](#)

## 1. Giới Thiệu Dự Án (Introduction)
**NEXUS 3D** là phần mềm Desktop chuyên nghiệp trên nền tảng Windows (WPF / .NET 10 LTS) được thiết kế để chuyển đổi, kiểm tra, chỉnh sửa và tối ưu hóa mô hình 3D đa định dạng — đặc biệt tập trung vào hệ sinh thái Voxel, Polygon Mesh và Block/Bone Hierarchy:
- **Minecraft Schematic** (`.schem`, `.schematic`, `.litematic`)
- **Wavefront OBJ Mesh** (`.obj`, `.mtl`)
- **Voxel Cuboid JSON** (`.json`, `.voxel.json`)
- **Bedrock Geometry JSON** (`.geo.json`)

## 2. Mục Tiêu Dự Án (Project Objectives)
- Cung cấp công cụ chuyển đổi hai chiều chính xác giữa lưới Voxel (Schematic), lưới đa giác (OBJ Mesh) và cấu trúc hình hộp phân cấp (Voxel JSON / Bedrock Geometry).
- Tối ưu hóa hình học tự động (Greedy Meshing, giảm đỉnh/mặt thừa, bảo toàn UV và Material).
- Đảm bảo hiệu năng cao, kiến trúc tách biệt rõ ràng (**Clean Architecture**), dễ dàng mở rộng định dạng mới thông qua hệ thống **Plugin**.

## 3. Công Nghệ Sử Dụng (Technology Stack)

| Thành phần | Công nghệ | Phiên bản |
|---|---|---|
| **Ngôn ngữ** | C# | C# 14 / .NET 10 |
| **Framework** | .NET 10 LTS | `net10.0` / `net10.0-windows` |
| **Giao diện Desktop** | WPF (Windows Presentation Foundation) | .NET 10 Windows Desktop |
| **Kiến trúc** | Clean Architecture | Layered & Modular |
| **UI Pattern** | MVVM (Model-View-ViewModel) | `CommunityToolkit.Mvvm` 8.4.2 |
| **Dependency Injection** | Microsoft Extensions DI | `Microsoft.Extensions.DependencyInjection` 10.0.14 |
| **Logging** | Microsoft Extensions Logging + File Provider | `Microsoft.Extensions.Logging` 10.0.14 |
| **Database** | SQLite (WAL Mode, Schema Migrations) | `Microsoft.Data.Sqlite` 10.0.14 |
| **JSON Serialization** | System.Text.Json | Built-in .NET 10 |
| **Testing** | xUnit + Visual Studio Runner | `xunit` 2.9.3 |

## 4. Kiến Trúc Hệ Thống (System Architecture)

NEXUS 3D tuân thủ nghiêm ngặt nguyên tắc **Clean Architecture**:

1. **`NEXUS3D.Shared`**: Hằng số hệ thống (`AppConstants`), `OperationResult<T>`, `Guard` clauses, bộ định dạng dung lượng/thời gian. Không phụ thuộc vào bất kỳ project nào khác.
2. **`NEXUS3D.Domain`**: Chứa các thực thể cốt lõi (`ModelDocumentSummary`, `ConversionHistoryRecord`, `RecentFileEntry`, `ConversionProfile`, `ExportConfiguration`, `AppSettingEntry`, `PluginRegistration`), Value Objects (`Vector3DValue`, `BoundingBox3D`), quy tắc nghiệp vụ (`ConversionDomainRules`) và các Repository Interfaces. Không phụ thuộc vào WPF, SQLite hay Infrastructure.
3. **`NEXUS3D.Formats.*`** (`Abstractions`, `Schematic`, `Obj`, `VoxelJson`, `BedrockGeometry`): Định nghĩa hợp đồng `IFormatDescriptor`, `IModelFormatParser`, `IModelFormatExporter`, `IFormatRegistry` và bộ mô tả cho từng định dạng 3D.
4. **`NEXUS3D.Engine` / `NEXUS3D.Rendering` / `NEXUS3D.PluginSystem`**: Định nghĩa các hợp đồng và trạng thái nền tảng cho pipeline chuyển đổi (`IConversionPipeline`), điều khiển góc nhìn 3D (`IViewportController`), và quản lý plugin (`IPluginCatalogService`).
5. **`NEXUS3D.Application`**: Điều phối các Use Case (`DashboardService`, `ConverterPreparationService`, `FileManagerService`, `SettingsService`, `NotificationService`) và kiểm tra tính hợp lệ (`PrepareConversionRequestValidator`).
6. **`NEXUS3D.Infrastructure`**: Triển khai hạ tầng kỹ thuật gồm:
   - **Logging**: `LogFileProvider`, `AppLogger`, `LogConfiguration`, `ExceptionLogger` (ghi file theo ngày, tự động ẩn thông tin nhạy cảm, ghi đầy đủ StackTrace).
   - **Persistence (SQLite)**: `DatabaseConfiguration`, `SqliteConnectionFactory`, `DatabaseSchemaMigrator`, `DatabaseInitializer` và 6 SQLite Repositories.
   - **FileSystem, Configuration & Caching**: `ApplicationPathsService`, `JsonConfigurationStore`, `InMemoryCacheService`, `FormatRegistry`.
7. **`NEXUS3D.App`**: Ứng dụng giao diện WPF theo mẫu MVVM (`MainWindowViewModel`, `DashboardViewModel`, `ConverterViewModel`, `FileManagerViewModel`, `SettingsViewModel`), khởi tạo DI qua `AppBootstrapper` / `ServiceRegistration` / `DependencyConfig`.

## 5. Cấu Trúc Thư Mục (Directory Structure)

```text
NEXUS-3D/
├── NEXUS-3D.sln
│
├── src/
│   ├── NEXUS3D.App/                        # WPF UI, ViewModels, Views, Controls, Themes, Bootstrapper
│   ├── NEXUS3D.Application/                # Use Cases, Application Interfaces, DTOs, Validators
│   ├── NEXUS3D.Domain/                     # Core Entities, Value Objects, Rules, Repository Contracts
│   ├── NEXUS3D.Infrastructure/             # File Logging, SQLite Persistence, JSON Config, Cache
│   ├── NEXUS3D.Formats.Abstractions/       # Format Parser/Exporter/Registry Contracts
│   ├── NEXUS3D.Formats.Schematic/          # Schematic (.schem/.schematic/.litematic) Contracts
│   ├── NEXUS3D.Formats.Obj/                # Wavefront OBJ (.obj/.mtl) Contracts
│   ├── NEXUS3D.Formats.VoxelJson/          # Voxel Cuboid JSON (.json) Contracts
│   ├── NEXUS3D.Formats.BedrockGeometry/    # Bedrock Geometry (.geo.json) Contracts
│   ├── NEXUS3D.Engine/                     # Conversion Pipeline Foundation
│   ├── NEXUS3D.Rendering/                  # 3D Viewport Controller Foundation
│   ├── NEXUS3D.PluginSystem/               # Plugin Architecture Contracts & Catalog
│   └── NEXUS3D.Shared/                     # Guards, OperationResult, Helpers, Constants
│
├── tests/
│   ├── NEXUS3D.Domain.Tests/               # Unit tests for Domain rules & entities
│   ├── NEXUS3D.Parser.Tests/               # Unit tests for Format descriptors & registry
│   ├── NEXUS3D.Engine.Tests/               # Unit tests for Engine & Rendering contracts
│   ├── NEXUS3D.Integration.Tests/          # Integration tests for DI, SQLite, Logging, MVVM
│   └── NEXUS3D.Performance.Tests/          # Performance benchmarks for Cache & Infrastructure
│
├── plugins/                                # External plugin drop-in directory
├── samples/                                # Sample 3D model files for testing
├── documentation/                          # Technical architecture & stage documentation
├── scripts/                                # Build & verification automation scripts
├── build/                                  # Build configuration & packaging metadata
├── .gitignore
├── README.md
└── LICENSE
```

## 6. Hướng Dẫn Cài Đặt, Build và Chạy Ứng Dụng

### Yêu cầu môi trường (Prerequisites)
- Hệ điều hành: **Windows 10 / Windows 11 (x64)**
- SDK: **.NET 10.0 SDK** (`10.0.100` trở lên, bao gồm Windows Desktop Workload)
- IDE khuyến nghị: **Visual Studio 2022 / 2026** hoặc **JetBrains Rider** / **VS Code**

### Khôi phục package (Restore)
```powershell
dotnet restore NEXUS-3D.sln
```

### Build dự án (Debug & Release)
```powershell
# Build cấu hình Debug
dotnet build NEXUS-3D.sln -c Debug

# Build cấu hình Release
dotnet build NEXUS-3D.sln -c Release
```

### Chạy bộ kiểm thử tự động (Run Tests)
```powershell
dotnet test NEXUS-3D.sln -c Release
```

### Chạy ứng dụng WPF Desktop (Run Application)
```powershell
dotnet run --project src/NEXUS3D.App/NEXUS3D.App.csproj
```
> Dữ liệu runtime (SQLite database `nexus3d.sqlite` và thư mục `Logs/`) được khởi tạo tự động tại `%LocalAppData%\NEXUS3D\`.

## 7. Trạng Thái Phát Triển & Lộ Trình 12 Giai Đoạn (12-Stage Roadmap)

| Giai đoạn | Tên Giai Đoạn | Nội dung trọng tâm | Trạng thái |
|---|---|---|---|
| **Giai đoạn 01** | **Foundation** | Khởi tạo Solution, Clean Architecture, MVVM, DI, File Logging, SQLite, UI khung chính | **Hoàn thành (100%)** |
| **Giai đoạn 02** | **Core 3D Domain & IR** | Xây dựng cấu trúc dữ liệu trung gian (Intermediate Representation) cho Voxel, Mesh và Bone/Cube | Chưa triển khai |
| **Giai đoạn 03** | **Schematic & Voxel Parser** | Triển khai bộ đọc/ghi NBT, `.schem`, `.schematic`, `.litematic` | Chưa triển khai |
| **Giai đoạn 04** | **OBJ & Material Parser/Exporter** | Triển khai bộ đọc/ghi Wavefront `.obj`, `.mtl` và ánh xạ UV | Chưa triển khai |
| **Giai đoạn 05** | **3D Viewport & Rendering** | Tích hợp khung nhìn 3D tăng tốc phần cứng (Camera Orbit/Pan/Zoom, Grid, Wireframe/Textured) | Chưa triển khai |
| **Giai đoạn 06** | **Voxel JSON & Bedrock Geometry** | Triển khai parser/exporter cho Java Block/Item `.json` và Bedrock `.geo.json` | Chưa triển khai |
| **Giai đoạn 07** | **Mesh Optimization & Greedy Meshing** | Thuật toán tối ưu hóa bề mặt Voxel sang Mesh và rút gọn đa giác | Chưa triển khai |
| **Giai đoạn 08** | **Voxelization Engine** | Thuật toán chuyển đổi chiều ngược từ Polygon Mesh (`.obj`) sang Voxel/Cuboid | Chưa triển khai |
| **Giai đoạn 09** | **Interactive 3D Editor Tools** | Công cụ biến đổi Translate/Rotate/Scale, chỉnh sửa Pivot, Bone và Texture | Chưa triển khai |
| **Giai đoạn 10** | **Batch Processing & CLI** | Chuyển đổi hàng loạt đa luồng, hàng đợi ưu tiên và giao diện dòng lệnh | Chưa triển khai |
| **Giai đoạn 11** | **Plugin SDK & Extension Hub** | Cho phép nạp động các định dạng 3D bên thứ ba qua Plugin Assembly | Chưa triển khai |
| **Giai đoạn 12** | **Packaging, Profiling & Release** | Tối ưu bộ nhớ cho mô hình lớn, đóng gói bộ cài đặt và phát hành chính thức | Chưa triển khai |

## 8. Bản Quyền (License)
Dự án được phân phối dưới giấy phép [MIT License](LICENSE).

