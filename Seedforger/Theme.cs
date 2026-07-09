using System;
using System.Drawing;
using System.Windows.Forms;

namespace Seedforger {

  /// <summary>
  /// Conservative, dependency-free modern restyle for the WinForms UI.
  ///
  /// Deliberately NON-invasive: it only switches the app font to Segoe UI, gives
  /// buttons a flat accent look, and tidies the log box, menu and status bar.
  /// It does NOT recursively repaint container/label/groupbox backgrounds - doing
  /// that on this hand-placed legacy layout made nested tab pages and transparent
  /// labels vanish. Modernizes the feel without breaking control visibility.
  /// </summary>
  internal static class Theme {

    private static readonly Color Accent = Color.FromArgb(0x2F, 0x6F, 0xED);
    private static readonly Color AccentText = Color.White;
    private static readonly Color Window = Color.FromArgb(0xF5, 0xF6, 0xF8);
    private static readonly Color LogBack = Color.FromArgb(0x1E, 0x21, 0x27);
    private static readonly Color LogText = Color.FromArgb(0xD7, 0xDB, 0xE2);

    internal static readonly Font UiFont =
      new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>Apply the modern restyle to a form and its children.</summary>
    internal static void Apply(Form form) {
      if (!AppOptions.ThemingEnabled) return;
      form.SuspendLayout();
      form.Font = UiFont;
      form.BackColor = Window;
      StyleTree(form);
      form.ResumeLayout(true);
    }

    /// <summary>Apply the modern restyle to a stand-alone control subtree.</summary>
    internal static void ApplyTo(Control root) {
      if (!AppOptions.ThemingEnabled) return;
      root.SuspendLayout();
      root.Font = UiFont;
      StyleTree(root);
      root.ResumeLayout(true);
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
          b.BackColor = Accent;
          b.ForeColor = AccentText;
          b.FlatAppearance.MouseOverBackColor = Lighten(Accent, 0.12);
          b.FlatAppearance.MouseDownBackColor = Darken(Accent, 0.12);
          b.UseVisualStyleBackColor = false;
          break;
        case RichTextBox rtb:
          rtb.BorderStyle = BorderStyle.FixedSingle;
          rtb.BackColor = LogBack;
          rtb.ForeColor = LogText;
          break;
        case MenuStrip ms:
          ms.Renderer = new ToolStripProfessionalRenderer(new AccentColors());
          break;
        case StatusStrip ss:
          ss.Renderer = new ToolStripProfessionalRenderer(new AccentColors());
          break;
        case ContextMenuStrip cms:
          cms.Renderer = new ToolStripProfessionalRenderer(new AccentColors());
          break;
      }
    }

    private sealed class AccentColors : ProfessionalColorTable {
      public AccentColors() { UseSystemColors = false; }
      public override Color MenuItemSelected => Accent;
      public override Color MenuItemSelectedGradientBegin => Accent;
      public override Color MenuItemSelectedGradientEnd => Accent;
      public override Color MenuItemBorder => Accent;
      public override Color MenuItemPressedGradientBegin => Lighten(Accent, 0.20);
      public override Color MenuItemPressedGradientEnd => Lighten(Accent, 0.20);
    }

    private static Color Lighten(Color c, double f) => Color.FromArgb(c.A,
      (int) Math.Min(255, c.R + 255 * f), (int) Math.Min(255, c.G + 255 * f),
      (int) Math.Min(255, c.B + 255 * f));

    private static Color Darken(Color c, double f) => Color.FromArgb(c.A,
      (int) Math.Max(0, c.R - 255 * f), (int) Math.Max(0, c.G - 255 * f),
      (int) Math.Max(0, c.B - 255 * f));
  }
}
