# ?? Telegram Smart Organizer

> **Context-aware file organizer** that automatically organizes your Telegram downloads based on the active chat/channel.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Version](https://img.shields.io/badge/Version-1.0.0-blue.svg)](CHANGELOG.md)

---

## ?? What It Does

**Problem**: Downloads from Telegram end up in a single messy folder.

**Solution**: Automatically organizes files into folders named after the chat/channel you're viewing.

```
?? Downloading PDF while viewing "CS50 Study Group"
    ?
?? Documents/Telegram Organized/CS50 Study Group/lecture.pdf
```

---

## ? Features

| Feature | Description |
|---------|-------------|
| ?? **Context Detection** | Captures active Telegram window when download starts |
| ?? **Auto-Organization** | Moves files to context-based folders automatically |
| ?? **Custom Rules** | Create rules by file extension, name pattern, or size |
| ?? **Statistics** | Track organized files, top groups, file types |
| ?? **Dark Theme** | Modern dark mode support |
| ?? **Notifications** | Get notified when files are organized |
| ?? **Auto-Update** | Check for new versions automatically |
| ?? **Error Reporting** | Comprehensive error logging |
| ?? **Arabic Support** | Full support for Arabic group names |

---

## ?? Installation

### Option 1: Portable (Recommended)
1. Download `TelegramSmartOrganizer.exe` from [Releases](../../releases)
2. Run the executable - no installation needed!

### Option 2: Build from Source
```bash
git clone https://github.com/yourusername/telegram-smart-organizer.git
cd telegram-smart-organizer/Project

# Build
dotnet build -c Release

# Run
dotnet run --project TelegramOrganizer.UI
```

### Requirements
- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## ?? Quick Start

1. **Run** Telegram Smart Organizer
2. **Open** Telegram Desktop and navigate to any chat/channel
3. **Download** a file - it will be automatically organized!

Default destination: `Documents\Telegram Organized\[ChatName]\`

---

## ?? Configuration

Access settings via **Settings** button or system tray:

| Setting | Default | Description |
|---------|---------|-------------|
| Destination Folder | `Documents\Telegram Organized` | Where organized files go |
| Downloads Folder | `Downloads` | Folder to monitor |
| Minimize to Tray | Yes | Hide to system tray on minimize |
| Dark Theme | No | Enable dark mode |
| Notifications | Yes | Show notifications on organize |
| Run on Startup | No | Start with Windows |

---

## ?? Custom Rules

Create rules to override default organization:

| Rule Type | Example |
|-----------|---------|
| **File Extension** | `.pdf` ? `Documents` folder |
| **File Name** | Contains "invoice" ? `Invoices` folder |
| **Group Name** | "Work" group ? `Work Files` folder |
| **File Size** | > 100MB ? `Large Files` folder |

---

## ?? Statistics

Track your organization history:
- Total files organized
- Total size processed
- Top groups by file count
- File type distribution
- Daily activity chart

---

## ??? Architecture

```
TelegramOrganizer/
??? Core/           # Business logic, interfaces, models
??? Infra/          # Windows API, file system, persistence
??? UI/             # WPF views, viewmodels, themes
??? Tests/          # Unit tests
```

**Tech Stack:**
- .NET 8.0 + WPF
- MVVM with CommunityToolkit.Mvvm
- Dependency Injection
- Win32 API for context detection

---

## ?? Data Storage

```
%LOCALAPPDATA%\TelegramOrganizer\
??? settings.json      # User preferences
??? state.json         # Pending downloads
??? rules.json         # Custom rules
??? statistics.json    # Usage statistics
??? log_*.txt          # Daily logs
??? ErrorLogs/         # Error reports
```

---

## ?? Building & Publishing

### Build Release
```bash
dotnet build -c Release
```

### Create Portable Executable
```bash
dotnet publish TelegramOrganizer.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

### Create Installer
1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php)
2. Run `Installer/build-installer.bat`

---

## ?? Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

---

## ?? License

MIT License - see [LICENSE](LICENSE) file.

---

## ?? Acknowledgments

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) for MVVM infrastructure
- [Inno Setup](https://jrsoftware.org/isinfo.php) for installer creation

---

<div align="center">
  <b>Made with ?? for productivity enthusiasts</b>
  <br><br>
  ? Star this repo if you find it helpful!
</div>