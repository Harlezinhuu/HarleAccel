// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HarleAccel.Core;
using Microsoft.Win32;

namespace HarleAccel.Views
{
    public partial class WizardView : UserControl, IRefreshable
    {
        int _step = 1;
        const int StepCount = 5;
        bool _ready;
        bool _busy;

        static readonly string[] StepNames = { "Boas-vindas", "Driver", "Checagem", "Sua base", "Ponto de partida" };

        public WizardView()
        {
            InitializeComponent();
            WGame.ItemsSource = Games.All;
            _ready = true;
        }

        public void Refresh()
        {
            _ready = false;
            var st = AppState.Current;
            WGame.SelectedItem = st.Game;
            WSens.Text = Ui.N(st.Sens, "0.####");
            WDpi.Text = st.Dpi.ToString();
            _ready = true;
            ShowStep(_step);
        }

        // ------------------------------------------------------------------ passos

        void ShowStep(int n)
        {
            _step = Math.Clamp(n, 1, StepCount);
            Step1.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;
            Step5.Visibility = _step == 5 ? Visibility.Visible : Visibility.Collapsed;

            BackBtn.Visibility = _step > 1 ? Visibility.Visible : Visibility.Hidden;
            NextBtn.Content = _step == 1 ? "Começar" : _step == StepCount ? "Aplicar e terminar" : "Próximo";
            NavStatus.Text = "";

            BuildStepBar();

            if (_step == 2) RefreshDriver();
            if (_step == 3) RefreshEpp();
            if (_step == 4) Base_Changed(this, EventArgs.Empty);
            if (_step == 5) Preset_Checked(this, new RoutedEventArgs());
        }

        void BuildStepBar()
        {
            Steps.Children.Clear();
            for (int i = 1; i <= StepCount; i++)
            {
                bool active = i == _step, done = i < _step;
                var b = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = active ? Ui.Res("AccentSoft") : Ui.Res("Panel"),
                    BorderBrush = active ? Ui.Res("Accent") : Ui.Res("Line"),
                    BorderThickness = new Thickness(1),
                    Child = Ui.Text($"{(done ? "✓" : i.ToString())}  {StepNames[i - 1]}", 12,
                                    active ? "Silver" : done ? "AccentHi" : "Muted", FontWeights.SemiBold),
                };
                Steps.Children.Add(b);
            }
        }

        void Back_Click(object sender, RoutedEventArgs e) => ShowStep(_step - 1);

        async void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;

            if (_step == 4 && !SaveBase()) return;

            if (_step < StepCount)
            {
                ShowStep(_step + 1);
                return;
            }

            // último passo: aplica a curva escolhida
            var st = AppState.Current;
            var p = PresetProfile();
            st.Profile = p;
            st.WizardDone = true;
            AppState.Save();

            if (DriverService.Status != DriverStatus.Active)
            {
                Ui.SetStatus(NavStatus, "Curva salva! Ela vai ser aplicada quando o driver estiver ativo (reinicie o PC e clique em Aplicar na tela Curva).", true);
                await System.Threading.Tasks.Task.Delay(2500);
                MainWindow.Instance?.Navigate("curve");
                return;
            }

            _busy = true;
            NextBtn.IsEnabled = false;
            Ui.SetStatus(NavStatus, "Aplicando...", true);
            var (ok, msg) = await DriverService.ApplyAsync(p);
            _busy = false;
            NextBtn.IsEnabled = true;
            if (!ok) { Ui.SetStatus(NavStatus, msg, false); return; }

            st.Applied = p.Clone();
            st.AddHistory("Assistente: " + p.Describe(), p);
            AppState.Save();
            MainWindow.Instance?.Navigate("curve");
        }

        // ------------------------------------------------------------------ passo 2: driver

        void RefreshDriver()
        {
            var s = DriverService.Status;
            DrvStatus.Text = DriverService.StatusText(s);
            DrvStatus.Foreground = Ui.Res(s == DriverStatus.Active ? "Ok" : "Warn");
            DownloadBtn.IsEnabled = !_busy;
            DownloadBtn.Content = DriverService.IsDownloaded
                ? "Baixado e verificado ✓ (baixar de novo)"
                : $"Baixar e verificar o Raw Accel oficial (v{DriverService.Version})";
            InstallBtn.IsEnabled = !_busy && DriverService.IsDownloaded;
            InstallBtn.Content = DriverService.IsDriverInstalled
                ? "Driver instalado ✓ (reinstalar)"
                : "Instalar o driver (pede administrador)";
            MainWindow.Instance?.RefreshDriverBadge();
        }

        async void Download_Click(object sender, RoutedEventArgs e)
        {
            _busy = true;
            DlProgress.Visibility = Visibility.Visible;
            DlProgress.Value = 0;
            RefreshDriver();
            try
            {
                await DriverService.DownloadAsync(new Progress<double>(v => DlProgress.Value = v));
                Ui.SetStatus(NavStatus, "Pacote oficial baixado e o hash SHA-256 confere.", true);
            }
            catch (Exception ex)
            {
                Ui.SetStatus(NavStatus, "Não consegui baixar: " + ex.Message, false);
            }
            finally
            {
                _busy = false;
                DlProgress.Visibility = Visibility.Collapsed;
                RefreshDriver();
            }
        }

        async void Install_Click(object sender, RoutedEventArgs e)
        {
            _busy = true;
            RefreshDriver();
            Ui.SetStatus(NavStatus, "Aceite o aviso do Windows. Na janela preta, aperte qualquer tecla quando terminar.", true);
            bool started;
            try { started = await DriverService.RunOfficialInstallerAsync(); }
            catch (Exception ex)
            {
                _busy = false;
                RefreshDriver();
                Ui.SetStatus(NavStatus, ex.Message, false);
                return;
            }
            _busy = false;
            RefreshDriver();

            if (!started) Ui.SetStatus(NavStatus, "Instalação cancelada.", false);
            else if (DriverService.IsDriverInstalled) Ui.SetStatus(NavStatus, "Driver instalado! Reinicie o PC para ele começar a funcionar.", true);
            else Ui.SetStatus(NavStatus, "O instalador fechou, mas o driver não apareceu. Veja a mensagem da janela preta.", false);
        }

        void PickFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog { Title = "Escolha a pasta do Raw Accel (onde fica o writer.exe)" };
            if (dlg.ShowDialog() != true) return;

            if (!DriverService.IsValidRawAccelDir(dlg.FolderName))
            {
                Ui.SetStatus(NavStatus, "Essa pasta não tem writer.exe, wrapper.dll e installer.exe do Raw Accel.", false);
                return;
            }
            string? bad = DriverService.FindTamperedFile(dlg.FolderName);
            if (bad != null)
            {
                Ui.SetStatus(NavStatus, $"Os arquivos dessa pasta não são do Raw Accel {DriverService.Version} oficial ({bad} diferente). Use o botão de baixar, que confere tudo.", false);
                return;
            }
            AppState.Current.CustomRawAccelDir = dlg.FolderName;
            AppState.Save();
            Ui.SetStatus(NavStatus, "Pasta do Raw Accel definida.", true);
            RefreshDriver();
        }

        // ------------------------------------------------------------------ passo 3: checagem

        void RefreshEpp()
        {
            bool? on = SystemCheck.IsEnhancePointerPrecisionOn();
            EppStatus.Text = on switch
            {
                true => "Está LIGADO. Ele soma a aceleração do Windows por cima da sua curva. Recomendado desligar.",
                false => "Desligado. Perfeito.",
                _ => "Não consegui verificar.",
            };
            EppStatus.Foreground = Ui.Res(on == false ? "Ok" : "Warn");
            EppFixBtn.Visibility = on == true ? Visibility.Visible : Visibility.Collapsed;
            ConflictHost.Content = new ConflictPanel();
        }

        void EppFix_Click(object sender, RoutedEventArgs e)
        {
            if (!SystemCheck.DisableEnhancePointerPrecision())
                Ui.SetStatus(NavStatus, "Não consegui desligar. Desligue em Configurações → Mouse → Opções adicionais.", false);
            RefreshEpp();
        }

        // ------------------------------------------------------------------ passo 4: base

        void Base_Changed(object sender, EventArgs e)
        {
            if (!_ready || WBaseResult == null) return;
            var g = WGame.SelectedItem as Game ?? Games.All[0];
            if (Ui.TryNum(WSens.Text, out var sens) && sens > 0 && Ui.TryNum(WDpi.Text, out var dpi) && dpi > 0)
            {
                double cm = SensMath.Cm360(dpi, sens, g);
                var (label, _) = SensMath.Classify(cm, g.Kind);
                WBaseResult.Text = $"{Ui.N(cm, "0.#")} cm/360 · sensibilidade {label.ToLower()} para {g.Name}";
            }
            else WBaseResult.Text = $"Exemplo de sens para {g.Name}: {g.SensHint}";
        }

        bool SaveBase()
        {
            if (!Ui.TryNum(WSens.Text, out var sens) || sens <= 0 || !Ui.TryNum(WDpi.Text, out var dpi) || dpi <= 0 || dpi > 64000)
            {
                Ui.SetStatus(NavStatus, "Preencha a sens e a DPI para continuar.", false);
                return false;
            }
            var st = AppState.Current;
            st.Sens = sens;
            st.Dpi = (int)Math.Round(dpi);
            st.GameName = (WGame.SelectedItem as Game ?? Games.All[0]).Name;
            st.Profile.Dpi = st.Dpi;
            AppState.Save();
            return true;
        }

        // ------------------------------------------------------------------ passo 5: ponto de partida

        static AxisCurve PresetCurve(int i) => i switch
        {
            1 => AxisCurve.Linear(8, 0.010, 1.3),
            2 => AxisCurve.Linear(5, 0.025, 1.6),
            3 => AxisCurve.Linear(3, 0.05, 2.0),
            _ => AxisCurve.NoAccel(),
        };

        int SelectedPreset => PSoft.IsChecked == true ? 1 : PMid.IsChecked == true ? 2 : PStrong.IsChecked == true ? 3 : 0;

        /// <summary>Monta o perfil com o ponto de partida escolhido (sem salvar).</summary>
        AccelProfile PresetProfile()
        {
            var p = AppState.Current.Profile.Clone();
            p.X = PresetCurve(SelectedPreset);
            p.SeparateY = false;
            p.BaseProfileJson = null;
            p.SensMultiplier = 1;
            p.Dpi = AppState.Current.Dpi;
            return p;
        }

        void Preset_Checked(object sender, RoutedEventArgs e)
        {
            if (!_ready || PreviewChart == null || PresetInfo == null) return;
            var p = PresetProfile();
            PreviewChart.SetCurves(s => p.MultX(s), Math.Abs(p.YxRatio - 1) > 1e-6 ? s => p.MultY(s) : null, null, 120);
            PresetInfo.Text = SelectedPreset == 0
                ? "A sens fica 1x em qualquer velocidade."
                : $"Flick a 60 cm/s: {Ui.N(p.MultX(60), "0.00")}x · microajuste continua 1x.";
        }
    }
}
