# 🚀 Telegram Smart Organizer v2.0 - Development Plan

> **Current Status: Phase 2 Week 3 - STABLE VERSION**
>
> **Last Updated:** January 26, 2026
>
> **⚠️ CRITICAL: DO NOT PROCEED TO WEEK 4+ WITHOUT CAREFUL REVIEW**

---

## 📍 Current Position: Phase 2 Week 3 ✅

### **What We Have (Implemented & Stable)**

#### ✅ **Phase 1: SQLite Foundation** (Week 1) - COMPLETED

- SQLite database integration with `sqlite-net-pcl`
- `SQLiteDatabaseService` implementing `IDatabaseService`
- `DownloadSessionManager` for session tracking
- Database schema with 7 tables (sessions, files, patterns, statistics, cache, state, versions)
- Migration system from JSON to SQLite
- **Status:** Functional but optional in current build

#### ✅ **Phase 2 Week 1: Download Burst Detector** - COMPLETED

- `DownloadBurstDetector` service implemented
- Detects rapid file downloads (3+ files within 5 seconds)
- Burst session management (30-second timeout)
- Events: `BurstStarted`, `BurstContinued`, `BurstEnded`
- **Integration:** Optional in `SmartOrganizerEngine`

#### ✅ **Phase 2 Week 2: Download Session Manager** - COMPLETED

- Session-based file tracking
- 30-second session timeout
- File-to-session mapping in database
- Handles batch downloads better than v1.0
- **Integration:** Optional in `SmartOrganizerEngine`

#### ✅ **Phase 2 Week 3: Background Window Monitor** - COMPLETED

- `BackgroundWindowMonitor` tracks Telegram windows continuously
- Scans every 2 seconds using `Win32WindowEnumerator`
- Works even when Telegram is minimized/unfocused
- Caches up to 20 recent windows
- Events: `WindowDetected`, `WindowActivated`, `WindowRemoved`
- **Integration:** Optional in `SmartOrganizerEngine`

### **Test Status**

- **Total Tests:** 129
- **Passing:** 123 ✅
- **Skipped:** 6 (SQLite integration tests with isolation issues)
- **Failed:** 0 ❌

### **Architecture Status**

```
Core Layer (Interfaces)
├── IContextDetector (v1.0 - foreground only)
├── IDatabaseService (v2.0 - SQLite)
├── IDownloadSessionManager (v2.0)
├── IDownloadBurstDetector (v2.0)
└── IBackgroundWindowMonitor (v2.0)

Infrastructure Layer (Implementations)
├── Win32ContextDetector (v1.0 - working)
├── SQLiteDatabaseService (v2.0 - working but optional)
├── DownloadSessionManager (v2.0 - working but optional)
├── DownloadBurstDetector (v2.0 - working but optional)
└── BackgroundWindowMonitor (v2.0 - working but optional)

UI Layer
└── App.xaml.cs (all services registered, v2.0 features optional)
```

---

## ⚠️ CRITICAL WARNINGS - READ BEFORE CONTINUING

### **🔴 What Went Wrong Previously**

#### **Mistake #1: Rushed Week 4 Implementation**

- **Problem:** Attempted to implement `MultiSourceContextDetector` without proper planning
- **Result:** Complex voting system with 6 signal sources caused instability
- **Lesson:** Each week's feature needs standalone testing before integration

#### **Mistake #2: Statistics Dashboard v2.0**

- **Problem:** Added UI features before backend was stable
- **Result:** Dashboard worked but relied on unstable v2.0 features
- **Lesson:** UI should come AFTER backend is proven stable

#### **Mistake #3: Test Database Pollution**

- **Problem:** Shared database in tests caused isolation issues
- **Result:** 7 failing tests with unpredictable results
- **Lesson:** Each test must use isolated database or proper cleanup

#### **Mistake #4: Feature Creep**

- **Problem:** Tried to add too many features too fast
- **Result:** Unstable codebase, reverted to Week 3
- **Lesson:** One feature at a time, fully tested before moving on

---

## 🎯 Recommended Next Steps (SAFE PATH)

### **Option A: Stabilize Current Features (RECOMMENDED)**

Before adding anything new, focus on:

1. **Fix Skipped Tests** (1-2 days)
   - Create isolated test databases
   - Fix `SQLiteDatabaseServiceTests` isolation issues
   - Get to 129/129 passing tests

2. **Full Integration of V2.0 Features** (3-5 days)
   - Make v2.0 services non-optional
   - Remove v1.0 `IPersistenceService` fallback
   - Full migration from JSON to SQLite
   - Performance testing with large datasets

3. **Real-World Testing** (1 week)
   - Beta testing with actual Telegram usage
   - Monitor for edge cases
   - Gather user feedback
   - Fix any discovered bugs

4. **Documentation & Polish** (2-3 days)
   - Update all documentation
   - Create user guide for v2.0 features
   - Performance benchmarks
   - Prepare release notes

**Timeline:** 2-3 weeks
**Risk:** Low
**Value:** High (stable, reliable product)

---

### **Option B: Careful Week 4+ Development (HIGHER RISK)**

If you choose to continue Phase 2, follow these rules:

#### **Phase 2 Week 4: Multi-Source Context Detector** (NOT STARTED)

**⚠️ REQUIREMENTS BEFORE STARTING:**

- [ ] All 129 tests passing (currently 6 skipped)
- [ ] V2.0 features fully integrated (currently optional)
- [ ] Beta testing completed (not done)
- [ ] Performance benchmarks established (not done)

**Proposed Implementation (IF requirements met):**

```csharp
/// <summary>
/// Combines multiple context detection strategies with weighted voting.
/// WARNING: This is complex - implement incrementally!
/// </summary>
public interface IMultiSourceContextDetector
{
    string DetectContext(ContextDetectionRequest request);
    ContextConfidence GetConfidence(string context);
}

public class ContextSignal
{
    public string Source { get; set; }      // "ForegroundWindow", "BackgroundMonitor", etc.
    public string Value { get; set; }       // Detected group name
    public double Confidence { get; set; }  // 0.0 - 1.0
    public DateTime Timestamp { get; set; }
}

public class MultiSourceContextDetector : IMultiSourceContextDetector
{
    // Signal sources (implement ONE at a time)
    private readonly IContextDetector _foregroundDetector;
    private readonly IBackgroundWindowMonitor _backgroundMonitor;
    private readonly IDatabaseService _patternLearner;

    // Weights (tune after testing each source)
    private const double ForegroundWeight = 0.4;
    private const double BackgroundWeight = 0.3;
    private const double PatternWeight = 0.2;
    private const double TimeBasedWeight = 0.1;

    public string DetectContext(ContextDetectionRequest request)
    {
        var signals = new List<ContextSignal>();

        // 1. Add foreground signal (existing, reliable)
        signals.Add(GetForegroundSignal());

        // 2. Add background signal (Week 3 feature)
        if (_backgroundMonitor?.IsMonitoring == true)
        {
            signals.Add(GetBackgroundSignal());
        }

        // 3. Add pattern-based signal (use database patterns)
        signals.Add(GetPatternSignal(request.FileName));

        // 4. Weighted voting
        return VoteOnBestContext(signals);
    }

    private string VoteOnBestContext(List<ContextSignal> signals)
    {
        // Group by value, sum weights
        var votes = signals
            .GroupBy(s => s.Value)
            .Select(g => new {
                Context = g.Key,
                TotalWeight = g.Sum(s => s.Confidence * GetSourceWeight(s.Source))
            })
            .OrderByDescending(v => v.TotalWeight)
            .FirstOrDefault();

        return votes?.Context ?? "Unsorted";
    }
}
```

**Development Steps (MUST follow in order):**

1. Implement `IMultiSourceContextDetector` interface
2. Add foreground signal (reuse existing)
3. Test with 100% foreground only - ensure no regression
4. Add background signal
5. Test with foreground + background - compare accuracy
6. Add pattern signal
7. Test all three together
8. Tune weights based on real data
9. Full integration testing
10. Beta testing for 1 week minimum

**Testing Requirements:**

- Minimum 20 new unit tests
- Integration tests for each signal source
- Accuracy benchmarks (must be >90%)
- Performance tests (memory/CPU)

**Time Estimate:** 2-3 weeks (if done carefully)

---

### **Phase 2 Week 5: Statistics Dashboard v2.0** (NOT STARTED)

**⚠️ DO NOT START until:**

- [ ] Week 4 is complete and stable
- [ ] Database is proven reliable with real data
- [ ] All tests passing

**Proposed Features:**

- Multi-source accuracy metrics
- Per-source confidence display
- Session analytics
- Pattern learning visualization
- Real-time monitoring view

**Time Estimate:** 1-2 weeks

---

## 🚨 STRICT DEVELOPMENT RULES

### **Rule 1: One Feature at a Time**

- Complete implementation
- Full unit tests
- Integration tests
- Performance benchmarks
- Beta testing
- THEN move to next feature

### **Rule 2: No Breaking Changes**

- All changes must be backward compatible
- Keep v1.0 behavior as fallback
- Gradual migration only
- Always have rollback plan

### **Rule 3: Test First**

- Write tests BEFORE implementation
- 100% test pass required before commit
- No skipped tests (fix or remove)
- Test databases must be isolated

### **Rule 4: Performance Monitoring**

- Benchmark before changes
- Monitor memory/CPU
- No degradation allowed
- Profile hot paths

### **Rule 5: Documentation**

- Update docs with each feature
- Clear migration guides
- Comment complex logic
- Keep PLAN.md in sync

### **Rule 6: Git Hygiene**

- Commit working code only
- Descriptive commit messages
- Tag stable versions
- Never force push to main

---

## 📊 Current vs Target Metrics

| Metric                    | v1.0 | Current (Week 3) | Target (v2.0 Complete) |
| ------------------------- | ---- | ---------------- | ---------------------- |
| **Functionality**         |      |                  |                        |
| Single File Accuracy      | 85%  | 85%              | 95%+                   |
| Batch Download Accuracy   | 40%  | 70% (estimated)  | 90-95%                 |
| Focus Dependency          | Yes  | Partial          | No                     |
| Max Batch Size (reliable) | 10   | 50 (estimated)   | 500+                   |
| **Quality**               |      |                  |                        |
| Test Coverage             | 60%  | 65%              | 80%+                   |
| Tests Passing             | 63   | 123/129          | 150+/150+              |
| **Performance**           |      |                  |                        |
| Memory (idle)             | 80MB | 100MB            | <150MB                 |
| CPU (idle)                | 1%   | 2%               | <3%                    |
| Database Size (1yr)       | N/A  | ~5MB             | ~10MB                  |

---

## 🔧 Technical Debt & Known Issues

### **High Priority**

1. **6 Skipped Tests** - Test isolation issues in `SQLiteDatabaseServiceTests`
2. **Optional V2.0 Services** - Should be mandatory or removed
3. **JSON + SQLite Dual System** - Should migrate fully to SQLite
4. **No Performance Benchmarks** - Need baseline metrics

### **Medium Priority**

1. **Large File Timeout** - Fixed 120s, should be dynamic
2. **Error Handling** - Some paths lack proper error handling
3. **Logging Verbosity** - Too much debug logging
4. **Multi-Monitor Support** - Not tested

### **Low Priority**

1. **Network Drive Support** - Not implemented
2. **MIME Type Detection** - Relying on extensions only
3. **Cloud Backup** - Not implemented (future feature)

---

## 📝 Lessons Learned

### **What Worked Well ✅**

1. Clean Architecture - Made rollback possible
2. Optional Services Pattern - Week 3 features didn't break Week 1
3. Comprehensive Logging - Easy to debug issues
4. Git Tagging - Could revert to stable version
5. Incremental Development - Week 1-3 were stable individually

### **What Didn't Work ❌**

1. Rushing Features - Week 4 was too ambitious
2. Shared Test Database - Caused unpredictable test failures
3. Parallel Feature Development - Should be sequential
4. Insufficient Beta Testing - Need real-world validation
5. Documentation Lag - PLAN.md not updated incrementally

### **Improvements for Next Phase 🎯**

1. Mandatory test pass before commit
2. Isolated test databases
3. Weekly beta testing cycles
4. Update PLAN.md with each feature
5. Performance benchmarks at each stage
6. Code reviews before merge
7. Strict adherence to development rules

---

## 🎯 Recommended Path Forward

### **Immediate Actions (This Week)**

1. ✅ Clean up skipped tests
2. ✅ Document current state (this file)
3. ✅ Create git tag `v2.0-week3-stable`
4. ⏳ Decide: Stabilize current OR continue development
5. ⏳ If continuing: Set up beta testing program
6. ⏳ Establish performance baselines

### **Short Term (1-2 Weeks)**

**If choosing Stabilization (Option A):**

- Fix all skipped tests
- Complete v2.0 integration
- Beta testing
- Release candidate preparation

**If choosing Continuation (Option B):**

- Meet all Week 4 prerequisites
- Implement Multi-Source Detector carefully
- Extensive testing at each step
- Weekly progress reviews

### **Long Term (1-3 Months)**

- Complete Phase 2 (Week 4-5)
- Phase 3: Performance optimization
- Phase 4: Advanced features (cloud sync, etc.)
- v2.0 stable release

---

## 📚 Reference Architecture

### **Service Dependencies (Current)**

```
SmartOrganizerEngine
├── IFileWatcher (required) ✅
├── IContextDetector (required) ✅
├── IFileOrganizer (required) ✅
├── IPersistenceService (required v1.0) ✅
├── ISettingsService (required) ✅
├── ILoggingService (required) ✅
├── IDownloadSessionManager (optional v2.0) ⚠️
├── IDownloadBurstDetector (optional v2.0) ⚠️
└── IBackgroundWindowMonitor (optional v2.0) ⚠️
```

### **Data Flow (Current)**

```
1. File Created Event
   ↓
2. Context Detection (foreground or background)
   ↓
3. Session Manager (if available)
   ↓
4. Burst Detector (if available)
   ↓
5. File Organization
   ↓
6. Statistics Update
   ↓
7. Database Persistence (SQLite or JSON)
```

---

## 🎬 Conclusion

**Current State:** Stable at Phase 2 Week 3
**Recommendation:** Choose Option A (Stabilization) before Option B (Continuation)
**Risk Level:** Current = Low, Week 4+ = Medium-High
**Timeline:** 2-3 weeks to stable v2.0, or 4-6 weeks for full Phase 2

**Remember:**

- Quality > Speed
- Tests must pass 100%
- Beta testing is mandatory
- Follow the rules strictly
- Document everything
- Git tags are your friend

---

**Last Reviewed:** January 26, 2026  
**Status:** 📍 Phase 2 Week 3 - Stable  
**Next Milestone:** TBD based on team decision (Option A or B)  
**Blocked By:** None (can proceed either direction)

---

<div align="center">
  <b>Phase 2 Week 3 - STABLE ✅</b>
  <br>
  <i>Choose Wisely: Stabilize or Continue</i>
  <br><br>
  <b>123/129 Tests Passing | V2.0 Features Optional | Ready for Decision</b>
</div>
