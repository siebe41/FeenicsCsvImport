using System;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using WinForms = System.Windows.Forms;
using FeenicsCsvImport.ClassLibrary; // Your Namespace

namespace FeenicsCardSwipeMonitor
{
    public partial class App : Application
    {
        private WinForms.NotifyIcon _trayIcon;
        private SerialPort _serialPort;
        private ImportService _importService;

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

            // 2. Initialize the Library Service
            InitializeImportService();
            // 3. Connect Hardware
            InitializeScanner(FeenicsCardSwipeMonitor.Properties.Settings.Default.ComPort);
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
                _serialPort = new SerialPort(port, 9600, Parity.None, 8, StopBits.One);
                _serialPort.DataReceived += async (s, e) =>
                {
                    string badge = _serialPort.ReadExisting().Trim();
                    if (string.IsNullOrEmpty(badge)) return;

                    // A. Immediate Clipboard Copy
                    Dispatcher.Invoke(() => Clipboard.SetText(badge));

                    // B. Call your library method
                    // (Assuming you've added the PostDeskLoginByCardAsync logic to ImportService)
                    await _importService.PostDeskLoginByCardAsync(badge);
                };
                _serialPort.Open();
            }
            catch { /* Handle Port Error */ }
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