// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    public partial class ProfileTestView : UserControl, IRefreshable
    {
        ProfileRecorder? _rec;
        bool _neutralChanged;
        AccelProfile? _curveDuringTest;

        public static bool Recording { get; private set; }

        public ProfileTestView()
        {
            InitializeComponent();
        }

        public void Refresh()
        {
            ShowReport(AppState.Current.LastReport);
        }

        // ------------------------------------------------------------------ gravação

        async void Start_Click(object sender, RoutedEventArgs e)
        {
            var st = AppState.Current;
            if (st.Dpi <= 0) { Ui.SetStatus(Status, "Configure a sua DPI na tela Sensibilidade primeiro.", false); return; }

            StartBtn.IsEnabled = false;
            _neutralChanged = false;
            _curveDuringTest = null;

            if (NeutralBox.IsChecked == true)
            {
                Ui.SetStatus(Status, "Desligando a sua curva durante o teste...", true);
                _neutralChanged = await DriverService.BeginNeutralAsync();
            }
            if (!_neutralChanged && DriverService.IsDriverActive && st.Applied != null && !st.Applied.IsNeutral && NeutralBox.IsChecked != true)
                _curveDuringTest = st.Applied; // a análise desfaz o efeito da curva

            try
            {
                _rec = new ProfileRecorder(st.Dpi);
            }
            catch (Exception ex)
            {
                Ui.SetStatus(Status, ex.Message, false);
                StartBtn.IsEnabled = true;
                if (_neutralChanged) await DriverService.EndNeutralAsync();
                return;
            }

            _rec.Tick += UpdateLive;
            Recording = true;
            LivePanel.Visibility = Visibility.Visible;
            StopBtn.IsEnabled = CancelBtn.IsEnabled = true;
            Ui.SetStatus(Status, _neutralChanged
                ? "Gravando. Sua curva foi desligada só durante o teste e volta no final."
                : "Gravando. Pode abrir o jogo.", true);
            UpdateLive();
            MainWindow.Instance?.UpdatePresence();
        }

        void UpdateLive()
        {
            if (_rec == null) return;
            bool inGame = _rec.InGame;
            GameDot.Fill = Ui.Res(inGame ? "Ok" : "Warn");
            GameText.Text = inGame ? "Jogo detectado: gravando" : "Aguardando o jogo (janela do jogo em foco e cursor escondido)";
            double prog = ProfileAnalyzer.Progress(_rec.MovingSeconds, _rec.LiveFlicks);
            Progress.Value = prog;
            LiveText.Text = $"Dados suficientes: {prog * 100:0}%  ·  {_rec.ActiveSeconds / 60:0.0} min jogando  ·  " +
                            $"{_rec.MovingSeconds / 60:0.0} min de mouse em movimento  ·  ~{_rec.LiveFlicks} flicks";
        }

        async void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (_rec == null) return;
            double prog = ProfileAnalyzer.Progress(_rec.MovingSeconds, _rec.LiveFlicks);
            if (prog < 1)
            {
                var r = MessageBox.Show(
                    $"Ainda não tem dados suficientes ({prog * 100:0}%). Com pouco dado a recomendação fica menos confiável.\n\nAnalisar assim mesmo?",
                    "Teste de Perfil", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }
            if (_rec.MovingSeconds < 20)
            {
                Ui.SetStatus(Status, "Quase nenhum movimento gravado no jogo. Jogue um pouco mais antes de terminar.", false);
                return;
            }

            _rec.Stop();
            var bins = _rec.Bins.ToList();
            double active = _rec.ActiveSeconds;
            await StopRecording();

            Ui.SetStatus(Status, "Analisando...", true);
            var st = AppState.Current;
            var curve = _curveDuringTest;
            var report = await Task.Run(() => ProfileAnalyzer.Analyze(bins, st.Dpi, active, curve, st.Game, st.Cm360));
            st.LastReport = report;
            AppState.Save();
            Ui.SetStatus(Status, "Pronto! Veja o resultado abaixo.", true);
            ShowReport(report);
        }

        async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            await StopRecording();
            Ui.SetStatus(Status, "Teste cancelado.", false);
        }

        async Task StopRecording()
        {
            if (_rec != null)
            {
                _rec.Tick -= UpdateLive;
                _rec.Dispose();
                _rec = null;
            }
            Recording = false;
            LivePanel.Visibility = Visibility.Collapsed;
            StopBtn.IsEnabled = CancelBtn.IsEnabled = false;
            StartBtn.IsEnabled = true;
            if (_neutralChanged)
            {
                await DriverService.EndNeutralAsync();
                _neutralChanged = false;
            }
            MainWindow.Instance?.UpdatePresence();
        }

        // ------------------------------------------------------------------ resultado

        void ShowReport(ProfileReport? r)
        {
            if (r == null || r.Recommended == null)
            {
                ResultCard.Visibility = Visibility.Collapsed;
                return;
            }
            ResultCard.Visibility = Visibility.Visible;
            ResultDate.Text = $"{r.Date:dd/MM/yyyy HH:mm} · {r.MovingSeconds / 60:0.0} min analisados";
            StyleText.Text = r.Style;
            InsightsText.Text = string.Join("\n", r.Insights.Select(i => "• " + i));
            RecTitle.Text = r.Recommended.Describe();
            WhyText.Text = string.Join("\n", r.RecommendationWhy.Select(i => "• " + i));

            var rec = r.Recommended;
            var applied = AppState.Current.Applied;
            Chart.SetCurves(s => rec.MultX(s), Math.Abs(rec.YxRatio - 1) > 1e-6 ? s => rec.MultY(s) : null,
                applied != null && CurveMath.CanPlot(applied.X) ? s => applied.MultX(s) * applied.SensMultiplier : null, 160);
            Chart.SetHistogram(ProfileAnalyzer.Edges, r.Histogram);
        }

        async void ApplyRec_Click(object sender, RoutedEventArgs e)
        {
            var st = AppState.Current;
            var rec = st.LastReport?.Recommended;
            if (rec == null) return;
            var p = rec.Clone();
            p.Dpi = st.Dpi;
            Ui.SetStatus(ResultStatus, "Aplicando...", true);
            var (ok, msg) = await DriverService.ApplyAsync(p);
            if (!ok) { Ui.SetStatus(ResultStatus, msg, false); return; }
            st.Profile = p.Clone();
            st.Applied = p.Clone();
            st.AddHistory("Teste de Perfil: " + p.Describe(), p);
            AppState.Save();
            Ui.SetStatus(ResultStatus, "Curva recomendada aplicada. Dica: faça a rotina de treino para acostumar.", true);
            ShowReport(st.LastReport);
        }

        void AB_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.Navigate("aim");

        void Edit_Click(object sender, RoutedEventArgs e)
        {
            var rec = AppState.Current.LastReport?.Recommended;
            if (rec == null) return;
            AppState.Current.Profile = rec.Clone();
            AppState.Save();
            MainWindow.Instance?.Navigate("curve");
        }

        void Train_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.Navigate("training");
    }
}
