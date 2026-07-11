using System;
using System.Drawing;
using System.Windows.Forms;

namespace Seedforger {

  /// <summary>
  /// A modal home for the settings that used to hide in the off-screen side panel
  /// (custom fingerprint, proxy). It borrows the existing group boxes — reparenting
  /// them in on open and putting them back on close — so every control, event
  /// handler and read/write path keeps working unchanged.
  /// </summary>
  internal sealed class AdvancedForm : Form {

    private readonly (Control ctl, Control parent, Point loc, AnchorStyles anchor)[] borrowed;

    internal AdvancedForm(params GroupBox[] boxes) {
      Text = "Advanced settings";
      FormBorderStyle = FormBorderStyle.FixedDialog;
      MaximizeBox = false; MinimizeBox = false;
      StartPosition = FormStartPosition.CenterParent;
      try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }

      var host = new Panel { Dock = DockStyle.Fill };
      Controls.Add(host);

      borrowed = new (Control, Control, Point, AnchorStyles)[boxes.Length];
      var top = 12; var maxRight = 360;
      for (var i = 0; i < boxes.Length; i++) {
        var gb = boxes[i];
        borrowed[i] = (gb, gb.Parent, gb.Location, gb.Anchor);
        gb.Anchor = AnchorStyles.Top | AnchorStyles.Left; // don't let it stretch
        gb.Parent = host;
        gb.Location = new Point(12, top);
        gb.Visible = true;
        gb.BringToFront();
        top += gb.Height + 12;                 // stack, using the box's own height
        maxRight = Math.Max(maxRight, gb.Right);
      }

      var close = new Button { Text = "Close", Width = 90, Height = 30, DialogResult = DialogResult.OK, Name = "GhostButton" };
      var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(10) };
      bar.Controls.Add(close);
      Controls.Add(bar);
      AcceptButton = close; CancelButton = close;

      var size = new Size(maxRight + 24, top + bar.Height + 4);
      MinimumSize = new Size(size.Width + 16, size.Height + 39); // outer size floor (border + caption)
      ClientSize = size;

      FormClosed += (s, e) => {
        foreach (var b in borrowed) { b.ctl.Parent = b.parent; b.ctl.Location = b.loc; b.ctl.Anchor = b.anchor; }
      };

      Theme.Apply(this);
      Localization.Apply(this);
    }
  }
}
