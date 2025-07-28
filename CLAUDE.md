# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Snappium is a cross-platform mobile screenshot automation tool that orchestrates iOS simulators, Android emulators, Appium servers, and .NET mobile app builds to generate screenshots across multiple devices, languages, and configurations. The project is implemented in C#/.NET 9 and packaged as a global .NET tool.

## Development Commands

### Build and Test
```bash
# Build the solution
dotnet build

# Build specific projects
dotnet build Snappium/Snappium.Core/
dotnet build Snappium/Snappium.Cli/
dotnet build Snappium/Snappium.Tests/

# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "ClassName=ScreenshotAutomationTests"

# Run tests with verbose output
dotnet test --verbosity normal

# Clean and rebuild
dotnet clean && dotnet build
```

### Package and Install Locally
```bash
# Pack the CLI tool
dotnet pack Snappium/Snappium.Cli/ -c Release

# Install locally for testing
dotnet tool install --global --add-source ./Snappium/Snappium.Cli/bin/Release Snappium.Cli

# Uninstall local version
dotnet tool uninstall --global Snappium.Cli
```

### Configuration Validation
```bash
# Validate sample configuration
snappium validate-config --config samples/screenshot_config.json

# Generate test matrix
snappium generate-matrix --config samples/screenshot_config.json

# Dry run
snappium run --config samples/screenshot_config.json --dry-run
```

## Architecture Overview

### Core Components

**Snappium.Core**: Business logic and orchestration
- `Orchestration/Orchestrator.cs`: Central coordinator that manages the entire screenshot automation workflow
- `Planning/RunPlanBuilder.cs`: Generates execution matrix from configuration (platforms × devices × languages × screenshots)
- `Planning/PortAllocator.cs`: Manages unique port allocation for parallel Appium sessions
- `Config/ConfigLoader.cs`: Loads and validates JSON configuration with schema validation
- `DeviceManagement/`: Platform-specific device managers for iOS (simctl) and Android (adb/emulator)
- `Appium/`: Appium server lifecycle, driver factory, and action execution
- `Build/BuildService.cs`: .NET project building and artifact discovery

**Snappium.Cli**: Command-line interface using System.CommandLine
- `Commands/RunCommand.cs`: Main execution command with filtering and override options
- `Commands/ValidateConfigCommand.cs`: Configuration validation
- `Commands/GenerateMatrixCommand.cs`: Test matrix display

**Snappium.Tests**: NUnit test suite with parameterized tests and Moq mocking

### Key Architectural Patterns

1. **Orchestration-Driven**: The `Orchestrator` class coordinates all operations through dependency injection
2. **Platform Abstraction**: Unified interfaces (`IIosDeviceManager`, `IAndroidDeviceManager`) hide platform differences
3. **Action-Based Configuration**: Screenshots are defined as sequences of configurable actions (tap, wait, capture)
4. **External Tool Coordination**: Uses `ICommandRunner` to execute simctl, adb, emulator, dotnet build commands
5. **Port Management**: `PortAllocator` ensures non-conflicting Appium server ports for parallel execution

### Configuration System

The system uses a hierarchical configuration approach:
1. **JSON Schema Validation**: Structural validation using `schema/screenshot_config.schema.json`
2. **DTO Deserialization**: JSON → DTOs with System.Text.Json
3. **Domain Model**: DTOs → strongly-typed domain objects
4. **CLI Overrides**: Command-line options override config file values

Critical configuration properties use PascalCase (`PlatformVersion`, `LocaleMapping`, `BuildConfig`) while nested platform-specific properties use lowercase.

### Execution Flow

1. **Configuration Loading**: Load and validate JSON configuration
2. **Run Plan Generation**: Build matrix of jobs (platform × device × language × screenshots)
3. **Port Allocation**: Assign unique Appium ports for each job
4. **Job Execution**: For each job:
   - Prepare device (shutdown, language, boot, status bar)
   - Build and install app
   - Create Appium driver session
   - Execute screenshot actions
   - Validate results
   - Cleanup device state
5. **Manifest Generation**: Create `run_manifest.json` and `run_summary.txt`

### Failure Handling

The system includes comprehensive failure artifact collection:
- **Page Source**: XML UI hierarchy at failure point
- **Driver Screenshot**: Visual state when failure occurred
- **Device Logs**: Platform-specific logs (simctl log, adb logcat)
- **Environment Info**: Versions, capabilities, session details

## Working with External Tools

Snappium orchestrates multiple external command-line tools:

### iOS Tools (macOS only)
- `xcrun simctl`: Simulator management (list, boot, install, screenshot)
- Status bar override: `simctl status_bar override`
- Language setting: `simctl spawn booted defaults`

### Android Tools
- `emulator`: Start/stop Android Virtual Devices
- `adb`: Device communication (install, screenshot, settings)
- Demo mode: `adb shell am broadcast` for status bar

### Build Tools
- `dotnet build`: Compile iOS/Android projects
- Artifact discovery: Glob pattern matching for .app/.apk files

### Appium
- Server lifecycle: Start/stop Appium servers on allocated ports
- Driver management: IOSDriver/AndroidDriver creation with capabilities
- Element finding: Multiple selector strategies (AccessibilityId, XPath, etc.)

## Configuration Best Practices

### Property Naming
- Use PascalCase for top-level configuration properties
- Platform-specific nested properties often use lowercase
- Selector properties use PascalCase (`AccessibilityId`, not `accessibility_id`)

### JSON Schema Validation
Always validate configuration changes against `schema/screenshot_config.schema.json`:
```bash
snappium validate-config --config your_config.json
```

### Action Sequences
Structure actions logically:
1. `wait_for` elements to be present
2. `tap` or other interactions
3. `wait` for animations/transitions
4. `capture` screenshots

### Device Configuration
- iOS devices: Use exact simulator names from `xcrun simctl list devices`
- Android devices: Use exact AVD names from `emulator -list-avds`
- Folder names should be filesystem-safe (no spaces, special characters)

## Testing and Debugging

### Unit Tests
The test suite uses NUnit with parameterized tests that cover all platform/device/language combinations. Tests extensively use Moq for mocking external dependencies.

### Debug Logging
Enable verbose mode for detailed execution tracing:
```bash
snappium run --config config.json --verbose
```

This provides:
- Colored console output with timestamps
- Shell command execution details
- Job-scoped logging with prefixes
- Enhanced error reporting

### Common Issues
- **Port conflicts**: Use `--base-port` to avoid conflicts with running Appium servers
- **Build failures**: Check project paths in `BuildConfig` are relative to config file location
- **Device not found**: Verify device names match exact simulator/AVD names
- **Element not found**: Use Appium Inspector to verify element selectors

## File Organization

```
Snappium/
├── Snappium.Core/          # Core business logic
│   ├── Orchestration/      # Main workflow coordination
│   ├── Planning/           # Job planning and port allocation
│   ├── Config/             # Configuration loading/validation
│   ├── DeviceManagement/   # iOS/Android device control
│   ├── Appium/            # Appium integration and actions
│   ├── Build/             # .NET project building
│   └── Logging/           # Enhanced console logging
├── Snappium.Cli/          # Command-line interface
│   └── Commands/          # CLI command implementations
├── Snappium.Tests/        # Unit test suite
├── samples/               # Sample configuration files
├── schema/               # JSON schema for validation
└── docs/                 # Architecture and troubleshooting docs
```

## Integration Points

When adding new features:

1. **New Actions**: Extend `ActionExecutor` and update JSON schema
2. **New Platforms**: Implement `IDeviceManager` interface
3. **New Build Systems**: Extend `IBuildService` interface
4. **New Validation**: Extend `IImageValidator` interface

Always ensure changes maintain the existing dependency injection pattern and include comprehensive unit tests with mocking.