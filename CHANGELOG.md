# Changelog

All notable changes to Telegram Smart Organizer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2024-01-18

### ?? Initial Release

First production-ready release of Telegram Smart Organizer.

### Added

#### Core Features
- **Context Detection**: Automatically detects active Telegram chat/channel using Win32 API
- **File Monitoring**: Real-time monitoring of Downloads folder for new files
- **Auto-Organization**: Moves files to folders named after the source chat/channel
- **Smart Filtering**: Ignores temporary files (.td, .tmp, .part, etc.)
- **Duplicate Handling**: Automatically renames files if destination exists

#### User Interface
- **Main Window**: Shows current context, logs, and quick actions
- **Settings Window**: Configure paths, notifications, startup options
- **Rules Manager**: Create and manage custom organization rules
- **Statistics Dashboard**: View organization history and metrics
- **System Tray**: Minimize to tray with context menu
- **Dark Theme**: Modern dark mode support

#### Custom Rules
- File extension rules (e.g., `.pdf` ? Documents)
- File name pattern rules (contains, starts with, etc.)
- Group name rules
- File size rules
- Priority-based rule execution

#### Statistics
- Total files organized counter
- Total size processed
- Top groups by file count
- File type distribution
- Daily activity tracking

#### System
- **Auto-Update**: Check for updates from GitHub releases
- **Error Reporting**: Comprehensive error logging to files
- **Data Persistence**: Settings, rules, and state saved to JSON
- **Arabic Support**: Full support for Arabic group/channel names

#### Developer
- Clean Architecture (Core/Infra/UI layers)
- Dependency Injection with Microsoft.Extensions.DependencyInjection
- MVVM pattern with CommunityToolkit.Mvvm
- 63 unit tests covering all services
- Inno Setup installer script

### Technical Details
- Target Framework: .NET 8.0
- UI Framework: WPF
- Single-file publish support
- Portable executable option

---

## [Unreleased]

### Planned
- Multi-language support
- Cloud backup for settings
- Telegram Bot integration
- File preview thumbnails

---

[1.0.0]: https://github.com/yourusername/telegram-smart-organizer/releases/tag/v1.0.0
