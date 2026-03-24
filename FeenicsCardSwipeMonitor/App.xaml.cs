using FeenicsCsvImport.ClassLibrary; // Your Namespace
using System;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace FeenicsCardSwipeMonitor
{
    public partial class App : Application
    {
        private WinForms.NotifyIcon _trayIcon;
        private SerialPort _serialPort;
        private ImportService _importService;
        private UpdateService _updateService;


        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Setup the Tray Icon
            _trayIcon = new WinForms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Shield,
                Visible = true,
                Text = "OLRA Badge Monitor",
                ContextMenu = new WinForms.ContextMenu(new[] {
                    new WinForms.MenuItem("Settings", (s, a) => new SettingsWindow().Show()),
                    new WinForms.MenuItem("-"),
                    new WinForms.MenuItem("Exit", (s, a) => ExitApp())
                })
            };
            InitializeUpdateChecker();
            // 2. Initialize the Library Service
            InitializeImportService();
            // 3. Connect Hardware
            InitializeScanner(FeenicsCardSwipeMonitor.Properties.Settings.Default.ComPort);
        }

        private void InitializeUpdateChecker()
        {
            // Grab the actual version of the running .exe (stamped by GitVersion)
            Version currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            _updateService = new UpdateService("siebe41", "FeenicsCsvImport", currentVersion);

            // Subscribe to the background event
            _updateService.UpdateAvailable += (s, updateInfo) =>
            {
                // Marshal back to the UI thread to interact with the tray icon
                Dispatcher.Invoke(() =>
                {
                    // Assuming _notifyIcon is your System.Windows.Forms.NotifyIcon
                    _trayIcon.ShowBalloonTip(
                        10000,
                        "Update Available",
                        $"Version {updateInfo.LatestVersion} of Feenics Tools is available. Click here to download.",
                        System.Windows.Forms.ToolTipIcon.Info);

                    // Optional: Wire up a one-time click event on the balloon to open the download page
                    _trayIcon.BalloonTipClicked -= NotifyIcon_BalloonTipClicked; // Prevent duplicates
                    _trayIcon.BalloonTipClicked += NotifyIcon_BalloonTipClicked;

                    // Store the URL temporarily so the click event knows where to go
                    _trayIcon.Tag = updateInfo.ReleaseUrl;
                });
            };

            // Tell it to check every 4 hours in the background
            _updateService.StartPeriodicCheck(TimeSpan.FromHours(4));

            // Check right now on startup as well
            _ = _updateService.CheckForUpdatesAsync();
        }

        private void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            if (_trayIcon.Tag is string url)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        private void InitializeImportService()
        {
            // Decrypt password from settings
            string clearPass = Decrypt(FeenicsCardSwipeMonitor.Properties.Settings.Default.EncryptedPassword);

            var config = new ImportConfiguration
            {
                ApiUrl = "https://api.us.acresecurity.cloud",
                Instance = FeenicsCardSwipeMonitor.Properties.Settings.Default.InstanceName,
                Username = FeenicsCardSwipeMonitor.Properties.Settings.Default.ApiUsername,
                Password = clearPass,
                DuplicateHandling = DuplicateHandling.Update,
                ApiCallDelayMs = 100,
                MaxRetries = 5,
                InitialRetryDelayMs = 1000,
                MaxRetryDelayMs = 30000
            };

            // Pipe logs to our Notify method instead of Console
            _importService = new ImportService(config, msg => Notify(msg));
        }

        private void InitializeScanner(string port)
        {
            try
            {
                // 1. Pull the saved numeric parameters (with safe defaults)
                    int baudRate = FeenicsCardSwipeMonitor.Properties.Settings.Default.BaudRate != 0 ? FeenicsCardSwipeMonitor.Properties.Settings.Default.BaudRate : 9600;
                int dataBits = FeenicsCardSwipeMonitor.Properties.Settings.Default.DataBits != 0 ? FeenicsCardSwipeMonitor.Properties.Settings.Default.DataBits : 8;

                // 2. Safely parse the string settings back into Enums
                Parity parity = Parity.None;
                if (!string.IsNullOrEmpty(FeenicsCardSwipeMonitor.Properties.Settings.Default.Parity))
                {
                    Enum.TryParse(FeenicsCardSwipeMonitor.Properties.Settings.Default.Parity, out parity);
                }

                StopBits stopBits = StopBits.One;
                if (!string.IsNullOrEmpty(FeenicsCardSwipeMonitor.Properties.Settings.Default.StopBits))
                {
                    Enum.TryParse(FeenicsCardSwipeMonitor.Properties.Settings.Default.StopBits, out stopBits);
                }

                // 3. Initialize the port with the dynamic user settings
                _serialPort = new SerialPort(port, baudRate, parity, dataBits, stopBits);

                _serialPort.DataReceived += async (s, e) =>
                {
                    string badge = _serialPort.ReadExisting().Trim();
                    if (string.IsNullOrEmpty(badge)) return;

                    // Immediate Clipboard Copy
                    Dispatcher.Invoke(() => Clipboard.SetText(badge));

                    // Call your library method to process the check-in
                    await _importService.PostDeskLoginByCardAsync(badge);
                };

                _serialPort.Open();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show($"Access denied to {port}. Please ensure the configuration utility is closed and no other programs are using the reader.",
                                "Port In Use",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open port {port}: {ex.Message}",
                                "Connection Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void Notify(string message)
        {
            // Only show balloon tips for relevant scan info to avoid spamming
            if (message.Contains("DESK LOGIN"))
            {
                _trayIcon.ShowBalloonTip(2000, "Check-In Success", message, WinForms.ToolTipIcon.Info);
            }
        }

        private string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            try
            {
                byte[] data = Convert.FromBase64String(cipherText);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch { return ""; }
        }

        private void ExitApp()
        {
            _trayIcon.Visible = false;
            _serialPort?.Close();
            Current.Shutdown();
        }
    }
}