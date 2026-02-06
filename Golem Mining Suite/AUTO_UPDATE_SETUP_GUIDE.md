# Auto-Update System Setup Guide

## Overview
This auto-update system checks GitHub for new releases on startup and notifies users when updates are available.

## Files Created
1. **UpdateChecker.cs** - Checks GitHub API for latest release
2. **UpdateAvailableWindow.xaml** - UI for update notification
3. **UpdateAvailableWindow.xaml.cs** - Logic for update window
4. **App.xaml.cs** - Modified to check updates on startup

## Setup Instructions

### Step 1: Add Files to Your Project
1. Add all 4 files to your Visual Studio project
2. Make sure they compile without errors

### Step 2: Configure Your GitHub Repository

#### A. Set Your Repository URL
In `UpdateChecker.cs`, line 14, replace:
```csharp
private const string GITHUB_API_URL = "https://api.github.com/repos/YOUR_USERNAME/YOUR_REPO/releases/latest";
```

With your actual repo:
```csharp
private const string GITHUB_API_URL = "https://api.github.com/repos/YourUsername/Golem-Mining-Suite/releases/latest";
```

Example:
```csharp
private const string GITHUB_API_URL = "https://api.github.com/repos/john-doe/golem-mining-suite/releases/latest";
```

#### B. Set Your Application Version
1. Right-click your project → **Properties**
2. Go to **Package** tab (or **Application** tab in older VS)
3. Set **Assembly version** to `1.0.0` (or your current version)
4. Format: `Major.Minor.Build` (e.g., `1.2.3`)

### Step 3: Create a GitHub Release

When you want to release an update:

1. **Build your application** (Release mode)
2. **Go to your GitHub repository**
3. Click **Releases** → **Create a new release**
4. **Tag version:** `v1.0.1` (must start with 'v')
5. **Release title:** "Version 1.0.1" (or whatever you want)
6. **Description:** Write release notes (what's new, bug fixes, etc.)
7. **Attach files:** Upload your compiled `.exe` or `.zip`
8. Click **Publish release**

### Step 4: Test the Update System

#### Test Scenario 1: No Update Available
- Current version: `1.0.0`
- Latest GitHub release: `1.0.0`
- **Expected:** App starts normally, no update window

#### Test Scenario 2: Update Available
- Current version: `1.0.0`
- Latest GitHub release: `1.0.1`
- **Expected:** Update window appears before main window

## How It Works

### On Startup:
1. App launches
2. `App.xaml.cs` calls `UpdateChecker.CheckForUpdatesAsync()`
3. UpdateChecker fetches latest release from GitHub API
4. Compares current version with latest version
5. If newer version exists:
   - Shows `UpdateAvailableWindow`
   - User can download or skip
6. MainWindow opens

### User Experience:
```
[App Starts]
    ↓
[Checking for updates...]
    ↓
[Update Found?]
    ├─ YES → [Show Update Window] → [User Downloads/Skips] → [Main Window Opens]
    └─ NO  → [Main Window Opens]
```

## Versioning System

### Version Format: `Major.Minor.Build`
- **Major:** Breaking changes (1.0.0 → 2.0.0)
- **Minor:** New features (1.0.0 → 1.1.0)
- **Build:** Bug fixes (1.0.0 → 1.0.1)

### Version Comparison:
- `1.0.0` < `1.0.1` ✓ Update available
- `1.0.0` < `1.1.0` ✓ Update available
- `1.0.0` < `2.0.0` ✓ Update available
- `1.0.1` = `1.0.1` ✗ No update
- `1.1.0` > `1.0.0` ✗ No update (user has newer version)

## GitHub Release Example

### Good Release (Will Work):
```
Tag: v1.0.1
Title: Version 1.0.1 - Bug Fixes
Description:
  ## What's New
  - Fixed calculator crash when selecting invalid station
  - Added UEX Corp logo button
  - Improved price loading performance
  
  ## Bug Fixes
  - Fixed duplicate stations in dropdown
  - Fixed price display issues
  
Files: Golem-Mining-Suite-v1.0.1.exe
```

### Bad Release (Won't Work):
```
Tag: 1.0.1          ❌ Missing 'v' prefix
Tag: v1.0           ❌ Missing build number
Tag: version-1.0.1  ❌ Wrong format
```

## Customization Options

### Change Update Check Frequency
Currently checks on every startup. To check less frequently:

In `App.xaml.cs`, add a check:
```csharp
private async Task CheckForUpdatesAsync()
{
    // Only check once per day
    var lastCheck = Properties.Settings.Default.LastUpdateCheck;
    if ((DateTime.Now - lastCheck).TotalDays < 1)
        return;
    
    var updateInfo = await UpdateChecker.CheckForUpdatesAsync();
    
    if (updateInfo != null && updateInfo.IsUpdateAvailable)
    {
        var updateWindow = new UpdateAvailableWindow(updateInfo);
        updateWindow.ShowDialog();
    }
    
    Properties.Settings.Default.LastUpdateCheck = DateTime.Now;
    Properties.Settings.Default.Save();
}
```

### Add "Don't Ask Again" Option
Add a checkbox in `UpdateAvailableWindow.xaml`:
```xml
<CheckBox Name="DontAskCheckBox" 
          Content="Don't show this again" 
          Foreground="#CCCCCC"/>
```

### Manual Update Check
Add a menu item to check manually:
```csharp
private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
{
    var updateInfo = await UpdateChecker.CheckForUpdatesAsync();
    
    if (updateInfo == null)
    {
        MessageBox.Show("Could not check for updates.", "Error");
        return;
    }
    
    if (updateInfo.IsUpdateAvailable)
    {
        var updateWindow = new UpdateAvailableWindow(updateInfo);
        updateWindow.ShowDialog();
    }
    else
    {
        MessageBox.Show("You're already using the latest version!", "Up to Date");
    }
}
```

## Troubleshooting

### "Update check failed" in Debug Output
**Causes:**
- Invalid GitHub URL
- Repository doesn't exist or is private
- No releases published
- GitHub API rate limit exceeded
- No internet connection

**Solution:**
1. Verify GitHub URL is correct
2. Make sure repository is public
3. Publish at least one release
4. Check internet connection

### Update window doesn't appear
**Causes:**
- Current version equals or exceeds latest version
- GitHub release tag format is wrong
- Update check silently failed

**Solution:**
1. Check your assembly version in project properties
2. Verify GitHub release tag starts with 'v'
3. Run in Debug mode and check Output window for errors

### "User-Agent required" error
**Already fixed** - UpdateChecker adds User-Agent header automatically

### Download button doesn't work
**Causes:**
- No files attached to GitHub release
- Invalid URL

**Solution:**
1. Attach your compiled .exe to the GitHub release
2. GitHub will automatically create a download URL

## Advanced Features

### Auto-Install Updates
To automatically download and install (requires more complex code):
1. Download the update file
2. Save to temp directory
3. Close current app
4. Run installer/updater
5. New version starts

**Not implemented** - requires additional security considerations

### Changelog in App
Store changelog in a separate file and display in app:
```csharp
var changelog = await httpClient.GetStringAsync("https://raw.githubusercontent.com/user/repo/main/CHANGELOG.md");
```

## Security Notes

- ✅ Uses HTTPS for GitHub API
- ✅ Only opens URLs, doesn't auto-download files
- ✅ User controls when to update
- ✅ Fails silently if update check fails (doesn't block app startup)
- ⚠️ Trusts GitHub's SSL certificates
- ⚠️ No code signing verification (add if needed for production)

## Summary

1. **Setup:** Configure GitHub URL and version
2. **Release:** Create GitHub releases with proper tags
3. **Automatic:** Checks on every app startup
4. **User-friendly:** Clear UI for downloading updates
5. **Safe:** Never auto-installs, just opens download page

Your users will always know when updates are available! 🚀
