# 🚀 Telegram Smart Organizer v2.0.0 - Development Plan

> **Major Release - Next Generation Architecture**

---

## 📊 Current State Analysis (v1.0.0)

### ✅ **نقاط القوة (Strengths)**

#### 1. **معمارية نظيفة ومنظمة**

- ✅ Clean Architecture (Core/Infra/UI layers)
- ✅ Dependency Injection بشكل صحيح
- ✅ MVVM Pattern محترم
- ✅ Unit Tests موجودة (63 test)

#### 2. **التعامل مع الملفات المؤقتة**

- ✅ نظام تتبع الملفات المؤقتة `.td`, `.tpart`
- ✅ File Stability Check (3 consecutive checks)
- ✅ Timeout ممتاز (120 ثانية)
- ✅ Exponential Backoff

#### 3. **إدارة البيانات**

- ✅ JSON Persistence للـ state
- ✅ Settings Service محترم
- ✅ Rules Engine قوي
- ✅ Statistics Tracking

#### 4. **تجربة المستخدم**

- ✅ System Tray Integration
- ✅ Notifications
- ✅ Custom Rules
- ✅ Dark Theme Support
- ✅ Arabic Text Support ممتاز

#### 5. **الأمان والاستقرار**

- ✅ Thread-Safe (`ConcurrentDictionary`)
- ✅ Comprehensive Logging
- ✅ Error Handling محترم
- ✅ Duplicate Event Prevention

---

### ❌ **نقاط الضعف (Critical Weaknesses)**

#### 🔴 **المشكلة #1: Batch Downloads Support (CRITICAL)**

**الوصف:**

- حالياً: البرنامج بيلتقط context **مرة واحدة** عند event `FileCreated`
- المشكلة: لما اليوزر ينزل **50 ملف مرة واحدة**، الـ context بيتاخد للملف الأول بس
- النتيجة: باقي الـ 49 ملف ممكن يروحوا `Unsorted` أو لـ group غلط

**السبب التقني:**

```csharp
private void OnFileCreated(object? sender, FileEventArgs e)
{
    // ❌ هنا بناخد الـ context مرة واحدة فقط
    string activeWindow = _contextDetector.GetActiveWindowTitle();
    string groupName = ExtractTelegramGroupName(activeWindow);

    // لو في 50 ملف بينزلوا في نفس الوقت
    // كل ملف هياخد context مختلف حسب timing الـ event
}
```

**الأثر:**

- Success Rate: ~40-60% في حالة batch downloads
- User Frustration عالي جداً
- الملفات بتروح أماكن غلط

---

#### 🔴 **المشكلة #2: Focus Dependency (CRITICAL)**

**الوصف:**

- حالياً: البرنامج **معتمد 100%** على `GetForegroundWindow()`
- المشكلة: لو اليوزر:
  - فتح تاب تاني في البراوزر
  - راح على application تاني
  - قفل window Telegram
  - مينيمايز الويندو
- النتيجة: الملفات **مش هتترتب صح**

**السبب التقني:**

```csharp
[DllImport("user32.dll")]
private static extern IntPtr GetForegroundWindow(); // ❌ Foreground only!

public string GetActiveWindowTitle()
{
    IntPtr handle = GetForegroundWindow(); // ❌ بيشوف اللي عليه focus بس
    // ...
}
```

**الأثر:**

- User must keep Telegram focused = سيئ جداً للـ UX
- ما ينفعش يعمل أي حاجة تانية أثناء التحميل
- Workflow interruption

---

#### 🟡 **مشاكل تانية (Medium Priority)**

##### 3. **Performance مع الملفات الكبيرة**

- `WaitForFileReady()` بتستهلك CPU لو الملف كبير (>500MB)
- مفيش progress indication
- الـ timeout ثابت (120s) مش dynamic

##### 4. **Multi-Monitor Support**

- مفيش handling للـ multi-monitor scenarios
- Window detection ممكن يفشل

##### 5. **Network Drive Support**

- مفيش support للـ network drives
- UNC paths ممكن تعمل مشاكل

##### 6. **File Type Detection**

- الاعتماد على extension بس
- مفيش MIME type detection
- ملفات بدون extension بتروح Unsorted

---

## 🎯 v2.0.0 Goals & Solutions

### 🔥 **Critical Fixes**

#### **Solution #1: Intelligent Batch Download Handler**

**الفكرة:**
بدل ما ناخد context للملف الواحد، نعمل **session-based tracking**:

```
1. Detect Download Session Start
   ↓
2. Capture Context ONCE for the entire session
   ↓
3. Apply same context to ALL files in the session
   ↓
4. Session expires after inactivity timeout (e.g., 30 seconds)
```

**التنفيذ المقترح:**

```csharp
// New Service: IDownloadSessionManager
public interface IDownloadSessionManager
{
    DownloadSession? GetActiveSession();
    void StartSession(string groupName);
    void AddFileToSession(string fileName);
    void EndSession();
    bool IsSessionActive();
}

public class DownloadSession
{
    public string GroupName { get; set; }
    public DateTime SessionStart { get; set; }
    public DateTime LastActivity { get; set; }
    public List<string> FilesInSession { get; set; }
    public int FileCount => FilesInSession.Count;
}
```

**الفوايد:**

- ✅ Success Rate: 95%+ في batch downloads
- ✅ Consistent organization
- ✅ User doesn't need to keep focus
- ✅ Handles 100+ files easily

---

#### **Solution #2: Background Window Monitoring**

**الفكرة:**
استخدام **multiple detection strategies** بدل الاعتماد على foreground بس:

**Strategy A: Process Monitoring**

```csharp
// Monitor Telegram process CONTINUOUSLY
public interface IProcessMonitor
{
    List<TelegramWindow> GetAllTelegramWindows(); // All windows, not just foreground
    TelegramWindow? GetMostRecentlyActive();
    event EventHandler<TelegramWindow> WindowActivated;
}
```

**Strategy B: Telegram Local Database**

```csharp
// Read Telegram's local database to get current active chat
// Path: %APPDATA%\Telegram Desktop\tdata\
public interface ITelegramDataReader
{
    string? GetCurrentActiveChat();
    ChatInfo? GetChatInfo(long chatId);
}
```

**Strategy C: File Metadata Tracking**

```csharp
// Track file creation patterns
public interface IFilePatternAnalyzer
{
    string PredictGroupFromPattern(string fileName, DateTime createdTime);
    void LearnPattern(string fileName, string actualGroup);
}
```

**الفوايد:**

- ✅ Works even if Telegram is minimized
- ✅ Works with multiple Telegram windows
- ✅ Learns from user behavior
- ✅ Fallback strategies

---

### 🚀 **Major Features**

#### **Feature #1: SQLite Database Integration**

**الفكرة:**
استبدال JSON بـ SQLite للبيانات المعقدة:

1. **Session Tracking**
   - Download sessions with relationships
   - File-to-session mapping
   - Active session detection

2. **Pattern Learning Database**
   - File patterns with confidence scores
   - Time-based patterns (hour/day)
   - Context accuracy tracking

3. **Advanced Analytics**
   - Complex queries للإحصائيات
   - Historical data analysis
   - Performance optimization with indexes

**الفوايد:**

- ✅ Fast querying with indexes
- ✅ Relationships (Foreign Keys)
- ✅ Scalable (100K+ records)
- ✅ Transaction support
- ✅ Only ~1MB overhead

**Schema:**

```sql
-- Download Sessions
CREATE TABLE download_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    group_name TEXT NOT NULL,
    start_time DATETIME NOT NULL,
    file_count INTEGER DEFAULT 0,
    is_active BOOLEAN DEFAULT 1
);

-- Pattern Learning
CREATE TABLE file_patterns (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_extension TEXT,
    group_name TEXT NOT NULL,
    confidence_score REAL,
    times_seen INTEGER,
    times_correct INTEGER
);

-- Statistics
CREATE TABLE file_statistics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_name TEXT,
    source_group TEXT,
    was_batch_download BOOLEAN,
    session_id INTEGER,
    timestamp DATETIME
);
```

---

#### **Feature #2: Smart Learning System**

**الفكرة:**
ML-based pattern recognition:

```csharp
public interface ISmartLearningEngine
{
    void TrainFromHistory(List<OrganizedFile> history);
    string PredictGroup(FileInfo file, DateTime downloadTime);
    double GetConfidenceScore(string prediction);
    void UpdateModel(string fileName, string actualGroup, bool wasCorrect);
}
```

**الفوايد:**

- ✅ Learns user's download patterns
- ✅ Predicts group even without context
- ✅ Gets smarter over time
- ✅ Confidence scoring

---

#### **Feature #3: Download Queue Manager**

**الفكرة:**
Proper queue management للـ batch downloads:

```csharp
public interface IDownloadQueueManager
{
    void EnqueueFile(QueuedFile file);
    QueuedFile? DequeueNext();
    void PauseQueue();
    void ResumeQueue();
    QueueStatus GetStatus();
    event EventHandler<QueuedFile> FileProcessed;
}

public class QueuedFile
{
    public string FilePath { get; set; }
    public string DetectedGroup { get; set; }
    public int Priority { get; set; } // High priority = user-selected
    public DateTime QueuedAt { get; set; }
    public QueueState State { get; set; } // Pending, Processing, Completed, Failed
}
```

**الفوايد:**

- ✅ Handles 1000+ files efficiently
- ✅ Priority system
- ✅ Retry failed files
- ✅ Better resource management

---

#### **Feature #4: Enhanced UI with Live Preview**

**الفكرة:**
Real-time monitoring dashboard:

```
┌─────────────────────────────────────────────┐
│ 🎯 Active Session: CS50 Study Group        │
│ ⏱️  Duration: 2m 15s                        │
│ 📥 Files Downloaded: 47/50                  │
├─────────────────────────────────────────────┤
│ Current File:                               │
│ ▓▓▓▓▓▓▓▓░░░░░░ 65% lecture-12.pdf (125 MB) │
├─────────────────────────────────────────────┤
│ Queue (3 remaining):                        │
│ • homework-5.pdf                            │
│ • solution-4.zip                            │
│ • notes.docx                                │
└─────────────────────────────────────────────┘
```

---

#### **Feature #5: Cloud Sync & Backup**

**الفكرة:**

```csharp
public interface ICloudSyncService
{
    Task SyncSettings();
    Task SyncRules();
    Task SyncStatistics();
    Task BackupState();
    Task RestoreFromBackup(string backupId);
}
```

**الفوايد:**

- ✅ Settings across devices
- ✅ Rules backup
- ✅ Statistics sync
- ✅ Disaster recovery

---

## 📋 v2.0.0 Feature List

### 🔴 **Critical (Must Have)**

| #   | Feature                                    | Priority | Effort |
| --- | ------------------------------------------ | -------- | ------ |
| 1   | SQLite Database Integration                | CRITICAL | Medium |
| 2   | Batch Download Session Manager             | CRITICAL | High   |
| 3   | Download Burst Detection                   | CRITICAL | Medium |
| 4   | Background Window Monitoring (EnumWindows) | CRITICAL | High   |
| 5   | Smart Context Cache                        | HIGH     | Low    |
| 6   | Multi-Source Context Detection             | HIGH     | Medium |

### 🟡 **Important (Should Have)**

| #   | Feature                              | Priority | Effort |
| --- | ------------------------------------ | -------- | ------ |
| 7   | File Pattern Analyzer (SQLite-based) | HIGH     | Medium |
| 8   | Smart Learning Engine (Lightweight)  | MEDIUM   | High   |
| 9   | Download Queue Manager               | MEDIUM   | Medium |
| 10  | Enhanced Error Recovery              | MEDIUM   | Low    |
| 11  | Multi-Window Support                 | MEDIUM   | Medium |

### 🟢 **Nice to Have**

| #   | Feature                          | Priority | Effort |
| --- | -------------------------------- | -------- | ------ |
| 11  | Cloud Sync Service               | LOW      | High   |
| 12  | Live Progress Dashboard          | LOW      | Medium |
| 13  | MIME Type Detection              | LOW      | Low    |
| 14  | Duplicate File Detection         | LOW      | Medium |
| 15  | Archive Extraction Auto-organize | LOW      | High   |

---

## 🏗️ Architecture Changes

### **New Services (v2.0.0)**

```
TelegramOrganizer.Core/
├── Contracts/
│   ├── IDatabaseService.cs             [NEW] ⭐
│   ├── IDownloadSessionManager.cs      [NEW]
│   ├── IProcessMonitor.cs              [NEW]
│   ├── IContextCacheService.cs         [NEW]
│   ├── IFilePatternAnalyzer.cs         [NEW]
│   ├── IDownloadBurstDetector.cs       [NEW] ⭐
│   ├── IMultiSourceDetector.cs         [NEW] ⭐
│   └── IDownloadQueueManager.cs        [NEW]
├── Models/
│   ├── DownloadSession.cs              [NEW]
│   ├── TelegramWindow.cs               [NEW]
│   ├── QueuedFile.cs                   [NEW]
│   ├── FilePattern.cs                  [NEW]
│   ├── CachedContext.cs                [NEW]
│   └── ContextSignal.cs                [NEW]

TelegramOrganizer.Infra/
├── Services/
│   ├── SQLiteDatabaseService.cs        [NEW] ⭐
│   ├── DownloadSessionManager.cs       [NEW]
│   ├── WindowsProcessMonitor.cs        [NEW] ⭐
│   ├── DownloadBurstDetector.cs        [NEW] ⭐
│   ├── MultiSourceContextDetector.cs   [NEW] ⭐
│   ├── SmartContextCache.cs            [NEW] ⭐
│   ├── FilePatternAnalyzer.cs          [NEW]
│   └── DownloadQueueManager.cs         [NEW]
├── Data/
│   ├── Migrations/
│   │   ├── Migration_V2_Initial.cs     [NEW]
│   │   └── Migration_V2_Indexes.cs     [NEW]
│   └── Schema.sql                      [NEW]
```

---

## 🎯 Implementation Roadmap

### **Phase 1: SQLite Foundation (1 week)**

**Goal:** Database infrastructure

- [ ] Day 1-2: SQLite integration & schema design
- [ ] Day 3: Implement `IDatabaseService`
- [ ] Day 4: Migration tool (JSON → SQLite)
- [ ] Day 5: Testing & validation

**Success Metrics:**

- ✅ SQLite integrated successfully
- ✅ All existing data migrated
- ✅ Performance better than JSON

---

### **Phase 2: Smart Detection (2-3 weeks)**

**Goal:** Fix critical context detection issues

- [ ] Week 2: Implement `DownloadBurstDetector`
  - Detect burst downloads (files within 5 seconds)
  - Apply same context to burst
  - SQLite session tracking

- [ ] Week 3: Implement `BackgroundWindowMonitor`
  - EnumWindows for all Telegram windows
  - Track recently active windows
  - Multi-window support

- [ ] Week 3-4: Implement `MultiSourceContextDetector`
  - Active window signal
  - Recent windows signal
  - File timing patterns signal
  - Confidence scoring

**Success Metrics:**

- ✅ 90-95% accuracy with batch downloads (50+ files)
- ✅ Works when Telegram is minimized
- ✅ No focus dependency

---

### **Phase 3: Learning & Intelligence (2 weeks)**

**Goal:** Pattern learning from SQLite data

- [ ] Week 5: Implement `FilePatternAnalyzer`
  - Learn from file extensions
  - Time-based patterns
  - SQLite pattern storage

- [ ] Week 6: Implement `SmartContextCache`
  - Window title caching
  - Accuracy tracking
  - Auto-correction from user feedback

**Success Metrics:**

- ✅ 85%+ accuracy with pattern prediction
- ✅ Cache hit rate >70%
- ✅ Learning improves over time

---

### **Phase 4: Queue & Performance (1 week)**

**Goal:** Optimize for large batches

- [ ] Week 7: Implement `DownloadQueueManager`
  - Priority-based queue
  - Batch processing
  - Progress tracking

- [ ] Week 7: Performance optimization
  - SQLite query optimization
  - Index tuning
  - Memory profiling

**Success Metrics:**

- ✅ Handles 1000+ files efficiently
- ✅ CPU usage <5%
- ✅ Memory usage <200MB

---

### **Phase 5: Polish & Release (1 week)**

**Goal:** Production ready

- [ ] Week 8: UI enhancements
  - Live session dashboard
  - Statistics improvements
  - Settings for new features

- [ ] Week 8: Testing & documentation
  - Beta testing (50 users)
  - Documentation updates
  - Migration guide

- [ ] Week 8: Release v2.0.0 🚀

**Total Timeline: 8 weeks** (down from 16 weeks!)

---

## 📊 Breaking Changes

### **Config Migration**

```json
// v1.0.0
{
  "DestinationBasePath": "...",
  "DownloadsFolderPath": "..."
}

// v2.0.0
{
  "DestinationBasePath": "...",
  "DownloadsFolderPath": "...",
  "SessionTimeout": 30,              // NEW
  "EnableSmartLearning": true,       // NEW
  "EnableTelegramIntegration": true, // NEW
  "QueueMaxSize": 1000,             // NEW
  "BackgroundMonitoring": true       // NEW
}
```

### **Database Schema**

```sql
-- v2.0.0 new tables
CREATE TABLE download_sessions (
    id INTEGER PRIMARY KEY,
    group_name TEXT,
    start_time DATETIME,
    end_time DATETIME,
    file_count INTEGER
);

CREATE TABLE file_patterns (
    id INTEGER PRIMARY KEY,
    pattern TEXT,
    group_name TEXT,
    confidence REAL,
    times_seen INTEGER
);
```

---

## 🧪 Testing Strategy

### **Unit Tests**

- Target: 80% code coverage
- Critical paths: 100% coverage
- Add 150+ new tests

### **Integration Tests**

- Batch download scenarios (10, 50, 100, 500 files)
- Multi-window scenarios
- Network interruption
- Telegram restart scenarios

### **Performance Tests**

- Memory leak detection
- CPU profiling
- Large file handling (5GB+)
- 1000+ files in queue

### **User Acceptance Tests**

- Beta program (50 users)
- Real-world usage (1 month)
- Feedback collection
- Bug fixes

---

## 🎯 Success Metrics (v2.0.0)

| Metric                    | v1.0.0      | v2.0.0 Target |
| ------------------------- | ----------- | ------------- |
| Batch Download Accuracy   | 40-60%      | 90-95%        |
| Single File Accuracy      | 85%         | 95%+          |
| Focus Dependency          | Required ❌ | Optional ✅   |
| Max Batch Size            | ~50 files   | 1000+ files   |
| Memory Usage              | <100MB      | <200MB        |
| CPU Usage (idle)          | <2%         | <3%           |
| Database Size             | N/A         | ~5-10MB/year  |
| Pattern Learning Accuracy | N/A         | 85%+          |
| User Satisfaction         | 7/10        | 9/10          |

---

## 💰 Development Cost Estimate

| Phase                    | Duration    | Complexity  | Resources     |
| ------------------------ | ----------- | ----------- | ------------- |
| Phase 1: SQLite          | 1 week      | Medium      | 1 Senior Dev  |
| Phase 2: Smart Detection | 3 weeks     | High        | 1 Senior Dev  |
| Phase 3: Learning        | 2 weeks     | Medium-High | 1 Senior Dev  |
| Phase 4: Performance     | 1 week      | Medium      | 1 Senior Dev  |
| Phase 5: Polish          | 1 week      | Low         | 1 Dev + QA    |
| **Total**                | **8 weeks** | -           | **~2 months** |

**Cost Savings:**

- ✅ 50% reduction in timeline (16 → 8 weeks)
- ✅ No ML engineer needed
- ✅ No complex API integration
- ✅ Simpler architecture = easier maintenance

---

## 🚨 Risks & Mitigation

### **Risk #1: SQLite Migration Issues**

- **Probability:** Low-Medium
- **Impact:** Medium
- **Mitigation:**
  - Comprehensive migration testing
  - Keep JSON backups during migration
  - Rollback mechanism
  - Gradual migration (keep both for 1 version)

### **Risk #2: Window Detection Accuracy**

- **Probability:** Medium
- **Impact:** Medium
- **Mitigation:**
  - Multiple fallback strategies
  - User feedback mechanism
  - Continuous learning from corrections
  - Allow manual override

### **Risk #3: Performance with Large Databases**

- **Probability:** Low
- **Impact:** Low
- **Mitigation:**
  - Proper indexing strategy
  - Regular VACUUM operations
  - Archive old data (>1 year)
  - Query optimization

### **Risk #4: Breaking Changes for Users**

- **Probability:** Low
- **Impact:** High
- **Mitigation:**
  - Automatic migration tool
  - Settings preserved
  - Clear upgrade guide
  - Beta testing period

---

## 📝 Next Steps

1. ✅ **Review this plan** with team
2. ✅ **Prioritize features** based on user feedback
3. ✅ **Create GitHub issues** for each task
4. ✅ **Start Phase 1** development
5. ✅ **Set up CI/CD** for v2.0 branch

---

## �️ Technology Stack & Dependencies

### **New Dependencies (v2.0.0)**

| Package                     | Version | Purpose        | Size   |
| --------------------------- | ------- | -------------- | ------ |
| `sqlite-net-pcl`            | Latest  | SQLite ORM     | ~500KB |
| `SQLitePCLRaw.bundle_green` | Latest  | SQLite runtime | ~1MB   |
| `Dapper` (optional)         | Latest  | Micro-ORM      | ~100KB |

### **Key Design Decisions**

1. **SQLite over JSON**
   - ✅ Better for relationships
   - ✅ Faster queries with indexes
   - ✅ ACID transactions
   - ❌ Slightly larger file size

2. **No External APIs**
   - ✅ Simple & maintainable
   - ✅ No user authentication needed
   - ✅ Privacy-friendly
   - ❌ 90-95% accuracy vs 99% with API

3. **Multi-Strategy Detection**
   - ✅ Robust with fallbacks
   - ✅ Works in edge cases
   - ✅ Self-improving
   - ❌ More complex code

---

## 📚 References

- [SQLite Documentation](https://www.sqlite.org/docs.html)
- [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net)
- [Win32 EnumWindows API](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows)
- [Win32 Process Monitoring](https://docs.microsoft.com/en-us/windows/win32/psapi/process-status-helper)

---

**Last Updated:** January 19, 2026  
**Status:** 📋 Planning Phase  
**Target Release:** Q1 2026 (March 2026)  
**Estimated Effort:** 8 weeks  
**Approach:** Smart Detection + SQLite (No External APIs)

---

<div align="center">
  <b>v2.0.0 - Smart & Simple 🚀</b>
  <br>
  <i>Intelligent • Independent • Fast</i>
  <br><br>
  <b>90-95% Accuracy | No API Dependencies | 8 Weeks Development</b>
</div>
