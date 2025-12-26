using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace gammaswitcher
{
    public partial class Form1 : Form
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc? _proc;
        private static IntPtr _hookID = IntPtr.Zero;
        private static bool _gammaOn = false;
        private static IntPtr _hdc = IntPtr.Zero;
        private static Ramp _normalRamp, _brightRamp;
        private static NotifyIcon? trayIconStatic;
        private static int _hotkey = 0x7B;

        private NotifyIcon trayIconInstance = null!;
        private ContextMenuStrip contextMenu = null!;
        private const string SettingsFile = "settings.json";

        [StructLayout(LayoutKind.Sequential)]
        public struct Ramp
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[]? Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[]? Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[]? Blue;
        }

        public class Settings
        {
            public int Hotkey { get; set; } = 0x7B;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("gdi32.dll")]
        private static extern bool SetDeviceGammaRamp(IntPtr hdc, ref Ramp ramp);

        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(IntPtr hwnd, IntPtr hdc);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == _hotkey)
                {
                    Toggle();
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void Toggle()
        {
            _gammaOn = !_gammaOn;
            Ramp r = _gammaOn ? _brightRamp : _normalRamp;
            SetDeviceGammaRamp(_hdc, ref r);
            
            if (trayIconStatic != null)
                trayIconStatic.Text = "Gamma: " + (_gammaOn ? "ON" : "OFF");
        }

        public Form1()
        {
            InitializeComponent();
            LoadConfig();
            InitTray();
            InitGamma();
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            Visible = false;
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string data = File.ReadAllText(SettingsFile);
                    var cfg = JsonSerializer.Deserialize<Settings>(data);
                    if (cfg != null)
                        _hotkey = cfg.Hotkey;
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                var cfg = new Settings { Hotkey = _hotkey };
                string data = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, data);
            }
            catch { }
        }

        private void InitTray()
        {
            trayIconInstance = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Gamma (" + GetKeyStr(_hotkey) + ")",
                Visible = true
            };
            trayIconStatic = trayIconInstance;

            contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Toggle", null, (s, e) => Toggle());
            contextMenu.Items.Add("Change Key", null, OnChangeKey);
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, OnExit);

            trayIconInstance.ContextMenuStrip = contextMenu;
            trayIconInstance.DoubleClick += (s, e) => Toggle();
        }

        private void OnChangeKey(object? s, EventArgs e)
        {
            var dlg = new KeySelector(_hotkey);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _hotkey = dlg.Selected;
                SaveConfig();
                trayIconInstance.Text = "Gamma (" + GetKeyStr(_hotkey) + ")";
                MessageBox.Show("Key: " + GetKeyStr(_hotkey), "Done");
            }
        }

        private string GetKeyStr(int vk)
        {
            return ((Keys)vk).ToString();
        }

        private void InitGamma()
        {
            _hdc = GetDC(IntPtr.Zero);
            _normalRamp = MakeRamp(1.0f);
            _brightRamp = MakeRamp(4f);
            SetDeviceGammaRamp(_hdc, ref _normalRamp);
            _proc = HookCallback;
            _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);

            if (_hookID == IntPtr.Zero)
            {
                MessageBox.Show("Failed to install hook! Error: " + Marshal.GetLastWin32Error());
            }
        }


        private Ramp MakeRamp(float g)
        {
            Ramp r = new Ramp
            {
                Red = new ushort[256],
                Green = new ushort[256],
                Blue = new ushort[256]
            };

            for (int i = 0; i < 256; i++)
            {
                float v = (float)Math.Pow(i / 255.0, 1.0 / g) * 65535;
                ushort val = (ushort)Math.Min(v, 65535);
                r.Red![i] = r.Green![i] = r.Blue![i] = val;
            }
            return r;
        }

        private void OnExit(object? s, EventArgs e)
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
            }
            if (_hdc != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, _hdc);
            }
            trayIconInstance.Visible = false;
            Application.Exit();
        }


        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }
    }

    public class KeySelector : Form
    {
        public int Selected { get; private set; }
        private Label lbl;

        public KeySelector(int current)
        {
            Selected = current;
            Text = "Set Key";
            Size = new Size(300, 150);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            lbl = new Label
            {
                Text = "Press key...\nNow: " + ((Keys)current).ToString(),
                AutoSize = false,
                Size = new Size(260, 60),
                Location = new Point(20, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var btn = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(100, 80),
                Size = new Size(80, 30)
            };

            Controls.Add(lbl);
            Controls.Add(btn);
            CancelButton = btn;

            KeyPreview = true;
            KeyDown += OnKey;
        }

        private void OnKey(object? s, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.None && e.KeyCode != Keys.ControlKey && 
                e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.Alt)
            {
                Selected = (int)e.KeyCode;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
