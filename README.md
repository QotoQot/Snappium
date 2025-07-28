# Snappium

Cross-platform screenshot automation tool for mobile apps using Appium.

Snappium automates the process of taking screenshots across multiple devices, languages, and orientations for mobile applications. It supports both iOS simulators and Android emulators, with action-driven configuration for flexible navigation workflows.

## Features

- **Cross-Platform**: Support for both iOS and Android apps
- **Multi-Language**: Automated language switching and locale configuration
- **Action-Driven**: Flexible navigation using configurable actions (tap, wait, capture)
- **Parallel Execution**: Multi-device screenshot generation with port management
- **Rich Configuration**: JSON-based configuration with schema validation
- **Failure Handling**: Automatic artifact collection (page source, screenshots, logs)
- **Status Bar Control**: Demo mode and status bar customization
- **Build Integration**: Automatic app building and artifact discovery
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

Install Snappium as a global .NET tool:

```bash
dotnet tool install --global Snappium.Cli
```

Or install locally in your project:

```bash
dotnet new tool-manifest  # if you don't have one already
dotnet tool install --local Snappium.Cli
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
curl -o screenshot_config.json https://raw.githubusercontent.com/snappium/snappium/main/samples/screenshot_config.json

# Or create from scratch using the schema
curl -o schema.json https://raw.githubusercontent.com/snappium/snappium/main/schema/screenshot_config.schema.json
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
  "devices": {
    "ios": [
      {
        "name": "iPhone 15",
        "udid": null,
        "folder": "iPhone_15_6.1",
        "platform_version": "17.5"
      }
    ],
    "android": [
      {
        "name": "Pixel 7",
        "avd": "Pixel_7_API_34",
        "folder": "Phone_6.1",
        "platform_version": "14"
      }
    ]
  },
  "languages": ["en-US", "es-ES"],
  "locale_mapping": {
    "en-US": {"ios": "en_US", "android": "en_US"},
    "es-ES": {"ios": "es_ES", "android": "es_ES"}
  },
  "screenshots": [
    {
      "name": "01_home",
      "orientation": "portrait",
      "actions": [
        {
          "wait_for": {
            "ios": {"accessibility_id": "main-view"},
            "android": {"accessibility_id": "main_view"},
            "timeout_ms": 5000
          }
        },
        {"capture": {"name": "01_home"}}
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
  "tap": {
    "ios": {"accessibility_id": "button-id"},
    "android": {"accessibility_id": "button_id"}
  }
}
```

#### Wait Action
```json
{
  "wait": {"seconds": 2.5}
}
```

#### Wait For Element
```json
{
  "wait_for": {
    "ios": {"xpath": "//XCUIElementTypeButton[@name='Continue']"},
    "android": {"id": "com.example:id/continue_button"},
    "timeout_ms": 5000
  }
}
```

#### Capture Screenshot
```json
{
  "capture": {"name": "screen_name"}
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
- `--build auto|always|never`: Build mode
- `--no-build`: Skip building apps
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

## Build Integration

Snappium can automatically build your apps before taking screenshots:

```json
{
  "build_config": {
    "ios": {
      "csproj": "iOS/iOS.csproj",
      "tfm": "net9.0-ios",
      "artifact_glob": "iOS/bin/Release/**/iOS.app"
    },
    "android": {
      "csproj": "Droid/Droid.csproj", 
      "tfm": "net9.0-android",
      "artifact_glob": "Droid/bin/Release/**/*.apk",
      "package": "com.example.app"
    }
  }
}
```

## Advanced Features

### Status Bar Customization

Configure demo status bars:

```json
{
  "status_bar": {
    "ios": {
      "time": "9:41",
      "wifi_bars": 3,
      "cellular_bars": 4,
      "battery_state": "charged"
    },
    "android": {
      "demo_mode": true,
      "clock": "1200",
      "battery": 100,
      "wifi": "4"
    }
  }
}
```

### App Reset Policies

Control app data between runs:

```json
{
  "app_reset": {
    "policy": "on_language_change",
    "clear_data_on_language_change": true,
    "reinstall_vs_relaunch": "relaunch"
  }
}
```

### Screenshot Validation

Validate screenshot dimensions:

```json
{
  "validation": {
    "enforce_image_size": true,
    "expected_sizes": {
      "ios": {
        "iPhone_15_6.1": {
          "portrait": [393, 852],
          "landscape": [852, 393]
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
  "failure_artifacts": {
    "save_page_source": true,
    "save_screenshot": true,
    "save_device_logs": true,
    "artifacts_dir": "failure_artifacts"
  }
}
```

### Popup Dismissors

Automatically dismiss common popups:

```json
{
  "dismissors": {
    "ios": [
      {"accessibility_id": "Allow"},
      {"accessibility_id": "OK"}
    ],
    "android": [
      {"id": "android:id/button1"}
    ]
  }
}
```

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

### Build and Test

```bash
# Build apps and run screenshots
snappium run --config config.json --build always --verbose
```

### Use Pre-Built Apps

```bash
# Use existing app builds
snappium run --config config.json --no-build \
  --ios-app ./iOS.app \
  --android-app ./app-release.apk
```

## Troubleshooting

### Common Issues

1. **Appium Server Conflicts**
   ```bash
   # Kill existing Appium processes
   pkill -f appium
   # Use custom port
   snappium run --config config.json --base-port 4724
   ```

2. **iOS Simulator Issues**
   ```bash
   # Reset simulators
   xcrun simctl erase all
   # Boot specific simulator
   xcrun simctl boot "iPhone 15"
   ```

3. **Android Emulator Issues**
   ```bash
   # List available AVDs
   emulator -list-avds
   # Start specific emulator
   emulator @Pixel_7_API_34
   ```

4. **Build Failures**
   ```bash
   # Clean and rebuild
   dotnet clean && dotnet build
   # Run with verbose build output
   snappium run --config config.json --verbose
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
