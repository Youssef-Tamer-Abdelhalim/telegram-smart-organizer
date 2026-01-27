# 🤖 AI Development Partner - Context & Guidelines

## Project Overview

**Project Name:** Telegram Smart Organizer  
**Current Version:** Phase 2 Week 3 (Stable)  
**Language:** C# (.NET 8.0)  
**Architecture:** Clean Architecture (Core/Infrastructure/UI)  
**Platform:** Windows Desktop (WPF)

**What it does:** Automatically organizes files downloaded from Telegram into folders based on the active chat/channel context.

---

## Current Stable State ✅

- **Status:** Phase 2 Week 3 - STABLE VERSION
- **Tests:** 123/129 passing (6 skipped SQLite tests with isolation issues)
- **Core Features Working:**
  - Context detection (foreground window)
  - File organization with custom rules
  - Statistics tracking
  - SQLite database (optional)
  - Burst detection (optional)
  - Session management (optional)
  - Background window monitoring (optional)

---

## Critical Context for AI Assistant

### What Happened Before (Learn from mistakes)

1. **Week 4 Attempt Failed:** Tried to implement complex multi-source context detector with 6 signals → caused instability
2. **Dashboard v2.0 Failed:** Added UI before backend was stable → had to revert
3. **Test Issues:** Shared database in tests caused isolation problems
4. **Lesson:** One feature at a time, fully tested, then move on

### Current Architecture (Don't break this!)

```
SmartOrganizerEngine (main orchestrator)
├── Required Services (v1.0 - STABLE):
│   ├── IFileWatcher
│   ├── IContextDetector (Win32ContextDetector)
│   ├── IFileOrganizer
│   ├── IPersistenceService (JSON - fallback)
│   ├── ISettingsService
│   └── ILoggingService
│
└── Optional Services (v2.0 - WORKING but OPTIONAL):
    ├── IDatabaseService (SQLiteDatabaseService)
    ├── IDownloadSessionManager
    ├── IDownloadBurstDetector
    └── IBackgroundWindowMonitor
```

**IMPORTANT:** Don't make v2.0 services required until all tests pass!

---

## Development Rules (STRICT - Follow or Break Things!)

### Rule #1: Test-Driven Development

- ❌ DON'T write code without tests
- ✅ DO write test first, then implement
- ✅ DO ensure all tests pass before committing
- ❌ DON'T skip failing tests (fix or remove properly)

### Rule #2: One Thing at a Time

- ❌ DON'T implement multiple features simultaneously
- ✅ DO complete one feature 100% before starting next
- ✅ DO stages: Interface → Implementation → Tests → Integration → Beta
- ❌ DON'T rush to "just make it work"

### Rule #3: No Breaking Changes

- ❌ DON'T remove existing functionality
- ✅ DO make changes backward compatible
- ✅ DO keep v1.0 as fallback for v2.0 features
- ❌ DON'T force users to migrate immediately

### Rule #4: Keep It Simple

- ❌ DON'T over-engineer solutions
- ✅ DO choose simple, maintainable approaches
- ✅ DO ask "is this really needed?" before adding complexity
- ❌ DON'T add features "because it would be cool"

### Rule #5: Documentation is Code

- ❌ DON'T write code without comments for complex logic
- ✅ DO update PLAN_V2.md when changing direction
- ✅ DO keep PROJECT_REFERENCE.md in sync
- ❌ DON'T assume code is self-documenting

### Rule #6: Git Discipline

- ❌ DON'T commit broken code
- ✅ DO write clear commit messages
- ✅ DO tag stable versions (e.g., `v2.0-week3-stable`)
- ❌ DON'T force push to main branch

---

## What You Should Focus On

### High-Priority Tasks ✅

1. **Fix 6 skipped tests** - Isolation issues in SQLiteDatabaseServiceTests
2. **Performance benchmarks** - Establish baseline metrics (memory/CPU)
3. **Beta testing preparation** - Make app ready for real users
4. **Documentation polish** - Ensure all docs are accurate

### Medium-Priority Improvements ⚠️

1. **Dynamic timeout** for large files (currently fixed 120s)
2. **Better error handling** in critical paths
3. **Reduce logging verbosity** in production mode
4. **Multi-monitor testing**

### Low-Priority / Future Features ⏳

1. Network drive support
2. MIME type detection (currently extension-based only)
3. Cloud backup/sync
4. Advanced ML-based pattern learning

---

## What You Should AVOID

### ❌ Anti-Patterns to Avoid

1. **Feature Creep**
   - "Let's also add X while we're at it" → NO!
   - Stay focused on the current task

2. **Over-Engineering**
   - Complex abstraction layers → Keep it simple
   - Perfect is the enemy of good → Ship working code

3. **Premature Optimization**
   - Don't optimize before measuring
   - Profile first, then optimize hot paths

4. **Test Shortcuts**
   - "Tests will come later" → They won't
   - "Just skip this flaky test" → Fix it properly

5. **Breaking Existing Functionality**
   - "We need to redesign everything" → Incremental changes
   - "V1.0 is old, remove it" → Keep as fallback

6. **Documentation Debt**
   - "I'll document this tomorrow" → Do it now
   - "The code is self-explanatory" → It's not

---

## How to Work with This Codebase

### When Adding a New Feature

```
1. Read PLAN_V2.md → Understand current phase
2. Check if it fits current goals → If not, discuss first
3. Create interface in Core layer → Define contract
4. Write unit tests → Test the interface
5. Implement in Infrastructure → Actual code
6. Test implementation → Ensure tests pass
7. Integrate into Engine → Make it optional first
8. Integration tests → Test end-to-end
9. Update documentation → PLAN_V2.md, PROJECT_REFERENCE.md
10. Commit with clear message → e.g., "feat: Add X for Y"
11. Beta test (if major feature) → Get real feedback
12. Make it non-optional (if proven stable) → Full integration
```

### When Fixing a Bug

```
1. Reproduce the bug → Write a failing test
2. Identify root cause → Don't guess, debug
3. Fix minimal code → Smallest change that works
4. Ensure test passes → Green tests
5. Check for regressions → Run all tests
6. Commit → "fix: Resolve X issue in Y"
```

### When Refactoring

```
1. Ask: Is this necessary NOW? → If no, skip
2. Ensure tests exist → You need safety net
3. Small incremental changes → Not big rewrites
4. Keep tests green → Refactor under test coverage
5. Document WHY → Not just WHAT changed
```

---

## Communication Style

### When Responding to User

✅ **DO:**

- Be direct and concise
- Explain WHY, not just WHAT
- Propose options when uncertain
- Admit if you don't know something
- Reference specific files/lines

❌ **DON'T:**

- Make assumptions without asking
- Over-explain simple things
- Propose massive rewrites without reason
- Ignore test failures
- Skip documentation updates

### When You're Uncertain

Instead of hallucinating or guessing:

```
"I need to check [specific thing] before proceeding. Let me:
1. Read [file/section]
2. Review [test/implementation]
3. Then provide accurate answer"
```

---

## Quick Reference

### Key Files to Know

```
PLAN_V2.md                    → Development roadmap, current status
PROJECT_REFERENCE.md          → Technical architecture, code structure
README.md                     → User-facing documentation

TelegramOrganizer.Core/       → Interfaces, models (business logic)
TelegramOrganizer.Infra/      → Implementations (file system, database, etc.)
TelegramOrganizer.UI/         → WPF UI, ViewModels
TelegramOrganizer.Tests/      → Unit and integration tests

App.xaml.cs                   → Service registration (DI container)
SmartOrganizerEngine.cs       → Main orchestration logic
```

### Common Commands

```bash
# Build
dotnet build -c Release

# Test
dotnet test --verbosity minimal

# Run
dotnet run --project TelegramOrganizer.UI

# Git
git log --oneline -10          # Recent commits
git status                     # Check changes
git commit -m "type: message"  # Commit (feat/fix/docs/test/refactor)
```

### Testing Checklist

Before any commit:

- [ ] All tests pass (dotnet test)
- [ ] No compiler warnings
- [ ] Code is commented (complex parts)
- [ ] Documentation updated (if needed)
- [ ] Git commit message is clear

---

## Your Mission

**Primary Goal:** Help stabilize Phase 2 Week 3 and prepare for next phase

**Success Criteria:**

1. All 129 tests passing (fix 6 skipped tests)
2. Performance benchmarks established
3. Documentation is accurate and complete
4. Code is maintainable and well-tested
5. User can upgrade smoothly

**Remember:**

- Quality > Speed
- Simple > Complex
- Working > Perfect
- Tested > Assumed

---

## Final Warning ⚠️

This project was reverted from Week 4/5 back to Week 3 due to:

- Rushed implementation
- Skipped tests
- Complex features without proper foundation
- Documentation lag

**Don't repeat these mistakes!**

Ask yourself before every change:

1. Is this really needed NOW?
2. Do I have tests for this?
3. Will this break existing functionality?
4. Can this be simpler?
5. Did I update documentation?

If any answer is concerning → STOP and discuss with user first.

---

<div align="center">
  <b>Welcome to the Team! 🚀</b>
  <br>
  <i>Let's build something stable and great</i>
  <br><br>
  <b>Focus • Test • Ship • Repeat</b>
</div>
