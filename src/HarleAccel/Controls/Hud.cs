// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace HarleAccel.Controls
{
    /// <summary>
    /// Visual HUD (no estilo da F.R.I.D.A.Y): cantoneiras nos cantos e linhas de scan bem fracas.
    /// Liga com Hud.Brackets="True" (o estilo CardBorder já liga em todos os cartões).
    /// </summary>
    public static class Hud
    {
        public static readonly DependencyProperty BracketsProperty =
            DependencyProperty.RegisterAttached("Brackets", typeof(bool), typeof(Hud),
                new PropertyMetadata(false, OnBracketsChanged));

        public static bool GetBrackets(DependencyObject o) => (bool)o.GetValue(BracketsProperty);
        public static void SetBrackets(DependencyObject o, bool v) => o.SetValue(BracketsProperty, v);

        static void OnBracketsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement fe || e.NewValue is not true) return;
            fe.Loaded += (_, _) => Attach(fe);
            fe.Unloaded += (_, _) => Detach(fe);
            if (fe.IsLoaded) Attach(fe);
        }

        static void Detach(FrameworkElement fe)
        {
            var layer = AdornerLayer.GetAdornerLayer(fe);
            var existing = layer?.GetAdorners(fe);
            if (existing == null) return;
            foreach (var a in existing)
                if (a is BracketAdorner) layer!.Remove(a);
        }

        static void Attach(FrameworkElement fe)
        {
            var layer = AdornerLayer.GetAdornerLayer(fe);
            if (layer == null) return;
            var existing = layer.GetAdorners(fe);
            if (existing != null)
                foreach (var a in existing)
                    if (a is BracketAdorner) return;
            layer.Add(new BracketAdorner(fe));
        }

        sealed class BracketAdorner : Adorner
        {
            static readonly Pen BracketPen;
            static readonly Pen GlowPen;
            static readonly Brush ScanBrush;

            static BracketAdorner()
            {
                var holo = Color.FromRgb(0x8C, 0xCF, 0xFF);
                BracketPen = new Pen(new SolidColorBrush(holo), 2) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square };
                BracketPen.Freeze();
                GlowPen = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0x8C, 0xCF, 0xFF)), 6);
                GlowPen.Freeze();

                // linhas de scan: 1px a cada 3px, quase invisível (igual o .scan da FRIDAY)
                var line = new GeometryDrawing(new SolidColorBrush(Color.FromArgb(0x0B, 0x8C, 0xCF, 0xFF)), null, new RectangleGeometry(new Rect(0, 2, 1, 1)));
                var tile = new DrawingBrush(line)
                {
                    TileMode = TileMode.Tile,
                    Viewport = new Rect(0, 0, 1, 3),
                    ViewportUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(0, 0, 1, 3),
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Stretch = Stretch.None,
                };
                tile.Freeze();
                ScanBrush = tile;
            }

            public BracketAdorner(UIElement el) : base(el)
            {
                IsHitTestVisible = false;
                // cartão escondido = cantoneiras escondidas também
                Visibility = el.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                el.IsVisibleChanged += (_, e) => Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }

            protected override void OnRender(DrawingContext dc)
            {
                var s = AdornedElement.RenderSize;
                if (s.Width < 20 || s.Height < 20) return;

                dc.DrawRectangle(ScanBrush, null, new Rect(1, 1, s.Width - 2, s.Height - 2));

                const double L = 14;
                // canto de cima à esquerda
                var tl = new StreamGeometry();
                using (var g = tl.Open())
                {
                    g.BeginFigure(new Point(0, L), false, false);
                    g.LineTo(new Point(0, 0), true, false);
                    g.LineTo(new Point(L, 0), true, false);
                }
                tl.Freeze();
                // canto de baixo à direita
                var br = new StreamGeometry();
                using (var g = br.Open())
                {
                    g.BeginFigure(new Point(s.Width - L, s.Height), false, false);
                    g.LineTo(new Point(s.Width, s.Height), true, false);
                    g.LineTo(new Point(s.Width, s.Height - L), true, false);
                }
                br.Freeze();

                dc.DrawGeometry(null, GlowPen, tl);
                dc.DrawGeometry(null, GlowPen, br);
                dc.DrawGeometry(null, BracketPen, tl);
                dc.DrawGeometry(null, BracketPen, br);
            }
        }
    }
}
