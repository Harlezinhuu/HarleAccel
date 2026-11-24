// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System.Windows;
using System.Windows.Controls;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    public partial class HomeView : UserControl, IRefreshable
    {
        public HomeView()
        {
            InitializeComponent();
        }

        public void Refresh()
        {
            var st = AppState.Current;

            // driver
            var ds = DriverService.Status;
            DriverTitle.Text = ds switch
            {
                DriverStatus.Active => "Ativo",
                DriverStatus.NeedsRestart => "Reinicie o PC",
                DriverStatus.NotInstalled => "Não instalado",
                _ => "Não baixado",
            };
            DriverTitle.Foreground = Ui.Res(ds == DriverStatus.Active ? "Ok" : "Warn");
            DriverText.Text = DriverService.StatusText(ds);

            // aceleração do Windows
            bool? epp = SystemCheck.IsEnhancePointerPrecisionOn();
            EppTitle.Text = epp switch { true => "Ligada", false => "Desligada", _ => "Não consegui ler" };
            EppTitle.Foreground = Ui.Res(epp == false ? "Ok" : "Warn");
            EppText.Text = epp == true
                ? "“Aumentar precisão do ponteiro” está ligado. Ele soma uma aceleração do Windows por cima da sua curva e bagunça a mira no desktop e em jogos que usam o cursor."
                : "Tudo certo: só a sua curva mexe na aceleração.";
            EppBtn.Visibility = epp == true ? Visibility.Visible : Visibility.Collapsed;

            // base
            bool pct = st.Game.Name.StartsWith("Fortnite");
            BaseTitle.Text = $"{st.Game.Name} · {Ui.N(st.Sens, "0.###")}{(pct ? " %" : "")}";
            BaseText.Text = $"{st.Dpi} DPI · {Ui.N(st.Cm360, "0.#")} cm/360 · eDPI {Ui.N(st.Dpi * (pct ? st.Sens / 100 : st.Sens), "0.#")}";

            // curva
            if (st.Applied == null)
            {
                CurveTitle.Text = "Nenhuma curva aplicada";
                CurveText.Text = "Monte a sua curva e aplique no driver.";
            }
            else
            {
                CurveTitle.Text = st.Applied.Describe();
                CurveText.Text = $"Aplicada com {st.Applied.Dpi} DPI." +
                                 (st.Applied.Dpi != st.Dpi ? " Atenção: a sua DPI mudou, aplique de novo." : "");
            }

            BuildNextSteps(st);
        }

        void BuildNextSteps(AppState st)
        {
            NextSteps.Children.Clear();
            void Step(string text, string key, string button)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var b = new Button { Content = button, Padding = new Thickness(12, 6, 12, 6) };
                b.Click += (_, _) => MainWindow.Instance?.Navigate(key);
                DockPanel.SetDock(b, Dock.Right);
                row.Children.Add(b);
                row.Children.Add(Ui.Text("• " + text, 13, "Silver"));
                NextSteps.Children.Add(row);
            }

            if (Migration.Detect().HasConflict) Step("Encontramos o Raw Accel original, que pode brigar com o HarleAccel. Resolva em um clique.", "settings", "Resolver");
            if (DriverService.Status != DriverStatus.Active) Step("Deixe o driver do Raw Accel pronto.", "wizard", "Assistente");
            if (st.LastReport == null) Step("Faça o Teste de Perfil: 2 ou 3 partidas e o app recomenda a sua curva.", "profile", "Teste de Perfil");
            else if (st.Applied == null || AimViewKey(st.Applied) != AimViewKey(st.LastReport.Recommended))
                Step($"Compare a curva recomendada ({st.LastReport.Style}) com a atual no teste cego A/B.", "aim", "Teste A/B");
            if (!st.AimResults.Exists(r => r.Kind == "teste")) Step("Faça um aim test para ter o seu “antes”.", "aim", "Aim test");
            Step("Confira a DPI real e se o mouse tem angle snapping escondido.", "tools", "Diagnóstico");
            if (st.Applied != null && !st.Applied.IsNeutral) Step("Acostume a mão com a curva: rotina de treino de 36 minutos.", "training", "Treino");
        }

        static string AimViewKey(AccelProfile? p) => p == null ? "" : AimView.CurveKey(p);

        void Epp_Click(object sender, RoutedEventArgs e)
        {
            SystemCheck.DisableEnhancePointerPrecision();
            Refresh();
        }

        void Wizard_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.Navigate("wizard");
        void Sens_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.Navigate("sens");
        void Curve_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.Navigate("curve");
    }
}
