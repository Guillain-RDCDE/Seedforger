using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Seedforger.Integrity.Figures {

  /// <summary>
  /// A tiny, dependency-free SVG chart builder. Enough to draw the papers' figures —
  /// axes, gridlines, polylines, bars, points, a legend — as self-contained SVG that
  /// GitHub renders inline. All numbers are formatted with the invariant culture so
  /// the output is byte-stable regardless of the machine locale.
  /// </summary>
  internal sealed class Svg {
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    // Palette shared with the Seedforger identity.
    public const string Ink = "#1f2328";
    public const string Axis = "#57606a";
    public const string Grid = "#d0d7de";
    public const string Green = "#2ea043";
    public const string Red = "#d1242f";
    public const string Blue = "#0969da";
    public const string Amber = "#bf8700";
    public const string Bg = "#ffffff";

    private readonly StringBuilder _b = new StringBuilder();
    public readonly int W, H;
    private readonly int _l, _r, _t, _bot; // plot-area margins
    private double _x0, _x1, _y0, _y1;      // data ranges

    public Svg(int w, int h, int left = 70, int right = 24, int top = 46, int bottom = 54) {
      W = w; H = h; _l = left; _r = right; _t = top; _bot = bottom;
      _b.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\" font-family=\"Segoe UI, Helvetica, Arial, sans-serif\">\n");
      Rect(0, 0, w, h, Bg, null, 0);
    }

    public void SetData(double x0, double x1, double y0, double y1) { _x0 = x0; _x1 = x1; _y0 = y0; _y1 = y1; }

    private double PX(double x) => _l + (x - _x0) / (_x1 - _x0) * (W - _l - _r);
    private double PY(double y) => (H - _bot) - (y - _y0) / (_y1 - _y0) * (H - _t - _bot);

    private static string F(double v) => v.ToString("0.###", C);

    public void Rect(double x, double y, double w, double h, string fill, string stroke, double sw) {
      _b.Append($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\"");
      if (fill != null) _b.Append($" fill=\"{fill}\""); else _b.Append(" fill=\"none\"");
      if (stroke != null) _b.Append($" stroke=\"{stroke}\" stroke-width=\"{F(sw)}\"");
      _b.Append("/>\n");
    }

    public void Title(string s) => Text(W / 2.0, 26, s, Ink, 17, "middle", true);
    public void XLabel(string s) => Text((_l + (W - _r)) / 2.0, H - 12, s, Axis, 13, "middle", false);
    public void YLabel(string s) {
      _b.Append($"<text x=\"18\" y=\"{F((_t + (H - _bot)) / 2.0)}\" fill=\"{Axis}\" font-size=\"13\" text-anchor=\"middle\" transform=\"rotate(-90 18 {F((_t + (H - _bot)) / 2.0)})\">{Esc(s)}</text>\n");
    }

    public void Text(double x, double y, string s, string fill, double size, string anchor, bool bold) {
      _b.Append($"<text x=\"{F(x)}\" y=\"{F(y)}\" fill=\"{fill}\" font-size=\"{F(size)}\" text-anchor=\"{anchor}\"");
      if (bold) _b.Append(" font-weight=\"600\"");
      _b.Append($">{Esc(s)}</text>\n");
    }

    public void AxesBox() {
      Rect(_l, _t, W - _l - _r, H - _t - _bot, null, Axis, 1.0);
    }

    /// <summary>Horizontal gridlines + y tick labels at the given data values.</summary>
    public void YTicks(IEnumerable<double> vals, Func<double, string> fmt) {
      foreach (var v in vals) {
        double y = PY(v);
        _b.Append($"<line x1=\"{F(_l)}\" y1=\"{F(y)}\" x2=\"{F(W - _r)}\" y2=\"{F(y)}\" stroke=\"{Grid}\" stroke-width=\"1\"/>\n");
        Text(_l - 8, y + 4, fmt(v), Axis, 12, "end", false);
      }
    }

    public void XTicks(IEnumerable<double> vals, Func<double, string> fmt) {
      foreach (var v in vals) {
        double x = PX(v);
        _b.Append($"<line x1=\"{F(x)}\" y1=\"{F(H - _bot)}\" x2=\"{F(x)}\" y2=\"{F(H - _bot + 5)}\" stroke=\"{Axis}\" stroke-width=\"1\"/>\n");
        Text(x, H - _bot + 19, fmt(v), Axis, 12, "middle", false);
      }
    }

    public void Polyline(IReadOnlyList<(double x, double y)> pts, string color, double width, bool dashed = false) {
      var sb = new StringBuilder();
      for (int i = 0; i < pts.Count; i++) sb.Append($"{F(PX(pts[i].x))},{F(PY(pts[i].y))} ");
      _b.Append($"<polyline points=\"{sb.ToString().Trim()}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{F(width)}\"");
      if (dashed) _b.Append(" stroke-dasharray=\"6 5\"");
      _b.Append(" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>\n");
    }

    public void Point(double x, double y, string color, double r) {
      _b.Append($"<circle cx=\"{F(PX(x))}\" cy=\"{F(PY(y))}\" r=\"{F(r)}\" fill=\"{color}\"/>\n");
    }

    public void Bar(double xData, double barPixW, double yData, string color) {
      double x = PX(xData) - barPixW / 2.0;
      double yTop = PY(yData);
      double yBase = PY(_y0);
      Rect(x, yTop, barPixW, yBase - yTop, color, null, 0);
    }

    /// <summary>A legend box at top-left of the plot area.</summary>
    public void Legend(IReadOnlyList<(string label, string color)> items) {
      double x = _l + 12, y = _t + 16;
      foreach (var it in items) {
        _b.Append($"<rect x=\"{F(x)}\" y=\"{F(y - 9)}\" width=\"13\" height=\"13\" rx=\"2\" fill=\"{it.color}\"/>\n");
        Text(x + 19, y + 2, it.label, Ink, 12.5, "start", false);
        y += 20;
      }
    }

    private static string Esc(string s) =>
      s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public string End() { _b.Append("</svg>\n"); return _b.ToString(); }
  }
}
