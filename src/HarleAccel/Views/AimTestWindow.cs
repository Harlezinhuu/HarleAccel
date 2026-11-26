// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    /// <summary>
    /// Mini aim test em tela cheia: alvos aparecem em distâncias variadas (perto = microajuste, longe = flick).
    /// Usa o cursor do Windows, que também passa pela curva do Raw Accel.
    /// </summary>
    public sealed class AimTestWindow : Window
    {
        readonly Canvas _canvas = new() { Background = Brushes.Transparent };
        readonly Ellipse _target;
        readonly TextBlock _hud, _big;
        readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
        readonly Random _rnd = new();
        readonly double _seconds;
        readonly Stopwatch _clock = new();
        readonly Stopwatch _targetClock = new();

        const double Radius = 15;
        Point _center;

        // caminho do mouse até o alvo atual (para o diagnóstico de passou/parou antes)
        readonly System.Collections.Generic.List<AimDiagnosis.Sample> _path = new();
        Point _start;
        readonly AimResult _counts = new();
        int _hits, _misses;
        double _sumHitMs;
        bool _running;
        double _countdown = 3;

        public AimResult? Result { get; private set; }

        public AimTestWindow(double seconds, string title)
        {
            _seconds = seconds;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Topmost = true;
            Background = (Brush)Application.Current.FindResource("Bg");
            FontFamily = (FontFamily)Application.Current.FindResource("UiFont");
            Cursor = Cursors.Cross;
            ShowInTaskbar = false;

            _target = new Ellipse
            {
                Width = Radius * 2,
                Height = Radius * 2,
                Fill = new SolidColorBrush(Color.FromArgb(0x66, 0x6F, 0xB8, 0xFF)),
                Stroke = new SolidColorBrush(Color.FromRgb(0x6F, 0xB8, 0xFF)),
                StrokeThickness = 2,
                Effect = (System.Windows.Media.Effects.Effect)Application.Current.FindResource("Glow"),
                Visibility = Visibility.Hidden,
                IsHitTestVisible = false,
            };
            _hud = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xE4, 0xEC, 0xFF)),
                FontFamily = (FontFamily)Application.Current.FindResource("DataFont"),
                FontSize = 15,
                Margin = new Thickness(24),
                IsHitTestVisible = false,
            };
            _big = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xE4, 0xEC, 0xFF)),
                FontFamily = (FontFamily)Application.Current.FindResource("DisplayFont"),
                Effect = (System.Windows.Media.Effects.Effect)Application.Current.FindResource("Glow"),
                FontSize = 64,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
            };

            var grid = new Grid();
            grid.Children.Add(_canvas);
            _canvas.Children.Add(_target);
            grid.Children.Add(_hud);
            var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
            center.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromRgb(0x83, 0x91, 0xAB)),
                FontSize = 18,
                TextAlignment = TextAlignment.Center,
            });
            center.Children.Add(_big);
            center.Children.Add(new TextBlock
            {
                Text = "Clique nos alvos o mais rápido que conseguir, sem errar. Esc cancela.",
                Foreground = new SolidColorBrush(Color.FromRgb(0x83, 0x91, 0xAB)),
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
            });
            grid.Children.Add(center);
            Content = grid;
            _centerPanel = center;

            _timer.Tick += OnTick;
            Loaded += (_, _) => { _clock.Start(); _timer.Start(); };
            PreviewMouseLeftButtonDown += OnClick;
            PreviewMouseMove += (_, e) =>
            {
                if (!_running) return;
                var p = e.GetPosition(_canvas);
                _path.Add(new AimDiagnosis.Sample(_targetClock.Elapsed.TotalSeconds, p.X, p.Y));
                if (_path.Count > 4000) _path.RemoveAt(0);
            };
            KeyDown += (_, e) => { if (e.Key == Key.Escape) { Result = null; Close(); } };
            Closed += (_, _) => _timer.Stop();
        }

        readonly StackPanel _centerPanel;

        void OnTick(object? sender, EventArgs e)
        {
            double t = _clock.Elapsed.TotalSeconds;
            if (!_running)
            {
                double left = _countdown - t;
                if (left > 0)
                {
                    _big.Text = Math.Ceiling(left).ToString();
                    return;
                }
                _running = true;
                _centerPanel.Visibility = Visibility.Collapsed;
                _clock.Restart();
                Spawn(new Point(ActualWidth / 2, ActualHeight / 2));
                return;
            }

            double remaining = _seconds - t;
            _hud.Text = $"{Math.Max(0, remaining):0.0}s   ·   acertos {_hits}   ·   erros {_misses}";
            if (remaining <= 0) Finish();
        }

        void Spawn(Point from)
        {
            double w = ActualWidth, h = ActualHeight;
            double margin = 80;
            double maxFar = Math.Min(w, h) * 0.45;
            for (int i = 0; i < 30; i++)
            {
                // metade perto (microajuste), metade longe (flick)
                double dist = _rnd.NextDouble() < 0.5 ? 50 + _rnd.NextDouble() * 170 : 260 + _rnd.NextDouble() * Math.Max(40, maxFar - 260);
                double ang = _rnd.NextDouble() * Math.PI * 2;
                var p = new Point(from.X + Math.Cos(ang) * dist, from.Y + Math.Sin(ang) * dist);
                if (p.X > margin && p.X < w - margin && p.Y > margin + 40 && p.Y < h - margin)
                {
                    _center = p;
                    break;
                }
                _center = new Point(margin + _rnd.NextDouble() * (w - 2 * margin), margin + 40 + _rnd.NextDouble() * (h - 2 * margin - 40));
            }
            Canvas.SetLeft(_target, _center.X - Radius);
            Canvas.SetTop(_target, _center.Y - Radius);
            _target.Visibility = Visibility.Visible;
            _targetClock.Restart();
            _start = IsLoaded ? Mouse.GetPosition(_canvas) : from;
            _path.Clear();
            _path.Add(new AimDiagnosis.Sample(0, _start.X, _start.Y));
        }

        void OnClick(object sender, MouseButtonEventArgs e)
        {
            if (!_running) return;
            var p = e.GetPosition(_canvas);
            if ((p - _center).Length <= Radius + 1)
            {
                _hits++;
                _sumHitMs += _targetClock.Elapsed.TotalMilliseconds;
                double dist = (_center - _start).Length;
                var approach = AimDiagnosis.Classify(_path, _start.X, _start.Y, _center.X, _center.Y, Radius);
                AimDiagnosis.Count(_counts, approach, dist < AimDiagnosis.NearMaxPx);
                Spawn(_center);
            }
            else _misses++;
        }

        void Finish()
        {
            _timer.Stop();
            _running = false;
            Result = new AimResult
            {
                Hits = _hits,
                Misses = _misses,
                Seconds = _seconds,
                AvgMsPerHit = _hits > 0 ? _sumHitMs / _hits : 0,
                NearOver = _counts.NearOver, NearUnder = _counts.NearUnder, NearDirect = _counts.NearDirect,
                FarOver = _counts.FarOver, FarUnder = _counts.FarUnder, FarDirect = _counts.FarDirect,
            };
            Close();
        }
    }
}
