// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    public partial class TrainingView : UserControl, IRefreshable
    {
        bool _loading = true;
        Routine? _routine;

        public TrainingView()
        {
            InitializeComponent();
            PlatformBox.ItemsSource = TrainingLibrary.Platforms;
            LevelBox.ItemsSource = TrainingLibrary.Levels;
            StyleBox.ItemsSource = TrainingLibrary.Styles.Concat(new[] { "Completa" }).ToArray();
            PlatformBox.SelectedIndex = 0; // Aim Lab (grátis) por padrão
            LevelBox.SelectedIndex = 0;
            _loading = false;
        }

        public void Refresh()
        {
            _loading = true;
            var rep = AppState.Current.LastReport;
            if (StyleBox.SelectedIndex < 0)
                StyleBox.SelectedItem = rep?.Style ?? "Híbrido";
            StyleHint.Text = rep != null
                ? $"Seu Teste de Perfil deu {rep.Style}, por isso esse é o foco sugerido."
                : "Faça o Teste de Perfil para o app escolher o foco certo para você. Sem ele, use Híbrido.";
            _loading = false;
            ShowRoutine();
            BuildEvolution();
        }

        void Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            ShowRoutine();
        }

        void ShowRoutine()
        {
            string platform = PlatformBox.SelectedItem as string ?? "Aim Lab";
            string level = LevelBox.SelectedItem as string ?? "Iniciante";
            string style = StyleBox.SelectedItem as string ?? "Híbrido";
            _routine = TrainingLibrary.Find(platform, level, style);
            BlocksPanel.Children.Clear();
            Status.Text = "";

            if (_routine == null)
            {
                RoutineTitle.Text = "Rotina não encontrada";
                RoutineDesc.Text = "";
                RoutineMinutes.Text = "";
                StartBtn.IsEnabled = false;
                CodeBtn.Visibility = LinkBtn.Visibility = Visibility.Collapsed;
                return;
            }

            RoutineTitle.Text = _routine.Title;
            RoutineDesc.Text = _routine.Description + (platform == "KovaaK's" ? " (O KovaaK's é pago. O Aim Lab é grátis e tem rotina equivalente.)" : "");
            RoutineMinutes.Text = $"{_routine.Minutes} min";
            CreditText.Text = "Crédito: " + _routine.Credit;
            StartBtn.IsEnabled = true;
            CodeBtn.Visibility = _routine.ShareCode != null ? Visibility.Visible : Visibility.Collapsed;
            LinkBtn.Visibility = _routine.Link != null ? Visibility.Visible : Visibility.Collapsed;

            int i = 1;
            foreach (var b in _routine.Blocks)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var num = Ui.Text(i++.ToString(), 15, "AccentHi", FontWeights.Bold);
                var info = new StackPanel();
                info.Children.Add(Ui.Text($"{b.Scenario}", 14, "Silver", FontWeights.SemiBold));
                info.Children.Add(Ui.Text($"{b.Category} · {b.Minutes} min (≈ {b.Minutes} runs) · {b.Focus}", 12, "Muted"));
                var copy = new Button { Content = "Copiar nome", Style = (Style)FindResource("Ghost"), Tag = b.Scenario, VerticalAlignment = VerticalAlignment.Center };
                copy.Click += (s, _) => { try { Clipboard.SetText((string)((Button)s).Tag); ((Button)s).Content = "Copiado"; } catch { } };

                Grid.SetColumn(info, 1);
                Grid.SetColumn(copy, 2);
                row.Children.Add(num);
                row.Children.Add(info);
                if (_routine.Style != "Completa") row.Children.Add(copy);
                BlocksPanel.Children.Add(row);
            }
        }

        void Start_Click(object sender, RoutedEventArgs e)
        {
            if (_routine == null) return;
            if (_routine.Style == "Completa")
            {
                Status.Text = "A rotina completa do Voltaic segue a ordem da playlist dentro do aim trainer. Use o código ou o link acima.";
                return;
            }
            var w = new TrainingTimerWindow(_routine) { Owner = Window.GetWindow(this) };
            MainWindow.Instance?.UpdatePresence("Treinando: " + _routine.Title);
            w.ShowDialog();
            MainWindow.Instance?.UpdatePresence();

            if (w.Session != null && w.Session.Scores.Count > 0)
            {
                AppState.Current.Training.Add(w.Session);
                AppState.Save();
                Ui.SetStatus(Status, $"Treino salvo: {w.Session.Scores.Count} cenário(s) com score.", true);
            }
            else if (w.Finished) Ui.SetStatus(Status, "Treino concluído!", true);
            BuildEvolution();
        }

        void Code_Click(object sender, RoutedEventArgs e)
        {
            if (_routine?.ShareCode == null) return;
            try { Clipboard.SetText(_routine.ShareCode); Ui.SetStatus(Status, $"Código {_routine.ShareCode} copiado. No KovaaK's, cole na aba Online Playlist.", true); } catch { }
        }

        void Link_Click(object sender, RoutedEventArgs e)
        {
            if (_routine?.Link == null) return;
            if (!SafeLinks.Open(_routine.Link)) Ui.SetStatus(Status, "Esse link não é de um site conhecido, então o app não abriu por segurança.", false);
        }

        void BuildEvolution()
        {
            EvolutionPanel.Children.Clear();
            var sessions = AppState.Current.Training;
            if (sessions.Count == 0)
            {
                EvolutionPanel.Children.Add(Ui.Text("Nenhum treino registrado. Ao fim de cada bloco, anote o seu score para acompanhar a evolução.", 13, "Muted"));
                return;
            }

            EvolutionPanel.Children.Add(Ui.Text($"{sessions.Count} treino(s) · último em {sessions[^1].Date:dd/MM/yyyy}", 13, "Muted"));
            var byScenario = sessions.SelectMany(s => s.Scores.Select(x => (s.Date, x.Scenario, x.Score)))
                                     .GroupBy(x => x.Scenario)
                                     .Where(g => g.Count() >= 1)
                                     .OrderByDescending(g => g.Max(x => x.Date));
            foreach (var g in byScenario.Take(12))
            {
                var list = g.OrderBy(x => x.Date).ToList();
                double first = list[0].Score, last = list[^1].Score;
                string trend = list.Count < 2 ? "1 registro"
                    : first > 0 ? $"{(last - first) / first * 100:+0;-0}% desde o primeiro" : "";
                var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                sp.Children.Add(Ui.Text(g.Key, 13, "Silver", FontWeights.SemiBold));
                sp.Children.Add(Ui.Text(string.Join("  →  ", list.TakeLast(6).Select(x => x.Score.ToString("0"))) + $"   ({trend})", 12, "AccentHi"));
                EvolutionPanel.Children.Add(sp);
            }
        }
    }
}
