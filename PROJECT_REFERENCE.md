# 📘 Telegram Smart Organizer - Project Reference Document

> **Complete technical reference for V2.0 Full Integration**
>
> **Created:** January 2026
>
> **Last Updated:** January 2026
>
> **Current Version:** V2.0 - Full Integration (All Services Required)

---

## 📑 Table of Contents

1. [Project Overview](#project-overview)
2. [Architecture](#architecture)
3. [Project Structure](#project-structure)
4. [Core Components](#core-components)
5. [V2.0 Services](#v20-services)
6. [Data Models](#data-models)
7. [UI Layer](#ui-layer)
8. [Current Workflow (V2.0)](#current-workflow-v20)
9. [Technology Stack](#technology-stack)
10. [Performance Benchmarks](#performance-benchmarks)

---

## 📋 Project Overview

### What is Telegram Smart Organizer?

**Telegram Smart Organizer** is a Windows desktop application that automatically organizes files downloaded from Telegram by detecting the **context** (active Telegram group/channel) when the download starts.

### Key Features (V2.0)

✅ **Context-Aware Organization**

- Detects active Telegram window title
- Background window monitoring (V2.0)
- Session-based batch download handling (V2.0)
- Extracts group/channel name
- Organizes files into folders named after the group

✅ **V2.0 Session Management**

- Download session tracking
- Burst detection (3+ files in 5 seconds)
- Session timeout handling (30 seconds)
- File-to-session mapping

✅ **SQLite Database (V2.0 Primary Storage)**

- Sessions, files, patterns, statistics
- Context cache for faster lookups
- File pattern learning
- Automatic migration from JSON

✅ **Smart File Handling**

- File stability detection (3 consecutive size checks)
- Exponential backoff (500ms → 2000ms)
- 120-second timeout for large files
- Duplicate event prevention

✅ **Custom Rules Engine**

- File extension-based rules
- File name pattern matching
- Group name-based rules
- Priority-based rule execution

✅ **Statistics & Analytics**

- Total files organized
- File type distribution
- Top groups
- Daily activity tracking
- Batch vs single download stats (V2.0)

---

## 🏗️ Architecture

### Clean Architecture Pattern (V2.0)

```
┌─────────────────────────────────────────────┐
│            UI Layer (WPF)                   │
│  - MainWindow, SettingsWindow, etc.         │
│  - ViewModels (MVVM pattern)                │
│  - Dependency Injection (DI)                │
└─────────────────────────────────────────────┘
                   ↓ Depends on
┌─────────────────────────────────────────────┐
│        Infrastructure Layer                 │
│  - SQLiteDatabaseService (V2.0 PRIMARY)     │
│  - Win32ContextDetector                     │
│  - FileOrganizerService                     │
│  - BackgroundWindowMonitor (V2.0)           │
│  - DownloadSessionManager (V2.0)            │
│  - DownloadBurstDetector (V2.0)             │
└─────────────────────────────────────────────┘
                   ↓ Implements
┌─────────────────────────────────────────────┐
│            Core Layer                       │
│  - Interfaces (Contracts)                   │
│  - Models (Domain entities)                 │
│  - SmartOrganizerEngine (V2.0)              │
│  - NO external dependencies                 │
└─────────────────────────────────────────────┘
```

### V2.0 Service Dependencies

```
SmartOrganizerEngine (V2.0)
├── IFileWatcher (required) ✅
├── IContextDetector (required) ✅
├── IFileOrganizer (required) ✅
├── ISettingsService (required) ✅
├── ILoggingService (required) ✅
├── IDatabaseService (V2.0)         ✅
├── IDownloadSessionManager (V2.0)  ✅
├── IDownloadBurstDetector (V2.0)   ✅
└── IBackgroundWindowMonitor (V2.0) ✅
```

**Note:** V2.0 services are now REQUIRED, not optional. The engine will throw `ArgumentNullException` if any are missing.

---

## 📁 Project Structure

```
TelegramOrganizer/
│
├── TelegramOrganizer.Core/             [Domain Layer - No Dependencies]
│   ├── Contracts/                      [Interfaces]
│   │   ├── IFileWatcher.cs
│   │   ├── IContextDetector.cs
│   │   ├── IFileOrganizer.cs
│   │   ├── ISettingsService.cs
│   │   ├── ILoggingService.cs
│   │   ├── IDatabaseService.cs         [V2.0]
│   │   ├── IDownloadSessionManager.cs  [V2.0]
│   │   ├── IDownloadBurstDetector.cs   [V2.0]
│   │   └── IBackgroundWindowMonitor.cs [V2.0]
│   │
│   ├── Models/
│   │   ├── DownloadSession.cs          [V2.0]
│   │   ├── FilePattern.cs              [V2.0]
│   │   ├── BurstDetectionResult.cs     [V2.0]
│   │   ├── WindowInfo.cs               [V2.0]
│   │   └── ...
│   │
│   └── Services/
│       └── SmartOrganizerEngine.cs     [V2.0 Required Services]
│
├── TelegramOrganizer.Infra/            [Infrastructure Layer]
│   ├── Services/
│   │   ├── SQLiteDatabaseService.cs    [V2.0 PRIMARY]
│   │   ├── DownloadSessionManager.cs   [V2.0]
│   │   ├── DownloadBurstDetector.cs    [V2.0]
│   │   ├── BackgroundWindowMonitor.cs  [V2.0]
│   │   ├── Win32ContextDetector.cs
│   │   ├── FileOrganizerService.cs
│   │   └── ...
│   │
│   └── Data/
│       ├── DatabaseEntities.cs         [V2.0]
│       └── Migrations/
│           └── JsonToSQLiteMigration.cs [V2.0]
│
├── TelegramOrganizer.UI/               [Presentation Layer]
│   ├── App.xaml.cs                     [V2.0 DI Configuration]
│   └── ...
│
└── TelegramOrganizer.Tests/            [Unit Tests]
    ├── Services/
    │   ├── SmartOrganizerEngineTests.cs
    │   ├── SQLiteDatabaseServiceTests.cs
    │   └── ...
    └── Performance/
        └── PerformanceBenchmarkTests.cs [V2.0]
```

---

## 🔧 Core Components

### SmartOrganizerEngine (V2.0)

**Location:** `TelegramOrganizer.Core/Services/SmartOrganizerEngine.cs`

**Constructor (V2.0 - All Required):**

```csharp
public SmartOrganizerEngine(
    IFileWatcher watcher,
    IContextDetector contextDetector,
    IFileOrganizer fileOrganizer,
    ISettingsService settingsService,
    ILoggingService loggingService,
    IDownloadSessionManager sessionManager,      // V2.0 REQUIRED
    IDownloadBurstDetector burstDetector,        // V2.0 REQUIRED
    IBackgroundWindowMonitor windowMonitor)      // V2.0 REQUIRED
```

**Key V2.0 Behaviors:**

- Always uses session manager for file tracking
- Burst detection active for all downloads
- Background window monitoring runs continuously
- No fallback to V1.0 JSON persistence

---

## 🆕 V2.0 Services

### IDatabaseService / SQLiteDatabaseService

**Purpose:** Primary data storage for V2.0

**Storage Location:** `%LocalAppData%\TelegramOrganizer\organizer.db`

**Tables:**
- `download_sessions` - Session tracking
- `session_files` - Files in sessions
- `file_patterns` - Learned patterns
- `file_statistics` - Organization history
- `context_cache` - Window title cache
- `app_state` - Key-value settings
- `schema_version` - Migration tracking

### IDownloadSessionManager / DownloadSessionManager

**Purpose:** Manages download sessions for batch handling

**Key Methods:**
```csharp
Task<DownloadSession> AddFileToSessionAsync(fileName, groupName, filePath, fileSize);
Task<DownloadSession?> GetActiveSessionAsync();
Task EndCurrentSessionAsync();
Task<int> CheckAndEndTimedOutSessionsAsync();
```

### IDownloadBurstDetector / DownloadBurstDetector

**Purpose:** Detects rapid file downloads (bursts)

**Events:**
- `BurstStarted` - 3+ files in 5 seconds
- `BurstContinued` - More files added
- `BurstEnded` - 30 seconds of inactivity

### IBackgroundWindowMonitor / BackgroundWindowMonitor

**Purpose:** Tracks Telegram windows in background

**Events:**
- `WindowDetected` - New Telegram window found
- `WindowActivated` - Window became active
- `WindowRemoved` - Window closed

---

## 📊 Current Workflow (V2.0)

```
1. FILE CREATED EVENT
   ↓
2. BURST DETECTION
   → Record download in burst detector
   → Check if part of active burst
   ↓
3. SESSION MANAGEMENT
   → Add file to active session (or create new)
   → Session tracks group context
   ↓
4. BACKGROUND MONITORING
   → Window monitor provides context enrichment
   → Works even when Telegram unfocused
   ↓
5. FILE ORGANIZATION
   → Uses session's group name
   → Organizes to destination folder
   ↓
6. DATABASE UPDATE
   → Record in SQLite statistics
   → Update patterns for learning
   ↓
7. SESSION TIMEOUT
   → Auto-end sessions after 30s inactivity
```

---

## ⚡ Performance Benchmarks

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Batch Download (100 files) | < 5s | ~2s | ✅ |
| Database Size (1000 files) | < 2MB | ~500KB | ✅ |
| Single File Operation | < 50ms | ~5ms | ✅ |
| Session Management | < 10s | ~3s | ✅ |
| Pattern Matching | < 1s | ~100ms | ✅ |
| Statistics Retrieval | < 500ms | ~50ms | ✅ |
| Context Cache | < 2s | ~500ms | ✅ |
| Database Maintenance | < 5s | ~100ms | ✅ |

---

## 🛠️ Technology Stack

| Component | Technology | Version | Purpose |
|-----------|------------|---------|---------|
| Target Framework | .NET 8 | 8.0 | Modern C# features |
| UI Framework | WPF | Built-in | Windows desktop UI |
| Database | SQLite | sqlite-net-pcl | V2.0 data storage |
| MVVM Toolkit | CommunityToolkit.Mvvm | 8.4.0 | MVVM helpers |
| DI Container | Microsoft.Extensions.DI | 10.0.2 | Dependency injection |
| Testing | xUnit + Moq | Latest | Unit testing |

---

## 📝 Data Storage (V2.0)

| Data | Location | Format | Purpose |
|------|----------|--------|---------|
| Database | `%LocalAppData%\TelegramOrganizer\organizer.db` | SQLite | Primary storage |
| Settings | `%LocalAppData%\TelegramOrganizer\settings.json` | JSON | User preferences |
| Rules | `%LocalAppData%\TelegramOrganizer\rules.json` | JSON | Custom rules |
| Statistics | `%LocalAppData%\TelegramOrganizer\statistics.json` | JSON | UI display cache |
| Logs | `%LocalAppData%\TelegramOrganizer\logs\` | Text | Debug logs |

---

## 🧪 Testing

### Test Statistics (V2.0)

- **Total Tests:** 141
- **Passing:** 141 ✅
- **Coverage:** ~80%

### Test Categories

1. **Engine Tests** - SmartOrganizerEngine behavior
2. **Database Tests** - SQLiteDatabaseService operations
3. **Session Tests** - DownloadSessionManager
4. **Burst Tests** - DownloadBurstDetector
5. **Monitor Tests** - BackgroundWindowMonitor
6. **Performance Tests** - Benchmarks

---

## 🚀 Quick Reference

### Add new V2.0 feature:

1. Add interface to `TelegramOrganizer.Core/Contracts/`
2. Implement in `TelegramOrganizer.Infra/Services/`
3. Add to SmartOrganizerEngine constructor
4. Register in `App.xaml.cs` DI configuration
5. Write tests
6. Update documentation

### Run tests:

```bash
dotnet test --verbosity minimal
```

### Run benchmarks:

```bash
dotnet test --filter "FullyQualifiedName~PerformanceBenchmarkTests"
```

---

**Document Version:** 2.0  
**Last Updated:** January 2026  
**Status:** V2.0 Full Integration Complete ✅
