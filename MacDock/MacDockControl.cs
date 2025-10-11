using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Security.Principal;

namespace MacDock
{
    public enum DockPosition
    {
        Bottom,
        Top,
        Left,
        Right
    }

    public partial class MacDockControl : Form
    {
        private class DockItem
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string IconPath { get; set; }
        }

        public class DockRulesOptions
        {
            public string OPT_IconSize { get; set; }
            public string OPT_DockPosition { get; set; }
            public string OPT_Layout32 {  get; set; }   
        }


        #region DICHIARAZIONI
        private List<DockItem> _items = new List<DockItem>();
        private Timer _hideTimer;
        private bool _isVisible = false;
        private int _dockSize = 80;
        private int _hiddenSize = 3;
        private DockPosition _dockPosition = DockPosition.Bottom;
        private string _configFile = Path.Combine(Application.StartupPath, "dock_items.json");
        private string _configOption = Path.Combine(Application.StartupPath, "dock_options.json");
        private const string AutoStartKeyName = "MacDockControlAutoStart";
        private const string TaskName = "MacDockControl_AutoStart";
        public DockPosition DockPosition
        {
            get => _dockPosition;
            set
            {
                _dockPosition = value;
                UpdateDockPosition();
                RebuildIcons();
            }
        }

        private int _iconSize = 48;

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

        public enum DockLayoutStyle
        {
            RowFirst,
            Alternating
        }

        private DockLayoutStyle _layoutStyle = DockLayoutStyle.RowFirst;
        public DockLayoutStyle LayoutStyle
        {
            get => _layoutStyle;
            set
            {
                _layoutStyle = value;
                RebuildIcons(); // ricostruisci le icone con il nuovo layout
            }
        }
        private System.Windows.Forms.ToolTip _toolTip;
        #endregion DICHIARAZIONI
   

        public MacDockControl()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.Black;
            Opacity = 0.6;
            StartPosition = FormStartPosition.Manual;

            _hideTimer = new Timer { Interval = 200 };
            _hideTimer.Tick += HideTimer_Tick;

            MouseEnter += (s, e) => ShowDock();
            MouseLeave += (s, e) => _hideTimer.Start();

            _toolTip = new System.Windows.Forms.ToolTip();

            UpdateDockPosition();
            LoadItems();
            LoaOpzions();
            RebuildIcons();

            this.ContextMenuStrip = BuildMainContextMenu();
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

        // --- PERSISTENZA OPZIONI
        private void LoaOpzions()
        {
            if (File.Exists(_configFile))
            {
                List< DockRulesOptions> DROs = new List< DockRulesOptions>();
                try
                {
                    var json = File.ReadAllText(_configOption, Encoding.UTF8);
                    DROs = JsonConvert.DeserializeObject<List<DockRulesOptions>>(json) ?? new List<DockRulesOptions>();
                }
                catch
                {
                    DROs = new List<DockRulesOptions>();
                }  
                
                foreach (var d in DROs)
                {
                    if (!string.IsNullOrEmpty(d.OPT_IconSize))
                        _iconSize = Convert.ToInt32(d.OPT_IconSize);
                    else
                        _iconSize = 48;

                    if (d.OPT_DockPosition == "Bottom")
                        _dockPosition = DockPosition.Bottom;
                    else if (d.OPT_DockPosition == "Top")
                        _dockPosition = DockPosition.Top;
                    else if (d.OPT_DockPosition == "Left")
                        _dockPosition = DockPosition.Left;
                    else if (d.OPT_DockPosition == "Right")
                        _dockPosition = DockPosition.Right;
                    else
                        _dockPosition = DockPosition.Bottom;

                    if (d.OPT_Layout32 == "RowFirst")
                        _layoutStyle = DockLayoutStyle.RowFirst;
                    else if (d.OPT_DockPosition == "Top")
                        _layoutStyle = DockLayoutStyle.Alternating;
                    else
                        _layoutStyle = DockLayoutStyle.RowFirst;
                }
            }
            else
            {
                SaveOptions();
            }
        }

        private void SaveOptions()
        {
            DockRulesOptions dro = new DockRulesOptions();
            dro.OPT_IconSize = _iconSize.ToString();
            dro.OPT_DockPosition = _dockPosition.ToString();  
            dro.OPT_Layout32 = _layoutStyle.ToString();
            List<DockRulesOptions> DROs = new List<DockRulesOptions>();
            DROs.Add(dro);

            try
            {
                var json = JsonConvert.SerializeObject(DROs, Formatting.Indented);
                File.WriteAllText(_configOption, json, Encoding.UTF8);
            }
            catch { /* ignore errors */ }
        }


        // --- POSIZIONE ---
        private void UpdateDockPosition(bool initiallyVisible = true)
        {
            Rectangle screen = Screen.PrimaryScreen.Bounds;

            switch (_dockPosition)
            {
                case DockPosition.Bottom:
                    SetBounds(0, screen.Bottom - _hiddenSize, screen.Width, _dockSize);
                    break;
                case DockPosition.Top:
                    SetBounds(0, screen.Top - (_dockSize - _hiddenSize), screen.Width, _dockSize);

                    break;
                case DockPosition.Left:
                    SetBounds(screen.Left, 0, _dockSize, screen.Height);
                    // Imposta visibilità iniziale
                    _isVisible = initiallyVisible;
                    Opacity = initiallyVisible ? 0.9 : 0.6;
                    break;
                case DockPosition.Right:
                    SetBounds(screen.Right - _dockSize, 0, _dockSize, screen.Height);
                    // Imposta visibilità iniziale
                    _isVisible = initiallyVisible;
                    Opacity = initiallyVisible ? 0.9 : 0.6;
                    break;
            }

        }



        // --- MOSTRA / NASCONDE ---
        private void HideTimer_Tick(object sender, EventArgs e)
        {
            if (!Bounds.Contains(Cursor.Position))
                HideDock();
        }

 

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Assicurati che il dock parta completamente visibile
            ShowDock();

            // Ora che il Form ha dimensioni corrette, ricostruisci le icone
            RebuildIcons();

            // Attiva timer per riduzione automatica
            _hideTimer.Start();
        }

        private void ShowDock()
        {
            Rectangle screen = Screen.PrimaryScreen.Bounds;

            switch (_dockPosition)
            {
                case DockPosition.Bottom:
                    Top = screen.Bottom - _dockSize;
                    Left = 0;
                    Width = screen.Width;
                    Height = _dockSize;
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

            Opacity = 0.9;
            _isVisible = true;
        }


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

        // --- RICOSTRUZIONE ICONE ---  
        private void RebuildIcons()
        {
            Controls.Clear();
            int spacing = 6;
            int maxPerRow = Math.Max(1, (Width - spacing) / (_iconSize + spacing));
            int maxPerCol = Math.Max(1, (Height - spacing) / (_iconSize + spacing));

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

                int row = 0, col = 0;

                // --- POSIZIONAMENTO ---
                if (_iconSize == 48)
                {
                    // Tutte su riga o colonna centrale
                    if (_dockPosition == DockPosition.Bottom || _dockPosition == DockPosition.Top)
                    {
                        int centerY = (Height - _iconSize) / 2;
                        pic.Left = spacing + i * (_iconSize + spacing);
                        pic.Top = centerY;
                    }
                    else // Left o Right
                    {
                        int centerX = (Width - _iconSize) / 2;
                        pic.Left = centerX;
                        pic.Top = spacing + i * (_iconSize + spacing);
                    }
                }
                else
                {
                    // Icone piccole (32x32)
                    switch (_dockPosition)
                    {
             

                        case DockPosition.Bottom:
                            {
                                // Prima riga sopra, seconda sotto
                                if (_layoutStyle == DockLayoutStyle.RowFirst)
                                {
                                    row = i / maxPerRow;
                                    col = i % maxPerRow;
                                }
                                else // Alternating
                                {
                                    row = i % 2;
                                    col = i / 2;
                                }

                                int totalHeight = (2 * _iconSize) + spacing; // solo 1 spaziatura verticale fra le due righe
                                int centerY = (Height - totalHeight) / 2;    // centro verticale del dock

                                // Riga 0 (sopra) → più in alto
                                pic.Left = spacing + col * (_iconSize + spacing);
                                pic.Top = centerY + (row * (_iconSize + spacing));
                                break;
                            }

                        case DockPosition.Top:
                            {
                                // Prima riga sotto, seconda sopra
                                if (_layoutStyle == DockLayoutStyle.RowFirst)
                                {

                                    row = i / maxPerRow;
                                    col = i % maxPerRow;
                                }
                                else // Alternating
                                {
                                    row = i % 2;
                                    col = i / 2;
                                }

                                int totalHeight = (2 * _iconSize) + spacing; // solo 1 spaziatura verticale fra le due righe
                                int centerY = (Height - totalHeight) / 2;    // centro verticale del dock

                                // Riga 0 (sotto) → più in basso
                                pic.Left = spacing + col * (_iconSize + spacing);
                                pic.Top = centerY + ((1 - row) * (_iconSize + spacing));
                                break;
                            }


                

                        case DockPosition.Left:
                            {
                                // prima colonna a destra, seconda a sinistra
                                if (_layoutStyle == DockLayoutStyle.RowFirst)
                                {

                                    int maxPerColL = Math.Max(1, (Height - spacing) / (_iconSize + spacing));
                                    int numCols = (_items.Count + maxPerColL - 1) / maxPerColL; // numero totale colonne

                                    row = i % maxPerColL;
                                    col = i / maxPerColL;

                                    // Calcola Left partendo dal bordo destro
                                    pic.Left = Width - spacing - ((col + 1) * _iconSize + col * spacing);
                                    pic.Top = spacing + row * (_iconSize + spacing);
                                }
                                else // Alternating
                                {
                                    col = i % 2;
                                    row = i / 2;
                                    col = 1 - col; // inverti per prima destra

                                    int totalWidthL = (2 * _iconSize) + spacing;          // larghezza totale delle due colonne
                                    int centerX = (Width - totalWidthL) / 2;             // centro orizzontale nel dock

                                    pic.Left = centerX + col * (_iconSize + spacing);
                                    pic.Top = spacing + row * (_iconSize + spacing);
                                }


                                break;
                            }

                        case DockPosition.Right:
                            {
                                // prima colonna sinistra, seconda a destra
                                if (_layoutStyle == DockLayoutStyle.RowFirst)
                                {
                                    col = i / maxPerCol;
                                    row = i % maxPerCol;
                                }
                                else // Alternating
                                {
                                    col = i % 2;
                                    row = i / 2;
                                }

                                int totalWidthR = (2 * _iconSize) + spacing;          // larghezza totale delle due colonne
                                int centerX = (Width - totalWidthR) / 2;             // centro orizzontale nel dock

                                pic.Left = centerX + col * (_iconSize + spacing);
                                pic.Top = spacing + row * (_iconSize + spacing);
                                break;
                            }


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
                        try { Process.Start(item.Path); }
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
            if (_iconSize == 32)
            {
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

                // Submenu layout
                var layoutMenu = new ToolStripMenuItem("Layout icone 32x32");
                var rowFirstItem = new ToolStripMenuItem("Rimepi prima la prima riga e poi la seconda", null, (s, e) => LayoutStyle = DockLayoutStyle.RowFirst)
                { Checked = _layoutStyle == DockLayoutStyle.RowFirst };
                var alternatingItem = new ToolStripMenuItem("2 righe alternate: Dispari/pari", null, (s, e) => LayoutStyle = DockLayoutStyle.Alternating)
                { Checked = _layoutStyle == DockLayoutStyle.Alternating };
                layoutMenu.DropDownItems.Add(rowFirstItem);
                layoutMenu.DropDownItems.Add(alternatingItem);
                //
                // Aggiorna il check al cambiamento
                rowFirstItem.Click += (s, e) =>
                {
                    LayoutStyle = DockLayoutStyle.RowFirst;
                    rowFirstItem.Checked = true;
                    alternatingItem.Checked = false;
                    SaveOptions();
                };
                alternatingItem.Click += (s, e) =>
                {
                    LayoutStyle = DockLayoutStyle.Alternating;
                    rowFirstItem.Checked = false;
                    alternatingItem.Checked = true;
                    SaveOptions();
                };

                var trasforma48 = new ToolStripMenuItem("Trasofrma icone in formato 48x48", null, (s, e) =>
                {
                    _iconSize = 48;
                    RebuildIcons();
                    SaveItems();
                    SaveOptions();
                    ContextMenuStrip = BuildMainContextMenu();
                });



                var exit = new ToolStripMenuItem("Chiudi Dock", null, (s, e) => Close());

                // --- Posizione del Dock ---
                var positionMenu = new ToolStripMenuItem("Posizione del Dock");
                foreach (DockPosition pos in Enum.GetValues(typeof(DockPosition)))
                {
                    var posItem = new ToolStripMenuItem(pos.ToString())
                    {
                        Checked = (_dockPosition == pos)
                    };
                    posItem.Click += (s, e) =>
                    {
                        _dockPosition = pos;
                        UpdateDockPosition();
                        RebuildIcons();
                        SaveItems();
                        SaveOptions();
                        foreach (ToolStripMenuItem mi in positionMenu.DropDownItems)
                            mi.Checked = (mi == posItem);
                    };
                    positionMenu.DropDownItems.Add(posItem);
                }

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

                menu.Items.AddRange(new ToolStripItem[] { add, clear, trasforma48, layoutMenu, new ToolStripSeparator(), positionMenu,  new ToolStripSeparator(),  autostartMenu, new ToolStripSeparator(),exit });

            }
            else
            {
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

                var trasforma32 = new ToolStripMenuItem("Trasofrma icone in formato 32x32", null, (s, e) =>
                {
                    _iconSize = 32;
                    RebuildIcons();
                    SaveItems();
                    SaveOptions();
                    ContextMenuStrip = BuildMainContextMenu();

                });
      

                // --- Posizione del Dock ---
                var positionMenu = new ToolStripMenuItem("Posizione del Dock");
                foreach (DockPosition pos in Enum.GetValues(typeof(DockPosition)))
                {
                    var posItem = new ToolStripMenuItem(pos.ToString())
                    {
                        Checked = (_dockPosition == pos)
                    };
                    posItem.Click += (s, e) =>
                    {
                        _dockPosition = pos;
                        UpdateDockPosition();
                        RebuildIcons();
                        SaveItems();
                        SaveOptions();
                        foreach (ToolStripMenuItem mi in positionMenu.DropDownItems)
                            mi.Checked = (mi == posItem);
                    };
                    positionMenu.DropDownItems.Add(posItem);
                }
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

                menu.Items.AddRange(new ToolStripItem[] { add, clear, trasforma32, new ToolStripSeparator(), positionMenu, new ToolStripSeparator(), autostartMenu, new ToolStripSeparator(), exit });
            }
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
    }
}

