# 📘 Telegram Smart Organizer - Project Reference Document

> **Complete technical reference for Phase 2 Week 3 codebase**
>
> **Created:** January 2026
>
> **Last Updated:** January 26, 2026
>
> **Current Version:** Phase 2 Week 3 - Background Window Monitor

---

## ?? Table of Contents

1. [Project Overview](#project-overview)
2. [Architecture](#architecture)
3. [Project Structure](#project-structure)
4. [Core Components](#core-components)
5. [Services Implementation](#services-implementation)
6. [Data Models](#data-models)
7. [UI Layer](#ui-layer)
8. [Current Workflow](#current-workflow)
9. [Technology Stack](#technology-stack)
10. [Known Limitations](#known-limitations)

---

## ?? Project Overview

### What is Telegram Smart Organizer?

**Telegram Smart Organizer** is a Windows desktop application that automatically organizes files downloaded from Telegram by detecting the **context** (active Telegram group/channel) when the download starts.

### Key Features (v1.0.0)

? **Context-Aware Organization**

- Detects active Telegram window title
- Extracts group/channel name
- Organizes files into folders named after the group

? **Temporary File Tracking**

- Monitors `.td`, `.tpart` temporary file extensions
- Waits for download completion
- Maps temp files to final files using rename events

? **Smart File Handling**

- File stability detection (3 consecutive size checks)
- Exponential backoff (500ms ? 2000ms)
- 120-second timeout for large files
- Duplicate event prevention

? **Custom Rules Engine**

- File extension-based rules (e.g., all PDFs ? Documents)
- File name pattern matching (Contains, StartsWith, EndsWith, Regex)
- Group name-based rules
- File size-based rules
- Priority-based rule execution

? **State Persistence**

- JSON-based state storage
- Survives application restarts
- Automatic cleanup of old entries (30 days retention)

? **Statistics & Analytics**

- Total files organized
- File type distribution
- Top groups
- Daily activity tracking (last 30 days)

? **Modern UI**

- WPF with MVVM pattern
- System tray integration
- Dark/Light theme support
- Real-time debug information
- Notifications

---

## ??? Architecture

### Clean Architecture Pattern

The project follows **Clean Architecture** principles with clear separation of concerns:

```
???????????????????????????????????????????????????????
?                    UI Layer (WPF)                   ?
?  - MainWindow, SettingsWindow, RulesWindow, etc.   ?
?  - ViewModels (MVVM pattern)                       ?
?  - Dependency Injection (DI)                       ?
???????????????????????????????????????????????????????
                   ? Depends on
???????????????????????????????????????????????????????
?              Infrastructure Layer                   ?
?  - Concrete implementations of interfaces          ?
?  - Win32ContextDetector, FileOrganizerService      ?
?  - JsonPersistenceService, WindowsWatcherService   ?
?  - File I/O, Windows API calls                     ?
???????????????????????????????????????????????????????
                   ? Implements
???????????????????????????????????????????????????????
?                  Core Layer                         ?
?  - Interfaces (Contracts)                          ?
?  - Models (Domain entities)                        ?
?  - SmartOrganizerEngine (Business logic)           ?
?  - NO external dependencies                        ?
???????????????????????????????????????????????????????
```

### Dependency Flow

- **UI ? Infra ? Core** (references)
- **Core** has ZERO external dependencies
- **Infra** implements Core contracts
- **UI** consumes services through DI

---

## ?? Project Structure

```
TelegramOrganizer/
?
??? TelegramOrganizer.Core/             [Domain Layer - No Dependencies]
?   ??? Contracts/                      [Interfaces]
?   ?   ??? IFileWatcher.cs            ? File monitoring contract
?   ?   ??? IContextDetector.cs        ? Window context detection
?   ?   ??? IFileOrganizer.cs          ? File organization logic
?   ?   ??? IPersistenceService.cs     ? State persistence
?   ?   ??? ISettingsService.cs        ? App settings
?   ?   ??? IRulesService.cs           ? Custom rules
?   ?   ??? IStatisticsService.cs      ? Analytics
?   ?   ??? ILoggingService.cs         ? Logging abstraction
?   ?   ??? IUpdateService.cs          ? Update checking
?   ?   ??? IErrorReportingService.cs  ? Error handling
?   ?
?   ??? Models/                         [Domain Models]
?   ?   ??? AppSettings.cs             ? Application settings
?   ?   ??? AppState.cs                ? Persistent state
?   ?   ??? FileContext.cs             ? Download context info
?   ?   ??? OrganizationRule.cs        ? Custom rule definition
?   ?   ??? OrganizationStatistics.cs  ? Statistics data
?   ?
?   ??? Services/                       [Core Business Logic]
?       ??? SmartOrganizerEngine.cs    ? Main orchestrator
?
??? TelegramOrganizer.Infra/            [Infrastructure Layer]
?   ??? Services/                       [Concrete Implementations]
?       ??? WindowsWatcherService.cs   ? FileSystemWatcher wrapper
?       ??? Win32ContextDetector.cs    ? Win32 API for window detection
?       ??? FileOrganizerService.cs    ? File move/organization logic
?       ??? JsonPersistenceService.cs  ? JSON state storage
?       ??? JsonSettingsService.cs     ? JSON settings storage
?       ??? JsonRulesService.cs        ? JSON rules storage
?       ??? JsonStatisticsService.cs   ? JSON statistics storage
?       ??? FileLoggingService.cs      ? File-based logging
?       ??? GitHubUpdateService.cs     ? GitHub releases API
?       ??? ErrorReportingService.cs   ? Error log generation
?
??? TelegramOrganizer.UI/               [Presentation Layer - WPF]
?   ??? App.xaml.cs                    ? Application entry + DI setup
?   ??? MainWindow.xaml                ? Main application window
?   ?
?   ??? ViewModels/                    [MVVM ViewModels]
?   ?   ??? MainViewModel.cs          ? Main window logic
?   ?   ??? SettingsViewModel.cs      ? Settings management
?   ?   ??? RulesViewModel.cs         ? Rules management
?   ?   ??? StatisticsViewModel.cs    ? Statistics display
?   ?
?   ??? Views/                         [XAML Windows]
?   ?   ??? SettingsWindow.xaml       ? Settings UI
?   ?   ??? RulesWindow.xaml          ? Rules editor UI
?   ?   ??? StatisticsWindow.xaml     ? Statistics UI
?   ?
?   ??? Themes/                        [UI Themes]
?       ??? DarkTheme.xaml
?       ??? LightTheme.xaml
?
??? TelegramOrganizer.Tests/           [Unit Tests]
    ??? Services/
    ?   ??? SmartOrganizerEngineTests.cs
    ?   ??? FileOrganizerServiceTests.cs
    ?   ??? JsonSettingsServiceTests.cs
    ?   ??? JsonRulesServiceTests.cs
    ?   ??? JsonStatisticsServiceTests.cs
    ??? Helpers/
        ??? TextHelperTests.cs
```

---

## ?? Core Components

### 1. SmartOrganizerEngine

**Location:** `TelegramOrganizer.Core/Services/SmartOrganizerEngine.cs`

**Purpose:** Main orchestrator that coordinates all services.

**Key Responsibilities:**

- Starts/stops file monitoring
- Handles file created/renamed events
- Captures window context when downloads start
- Manages pending downloads tracking
- Coordinates file organization

**Critical Data Structures:**

```csharp
// Thread-safe dictionaries for tracking
private readonly ConcurrentDictionary<string, FileContext> _pendingDownloads = new();
private readonly ConcurrentDictionary<string, DateTime> _processingFiles = new();
```

**Event Handlers:**

1. **`OnFileCreated()`**
   - Triggered when a new file appears in Downloads folder
   - Captures active window title using `IContextDetector`
   - Extracts Telegram group name
   - Creates `FileContext` and stores in `_pendingDownloads`
   - For temp files (.td, .tpart): tracks and waits
   - For direct downloads: immediately starts organization

2. **`OnFileRenamed()`**
   - Triggered when file is renamed (temp ? final)
   - Looks up context from `_pendingDownloads`
   - Starts organization with stored context
   - Fallback: uses current window context if not found

**Key Methods:**

```csharp
// Wait for file to be ready (not locked)
private async Task<bool> WaitForFileReady(string filePath, int timeoutMs)
{
    // - Uses exponential backoff (500ms ? 2000ms)
    // - Checks file size stability (3 consecutive same size)
    // - Attempts exclusive file access
    // - 120-second timeout for large files
}

// Extract group name from window title
private string ExtractTelegramGroupName(string windowTitle)
{
    // - Removes unread count: "(123) GroupName"
    // - Removes message count: "GroupName � (3082)"
    // - Removes " - Telegram" suffix
    // - Removes emojis (keeps Arabic/English text)
    // - Returns "Unsorted" if empty
}

// Check if file is temporary
private bool IsTemporaryFile(string fileName)
{
    // Returns true for: .td, .tpart, .crdownload, .part, .tmp, .download
}
```

---

### 2. IContextDetector / Win32ContextDetector

**Location:** `TelegramOrganizer.Infra/Services/Win32ContextDetector.cs`

**Purpose:** Detects the currently active window and extracts context.

**Implementation:**

```csharp
[DllImport("user32.dll")]
private static extern IntPtr GetForegroundWindow();

[DllImport("user32.dll")]
private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

[DllImport("user32.dll")]
private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

public string GetActiveWindowTitle()
{
    // Gets handle of foreground window
    // Retrieves window title text
}

public string GetProcessName()
{
    // Gets process ID from window handle
    // Returns process name (e.g., "Telegram")
}
```

**?? LIMITATION:** Only detects **foreground** window (current v1.0.0 issue)

---

### 3. IFileWatcher / WindowsWatcherService

**Location:** `TelegramOrganizer.Infra/Services/WindowsWatcherService.cs`

**Purpose:** Monitors Downloads folder for file system changes.

**Implementation:**

```csharp
private FileSystemWatcher? _watcher;

public void Start(string path)
{
    _watcher = new FileSystemWatcher
    {
        Path = path,
        NotifyFilter = NotifyFilters.FileName |
                      NotifyFilters.LastWrite |
                      NotifyFilters.CreationTime,
        Filter = "*.*",
        EnableRaisingEvents = true,
        IncludeSubdirectories = false
    };

    _watcher.Created += OnCreated;
    _watcher.Renamed += OnRenamed;
}
```

**Events:**

- `FileCreated` ? New file detected
- `FileRenamed` ? File renamed (temp ? final)

---

### 4. IFileOrganizer / FileOrganizerService

**Location:** `TelegramOrganizer.Infra/Services/FileOrganizerService.cs`

**Purpose:** Moves files to organized folders.

**Workflow:**

```
1. Check if custom rule matches
   ?? Yes ? Use rule's target folder
   ?? No  ? Use detected group name

2. Sanitize folder name
   - Remove invalid characters
   - Keep Arabic/English text
   - Limit to 100 characters

3. Create destination folder
   Path: DestinationBasePath/GroupName/

4. Handle duplicates
   file.pdf ? file (1).pdf ? file (2).pdf

5. Move file
   File.Move(source, destination)

6. Record statistics
   - Increment file count
   - Track file size
   - Update group statistics
   - Record file type
```

**Key Methods:**

```csharp
public string OrganizeFile(string filePath, string groupName)
{
    // 1. Check rules
    var matchingRule = _rulesService.FindMatchingRule(fileName, groupName, fileSize);

    // 2. Sanitize folder name
    string safeFolderName = SanitizeFolderName(targetFolder);

    // 3. Create destination
    string destinationFolder = Path.Combine(baseDestination, safeFolderName);
    Directory.CreateDirectory(destinationFolder);

    // 4. Get unique path
    string destPath = GetUniqueFilePath(destPath);

    // 5. Move file
    File.Move(filePath, destPath);

    // 6. Record stats
    _statisticsService.RecordFileOrganized(fileName, safeFolderName, fileSize);
}
```

---

### 5. IPersistenceService / JsonPersistenceService

**Location:** `TelegramOrganizer.Infra/Services/JsonPersistenceService.cs`

**Purpose:** Persists application state to survive restarts.

**Storage Location:** `%LocalAppData%\TelegramOrganizer\state.json`

**Data Structure:**

```json
{
  "pendingDownloads": {
    "file1.pdf.td": {
      "originalTempName": "file1.pdf.td",
      "detectedGroupName": "CS50 Study Group",
      "capturedAt": "2025-01-19T10:30:00"
    }
  },
  "lastSavedAt": "2025-01-19T10:30:15",
  "version": "1.0.0",
  "totalFilesOrganized": 1247
}
```

**Key Methods:**

```csharp
public void AddOrUpdateEntry(string fileName, FileContext context)
{
    // Thread-safe update
    // Saves state immediately
}

public void RemoveEntry(string fileName)
{
    // Removes completed download
    // Increments TotalFilesOrganized
}

public int CleanupOldEntries(int retentionDays = 30)
{
    // Removes entries older than retention period
    // Called on engine start
}
```

**Thread Safety:** Uses `lock (_lock)` for all operations.

---

### 6. IRulesService / JsonRulesService

**Location:** `TelegramOrganizer.Infra/Services/JsonRulesService.cs`

**Purpose:** Manages custom organization rules.

**Storage Location:** `%LocalAppData%\TelegramOrganizer\rules.json`

**Rule Types:**

```csharp
public enum RuleType
{
    FileExtension,    // e.g., .pdf, .jpg
    FileNamePattern,  // e.g., contains "invoice"
    GroupName,        // e.g., from "Work Group"
    FileSize,         // e.g., 0-1024 KB
    Combined          // Multiple conditions
}
```

**Pattern Matching:**

```csharp
public enum PatternMatchType
{
    Exact,       // Exact match
    Contains,    // Substring
    StartsWith,  // Prefix
    EndsWith,    // Suffix
    Regex        // Regular expression
}
```

**Default Rules:** (Disabled by default)

- Images: `.jpg|.jpeg|.png|.gif|.bmp|.svg|.webp` ? Images/
- Documents: `.pdf|.docx|.doc|.txt|.xlsx|.pptx` ? Documents/
- Videos: `.mp4|.mkv|.avi|.mov|.wmv` ? Videos/
- Audio: `.mp3|.wav|.flac|.aac|.ogg` ? Audio/
- Archives: `.zip|.rar|.7z|.tar|.gz` ? Archives/

**Priority System:** Higher priority rules execute first.

---

### 7. IStatisticsService / JsonStatisticsService

**Location:** `TelegramOrganizer.Infra/Services/JsonStatisticsService.cs`

**Purpose:** Tracks organization metrics.

**Storage Location:** `%LocalAppData%\TelegramOrganizer\statistics.json`

**Tracked Metrics:**

```csharp
public class OrganizationStatistics
{
    public int TotalFilesOrganized { get; set; }          // Lifetime counter
    public long TotalSizeBytes { get; set; }              // Total size
    public Dictionary<string, int> TopGroups { get; set; } // Top 10 groups
    public Dictionary<string, int> FileTypeDistribution { get; set; } // .pdf: 50, .jpg: 30
    public Dictionary<DateTime, int> DailyActivity { get; set; } // Last 30 days
    public DateTime LastUpdated { get; set; }
}
```

---

## ?? Data Models

### FileContext

**Purpose:** Stores context information for a pending download.

```csharp
public class FileContext
{
    public string OriginalTempName { get; set; }    // "file.pdf.td"
    public string DetectedGroupName { get; set; }   // "CS50 Study Group"
    public DateTime CapturedAt { get; set; }        // Timestamp for cleanup
}
```

### AppSettings

**Purpose:** User-configurable application settings.

```csharp
public class AppSettings
{
    public string DestinationBasePath { get; set; }  // "C:\Users\...\Documents\Telegram Organized"
    public string DownloadsFolderPath { get; set; }  // "C:\Users\...\Downloads"
    public int RetentionDays { get; set; } = 30;
    public bool StartMinimized { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool UseDarkTheme { get; set; } = false;
    public bool RunOnStartup { get; set; } = false;
    public string Version { get; set; } = "1.0.0";
}
```

### OrganizationRule

**Purpose:** Custom organization rule definition.

```csharp
public class OrganizationRule
{
    public string Id { get; set; }               // Unique GUID
    public string Name { get; set; }             // "Work Documents"
    public string Description { get; set; }      // "All PDFs from work"
    public RuleType RuleType { get; set; }       // FileExtension, etc.
    public string Pattern { get; set; }          // ".pdf"
    public PatternMatchType MatchType { get; set; } // Contains
    public string TargetFolder { get; set; }     // "Work/Documents"
    public int Priority { get; set; } = 0;       // Higher = first
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public int TimesApplied { get; set; } = 0;   // Usage counter
}
```

---

## ??? UI Layer

### Dependency Injection Setup

**Location:** `TelegramOrganizer.UI/App.xaml.cs`

```csharp
private static IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();

    // Core Services (order matters!)
    services.AddSingleton<ILoggingService, FileLoggingService>();
    services.AddSingleton<ISettingsService, JsonSettingsService>();
    services.AddSingleton<IPersistenceService, JsonPersistenceService>();
    services.AddSingleton<IRulesService, JsonRulesService>();
    services.AddSingleton<IStatisticsService, JsonStatisticsService>();
    services.AddSingleton<IContextDetector, Win32ContextDetector>();
    services.AddSingleton<IFileWatcher, WindowsWatcherService>();
    services.AddSingleton<IFileOrganizer, FileOrganizerService>();
    services.AddSingleton<SmartOrganizerEngine>();
    services.AddSingleton<IUpdateService, GitHubUpdateService>();
    services.AddSingleton<IErrorReportingService, ErrorReportingService>();

    // ViewModels
    services.AddTransient<MainViewModel>();
    services.AddTransient<SettingsViewModel>();
    services.AddTransient<RulesViewModel>();
    services.AddTransient<StatisticsViewModel>();

    // Views
    services.AddTransient<MainWindow>();
    services.AddTransient<SettingsWindow>();
    services.AddTransient<RulesWindow>();
    services.AddTransient<StatisticsWindow>();

    return services.BuildServiceProvider();
}
```

### MVVM Pattern

**ViewModels use:**

- `CommunityToolkit.Mvvm` for `ObservableObject` and `RelayCommand`
- `INotifyPropertyChanged` for data binding
- Dependency injection for services

**Example:**

```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentWindowTitle = "Waiting...";

    [RelayCommand]
    private void OpenSettings()
    {
        // Command implementation
    }
}
```

### System Tray Integration

**Features:**

- Minimize to tray
- Context menu (Show, Settings, Rules, Statistics, Exit)
- Balloon notifications on file organization
- Double-click to restore window

---

## ?? Current Workflow

### Complete Download Flow

```
1. USER ACTION
   ?? User clicks "Download" in Telegram

2. FILE CREATED EVENT
   ?? FileSystemWatcher detects new file
   ?? File name: "document.pdf.td" (temp file)

3. CONTEXT CAPTURE
   ?? SmartOrganizerEngine.OnFileCreated()
   ?? Calls: _contextDetector.GetActiveWindowTitle()
   ?? Returns: "CS50 Study Group - Telegram"
   ?? Extracts: "CS50 Study Group"

4. CONTEXT STORAGE
   ?? Creates FileContext:
       {
         OriginalTempName: "document.pdf.td",
         DetectedGroupName: "CS50 Study Group",
         CapturedAt: "2025-01-19T10:30:00"
       }
   ?? Stores in: _pendingDownloads dictionary
   ?? Persists to: state.json

5. DOWNLOAD CONTINUES
   ?? Telegram downloads file
   ?? Multiple rename events may occur:
       - document.pdf.td ? document.pdf.tpart
       - document.pdf.tpart ? document.pdf.tpart2
       - Each rename updates tracking

6. DOWNLOAD COMPLETES
   ?? Final rename: document.pdf.tpart ? document.pdf
   ?? SmartOrganizerEngine.OnFileRenamed()
   ?? Looks up context from _pendingDownloads
   ?? Found: "CS50 Study Group"

7. FILE STABILITY CHECK
   ?? WaitForFileReady() called
   ?? Checks file size 3 times (500ms intervals)
   ?? Attempts exclusive file access
   ?? Timeout: 120 seconds

8. RULE MATCHING
   ?? Calls: _rulesService.FindMatchingRule()
   ?? No rule matches ? uses detected group name

9. FILE ORGANIZATION
   ?? Sanitize folder name: "CS50 Study Group"
   ?? Create folder: "C:\...\Telegram Organized\CS50 Study Group\"
   ?? Move file: document.pdf ? CS50 Study Group\document.pdf

10. CLEANUP
    ?? Remove from _pendingDownloads
    ?? Remove from state.json
    ?? Record statistics
    ?? Show notification: "[SUCCESS] document.pdf ? CS50 Study Group"
```

### Edge Cases Handled

? **Direct Downloads (no temp file)**

- File appears as "document.pdf" immediately
- Marked in `_processingFiles` to prevent duplicates
- Waits 120 seconds for file to be ready
- Organizes immediately without rename tracking

? **Multiple Rename Events**

- Each rename updates `_pendingDownloads` dictionary
- Old key removed, new key added
- State persisted on each update

? **App Restart During Download**

- State loaded from `state.json`
- Pending downloads restored
- Orphaned entries (file doesn't exist) cleaned up

? **Duplicate Events**

- `_processingFiles` dictionary prevents double-processing
- Event deduplication using timestamps

---

## ??? Technology Stack

### Frameworks & Libraries

| Component        | Technology                               | Version  | Purpose              |
| ---------------- | ---------------------------------------- | -------- | -------------------- |
| Target Framework | .NET 8                                   | 8.0      | Modern C# features   |
| UI Framework     | WPF                                      | Built-in | Windows desktop UI   |
| MVVM Toolkit     | CommunityToolkit.Mvvm                    | 8.4.0    | MVVM helpers         |
| DI Container     | Microsoft.Extensions.DependencyInjection | 10.0.2   | Dependency injection |
| Testing          | xUnit + Moq                              | Latest   | Unit testing         |
| System Tray      | Windows.Forms.NotifyIcon                 | Built-in | Tray integration     |

### Windows APIs Used

```csharp
// Window detection
[DllImport("user32.dll")]
private static extern IntPtr GetForegroundWindow();

[DllImport("user32.dll")]
private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

[DllImport("user32.dll")]
private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
```

### File Storage

| Data              | Location                                                 | Format | Size (typical) |
| ----------------- | -------------------------------------------------------- | ------ | -------------- |
| Application State | `%LocalAppData%\TelegramOrganizer\state.json`            | JSON   | ~10 KB         |
| Settings          | `%LocalAppData%\TelegramOrganizer\settings.json`         | JSON   | ~1 KB          |
| Rules             | `%LocalAppData%\TelegramOrganizer\rules.json`            | JSON   | ~5 KB          |
| Statistics        | `%LocalAppData%\TelegramOrganizer\statistics.json`       | JSON   | ~20 KB         |
| Logs              | `%LocalAppData%\TelegramOrganizer\logs\log_YYYYMMDD.txt` | Text   | ~100 KB/day    |

---

## ?? Known Limitations (v1.0.0)

### ?? CRITICAL Issues

#### 1. **Batch Downloads Fail (~40-60% success rate)**

**Problem:**

```
User downloads 50 files at once
? OnFileCreated() fired 50 times
? Each call captures window context independently
? Context might change between files
? Files 2-50 might get wrong group or "Unsorted"
```

**Root Cause:**

```csharp
private void OnFileCreated(object? sender, FileEventArgs e)
{
    // ? Captures context PER FILE, not per SESSION
    string activeWindow = _contextDetector.GetActiveWindowTitle();
    string groupName = ExtractTelegramGroupName(activeWindow);
    // ...
}
```

**Why It Fails:**

- Download events fire rapidly (50 files in <5 seconds)
- User might click away from Telegram during batch
- Window title might change mid-batch
- Each file gets different context

**Impact:** Major UX issue for power users

---

#### 2. **Focus Dependency**

**Problem:**

```
User must keep Telegram window FOCUSED during download
? If user switches to browser: context = "Google Chrome"
? If user minimizes Telegram: context = ""
? Files go to wrong folder or "Unsorted"
```

**Root Cause:**

```csharp
[DllImport("user32.dll")]
private static extern IntPtr GetForegroundWindow(); // ? FOREGROUND ONLY!

public string GetActiveWindowTitle()
{
    IntPtr handle = GetForegroundWindow(); // Only sees focused window
    // ...
}
```

**Why It Fails:**

- `GetForegroundWindow()` ONLY returns the window with keyboard focus
- Telegram might be open but not focused
- No access to background window information

**Impact:** Forces users to stay on Telegram = bad UX

---

### ?? Medium Priority Issues

#### 3. **Performance with Large Files**

**Problem:**

- `WaitForFileReady()` polls file every 500-2000ms
- CPU usage spikes during large file downloads
- Fixed 120-second timeout (not dynamic based on file size)

**Example:**

```
5GB video file
? 120-second timeout might not be enough
? File marked as "failed" but still downloading
```

**Potential Fix:**

- Dynamic timeout based on file size
- Async/await instead of polling
- File size growth rate detection

---

#### 4. **No Multi-Monitor Support**

**Problem:**

- Window detection doesn't account for multi-monitor setups
- Window might be on different monitor

---

#### 5. **File Type Detection**

**Problem:**

- Only uses file extension
- Files without extension ? "Unsorted"
- No MIME type detection

**Example:**

```
"image" (no extension) ? Unsorted
Could be detected as JPG by reading file header
```

---

## ?? Performance Characteristics

### Memory Usage

| Scenario                       | Memory Usage |
| ------------------------------ | ------------ |
| Idle                           | ~50 MB       |
| Active (10 pending downloads)  | ~70 MB       |
| Active (100 pending downloads) | ~120 MB      |

### CPU Usage

| Activity           | CPU Usage           |
| ------------------ | ------------------- |
| Idle               | <1%                 |
| File monitoring    | <2%                 |
| File organization  | 5-10% (brief spike) |
| Large file waiting | 3-5%                |

### Disk I/O

- **State persistence:** ~10 writes/minute (when active)
- **Statistics update:** ~1 write/file organized
- **Logging:** ~5-10 KB/minute (debug logging enabled)

---

## ?? Testing Coverage

### Current Test Stats (v1.0.0)

- **Total Tests:** 63
- **Coverage:** ~75%
- **Test Projects:** TelegramOrganizer.Tests

### Test Categories

1. **Engine Tests** (`SmartOrganizerEngineTests.cs`)
   - Start/Stop behavior
   - Event handling
   - State restoration
   - Cleanup

2. **Organizer Tests** (`FileOrganizerServiceTests.cs`)
   - File moving
   - Rule matching
   - Duplicate handling
   - Name sanitization

3. **Service Tests**
   - Settings persistence
   - Rules management
   - Statistics tracking

---

## ?? Next Steps for v2.0.0

Based on this reference and PLAN_V2.md:

### Phase 1: Database Foundation

1. Add SQLite database
2. Migrate state from JSON to SQLite
3. Create session tracking tables

### Phase 2: Smart Detection

1. Implement Download Burst Detector
2. Implement Background Window Monitor
3. Implement Multi-Source Context Detector

### Phase 3: Learning & Intelligence

1. Implement File Pattern Analyzer
2. Implement Smart Context Cache
3. Add confidence scoring

### Phase 4: Queue & Performance

1. Implement Download Queue Manager
2. Optimize performance
3. Add progress tracking

---

## ?? File Naming Conventions

### Code Files

- **Interfaces:** `I{Name}Service.cs` or `I{Name}.cs`
- **Implementations:** `{Implementation}{Name}Service.cs`
  - Example: `JsonSettingsService`, `Win32ContextDetector`
- **Models:** `{Entity}.cs` (e.g., `AppSettings.cs`)
- **ViewModels:** `{View}ViewModel.cs` (e.g., `MainViewModel.cs`)

### Namespaces

- Core: `TelegramOrganizer.Core.{Contracts|Models|Services}`
- Infra: `TelegramOrganizer.Infra.Services`
- UI: `TelegramOrganizer.UI.{ViewModels|Views}`

---

## ?? Key Design Patterns Used

1. **Dependency Injection:** All services injected via constructor
2. **MVVM:** ViewModels separate from Views
3. **Repository Pattern:** Services abstract data access
4. **Observer Pattern:** Events for file monitoring
5. **Strategy Pattern:** Different rule types
6. **Singleton Pattern:** Service lifetime in DI container

---

## ?? Security Considerations

### Current Implementation

? **Good:**

- No network calls except update check
- No sensitive data storage
- File operations within user folders
- No admin privileges required

?? **Potential Issues:**

- Logs might contain full file paths
- No encryption for state/settings (not needed for current use case)
- No input validation on file paths (handled by OS)

---

## ?? Additional Resources

- **GitHub Repository:** https://github.com/Youssef-Tamer-Abdelhalim/telegram-smart-organizer
- **Development Plan:** See PLAN_V2.md
- **Test Coverage:** Run `dotnet test --collect:"XPlat Code Coverage"`

---

**Document Version:** 1.0  
**Last Updated:** January 2025  
**Created By:** AI Assistant  
**Purpose:** Reference for v2.0.0 development

---

## ?? Quick Reference: Critical Code Locations

### To modify context detection:

- `TelegramOrganizer.Infra/Services/Win32ContextDetector.cs`
- `TelegramOrganizer.Core/Services/SmartOrganizerEngine.cs` ? `OnFileCreated()`

### To modify file organization logic:

- `TelegramOrganizer.Infra/Services/FileOrganizerService.cs`

### To modify state persistence:

- `TelegramOrganizer.Infra/Services/JsonPersistenceService.cs`

### To add new service:

1. Add interface to `TelegramOrganizer.Core/Contracts/`
2. Implement in `TelegramOrganizer.Infra/Services/`
3. Register in `TelegramOrganizer.UI/App.xaml.cs` ? `ConfigureServices()`

---

**End of Reference Document**
