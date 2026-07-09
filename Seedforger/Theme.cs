using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Seedforger {

  /// <summary>
  /// Self-contained, dependency-free modern theming for the WinForms UI.
  /// Applies a flat light or dark palette recursively to a control tree,
  /// switches the whole app to Segoe UI, flattens buttons/combos/inputs and
  /// darkens the window title bar (DWM). Replaces the removed sergiye.Common.UI
  /// theme engine.
  /// </summary>
  internal static class Theme {

    internal sealed class Palette {
      public Color Window;      // form background
      public Color Surface;     // panels / inputs
      public Color SurfaceAlt;  // grouped / striped areas
      public Color Text;
      public Color SubtleText;
      public Color Border;
      public Color Accent;      // buttons / selection
      public Color AccentText;
    }

    internal static readonly Palette Light = new Palette {
      Window = Color.FromArgb(0xF5, 0xF6, 0xF8),
      Surface = Color.White,
      SurfaceAlt = Color.FromArgb(0xEC, 0xEE, 0xF2),
      Text = Color.FromArgb(0x1E, 0x21, 0x27),
      SubtleText = Color.FromArgb(0x5A, 0x61, 0x6B),
      Border = Color.FromArgb(0xD0, 0xD4, 0xDB),
      Accent = Color.FromArgb(0x2F, 0x6F, 0xED),
      AccentText = Color.White,
    };

    internal static readonly Palette Dark = new Palette {
      Window = Color.FromArgb(0x1E, 0x1F, 0x22),
      Surface = Color.FromArgb(0x2A, 0x2B, 0x2F),
      SurfaceAlt = Color.FromArgb(0x33, 0x35, 0x3A),
      Text = Color.FromArgb(0xE8, 0xEA, 0xED),
      SubtleText = Color.FromArgb(0xA0, 0xA6, 0xAE),
      Border = Color.FromArgb(0x3C, 0x3F, 0x45),
      Accent = Color.FromArgb(0x4A, 0x86, 0xFF),
      AccentText = Color.White,
    };

    internal static readonly Font UiFont = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

    internal static Palette Current => AppOptions.DarkMode ? Dark : Light;

    internal static bool IsSystemDark() {
      try {
        using var key = Registry.CurrentUser.OpenSubKey(
          @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var v = key?.GetValue("AppsUseLightTheme");
        if (v is int i) return i == 0;
      }
      catch { /* default to light */ }
      return false;
    }

    /// <summary>Apply the theme to a form and all of its children.</summary>
    internal static void Apply(Form form, bool dark) {
      var p = dark ? Dark : Light;
      form.SuspendLayout();
      form.BackColor = p.Window;
      form.ForeColor = p.Text;
      form.Font = UiFont;
      ApplyToChildren(form, p);
      TrySetDarkTitleBar(form, dark);
      form.ResumeLayout(true);
    }

    /// <summary>Apply the theme to a stand-alone control subtree (e.g. a UserControl).</summary>
    internal static void ApplyTo(Control root, bool dark) {
      var p = dark ? Dark : Light;
      root.SuspendLayout();
      root.BackColor = p.Window;
      root.ForeColor = p.Text;
      root.Font = UiFont;
      ApplyToChildren(root, p);
      root.ResumeLayout(true);
    }

    private static void ApplyToChildren(Control parent, Palette p) {
      foreach (Control c in parent.Controls) {
        StyleControl(c, p);
        if (c.HasChildren) ApplyToChildren(c, p);
      }
    }

    private static void StyleControl(Control c, Palette p) {
      c.ForeColor = p.Text;
      switch (c) {
        case Button b:
          b.FlatStyle = FlatStyle.Flat;
          b.FlatAppearance.BorderSize = 0;
          b.BackColor = p.Accent;
          b.ForeColor = p.AccentText;
          b.FlatAppearance.MouseOverBackColor = Lighten(p.Accent, 0.12);
          b.FlatAppearance.MouseDownBackColor = Darken(p.Accent, 0.12);
          b.Padding = new Padding(6, 3, 6, 3);
          break;
        case TextBox tb:
          tb.BorderStyle = BorderStyle.FixedSingle;
          tb.BackColor = p.Surface;
          tb.ForeColor = p.Text;
          break;
        case ComboBox cb:
          cb.FlatStyle = FlatStyle.Flat;
          cb.BackColor = p.Surface;
          cb.ForeColor = p.Text;
          break;
        case CheckBox _:
        case RadioButton _:
          c.BackColor = Color.Transparent;
          break;
        case Label _:
          c.BackColor = Color.Transparent;
          break;
        case GroupBox _:
          c.BackColor = Color.Transparent;
          c.ForeColor = p.SubtleText;
          break;
        case TabControl tc:
          tc.BackColor = p.Window;
          break;
        case TabPage tp:
          tp.BackColor = p.Window;
          tp.ForeColor = p.Text;
          break;
        case Panel _:
          c.BackColor = p.Window;
          break;
        case MenuStrip ms:
          ms.BackColor = p.Surface;
          ms.ForeColor = p.Text;
          ms.Renderer = new ToolStripProfessionalRenderer(new ThemeColorTable(p));
          break;
        case StatusStrip ss:
          ss.BackColor = p.Surface;
          ss.ForeColor = p.SubtleText;
          ss.Renderer = new ToolStripProfessionalRenderer(new ThemeColorTable(p));
          break;
        case ToolStrip ts:
          ts.BackColor = p.Surface;
          ts.ForeColor = p.Text;
          ts.Renderer = new ToolStripProfessionalRenderer(new ThemeColorTable(p));
          break;
        case RichTextBox rtb:
          rtb.BorderStyle = BorderStyle.FixedSingle;
          rtb.BackColor = p.Surface;
          rtb.ForeColor = p.Text;
          break;
        default:
          c.BackColor = p.Window;
          break;
      }
    }

    private sealed class ThemeColorTable : ProfessionalColorTable {
      private readonly Palette p;
      public ThemeColorTable(Palette palette) { p = palette; UseSystemColors = false; }
      public override Color MenuItemSelected => p.Accent;
      public override Color MenuItemSelectedGradientBegin => p.Accent;
      public override Color MenuItemSelectedGradientEnd => p.Accent;
      public override Color MenuItemBorder => p.Accent;
      public override Color MenuBorder => p.Border;
      public override Color ToolStripDropDownBackground => p.Surface;
      public override Color ImageMarginGradientBegin => p.Surface;
      public override Color ImageMarginGradientMiddle => p.Surface;
      public override Color ImageMarginGradientEnd => p.Surface;
      public override Color MenuStripGradientBegin => p.Surface;
      public override Color MenuStripGradientEnd => p.Surface;
      public override Color MenuItemPressedGradientBegin => p.Surface;
      public override Color MenuItemPressedGradientEnd => p.Surface;
      public override Color SeparatorDark => p.Border;
      public override Color ToolStripBorder => p.Border;
    }

    // ---- DWM dark title bar ----
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static void TrySetDarkTitleBar(Form form, bool dark) {
      try {
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        var use = dark ? 1 : 0;
        DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref use, sizeof(int));
      }
      catch { /* non-Windows-11 or unsupported: ignore */ }
    }

    private static Color Lighten(Color c, double f) {
      return Color.FromArgb(c.A,
        (int) Math.Min(255, c.R + 255 * f),
        (int) Math.Min(255, c.G + 255 * f),
        (int) Math.Min(255, c.B + 255 * f));
    }

    private static Color Darken(Color c, double f) {
      return Color.FromArgb(c.A,
        (int) Math.Max(0, c.R - 255 * f),
        (int) Math.Max(0, c.G - 255 * f),
        (int) Math.Max(0, c.B - 255 * f));
    }
  }
}
