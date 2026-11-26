// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HarleAccel.Core;
using HarleAccel.Views;

namespace HarleAccel
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }

        readonly Dictionary<string, Func<UserControl>> _factories;
        readonly Dictionary<string, UserControl> _views = new();
        readonly Dictionary<string, RadioButton> _nav;
        string _current = "home";
        UpdateChecker.UpdateInfo? _update;

        static readonly Dictionary<string, string> PresenceText = new()
        {
            ["home"] = "No início",
            ["wizard"] = "Configurando pela primeira vez",
            ["chat"] = "Conversando com o Harle IA",
            ["sens"] = "Calculando a sensibilidade",
            ["curve"] = "Ajustando a curva",
            ["history"] = "Vendo o histórico de curvas",
            ["profile"] = "No Teste de Perfil",
            ["finder"] = "Buscando a sens ideal",
            ["aim"] = "No aim test",
            ["tools"] = "Diagnosticando o mouse",
            ["training"] = "Escolhendo um treino",
            ["settings"] = "Nas configurações",
            ["credits"] = "Nos créditos",
        };

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            _factories = new()
            {
                ["home"] = () => new HomeView(),
                ["wizard"] = () => new WizardView(),
                ["chat"] = () => new ChatView(),
                ["sens"] = () => new SensView(),
                ["curve"] = () => new CurveView(),
                ["history"] = () => new HistoryView(),
                ["profile"] = () => new ProfileTestView(),
                ["finder"] = () => new SensFinderView(),
                ["aim"] = () => new AimView(),
                ["tools"] = () => new ToolsView(),
                ["training"] = () => new TrainingView(),
                ["settings"] = () => new SettingsView(),
                ["credits"] = () => new CreditsView(),
            };
            _nav = new()
            {
                ["home"] = NavHome,
                ["wizard"] = NavWizard,
                ["chat"] = NavChat,
                ["sens"] = NavSens,
                ["curve"] = NavCurve,
                ["history"] = NavHistory,
                ["profile"] = NavProfile,
                ["finder"] = NavFinder,
                ["aim"] = NavAim,
                ["tools"] = NavTools,
                ["training"] = NavTraining,
                ["settings"] = NavSettings,
                ["credits"] = NavCredits,
            };

            TryLoadLogo();
            StateChanged += (_, _) => RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            AppState.Changed += RefreshDriverBadge;
            Activated += (_, _) => RefreshDriverBadge();
            Closing += OnClosing;

            Loaded += async (_, _) =>
            {
                RefreshDriverBadge();
                Navigate(AppState.Current.WizardDone ? "home" : "wizard");

                // se o app fechou no meio de um teste, devolve a curva
                if (AppState.Current.NeutralActive) await DriverService.EndNeutralAsync();

                AutoStart.Sync(); // mantém a reaplicação no login (e atualiza o caminho se o .exe mudou de lugar)
                StartGameWatch(); // mostra no Discord o jogo aberto junto com o status

                await TrainingLibrary.LoadAsync();

                if (AppState.Current.Settings.CheckUpdates)
                {
                    var info = await UpdateChecker.CheckAsync();
                    if (info != null) ShowUpdate(info);
                }
            };
        }

        async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (AppState.Current.NeutralActive)
            {
                e.Cancel = true;
                await DriverService.EndNeutralAsync();
                Closing -= OnClosing;
                Close();
                return;
            }
            await DiscordRpc.ClearAsync();
        }

        /// <summary>Logo: coloque a imagem em Assets/logo.png (a mesma do Harle) e recompile.</summary>
        void TryLoadLogo()
        {
            try
            {
                var img = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png"));
                LogoBrush.ImageSource = img;
                LogoBorder.Visibility = Visibility.Visible;
                LogoFallback.Visibility = Visibility.Collapsed;
            }
            catch
            {
                // sem logo: fica o "H" azul
            }
        }

        public void Navigate(string key)
        {
            if (!_nav.TryGetValue(key, out var nav)) nav = NavHome;
            if (nav.IsChecked != true) nav.IsChecked = true; // dispara Nav_Checked
            else Show(key);
        }

        void Nav_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var kv in _nav)
                if (kv.Value == sender) { Show(kv.Key); return; }
        }

        void Show(string key)
        {
            if (!_views.TryGetValue(key, out var view))
            {
                view = _factories[key]();
                _views[key] = view;
            }
            if (view is IRefreshable r) r.Refresh();
            Host.Content = view;
            _current = key;
            UpdatePresence();
        }

        // ------------------------------------------------------------------ Discord

        string? _game;          // jogo aberto agora (null = nenhum)
        long _gameStart;
        readonly System.Windows.Threading.DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(10) };
        bool _gameChecking;

        void StartGameWatch()
        {
            _gameTimer.Tick += async (_, _) => await CheckGameAsync();
            _gameTimer.Start();
            _ = CheckGameAsync();
        }

        async Task CheckGameAsync()
        {
            if (_gameChecking || !AppState.Current.Settings.DiscordEnabled) return;
            _gameChecking = true;
            try
            {
                var game = await Task.Run(GameDetector.Current);
                if (game == _game) return;
                _game = game;
                _gameStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                UpdatePresence();
            }
            catch { }
            finally { _gameChecking = false; }
        }

        public void UpdatePresence(string? overrideDetails = null)
        {
            var st = AppState.Current;
            string details = overrideDetails
                ?? (ProfileTestView.Recording ? "Fazendo o Teste de Perfil" : PresenceText.GetValueOrDefault(_current, "No HarleAccel"));
            string state = st.Applied == null || st.Applied.IsNeutral
                ? "Sem aceleração"
                : "Curva: " + st.Applied.X.Describe();
            if (state.Length > 120) state = state[..120];

            if (_game != null)
            {
                // jogando: o jogo vai em cima (e na lista de membros); a tela do app não importa
                string? extra = overrideDetails ?? (ProfileTestView.Recording ? "Fazendo o Teste de Perfil" : null);
                _ = DiscordRpc.SetAsync(_game, extra != null ? $"{extra} · {state}" : state, _gameStart);
                return;
            }
            _ = DiscordRpc.SetAsync(details, state);
        }

        // ------------------------------------------------------------------ atualização

        public void ShowUpdate(UpdateChecker.UpdateInfo info)
        {
            _update = info;
            UpdateText.Text = $"Versão nova do HarleAccel disponível: {info.Tag} (você está na v{UpdateChecker.CurrentVersion.ToString(3)}).";
            UpdateBanner.Visibility = Visibility.Visible;
        }

        void UpdateGet_Click(object sender, RoutedEventArgs e)
        {
            if (_update == null) return;
            SafeLinks.Open(_update.Url);
        }

        void UpdateLater_Click(object sender, RoutedEventArgs e) => UpdateBanner.Visibility = Visibility.Collapsed;

        // ------------------------------------------------------------------ driver

        public void RefreshDriverBadge()
        {
            var s = DriverService.Status;
            (DriverDot.Fill, DriverShort.Text) = s switch
            {
                DriverStatus.Active => ((Brush)FindResource("Ok"), $"Raw Accel {DriverService.Version} ativo"),
                DriverStatus.NeedsRestart => ((Brush)FindResource("Warn"), "Reinicie o PC"),
                DriverStatus.NotInstalled => ((Brush)FindResource("Warn"), "Driver não instalado"),
                _ when DriverService.IsDriverActive => ((Brush)FindResource("Warn"), "Driver ativo · baixe os arquivos"),
                _ => ((Brush)FindResource("Danger"), "Driver não baixado"),
            };
        }

        void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        void Max_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        void Close_Click(object sender, RoutedEventArgs e) => Close();
    }

    /// <summary>Telas que atualizam os dados quando aparecem.</summary>
    public interface IRefreshable
    {
        void Refresh();
    }
}
