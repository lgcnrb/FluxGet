# FluxGet

**Advanced download manager. Fast, reliable, smart.**

A powerful IDM-like download manager built with WinUI 3 and .NET 8. Features parallel chunk downloads, YouTube support, browser extension, and priority queue system.

---

## Screenshot

> Coming soon

---

## Features

### Download Engine
- **Parallel Chunk Downloads** - Split files into multiple parallel connections for faster speeds
- **Pause / Resume / Cancel** - Full control over your downloads
- **Auto Retry** - Automatic retry on failures with exponential backoff
- **Resume Support** - Continue interrupted downloads from where they left off
- **Hash Verification** - SHA256/SHA1/MD5 file integrity verification

### Speed & Control
- **Global Speed Limit** - Set overall speed cap for all downloads
- **Per-Download Speed Limit** - Individual speed limits per download
- **Priority Queue** - Order downloads by priority (0-10)
- **Concurrent Downloads** - Configure max simultaneous downloads (1-20)

### YouTube Support
- **Video Downloads** - Resolution options from 360p to 2160p
- **MP3 Conversion** - Extract audio from YouTube videos
- **yt-dlp Integration** - Powerful YouTube download engine
- **ffmpeg Support** - Audio/video conversion tool

### Browser Extension
- **Chrome Manifest v3** - Modern browser extension standards
- **One-Click Downloads** - Add downloads directly from browser
- **YouTube Detection** - Resolution picker modal on YouTube pages
- **Download History** - Save download history in browser
- **Context Menu** - Right-click support for links, videos, images, and pages

### Interface
- **Dark Theme** - Easy on the eyes dark UI
- **Modern WinUI 3 Design** - Fluent Design System
- **Drag & Drop** - Download by dragging and dropping URLs
- **Clipboard Detection** - Auto-detect URLs from clipboard
- **Notifications** - Download completion notifications

---

## Architecture

```
FluxGet/
├── Core/
│   ├── Data/           # EF Core DbContext
│   ├── Helpers/        # Utility classes
│   ├── Models/         # Data models
│   ├── Security/       # Auth, token, sanitizer
│   └── Services/       # Business logic services
├── UI/
│   ├── Converters/     # XAML value converters
│   ├── ViewModels/     # MVVM ViewModels
│   └── Views/          # WinUI 3 pages
├── BrowserExtension/   # Chrome browser extension
└── Assets/             # Application assets
```

---

## Tech Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| C# | 12 | Programming language |
| .NET | 8.0 | Runtime platform |
| Windows App SDK | 2.2 | WinUI 3 framework |
| WinUI | 3 | User interface |
| Entity Framework Core | 8.0 | Database ORM (SQLite) |
| CommunityToolkit.Mvvm | 8.2 | MVVM infrastructure |
| System.Reactive | 6.0 | Reactive programming |
| yt-dlp | - | YouTube video downloading |
| ffmpeg | - | Audio/video conversion |

---

## Requirements

- **OS**: Windows 10 (version 1809 / build 17763) or later
- **Platform**: x64, x86, or ARM64
- **.NET**: .NET 8.0 Runtime
- **Disk**: Minimum 100 MB free space
- **Internet**: Required for downloads and YouTube

---

## Installation

### Method 1: From Source

```bash
# Clone the repository
git clone https://github.com/lgcnrb/FluxGet.git
cd FluxGet

# Build
dotnet build -p:Platform=x64

# Run
dotnet run --project FluxGet -p:Platform=x64
```

### Method 2: Released Version

1. Download the latest version from [Releases](https://github.com/lgcnrb/FluxGet/releases)
2. Run the `.msix` or `.appxbundle` file
3. Follow the installation wizard

---

## Getting Started

1. Launch the application
2. Go to **Tools** page and select `yt-dlp` and `ffmpeg` files (required for YouTube)
3. Go to **Settings** page and set your default download location
4. (Optional) Install the Chrome extension from **Browser Extension** page
5. Paste a URL or drag and drop to start downloading!

---

## Browser Extension Setup

1. Copy the `FluxGet/BrowserExtension` folder
2. Open `chrome://extensions/` in Chrome
3. Enable "Developer mode"
4. Click "Load unpacked"
5. Select the FluxGet folder
6. Extension is ready!

---

## License

This project is licensed under the [MIT License](LICENSE).

```
MIT License

Copyright (c) 2026 lgcnrb

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction...
```

---

## Contributing

Contributions are welcome! Fork the repo, create a branch, and open a PR.

---

## Contact

- **Issues**: [GitHub Issues](https://github.com/lgcnrb/FluxGet/issues)
- **Developer**: lgcnrb
