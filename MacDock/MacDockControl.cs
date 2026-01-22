using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace MacDock
{
   

    public partial class MacDockControl : Form
    {
        private class DockItem
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string IconPath { get; set; }
        }


        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const uint MOD_ALT = 0x0001;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;

        const int SW_RESTORE = 9;
        const int HOTKEY_ID = 100;

        #region DICHIARAZIONI
        private List<DockItem> _items = new List<DockItem>();
        private Timer _hideTimer;
        private bool _isVisible = false;
        private int _iconSize = 48;
        private int _dockSize = 48 + 12;
        //private DockPosition _dockPosition = DockPosition.Bottom;
        private string _configFile = Path.Combine(Application.StartupPath, "dock_items.json");
        private string _configOption = Path.Combine(Application.StartupPath, "dock_options.json");
        private const string AutoStartKeyName = "MacDockControlAutoStart";
        private const string TaskName = "MacDockControl_AutoStart";
  
        public int IconSize
        {
            get => _iconSize;
            set
            {
                if (value < 16) value = 16; // dimensione minima
                if (value > 128) value = 128; // dimensione massima
                _iconSize = value;
                RebuildIcons(); // ricostruisci le icone con la nuova dimensione
            }
        }


        private System.Windows.Forms.ToolTip _toolTip;
        #endregion DICHIARAZIONI
   

        public MacDockControl()
        {
            

            this.Height = _dockSize;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            //TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.Black;
            //Opacity = 50;
            StartPosition = FormStartPosition.Manual;


            _toolTip = new System.Windows.Forms.ToolTip();

         
            LoadItems();
            RebuildIcons();

            this.ContextMenuStrip = BuildMainContextMenu();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            bool ok = RegisterHotKey(
                this.Handle,
                HOTKEY_ID,
                MOD_CONTROL | MOD_ALT,
                (uint)Keys.F12
            );

            if (!ok)
                MessageBox.Show("HotKey già in uso");

            PosizionaFinestra();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;

            const int WM_SETTINGCHANGE = 0x001A;
            const int WM_DISPLAYCHANGE = 0x007E;

            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                PortaInPrimoPiano();
            }

            base.WndProc(ref m);

            if (m.Msg == WM_SETTINGCHANGE || m.Msg == WM_DISPLAYCHANGE)
            {
                PosizionaFinestra();
                RebuildIcons();
            }
        }

  

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnFormClosing(e);
        }


        void PortaInPrimoPiano()
        {
            if (WindowState == FormWindowState.Minimized)
                ShowWindow(this.Handle, SW_RESTORE);

            SetForegroundWindow(this.Handle);
        }



        // --- PERSISTENZA ---
        private void LoadItems()
        {
            if (File.Exists(_configFile))
            {
                try
                {
                    var json = File.ReadAllText(_configFile, Encoding.UTF8);
                    _items = JsonConvert.DeserializeObject<List<DockItem>>(json) ?? new List<DockItem>();
                }
                catch
                {
                    _items = new List<DockItem>();
                }
            }
        }

        private void SaveItems()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_items, Formatting.Indented);
                File.WriteAllText(_configFile, json, Encoding.UTF8);
            }
            catch { /* ignore errors */ }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Assicurati che il dock parta completamente visibile
            ShowDock();

            // Ora che il Form ha dimensioni corrette, ricostruisci le icone
            RebuildIcons();

            // Attiva timer per riduzione automatica
            //_hideTimer.Start();
        }

        private void ShowDock()
        {
            Rectangle screen = Screen.PrimaryScreen.Bounds;

            /*
            switch (_dockPosition)
            {
                case DockPosition.Bottom:
                 
                    Top = screen.Bottom - _dockSize - 3; // leggermente sopra la taskbar
                    Left = 0;
                    Width = screen.Width;
                    Height = _dockSize;
                    SetWindowPos(Handle, HWND_TOPMOST, Left, Top, Width, Height, SWP_SHOWWINDOW | SWP_NOACTIVATE);
                    break;
                case DockPosition.Top:
                    Top = screen.Top;
                    Left = 0;
                    Width = screen.Width;
                    Height = _dockSize;
                    break;
                case DockPosition.Left:
                    Left = screen.Left;
                    Top = 0;
                    Width = _dockSize;
                    Height = screen.Height;
                    break;
                case DockPosition.Right:
                    Left = screen.Right - _dockSize;
                    Top = 0;
                    Width = _dockSize;
                    Height = screen.Height;
                    break;
            }
            */

            //Opacity = 0.9;
            _isVisible = true;
        }

        /*
        private void HideDock()
        {
            if (!_isVisible) return;
            _isVisible = false;

            Rectangle screen = Screen.PrimaryScreen.Bounds;
            switch (_dockPosition)
            {
                case DockPosition.Bottom:
                    Top = screen.Bottom - _hiddenSize;
                    break;
                case DockPosition.Top:
                    Top = screen.Top - (_dockSize - _hiddenSize);
                    break;
                case DockPosition.Left:
                    Left = screen.Left - (_dockSize - _hiddenSize);
                    break;
                case DockPosition.Right:
                    Left = screen.Right - _hiddenSize;
                    break;
            }

            Opacity = 0.6;
        }
        */

        // --- RICOSTRUZIONE ICONE ---  
        private void RebuildIcons()
        {
            Controls.Clear();
            int spacing = 6;
            //int maxPerRow = Math.Max(1, (Width - spacing) / (_iconSize + spacing));
            //int maxPerCol = Math.Max(1, (Height - spacing) / (_iconSize + spacing));

            if (_iconSize == 48)
            {
               

                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    PictureBox pic = new PictureBox
                    {
                        Size = new Size(_iconSize, _iconSize),
                        BackColor = Color.Transparent,
                        SizeMode = PictureBoxSizeMode.StretchImage,
                        Cursor = Cursors.Hand,
                        Tag = item
                    };

                    try
                    {
                        if (!string.IsNullOrEmpty(item.IconPath) && File.Exists(item.IconPath))
                            pic.Image = Image.FromFile(item.IconPath);
                        else if (File.Exists(item.Path))
                            pic.Image = Icon.ExtractAssociatedIcon(item.Path).ToBitmap();
                        else
                            pic.Image = SystemIcons.Application.ToBitmap();
                    }
                    catch
                    {
                        pic.Image = SystemIcons.Application.ToBitmap();
                    }

                    //int row = 0, col = 0;

                    // --- POSIZIONAMENTO ---
                    bool isPari = (_items.Count % 2 == 0);
                    int metaX = Width / 2;
                    int _top = (Height - _iconSize) / 2;

                    //
                    pic.Top = _top;
                    if (_items.Count == 1)
                        pic.Left = metaX - _iconSize / 2;
                    else
                    {
                        if (isPari)
                        {
                            int primaparte = _items.Count / 2;
                            int x_start_prima = primaparte * (_iconSize + spacing) + spacing;
                            pic.Left = metaX - x_start_prima + (_iconSize + spacing) * i;
                        }
                        else
                        {
                            int numMenoUno = _items.Count - 1;
                            int primaparte = numMenoUno / 2;
                            int x_start_prima = primaparte * (_iconSize + spacing) + (_iconSize / 2);
                            pic.Left = metaX - x_start_prima + (_iconSize + spacing) * i;
                        }

                    }

                    // --- Context menu singola icona ---
                    var iconMenu = new ContextMenuStrip();
                    iconMenu.Items.Add("Rimuovi da Dock", null, (s2, e2) => RemoveProgram(item));
                    pic.ContextMenuStrip = iconMenu;

                    // --- Click sinistro per avvio ---
                    pic.MouseDown += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Left)
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = item.Path,
                                UseShellExecute = true,
                                Verb = "runas" // richiede esecuzione come amministratore
                            };

                            try
                            {
                                //avvio il programma
                                //Process.Start(item.Path); 
                                Process.Start(psi);
                            }
                            catch
                            {
                                MessageBox.Show($"Impossibile avviare: {item.Path}",
                                    "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    };

                    pic.MouseEnter += (s, e) => pic.BackColor = Color.FromArgb(40, Color.White);
                    pic.MouseLeave += (s, e) => pic.BackColor = Color.Transparent;

                    _toolTip.SetToolTip(pic, item.Name);
                    Controls.Add(pic);
                }
            }
        }


        private void RemoveProgram(DockItem item)
        {
            if (MessageBox.Show($"Rimuovere '{item.Name}' dalla Dock?", "Conferma",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _items.Remove(item);
                SaveItems();
                RebuildIcons();
            }
        }

        // --- MENU PRINCIPALE DOCK (sfondo) ---
        private ContextMenuStrip BuildMainContextMenu()
        {
            var menu = new ContextMenuStrip();
            var add = new ToolStripMenuItem("Aggiungi programma...", null, (s, e) => AddProgram());

            var clear = new ToolStripMenuItem("Svuota Dock", null, (s, e) =>
            {
                if (MessageBox.Show("Svuotare tutte le icone?", "Conferma", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _items.Clear();
                    SaveItems();
                    RebuildIcons();
                }
            });

            var exit = new ToolStripMenuItem("Chiudi Dock", null, (s, e) => Close());

            // Nuova voce: avvio automatico
            var autostartMenu = new ToolStripMenuItem("Avvio automatico all'accensione");
            autostartMenu.CheckOnClick = true;
            autostartMenu.Checked = IsAutoStartEnabled();
            autostartMenu.CheckedChanged += (s, e) =>
            {
                if (autostartMenu.Checked)
                    EnableAutoStart();
                else
                    DisableAutoStart();
            };

            menu.Items.AddRange(new ToolStripItem[] { add, clear, new ToolStripSeparator(), autostartMenu, new ToolStripSeparator(), exit });

            return menu;
        }

        private void EnableAutoStart()
        {
            string exePath = Application.ExecutablePath;
            string userName = WindowsIdentity.GetCurrent().Name;

            using (TaskService ts = new TaskService())
            {
                // Elimina eventuale vecchia attività
                ts.RootFolder.DeleteTask(TaskName, false);

                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = "Avvia automaticamente MacDockControl all'accesso dell'utente";

                // Avvio al logon dell'utente corrente
                td.Triggers.Add(new LogonTrigger { UserId = userName });

                // Esegui il programma
                td.Actions.Add(new ExecAction(exePath, null, Path.GetDirectoryName(exePath)));

                // Esegui con privilegi elevati
                td.Principal.UserId = userName;
                td.Principal.LogonType = TaskLogonType.InteractiveToken;
                td.Principal.RunLevel = TaskRunLevel.Highest;

                // Impostazioni per compatibilità
                td.Settings.DisallowStartIfOnBatteries = false;
                td.Settings.StopIfGoingOnBatteries = false;
                td.Settings.RunOnlyIfNetworkAvailable = false;
                td.Settings.StartWhenAvailable = true;
                td.Settings.AllowHardTerminate = false;
                td.Settings.Hidden = false;

                ts.RootFolder.RegisterTaskDefinition(TaskName, td);
            }
        }

        private void DisableAutoStart()
        {
            using (TaskService ts = new TaskService())
                ts.RootFolder.DeleteTask(TaskName, false);
        }

        private bool IsAutoStartEnabled()
        {
            using (TaskService ts = new TaskService())
                return ts.GetTask(TaskName) != null;
        }

        private void AddProgram()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "Applicazioni (*.exe)|*.exe";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var item = new DockItem
                    {
                        Name = Path.GetFileNameWithoutExtension(dlg.FileName),
                        Path = dlg.FileName,
                        IconPath = ""
                    };

                    _items.Add(item);
                    SaveItems();
                    RebuildIcons();
                }
            }
        }

   

        private void PosizionaFinestra()
        {
            Screen screen = Screen.PrimaryScreen;
            Rectangle workingArea = screen.WorkingArea;

            int x = 20;

            int newTop = workingArea.Bottom -  (int)(_dockSize * 1.25);


            // sicurezza: evita valori negativi
            if (newTop < workingArea.Top)
                newTop = workingArea.Top;

            this.Location = new Point(x, newTop);
            this.Size = new Size(workingArea.Width - 2 * x, _dockSize);

        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PosizionaFinestra();
        }

   
    }
}

