// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace HarleAccel.Controls
{
    /// <summary>
    /// Gráfico da curva: eixo X = velocidade da mão (cm/s), eixo Y = multiplicador de sensibilidade.
    /// Desenhado na mão (sem biblioteca) para ficar leve e com a cara do app.
    /// </summary>
    public sealed class CurveChart : FrameworkElement
    {
        Func<double, double>? _fx;
        Func<double, double>? _fy;
        Func<double, double>? _compare; // curva anterior (para comparar)
        double _maxSpeed = 120;
        Point? _mouse;
        string? _message;
        double[]? _histEdges, _hist;

        public event Action<double?>? HoverSpeedChanged;

        // zonas aproximadas de velocidade da mão
        public const double MicroMax = 8;   // cm/s
        public const double TrackMax = 40;  // cm/s

        static readonly Brush BgBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xB3, 0x07, 0x0F, 0x24)));
        static readonly Brush GridBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x24, 0x6F, 0xB8, 0xFF)));
        static readonly Brush AxisText = Freeze(new SolidColorBrush(Color.FromRgb(0x93, 0xA3, 0xC8)));
        static readonly Brush ZoneMicro = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0x5B, 0xE3, 0xA8)));
        static readonly Brush ZoneTrack = Freeze(new SolidColorBrush(Color.FromArgb(0x10, 0x6F, 0xB8, 0xFF)));
        static readonly Brush ZoneFlick = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0x8A, 0x3D)));
        static readonly Pen XPen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(0x6F, 0xB8, 0xFF)), 2.5) { LineJoin = PenLineJoin.Round });
        static readonly Pen YPen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(0xE4, 0xEC, 0xFF)), 1.6) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) });
        static readonly Pen CmpPen = FreezePen(new Pen(new SolidColorBrush(Color.FromArgb(0x99, 0x93, 0xA3, 0xC8)), 1.5) { DashStyle = DashStyles.Dot });
        static readonly Pen GridPen = FreezePen(new Pen(GridBrush, 1));
        static readonly Pen GlowPen = FreezePen(new Pen(new SolidColorBrush(Color.FromArgb(0x33, 0x6F, 0xB8, 0xFF)), 9) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
        static readonly Pen HoverPen = FreezePen(new Pen(new SolidColorBrush(Color.FromArgb(0x99, 0xD5, 0xDC, 0xE6)), 1) { DashStyle = DashStyles.Dash });

        static Brush Freeze(Brush b) { b.Freeze(); return b; }
        static Pen FreezePen(Pen p) { p.Freeze(); return p; }

        public CurveChart()
        {
            ClipToBounds = true;
            Cursor = Cursors.Cross;
        }

        public void SetCurves(Func<double, double> fx, Func<double, double>? fy, Func<double, double>? compare, double maxSpeed = 120)
        {
            _fx = fx; _fy = fy; _compare = compare; _maxSpeed = maxSpeed; _message = null;
            InvalidateVisual();
        }

        /// <summary>Mostra, por baixo da curva, onde a sua mão passa o tempo (do Teste de Perfil).</summary>
        public void SetHistogram(double[]? edges, double[]? fractions)
        {
            _histEdges = edges; _hist = fractions;
            InvalidateVisual();
        }

        static readonly Brush HistBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0x6F, 0xB8, 0xFF)));

        public void SetMessage(string msg)
        {
            _message = msg; _fx = null; _fy = null; _compare = null;
            InvalidateVisual();
        }

        Rect PlotRect => new(48, 12, Math.Max(10, ActualWidth - 60), Math.Max(10, ActualHeight - 44));

        double MaxY()
        {
            double max = 1.5;
            foreach (var f in new[] { _fx, _fy, _compare })
            {
                if (f == null) continue;
                for (int i = 1; i <= 60; i++)
                {
                    double v = f(_maxSpeed * i / 60.0);
                    if (!double.IsNaN(v) && !double.IsInfinity(v)) max = Math.Max(max, v);
                }
            }
            return Math.Ceiling(max * 1.15 * 4) / 4; // arredonda para 0.25
        }

        protected override void OnRender(DrawingContext dc)
        {
            var r = PlotRect;
            dc.DrawRectangle(BgBrush, new Pen(GridBrush, 1), new Rect(0.5, 0.5, Math.Max(0, ActualWidth - 1), Math.Max(0, ActualHeight - 1)));

            if (_message != null)
            {
                var t = Text(_message, 14, AxisText);
                t.MaxTextWidth = Math.Max(50, ActualWidth - 60);
                t.TextAlignment = TextAlignment.Center;
                dc.DrawText(t, new Point(30, ActualHeight / 2 - t.Height / 2));
                return;
            }
            if (_fx == null) return;

            double yMin = 0, yMax = MaxY();
            double X(double s) => r.Left + s / _maxSpeed * r.Width;
            double Y(double m) => r.Bottom - (m - yMin) / (yMax - yMin) * r.Height;

            // zonas
            dc.DrawRectangle(ZoneMicro, null, new Rect(X(0), r.Top, X(MicroMax) - X(0), r.Height));
            dc.DrawRectangle(ZoneTrack, null, new Rect(X(MicroMax), r.Top, X(TrackMax) - X(MicroMax), r.Height));
            dc.DrawRectangle(ZoneFlick, null, new Rect(X(TrackMax), r.Top, r.Right - X(TrackMax), r.Height));
            DrawLabel(dc, "microajuste", X(0) + 4, r.Top + 4);
            DrawLabel(dc, "tracking / controle", X(MicroMax) + 6, r.Top + 4);
            DrawLabel(dc, "flick", X(TrackMax) + 6, r.Top + 4);

            // grade
            double yStep = yMax <= 2 ? 0.25 : yMax <= 4 ? 0.5 : 1;
            for (double m = 0; m <= yMax + 1e-9; m += yStep)
            {
                dc.DrawLine(GridPen, new Point(r.Left, Y(m)), new Point(r.Right, Y(m)));
                var t = Text(m.ToString("0.##", CultureInfo.InvariantCulture) + "x", 11, AxisText);
                dc.DrawText(t, new Point(r.Left - t.Width - 6, Y(m) - t.Height / 2));
            }
            double xStep = _maxSpeed <= 80 ? 10 : 20;
            for (double s = 0; s <= _maxSpeed + 1e-9; s += xStep)
            {
                dc.DrawLine(GridPen, new Point(X(s), r.Top), new Point(X(s), r.Bottom));
                var t = Text(s.ToString("0", CultureInfo.InvariantCulture), 11, AxisText);
                dc.DrawText(t, new Point(X(s) - t.Width / 2, r.Bottom + 4));
            }
            var axis = Text("velocidade da mão (cm/s)", 11, AxisText);
            dc.DrawText(axis, new Point(r.Left + r.Width / 2 - axis.Width / 2, r.Bottom + 20));

            // histograma do Teste de Perfil
            if (_hist != null && _histEdges != null && _hist.Length == _histEdges.Length - 1)
            {
                double hmax = 0;
                for (int i = 0; i < _hist.Length; i++)
                {
                    double w = _histEdges[i + 1] - _histEdges[i];
                    if (w > 0) hmax = Math.Max(hmax, _hist[i] / w);
                }
                if (hmax > 0)
                {
                    for (int i = 0; i < _hist.Length; i++)
                    {
                        double a = _histEdges[i], b = Math.Min(_histEdges[i + 1], _maxSpeed);
                        if (a >= _maxSpeed) break;
                        double dens = _hist[i] / (_histEdges[i + 1] - _histEdges[i]) / hmax;
                        double h = dens * r.Height * 0.35;
                        dc.DrawRectangle(HistBrush, null, new Rect(X(a) + 1, r.Bottom - h, Math.Max(1, X(b) - X(a) - 2), h));
                    }
                    DrawLabel(dc, "barras = onde a sua mão passa o tempo (Teste de Perfil)", r.Left + 6, r.Bottom - 36);
                }
            }

            // curvas
            if (_compare != null) DrawCurve(dc, _compare, CmpPen, X, Y, yMax);
            if (_fy != null) DrawCurve(dc, _fy, YPen, X, Y, yMax);
            DrawCurve(dc, _fx, GlowPen, X, Y, yMax); // brilho de holograma por baixo
            DrawCurve(dc, _fx, XPen, X, Y, yMax);

            // legenda
            double lx = r.Right - 150, ly = r.Bottom - 20;
            if (_fy != null)
            {
                dc.DrawLine(YPen, new Point(lx, ly), new Point(lx + 18, ly));
                dc.DrawText(Text("vertical", 11, AxisText), new Point(lx + 22, ly - 8));
                lx += 75;
            }
            dc.DrawLine(XPen, new Point(lx, ly), new Point(lx + 18, ly));
            dc.DrawText(Text(_fy != null ? "horizontal" : "curva", 11, AxisText), new Point(lx + 22, ly - 8));
            if (_compare != null)
            {
                dc.DrawLine(CmpPen, new Point(r.Left + 10, ly), new Point(r.Left + 28, ly));
                dc.DrawText(Text("curva aplicada agora", 11, AxisText), new Point(r.Left + 32, ly - 8));
            }

            // hover
            if (_mouse is Point mp && r.Contains(mp))
            {
                double s = (mp.X - r.Left) / r.Width * _maxSpeed;
                dc.DrawLine(HoverPen, new Point(mp.X, r.Top), new Point(mp.X, r.Bottom));
                double mx = _fx(s);
                if (!double.IsNaN(mx))
                {
                    dc.DrawEllipse(Brushes.White, XPen, new Point(mp.X, Y(Math.Min(mx, yMax))), 5, 5);
                    var tip = Text($"{s:0} cm/s → {mx.ToString("0.00", CultureInfo.InvariantCulture)}x", 12, Brushes.White);
                    double tx = Math.Min(mp.X + 10, r.Right - tip.Width - 14);
                    double ty = Math.Max(r.Top + 22, Y(Math.Min(mx, yMax)) - 34);
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xE6, 0x08, 0x12, 0x30)), new Pen(XPen.Brush, 1),
                        new Rect(tx - 6, ty - 4, tip.Width + 12, tip.Height + 8));
                    dc.DrawText(tip, new Point(tx, ty));
                }
            }
        }

        void DrawCurve(DrawingContext dc, Func<double, double> f, Pen pen,
                       Func<double, double> X, Func<double, double> Y, double yMax)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                bool started = false;
                const int N = 240;
                for (int i = 0; i <= N; i++)
                {
                    // começa um pouco acima de 0 (a curva personalizada não é definida em 0)
                    double s = Math.Max(0.05, i / (double)N * _maxSpeed);
                    double m = f(s);
                    if (double.IsNaN(m) || double.IsInfinity(m)) { started = false; continue; }
                    var p = new Point(X(s), Y(Math.Min(m, yMax * 1.05)));
                    if (!started) { ctx.BeginFigure(p, false, false); started = true; }
                    else ctx.LineTo(p, true, true);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        static Typeface? _typeface;
        static Typeface ChartTypeface => _typeface ??= new Typeface(
            Application.Current?.TryFindResource("DataFont") as FontFamily ?? new FontFamily("Consolas"),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        void DrawLabel(DrawingContext dc, string text, double x, double y) =>
            dc.DrawText(Text(text, 10, AxisText), new Point(x, y));

        FormattedText Text(string s, double size, Brush b) =>
            new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, ChartTypeface, size, b,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            _mouse = e.GetPosition(this);
            var r = PlotRect;
            HoverSpeedChanged?.Invoke(r.Contains(_mouse.Value) ? (_mouse.Value.X - r.Left) / r.Width * _maxSpeed : null);
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            _mouse = null;
            HoverSpeedChanged?.Invoke(null);
            InvalidateVisual();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            InvalidateVisual();
        }
    }
}
