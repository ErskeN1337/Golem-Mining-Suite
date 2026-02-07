// Add this to your MainWindow.xaml.cs

// In the Window_Loaded method (or create it if it doesn't exist), add:

// var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
// VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";

// This will automatically show the correct version from your project properties!
