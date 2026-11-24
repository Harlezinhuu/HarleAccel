// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using HarleAccel.Core;

namespace HarleAccel
{
    public partial class App : Application
    {
        static System.Threading.Mutex? _single;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool applyMode = e.Args.Any(a => a.Equals(AutoStart.ApplyArg, StringComparison.OrdinalIgnoreCase));

            // só uma janela do app por vez (duas brigariam pelo driver e pela leitura do mouse)
            _single = new System.Threading.Mutex(true, @"Local\HarleAccel.SingleInstance", out bool first);
            if (!first)
            {
                if (!applyMode)
                    MessageBox.Show("O HarleAccel já está aberto.", "HarleAccel", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            AppState.Load();
            // migração: o padrão antigo não tinha nível grátis confirmado
            if (AppState.Current.Settings.GeminiModel == "gemini-3.5-flash-lite")
                AppState.Current.Settings.GeminiModel = GeminiClient.DefaultModel;
            DispatcherUnhandledException += OnUnhandled;
            base.OnStartup(e);

            if (applyMode)
            {
                // modo silencioso do login do Windows: reaplica a curva e fecha, sem abrir janela
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                ApplySavedCurveAndExit();
                return;
            }

            LoadFonts();
            var w = new MainWindow();
            MainWindow = w;
            w.Show();
        }

        /// <summary>
        /// Fontes do visual HUD (as mesmas da F.R.I.D.A.Y), embutidas no .exe: Tektur (títulos),
        /// Sora (texto) e JetBrains Mono (números). Licença OFL, veja THIRD_PARTY_NOTICES.md.
        /// Se algo der errado, fica a Segoe UI definida no tema.
        /// </summary>
        void LoadFonts()
        {
            try
            {
                var baseUri = new Uri("pack://application:,,,/");
                Resources["UiFont"] = new System.Windows.Media.FontFamily(baseUri, "./Fonts/#Sora, Segoe UI");
                Resources["DisplayFont"] = new System.Windows.Media.FontFamily(baseUri, "./Fonts/#Tektur, Bahnschrift, Segoe UI");
                Resources["DataFont"] = new System.Windows.Media.FontFamily(baseUri, "./Fonts/#JetBrains Mono, Consolas");
            }
            catch { }
        }

        /// <summary>O driver não guarda a curva depois de reiniciar: aqui ela volta.</summary>
        async void ApplySavedCurveAndExit()
        {
            try
            {
                var st = AppState.Current;
                if (st.Settings.ApplyOnStartup && (st.Applied != null || st.NeutralActive))
                {
                    // no login o driver pode demorar uns segundos para ficar pronto
                    for (int i = 0; i < 30 && !DriverService.IsDriverActive; i++)
                        await Task.Delay(1000);

                    if (DriverService.IsDriverActive)
                    {
                        if (st.NeutralActive) await DriverService.EndNeutralAsync(); // fechou no meio de um teste
                        else if (st.Applied != null) await DriverService.ApplyAsync(st.Applied);
                    }
                }
            }
            catch { /* no login não dá para mostrar erro; o app mostra o status quando for aberto */ }
            finally
            {
                Shutdown();
            }
        }

        void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Algo deu errado:\n\n" + e.Exception.Message, "HarleAccel",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
