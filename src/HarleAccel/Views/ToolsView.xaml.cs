// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    public partial class ToolsView : UserControl, IRefreshable
    {
        RawMouseListener? _listener;
        bool _neutralChanged;

        public ToolsView()
        {
            InitializeComponent();
        }

        public void Refresh() { }

        async System.Threading.Tasks.Task<bool> BeginAsync(TextBlock status)
        {
            if (_listener != null || ProfileTestView.Recording)
            {
                Ui.SetStatus(status, "Já tem uma medição (ou o Teste de Perfil) em andamento.", false);
                return false;
            }
            _neutralChanged = await DriverService.BeginNeutralAsync();
            try
            {
                _listener = new RawMouseListener();
                return true;
            }
            catch (Exception ex)
            {
                Ui.SetStatus(status, ex.Message, false);
                await EndAsync();
                return false;
            }
        }

        async System.Threading.Tasks.Task EndAsync()
        {
            _listener?.Dispose();
            _listener = null;
            if (_neutralChanged)
            {
                await DriverService.EndNeutralAsync();
                _neutralChanged = false;
            }
        }

        // ------------------------------------------------------------------ DPI real

        readonly List<double> _dpiTries = new();
        double _dpiMeasured;
        bool _dragging;
        long _sumX, _sumY;

        double DistanceCm => D5.IsChecked == true ? 5 : D20.IsChecked == true ? 20 : 10;

        async void DpiStart_Click(object sender, RoutedEventArgs e)
        {
            DpiResult.Text = "";
            DpiUse.Visibility = Visibility.Collapsed;
            if (!await BeginAsync(DpiResult)) return;

            DpiStart.IsEnabled = false;
            _dpiTries.Clear();
            _dragging = false;
            DpiLive.Text = $"Tentativa 1 de 3: segure o botão esquerdo e arraste {DistanceCm:0} cm";
            _listener!.Buttons += OnDpiButton;
            _listener.Moved += OnDpiMove;
        }

        void OnDpiMove(int dx, int dy, long ts)
        {
            if (!_dragging) return;
            _sumX += dx;
            _sumY += dy;
            double counts = Math.Sqrt((double)_sumX * _sumX + (double)_sumY * _sumY);
            DpiLive.Text = $"{counts:0} counts...";
        }

        async void OnDpiButton(bool down, bool up, long ts)
        {
            if (down)
            {
                _dragging = true;
                _sumX = _sumY = 0;
                return;
            }
            if (!up || !_dragging) return;
            _dragging = false;

            double counts = Math.Sqrt((double)_sumX * _sumX + (double)_sumY * _sumY);
            if (counts < 20)
            {
                DpiLive.Text = "Movimento muito curto. Tente de novo: segure e arraste a distância toda.";
                return;
            }
            double dpi = counts / (DistanceCm / SensMath.CmPerInch);
            _dpiTries.Add(dpi);

            if (_dpiTries.Count < 3)
            {
                DpiLive.Text = $"Tentativa {_dpiTries.Count}: {dpi:0} DPI. Agora a tentativa {_dpiTries.Count + 1} de 3.";
                return;
            }

            await Dispatcher.Yield(); // sai do callback do Raw Input antes de fechar o listener
            await EndAsync();
            DpiStart.IsEnabled = true;

            double avg = _dpiTries.Average();
            double spread = _dpiTries.Max() - _dpiTries.Min();
            _dpiMeasured = Math.Round(avg);
            int declared = AppState.Current.Dpi;
            double diff = declared > 0 ? (avg - declared) / declared * 100 : 0;
            DpiLive.Text = "Pronto!";

            string trust = spread / avg > 0.06
                ? "As tentativas variaram bastante. Vale repetir com mais cuidado (linha reta, distância exata)."
                : "As 3 tentativas ficaram parecidas, então a medida é confiável.";
            DpiResult.Text =
                $"DPI real: {avg:0} (tentativas: {string.Join(", ", _dpiTries.Select(d => d.ToString("0")))})\n" +
                $"DPI configurada no app: {declared} → diferença de {diff:+0.0;-0.0}%\n" + trust;
            DpiResult.Foreground = Ui.Res("Silver");
            DpiUse.Visibility = Math.Abs(diff) >= 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        void DpiUse_Click(object sender, RoutedEventArgs e)
        {
            var st = AppState.Current;
            st.Dpi = (int)_dpiMeasured;
            st.Profile.Dpi = st.Dpi;
            AppState.Save();
            DpiUse.Visibility = Visibility.Collapsed;
            DpiResult.Text += $"\nDPI atualizada para {st.Dpi}. Aplique a curva de novo na tela Curva para usar a DPI real.";
        }

        // ------------------------------------------------------------------ angle snapping

        readonly List<double> _chunkAngles = new();
        int _chunkX, _chunkY;
        double _px, _py;
        Polyline? _path;
        DispatcherTimer? _snapTimer;
        DateTime _snapEnd;
        const double ChunkCounts = 40;

        async void SnapStart_Click(object sender, RoutedEventArgs e)
        {
            SnapResult.Text = "";
            if (!await BeginAsync(SnapResult)) return;

            SnapStart.IsEnabled = false;
            _chunkAngles.Clear();
            _chunkX = _chunkY = 0;
            SnapCanvas.Children.Clear();
            _px = SnapCanvas.ActualWidth / 2;
            _py = SnapCanvas.ActualHeight / 2;
            _path = new Polyline { Stroke = Ui.Res("AccentHi"), StrokeThickness = 1.5 };
            _path.Points.Add(new Point(_px, _py));
            SnapCanvas.Children.Add(_path);

            _listener!.Moved += OnSnapMove;
            _snapEnd = DateTime.Now.AddSeconds(15);
            _snapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _snapTimer.Tick += async (_, _) =>
            {
                double left = (_snapEnd - DateTime.Now).TotalSeconds;
                SnapLive.Text = left > 0 ? $"Desenhe círculos... {left:0}s  ·  {_chunkAngles.Count} amostras" : "Analisando...";
                if (left <= 0)
                {
                    _snapTimer!.Stop();
                    await EndAsync();
                    SnapStart.IsEnabled = true;
                    SnapLive.Text = "";
                    ShowSnapResult();
                }
            };
            _snapTimer.Start();
        }

        void OnSnapMove(int dx, int dy, long ts)
        {
            _chunkX += dx;
            _chunkY += dy;
            if (Math.Sqrt((double)_chunkX * _chunkX + (double)_chunkY * _chunkY) >= ChunkCounts)
            {
                // ângulo até o eixo mais próximo (0° = reto no eixo, 45° = diagonal)
                double ang = Math.Atan2(Math.Abs(_chunkY), Math.Abs(_chunkX)) * 180 / Math.PI;
                _chunkAngles.Add(Math.Min(ang, 90 - ang));
                _chunkX = _chunkY = 0;
            }

            if (_path != null)
            {
                double w = SnapCanvas.ActualWidth, h = SnapCanvas.ActualHeight;
                _px = Math.Clamp(_px + dx * 0.15, 0, w);
                _py = Math.Clamp(_py + dy * 0.15, 0, h);
                _path.Points.Add(new Point(_px, _py));
                if (_path.Points.Count > 4000) _path.Points.RemoveAt(0);
            }
        }

        void ShowSnapResult()
        {
            int n = _chunkAngles.Count;
            if (n < 120)
            {
                SnapResult.Text = $"Inconclusivo: só {n} amostras. Faça círculos maiores e contínuos durante os 15 segundos.";
                SnapResult.Foreground = Ui.Res("Warn");
                return;
            }
            double exact = _chunkAngles.Count(a => a < 0.5) / (double)n;
            double near = _chunkAngles.Count(a => a < 3) / (double)n;

            string verdict;
            string brush;
            if (exact >= 0.06)
            {
                verdict = "Provável angle snapping LIGADO. Muitos trechos saíram perfeitamente retos no eixo, coisa que a mão sozinha quase nunca faz. Procure no software do mouse por “angle snapping”, “correção de ângulo” ou “straightening” e desligue.";
                brush = "Danger";
            }
            else if (exact >= 0.035)
            {
                verdict = "Suspeito: um pouco mais de trechos retos do que o normal. Confira o software do mouse e repita o teste.";
                brush = "Warn";
            }
            else
            {
                verdict = "Parece DESLIGADO. Seus movimentos saíram com ângulos naturais.";
                brush = "Ok";
            }
            SnapResult.Text = $"{verdict}\n({n} amostras · {exact * 100:0.0}% exatamente no eixo · {near * 100:0}% a menos de 3° do eixo. Sem snapping, o esperado é por volta de 1 a 2%.)";
            SnapResult.Foreground = Ui.Res(brush);
        }
    }
}
