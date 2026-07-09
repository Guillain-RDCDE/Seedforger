using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Seedforger {

  /// <summary>
  /// Flat, modern, self-contained restyle for the WinForms UI. Turns the dated
  /// 3D group boxes into clean cards, flattens inputs, gives buttons semantic
  /// colours (green start / red stop) and a dark log. Dependency-free.
  /// </summary>
  internal static class Theme {

    // Palette
    private static readonly Color Window = Color.FromArgb(0xF6, 0xF7, 0xF9);
    private static readonly Color Card = Color.White;
    private static readonly Color Border = Color.FromArgb(0xDD, 0xE1, 0xE6);
    private static readonly Color Text = Color.FromArgb(0x1F, 0x23, 0x28);
    private static readonly Color Subtle = Color.FromArgb(0x5B, 0x63, 0x6E);
    private static readonly Color Header = Color.FromArgb(0x2F, 0x6F, 0xED);
    private static readonly Color Accent = Color.FromArgb(0x2F, 0x6F, 0xED);
    private static readonly Color Green = Color.FromArgb(0x1F, 0xA9, 0x6B);
    private static readonly Color Red = Color.FromArgb(0xE0, 0x3E, 0x3E);
    private static readonly Color Neutral = Color.FromArgb(0x5B, 0x63, 0x6E);
    private static readonly Color LogBack = Color.FromArgb(0x1B, 0x1E, 0x23);
    private static readonly Color LogText = Color.FromArgb(0x8C, 0xE0, 0xB0);

    internal static readonly Font UiFont =
      new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font HeaderFont =
      new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold, GraphicsUnit.Point);

    internal static void Apply(Form form) {
      if (!AppOptions.ThemingEnabled) return;
      form.SuspendLayout();
      form.Font = UiFont;
      form.BackColor = Window;
      StyleTree(form);
      form.ResumeLayout(true);
      form.Invalidate(true);
    }

    internal static void ApplyTo(Control root) {
      if (!AppOptions.ThemingEnabled) return;
      root.SuspendLayout();
      root.Font = UiFont;
      root.BackColor = Window;
      StyleTree(root);
      root.ResumeLayout(true);
      root.Invalidate(true);
    }

    private static void StyleTree(Control parent) {
      foreach (Control c in parent.Controls) {
        Style(c);
        if (c.HasChildren) StyleTree(c);
      }
    }

    private static void Style(Control c) {
      switch (c) {
        case Button b:
          b.FlatStyle = FlatStyle.Flat;
          b.FlatAppearance.BorderSize = 0;
          b.ForeColor = Color.White;
          b.BackColor = SemanticColor(b.Text);
          b.UseVisualStyleBackColor = false;
          b.FlatAppearance.MouseOverBackColor = Lighten(b.BackColor, 0.10);
          b.FlatAppearance.MouseDownBackColor = Darken(b.BackColor, 0.10);
          b.Cursor = Cursors.Hand;
          break;
        case TextBox tb:
          tb.BorderStyle = BorderStyle.FixedSingle;
          tb.BackColor = tb.ReadOnly ? Window : Card;
          tb.ForeColor = Text;
          break;
        case ComboBox cb:
          cb.FlatStyle = FlatStyle.Flat;
          cb.BackColor = Card;
          cb.ForeColor = Text;
          break;
        case CheckBox _:
          c.BackColor = Color.Transparent;
          c.ForeColor = Text;
          break;
        case RadioButton _:
          c.BackColor = Color.Transparent;
          c.ForeColor = Text;
          break;
        case Label _:
          c.BackColor = Color.Transparent;
          c.ForeColor = Text;
          break;
        case GroupBox gb:
          gb.BackColor = Window;
          gb.ForeColor = Header;
          if (!(gb.Tag is string s && s == "flat")) {
            gb.Tag = "flat";
            gb.Paint += FlatGroupBoxPaint;
          }
          break;
        case TabControl tc:
          tc.BackColor = Window;
          break;
        case TabPage tp:
          tp.BackColor = Window;
          tp.ForeColor = Text;
          break;
        case Panel _:
          c.BackColor = Window;
          break;
        case RichTextBox rtb:
          rtb.BorderStyle = BorderStyle.None;
          rtb.BackColor = LogBack;
          rtb.ForeColor = LogText;
          break;
        case MenuStrip ms:
          ms.BackColor = Card;
          ms.ForeColor = Text;
          ms.Renderer = new FlatMenuRenderer();
          break;
        case StatusStrip ss:
          ss.BackColor = Card;
          ss.ForeColor = Subtle;
          ss.Renderer = new FlatMenuRenderer();
          break;
        case ContextMenuStrip cms:
          cms.Renderer = new FlatMenuRenderer();
          break;
      }
    }

    private static Color SemanticColor(string text) {
      var t = (text ?? string.Empty).ToUpperInvariant();
      if (t.Contains("START")) return Green;
      if (t.Contains("STOP")) return Red;
      if (t.Contains("DEFAULT")) return Neutral;
      return Accent;
    }

    // Redraw group boxes as flat rounded cards with a coloured bold header.
    private static void FlatGroupBoxPaint(object sender, PaintEventArgs e) {
      var gb = (GroupBox) sender;
      var g = e.Graphics;
      g.SmoothingMode = SmoothingMode.AntiAlias;
      g.Clear(gb.BackColor);

      var headerH = gb.Font.Height;
      var rect = new Rectangle(0, headerH / 2, gb.Width - 1, gb.Height - headerH / 2 - 1);
      using (var path = RoundedRect(rect, 8))
      using (var pen = new Pen(Border, 1f)) {
        g.DrawPath(pen, path);
      }

      if (!string.IsNullOrEmpty(gb.Text)) {
        var size = g.MeasureString(gb.Text, HeaderFont);
        // clear a gap in the border behind the title
        using (var bg = new SolidBrush(gb.BackColor))
          g.FillRectangle(bg, 10, 0, size.Width + 6, headerH);
        using (var brush = new SolidBrush(Header))
          g.DrawString(gb.Text, HeaderFont, brush, 12, 0);
      }
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
        e.TextColor = e.Item.Selected ? Color.White : Text;
        base.OnRenderItemText(e);
      }
      private sealed class Colors : ProfessionalColorTable {
        public Colors() { UseSystemColors = false; }
        public override Color MenuItemSelected => Accent;
        public override Color MenuItemSelectedGradientBegin => Accent;
        public override Color MenuItemSelectedGradientEnd => Accent;
        public override Color MenuItemBorder => Accent;
        public override Color MenuItemPressedGradientBegin => Card;
        public override Color MenuItemPressedGradientEnd => Card;
        public override Color ToolStripDropDownBackground => Card;
        public override Color ImageMarginGradientBegin => Card;
        public override Color ImageMarginGradientMiddle => Card;
        public override Color ImageMarginGradientEnd => Card;
        public override Color MenuBorder => Border;
        public override Color SeparatorDark => Border;
      }
    }

    private static Color Lighten(Color c, double f) => Color.FromArgb(c.A,
      (int) Math.Min(255, c.R + (255 - c.R) * f), (int) Math.Min(255, c.G + (255 - c.G) * f),
      (int) Math.Min(255, c.B + (255 - c.B) * f));

    private static Color Darken(Color c, double f) => Color.FromArgb(c.A,
      (int) Math.Max(0, c.R * (1 - f)), (int) Math.Max(0, c.G * (1 - f)), (int) Math.Max(0, c.B * (1 - f)));
  }
}
