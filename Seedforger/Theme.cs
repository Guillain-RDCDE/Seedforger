using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Seedforger {

  /// <summary>
  /// Flat, modern, self-contained restyle for the WinForms UI, with full light
  /// AND dark themes. Group boxes become clean cards, inputs are flattened,
  /// buttons get semantic colours (green start / red stop), tab headers and the
  /// window title bar are owner-drawn / DWM-darkened so dark mode looks complete.
  /// Dependency-free.
  /// </summary>
  internal static class Theme {

    internal sealed class Palette {
      public Color Window, Card, Border, Text, Subtle, Header, Input, InputText;
    }

    private static readonly Palette Light = new Palette {
      Window = Color.FromArgb(0xF6, 0xF7, 0xF9),
      Card = Color.White,
      Border = Color.FromArgb(0xDD, 0xE1, 0xE6),
      Text = Color.FromArgb(0x1F, 0x23, 0x28),
      Subtle = Color.FromArgb(0x5B, 0x63, 0x6E),
      Header = Color.FromArgb(0x2F, 0x6F, 0xED),
      Input = Color.White,
      InputText = Color.FromArgb(0x1F, 0x23, 0x28),
    };

    private static readonly Palette Dark = new Palette {
      Window = Color.FromArgb(0x1E, 0x20, 0x24),
      Card = Color.FromArgb(0x2A, 0x2D, 0x33),
      Border = Color.FromArgb(0x3A, 0x3E, 0x46),
      Text = Color.FromArgb(0xE6, 0xE9, 0xEE),
      Subtle = Color.FromArgb(0x9A, 0xA1, 0xAB),
      Header = Color.FromArgb(0x5A, 0x9C, 0xFF),
      Input = Color.FromArgb(0x33, 0x37, 0x3E),
      InputText = Color.FromArgb(0xE6, 0xE9, 0xEE),
    };

    // Semantic colours are shared by both themes.
    private static readonly Color Accent = Color.FromArgb(0x2F, 0x6F, 0xED);
    private static readonly Color Green = Color.FromArgb(0x1F, 0xA9, 0x6B);
    private static readonly Color Red = Color.FromArgb(0xE0, 0x3E, 0x3E);
    private static readonly Color LogBack = Color.FromArgb(0x14, 0x16, 0x1A);
    private static readonly Color LogText = Color.FromArgb(0x8C, 0xE0, 0xB0);

    internal static readonly Font UiFont =
      new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font HeaderFont =
      new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold, GraphicsUnit.Point);

    internal static Palette Cur => AppOptions.DarkMode ? Dark : Light;

    internal static bool IsSystemDark() {
      try {
        using var key = Registry.CurrentUser.OpenSubKey(
          @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        if (key?.GetValue("AppsUseLightTheme") is int i) return i == 0;
      }
      catch { /* default light */ }
      return false;
    }

    internal static void Apply(Form form) {
      if (!AppOptions.ThemingEnabled) return;
      form.SuspendLayout();
      form.Font = UiFont;
      form.BackColor = Cur.Window;
      StyleTree(form);
      form.ResumeLayout(true);
      TrySetDarkTitleBar(form, AppOptions.DarkMode);
      form.Invalidate(true);
    }

    internal static void ApplyTo(Control root) {
      if (!AppOptions.ThemingEnabled) return;
      root.SuspendLayout();
      root.Font = UiFont;
      root.BackColor = Cur.Window;
      StyleTree(root);
      root.ResumeLayout(true);
      root.Invalidate(true);
    }

    /// <summary>Re-assert the theme on a single control — used after programmatic
    /// value changes, which can revert an input's BackColor to its designer default.</summary>
    internal static void Restyle(Control c) {
      if (AppOptions.ThemingEnabled && c != null) Style(c);
    }

    private static void StyleTree(Control parent) {
      foreach (Control c in parent.Controls) {
        Style(c);
        if (c.HasChildren) StyleTree(c);
      }
    }

    private static void Style(Control c) {
      var p = Cur;
      switch (c) {
        case Button b:
          StyleButton(b);
          break;
        case TextBox tb:
          tb.BorderStyle = BorderStyle.FixedSingle;
          tb.BackColor = tb.ReadOnly ? p.Window : p.Input;
          tb.ForeColor = p.InputText;
          break;
        case ComboBox cb:
          cb.FlatStyle = FlatStyle.Flat;
          cb.BackColor = p.Input;
          cb.ForeColor = p.InputText;
          // A DropDownList combo ignores BackColor/ForeColor under visual styles
          // and renders white — so owner-draw the item area in the theme colours.
          if (cb.DropDownStyle != ComboBoxStyle.Simple) {
            cb.DrawMode = DrawMode.OwnerDrawFixed;
            if (!(cb.Tag is string cs && cs == "themed")) {
              cb.Tag = "themed";
              cb.DrawItem += ComboDrawItem;
            }
          }
          break;
        case CheckBox _:
        case RadioButton _:
          c.BackColor = Color.Transparent;
          c.ForeColor = p.Text;
          break;
        case Label _:
          c.BackColor = Color.Transparent;
          c.ForeColor = p.Text;
          break;
        case GroupBox gb:
          gb.BackColor = p.Window;
          gb.ForeColor = p.Header;
          if (!(gb.Tag is string gs && gs == "flat")) {
            gb.Tag = "flat";
            gb.Paint += FlatGroupBoxPaint;
          }
          break;
        case TabControl tc:
          tc.BackColor = p.Window;
          tc.DrawMode = TabDrawMode.OwnerDrawFixed;
          if (!(tc.Tag is string ts && ts == "flat")) {
            tc.Tag = "flat";
            tc.DrawItem += TabDrawItem;
          }
          break;
        case TabPage tp:
          tp.BackColor = p.Window;
          tp.ForeColor = p.Text;
          break;
        case Panel _:
          c.BackColor = p.Window;
          break;
        case RichTextBox rtb:
          rtb.BorderStyle = BorderStyle.None;
          rtb.BackColor = LogBack;
          rtb.ForeColor = LogText;
          // Vertical-only: a squeezed RichTextBox otherwise shows a light
          // horizontal scrollbar that reads as an ugly white band in dark mode.
          rtb.WordWrap = true;
          rtb.ScrollBars = RichTextBoxScrollBars.Vertical;
          break;
        case MenuStrip ms:
          ms.BackColor = p.Card;
          ms.ForeColor = p.Text;
          ms.Renderer = new FlatMenuRenderer();
          break;
        case StatusStrip ss:
          ss.BackColor = p.Card;
          ss.ForeColor = p.Subtle;
          ss.Renderer = new FlatMenuRenderer();
          break;
        case ContextMenuStrip cms:
          cms.BackColor = p.Card;
          cms.ForeColor = p.Text;
          cms.Renderer = new FlatMenuRenderer();
          break;
      }
    }

    // 1 = primary (green), 2 = danger (red), 0 = secondary (neutral ghost).
    // Keyed off the control Name so it survives UI translation of the label.
    private static int ButtonKind(Button b) {
      var n = (b.Name ?? string.Empty).ToUpperInvariant();
      if (n.Contains("START")) return 1;
      if (n.Contains("STOP")) return 2;
      var t = (b.Text ?? string.Empty).ToUpperInvariant();
      if (t.Contains("START")) return 1;
      if (t.Contains("STOP")) return 2;
      return 0;
    }

    private static void StyleButton(Button b) {
      b.FlatStyle = FlatStyle.Flat;
      b.FlatAppearance.BorderSize = 0;
      b.FlatAppearance.MouseOverBackColor = Color.Transparent; // drawn ourselves
      b.FlatAppearance.MouseDownBackColor = Color.Transparent;
      b.UseVisualStyleBackColor = false;
      var back = b.Parent?.BackColor ?? Cur.Window;
      b.BackColor = back;
      b.ForeColor = back; // hide the default label; FlatButtonPaint draws it
      b.Cursor = Cursors.Hand;
      if (!(b.Tag is string s && s == "btn")) {
        b.Tag = "btn";
        b.Paint += FlatButtonPaint;
        b.MouseEnter += (o, e) => ((Control) o).Invalidate();
        b.MouseLeave += (o, e) => ((Control) o).Invalidate();
      }
    }

    // Rounded, spaced "pill" buttons. Only start/stop are coloured; the rest are
    // quiet neutral/ghost buttons so the row doesn't look like a block of colour.
    private static void FlatButtonPaint(object sender, PaintEventArgs e) {
      var b = (Button) sender;
      var g = e.Graphics;
      g.SmoothingMode = SmoothingMode.AntiAlias;
      g.Clear(b.Parent?.BackColor ?? Cur.Window);

      const int inset = 4;
      var rect = new Rectangle(inset, inset, b.Width - 2 * inset - 1, b.Height - 2 * inset - 1);
      if (rect.Width <= 2 || rect.Height <= 2) return;

      var kind = ButtonKind(b);
      var hover = b.Enabled && b.ClientRectangle.Contains(b.PointToClient(Control.MousePosition));
      var radius = Math.Min(10, rect.Height / 2);

      using (var path = RoundedRect(rect, radius)) {
        Color textc;
        if (!b.Enabled) {
          using (var bg = new SolidBrush(Blend(Cur.Window, Cur.Border, 0.5))) g.FillPath(bg, path);
          textc = Cur.Subtle;
        }
        else if (kind == 0) { // ghost / secondary
          using (var bg = new SolidBrush(hover ? Blend(Cur.Card, Accent, 0.12) : Cur.Card))
            g.FillPath(bg, path);
          using (var pen = new Pen(hover ? Accent : Cur.Border, 1.4f))
            g.DrawPath(pen, path);
          textc = hover ? Accent : Cur.Text;
        }
        else {
          var fill = kind == 1 ? Green : Red;
          if (hover) fill = Lighten(fill, 0.10);
          using (var bg = new SolidBrush(fill)) g.FillPath(bg, path);
          textc = Color.White;
        }

        TextRenderer.DrawText(g, b.Text, b.Font, rect, textc,
          TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
      }
    }

    private static void ComboDrawItem(object sender, DrawItemEventArgs e) {
      var cb = (ComboBox) sender;
      var selected = (e.State & DrawItemState.Selected) != 0;
      var back = selected ? Accent : Cur.Input;
      var fore = selected ? Color.White : Cur.InputText;
      using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
      var text = e.Index >= 0 ? cb.GetItemText(cb.Items[e.Index]) : cb.Text;
      TextRenderer.DrawText(e.Graphics, text, cb.Font, e.Bounds, fore,
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static Color Blend(Color a, Color b, double t) => Color.FromArgb(
      (int) (a.R + (b.R - a.R) * t), (int) (a.G + (b.G - a.G) * t), (int) (a.B + (b.B - a.B) * t));

    // Group boxes as flat rounded cards with a coloured bold header.
    private static void FlatGroupBoxPaint(object sender, PaintEventArgs e) {
      var gb = (GroupBox) sender;
      var g = e.Graphics;
      g.SmoothingMode = SmoothingMode.AntiAlias;
      g.Clear(gb.BackColor);

      var headerH = gb.Font.Height;
      var rect = new Rectangle(0, headerH / 2, gb.Width - 1, gb.Height - headerH / 2 - 1);
      using (var path = RoundedRect(rect, 8))
      using (var pen = new Pen(Cur.Border, 1f))
        g.DrawPath(pen, path);

      if (!string.IsNullOrEmpty(gb.Text)) {
        var size = g.MeasureString(gb.Text, HeaderFont);
        using (var bg = new SolidBrush(gb.BackColor))
          g.FillRectangle(bg, 10, 0, size.Width + 6, headerH);
        using (var brush = new SolidBrush(Cur.Header))
          g.DrawString(gb.Text, HeaderFont, brush, 12, 0);
      }
    }

    // Flat, themed tab headers (green when selected).
    private static void TabDrawItem(object sender, DrawItemEventArgs e) {
      var tc = (TabControl) sender;
      if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
      var selected = tc.SelectedIndex == e.Index;
      var r = tc.GetTabRect(e.Index);
      using (var bg = new SolidBrush(selected ? Accent : Cur.Card))
        e.Graphics.FillRectangle(bg, r);
      TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text, tc.Font, r,
        selected ? Color.White : Cur.Text,
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius) {
      var d = radius * 2;
      var p = new GraphicsPath();
      p.AddArc(r.X, r.Y, d, d, 180, 90);
      p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
      p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
      p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
      p.CloseFigure();
      return p;
    }

    private sealed class FlatMenuRenderer : ToolStripProfessionalRenderer {
      internal FlatMenuRenderer() : base(new Colors()) { }
      protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
      protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
        // White on the accent highlight — for both the hovered (Selected) state
        // and the open top-level menu (Pressed), otherwise the open menu's title
        // rendered white-on-white and vanished.
        e.TextColor = (e.Item.Selected || e.Item.Pressed) ? Color.White : Cur.Text;
        base.OnRenderItemText(e);
      }
      private sealed class Colors : ProfessionalColorTable {
        public Colors() { UseSystemColors = false; }
        public override Color MenuItemSelected => Accent;
        public override Color MenuItemSelectedGradientBegin => Accent;
        public override Color MenuItemSelectedGradientEnd => Accent;
        public override Color MenuItemBorder => Accent;
        public override Color MenuItemPressedGradientBegin => Accent;
        public override Color MenuItemPressedGradientEnd => Accent;
        public override Color ToolStripDropDownBackground => Cur.Card;
        public override Color ImageMarginGradientBegin => Cur.Card;
        public override Color ImageMarginGradientMiddle => Cur.Card;
        public override Color ImageMarginGradientEnd => Cur.Card;
        public override Color MenuBorder => Cur.Border;
        public override Color SeparatorDark => Cur.Border;
        public override Color SeparatorLight => Cur.Border;
      }
    }

    // ---- DWM dark title bar (Windows 10 2004+/11) ----
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private static void TrySetDarkTitleBar(Form form, bool dark) {
      try {
        if (!form.IsHandleCreated) return;
        var use = dark ? 1 : 0;
        DwmSetWindowAttribute(form.Handle, 20, ref use, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
      }
      catch { /* older Windows: ignore */ }
    }

    private static Color Lighten(Color c, double f) => Color.FromArgb(c.A,
      (int) Math.Min(255, c.R + (255 - c.R) * f), (int) Math.Min(255, c.G + (255 - c.G) * f),
      (int) Math.Min(255, c.B + (255 - c.B) * f));

    private static Color Darken(Color c, double f) => Color.FromArgb(c.A,
      (int) Math.Max(0, c.R * (1 - f)), (int) Math.Max(0, c.G * (1 - f)), (int) Math.Max(0, c.B * (1 - f)));
  }
}
