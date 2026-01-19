# Build Instructions

## Quick Build (Portable)

Run from command line:
```bash
cd Project
dotnet publish TelegramOrganizer.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

The executable will be at: `publish/TelegramSmartOrganizer.exe`

## Build Installer

### Prerequisites
1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php)
2. .NET 8 SDK

### Steps
1. Run `Installer/build-installer.bat`
2. Installer will be created at `Installer/Output/`

### Manual Steps (if batch file doesn't work)

1. **Publish the app:**
   ```bash
   dotnet publish TelegramOrganizer.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
   ```

2. **Build installer:**
   - Open Inno Setup Compiler
   - Open `Installer/TelegramOrganizerSetup.iss`
   - Click Build > Compile

## Output Files

| File | Description |
|------|-------------|
| `publish/TelegramSmartOrganizer.exe` | Portable executable |
| `Installer/Output/TelegramSmartOrganizer_Setup_1.0.0.exe` | Windows Installer |

## Requirements for End Users

- Windows 10 or later
- .NET 8.0 Runtime (installer will prompt if missing)
