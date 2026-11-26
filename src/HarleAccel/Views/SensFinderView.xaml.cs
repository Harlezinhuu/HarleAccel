// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    public partial class SensFinderView : UserControl, IRefreshable
    {
        readonly Random _rnd = new();
        SensFinder? _finder;
        AccelProfile? _base;          // curva da pessoa (o teste só multiplica a sens dela)
        double _baseSens;             // sens no jogo quando o teste começou
        Game _game = Games.All[0];
        bool _oneIsLow;
        AimResult? _r1, _r2;
        readonly List<(double Factor, AimResult Result)> _runs = new();
        double _finalFactor = 1;
        bool _running;

        public static bool Running { get; private set; }

        public SensFinderView()
        {
            InitializeComponent();
        }

        public void Refresh()
        {
            if (_running) return;
            var st = AppState.Current;
            BaseText.Text = $"{st.Game.Name} · sens {AssistantTools.FormatSens(st.Game, st.Sens)} · {st.Dpi} DPI · {Ui.N(st.Cm360, "0.#")} cm/360";
            bool ok = DriverService.Status == DriverStatus.Active;
            StartBtn.IsEnabled = ok;
            if (!ok) Ui.SetStatus(Status, "O buscador muda a sens pelo driver durante o teste, então precisa do driver ativo (Assistente).", false);
            else if (SystemCheck.IsEnhancePointerPrecisionOn() == true) Ui.SetStatus(Status, "Desligue o “Aumentar precisão do ponteiro” do Windows antes (tela Início), senão o resultado sai torto.", false);
            else Status.Text = "";
        }

        // ------------------------------------------------------------------ fluxo

        async void Start_Click(object sender, RoutedEventArgs e)
        {
            var st = AppState.Current;
            if (DriverService.Status != DriverStatus.Active) return;
            if (ProfileTestView.Recording) { Ui.SetStatus(Status, "Termine o Teste de Perfil antes.", false); return; }

            _finder = new SensFinder();
            _base = (st.Applied ?? AccelProfile.Neutral(st.Dpi)).Clone();
            _base.Dpi = st.Dpi;
            _baseSens = st.Sens;
            _game = st.Game;
            _runs.Clear();

            // marca que o driver está com uma mudança temporária: se o app fechar, ele devolve tudo
            st.NeutralActive = true;
            AppState.Save();

            _running = Running = true;
            StartBtn.IsEnabled = false;
            CancelBtn.Visibility = Visibility.Visible;
            ResultCard.Visibility = Visibility.Collapsed;
            RoundCard.Visibility = Visibility.Visible;
            Status.Text = "";
            await PlayRound();
        }

        async Task PlayRound()
        {
            var f = _finder!;
            RoundTitle.Text = $"Rodada {f.Round + 1} de {SensFinder.Rounds}";
            RoundRange.Text = $"testando {Sens(f.Low)} e {Sens(f.High)}";
            RoundProgress.Value = f.Round;
            ChoicePanel.Visibility = RevealPanel.Visibility = Visibility.Collapsed;

            _oneIsLow = _rnd.Next(2) == 0;
            double one = _oneIsLow ? f.Low : f.High;
            double two = _oneIsLow ? f.High : f.Low;

            _r1 = await Play(one, $"Rodada {f.Round + 1} de {SensFinder.Rounds}  ·  Versão ①");
            if (_r1 == null) { await Stop("Cancelado. Sua sens e a sua curva voltaram ao normal."); return; }
            _r2 = await Play(two, $"Rodada {f.Round + 1} de {SensFinder.Rounds}  ·  Versão ②");
            if (_r2 == null) { await Stop("Cancelado. Sua sens e a sua curva voltaram ao normal."); return; }

            _runs.Add((one, _r1));
            _runs.Add((two, _r2));
            ChoicePanel.Visibility = Visibility.Visible;
        }

        /// <summary>Aplica a sens multiplicada e roda um aim test de 15 s.</summary>
        async Task<AimResult?> Play(double factor, string title)
        {
            var p = _base!.Clone();
            p.SensMultiplier = (_base.SensMultiplier > 0 ? _base.SensMultiplier : 1) * factor;
            Ui.SetStatus(Status, "Preparando...", true);
            var (ok, msg) = await DriverService.ApplyAsync(p);
            if (!ok) { Ui.SetStatus(Status, msg, false); return null; }
            Status.Text = "";

            var w = new AimTestWindow(15, title);
            w.ShowDialog();
            return w.Result;
        }

        void Pick1_Click(object sender, RoutedEventArgs e) => Pick(_oneIsLow ? -1 : +1, "①");
        void Pick2_Click(object sender, RoutedEventArgs e) => Pick(_oneIsLow ? +1 : -1, "②");
        void Pick0_Click(object sender, RoutedEventArgs e) => Pick(0, null);

        void Pick(int choice, string? label)
        {
            var f = _finder!;
            double low = f.Low, high = f.High;
            var lowR = _oneIsLow ? _r1! : _r2!;
            var highR = _oneIsLow ? _r2! : _r1!;

            f.Choose(choice);
            ChoicePanel.Visibility = Visibility.Collapsed;
            RevealPanel.Visibility = Visibility.Visible;

            string picked = choice == 0 ? "Você achou iguais, então o meio continua."
                : $"Você escolheu a {label}, que era a mais {(choice < 0 ? "BAIXA" : "ALTA")}.";
            RevealText.Text =
                $"{picked}\n" +
                $"Mais baixa ({Sens(low)}): {lowR.Score:0} pts, {lowR.Accuracy * 100:0}% de precisão{Miss(lowR)}\n" +
                $"Mais alta ({Sens(high)}): {highR.Score:0} pts, {highR.Accuracy * 100:0}% de precisão{Miss(highR)}";

            if (f.Done)
            {
                NextBtn.Content = "Ver o resultado";
            }
            else
            {
                NextBtn.Content = "Próxima rodada";
                RoundRange.Text = $"próxima: {Sens(f.Low)} e {Sens(f.High)}";
            }
            RoundProgress.Value = f.Round;
        }

        static string Miss(AimResult r)
        {
            int over = r.NearOver + r.FarOver, under = r.NearUnder + r.FarUnder;
            int n = over + under + r.NearDirect + r.FarDirect;
            return n < 4 ? "" : $", passou do alvo {100.0 * over / n:0}% / parou antes {100.0 * under / n:0}%";
        }

        async void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_finder == null) return;
            if (!_finder.Done) { await PlayRound(); return; }
            await Finish();
        }

        async Task Finish()
        {
            var f = _finder!;
            _finalFactor = f.Center;
            await Stop(null);

            var (lo, hi) = f.Range;
            ResultBig.Text = Sens(f.Center);
            ResultText.Text =
                $"A sua faixa boa no {_game.Name} vai de {Sens(lo)} a {Sens(hi)}. O centro é {Sens(f.Center)}" +
                $" ({Ui.N(SensMath.Cm360(AppState.Current.Dpi, _baseSens * f.Center, _game), "0.#")} cm/360).\n" +
                (Math.Abs(f.Center - 1) < 0.03
                    ? "Isso é praticamente a sens que você já usa. Ela já estava boa!"
                    : $"Isso é {Math.Abs(f.Center - 1) * 100:0}% {(f.Center > 1 ? "mais alta" : "mais baixa")} do que a sua sens atual ({AssistantTools.FormatSens(_game, _baseSens)}).");

            // diagnóstico com as 4 últimas partidas (as mais próximas do resultado)
            var last = AimDiagnosis.Sum(_runs.TakeLast(4).Select(r => r.Result));
            ResultDiag.Text = string.Join("\n", AimDiagnosis.Explain(last, _base != null && !_base.IsNeutral));

            UseStatus.Text = "";
            UseBtn.IsEnabled = Math.Abs(f.Center - 1) >= 0.005;
            ResultCard.Visibility = Visibility.Visible;
            RoundCard.Visibility = Visibility.Collapsed;
        }

        async void Cancel_Click(object sender, RoutedEventArgs e) =>
            await Stop("Cancelado. Sua sens e a sua curva voltaram ao normal.");

        async Task Stop(string? message)
        {
            await DriverService.EndNeutralAsync(); // devolve a curva e a sens originais
            _running = Running = false;
            StartBtn.IsEnabled = DriverService.Status == DriverStatus.Active;
            CancelBtn.Visibility = Visibility.Collapsed;
            if (message != null)
            {
                RoundCard.Visibility = Visibility.Collapsed;
                Ui.SetStatus(Status, message, false);
            }
        }

        // ------------------------------------------------------------------ resultado

        string Sens(double factor) => AssistantTools.FormatSens(_game, _baseSens * factor);

        void Use_Click(object sender, RoutedEventArgs e)
        {
            var st = AppState.Current;
            double s = _baseSens * _finalFactor;
            st.GameName = _game.Name;
            st.Sens = Math.Round(s, AssistantTools.IsFortnite(_game) ? 2 : 3);
            AppState.Save();
            UseBtn.IsEnabled = false;
            Ui.SetStatus(UseStatus,
                $"Pronto! Agora mude a sens dentro do {_game.Name} para {AssistantTools.FormatSens(_game, st.Sens)}. " +
                "A curva continua igual: ela já usa essa sens como base.", true);
        }

        void Converter_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.Navigate("sens");
    }
}
