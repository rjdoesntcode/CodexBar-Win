# CodexBar for Windows

A Windows system tray application that monitors AI usage across multiple providers. This is a Windows port of [CodexBar](https://github.com/steipete/CodexBar) for macOS.

## Features

- **Multi-Provider Support**: Monitor usage for Claude, Cursor, Codex, GitHub Copilot, and more
- **System Tray Integration**: Unobtrusive monitoring with status indicators
- **Browser Cookie Import**: Automatically reads session cookies from Chrome, Edge, Firefox, Brave, and Opera
- **CLI Tool**: Command-line interface for scripting and automation
- **Notifications**: Get notified when approaching or exceeding rate limits
- **Configurable Refresh**: Set your preferred refresh interval

## Supported Providers

| Provider | Authentication Method |
|----------|----------------------|
| Claude | CLI or Browser Cookies |
| Cursor | Browser Cookies |
| Codex | CLI |
| GitHub Copilot | GitHub CLI (gh) |

## Installation

### Prerequisites

- Windows 10/11
- .NET 8.0 Runtime

### Build from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/CodexBar-Win.git
cd CodexBar-Win

# Build
dotnet build

# Run
dotnet run --project src/CodexBar/CodexBar.csproj
```

### CLI Usage

```bash
# Show usage status for all providers
codexbar status

# Show status in JSON format
codexbar status --json

# List available providers
codexbar providers

# Show configuration
codexbar config --show
```

## Configuration

Settings are stored in `%LOCALAPPDATA%\CodexBar\settings.json`.

### Provider Setup

#### Claude
1. Install [Claude CLI](https://claude.ai/download) and run `claude login`, or
2. Sign in to [claude.ai](https://claude.ai) in your browser

#### Cursor
Sign in to [cursor.com](https://cursor.com) in your browser

#### Codex
Install [Codex CLI](https://github.com/openai/codex) and authenticate

#### GitHub Copilot
Install [GitHub CLI](https://cli.github.com/) and run `gh auth login`

## Architecture

```
CodexBar-Win/
├── src/
│   ├── CodexBar/           # WPF System Tray Application
│   ├── CodexBar.Core/      # Core Library
│   │   ├── Browser/        # Cookie reading
│   │   ├── Models/         # Data models
│   │   └── Providers/      # Provider implementations
│   └── CodexBar.CLI/       # Command-line interface
└── CodexBar.sln
```

## Privacy

CodexBar reads only from known, specific locations:
- Browser cookies (opt-in, for web-based providers)
- CLI tool outputs
- No filesystem scanning beyond configured paths

## License

MIT License - See [LICENSE](LICENSE) for details.

## Credits

- Original [CodexBar](https://github.com/steipete/CodexBar) by Peter Steinberger
- Windows port by CodexBar-Win contributors

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.
