// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    /// <summary>
    /// Detecta o Raw Accel original de quem já usava e oferece um botão para cada conflito.
    /// Usado no Assistente e nas Configurações.
    /// </summary>
    public sealed class ConflictPanel : Border
    {
        readonly StackPanel _root = new();
        readonly TextBlock _status;
        bool _busy;

        public event Action? Changed;

        public ConflictPanel()
        {
            Background = Ui.Res("Panel");
            CornerRadius = new CornerRadius(0);
            Padding = new Thickness(14);
            Child = _root;
            _status = Ui.Text("", 12, "Muted");
            Refresh();
        }

        public void Refresh()
        {
            _root.Children.Clear();
            var r = Migration.Detect();

            if (!r.HasConflict && r.OldSettingsPath == null)
            {
                _root.Children.Add(Ui.Text("Raw Accel original", 14, "Silver", FontWeights.SemiBold));
                _root.Children.Add(Ui.Text("Nenhum conflito encontrado. ✓", 12, "Ok"));
                AddRestore();
                _root.Children.Add(_status);
                return;
            }

            _root.Children.Add(Ui.Text("Encontramos o Raw Accel original", 14, "Warn", FontWeights.SemiBold));
            _root.Children.Add(Ui.Text("Ele pode brigar com o HarleAccel. Resolva cada item abaixo (nada é apagado; dá para desfazer):", 12, "Muted"));

            if (r.Shortcuts.Count > 0)
                Item("Na inicialização do Windows",
                     "O Raw Accel antigo reaplica as configurações dele toda vez que o PC liga, por cima das do HarleAccel.",
                     "Tirar da inicialização", () =>
                     {
                         int n = Migration.DisableShortcuts(r);
                         return $"{n} atalho(s) guardado(s) no backup. O HarleAccel passa a reaplicar a sua curva no lugar dele.";
                     });

            if (r.GuiRunning)
                Item("Janela do Raw Accel aberta",
                     "Enquanto ela estiver aberta, pode aplicar as configurações dela por cima.",
                     "Fechar o Raw Accel", () =>
                     {
                         Migration.CloseGui();
                         return "Raw Accel fechado.";
                     });

            if (r.DriverIsOfficial == false)
                ItemAsync("Driver de outra versão",
                     $"O driver instalado não é o Raw Accel {DriverService.Version} que o HarleAccel usa, então as curvas não vão ser aceitas. " +
                     "O instalador oficial remove o antigo sozinho e coloca o novo. Depois é só reiniciar o PC.",
                     "Instalar a versão certa por cima", async () =>
                     {
                         if (!DriverService.IsDownloaded) await DriverService.DownloadAsync();
                         bool ok = await DriverService.RunOfficialInstallerAsync();
                         return ok ? "Instalado. Reinicie o PC para o driver novo começar a funcionar." : "Instalação cancelada.";
                     });

            if (r.OldSettingsPath != null)
                Item("Suas configurações antigas",
                     "Achamos o settings.json do seu Raw Accel. Dá para trazer a sua curva para o HarleAccel.",
                     "Importar para o HarleAccel", () =>
                     {
                         var st = AppState.Current;
                         var p = RawAccelJson.Parse(File.ReadAllText(r.OldSettingsPath), st.Dpi, out var warn);
                         st.AddHistory("Antes de importar: " + st.Profile.Describe(), st.Profile);
                         st.Profile = p;
                         AppState.Save();
                         return "Importado para o editor. Confira na tela Curva e clique em Aplicar." + (warn != null ? " " + warn : "");
                     });

            AddRestore();
            _root.Children.Add(_status);
        }

        void AddRestore()
        {
            if (!Migration.HasBackup) return;
            var b = new Button { Content = "Devolver os atalhos antigos do Raw Accel", Style = (Style)Application.Current.FindResource("Ghost"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
            b.Click += (_, _) =>
            {
                int n = Migration.RestoreShortcuts();
                Done($"{n} atalho(s) devolvido(s) para a inicialização.", true);
            };
            _root.Children.Add(b);
        }

        void Item(string title, string text, string button, Func<string> action) =>
            ItemAsync(title, text, button, () => System.Threading.Tasks.Task.FromResult(action()));

        void ItemAsync(string title, string text, string button, Func<System.Threading.Tasks.Task<string>> action)
        {
            var row = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            var btn = new Button { Content = button, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Padding = new Thickness(12, 7, 12, 7) };
            DockPanel.SetDock(btn, Dock.Right);
            var info = new StackPanel();
            info.Children.Add(Ui.Text(title, 13, "Silver", FontWeights.SemiBold));
            info.Children.Add(Ui.Text(text, 12, "Muted"));
            row.Children.Add(btn);
            row.Children.Add(info);
            _root.Children.Add(row);

            btn.Click += async (_, _) =>
            {
                if (_busy) return;
                _busy = true;
                btn.IsEnabled = false;
                try { Done(await action(), true); }
                catch (Exception ex) { Done(ex.Message, false); }
                finally { _busy = false; }
            };
        }

        void Done(string msg, bool ok)
        {
            Refresh();
            Ui.SetStatus(_status, msg, ok);
            _status.Margin = new Thickness(0, 10, 0, 0);
            MainWindow.Instance?.RefreshDriverBadge();
            Changed?.Invoke();
        }
    }
}
