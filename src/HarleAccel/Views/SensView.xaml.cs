// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    public partial class SensView : UserControl, IRefreshable
    {
        bool _ready;

        public SensView()
        {
            InitializeComponent();
            GameBox.ItemsSource = Games.All;
            _ready = true;
        }

        public void Refresh()
        {
            _ready = false;
            var st = AppState.Current;
            GameBox.SelectedItem = st.Game;
            SensBox.Text = Ui.N(st.Sens, "0.####");
            DpiBox.Text = st.Dpi.ToString();
            SaveStatus.Text = "";
            _ready = true;
            Recalc();
        }

        Game SelectedGame => GameBox.SelectedItem as Game ?? Games.All[0];

        void Input_Changed(object sender, EventArgs e)
        {
            if (!_ready) return;
            SaveStatus.Text = "";
            Recalc();
        }

        void Recalc()
        {
            var game = SelectedGame;
            SensLabel.Text = "SENSIBILIDADE NO JOGO (" + game.SensHint + ")";

            bool okSens = Ui.TryNum(SensBox.Text, out double sens) && sens > 0;
            bool okDpi = Ui.TryNum(DpiBox.Text, out double dpi) && dpi > 0;

            ConvertGrid.Children.Clear();
            ConvertGrid.RowDefinitions.Clear();
            ConvertGrid.ColumnDefinitions.Clear();

            if (!okSens || !okDpi)
            {
                CmText.Text = EdpiText.Text = "—";
                ClassText.Text = "—";
                ClassDetail.Text = "Preencha a sensibilidade e a DPI.";
                CurveInfo.Text = "—";
                Target_Changed(this, EventArgs.Empty);
                return;
            }

            double cm = SensMath.Cm360(dpi, sens, game);
            CmText.Text = Ui.N(cm, "0.#") + " cm";
            // eDPI do Fortnite é calculado com o valor decimal (8% = 0.08)
            double edpiSens = game.Name.StartsWith("Fortnite") ? sens / 100.0 : sens;
            EdpiText.Text = Ui.N(SensMath.EDpi(dpi, edpiSens), "0.#");

            var (label, detail) = SensMath.Classify(cm, game.Kind);
            ClassText.Text = label;
            ClassDetail.Text = detail + " (faixa aproximada, não é regra.)";

            CurveInfo.Text = DescribeWithCurve(cm);
            BuildConvertTable(sens, dpi, game);
            Target_Changed(this, EventArgs.Empty);
        }

        static string DescribeWithCurve(double baseCm)
        {
            var p = AppState.Current.Applied ?? AppState.Current.Profile;
            if (p.X.Style == CurveStyle.None && !p.SeparateY && Math.Abs(p.YxRatio - 1) < 1e-6 && Math.Abs(p.SensMultiplier - 1) < 1e-6)
                return "Sem aceleração: o cm/360 é o mesmo em qualquer velocidade.";

            string Line(double speed, string name)
            {
                double mx = p.MultX(speed) * p.SensMultiplier;
                return $"{name} ({speed:0} cm/s): {Ui.N(baseCm / mx, "0.#")} cm/360";
            }

            string s = string.Join("\n", Line(5, "Devagar"), Line(25, "Controle"), Line(60, "Flick"));

            double my = p.MultY(5) * p.SensMultiplier;
            if (Math.Abs(my - p.MultX(5) * p.SensMultiplier) > 1e-3)
                s += $"\nVertical devagar: {Ui.N(baseCm / my, "0.#")} cm/360";

            s += "\n(equivalente naquele instante; com aceleração o cm/360 muda com a velocidade)";
            return s;
        }

        void BuildConvertTable(double sens, double dpi, Game from)
        {
            ConvertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            ConvertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ConvertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ConvertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            int row = 0;
            void AddRow(UIElement a, UIElement b, UIElement c, UIElement? d = null)
            {
                ConvertGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                foreach (var (el, col) in new[] { (a, 0), (b, 1), (c, 2) })
                {
                    Grid.SetRow(el, row); Grid.SetColumn(el, col);
                    if (el is FrameworkElement fe) fe.Margin = new Thickness(0, 6, 0, 6);
                    ConvertGrid.Children.Add(el);
                }
                if (d != null) { Grid.SetRow(d, row); Grid.SetColumn(d, 3); ConvertGrid.Children.Add(d); }
                row++;
            }

            AddRow(Ui.Text("JOGO", 11, "Muted", FontWeights.SemiBold),
                   Ui.Text("SENSIBILIDADE", 11, "Muted", FontWeights.SemiBold),
                   Ui.Text("eDPI", 11, "Muted", FontWeights.SemiBold));

            foreach (var g in Games.All.Where(g => g != from))
            {
                double conv = SensMath.Convert(sens, from, g);
                double edpi = g.Name.StartsWith("Fortnite") ? dpi * conv / 100.0 : dpi * conv;
                string sensStr = Ui.N(conv, g.Name.StartsWith("Fortnite") ? "0.0#" : "0.###") + (g.Name.StartsWith("Fortnite") ? " %" : "");

                var copy = new Button { Content = "Copiar", Style = (Style)FindResource("Ghost"), Tag = Ui.N(conv, "0.###") };
                copy.Click += (s, e) =>
                {
                    try { Clipboard.SetText((string)((Button)s).Tag); ((Button)s).Content = "Copiado"; } catch { }
                };

                AddRow(Ui.Text(g.Name, 14),
                       Ui.Text(sensStr, 14, "AccentHi", FontWeights.SemiBold),
                       Ui.Text(Ui.N(edpi, "0.#"), 13, "Muted"),
                       copy);
            }
        }

        void Target_Changed(object sender, EventArgs e)
        {
            if (!_ready || TargetResult == null) return;
            if (!Ui.TryNum(TargetCmBox.Text, out double cm) || cm <= 0 ||
                !Ui.TryNum(DpiBox.Text, out double dpi) || dpi <= 0)
            {
                TargetResult.Text = "digite um valor";
                return;
            }
            var g = SelectedGame;
            double s = SensMath.SensForCm360(cm, dpi, g);
            TargetResult.Text = $"{g.Name}: {Ui.N(s, "0.###")}{(g.Name.StartsWith("Fortnite") ? " %" : "")}";
        }

        void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!Ui.TryNum(SensBox.Text, out double sens) || sens <= 0 ||
                !Ui.TryNum(DpiBox.Text, out double dpi) || dpi <= 0 || dpi > 64000)
            {
                Ui.SetStatus(SaveStatus, "Confira a sensibilidade e a DPI.", false);
                return;
            }

            var st = AppState.Current;
            st.Sens = sens;
            st.Dpi = (int)Math.Round(dpi);
            st.GameName = SelectedGame.Name;
            st.Profile.Dpi = st.Dpi;
            AppState.Save();

            bool needsReapply = st.Applied != null && st.Applied.Dpi != st.Dpi;
            Ui.SetStatus(SaveStatus, needsReapply
                ? "Base salva. A DPI mudou: aplique a curva de novo na tela Curva."
                : "Base salva.", !needsReapply);
        }
    }
}
