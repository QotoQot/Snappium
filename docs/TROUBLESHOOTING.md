# Snappium Troubleshooting Guide

This comprehensive troubleshooting guide helps you diagnose and resolve common issues when setting up and running Snappium for mobile app screenshot automation. This guide assumes you're new to Appium and mobile automation.

## Table of Contents

- [Quick Diagnosis](#quick-diagnosis)
- [Prerequisites & Setup Issues](#prerequisites--setup-issues)
- [Configuration Issues](#configuration-issues)
- [Build & Deployment Issues](#build--deployment-issues)
- [Device Management Issues](#device-management-issues)
- [Appium & Driver Issues](#appium--driver-issues)
- [Screenshot Execution Issues](#screenshot-execution-issues)
- [Network & Port Issues](#network--port-issues)
- [Performance & Timeout Issues](#performance--timeout-issues)
- [Process Management Issues](#process-management-issues)
- [Platform-Specific Issues](#platform-specific-issues)
- [Advanced Debugging](#advanced-debugging)

## Quick Diagnosis

### First Steps for Any Issue

1. **Run with verbose logging**:
   ```bash
   snappium run --config config.json --verbose
   ```

2. **Validate your configuration**:
   ```bash
   snappium validate-config --config config.json
   ```

3. **Test with dry run**:
   ```bash
   snappium run --config config.json --dry-run
   ```

4. **Check tool versions**:
   ```bash
   dotnet --version          # Should be 9.0+
   appium --version          # Should be 2.0+
   adb --version             # Android SDK tools
   xcrun simctl help         # iOS tools (macOS only)
   ```

### Common Error Patterns

| Error Pattern | Likely Cause | Quick Fix |
|---------------|--------------|-----------|
| "Base port must be between 1024 and 65535" | Port configuration issue | Check `Ports.BasePort` in config |
| "Could not find iOS.app artifact" | Build issue | Run `dotnet build` manually first |
| "Appium server not responding" | Appium not running | Install/restart Appium server |
| "Device not found" | Device/emulator issue | Check device names and availability |
| "Element not found" | Selector issue | Verify element selectors exist |
| "Configuration validation failed" | JSON syntax/schema issue | Check JSON formatting and required fields |

## Prerequisites & Setup Issues

### .NET Installation Issues

**Problem**: `dotnet: command not found`
```bash
$ snappium --version
bash: snappium: command not found
```

**Solutions**:
1. **Install .NET 9.0 SDK**:
   ```bash
   # macOS
   brew install dotnet
   
   # Or download from: https://dotnet.microsoft.com/download
   ```

2. **Verify installation**:
   ```bash
   dotnet --version
   # Should output: 9.0.x
   ```

3. **Check PATH**:
   ```bash
   echo $PATH | grep dotnet
   # Should include dotnet installation path
   ```

**Problem**: Wrong .NET version
```
error NETSDK1045: The current .NET SDK does not support targeting .NET 9.0
```

**Solution**: Update to .NET 9.0 SDK:
```bash
# Check current version
dotnet --list-sdks

# If older than 9.0, download latest from:
# https://dotnet.microsoft.com/download/dotnet/9.0
```

### Node.js & npm Issues

**Problem**: `npm: command not found`

**Solutions**:
1. **Install Node.js**:
   ```bash
   # macOS
   brew install node
   
   # Or download from: https://nodejs.org/
   ```

2. **Verify installation**:
   ```bash
   node --version    # Should be 18.0+
   npm --version     # Should be 9.0+
   ```

### Appium Installation Issues

**Problem**: `appium: command not found`

**Solutions**:
1. **Install Appium globally**:
   ```bash
   npm install -g appium
   ```

2. **Install required drivers**:
   ```bash
   appium driver install uiautomator2    # Android
   appium driver install xcuitest        # iOS (macOS only)
   ```

3. **Verify installation**:
   ```bash
   appium --version                      # Should be 2.0+
   appium driver list                    # Should show installed drivers
   ```

**Problem**: Permission errors during npm install
```
EACCES: permission denied, mkdir '/usr/local/lib/node_modules/appium'
```

**Solutions**:
1. **Use npx** (recommended):
   ```bash
   npx appium --version
   ```

2. **Fix npm permissions**:
   ```bash
   sudo chown -R $(whoami) /usr/local/lib/node_modules
   npm install -g appium
   ```

3. **Use yarn instead**:
   ```bash
   yarn global add appium
   ```

### Xcode & iOS Tools Issues (macOS only)

**Problem**: `xcrun: error: invalid active developer path`

**Solutions**:
1. **Install Xcode Command Line Tools**:
   ```bash
   xcode-select --install
   ```

2. **Accept Xcode license**:
   ```bash
   sudo xcodebuild -license accept
   ```

3. **Verify tools**:
   ```bash
   xcrun simctl list devices             # List simulators
   xcrun --find simctl                   # Find simctl path
   ```

**Problem**: No iOS simulators available
```
No iOS simulators found
```

**Solutions**:
1. **Install simulators via Xcode**:
   - Open Xcode → Preferences → Components
   - Download iOS simulators you need

2. **Create simulator manually**:
   ```bash
   xcrun simctl create "iPhone 15" "iPhone 15" "iOS-17-5"
   ```

3. **List available devices**:
   ```bash
   xcrun simctl list devicetypes         # Available device types
   xcrun simctl list runtimes           # Available iOS versions
   ```

### Android SDK Issues

**Problem**: `adb: command not found`

**Solutions**:
1. **Install Android SDK**:
   - Download Android Studio: https://developer.android.com/studio
   - Or use command line tools: https://developer.android.com/studio/command-line

2. **Set environment variables**:
   ```bash
   # Add to ~/.bashrc or ~/.zshrc
   export ANDROID_HOME=/path/to/android-sdk
   export PATH=$PATH:$ANDROID_HOME/platform-tools
   export PATH=$PATH:$ANDROID_HOME/emulator
   ```

3. **Verify installation**:
   ```bash
   adb --version                         # ADB version
   emulator -list-avds                   # List emulators
   ```

**Problem**: No Android emulators available
```
No Android Virtual Devices found
```

**Solutions**:
1. **Create AVD through Android Studio**:
   - Tools → AVD Manager → Create Virtual Device

2. **Create AVD via command line**:
   ```bash
   # List available system images
   avdmanager list target
   
   # Create AVD
   avdmanager create avd -n "Pixel_7_API_34" -k "system-images;android-34;google_apis;x86_64"
   ```

## Configuration Issues

### JSON Syntax Errors

**Problem**: Configuration validation fails with JSON syntax errors
```
Configuration validation failed
System.Text.Json.JsonException: Unexpected character encountered while parsing value
```

**Solutions**:
1. **Validate JSON syntax**:
   ```bash
   # Use online JSON validator: https://jsonlint.com/
   # Or command line:
   python -m json.tool screenshot_config.json
   ```

2. **Common JSON issues**:
   - Missing commas between array/object elements
   - Trailing commas (not allowed in JSON)
   - Unescaped quotes in strings
   - Missing closing brackets/braces

3. **Use schema validation**:
   ```bash
   snappium validate-config --config screenshot_config.json
   ```

### Required Field Issues

**Problem**: Missing required configuration fields
```
JSON deserialization for type 'IosDeviceDto' was missing required properties including: 'PlatformVersion'
```

**Solutions**:
1. **Check required fields** (see [sample config](../samples/screenshot_config.json)):
   ```json
   {
     "devices": {
       "ios": [
         {
           "name": "iPhone 15",           // Required
           "folder": "iPhone_15_6.1",     // Required
           "PlatformVersion": "17.5"      // Required (note: PascalCase)
         }
       ]
     },
     "languages": ["en-US"],             // Required
     "LocaleMapping": {                  // Required (note: PascalCase)
       "en-US": {"ios": "en_US", "android": "en_US"}
     },
     "screenshots": [...]                // Required
   }
   ```

2. **Property naming conventions**:
   - Use PascalCase for most properties: `PlatformVersion`, `LocaleMapping`, `BuildConfig`
   - Some nested properties use lowercase: `"ios"`, `"android"`, `"name"`

### Device Configuration Issues

**Problem**: Device names don't match available devices
```
Device 'iPhone 15 Pro Max Ultra' not found in available simulators
```

**Solutions**:
1. **List available iOS simulators**:
   ```bash
   xcrun simctl list devices available
   ```

2. **List available Android emulators**:
   ```bash
   emulator -list-avds
   ```

3. **Use exact names from device lists**:
   ```json
   {
     "devices": {
       "ios": [
         {
           "name": "iPhone 15",              // Must match simulator name
           "folder": "iPhone_15_6.1",
           "PlatformVersion": "17.5"
         }
       ],
       "android": [
         {
           "name": "Pixel 7",
           "avd": "Pixel_7_API_34",          // Must match AVD name
           "folder": "Phone_6.1",
           "PlatformVersion": "14"
         }
       ]
     }
   }
   ```

### Build Configuration Issues

**Problem**: Build configuration pointing to wrong paths
```
Could not find project file: iOS/iOS.csproj
```

**Solutions**:
1. **Use relative paths from config file location**:
   ```json
   {
     "BuildConfig": {
       "ios": {
         "Csproj": "./iOS/iOS.csproj",        // Relative to config file
         "Tfm": "net9.0-ios",
         "ArtifactGlob": "iOS/bin/Release/**/iOS.app"
       }
     }
   }
   ```

2. **Verify project files exist**:
   ```bash
   ls -la iOS/iOS.csproj                     # Check iOS project
   ls -la Droid/Droid.csproj               # Check Android project
   ```

3. **Check target framework** matches project:
   ```xml
   <!-- In iOS.csproj -->
   <TargetFramework>net9.0-ios</TargetFramework>
   ```

## Build & Deployment Issues

### .NET Build Failures

**Problem**: Project build fails
```
Build failed: Error MSB3644: The reference assemblies for .NETCore,Version=v9.0 were not found
```

**Solutions**:
1. **Check target framework**:
   ```xml
   <!-- Correct for .NET 9 -->
   <TargetFramework>net9.0-ios</TargetFramework>      <!-- iOS -->
   <TargetFramework>net9.0-android</TargetFramework>  <!-- Android -->
   ```

2. **Restore packages**:
   ```bash
   dotnet restore
   dotnet build
   ```

3. **Clean and rebuild**:
   ```bash
   dotnet clean
   dotnet build --configuration Release
   ```

**Problem**: Missing iOS workload
```
error NETSDK1147: To build this project, the following workloads must be installed: ios
```

**Solutions**:
1. **Install iOS workload**:
   ```bash
   dotnet workload install ios
   ```

2. **Install Android workload**:
   ```bash
   dotnet workload install android
   ```

3. **Verify workloads**:
   ```bash
   dotnet workload list
   ```

### App Artifact Discovery Issues

**Problem**: Built app not found
```
Could not find *.app artifact in /path/to/build/output
```

**Solutions**:
1. **Check build output manually**:
   ```bash
   find . -name "*.app" -type d          # Find iOS apps
   find . -name "*.apk" -type f          # Find Android APKs
   ```

2. **Verify ArtifactGlob patterns**:
   ```json
   {
     "BuildConfig": {
       "ios": {
         "ArtifactGlob": "iOS/bin/Release/**/*.app"     // Adjust pattern
       },
       "android": {
         "ArtifactGlob": "Droid/bin/Release/**/*.apk"   // Adjust pattern
       }
     }
   }
   ```

3. **Build manually first**:
   ```bash
   dotnet build iOS/iOS.csproj -c Release -f net9.0-ios
   dotnet build Droid/Droid.csproj -c Release -f net9.0-android
   ```

### App Installation Issues

**Problem**: iOS app installation fails
```
Failed to install app: The application could not be verified
```

**Solutions**:
1. **Check app bundle validity**:
   ```bash
   codesign -dv --verbose=4 path/to/iOS.app
   ```

2. **Use simulator build** (not device build):
   ```bash
   dotnet build iOS/iOS.csproj -c Release -f net9.0-ios
   # Not: dotnet build iOS/iOS.csproj -c Release -f net9.0-ios --runtime ios-arm64
   ```

3. **Reset simulator**:
   ```bash
   xcrun simctl shutdown "iPhone 15"
   xcrun simctl erase "iPhone 15"
   xcrun simctl boot "iPhone 15"
   ```

**Problem**: Android app installation fails
```
adb: failed to install app.apk: Failure [INSTALL_PARSE_FAILED_NO_CERTIFICATES]
```

**Solutions**:
1. **Use debug build for emulators**:
   ```bash
   dotnet build Droid/Droid.csproj -c Debug -f net9.0-android
   ```

2. **Enable unknown sources** (if needed):
   ```bash
   adb shell settings put secure install_non_market_apps 1
   ```

3. **Clear previous installation**:
   ```bash
   adb uninstall com.your.package.name
   ```

## Device Management Issues

### iOS Simulator Issues

**Problem**: Simulator won't start
```
Unable to boot device in current state: Booted
```

**Solutions**:
1. **Reset simulator state**:
   ```bash
   xcrun simctl shutdown "iPhone 15"
   xcrun simctl boot "iPhone 15"
   ```

2. **Erase simulator content**:
   ```bash
   xcrun simctl erase "iPhone 15"
   ```

3. **Kill stuck processes**:
   ```bash
   sudo pkill -f Simulator
   xcrun simctl shutdown all
   ```

**Problem**: Language settings not applying
```
Simulator still showing wrong language after setup
```

**Solutions**:
1. **Check locale mapping**:
   ```json
   {
     "LocaleMapping": {
       "en-US": {"ios": "en_US", "android": "en_US"},    // iOS uses underscore
       "es-ES": {"ios": "es_ES", "android": "es_ES"}
     }
   }
   ```

2. **Verify language setting manually**:
   ```bash
   xcrun simctl spawn booted defaults read -g AppleLanguages
   xcrun simctl spawn booted defaults read -g AppleLocale
   ```

3. **Restart simulator after language change**:
   ```bash
   xcrun simctl shutdown "iPhone 15"
   xcrun simctl boot "iPhone 15"
   ```

### Android Emulator Issues

**Problem**: Emulator fails to start
```
ERROR: AVD 'Pixel_7_API_34' is not available
```

**Solutions**:
1. **List available AVDs**:
   ```bash
   emulator -list-avds
   ```

2. **Create missing AVD**:
   ```bash
   avdmanager create avd -n "Pixel_7_API_34" \
     -k "system-images;android-34;google_apis;x86_64"
   ```

3. **Check emulator path**:
   ```bash
   which emulator
   echo $ANDROID_HOME/emulator
   ```

**Problem**: Emulator starts but ADB can't connect
```
error: device offline
```

**Solutions**:
1. **Wait for full boot**:
   ```bash
   adb wait-for-device
   adb shell getprop sys.boot_completed    # Should return "1"
   ```

2. **Restart ADB**:
   ```bash
   adb kill-server
   adb start-server
   adb devices
   ```

3. **Check emulator networking**:
   ```bash
   adb shell ping 8.8.8.8                 # Test network connectivity
   ```

## Appium & Driver Issues

### Appium Server Issues

**Problem**: Appium server won't start
```
Error: listen EADDRINUSE: address already in use :::4723
```

**Solutions**:
1. **Kill existing server**:
   ```bash
   pkill -f appium
   # Or find and kill specific process:
   lsof -ti:4723 | xargs kill
   ```

2. **Use different port**:
   ```bash
   snappium run --config config.json --base-port 4724
   ```

3. **Start server manually**:
   ```bash
   appium server --port 4723 --allow-insecure chromedriver_autodownload
   ```

**Problem**: Appium driver not found
```
No drivers have been installed. Use the "appium driver" command to install the latest version of the official drivers
```

**Solutions**:
1. **Install missing drivers**:
   ```bash
   appium driver install uiautomator2    # Android
   appium driver install xcuitest        # iOS
   ```

2. **Verify driver installation**:
   ```bash
   appium driver list
   ```

3. **Update drivers**:
   ```bash
   appium driver update uiautomator2
   appium driver update xcuitest
   ```

### Driver Session Issues

**Problem**: Cannot create driver session
```
An unknown server-side error occurred while processing the command. Original error: Could not find a connected Android device
```

**Solutions**:
1. **Verify device availability**:
   ```bash
   # Android
   adb devices
   emulator -list-avds
   
   # iOS
   xcrun simctl list devices available
   ```

2. **Check device capabilities**:
   ```json
   {
     "platforms": {
       "android": {
         "deviceName": "Pixel 7",          // Must match device
         "avd": "Pixel_7_API_34",         // Must match AVD name
         "PlatformVersion": "14"          // Must match API level
       }
     }
   }
   ```

3. **Start device manually first**:
   ```bash
   # Android
   emulator @Pixel_7_API_34 -no-window
   
   # iOS
   xcrun simctl boot "iPhone 15"
   ```

**Problem**: Driver session timeout
```
Timeout waiting for driver session to start
```

**Solutions**:
1. **Increase timeout in config**:
   ```json
   {
     "Timeouts": {
       "DefaultWaitMs": 10000,            // Increase from 5000
       "PageLoadTimeoutMs": 60000         // Increase from 30000
     }
   }
   ```

2. **Check system resources**:
   ```bash
   # Monitor CPU/memory usage
   top
   # Kill unnecessary processes
   ```

3. **Use fewer parallel jobs**:
   ```bash
   # Run one device at a time
   snappium run --config config.json --devices iPhone15
   ```

## Screenshot Execution Issues

### Element Finding Issues

**Problem**: Elements not found during screenshot execution
```
Element not found: AccessibilityId 'settings-button'
```

**Solutions**:
1. **Verify element exists**:
   - Use Appium Inspector to find elements
   - Check element IDs in your app's accessibility settings

2. **Try different selector strategies**:
   ```json
   {
     "tap": {
       // Try different selectors
       "AccessibilityId": "settings-button",        // iOS/Android
       "XPath": "//android.widget.Button[@text='Settings']", // Android
       "Id": "com.app:id/settings_button"          // Android resource ID
     }
   }
   ```

3. **Add waits before element interaction**:
   ```json
   {
     "actions": [
       {
         "wait_for": {
           "timeout": 10000,
           "selector": {"AccessibilityId": "settings-button"}
         }
       },
       {
         "tap": {"AccessibilityId": "settings-button"}
       }
     ]
   }
   ```

**Problem**: Elements change between app versions
```
Element 'login-button' worked yesterday but not today
```

**Solutions**:
1. **Use more stable selectors**:
   ```json
   // Prefer accessibility IDs over XPath
   {"AccessibilityId": "loginButton"}     // Good
   {"XPath": "//button[1]"}              // Fragile
   ```

2. **Use multiple selector strategies**:
   ```json
   // Create multiple screenshot plans for different app versions
   {
     "name": "login_v1",
     "actions": [
       {"tap": {"AccessibilityId": "login-button"}}
     ]
   },
   {
     "name": "login_v2", 
     "actions": [
       {"tap": {"AccessibilityId": "loginButton"}}
     ]
   }
   ```

### Screenshot Capture Issues

**Problem**: Screenshots are blank or corrupted
```
Screenshot captured but image is empty/black
```

**Solutions**:
1. **Check device state**:
   ```bash
   # iOS - make sure simulator is visible and unlocked
   xcrun simctl status_bar "iPhone 15" override --time "9:41"
   
   # Android - wake up device
   adb shell input keyevent 26  # Power button
   adb shell input keyevent 82  # Menu/unlock
   ```

2. **Add delays before screenshot**:
   ```json
   {
     "actions": [
       {"wait": {"seconds": 2}},           // Wait for UI to settle
       {"capture": {"name": "screen"}}
     ]
   }
   ```

3. **Check screenshot permissions**:
   ```bash
   ls -la Screenshots/                    # Check output directory exists
   chmod 755 Screenshots/                 # Fix permissions if needed
   ```

### Navigation Issues

**Problem**: App navigation fails during automation
```
Could not navigate to settings screen - app crashed
```

**Solutions**:
1. **Add error handling with dismissors**:
   ```json
   {
     "Dismissors": {
       "ios": [
         {"AccessibilityId": "Allow"},
         {"AccessibilityId": "OK"},
         {"AccessibilityId": "Continue"}
       ],
       "android": [
         {"Id": "android:id/button1"},
         {"Id": "com.android.permissioncontroller:id/permission_allow_button"}
       ]
     }
   }
   ```

2. **Use more robust navigation**:
   ```json
   {
     "actions": [
       // Wait for app to fully load
       {
         "wait_for": {
           "timeout": 15000,
           "selector": {"AccessibilityId": "main-screen"}
         }
       },
       // Small delay for animations
       {"wait": {"seconds": 1}},
       // Then navigate
       {"tap": {"AccessibilityId": "settings-tab"}}
     ]
   }
   ```

3. **Reset app state between screenshots**:
   ```json
   {
     "AppReset": {
       "Policy": "always",                 // Reset app data each run
       "ReinstallVsRelaunch": "relaunch"
     }
   }
   ```

## Network & Port Issues

### Port Conflicts

**Problem**: Port already in use errors
```
Error: listen EADDRINUSE: address already in use :::4723
```

**Solutions**:
1. **Use different base port**:
   ```bash
   snappium run --config config.json --base-port 4724
   ```

2. **Configure port range in config**:
   ```json
   {
     "Ports": {
       "BasePort": 4724,
       "PortOffset": 20,
       "EmulatorStartPort": 5554,
       "EmulatorEndPort": 5600
     }
   }
   ```
   
   **Note**: All default values are centrally managed and can be overridden via configuration or command-line options.

3. **Kill processes using ports**:
   ```bash
   # Find process using port
   lsof -ti:4723
   
   # Kill process
   kill $(lsof -ti:4723)
   
   # Or kill all Appium processes
   pkill -f appium
   ```

### Network Connectivity Issues

**Problem**: Cannot connect to Appium server
```
Unable to connect to Appium server at http://localhost:4723
```

**Solutions**:
1. **Check server is running**:
   ```bash
   curl http://localhost:4723/status
   ```

2. **Check firewall/antivirus**:
   - Ensure localhost connections allowed
   - Add Appium to firewall exceptions

3. **Use explicit server URL**:
   ```bash
   snappium run --config config.json --server-url http://127.0.0.1:4723
   ```

## Performance & Timeout Issues

### Slow Execution

**Problem**: Screenshot execution is very slow
```
Each job takes 10+ minutes to complete
```

**Solutions**:
1. **Optimize device preparation**:
   ```json
   {
     "AppReset": {
       "Policy": "never"                  // Skip app reset if not needed
     }
   }
   ```

2. **Reduce wait times**:
   ```json
   {
     "Timeouts": {
       "DefaultWaitMs": 3000,             // Reduce from 5000
       "ImplicitWaitMs": 1000             // Reduce from 2000
     }
   }
   ```

3. **Use pre-built apps**:
   ```bash
   # Build once, reuse multiple times
   dotnet build iOS/iOS.csproj -c Release
   snappium run --config config.json --no-build --ios-app path/to/iOS.app
   ```

### Timeout Issues

**Problem**: Operations timeout frequently
```
Timeout waiting for element 'loading-indicator' to disappear
```

**Solutions**:
1. **Increase specific timeouts**:
   ```json
   {
     "Timeouts": {
       "DefaultWaitMs": 15000,            // Increase wait time
       "PageLoadTimeoutMs": 60000         // Increase page load timeout
     }
   }
   ```

2. **Optimize wait strategies**:
   ```json
   {
     "actions": [
       // Wait for loading to finish
       {
         "wait_for": {
           "timeout": 30000,
           "selector": {"AccessibilityId": "content-loaded"}
         }
       },
       // Then capture
       {"capture": {"name": "screen"}}
     ]
   }
   ```

3. **Check system performance**:
   ```bash
   # Monitor system resources
   top
   
   # Check available disk space
   df -h
   
   # Check memory usage
   free -h    # Linux
   vm_stat    # macOS
   ```

## Process Management Issues

### Graceful Shutdown

**Problem**: Need to stop Snappium execution safely
```
User wants to interrupt long-running screenshot job
```

**Solutions**:
1. **Use Ctrl+C for graceful shutdown**:
   ```bash
   # Press Ctrl+C during execution
   # Snappium will automatically:
   # - Stop all Appium servers
   # - Shutdown emulators and simulators  
   # - Clean up temporary resources
   # - Complete current screenshot before stopping
   ```

2. **Wait for cleanup completion**:
   ```
   Cancellation requested. Shutting down managed processes...
   Process cleanup completed.
   ```

3. **Force termination only if needed**:
   ```bash
   # If graceful shutdown hangs, force kill (not recommended)
   pkill -f snappium
   ```

### Zombie Processes

**Problem**: Processes left running after Snappium exits
```
Appium servers or emulators still running after job completion
```

**Solutions**:
1. **Check for managed process cleanup**:
   ```bash
   # Verify no Snappium-managed processes remain
   ps aux | grep appium
   ps aux | grep emulator
   ps aux | grep Simulator
   ```

2. **Manual cleanup if needed**:
   ```bash
   # Kill remaining Appium servers
   pkill -f appium
   
   # Stop Android emulators
   adb emu kill
   
   # Shutdown iOS simulators
   xcrun simctl shutdown all
   ```

3. **Verify process manager functionality**:
   ```bash
   # Run with verbose logging to see cleanup messages
   snappium run --config config.json --verbose
   # Look for "Process cleanup completed" messages
   ```

### Port Cleanup Issues

**Problem**: Ports remain allocated after process termination
```
Error: listen EADDRINUSE: address already in use :::4723
```

**Solutions**:
1. **Use centralized port management**:
   ```json
   {
     "Ports": {
       "BasePort": 4723,
       "PortOffset": 10,
       "EmulatorStartPort": 5554,
       "EmulatorEndPort": 5600
     }
   }
   ```

2. **Clear port conflicts manually**:
   ```bash
   # Find processes using default ports
   lsof -ti:4723 | xargs kill  # Appium
   lsof -ti:5554 | xargs kill  # Android emulator
   
   # Or use different port range
   snappium run --config config.json --base-port 4724
   ```

3. **Verify automatic port cleanup**:
   ```bash
   # Check that ports are freed after graceful shutdown
   netstat -tuln | grep 4723
   ```

## Platform-Specific Issues

### iOS-Specific Issues

**Problem**: iOS simulator keyboard covers UI elements
```
Cannot tap element - keyboard is covering it
```

**Solutions**:
1. **Dismiss keyboard before screenshots**:
   ```json
   {
     "actions": [
       {"tap": {"AccessibilityId": "keyboard-dismiss"}},
       {"wait": {"seconds": 1}},
       {"capture": {"name": "screen"}}
     ]
   }
   ```

2. **Use hardware keyboard**:
   ```bash
   # Enable hardware keyboard in simulator
   xcrun simctl spawn booted defaults write com.apple.iphonesimulator ConnectHardwareKeyboard 1
   ```

**Problem**: iOS status bar shows real time/battery
```
Screenshots show inconsistent status bar information
```

**Solutions**:
1. **Configure status bar override**:
   ```json
   {
     "StatusBar": {
       "ios": {
         "Time": "9:41",
         "WifiBars": 3,
         "CellularBars": 4,
         "BatteryState": "charged"
       }
     }
   }
   ```

2. **Verify override is applied**:
   ```bash
   xcrun simctl status_bar "iPhone 15" list
   ```

### Android-Specific Issues

**Problem**: Android emulator is very slow
```
Emulator takes 5+ minutes to boot
```

**Solutions**:
1. **Use hardware acceleration**:
   ```bash
   # Enable HAXM (Intel) or Hyper-V (Windows)
   # Or use ARM images on Apple Silicon Macs
   ```

2. **Allocate more resources**:
   ```bash
   emulator @Pixel_7_API_34 -memory 4096 -cores 4
   ```

3. **Use Google Play images** (faster):
   ```bash
   # When creating AVD, choose Google Play system image
   avdmanager create avd -n "Pixel_7_API_34" \
     -k "system-images;android-34;google_apis_playstore;x86_64"
   ```

**Problem**: Android permissions dialogs interrupt automation
```
Permission dialog appears during screenshot capture
```

**Solutions**:
1. **Grant permissions automatically**:
   ```json
   {
     "Capabilities": {
       "android": {
         "autoGrantPermissions": true,
         "noReset": false
       }
     }
   }
   ```

2. **Add permission dismissors**:
   ```json
   {
     "Dismissors": {
       "android": [
         {"Id": "com.android.permissioncontroller:id/permission_allow_button"},
         {"Id": "android:id/button1"}
       ]
     }
   }
   ```

## Advanced Debugging

### Enable Debug Logging

1. **Appium server debug logs**:
   ```bash
   appium server --port 4723 --log-level debug
   ```

2. **Snappium verbose output**:
   ```bash
   snappium run --config config.json --verbose
   ```

3. **Enable device logs**:
   ```json
   {
     "FailureArtifacts": {
       "SavePageSource": true,
       "SaveScreenshot": true,
       "SaveDeviceLogs": true
     }
   }
   ```

### Manual Testing Steps

When automation fails, test steps manually:

1. **Test device connectivity**:
   ```bash
   # iOS
   xcrun simctl list devices available
   xcrun simctl boot "iPhone 15"
   
   # Android
   adb devices
   emulator @Pixel_7_API_34
   ```

2. **Test app installation**:
   ```bash
   # iOS
   xcrun simctl install booted path/to/iOS.app
   
   # Android
   adb install path/to/app.apk
   ```

3. **Test Appium connection**:
   ```bash
   curl -X POST http://localhost:4723/session \
     -H "Content-Type: application/json" \
     -d '{"capabilities": {"alwaysMatch": {"platformName": "iOS"}}}'
   ```

### Collecting Diagnostic Information

When reporting issues, collect:

1. **System information**:
   ```bash
   uname -a                              # OS version
   dotnet --version                      # .NET version
   appium --version                      # Appium version
   adb --version                         # Android tools version
   xcrun simctl help | head -1           # iOS tools version
   ```

2. **Configuration files**:
   - Your `screenshot_config.json`
   - Any error logs from `--verbose` output

3. **Device information**:
   ```bash
   xcrun simctl list devices            # iOS simulators
   emulator -list-avds                  # Android emulators
   adb devices                          # Connected Android devices
   ```

4. **Generated artifacts**:
   - Screenshots from successful runs
   - Failure artifacts (page source, logs)
   - Run manifest files

### Getting Help

If you're still having issues:

1. **Check existing issues**: https://github.com/snappium/snappium/issues
2. **Create new issue** with:
   - Operating system and version
   - .NET, Appium, and tool versions
   - Complete error messages
   - Your configuration file (remove sensitive data)
   - Steps to reproduce the issue

3. **Community resources**:
   - Appium documentation: https://appium.io/docs/
   - .NET documentation: https://docs.microsoft.com/dotnet/
   - Mobile development forums

Remember: Most issues are related to environment setup, configuration syntax, or device/app state. Work through the troubleshooting steps systematically, and use verbose logging to understand what's happening at each step.