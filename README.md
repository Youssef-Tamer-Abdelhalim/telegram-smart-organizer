# 📦 Telegram Smart Organizer

> **Context-aware file organizer** that automatically organizes your Telegram downloads based on the active chat/channel.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Version](https://img.shields.io/badge/Version-2.0--Week3-blue.svg)](CHANGELOG.md)

---

## 💡 What It Does

**Problem**: Downloads from Telegram end up in a single messy folder.

**Solution**: Automatically organizes files into folders named after the chat/channel you're viewing.

```
📥 Downloading PDF while viewing "CS50 Study Group"
    ⬇
📁 Documents/Telegram Organized/CS50 Study Group/lecture.pdf
```

## [Phase 2 Week 3] - 2026-01-26

---

## ✨ Features

### Core Features (v1.0)

| Feature                  | Description                                           |
| ------------------------ | ----------------------------------------------------- |
| 🎯 **Context Detection** | Captures active Telegram window when download starts  |
| 🚀 **Auto-Organization** | Moves files to context-based folders automatically    |
| ⚙️ **Custom Rules**      | Create rules by file extension, name pattern, or size |
| 📊 **Statistics**        | Track organized files, top groups, file types         |
| 🌙 **Dark Theme**        | Modern dark mode support                              |
| 🔔 **Notifications**     | Get notified when files are organized                 |
| 🔄 **Auto-Update**       | Check for new versions automatically                  |
| 📝 **Error Reporting**   | Comprehensive error logging                           |
| 🌍 **Arabic Support**    | Full support for Arabic group names                   |

### Phase 2 Features (Week 1-3)

| Feature                   | Description                                   |
| ------------------------- | --------------------------------------------- |
| 💾 **SQLite Database**    | Persistent session tracking and statistics    |
| 📦 **Download Sessions**  | Intelligent batch download detection          |
| ⚡ **Burst Detection**    | Groups rapid downloads from same source       |
| 👁️ **Background Monitor** | Tracks Telegram windows even when not focused |

---

## 📥 Installation

### Option 1: Windows Installer (Recommended)

1. Download `TelegramSmartOrganizer_Setup_1.0.0.exe` from [Releases](../../releases)
2. Run installer and follow instructions
3. Launch from Start Menu or Desktop shortcut

### Option 2: Portable Executable

1. Download `TelegramSmartOrganizer.exe` from [Releases](../../releases)
2. Run the executable - no installation needed!

### Option 3: Build from Source

```bash
git clone https://github.com/yourusername/telegram-smart-organizer.git
cd telegram-smart-organizer/Project

# Build
dotnet build -c Release

# Run
dotnet run --project TelegramOrganizer.UI
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

**Default Rules Included:**

- Images (jpg, png, gif) → `Images/`
- Documents (pdf, docx, txt) → `Documents/`
- Videos (mp4, mkv, avi) → `Videos/`
- Audio (mp3, m4a, flac) → `Music/`
- Archives (zip, rar, 7z) → `Archives/`

---

## 📊 Statistics Dashboard

Track your organization activity and gain insights:

- **Total Files Organized**: Running count of all organized files
- **Total Size Processed**: Cumulative size of all transfers
- **Top Groups**: Your most active Telegram groups/channels
- **File Type Distribution**: Breakdown by file extension
- **Daily Activity**: Chart showing organization activity over time

Access via **View Statistics** in the main window.

---

## 🏗️ Architecture

```
TelegramOrganizer/
├── Core/           # Business logic, interfaces, models
├── Infra/          # Windows API, file system, persistence
├── UI/             # WPF views, viewmodels, themes
└── Tests/          # Unit tests (63 tests)
```

**Tech Stack:**

- **.NET 8.0** + WPF for modern Windows desktop
- **MVVM Pattern** with CommunityToolkit.Mvvm
- **Dependency Injection** for clean architecture
- **Win32 API** for context detection
- **FileSystemWatcher** for real-time monitoring

---

## 💾 Data Storage

All application data is stored locally:

```
%LOCALAPPDATA%\TelegramOrganizer\
├── settings.json      # User preferences
├── state.json         # Pending downloads tracking
├── rules.json         # Custom organization rules
├── statistics.json    # Usage statistics
├── log_*.txt          # Daily operation logs
└── ErrorLogs/         # Detailed error reports
```

**Privacy**: No data ever leaves your computer.

---

## 🛠️ Building & Publishing

### Build Release

```bash
dotnet build -c Release
```

### Create Portable Executable

```bash
dotnet publish TelegramOrganizer.UI -c Release -r win-x64 ^
  --self-contained false -p:PublishSingleFile=true -o publish
```

### Create Windows Installer

1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php)
2. Run `Installer/build-installer.bat`
3. Find installer in `Installer/Output/`

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

Please read [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md) for coding standards.

---

## 📄 License

This project is licensed under the **MIT License** - see [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM infrastructure
- [Inno Setup](https://jrsoftware.org/isinfo.php) - Windows installer creation
- Telegram Desktop - The messenger this tool supports

---

## 📚 Documentation

- [User Guide](USER_GUIDE.md) - Complete usage instructions
- [Developer Guide](DEVELOPER_GUIDE.md) - Architecture and development setup
- [Quick Reference](QUICK_REFERENCE.md) - Fast lookup for common tasks
- [Changelog](CHANGELOG.md) - Version history and updates

---

<div align="center">
  <b>Made with ❤️ for productivity enthusiasts</b>
  <br><br>
  ⭐ Star this repo if you find it helpful!
</div>
