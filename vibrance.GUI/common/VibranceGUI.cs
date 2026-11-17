using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Application = System.Windows.Forms.Application;
using MessageBox = System.Windows.Forms.MessageBox;

namespace vibrance.GUI.common
{
    public partial class VibranceGUI : Form
    {
        private readonly int _defaultWindowsLevel;
        private readonly int _minTrackBarValue;
        private readonly int _maxTrackBarValue;
        private readonly int _defaultIngameValue;
        private readonly Func<int, string> _resolveLabelLevel;
        private readonly IVibranceProxy _v;
        private IRegistryController _registryController;
        private const string AppName = "vibranceGUI";

        private bool _allowVisible;
        private bool _exitRequested;
        private List<ApplicationSetting> _applicationSettings;
        private readonly List<ResolutionModeWrapper> _supportedResolutionList;
        private readonly Dictionary<string, Tuple<ResolutionModeWrapper, List<ResolutionModeWrapper>>> _windowsResolutionSettings;

        public VibranceGUI(
            Func<List<ApplicationSetting>, Dictionary<string, Tuple<ResolutionModeWrapper, List<ResolutionModeWrapper>>>, IVibranceProxy> getProxy, 
            int defaultWindowsLevel, 
            int minTrackBarValue,
            int maxTrackBarValue,
            int defaultIngameValue,
            Func<int, string> resolveLabelLevel)
        {
            _defaultWindowsLevel = defaultWindowsLevel;
            _minTrackBarValue = minTrackBarValue;
            _maxTrackBarValue = maxTrackBarValue;
            _defaultIngameValue = defaultIngameValue;
            _resolveLabelLevel = resolveLabelLevel;
            _allowVisible = true;

            InitializeComponent();
            DarkTheme.Apply(this);
            DarkTheme.PaintMenu(contextMenuStrip);

            trackBarWindowsLevel.Minimum = minTrackBarValue;
            trackBarWindowsLevel.Maximum = maxTrackBarValue;

            _windowsResolutionSettings = new Dictionary<string, Tuple<ResolutionModeWrapper, List<ResolutionModeWrapper>>>();
            foreach(Screen screen in Screen.AllScreens)
            {
                Devmode currentResolutionMode;
                if (ResolutionHelper.GetCurrentResolutionSettings(out currentResolutionMode, screen.DeviceName))
                {
                    List<ResolutionModeWrapper> availableResolutions = ResolutionHelper.EnumerateSupportedResolutionModes(screen.DeviceName);
                    if(screen.Primary)
                    {
                        _supportedResolutionList = availableResolutions;
                    }
                    var tuple = new Tuple<ResolutionModeWrapper, List<ResolutionModeWrapper>>(new ResolutionModeWrapper(currentResolutionMode), availableResolutions);
                    _windowsResolutionSettings.Add(screen.DeviceName, tuple);                    
                }
                else
                {
                    MessageBox.Show("無法取得目前解析度，因此無法切回原本的 Windows 解析度。");
                }
            }
            _applicationSettings = new List<ApplicationSetting>();
            _v = getProxy(_applicationSettings, _windowsResolutionSettings);

            backgroundWorker.WorkerReportsProgress = true;
            settingsBackgroundWorker.WorkerReportsProgress = true;

            backgroundWorker.RunWorkerAsync();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!_allowVisible)
            {
                value = false;
                if (!this.IsHandleCreated)
                {
                    CreateHandle();
                }
            }
            base.SetVisibleCore(value);
        }

        public void SetAllowVisible(bool value)
        {
            _allowVisible = value;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SetGuiEnabledFlag(false);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                //this.notifyIcon.Visible = true;
                //this.notifyIcon.BalloonTipText = "已縮小至系統匣，程式仍在執行。";
                //this.notifyIcon.ShowBalloonTip(250);
                this.Hide();
            }
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            int vibranceWindowsLevel = _defaultWindowsLevel;
            bool affectPrimaryMonitorOnly = false;
            bool neverSwitchResolution = false;

            while (!this.IsHandleCreated)
            {
                Thread.Sleep(500);
            }

            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    ReadVibranceSettings(out vibranceWindowsLevel, out affectPrimaryMonitorOnly, out neverSwitchResolution);
                });
            }
            else
            {
                ReadVibranceSettings(out vibranceWindowsLevel, out affectPrimaryMonitorOnly, out neverSwitchResolution);
            }

            if (_v.GetVibranceInfo().isInitialized)
            {
                backgroundWorker.ReportProgress(1);

                SetGuiEnabledFlag(true);

                _v.SetApplicationSettings(_applicationSettings);
                _v.SetShouldRun(true);
                _v.SetVibranceWindowsLevel(vibranceWindowsLevel);
                _v.SetAffectPrimaryMonitorOnly(affectPrimaryMonitorOnly);
                _v.SetNeverSwitchResolution(neverSwitchResolution);
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (_v != null && _v.GetVibranceInfo().isInitialized)
            {
                SetGuiEnabledFlag(true);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_exitRequested)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            CleanUp();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _exitRequested = true;
            Close();
        }

        private void trackBarWindowsLevel_Scroll(object sender, EventArgs e)
        {
            _v.SetVibranceWindowsLevel(trackBarWindowsLevel.Value);
            labelWindowsLevel.Text = _resolveLabelLevel(trackBarWindowsLevel.Value);
            if (!settingsBackgroundWorker.IsBusy)
            {
                settingsBackgroundWorker.RunWorkerAsync();
            }
        }

        private void settingsBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.Sleep(5000);
            ForceSaveVibranceSettings();
        }

        private void ForceSaveVibranceSettings()
        {
            int windowsLevel = 0;
            bool affectPrimaryMonitorOnly = false;
            bool neverSwitchResolution = false;
            this.Invoke((MethodInvoker)delegate
            {
                windowsLevel = trackBarWindowsLevel.Value;
                affectPrimaryMonitorOnly = checkBoxPrimaryMonitorOnly.Checked;
                neverSwitchResolution = checkBoxNeverChangeResolutions.Checked;
            });
            SaveVibranceSettings(windowsLevel, affectPrimaryMonitorOnly, neverSwitchResolution);
        }

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 1)
            {
                this.statusLabel.Text = "執行中";
                this.statusLabel.ForeColor = Color.FromArgb(118, 224, 166);
            }
            else if (e.ProgressPercentage == 2)
            {
                this.statusLabel.Text = $"NVAPI 卸載狀態：{e.UserState}";
            }
        }

        private void settingsBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _allowVisible = true;
            this.Show();

            this.WindowState = FormWindowState.Normal;
            this.Visible = true;

            this.Refresh();
            this.ShowInTaskbar = true;
        }

        private void checkBoxPrimaryMonitorOnly_CheckedChanged(object sender, EventArgs e)
        {
            if (this._v == null)
            {
                return;
            }

            this._v.SetAffectPrimaryMonitorOnly(this.checkBoxPrimaryMonitorOnly.Checked);
            if (!this.settingsBackgroundWorker.IsBusy)
            {
                this.settingsBackgroundWorker.RunWorkerAsync();
            }
        }
        
        private void checkBoxNeverChangeResolutions_CheckedChanged(object sender, EventArgs e)
        {
            if (this._v == null)
            {
                return;
            }

            this._v.SetNeverSwitchResolution(this.checkBoxNeverChangeResolutions.Checked);
            if (!this.settingsBackgroundWorker.IsBusy)
            {
                this.settingsBackgroundWorker.RunWorkerAsync();
            }
        }

        private void checkBoxAutostart_CheckedChanged(object sender, EventArgs e)
        {
            RegistryController autostartController = new RegistryController();
            if (this.checkBoxAutostart.Checked)
            {
                string pathToExe = "\"" + Application.ExecutablePath + "\" -minimized";
                if (!autostartController.IsProgramRegistered(AppName))
                {
                    this.notifyIcon.BalloonTipText = autostartController.RegisterProgram(AppName, pathToExe) 
                        ? "已啟用開機自動啟動。" 
                        : "無法啟用開機自動啟動。";
                }
                else if (!autostartController.IsStartupPathUnchanged(AppName, pathToExe))
                {
                    this.notifyIcon.BalloonTipText = autostartController.RegisterProgram(AppName, pathToExe)
                        ? "已更新開機啟動路徑。"
                        : "無法更新開機啟動路徑。";
                }
                else
                {
                    return;
                }
            }
            else
            {
                this.notifyIcon.BalloonTipText = autostartController.UnregisterProgram(AppName) 
                    ? "已停用開機自動啟動。" 
                    : "無法停用開機自動啟動。";
            }

            notifyIcon.ShowBalloonTip(250);
        }

        private void SetGuiEnabledFlag(bool flag)
        {
            this.Invoke((MethodInvoker)delegate
            {
                this.trackBarWindowsLevel.Enabled = flag;
                this.checkBoxAutostart.Enabled = flag;
                this.checkBoxPrimaryMonitorOnly.Enabled = flag;
                this.buttonAddProgram.Enabled = flag;
                this.buttonProcessExplorer.Enabled = flag;
                this.buttonRemoveProgram.Enabled = flag;
            });
        }

        private void CleanUp()
        {
            try
            {
                this.statusLabel.Text = "正在關閉…";
                this.statusLabel.ForeColor = Color.FromArgb(255, 152, 152);
                this.Update();
                if (_v != null && _v.GetVibranceInfo().isInitialized)
                {
                    _v.HandleDvcExit();
                    _v.SetShouldRun(false);
                    _v.UnloadLibraryEx();
                }
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        public static void Log(Exception ex)
        {
            using (StreamWriter w = File.AppendText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vibranceGUI.log")))
            {
                w.Write("\r\nLog Entry : ");
                w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(),
                    DateTime.Now.ToLongDateString());
                w.WriteLine("Exception Found:\nType: {0}", ex.GetType().FullName);
                w.WriteLine("Message: {0}", ex.Message);
                w.WriteLine("Source: {0}", ex.Source);
                w.WriteLine("Stacktrace: {0}", ex.StackTrace);
                w.WriteLine("Exception String: {0}", ex.ToString());

                w.WriteLine("-------------------------------");
            }
        }

        public static void Log(string msg)
        {
            using (StreamWriter w = File.AppendText("vibranceGUI_log.txt"))
            {
                w.Write("\r\nLog Entry : ");
                w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(),
                    DateTime.Now.ToLongDateString());
                w.WriteLine(msg);
                w.WriteLine("-------------------------------");
            }
        }

        private void ReadVibranceSettings(out int vibranceWindowsLevel, out bool affectPrimaryMonitorOnly, out bool neverSwitchResolution)
        {
            _registryController = new RegistryController();
            this.checkBoxAutostart.Checked = _registryController.IsProgramRegistered(AppName);

            SettingsController settingsController = new SettingsController();
            settingsController.ReadVibranceSettings(_v.GraphicsAdapter, out vibranceWindowsLevel, out affectPrimaryMonitorOnly, out neverSwitchResolution, out _applicationSettings);

            if (this.IsHandleCreated)
            {
                //no null check needed, SettingsController will always return matching values.
                labelWindowsLevel.Text = _resolveLabelLevel(vibranceWindowsLevel);

                trackBarWindowsLevel.Value = vibranceWindowsLevel;
                checkBoxPrimaryMonitorOnly.Checked = affectPrimaryMonitorOnly;
                checkBoxNeverChangeResolutions.Checked = neverSwitchResolution;
                foreach (ApplicationSetting application in _applicationSettings.ToList())
                {
                    if (!File.Exists(application.FileName))
                    {
                        _applicationSettings.Remove(application);
                        continue;
                    }                        

                    InitializeApplicationList();

                    Icon icon = Icon.ExtractAssociatedIcon(application.FileName);
                    if (icon != null)
                    {
                        this.listApplications.LargeImageList.Images.Add(icon);
                        ListViewItem lvi = new ListViewItem(application.Name);
                        lvi.ImageIndex = this.listApplications.Items.Count;
                        lvi.Tag = application.FileName;
                        this.listApplications.Items.Add(lvi);
                    }
                }
            }
        }

        private void SaveVibranceSettings(int windowsLevel, bool affectPrimaryMonitorOnly, bool neverSwitchResolution)
        {
            SettingsController settingsController = new SettingsController();

            settingsController.SetVibranceSettings(
                windowsLevel.ToString(),
                affectPrimaryMonitorOnly.ToString(),
                neverSwitchResolution.ToString(),
                _applicationSettings
            );
        }

        private void buttonAddProgram_Click(object sender, EventArgs e)
        {
            InitializeApplicationList();

            OpenFileDialog fileDialog = new OpenFileDialog();
            DialogResult result = fileDialog.ShowDialog();
            if (result == DialogResult.OK && fileDialog.CheckFileExists && fileDialog.SafeFileName != null 
                && _applicationSettings.FirstOrDefault(x => x.FileName.ToLower() == fileDialog.FileName.ToLower()) == null)
            {
                Icon icon = Icon.ExtractAssociatedIcon(fileDialog.FileName);
                if (icon != null)
                {
                    ProcessExplorerEntry processExplorerEntry = new ProcessExplorerEntry(fileDialog.FileName, icon, Path.GetFileNameWithoutExtension(fileDialog.FileName));
                    AddProgramIntern(processExplorerEntry);
                }
            }
        }

        public void AddProgramExtern(ProcessExplorerEntry processExplorerEntry)
        {
            if(this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    AddProgramIntern(processExplorerEntry);
                });
            }
            else
            {
                AddProgramIntern(processExplorerEntry);
            }
        }

        private void AddProgramIntern(ProcessExplorerEntry processExplorerEntry)
        {
            InitializeApplicationList();
            
            if(!File.Exists(processExplorerEntry.Path) || _applicationSettings.FirstOrDefault(x => x.FileName.ToLower() == processExplorerEntry.Path.ToLower()) != null)
            {
                this.listApplications.SelectedIndices.Clear();
                return; 
            }

            Icon icon = processExplorerEntry.Icon;
            string path = processExplorerEntry.Path;
            if (icon != null)
            {
                this.listApplications.LargeImageList.Images.Add(icon);
                ListViewItem lvi = new ListViewItem(Path.GetFileNameWithoutExtension(path));
                lvi.ImageIndex = this.listApplications.Items.Count;
                lvi.Tag = path;
                this.listApplications.Items.Add(lvi);
                this.listApplications.SelectedIndices.Clear();
                lvi.Selected = true;
                listApplications_DoubleClick(this, EventArgs.Empty);
            }
        }

        private void InitializeApplicationList()
        {
            if (this.listApplications.LargeImageList == null)
            {
                ImageList imageList = new ImageList();
                imageList.ImageSize = new Size(48, 48);
                imageList.ColorDepth = ColorDepth.Depth32Bit;
                this.listApplications.LargeImageList = imageList;
                ListViewItem_SetSpacing(this.listApplications, 48 + 24, 48 + 6 + 16);
            }
        }

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public int MakeLong(short lowPart, short highPart)
        {
            return (int)(((ushort)lowPart) | (uint)(highPart << 16));
        }

        public void ListViewItem_SetSpacing(ListView listview, short leftPadding, short topPadding)
        {
            const int LVM_FIRST = 0x1000;
            const int LVM_SETICONSPACING = LVM_FIRST + 53;
            SendMessage(listview.Handle, LVM_SETICONSPACING, IntPtr.Zero, (IntPtr)MakeLong(leftPadding, topPadding));
        }

        private void listApplications_DoubleClick(object sender, EventArgs e)
        {
            ListViewItem selectedItem = this.listApplications.SelectedItems[0];
            if (selectedItem != null)
            {
                ApplicationSetting actualSetting = _applicationSettings.FirstOrDefault(x => x.FileName == selectedItem.Tag.ToString());
                VibranceSettings settingsWindow = new VibranceSettings(_v, _minTrackBarValue, _maxTrackBarValue, _defaultIngameValue, selectedItem, actualSetting, _supportedResolutionList, _resolveLabelLevel);
                DialogResult result = settingsWindow.ShowDialog();
                if (result == DialogResult.OK)
                {
                    ApplicationSetting newSetting = settingsWindow.GetApplicationSetting();
                    if (_applicationSettings.FirstOrDefault(x => x.FileName == newSetting.FileName) != null)
                    {
                        _applicationSettings.Remove(_applicationSettings.First(x => x.FileName == newSetting.FileName));
                    }
                    _applicationSettings.Add(newSetting);
                    ForceSaveVibranceSettings();
                }
                else if(actualSetting == null)
                {
                    removeApplicationListItem(selectedItem);
                }
            }
        }

        private void buttonRemoveProgram_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem eachItem in listApplications.SelectedItems)
            {
                for (int i = eachItem.Index + 1; i < listApplications.Items.Count; i++)
                    listApplications.Items[i].ImageIndex--;

                removeApplicationListItem(eachItem);
                _applicationSettings.Remove(_applicationSettings.FirstOrDefault(x => x.FileName.Equals(eachItem.Tag.ToString())));
            }

            ForceSaveVibranceSettings();
        }

        private void removeApplicationListItem(ListViewItem item)
        {
            Image img = this.listApplications.LargeImageList.Images[item.ImageIndex];
            this.listApplications.LargeImageList.Images.RemoveAt(item.ImageIndex);
            img.Dispose();
            this.listApplications.Items.Remove(item);
        }

        private void buttonProcessExplorer_Click(object sender, EventArgs e)
        {
            ProcessExplorer ex = new ProcessExplorer(this);
            ex.Show();
        }
    }
}