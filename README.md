# 📦 Telegram Smart Organizer

> **Context-aware file organizer** that automatically organizes your Telegram downloads based on the active chat/channel.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Version](https://img.shields.io/badge/Version-2.1.0-blue.svg)](RELEASE_NOTES_v2.1.md)
[![Tests](https://img.shields.io/badge/Tests-193%20Passing-brightgreen.svg)]()

---

## 💡 What It Does

**Problem**: Downloads from Telegram end up in a single messy folder.

**Solution**: Automatically organizes files into folders named after the chat/channel you're viewing.

```
📥 Downloading PDF while viewing "CS50 Study Group"
    ⬇
📁 Documents/Telegram Organized/CS50 Study Group/lecture.pdf
```

---

## ✨ Features

### Core Features

| Feature                  | Description                                           |
| ------------------------ | ----------------------------------------------------- |
| 🎯 **Context Detection** | Multi-source detection with weighted voting           |
| 🚀 **Auto-Organization** | Moves files to context-based folders automatically    |
| ⚡ **Session Boost**     | Maintains batch download consistency                  |
| ⚙️ **Custom Rules**      | Create rules by file extension, name pattern, or size |
| 📊 **Statistics**        | Track organized files, top groups, file types         |
| 🌙 **Dark Theme**        | Modern dark mode support                              |
| 🔔 **Notifications**     | Get notified when files are organized                 |
| 🌍 **Arabic Support**    | Full support for Arabic group names                   |

### v2.1 Features (NEW)

| Feature                        | Description                                    |
| ------------------------------ | ---------------------------------------------- |
| 🎯 **Multi-Source Detection**  | Combines 4 signal sources with weighted voting |
| ⚡ **Session Priority Boost**  | Smart batch download consistency               |
| 💾 **SQLite Database**         | Persistent session and pattern tracking        |
| 📦 **Download Sessions**       | Intelligent batch download detection           |
| 👁️ **Background Monitor**      | Tracks Telegram even when not focused          |

### Detection Accuracy

| Scenario | v1.0 | v2.1 |
|----------|------|------|
| Single File | 85% | 95%+ |
| Batch Download | 40% | 90-95% |

---

## 📥 Installation

### Option 1: Windows Installer (Recommended)

1. Download `TelegramSmartOrganizer_Setup_2.1.0.exe` from [Releases](../../releases)
2. Run installer and follow instructions
3. Launch from Start Menu or Desktop shortcut

### Option 2: Portable Executable

1. Download `TelegramSmartOrganizer.exe` from [Releases](../../releases)
2. Run the executable - no installation needed!

### Option 3: Build from Source

```bash
git clone https://github.com/Youssef-Tamer-Abdelhalim/telegram-smart-organizer.git
cd telegram-smart-organizer/Project

# Build
dotnet build -c Release

# Run
dotnet run --project TelegramOrganizer.UI

# Run tests
dotnet test
```

### System Requirements

- **OS**: Windows 10 (1809+) or Windows 11
- **Framework**: [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **RAM**: 100 MB minimum
- **Disk**: 50 MB free space

---

## 🚀 Quick Start

1. **Run** Telegram Smart Organizer
2. **Open** Telegram Desktop and navigate to any chat/channel
3. **Download** a file - it will be automatically organized!

**Default destination**: `Documents\Telegram Organized\[ChatName]\`

---

## 🎯 How Multi-Source Detection Works (v2.1)

The app combines multiple signals to determine the correct folder:

```
Signal Sources (Weighted Voting)
├── Foreground Window (50%) - Active Telegram chat
├── Active Session (40%)    - Current download batch context
├── Background Monitor (30%) - Recent Telegram windows
└── Pattern Learning (20%)   - Historical file patterns
```

**Session Priority Boost**: When you switch away from Telegram during a batch download (e.g., to VS Code), the session signal is boosted to maintain consistency. All files in the batch go to the same folder.

---

## ⚙️ Configuration

Access settings via **Settings** button or system tray menu:

| Setting                | Default                        | Description                     |
| ---------------------- | ------------------------------ | ------------------------------- |
| **Destination Folder** | `Documents\Telegram Organized` | Where organized files go        |
| **Downloads Folder**   | `Downloads`                    | Folder to monitor               |
| **Retention Days**     | 30 days                        | How long to keep file history   |
| **Minimize to Tray**   | Yes                            | Hide to system tray on minimize |
| **Dark Theme**         | No                             | Enable dark mode                |
| **Notifications**      | Yes                            | Show notifications on organize  |
| **Run on Startup**     | No                             | Start with Windows              |

---

## 📋 Custom Rules

Create powerful rules to override default organization behavior:

| Rule Type             | Example                         | Description                    |
| --------------------- | ------------------------------- | ------------------------------ |
| **File Extension**    | `.pdf` → `Documents`            | Route PDFs to Documents folder |
| **File Name Pattern** | Contains "invoice" → `Invoices` | Match file name patterns       |
| **Group Name**        | "Work Team" → `Work Files`      | Organize by source group       |
| **File Size**         | > 100MB → `Large Files`         | Handle large files differently |

---

## 🏗️ Architecture

```
TelegramOrganizer/
├── Core/           # Business logic, interfaces, models
├── Infra/          # Windows API, file system, SQLite
├── UI/             # WPF views, viewmodels, themes
└── Tests/          # Unit tests (193 tests)
```

**Tech Stack:**

- **.NET 8.0** + WPF for modern Windows desktop
- **MVVM Pattern** with CommunityToolkit.Mvvm
- **SQLite** for persistent data storage
- **Win32 API** for context detection
- **FileSystemWatcher** for real-time monitoring

---

## 💾 Data Storage

All application data is stored locally:

```
%LOCALAPPDATA%\TelegramOrganizer\
├── organizer.db       # SQLite database (sessions, patterns, stats)
├── settings.json      # User preferences
├── rules.json         # Custom organization rules
└── Logs/
    └── organizer_*.log  # Daily operation logs
```

**Privacy**: No data ever leaves your computer.

---

## 🛠️ Building & Publishing

### Build Release

```bash
dotnet build -c Release
```

### Create Windows Installer

```bash
cd Installer
build-installer.bat
```

### Run Tests

```bash
dotnet test
```

---

## 🤝 Contributing

Contributions are welcome! Here's how:

1. **Fork** the repository
2. **Create** feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** changes (`git commit -m 'Add amazing feature'`)
4. **Push** to branch (`git push origin feature/amazing-feature`)
5. **Open** Pull Request

---

## 📄 License

This project is licensed under the **MIT License** - see [LICENSE](LICENSE) file for details.

---

## 📚 Documentation

- [Release Notes v2.1](RELEASE_NOTES_v2.1.md) - What's new
- [User Guide](USER_GUIDE.md) - Complete usage instructions
- [Developer Guide](DEVELOPER_GUIDE.md) - Architecture and development setup
- [Project Reference](PROJECT_REFERENCE.md) - Technical reference

---

<div align="center">
  <b>Made with ❤️ for productivity enthusiasts</b>
  <br><br>
  ⭐ Star this repo if you find it helpful!
</div>
