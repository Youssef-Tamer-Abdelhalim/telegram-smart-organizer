# ????? Developer Guide - Telegram Smart Organizer

> ???? ?????? ?????? ???????? ?? ????? ???????

---

## ?? Table of Contents

1. [Development Setup](#development-setup)
2. [Architecture Deep Dive](#architecture-deep-dive)
3. [Code Standards](#code-standards)
4. [Adding New Features](#adding-new-features)
5. [Debugging Tips](#debugging-tips)
6. [Contributing Guidelines](#contributing-guidelines)

---

## ??? Development Setup

### Prerequisites
```bash
- Visual Studio 2022 (v17.8+)
- .NET 8 SDK
- Windows 10/11
- Git
```

### Clone and Build
```bash
git clone https://github.com/yourusername/telegram-smart-organizer.git
cd telegram-smart-organizer
dotnet restore
dotnet build
```

### Run in Debug Mode
```bash
dotnet run --project TelegramOrganizer.UI
```

---

## ??? Architecture Deep Dive

### Project Structure

```
TelegramOrganizer/
??? TelegramOrganizer.Core/          # Domain Layer
?   ??? Contracts/                   # Interfaces
?   ?   ??? IContextDetector.cs
?   ?   ??? IFileWatcher.cs
?   ?   ??? IFileOrganizer.cs
?   ?   ??? IPersistenceService.cs
?   ?   ??? ISettingsService.cs
?   ?   ??? IRulesService.cs         # Phase 3
?   ?   ??? IStatisticsService.cs    # Phase 3
?   ?   ??? ILoggingService.cs       # Phase 3
?   ??? Models/
?   ?   ??? FileContext.cs
?   ?   ??? AppState.cs
?   ?   ??? AppSettings.cs
?   ?   ??? OrganizationRule.cs      # Phase 3
?   ?   ??? OrganizationStatistics.cs # Phase 3
?   ??? Services/
?       ??? SmartOrganizerEngine.cs  # Main orchestrator
?
??? TelegramOrganizer.Infra/         # Infrastructure Layer
?   ??? Services/
?       ??? Win32ContextDetector.cs
?       ??? WindowsWatcherService.cs
?       ??? FileOrganizerService.cs
?       ??? JsonPersistenceService.cs
?       ??? JsonSettingsService.cs
?       ??? JsonRulesService.cs      # Phase 3
?       ??? JsonStatisticsService.cs # Phase 3
?       ??? FileLoggingService.cs    # Phase 3
?
??? TelegramOrganizer.UI/            # Presentation Layer
    ??? ViewModels/
    ?   ??? MainViewModel.cs
    ?   ??? SettingsViewModel.cs
    ?   ??? RulesViewModel.cs        # Phase 3
    ?   ??? StatisticsViewModel.cs   # Phase 3
    ??? Views/
    ?   ??? MainWindow.xaml
    ?   ??? SettingsWindow.xaml
    ?   ??? RulesWindow.xaml         # Phase 3
    ?   ??? StatisticsWindow.xaml    # Phase 3
    ??? App.xaml.cs                  # DI Configuration
```

### Layer Responsibilities

| Layer | Purpose | Dependencies |
|-------|---------|--------------|
| **Core** | Business logic, Interfaces | None |
| **Infra** | Platform-specific implementations | Core |
| **UI** | User interface (MVVM) | Core, Infra |

---

## ?? Design Patterns Used

### 1. Dependency Injection
```csharp
// App.xaml.cs
services.AddSingleton<IFileOrganizer, FileOrganizerService>();
services.AddSingleton<IRulesService, JsonRulesService>();
services.AddSingleton<IStatisticsService, JsonStatisticsService>();
```

### 2. Repository Pattern
```csharp
public interface IPersistenceService
{
    AppState LoadState();
    void SaveState(AppState state);
}
```

### 3. Observer Pattern
```csharp
public event EventHandler<string> OperationCompleted;
```

### 4. MVVM
```csharp
[ObservableProperty]
private string _currentWindowTitle;
```

---

## ?? Code Standards

### Naming Conventions
```csharp
public interface IFileOrganizer { }        // Interfaces: I prefix
public class FileOrganizerService { }      // Classes: PascalCase
private readonly ISettingsService _service; // Fields: _camelCase
public string DestinationPath { get; set; } // Properties: PascalCase
```

### Arabic Text Handling
```csharp
// Supported Unicode ranges for Arabic:
// \u0600-\u06FF (Arabic)
// \u0750-\u077F (Arabic Supplement)
// \uFB50-\uFDFF (Arabic Presentation Forms-A)
// \uFE70-\uFEFF (Arabic Presentation Forms-B)

string cleanName = Regex.Replace(name, 
    @"[^\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFFa-zA-Z0-9\s\-_\.]+", "");
```

---

## ? Adding New Features

### Example: Add New Rule Type

#### Step 1: Update Model
```csharp
// OrganizationRule.cs
public enum RuleType
{
    FileExtension,
    FileNamePattern,
    GroupName,
    FileSize,
    NewRuleType  // Add here
}
```

#### Step 2: Update Service
```csharp
// JsonRulesService.cs
private bool IsMatch(OrganizationRule rule, ...)
{
    switch (rule.RuleType)
    {
        case RuleType.NewRuleType:
            // Implement matching logic
            break;
    }
}
```

#### Step 3: Update ViewModel (if needed)
```csharp
// RulesViewModel.cs
public Array RuleTypes => Enum.GetValues(typeof(RuleType));
```

---

## ?? Debugging Tips

### Enable Debug Logging
- ??? Logs ????? ??: `%LOCALAPPDATA%\TelegramOrganizer\log_*.txt`
- ?????? ?? "Open Logs" ?? ??? UI

### Common Debug Scenarios

| ??????? | ???? |
|---------|------|
| ??? ?? ?????? | ???? ??? Log ??? [WARN] ?? [ERROR] |
| Timeout | ???? ?? ?????? ??????? ??? 2 ????? |
| ??? ???? | ???? `ExtractTelegramGroupName()` |

### Key Log Messages
```
[INFO ] Engine starting...
[DEBUG] File: test.pdf | Window: GroupName
[FILE ] [ORGANIZED] File: test.pdf | Group: GroupName
[WARN ] File not ready...
[ERROR] Failed to organize...
```

---

## ?? Data Storage

### File Locations
```
%LOCALAPPDATA%\TelegramOrganizer\
??? settings.json      # User preferences
??? state.json         # Pending downloads
??? rules.json         # Custom rules
??? statistics.json    # Usage statistics
??? log_YYYY-MM-DD.txt # Daily logs
```

### JSON Schemas

#### settings.json
```json
{
  "destinationBasePath": "...",
  "downloadsFolderPath": "...",
  "retentionDays": 30,
  "startMinimized": false,
  "minimizeToTray": true,
  "showNotifications": true,
  "runOnStartup": false
}
```

#### rules.json
```json
[{
  "Id": "guid",
  "Name": "Documents",
  "RuleType": "FileExtension",
  "Pattern": ".pdf|.docx",
  "MatchType": "Contains",
  "TargetFolder": "Documents",
  "Priority": 10,
  "IsEnabled": true
}]
```

#### statistics.json
```json
{
  "TotalFilesOrganized": 100,
  "TotalSizeBytes": 1073741824,
  "TopGroups": {"GroupA": 50, "GroupB": 30},
  "FileTypeDistribution": {".pdf": 60, ".jpg": 40},
  "DailyActivity": {"2024-01-15": 10}
}
```

---

## ?? Contributing Guidelines

### Git Workflow
```bash
git checkout -b feature/my-feature
git commit -m "feat: Add new feature"
git push origin feature/my-feature
# Create Pull Request
```

### Commit Types
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `refactor`: Code refactoring

### Pull Request Checklist
- [ ] Build succeeds
- [ ] No compiler warnings
- [ ] Tested manually
- [ ] Documentation updated
- [ ] CHANGELOG.md updated

---

*Happy Coding! ??*
