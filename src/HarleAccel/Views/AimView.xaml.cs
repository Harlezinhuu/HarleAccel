// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    public partial class AimView : UserControl, IRefreshable
    {
        bool _loading = true;
        readonly Random _rnd = new();

        // estado do A/B
        bool _aIsOne;
        readonly List<AimResult> _one = new(), _two = new();
        AccelProfile? _a, _b;

        public AimView()
        {
            InitializeComponent();
            _loading = false;
        }

        /// <summary>Identifica a curva (ignorando o nome do perfil).</summary>
        public static string CurveKey(AccelProfile p)
        {
            var c = p.Clone();
            c.Name = "k";
            return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(RawAccelJson.Build(c))))[..12];
        }

        public void Refresh()
        {
            _loading = true;
            var st = AppState.Current;
            AText.Text = CurrentA().Describe();
            if (BSource.SelectedIndex < 0) BSource.SelectedIndex = st.LastReport?.Recommended != null ? 0 : 1;
            _loading = false;
            UpdateB();
            BuildResults();
        }

        /// <summary>A curva que está valendo agora (sem driver ativo, nada está valendo).</summary>
        static AccelProfile CurrentA() =>
            DriverService.IsDriverActive && AppState.Current.Applied != null
                ? AppState.Current.Applied.Clone()
                : AccelProfile.Neutral(AppState.Current.Dpi);

        AccelProfile? SelectedB()
        {
            var st = AppState.Current;
            return BSource.SelectedIndex switch
            {
                0 => st.LastReport?.Recommended?.Clone(),
                1 => st.Profile.Clone(),
                _ => AccelProfile.Neutral(st.Dpi),
            };
        }

        void BSource_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            UpdateB();
        }

        void UpdateB()
        {
            var b = SelectedB();
            BText.Text = b == null ? "Faça o Teste de Perfil primeiro para ter uma curva recomendada." : b.Describe();
        }

        // ------------------------------------------------------------------ A/B

        async void ABStart_Click(object sender, RoutedEventArgs e)
        {
            var st = AppState.Current;
            _a = CurrentA();
            _b = SelectedB();
            if (_b == null) { Ui.SetStatus(ABStatus, "Escolha a curva B (o Teste de Perfil ainda não foi feito).", false); return; }
            _a.Dpi = _b.Dpi = st.Dpi;
            if (CurveKey(_a) == CurveKey(_b)) { Ui.SetStatus(ABStatus, "A e B são a mesma curva. Escolha outra para a B.", false); return; }
            if (DriverService.Status != DriverStatus.Active) { Ui.SetStatus(ABStatus, "O teste A/B troca a curva entre as rodadas, então precisa do driver ativo.", false); return; }

            ABStart.IsEnabled = false;
            ChoicePanel.Visibility = RevealPanel.Visibility = Visibility.Collapsed;
            _one.Clear(); _two.Clear();
            _aIsOne = _rnd.Next(2) == 0;
            int first = _rnd.Next(1, 3);
            int[] order = first == 1 ? new[] { 1, 2, 1, 2 } : new[] { 2, 1, 2, 1 };

            for (int i = 0; i < order.Length; i++)
            {
                int blind = order[i];
                var curve = (blind == 1) == _aIsOne ? _a : _b;
                Ui.SetStatus(ABStatus, $"Preparando a rodada {i + 1} de 4...", true);
                var (ok, msg) = await DriverService.ApplyAsync(curve);
                if (!ok) { await RestoreA(); Ui.SetStatus(ABStatus, msg, false); ABStart.IsEnabled = true; return; }

                var w = new AimTestWindow(30, $"Rodada {i + 1} de 4  ·  Curva {(blind == 1 ? "①" : "②")}");
                w.ShowDialog();
                if (w.Result == null)
                {
                    await RestoreA();
                    Ui.SetStatus(ABStatus, "Teste A/B cancelado. Sua curva atual foi mantida.", false);
                    ABStart.IsEnabled = true;
                    return;
                }
                w.Result.Kind = "A/B";
                w.Result.Curve = curve.Describe();
                w.Result.CurveKey = CurveKey(curve);
                (blind == 1 ? _one : _two).Add(w.Result);
                st.AddAim(w.Result);
            }

            await RestoreA();
            AppState.Save();
            ABStart.IsEnabled = true;
            ABStatus.Text = "";
            ChoicePanel.Visibility = Visibility.Visible;
        }

        async Task RestoreA()
        {
            if (_a != null) await DriverService.ApplyAsync(_a);
        }

        void Choose1_Click(object sender, RoutedEventArgs e) => Reveal(1);
        void Choose2_Click(object sender, RoutedEventArgs e) => Reveal(2);
        void Choose0_Click(object sender, RoutedEventArgs e) => Reveal(0);

        void Reveal(int choice)
        {
            ChoicePanel.Visibility = Visibility.Collapsed;
            RevealPanel.Visibility = Visibility.Visible;

            double avg1 = _one.Count > 0 ? _one.Average(r => r.Score) : 0;
            double avg2 = _two.Count > 0 ? _two.Average(r => r.Score) : 0;
            double acc1 = _one.Count > 0 ? _one.Average(r => r.Accuracy) : 0;
            double acc2 = _two.Count > 0 ? _two.Average(r => r.Accuracy) : 0;
            string nameOne = _aIsOne ? "A (atual)" : "B";
            string nameTwo = _aIsOne ? "B" : "A (atual)";

            string pick = choice == 0 ? "Você não sentiu diferença."
                : $"Você preferiu a Curva {(choice == 1 ? "①" : "②")}, que era a {(choice == 1 ? nameOne : nameTwo)}.";
            int better = avg1 > avg2 * 1.03 ? 1 : avg2 > avg1 * 1.03 ? 2 : 0;
            string numbers = better == 0 ? "Pelos números, as duas ficaram praticamente empatadas (diferença menor que 3%)."
                : $"Pelos números, a Curva {(better == 1 ? "①" : "②")} ({(better == 1 ? nameOne : nameTwo)}) foi melhor.";

            RevealText.Text =
                $"{pick}\n" +
                $"Curva ① = {nameOne}: {avg1:0} pts em média, {acc1 * 100:0}% de precisão\n" +
                $"Curva ② = {nameTwo}: {avg2:0} pts em média, {acc2 * 100:0}% de precisão\n" +
                numbers +
                (choice != 0 && better != 0 && choice != better
                    ? "\nSua sensação e os números discordaram. Isso é normal com curva nova: a mão ainda não acostumou. Vale repetir daqui a uns dias de treino."
                    : "");
        }

        async void KeepA_Click(object sender, RoutedEventArgs e)
        {
            Ui.SetStatus(ABStatus, "Mantendo a curva atual.", true);
            RevealPanel.Visibility = Visibility.Collapsed;
            await RestoreA();
        }

        async void KeepB_Click(object sender, RoutedEventArgs e)
        {
            if (_b == null) return;
            var (ok, msg) = await DriverService.ApplyAsync(_b);
            if (!ok) { Ui.SetStatus(ABStatus, msg, false); return; }
            var st = AppState.Current;
            st.Applied = _b.Clone();
            st.Profile = _b.Clone();
            st.AddHistory("Teste A/B: " + _b.Describe(), _b);
            AppState.Save();
            RevealPanel.Visibility = Visibility.Collapsed;
            AText.Text = _b.Describe();
            Ui.SetStatus(ABStatus, "Curva B aplicada! Use a rotina de treino nos próximos dias para acostumar.", true);
        }

        // ------------------------------------------------------------------ antes e depois

        void Single_Click(object sender, RoutedEventArgs e)
        {
            if (SystemCheck.IsEnhancePointerPrecisionOn() == true)
                Ui.SetStatus(SingleStatus, "Aviso: a precisão do ponteiro do Windows está ligada e vai interferir.", false);

            var cur = CurrentA();
            var w = new AimTestWindow(45, "Aim test · 45 segundos");
            w.ShowDialog();
            if (w.Result == null) { Ui.SetStatus(SingleStatus, "Cancelado.", false); return; }

            w.Result.Kind = "teste";
            w.Result.Curve = cur.Describe();
            w.Result.CurveKey = CurveKey(cur);
            AppState.Current.AddAim(w.Result);
            AppState.Save();
            Ui.SetStatus(SingleStatus, $"{w.Result.Score:0} pts · {w.Result.Hits} acertos · {w.Result.Accuracy * 100:0}% de precisão · {w.Result.AvgMsPerHit:0} ms por alvo", true);
            BuildResults();
        }

        void BuildResults()
        {
            ResultsList.Children.Clear();
            var tests = AppState.Current.AimResults.Where(r => r.Kind == "teste").ToList();
            if (tests.Count == 0)
            {
                CompareText.Text = "";
                ResultsList.Children.Add(Ui.Text("Nenhum aim test ainda.", 13, "Muted"));
                return;
            }

            var groups = tests.GroupBy(r => r.CurveKey)
                              .Select(g => new
                              {
                                  Curve = g.Last().Curve,
                                  Last = g.Max(r => r.Date),
                                  First = g.Min(r => r.Date),
                                  N = g.Count(),
                                  Avg = g.Average(r => r.Score),
                                  Best = g.Max(r => r.Score),
                                  Acc = g.Average(r => r.Accuracy),
                              })
                              .OrderBy(g => g.First)
                              .ToList();

            if (groups.Count >= 2)
            {
                var before = groups[^2];
                var after = groups[^1];
                double diff = before.Avg > 0 ? (after.Avg - before.Avg) / before.Avg * 100 : 0;
                CompareText.Text =
                    $"Antes ({before.Curve}): {before.Avg:0} pts\n" +
                    $"Depois ({after.Curve}): {after.Avg:0} pts\n" +
                    (diff >= 0 ? $"Você melhorou {diff:0}%." : $"Caiu {Math.Abs(diff):0}%. Normal nos primeiros dias com a curva nova; continue treinando e teste de novo.");
            }
            else CompareText.Text = "Faça testes com outra curva para ver o antes e depois.";

            foreach (var g in groups.AsEnumerable().Reverse())
            {
                var card = new Border
                {
                    Background = Ui.Res("Panel"),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 8),
                };
                var sp = new StackPanel();
                sp.Children.Add(Ui.Text(g.Curve, 13, "Silver", FontWeights.SemiBold));
                sp.Children.Add(Ui.Text($"{g.N} teste(s) · média {g.Avg:0} pts · melhor {g.Best:0} pts · precisão {g.Acc * 100:0}% · último em {g.Last:dd/MM HH:mm}", 12, "Muted"));
                card.Child = sp;
                ResultsList.Children.Add(card);
            }
        }
    }
}
