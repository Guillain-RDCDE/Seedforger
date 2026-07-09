using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace Seedforger {
  public partial class MainForm : Form {

    private readonly RMCollection<RM> data = new RMCollection<RM>();

    // RM current;
    private int items;

    // RM current;
    private int allit;
    private bool trayIconBalloonIsUp;

    private ToolStripMenuItem realisticSpeedMenuItem;
    private ToolStripMenuItem darkModeMenuItem;

    internal MainForm() {
      InitializeComponent();
      Icon = System.Drawing.Icon.ExtractAssociatedIcon(typeof(MainForm).Assembly.Location);
      Text = AppInfo.Title;
      // The legacy build could restore an off-screen position; always start centered.
      StartPosition = FormStartPosition.CenterScreen;

      BuildOptionsMenu();
      LoadSettings();
    }

    private void BuildOptionsMenu() {
      realisticSpeedMenuItem = new ToolStripMenuItem("Realistic speed (ramp-up)") {
        CheckOnClick = true,
        Checked = AppOptions.RealisticSpeed,
      };
      realisticSpeedMenuItem.Click += (s, e) => {
        AppOptions.RealisticSpeed = realisticSpeedMenuItem.Checked;
        Settings.Current.RealisticSpeed = AppOptions.RealisticSpeed;
        Settings.Current.Save();
      };

      darkModeMenuItem = new ToolStripMenuItem("Dark mode") {
        CheckOnClick = true,
        Checked = AppOptions.DarkMode,
      };
      darkModeMenuItem.Click += (s, e) => {
        AppOptions.DarkMode = darkModeMenuItem.Checked;
        Settings.Current.DarkMode = AppOptions.DarkMode;
        Settings.Current.Save();
        ApplyThemeAll();
      };

      settingsToolStripMenuItem.DropDownItems.Insert(0, new ToolStripSeparator());
      settingsToolStripMenuItem.DropDownItems.Insert(0, realisticSpeedMenuItem);
      settingsToolStripMenuItem.DropDownItems.Insert(0, darkModeMenuItem);
    }

    /// <summary>Apply the modern restyle to the main window and every open tab.</summary>
    internal void ApplyThemeAll() {
      Theme.Apply(this);
      foreach (TabPage page in tab.TabPages) {
        if (page.Controls.Count > 0 && page.Controls[0] is RM rm) {
          Theme.ApplyTo(rm);
        }
      }
    }

    private void aboutToolStripMenuItem_Click(object sender, EventArgs e) {
      MessageBox.Show(
        $"{AppInfo.Name} v{AppInfo.Version}\n\n{AppInfo.SiteUrl}",
        AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void goToProgramSiteToolStripMenuItem_Click(object sender, EventArgs e) {
      try {
        Process.Start(new ProcessStartInfo(AppInfo.SiteUrl) { UseShellExecute = true });
      }
      catch (Exception ex) {
        MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
      }
    }

    private void newToolStripMenuItem_Click(object sender, EventArgs e) {
      Add("");
    }

    private void MainForm_Load(object sender, EventArgs e) {
      // txtVersion.Text = VersionChecker.PublicVersion;
      // txtRemote.Text = versionChecker.RemoteVersion;
      // txtLocal.Text = VersionChecker.LocalVersion;
      // txtReleaseDate.Text = VersionChecker.ReleaseDate;
      // Log += versionChecker.Log;

      // lblSize.Text = this.Width + "x" + this.Height;

      // trayIcon.Text += versionChecker.PublicVersion;
      // trayIcon.BalloonTipTitle += " " + versionChecker.PublicVersion;
      Add("");
      lblIp.Text = Functions.GetIp();
      tab_TabIndexChanged(null, null);
      trayIcon.Icon = Icon; // keep the tray icon in sync with the app icon
      ApplyThemeAll();
      // tab.Size = new Size(Width - 8, Height - 80);
    }

    private void WinRestore() {
      if (WindowState == FormWindowState.Minimized) {
        Show();
        WindowState = FormWindowState.Normal;
        trayIcon.Visible = false;
      }

      // Activate the form.
      Activate();
      Focus();
    }

    private void MainForm_Move(object sender, EventArgs e) {
      // If we are minimizing the form then hide it so it doesn't show up on the task bar
      if (WindowState == FormWindowState.Minimized && chkMinimize.Checked) {
        Hide();
        trayIcon.Visible = true;
      }
      else {
        // any other windows state show it.
        Show();
      }

      // lblLocation.Text = this.Location.X + "x" + this.Location.Y;
    }

    private void Exit() {
      SaveSettings((RM) tab.SelectedTab.Controls[0]);
      foreach (TabPage page in tab.TabPages) {
        ((RM) page.Controls[0]).StopButton_Click(null, null);
      }

      Application.Exit();
      Process.GetCurrentProcess().Kill();
    }

    #region Tray items

    private void trayIcon_MouseDoubleClick(object sender, MouseEventArgs e) {
      if (e.Button != MouseButtons.Right) {
        WinRestore();
      }
    }

    private void restoreToolStripMenuItem_Click(object sender, EventArgs e) {
      WinRestore();
    }

    private void trayIcon_MouseMove(object sender, MouseEventArgs e) {
      if (checkShowTrayBaloon.Checked && trayIconBalloonIsUp == false) {
        trayIcon.BalloonTipText = "";
        foreach (TabPage page in tab.TabPages) {
          try {
            trayIcon.BalloonTipText += page.Text + " - " + ((RM) page.Controls[0]).currentTorrentFile.Name + "\n";
          }
          catch {
            trayIcon.BalloonTipText += page.Text + " - Not opened!" + "\n";
          }
        }

        trayIcon.ShowBalloonTip(0);
      }
    }

    private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
      Exit();
    }

    private void trayIcon_BalloonTipClicked(object sender, EventArgs e) {
      trayIconBalloonIsUp = false;
    }

    private void trayIcon_BalloonTipClosed(object sender, EventArgs e) {
      trayIconBalloonIsUp = false;
    }

    private void trayIcon_BalloonTipShown(object sender, EventArgs e) {
      trayIconBalloonIsUp = true;
    }

    #endregion

    #region Tabs

    private void EditCurrent(string fileName) {
      ((RM) tab.SelectedTab.Controls[0]).LoadTorrentFileInfo(fileName);
    }

    private void Add(string fileName) {
      items++;
      allit++;
      var rm1 = new RM();
      data.Add(rm1);

      // current = rm1;
      var page1 = new TabPage("RM " + allit);
      page1.Name = "RM" + items;
      rm1.Dock = DockStyle.Fill;
      page1.Controls.Add(rm1);

      // page1.Enter += new EventHandler(this.TabPage_Enter);
      // page1.BorderStyle = BorderStyle.FixedSingle;
      // page1.BackColor = Color.White;
      tab.Controls.Add(page1);
      tab.SelectedTab = page1;
      lblTabs.Text = allit.ToString();
      if (fileName != "") {
        ((RM) tab.SelectedTab.Controls[0]).LoadTorrentFileInfo(fileName);
      }

      page1.ToolTipText = "Double click to rename this tab";
      tab.ShowToolTips = true;
    }

    private void Remove() {
      if (tab.TabPages.Count < 2) return;
      var last = tab.SelectedIndex;
      ((RM) tab.SelectedTab.Controls[0]).StopButton_Click(null, null);
      allit--;

      tab.TabPages.Remove(tab.SelectedTab);
      tab.SelectedIndex = last;
      lblTabs.Text = allit.ToString();
    }

    private void RenameTabs() {
      var curr = 0;
      foreach (TabPage page in tab.TabPages) {
        if (page.Text.Contains("RM ")) {
          curr++;
          page.Text = "RM " + curr;
        }
      }
    }

    private void renameCurrentToolStripMenuItem_Click(object sender, EventArgs e) {
      var prompt = new Prompt("Please select new tab name", "Type new tab name:", tab.SelectedTab.Text);
      if (prompt.ShowDialog() == DialogResult.OK) tab.SelectedTab.Text = prompt.Result;
    }

    private void removeCurrentToolStripMenuItem_Click(object sender, EventArgs e) {
      Remove();
      RenameTabs();
    }

    #endregion

    private void LoadSettings() {
      try {
        var s = Settings.Current;
        checkShowTrayBaloon.Checked = s.BallonTip;
        chkMinimize.Checked = s.MinimizeToTray;
        closeToTrayToolStripMenuItem.Checked = s.CloseToTray;
        AppOptions.RealisticSpeed = s.RealisticSpeed;
        // First launch follows the OS theme; afterwards the user's choice sticks.
        AppOptions.DarkMode = Settings.IsFirstRun ? Theme.IsSystemDark() : s.DarkMode;
        realisticSpeedMenuItem.Checked = AppOptions.RealisticSpeed;
        darkModeMenuItem.Checked = AppOptions.DarkMode;
        s.DarkMode = AppOptions.DarkMode;
        // Ensure the portable settings file exists next to the exe from the
        // first launch (mirrors the legacy key-creation on first read).
        s.Save();
      }
      catch {
      }
    }

    private void SaveSettings(RM rmData) {
      try {
        var s = Settings.Current;

        s.NewValues = rmData.chkNewValues.Checked;
        s.BallonTip = checkShowTrayBaloon.Checked;
        s.MinimizeToTray = chkMinimize.Checked;
        s.CloseToTray = closeToTrayToolStripMenuItem.Checked;
        s.RealisticSpeed = AppOptions.RealisticSpeed;
        s.DarkMode = AppOptions.DarkMode;

        s.Client = rmData.cmbClient.SelectedItem?.ToString() ?? s.Client;
        s.ClientVersion = rmData.cmbVersion.SelectedItem?.ToString() ?? s.ClientVersion;
        s.UploadRate = rmData.uploadRate.Text;
        s.DownloadRate = rmData.downloadRate.Text;
        s.Interval = rmData.interval.Text;
        s.FileSize = rmData.fileSize.Text;
        s.Directory = rmData.DefaultDirectory;
        s.TCPlistener = rmData.checkTCPListen.Checked;
        s.ScrapeInfo = rmData.checkRequestScrap.Checked;

        // Random value
        s.GetRandUp = rmData.chkRandUP.Checked;
        s.GetRandDown = rmData.chkRandDown.Checked;
        s.MinRandUp = rmData.txtRandUpMin.Text;
        s.MaxRandUp = rmData.txtRandUpMax.Text;
        s.MinRandDown = rmData.txtRandDownMin.Text;
        s.MaxRandDown = rmData.txtRandDownMax.Text;

        // Custom values
        s.CustomKey = rmData.customKey.Text;
        s.CustomPeerID = rmData.customPeerID.Text;
        s.CustomPeers = rmData.customPeersNum.Text;
        s.CustomPort = rmData.customPort.Text;

        // Stop after...
        s.StopWhen = rmData.cmbStopAfter.SelectedItem?.ToString() ?? s.StopWhen;
        s.StopAfter = rmData.txtStopValue.Text;

        // Proxy
        s.ProxyType = rmData.comboProxyType.SelectedItem?.ToString() ?? s.ProxyType;
        s.ProxyAdress = rmData.textProxyHost.Text;
        s.ProxyUser = rmData.textProxyUser.Text;
        s.ProxyPass = rmData.textProxyPass.Text;
        s.ProxyPort = rmData.textProxyPort.Text;

        // Random value on next
        s.GetRandUpNext = rmData.checkRandomUpload.Checked;
        s.GetRandDownNext = rmData.checkRandomDownload.Checked;
        s.MinRandUpNext = rmData.RandomUploadFrom.Text;
        s.MaxRandUpNext = rmData.RandomUploadTo.Text;
        s.MinRandDownNext = rmData.RandomDownloadFrom.Text;
        s.MaxRandDownNext = rmData.RandomDownloadTo.Text;
        s.IgnoreFailureReason = rmData.checkIgnoreFailureReason.Checked;

        s.Save();
      }
      catch (Exception e) {
        // Log += "Error in SetSettings(): " + e.Message + "\n";
      }
    }

    internal static long ParseValidInt64(string str, long defVal) {
      try {
        return long.Parse(str);
      }
      catch (Exception) {
        return defVal;
      }
    }

    internal static int ParseValidInt(string str, int defVal) {
      try {
        return int.Parse(str);
      }
      catch (Exception) {
        return defVal;
      }
    }

    internal static int BtoI(bool b) {
      if (b) return 1;
      else return 0;
    }

    internal static bool ItoB(int param) {
      if (param == 0) return false;
      if (param == 1) return true;
      return true;
    }

    internal void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
      if (closeToTrayToolStripMenuItem.Checked && chkMinimize.Checked) {
        e.Cancel = true;
        WindowState = FormWindowState.Minimized;
        Hide();
        trayIcon.Visible = true;
      }
      else Exit();
    }

    private void startToolStripMenuItem_Click(object sender, EventArgs e) {
      ((RM) tab.SelectedTab.Controls[0]).StartButton_Click(null, null);
    }

    private void manualUpdateToolStripMenuItem_Click(object sender, EventArgs e) {
      ((RM) tab.SelectedTab.Controls[0]).manualUpdateButton_Click(null, null);
    }

    private void stopToolStripMenuItem_Click(object sender, EventArgs e) {
      ((RM) tab.SelectedTab.Controls[0]).StopButton_Click(null, null);
    }

    #region Sessions

    private bool startThem;
    private bool stopThem;

    private static void AppendItem(XmlDocument aXmlDoc, XmlElement aXmlElement, string value, string name) {
      var itemElement = aXmlDoc.CreateElement(name);
      itemElement.InnerText = value;
      aXmlElement.AppendChild(itemElement);
    }

    private static void NewMainItem(XmlDocument aXmlDoc, XmlElement aXmlElement, RM data, string name) {
      AppendItem(aXmlDoc, aXmlElement, name, "Name");
      if (data.currentTorrent.filename != null)
        AppendItem(aXmlDoc, aXmlElement, data.currentTorrent.filename, "Address");
      else AppendItem(aXmlDoc, aXmlElement, data.torrentFile.Text, "Address");
      AppendItem(aXmlDoc, aXmlElement, data.trackerAddress.Text, "Tracker");
      AppendItem(aXmlDoc, aXmlElement, data.uploadRate.Text, "UploadSpeed");
      AppendItem(aXmlDoc, aXmlElement, data.chkRandUP.Checked.ToString(), "UploadRandom");
      AppendItem(aXmlDoc, aXmlElement, data.txtRandUpMin.Text, "UploadRandMin");
      AppendItem(aXmlDoc, aXmlElement, data.txtRandUpMax.Text, "UploadRandMax");
      AppendItem(aXmlDoc, aXmlElement, data.downloadRate.Text, "DownloadSpeed");
      AppendItem(aXmlDoc, aXmlElement, data.chkRandDown.Checked.ToString(), "DownloadRandom");
      AppendItem(aXmlDoc, aXmlElement, data.txtRandDownMin.Text, "DownloadRandMin");
      AppendItem(aXmlDoc, aXmlElement, data.txtRandDownMax.Text, "DownloadRandMax");
      AppendItem(aXmlDoc, aXmlElement, data.cmbClient.SelectedItem.ToString(), "Client");
      AppendItem(aXmlDoc, aXmlElement, data.cmbVersion.SelectedItem.ToString(), "Version");
      AppendItem(aXmlDoc, aXmlElement, data.fileSize.Text, "Finished");
      AppendItem(aXmlDoc, aXmlElement, data.cmbStopAfter.SelectedItem.ToString(), "StopType");
      AppendItem(aXmlDoc, aXmlElement, data.txtStopValue.Text, "StopValue");
      AppendItem(aXmlDoc, aXmlElement, data.customPort.Text, "Port");
      AppendItem(aXmlDoc, aXmlElement, data.checkTCPListen.Checked.ToString(), "UseTCP");
      AppendItem(aXmlDoc, aXmlElement, data.checkRequestScrap.Checked.ToString(), "UseScrape");
      AppendItem(aXmlDoc, aXmlElement, data.comboProxyType.SelectedItem.ToString(), "ProxyType");
      AppendItem(aXmlDoc, aXmlElement, data.textProxyUser.Text, "ProxyUser");
      AppendItem(aXmlDoc, aXmlElement, data.textProxyPass.Text, "ProxyPass");
      AppendItem(aXmlDoc, aXmlElement, data.textProxyHost.Text, "ProxyHost");
      AppendItem(aXmlDoc, aXmlElement, data.textProxyPort.Text, "ProxyPort");
      AppendItem(aXmlDoc, aXmlElement, data.checkRandomUpload.Checked.ToString(), "NextUpdateUpload");
      AppendItem(aXmlDoc, aXmlElement, data.RandomUploadFrom.Text, "NextUpdateUploadFrom");
      AppendItem(aXmlDoc, aXmlElement, data.RandomUploadTo.Text, "NextUpdateUploadTo");
      AppendItem(aXmlDoc, aXmlElement, data.checkRandomDownload.Checked.ToString(), "NextUpdateDownload");
      AppendItem(aXmlDoc, aXmlElement, data.RandomDownloadFrom.Text, "NextUpdateDownloadFrom");
      AppendItem(aXmlDoc, aXmlElement, data.RandomDownloadTo.Text, "NextUpdateDownloadTo");
      AppendItem(aXmlDoc, aXmlElement, data.checkIgnoreFailureReason.Checked.ToString(), "IgnoreFailureReason");
    }

    private void saveCurrentSessionToolStripMenuItem_Click(object sender, EventArgs e) {
      stopThem = true;
      saveSession.ShowDialog();
    }

    private void loadSessionToolStripMenuItem_Click(object sender, EventArgs e) {
      startThem = false;
      loadSession.ShowDialog();
    }

    private void loadSessionAndStartToolStripMenuItem_Click(object sender, EventArgs e) {
      startThem = true;
      loadSession.ShowDialog();
    }

    private void saveCurrentSessionToolStripMenuItem1_Click(object sender, EventArgs e) {
      stopThem = false;
      saveSession.ShowDialog();
    }

    private void SaveSession(string path) {
      var doc = new XmlDocument();
      var main = doc.CreateElement("main");
      doc.AppendChild(main);
      foreach (TabPage page in tab.TabPages) {
        if (stopThem) ((RM) page.Controls[0]).StopButton_Click(null, null);
        var child = doc.CreateElement("Seedforger");
        main.AppendChild(child);
        NewMainItem(doc, child, (RM) page.Controls[0], page.Text);
      }

      doc.Save(path);
    }

    private void LoadSession(string path) {
      var doc = new XmlDocument();
      doc.Load(path);
      XmlNode root = doc.DocumentElement;
      foreach (XmlNode node in root.ChildNodes) {
        Add("");
        tab.SelectedTab.Text = node["Name"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).torrentFile.Text = node["Address"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).openFileDialog1.FileName = node["Address"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).openFileDialog1_FileOk(null, null);
        ((RM) tab.SelectedTab.Controls[0]).trackerAddress.Text = node["Tracker"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).uploadRate.Text = node["UploadSpeed"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).txtRandUpMin.Text = node["UploadRandMin"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).txtRandUpMax.Text = node["UploadRandMax"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).downloadRate.Text = node["DownloadSpeed"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).txtRandDownMin.Text = node["DownloadRandMin"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).txtRandDownMax.Text = node["DownloadRandMax"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).chkRandUP.Checked = bool.Parse(node["UploadRandom"].InnerText);
        ((RM) tab.SelectedTab.Controls[0]).chkRandDown.Checked = bool.Parse(node["DownloadRandom"].InnerText);
        ((RM) tab.SelectedTab.Controls[0]).cmbClient.SelectedItem = node["Client"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).cmbVersion.SelectedItem = node["Version"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).fileSize.Text = node["Finished"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).cmbStopAfter.SelectedItem = node["StopType"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).txtStopValue.Text = node["StopValue"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).customPort.Text = node["Port"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).checkTCPListen.Checked = bool.Parse(node["UseTCP"].InnerText);
        ((RM) tab.SelectedTab.Controls[0]).checkRequestScrap.Checked = bool.Parse(node["UseScrape"].InnerText);
        ((RM) tab.SelectedTab.Controls[0]).comboProxyType.SelectedItem = node["ProxyType"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).textProxyUser.Text = node["ProxyUser"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).textProxyPass.Text = node["ProxyPass"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).textProxyHost.Text = node["ProxyHost"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).textProxyPort.Text = node["ProxyPort"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).checkRandomUpload.Checked = bool.Parse(node["NextUpdateUpload"].InnerText);
        ((RM) tab.SelectedTab.Controls[0]).checkRandomDownload.Checked =
          bool.Parse(node["NextUpdateDownload"].InnerText);
        ((RM) tab.SelectedTab.Controls[0]).RandomUploadFrom.Text = node["NextUpdateUploadFrom"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).RandomUploadTo.Text = node["NextUpdateUploadTo"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).RandomDownloadFrom.Text = node["NextUpdateDownloadFrom"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).RandomDownloadTo.Text = node["NextUpdateDownloadTo"].InnerText;
        ((RM) tab.SelectedTab.Controls[0]).checkIgnoreFailureReason.Checked =
          bool.Parse(node["IgnoreFailureReason"].InnerText);
        if (startThem) ((RM) tab.SelectedTab.Controls[0]).StartButton_Click(null, null);
      }

      RenameTabs();
    }

    private void saveSession_FileOk(object sender, System.ComponentModel.CancelEventArgs e) {
      SaveSession(saveSession.FileName);
    }

    private void loadSession_FileOk(object sender, System.ComponentModel.CancelEventArgs e) {
      LoadSession(loadSession.FileName);
    }

    #endregion

    #region All Seedforgers menu

    private void startToolStripMenuItem1_Click(object sender, EventArgs e) {
      foreach (TabPage page in tab.TabPages) {
        ((RM) page.Controls[0]).StartButton_Click(null, null);
      }
    }

    private void stopToolStripMenuItem1_Click(object sender, EventArgs e) {
      foreach (TabPage page in tab.TabPages) {
        ((RM) page.Controls[0]).StopButton_Click(null, null);
      }
    }

    private void updateToolStripMenuItem_Click(object sender, EventArgs e) {
      foreach (TabPage page in tab.TabPages) {
        ((RM) page.Controls[0]).manualUpdateButton_Click(null, null);
      }
    }

    private void setUploadSpeedToToolStripMenuItem_Click(object sender, EventArgs e) {
      var prompt = new Prompt("Please type valid integer value", "Type new upload speed for all tabs:", "100");
      if (prompt.ShowDialog() == DialogResult.OK) {
        int value;
        try {
          value = int.Parse(prompt.Result);
        }
        catch {
          MessageBox.Show("Invalid value!\nTry again!", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return;
        }

        foreach (TabPage page in tab.TabPages) {
          ((RM) page.Controls[0]).UpdateTextBox(((RM) page.Controls[0]).uploadRate, value.ToString());
        }
      }
    }

    private void setDownloadSpeedToToolStripMenuItem_Click(object sender, EventArgs e) {
      var prompt = new Prompt("Please type valid integer value", "Type new download speed for all tabs:", "100");
      if (prompt.ShowDialog() == DialogResult.OK) {
        int value;
        try {
          value = int.Parse(prompt.Result);
        }
        catch {
          MessageBox.Show("Invalid value!\nTry again!", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return;
        }

        foreach (TabPage page in tab.TabPages) {
          ((RM) page.Controls[0]).UpdateTextBox(((RM) page.Controls[0]).downloadRate, value.ToString());
        }
      }
    }

    #endregion

    private void saveSettingsFromCurrentTabToolStripMenuItem_Click(object sender, EventArgs e) {
      SaveSettings((RM) tab.SelectedTab.Controls[0]);
    }

    private void tab_TabIndexChanged(object sender, EventArgs e) {
      /*
      if (GetTabType(tab.SelectedTab) == TabType.Seedforger)
      {
          currentToolStripMenuItem.Enabled = true;
          allSeedforgersToolStripMenuItem.Enabled = true;
          saveSettingsFromCurrentTabToolStripMenuItem.Enabled = true;
          newToolStripMenuItem.Enabled = true;
          browserToolStripMenuItem.Enabled = false;
      }
      else if (GetTabType(tab.SelectedTab) == TabType.Browser)
      {
          currentToolStripMenuItem.Enabled = false;
          allSeedforgersToolStripMenuItem.Enabled = false;
          saveSettingsFromCurrentTabToolStripMenuItem.Enabled = false;
          newToolStripMenuItem.Enabled = false;
          browserToolStripMenuItem.Enabled = true;
      }
       */
    }

    private static string GetFileExtension(string file) {
      var info = new FileInfo(file);
      return info.Extension;
    }

    private void MainForm_DragEnter(object sender, DragEventArgs e) {
      if (e.Data.GetDataPresent(DataFormats.FileDrop, false)) {
        e.Effect = DragDropEffects.All;
      }
    }

    private void MainForm_DragDrop(object sender, DragEventArgs e) {
      foreach (var fileName in (string[]) e.Data.GetData(DataFormats.FileDrop)) {
        // MessageBox.Show(fileName + "\n" + GetFileExtension(fileName), "Debug");
        if (GetFileExtension(fileName) == ".torrent") {
          if (MessageBox.Show(
                "You have successfully loaded this torrent file:\n" + fileName +
                "\nDo you want to load this torrent file in a new tab?", AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
              DialogResult.Yes) Add(fileName);
          else EditCurrent(fileName);
        }
        else if (GetFileExtension(fileName) == ".session") {
          MessageBox.Show("You have successfuly loaded this session file:\n" + fileName, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
          startThem = false;
          LoadSession(fileName);
        }
      }
    }
  }
}