// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

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
            // só uma janela do app por vez (duas brigariam pelo driver e pela leitura do mouse)
            _single = new System.Threading.Mutex(true, @"Local\HarleAccel.SingleInstance", out bool first);
            if (!first)
            {
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
            var w = new MainWindow();
            MainWindow = w;
            w.Show();
        }

        void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Algo deu errado:\n\n" + e.Exception.Message, "HarleAccel",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
