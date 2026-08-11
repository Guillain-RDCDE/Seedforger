using System.Drawing;
using System.Windows.Forms;

namespace Seedforger {

  /// <summary>
  /// A <see cref="TabControl"/> whose tab-strip background can follow a dark theme.
  /// Windows paints the strip itself and ignores <c>BackColor</c>, so after it
  /// paints we fill the strip band *minus* the tab rectangles (the owner-drawn tabs
  /// stay). This is the piece SetWindowTheme can't reach.
  /// </summary>
  internal sealed class DarkTabControl : TabControl {

    private const int WM_PAINT = 0x000F;

    internal bool DarkStrip { get; set; }
    internal Color StripColor { get; set; } = Color.FromArgb(0x1E, 0x20, 0x24);

    protected override void WndProc(ref Message m) {
      base.WndProc(ref m);
      if (m.Msg != WM_PAINT || !DarkStrip || !IsHandleCreated || TabCount == 0) return;
      try {
        using (var g = Graphics.FromHwnd(Handle))
        using (var brush = new SolidBrush(StripColor))
        using (var region = new Region(new Rectangle(0, 0, Width, DisplayRectangle.Top))) {
          for (var i = 0; i < TabCount; i++) region.Exclude(GetTabRect(i));
          g.FillRegion(brush, region);
        }
      }
      catch { /* transient handle/region issues during layout */ }
    }
  }
}
