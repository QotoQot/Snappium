# Snappium

Cross-platform screenshot automation tool for mobile apps using Appium.

Snappium automates the process of taking screenshots across multiple devices, languages, and orientations for mobile applications. It supports both iOS simulators and Android emulators, with action-driven configuration for flexible navigation workflows.

## Features

- **Cross-Platform**: Support for both iOS and Android apps
- **Multi-Language**: Automated language switching and locale configuration
- **Action-Driven**: Flexible navigation using configurable actions (tap, wait, capture)
- **Sequential Execution**: Jobs executed one at a time with proper device isolation and cleanup
- **Rich Configuration**: JSON-based configuration with schema validation and centralized defaults
- **Graceful Shutdown**: Proper cleanup of all managed processes on interruption (Ctrl+C)
- **Failure Handling**: Automatic artifact collection (page source, screenshots, device logs) with process cleanup
- **Status Bar Control**: Demo mode and status bar customization
- **Artifact-Based**: Uses pre-built app artifacts (no build integration)
- **Comprehensive Logging**: Colored console output with verbose mode

## Installation

### Prerequisites

Ensure you have the following tools installed:

- **.NET 9.0 SDK** or later
- **Node.js** (for Appium)
- **Appium** with iOS and Android drivers:
  ```bash
  npm install -g appium
  appium driver install uiautomator2
  appium driver install xcuitest
  ```
- **Xcode Command Line Tools** (macOS only, for iOS):
  ```bash
  xcode-select --install
  ```
- **Android SDK** with emulator support
- **Java 11+** (for Android)

### Install Snappium

Clone the repository and install from source:

```bash
# Clone the repository
git clone https://github.com/QotoQot/Snappium.git
cd Snappium

# Pack the CLI tool
dotnet pack Snappium/Snappium.Cli/ -c Release

# Install globally from local source
dotnet tool install --global --add-source ./Snappium/Snappium.Cli/bin/Release Snappium.Cli
```

Verify installation:

```bash
snappium --help
```

## Quick Start

### 1. Create Configuration

Start with the sample configuration:

```bash
# Download sample configuration
curl -o screenshot_config.json https://raw.githubusercontent.com/QotoQot/Snappium/refs/heads/main/samples/screenshot_config.json

# Or create from scratch using the schema
curl -o schema.json https://raw.githubusercontent.com/QotoQot/Snappium/refs/heads/main/schema/screenshot_config.schema.json
```

### 2. Validate Configuration

```bash
snappium validate-config --config screenshot_config.json
```

### 3. Generate Test Matrix

See what screenshots will be generated:

```bash
snappium generate-matrix --config screenshot_config.json
```

### 4. Run Screenshot Automation

Execute a dry run to see the plan:

```bash
snappium run --config screenshot_config.json --dry-run
```

Run for real:

```bash
snappium run --config screenshot_config.json
```

## Configuration

Snappium uses a JSON configuration file that defines devices, languages, screenshots, and automation behavior.

### Basic Structure

```json
{
  "Devices": {
    "Ios": [
      {
        "Name": "iPhone 15",
        "Udid": null,
        "Folder": "iPhone_15_6.1",
        "PlatformVersion": "17.5"
      }
    ],
    "Android": [
      {
        "Name": "Pixel 7",
        "Avd": "Pixel_7_API_34",
        "Folder": "Phone_6.1",
        "PlatformVersion": "14"
      }
    ]
  },
  "Languages": ["en-US", "es-ES"],
  "LocaleMapping": {
    "en-US": {"Ios": "en_US", "Android": "en_US"},
    "es-ES": {"Ios": "es_ES", "Android": "es_ES"}
  },
  "Artifacts": {
    "Ios": {
      "ArtifactGlob": "iOS/bin/Release/net9.0-ios/**/*.app",
      "Package": "com.example.ios.app"
    },
    "Android": {
      "ArtifactGlob": "Droid/bin/Release/net9.0-android/**/*.apk",
      "Package": "com.example.app"
    }
  },
  "Screenshots": [
    {
      "Name": "01_home",
      "Orientation": "portrait",
      "Actions": [
        {
          "WaitFor": {
            "Selector": {"accessibility_id": "main-view"},
            "TimeoutMs": 5000
          }
        },
        {"Capture": {"Name": "01_home"}}
      ]
    }
  ]
}
```

### Actions

Snappium supports various actions for navigation:

#### Tap Action
```json
{
  "Tap": {"accessibility_id": "button-id"}
}
```

#### Wait Action
```json
{
  "Wait": {"Seconds": 2.5}
}
```

#### Wait For Element
```json
{
  "WaitFor": {
    "Selector": {"accessibility_id": "continue-button"},
    "TimeoutMs": 5000
  }
}
```

#### Capture Screenshot
```json
{
  "Capture": {"Name": "screen_name"}
}
```

### Selectors

Multiple selector strategies are supported:

- `accessibility_id`: Accessibility identifier
- `xpath`: XPath expression
- `class_chain`: iOS class chain (iOS only)
- `uiautomator`: UiAutomator selector (Android only)
- `id`: Element ID

## Command Line Interface

### Run Command

Execute screenshot automation:

```bash
snappium run --config screenshot_config.json [options]
```

**Options:**
- `--platforms ios,android`: Filter platforms
- `--devices iPhone15,Pixel7`: Filter devices  
- `--langs en-US,es-ES`: Filter languages
- `--screens home,settings`: Filter screenshots
- `--output ./Screenshots`: Output directory
- `--ios-app path/to/app.app`: iOS app path override
- `--android-app path/to/app.apk`: Android app path override
- `--base-port 4723`: Appium base port
- `--dry-run`: Print plan without executing
- `--verbose`: Enable verbose logging

### Validate Config Command

Validate configuration file:

```bash
snappium validate-config --config screenshot_config.json
```

### Generate Matrix Command

Display test matrix:

```bash
snappium generate-matrix --config screenshot_config.json [filters]
```

## Artifacts Configuration

Snappium requires pre-built app artifacts. Configure paths to your built apps:

```json
{
  "Artifacts": {
    "Ios": {
      "ArtifactGlob": "iOS/bin/Release/net9.0-ios/**/*.app",
      "Package": "com.example.ios.app"
    },
    "Android": {
      "ArtifactGlob": "Droid/bin/Release/net9.0-android/**/*.apk",
      "Package": "com.example.app"
    }
  }
}
```

**Important**: Build your apps before running Snappium. The tool will locate artifacts using the glob patterns and install them on devices using the specified package identifiers.

## Advanced Features

### Status Bar Customization

Configure demo status bars:

```json
{
  "StatusBar": {
    "Ios": {
      "Time": "9:41",
      "WifiBars": 3,
      "CellularBars": 4,
      "BatteryState": "charged"
    },
    "Android": {
      "DemoMode": true,
      "Clock": "1200",
      "Battery": 100,
      "Wifi": "4"
    }
  }
}
```

### Fresh App Installation

Snappium always performs fresh app installations between runs to ensure clean state. Apps are uninstalled and reinstalled for each language/device combination, eliminating configuration complexity and ensuring consistent results.

### Screenshot Validation

Validate screenshot dimensions:

```json
{
  "Validation": {
    "EnforceImageSize": true,
    "ExpectedSizes": {
      "Ios": {
        "iPhone_15_6.1": {
          "Portrait": [393, 852],
          "Landscape": [852, 393]
        }
      }
    }
  }
}
```

### Failure Artifacts

Automatic failure debugging:

```json
{
  "FailureArtifacts": {
    "SavePageSource": true,
    "SaveScreenshot": true,
    "SaveDeviceLogs": true,
    "ArtifactsDir": "failure_artifacts"
  }
}
```

### Popup Dismissors

Automatically dismiss common popups:

```json
{
  "Dismissors": {
    "Ios": [
      {"AccessibilityId": "Allow"},
      {"AccessibilityId": "OK"}
    ],
    "Android": [
      {"Id": "android:id/button1"}
    ]
  }
}
```

### Port Configuration

Configure port ranges for Appium servers and emulator management:

```json
{
  "ports": {
    "basePort": 4723,
    "portOffset": 10,
    "emulatorStartPort": 5554,
    "emulatorEndPort": 5600
  }
}
```

**Port Settings:**
- `basePort`: Starting port for Appium servers (default: 4723)
- `portOffset`: Port spacing for sequential job execution (default: 10)
- `emulatorStartPort`: Starting port for Android emulator allocation (default: 5554)
- `emulatorEndPort`: Ending port for Android emulator allocation (default: 5600)

All default values are centrally managed and can be overridden in configuration or via command-line options.

## Output

Snappium generates:

1. **Screenshots**: Organized by platform/device/language/screenshot
2. **Manifest JSON**: `run_manifest.json` with detailed results
3. **Summary Text**: `run_summary.txt` with human-readable summary
4. **Failure Artifacts**: Page source, screenshots, and logs on failures

Example output structure:
```
Screenshots/
├── ios/
│   └── iPhone_15_6.1/
│       └── en-US/
│           ├── 01_home.png
│           └── 02_settings.png
├── android/
│   └── Phone_6.1/
│       └── en-US/
│           ├── 01_home.png
│           └── 02_settings.png
├── run_manifest.json
└── run_summary.txt
```

## Examples

### Basic iOS Screenshot

```bash
# Run only iOS devices in English
snappium run --config config.json --platforms ios --langs en-US
```

### Multi-Language Android

```bash
# Run Android with multiple languages
snappium run --config config.json --platforms android --langs en-US,es-ES,de-DE
```

### Specific Screenshots

```bash
# Run only specific screenshots
snappium run --config config.json --screens home,settings,profile
```

### Verbose Output

```bash
# Run with detailed logging
snappium run --config config.json --verbose
```

### Override App Paths

```bash
# Override artifact paths
snappium run --config config.json \
  --ios-app ./iOS.app \
  --android-app ./app-release.apk
```

## Troubleshooting

### Common Issues

1. **Graceful Shutdown**
   ```bash
   # Interrupt execution safely with Ctrl+C
   # Snappium will clean up all managed processes:
   # - Stop Appium servers
   # - Shutdown emulators/simulators  
   # - Clean up temporary resources
   ```

2. **Appium Server Conflicts**
   ```bash
   # Kill existing Appium processes
   pkill -f appium
   # Use custom port
   snappium run --config config.json --base-port 4724
   ```

3. **iOS Simulator Issues**
   ```bash
   # Reset simulators
   xcrun simctl erase all
   # Boot specific simulator
   xcrun simctl boot "iPhone 15"
   ```

4. **Android Emulator Issues**
   ```bash
   # List available AVDs
   emulator -list-avds
   # Start specific emulator
   emulator @Pixel_7_API_34
   ```

5. **Missing Artifacts**
   ```bash
   # Build your apps first
   dotnet build -c Release
   # Check artifact paths match your configuration
   snappium validate-config --config config.json
   ```

### Debug Mode

Enable verbose logging for detailed output:

```bash
snappium run --config config.json --verbose
```

This shows:
- Colored console output with timestamps
- Shell command execution details
- Job-scoped logging with prefixes
- Enhanced error reporting

### Configuration Validation

Validate your configuration:

```bash
snappium validate-config --config screenshot_config.json
```

Common validation errors:
- Invalid device folder names
- Missing required fields
- Invalid selector syntax
- Locale mapping mismatches

## Documentation

For more detailed information:

- **[Architecture Guide](docs/ARCHITECTURE.md)** - Comprehensive overview of Snappium's system architecture, components, and orchestration flow
- **[Troubleshooting Guide](docs/TROUBLESHOOTING.md)** - Detailed troubleshooting steps for common issues, setup problems, and debugging techniques

## Related Projects

- [Appium](https://appium.io/) - Mobile automation framework
- [Appium .NET Client](https://github.com/appium/dotnet-client) - .NET bindings for Appium
- [System.CommandLine](https://github.com/dotnet/command-line-api) - Command line parsing for .NET
