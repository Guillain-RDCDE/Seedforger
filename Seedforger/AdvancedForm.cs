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
      FormBorderStyle = FormBorderStyle.Sizable;
      MaximizeBox = false; MinimizeBox = false;
      StartPosition = FormStartPosition.CenterParent;
      // The borrowed group boxes are already sized in the host's DPI pixels; don't
      // let this form re-scale them, or the content gets clipped on hi-DPI screens.
      AutoScaleMode = AutoScaleMode.None;
      try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }

      // AutoScroll so a box that's wider/taller than the dialog is reachable rather
      // than clipped off the right edge (the old fixed-size dialog cut content off).
      var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
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
        // Account for anything a child pokes past the box's own right edge.
        var boxRight = gb.Right;
        foreach (Control child in gb.Controls) boxRight = Math.Max(boxRight, gb.Left + child.Right);
        maxRight = Math.Max(maxRight, boxRight);
      }

      var close = new Button { Text = "Close", Width = 90, Height = 30, DialogResult = DialogResult.OK, Name = "GhostButton" };
      var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(10) };
      bar.Controls.Add(close);
      Controls.Add(bar);
      AcceptButton = close; CancelButton = close;

      // +24 padding, +18 so a vertical scrollbar never overlaps the content.
      ClientSize = new Size(maxRight + 24 + 18, top + bar.Height + 4);
      MinimumSize = new Size(340, 240);

      FormClosed += (s, e) => {
        foreach (var b in borrowed) { b.ctl.Parent = b.parent; b.ctl.Location = b.loc; b.ctl.Anchor = b.anchor; }
      };

      Theme.Apply(this);
      Localization.Apply(this);
    }
  }
}
