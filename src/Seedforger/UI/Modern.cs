using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Seedforger.UI {

  /// <summary>Palette + small helpers for the new flat UI (dark-first, from scratch).</summary>
  internal static class Modern {
    internal static readonly Color Bg        = Color.FromArgb(0x15, 0x16, 0x1A);
    internal static readonly Color Card      = Color.FromArgb(0x1E, 0x20, 0x25);
    internal static readonly Color CardHi    = Color.FromArgb(0x24, 0x26, 0x2C);
    internal static readonly Color Border    = Color.FromArgb(0x2C, 0x2F, 0x36);
    internal static readonly Color Text      = Color.FromArgb(0xEC, 0xEE, 0xF2);
    internal static readonly Color Muted     = Color.FromArgb(0x8A, 0x90, 0x9C);
    internal static readonly Color Accent    = Color.FromArgb(0x4C, 0x8D, 0xF6);
    internal static readonly Color Green     = Color.FromArgb(0x22, 0xC5, 0x5E);
    internal static readonly Color Red       = Color.FromArgb(0xF0, 0x50, 0x50);
    internal static readonly Color Input     = Color.FromArgb(0x16, 0x17, 0x1B);
    internal static readonly Color LogBg     = Color.FromArgb(0x0F, 0x10, 0x13);
    internal static readonly Color LogText   = Color.FromArgb(0x86, 0xE0, 0xA8);

    internal static readonly string Family = "Segoe UI";
    internal static Font F(float size, FontStyle style = FontStyle.Regular) => new Font(Family, size, style, GraphicsUnit.Point);
    internal static Font Semibold(float size) => new Font("Segoe UI Semibold", size, FontStyle.Bold, GraphicsUnit.Point);

    internal static GraphicsPath Round(Rectangle r, int radius) {
      var d = radius * 2; var p = new GraphicsPath();
      if (d <= 0) { p.AddRectangle(r); return p; }
      p.AddArc(r.X, r.Y, d, d, 180, 90);
      p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
      p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
      p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
      p.CloseFigure();
      return p;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string sub, string idList);

    /// <summary>Give a DropDownList combo a fully dark look (owner-drawn items + dark drop button).</summary>
    internal static void DarkCombo(ComboBox cb) {
      cb.FlatStyle = FlatStyle.Flat; cb.BackColor = Input; cb.ForeColor = Text; cb.Font = F(9.5f);
      cb.DrawMode = DrawMode.OwnerDrawFixed;
      cb.DrawItem += (s, e) => {
        var box = (ComboBox) s;
        var sel = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(sel ? Accent : Input)) e.Graphics.FillRectangle(bg, e.Bounds);
        var txt = e.Index >= 0 ? box.GetItemText(box.Items[e.Index]) : box.Text;
        TextRenderer.DrawText(e.Graphics, txt, box.Font, e.Bounds, sel ? Color.White : Text,
          TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
      };
      cb.HandleCreated += (s, e) => { try { SetWindowTheme(cb.Handle, "DarkMode_CFD", null); } catch { } };
    }

    internal static void FillRound(Graphics g, Rectangle r, int radius, Color fill, Color? border = null) {
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using (var path = Round(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), radius)) {
        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        if (border.HasValue) using (var pen = new Pen(border.Value, 1f)) g.DrawPath(pen, path);
      }
    }
  }

  /// <summary>A rounded card surface.</summary>
  internal sealed class Card : Panel {
    internal int Radius = 12;
    internal Color Fill = Modern.Card;
    internal Color Stroke = Modern.Border;
    internal Card() { DoubleBuffered = true; BackColor = Modern.Bg; }
    protected override void OnPaint(PaintEventArgs e) {
      e.Graphics.Clear(Parent?.BackColor ?? Modern.Bg);
      Modern.FillRound(e.Graphics, ClientRectangle, Radius, Fill, Stroke);
      base.OnPaint(e);
    }
  }

  /// <summary>A flat filled pill button, owner-drawn, with a hover lift.</summary>
  internal sealed class PillButton : Button {
    internal Color Fill = Modern.Accent;
    internal Color TextColor = Color.White;
    internal int Radius = 9;
    private bool hover;
    internal PillButton() {
      FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
      FlatAppearance.MouseOverBackColor = Color.Transparent;
      FlatAppearance.MouseDownBackColor = Color.Transparent;
      SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
      BackColor = Modern.Bg; ForeColor = Color.White; Cursor = Cursors.Hand;
      MouseEnter += (s, e) => { hover = true; Invalidate(); };
      MouseLeave += (s, e) => { hover = false; Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics;
      g.Clear(Parent?.BackColor ?? Modern.Bg);
      var f = Enabled ? (hover ? Lighten(Fill, 0.12) : Fill) : Modern.CardHi;
      Modern.FillRound(g, ClientRectangle, Radius, f);
      TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? TextColor : Modern.Muted,
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
    private static Color Lighten(Color c, double f) => Color.FromArgb(c.A,
      (int) Math.Min(255, c.R + (255 - c.R) * f), (int) Math.Min(255, c.G + (255 - c.G) * f),
      (int) Math.Min(255, c.B + (255 - c.B) * f));
  }

  /// <summary>A borderless header nav button: quiet text that lifts to accent on hover.</summary>
  internal sealed class NavButton : Button {
    private bool hover;
    internal NavButton(string text) {
      Text = text;
      FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
      FlatAppearance.MouseOverBackColor = Color.Transparent;
      FlatAppearance.MouseDownBackColor = Color.Transparent;
      SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
      BackColor = Modern.Bg; Cursor = Cursors.Hand; AutoSize = false; Height = 30;
      Font = Modern.F(9.5f);
      MouseEnter += (s, e) => { hover = true; Invalidate(); };
      MouseLeave += (s, e) => { hover = false; Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics;
      g.Clear(Parent?.BackColor ?? Modern.Bg);
      if (hover) Modern.FillRound(g, ClientRectangle, 8, Modern.CardHi);
      TextRenderer.DrawText(g, Text, Font, ClientRectangle, hover ? Modern.Text : Modern.Muted,
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
  }

  /// <summary>A dark drop-down menu (context menu) matching the flat palette.</summary>
  internal static class DarkMenu {
    internal static ContextMenuStrip Create() {
      var m = new ContextMenuStrip { Renderer = new Renderer(), BackColor = Modern.Card, ForeColor = Modern.Text, Font = Modern.F(9.5f), ShowImageMargin = false };
      return m;
    }
    internal static ToolStripMenuItem Item(string text, EventHandler onClick = null, bool? check = null) {
      var it = new ToolStripMenuItem(text) { ForeColor = Modern.Text };
      if (check.HasValue) { it.CheckOnClick = true; it.Checked = check.Value; }
      if (onClick != null) it.Click += onClick;
      return it;
    }
    private sealed class Renderer : ToolStripProfessionalRenderer {
      internal Renderer() : base(new Cols()) { }
      protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
      protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
        e.TextColor = (e.Item.Selected || e.Item.Pressed) ? Color.White : Modern.Text;
        base.OnRenderItemText(e);
      }
      private sealed class Cols : ProfessionalColorTable {
        public Cols() { UseSystemColors = false; }
        public override Color MenuItemSelected => Modern.Accent;
        public override Color MenuItemSelectedGradientBegin => Modern.Accent;
        public override Color MenuItemSelectedGradientEnd => Modern.Accent;
        public override Color MenuItemBorder => Modern.Accent;
        public override Color MenuItemPressedGradientBegin => Modern.Accent;
        public override Color MenuItemPressedGradientEnd => Modern.Accent;
        public override Color ToolStripDropDownBackground => Modern.Card;
        public override Color ImageMarginGradientBegin => Modern.Card;
        public override Color ImageMarginGradientMiddle => Modern.Card;
        public override Color ImageMarginGradientEnd => Modern.Card;
        public override Color MenuBorder => Modern.Border;
        public override Color SeparatorDark => Modern.Border;
        public override Color SeparatorLight => Modern.Border;
      }
    }
  }

  /// <summary>A borderless text box on a rounded, bordered surface — a modern input.</summary>
  internal sealed class Field : Panel {
    internal readonly TextBox Box = new TextBox { BorderStyle = BorderStyle.None };
    internal Field() {
      DoubleBuffered = true; BackColor = Modern.Bg; Padding = new Padding(12, 0, 12, 0); Height = 38;
      Box.BackColor = Modern.Input; Box.ForeColor = Modern.Text; Box.Font = Modern.F(10.5f);
      Box.BorderStyle = BorderStyle.None; Box.Dock = DockStyle.Fill;
      var holder = new Panel { Dock = DockStyle.Fill, BackColor = Modern.Input, Padding = new Padding(0, 9, 0, 0) };
      holder.Controls.Add(Box);
      Controls.Add(holder);
    }
    protected override void OnPaint(PaintEventArgs e) {
      e.Graphics.Clear(Parent?.BackColor ?? Modern.Card);
      Modern.FillRound(e.Graphics, ClientRectangle, 8, Modern.Input, Modern.Border);
      base.OnPaint(e);
    }
  }
}
