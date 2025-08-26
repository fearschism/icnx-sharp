# ICNX Sharp

A powerful desktop download manager built with .NET 9.0 and Avalonia UI for fast, reliable content downloading.

## Download Features

- **URL-Based Downloads**: Paste any HTTP/HTTPS URL and start downloading instantly
- **Multi-Session Downloads**: Manage multiple concurrent download sessions
- **Real-Time Progress**: Live progress tracking with speed and ETA indicators
- **Download History**: Keep track of all your downloads with session management
- **Smart Error Handling**: Automatic retry logic and detailed error reporting
- **Resume Downloads**: Continue interrupted downloads from where they left off
- **Batch Downloads**: Queue multiple URLs for sequential downloading
- **Download Validation**: URL verification with instant feedback notifications

## Tech Stack

- **.NET 9.0** - High-performance runtime for fast downloads
- **Avalonia UI** - Cross-platform desktop interface
- **HTTP Client** - Optimized downloading with progress tracking
- **SQLite** - Download history and session persistence

## Download Engine

- **Core Engine**: `src/ICNX.Download` - High-performance download engine
- **Session Management**: Persistent download sessions with SQLite storage
- **Progress Tracking**: Real-time bandwidth and completion monitoring
- **Error Recovery**: Intelligent retry mechanisms for failed downloads

## Testing
```bash
dotnet test
```

## Project Structure

- `src/ICNX.Download` - Download engine and session management
- `src/ICNX.App` - Desktop UI for download management
- `src/ICNX.Core` - Download models and interfaces
- `src/ICNX.Persistence` - Download history database
- `tests/ICNX.Tests` - Unit and integration tests

## License

MIT License
