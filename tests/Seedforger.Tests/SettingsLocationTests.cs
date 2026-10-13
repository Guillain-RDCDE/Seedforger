using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {

  /// <summary>Where settings.json lives: portable next to the executable when that
  /// folder is writable and already holds one, the per-user folder otherwise, with a
  /// one-time carry-over from a read-only folder. Everything runs in temp folders.</summary>
  public sealed class SettingsLocationTests : IDisposable {

    private readonly string root = Path.Combine(Path.GetTempPath(), "seedforger-settings-" + Guid.NewGuid().ToString("N"));
    private readonly string exeDir;
    private readonly string userDir;

    public SettingsLocationTests() {
      exeDir = Path.Combine(root, "app");
      userDir = Path.Combine(root, "config", "seedforger");
      Directory.CreateDirectory(exeDir);
    }

    public void Dispose() {
      try { Directory.Delete(root, recursive: true); } catch (Exception) { /* best effort */ }
    }

    private string PortableFile => Path.Combine(exeDir, SettingsLocation.FileName);
    private string UserFile => Path.Combine(userDir, SettingsLocation.FileName);

    private static void Write(string path, int uploadRate) {
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      File.WriteAllText(path, "{ \"UploadRate\": " + uploadRate + ", \"Language\": \"fr\" }");
    }

    private static int SavedUploadRate(string path) {
      using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
      return doc.RootElement.GetProperty("UploadRate").GetInt32();
    }

    [Fact]
    public void WritableFolderWithSettings_StaysPortable_AndKeepsItsValues() {
      Write(PortableFile, 4321);

      var loc = SettingsLocation.Resolve(exeDir, userDir, SettingsLocation.CanWriteTo);
      Assert.True(loc.Portable);
      Assert.Equal(PortableFile, loc.FilePath);
      Assert.Null(loc.MigrateFrom);

      var s = Settings.Load(loc);
      Assert.Equal(4321, s.UploadRate);
      Assert.Equal("fr", s.Language);
      Assert.True(s.Update(x => x.UploadRate = 99));
      Assert.Equal(99, SavedUploadRate(PortableFile));
      Assert.False(Directory.Exists(userDir));
    }

    [Fact]
    public void WritableFolderWithoutSettings_UsesTheUserFolder() {
      var loc = SettingsLocation.Resolve(exeDir, userDir, SettingsLocation.CanWriteTo);
      Assert.False(loc.Portable);
      Assert.Equal(UserFile, loc.FilePath);
      Assert.Null(loc.MigrateFrom);

      var s = Settings.Load(loc);
      Assert.Equal(new Settings().UploadRate, s.UploadRate);
      Assert.True(s.Update(x => x.UploadRate = 77));   // creates the missing folder
      Assert.Equal(77, SavedUploadRate(UserFile));
      Assert.False(File.Exists(PortableFile));
    }

    [Fact]
    public void EmptySettingsFileNextToTheExe_MakesTheCopyPortable() {
      File.WriteAllText(PortableFile, "");
      var loc = SettingsLocation.Resolve(exeDir, userDir, SettingsLocation.CanWriteTo);
      Assert.True(loc.Portable);
      var s = Settings.Load(loc);
      Assert.Equal(new Settings().UploadRate, s.UploadRate);
      Assert.False(File.Exists(PortableFile + ".bad"));
    }

    [Fact]
    public void ReadOnlyFolder_UsesTheUserFolder_AndCarriesTheOldFileOverOnce() {
      Write(PortableFile, 4321);
      var before = File.ReadAllText(PortableFile);
      var messages = CaptureLog(() => {
        var loc = SettingsLocation.Resolve(exeDir, userDir, _ => false);
        Assert.False(loc.Portable);
        Assert.Equal(UserFile, loc.FilePath);
        Assert.Equal(PortableFile, loc.MigrateFrom);

        var s = Settings.Load(loc);
        Assert.Equal(4321, s.UploadRate);
        Assert.Equal("fr", s.Language);
        Assert.Equal(4321, SavedUploadRate(UserFile));   // carried over right away
        Assert.True(s.Update(x => x.UploadRate = 55));
      });
      Assert.Contains(messages, m => m.Contains(PortableFile) && m.Contains(UserFile));
      Assert.Equal(before, File.ReadAllText(PortableFile)); // the old file is never touched

      // Next launch: the user file exists, so the old one is not read again.
      var again = SettingsLocation.Resolve(exeDir, userDir, _ => false);
      Assert.Null(again.MigrateFrom);
      Assert.Equal(55, Settings.Load(again).UploadRate);
    }

    [Fact]
    public void ReadOnlySettingsFile_InAWritableFolder_IsCarriedOverToo() {
      Write(PortableFile, 4321);
      File.SetAttributes(PortableFile, FileAttributes.ReadOnly);
      try {
        var loc = SettingsLocation.Resolve(exeDir, userDir, SettingsLocation.CanWriteTo);
        Assert.False(loc.Portable);
        Assert.Equal(PortableFile, loc.MigrateFrom);
        Assert.Equal(4321, Settings.Load(loc).UploadRate);
      }
      finally {
        File.SetAttributes(PortableFile, FileAttributes.Normal);
      }
    }

    [Fact]
    public void RealReadOnlyFolder_IsDetected_AndSendsSettingsToTheUserFolder() {
      Write(PortableFile, 4321);
      using (MakeReadOnly(exeDir)) {
        if (SettingsLocation.CanWriteTo(exeDir)) return; // root ignores permission bits: nothing to observe
        var loc = SettingsLocation.Resolve(exeDir, userDir, SettingsLocation.CanWriteTo);
        Assert.False(loc.Portable);
        Assert.Equal(UserFile, loc.FilePath);
        Assert.Equal(PortableFile, loc.MigrateFrom);
        var s = Settings.Load(loc);
        Assert.Equal(4321, s.UploadRate);
        Assert.True(s.Update(x => x.UploadRate = 66));
        Assert.Equal(66, SavedUploadRate(UserFile));
        Assert.Equal(4321, SavedUploadRate(PortableFile));
      }
      Assert.True(SettingsLocation.CanWriteTo(exeDir));
    }

    [Fact]
    public void FailedSave_IsReported_OnceUntilASaveSucceeds() {
      // A file where the settings folder should be: the folder cannot be created.
      var blocker = Path.Combine(root, "blocker");
      File.WriteAllText(blocker, "");
      var target = Path.Combine(blocker, SettingsLocation.FileName);
      var messages = CaptureLog(() => {
        var s = Settings.Load(new SettingsLocation(target, null, portable: false));
        Assert.False(s.Save());
        Assert.False(s.Update(x => x.UploadRate = 1));
      });
      Assert.Single(messages, m => m.Contains(target));
    }

    [Fact]
    public void CorruptFile_IsSetAside_AndReported() {
      Directory.CreateDirectory(userDir);
      File.WriteAllText(UserFile, "{ not json");
      var messages = CaptureLog(() => {
        var s = Settings.Load(SettingsLocation.Resolve(exeDir, userDir, SettingsLocation.CanWriteTo));
        Assert.Equal(new Settings().UploadRate, s.UploadRate);
      });
      Assert.True(File.Exists(UserFile + ".bad"));
      Assert.Contains(messages, m => m.Contains(UserFile + ".bad"));
    }

    [Fact]
    public void UserConfigDirectory_FollowsEachPlatformsConvention() {
      var home = Path.Combine(root, "home");
      var appData = Path.Combine(home, "AppData", "Roaming");
      var xdg = Path.Combine(root, "xdg");

      Assert.Equal(Path.Combine(appData, "Seedforger"),
        SettingsLocation.UserConfigDirectory(ConfigPlatform.Windows, home, appData, xdg));
      Assert.Equal(Path.Combine(home, "Library", "Application Support", "Seedforger"),
        SettingsLocation.UserConfigDirectory(ConfigPlatform.MacOS, home, Path.Combine(home, ".config"), xdg));
      Assert.Equal(Path.Combine(xdg, "seedforger"),
        SettingsLocation.UserConfigDirectory(ConfigPlatform.Unix, home, null, xdg));
      Assert.Equal(Path.Combine(home, ".config", "seedforger"),
        SettingsLocation.UserConfigDirectory(ConfigPlatform.Unix, home, null, null));
      Assert.Equal(Path.Combine(home, ".config", "seedforger"),
        SettingsLocation.UserConfigDirectory(ConfigPlatform.Unix, home, null, "relative/config"));
    }

    [Fact]
    public void CurrentProcess_ResolvesToTheExeFolderOrTheUserFolder() {
      var loc = SettingsLocation.ForCurrentProcess();
      var expected = loc.Portable ? AppContext.BaseDirectory : SettingsLocation.UserConfigDirectory();
      Assert.Equal(Path.Combine(expected, SettingsLocation.FileName), loc.FilePath);
    }

    // ---- clients.json and the sample files ----

    private const string OverrideJson =
      "[ { \"Family\": \"TestClient\", \"Version\": \"{0}\", \"PeerIdPrefix\": \"-TC0000-\" } ]";

    private static string Override(string version) => OverrideJson.Replace("{0}", version);

    [Fact]
    public void ClientsJson_UserFolderCopyWins_ThenTheExeFolder() {
      var exeCopy = Path.Combine(exeDir, TorrentClientFactory.OverridesFileName);
      var userCopy = Path.Combine(userDir, TorrentClientFactory.OverridesFileName);
      Assert.Null(SettingsLocation.FindDataFile(TorrentClientFactory.OverridesFileName, exeDir, userDir));

      File.WriteAllText(exeCopy, Override("1.0"));
      Assert.Equal(exeCopy, SettingsLocation.FindDataFile(TorrentClientFactory.OverridesFileName, exeDir, userDir));

      Directory.CreateDirectory(userDir);
      File.WriteAllText(userCopy, Override("2.0"));
      var found = SettingsLocation.FindDataFile(TorrentClientFactory.OverridesFileName, exeDir, userDir);
      Assert.Equal(userCopy, found);

      var profiles = TorrentClientFactory.LoadProfiles(found);
      Assert.Contains(profiles, p => p.FullName == "TestClient 2.0");
      Assert.DoesNotContain(profiles, p => p.FullName == "TestClient 1.0");
      Assert.Equal(DefaultClientProfiles.All.Count + 1, profiles.Count);
      Assert.Equal(DefaultClientProfiles.All.Count, TorrentClientFactory.LoadProfiles(null).Count);
    }

    [Fact]
    public void Samples_GoNextToTheExe_WhenItIsWritable() {
      var dir = SettingsLocation.SampleDirectory(exeDir, userDir, SettingsLocation.CanWriteTo);
      Assert.Equal(exeDir, dir);
      TorrentClientFactory.ExportSampleIfMissing(dir);
      Campaign.ExportSampleIfMissing(dir);
      Assert.True(File.Exists(Path.Combine(exeDir, TorrentClientFactory.SampleFileName)));
      Assert.True(File.Exists(Path.Combine(exeDir, Campaign.SampleFileName)));
      Assert.False(Directory.Exists(userDir));
    }

    [Fact]
    public void Samples_GoToTheUserFolder_WhenTheExeFolderIsReadOnly() {
      using (MakeReadOnly(exeDir)) {
        if (SettingsLocation.CanWriteTo(exeDir)) return; // root ignores permission bits: nothing to observe
        var dir = SettingsLocation.SampleDirectory(exeDir, userDir, SettingsLocation.CanWriteTo);
        Assert.Equal(userDir, dir);
        TorrentClientFactory.ExportSampleIfMissing(dir);   // creates the missing folder
        Campaign.ExportSampleIfMissing(dir);
      }
      var sample = Path.Combine(userDir, TorrentClientFactory.SampleFileName);
      Assert.True(File.Exists(sample));
      Assert.True(File.Exists(Path.Combine(userDir, Campaign.SampleFileName)));
      Assert.False(File.Exists(Path.Combine(exeDir, TorrentClientFactory.SampleFileName)));
      // The sample is the override format: copied to clients.json it loads every built-in profile.
      Assert.Equal(DefaultClientProfiles.All.Count, TorrentClientFactory.LoadProfiles(sample).Count);
      Assert.NotNull(Campaign.Load(Path.Combine(userDir, Campaign.SampleFileName)));
    }

    // ---- helpers ----

    private static List<string> CaptureLog(Action body) {
      var lines = new List<string>();
      Settings.Log = line => { lock (lines) lines.Add(line); };
      try { body(); }
      finally { Settings.Log = null; }
      return lines;
    }

    private static IDisposable MakeReadOnly(string dir) {
      if (OperatingSystem.IsWindows()) return DenyWrites(dir);
      return ChmodReadOnly(dir);
    }

    [UnsupportedOSPlatform("windows")]
    private static IDisposable ChmodReadOnly(string dir) {
      var mode = File.GetUnixFileMode(dir);
      File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
      return new Restore(() => File.SetUnixFileMode(dir, mode));
    }

    [SupportedOSPlatform("windows")]
    private static IDisposable DenyWrites(string dir) {
      var info = new DirectoryInfo(dir);
      var rule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,
        FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories, AccessControlType.Deny);
      var acl = info.GetAccessControl();
      acl.AddAccessRule(rule);
      info.SetAccessControl(acl);
      return new Restore(() => {
        var back = info.GetAccessControl();
        back.RemoveAccessRule(rule);
        info.SetAccessControl(back);
      });
    }

    private sealed class Restore : IDisposable {
      private Action undo;
      internal Restore(Action undo) { this.undo = undo; }
      public void Dispose() { undo?.Invoke(); undo = null; }
    }
  }
}
