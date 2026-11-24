// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HarleAccel.Core;
using Microsoft.Win32;

namespace HarleAccel.Views
{
    public partial class SettingsView : UserControl, IRefreshable
    {
        bool _loading = true;

        public SettingsView()
        {
            InitializeComponent();
            _loading = false;
        }

        public void Refresh()
        {
            _loading = true;
            var s = AppState.Current.Settings;
            UpdatesBox.IsChecked = s.CheckUpdates;
            AutoStartBox.IsChecked = s.ApplyOnStartup;
            ConflictHost.Content = new ConflictPanel();
            UninstallRestoreBox.Visibility = Migration.HasBackup ? Visibility.Visible : Visibility.Collapsed;
            UninstallRestoreBox.IsChecked = Migration.HasBackup;
            DiscordBox.IsChecked = s.DiscordEnabled;
            DiscordIdBox.Text = s.DiscordClientId;
            RoutinesUrlBox.Text = s.RoutinesUrl;
            GeminiModelBox.Text = string.IsNullOrWhiteSpace(s.GeminiModel) ? GeminiClient.DefaultModel : s.GeminiModel;
            GeminiKeyBox.Password = "";
            KeyStatus.Text = SecretStore.HasGeminiKey ? "Chave salva ✓" : "Nenhuma chave salva.";
            KeyStatus.Foreground = Ui.Res(SecretStore.HasGeminiKey ? "Ok" : "Muted");
            DataPath.Text = "Configurações, histórico e resultados ficam em: " + AppState.Folder;
            _loading = false;
            UpdateDiscordStatus();
        }

        void Save_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var s = AppState.Current.Settings;
            s.CheckUpdates = UpdatesBox.IsChecked == true;
            s.ApplyOnStartup = AutoStartBox.IsChecked == true;
            AutoStart.Sync();
            s.DiscordEnabled = DiscordBox.IsChecked == true;
            string id = DiscordIdBox.Text.Trim();
            s.DiscordClientId = System.Text.RegularExpressions.Regex.IsMatch(id, "^[0-9]{5,25}$") ? id : "";
            string routinesUrl = RoutinesUrlBox.Text.Trim();
            if (routinesUrl.Length == 0 || SafeLinks.IsAllowed(routinesUrl)) s.RoutinesUrl = routinesUrl;
            else RoutinesStatus.Text = "Endereço recusado: use um link https do GitHub.";
            s.GeminiModel = string.IsNullOrWhiteSpace(GeminiModelBox.Text) ? GeminiClient.DefaultModel : GeminiModelBox.Text.Trim();
            AppState.Save();
            MainWindow.Instance?.UpdatePresence();
            UpdateDiscordStatus();
        }

        async void UpdateDiscordStatus()
        {
            var s = AppState.Current.Settings;
            if (!s.DiscordEnabled) { DiscordStatus.Text = ""; return; }
            if (string.IsNullOrWhiteSpace(s.DiscordClientId)) { Ui.SetStatus(DiscordStatus, "Cole o Application ID para ativar.", false); return; }
            await System.Threading.Tasks.Task.Delay(1500);
            Ui.SetStatus(DiscordStatus, DiscordRpc.Connected ? "Conectado ao Discord." : "Não conectou. O Discord está aberto? O ID está certo?", DiscordRpc.Connected);
        }

        void SaveKey_Click(object sender, RoutedEventArgs e)
        {
            string key = GeminiKeyBox.Password.Trim();
            if (key.Length < 20) { Ui.SetStatus(KeyStatus, "Essa chave parece incompleta.", false); return; }
            AppState.Current.Settings.GeminiKeyProtected = SecretStore.Protect(key);
            AppState.Save();
            GeminiKeyBox.Password = "";
            Ui.SetStatus(KeyStatus, "Chave salva e criptografada ✓", true);
        }

        void ClearKey_Click(object sender, RoutedEventArgs e)
        {
            AppState.Current.Settings.GeminiKeyProtected = "";
            AppState.Save();
            GeminiKeyBox.Password = "";
            KeyStatus.Text = "Chave apagada.";
            KeyStatus.Foreground = Ui.Res("Muted");
        }

        void AiStudio_Click(object sender, RoutedEventArgs e)
        {
            SafeLinks.Open("https://aistudio.google.com/apikey");
        }

        async void CheckNow_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus.Text = "Procurando...";
            var info = await UpdateChecker.CheckAsync();
            if (info == null) Ui.SetStatus(UpdateStatus, $"Você está na versão mais nova (v{UpdateChecker.CurrentVersion.ToString(3)}), ou não deu para checar agora.", true);
            else
            {
                Ui.SetStatus(UpdateStatus, $"Versão {info.Tag} disponível!", true);
                MainWindow.Instance?.ShowUpdate(info);
            }
        }

        async void ReloadRoutines_Click(object sender, RoutedEventArgs e)
        {
            RoutinesStatus.Text = "Carregando...";
            await TrainingLibrary.LoadAsync();
            RoutinesStatus.Text = $"{TrainingLibrary.Current.Routines.Count} rotinas (versão {TrainingLibrary.Current.Version}, {TrainingLibrary.Current.Updated}).";
        }

        void ExportRoutines_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { FileName = "routines.json", Filter = "routines.json|*.json" };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, TrainingLibrary.ToJson(TrainingLibrary.Current));
            RoutinesStatus.Text = "Exportado. Suba esse arquivo em routines/routines.json no GitHub.";
        }

        async void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            bool deleteData = UninstallDataBox.IsChecked == true;
            var answer = MessageBox.Show(
                "Isso vai:\n" +
                "• remover o driver do Raw Accel (vai aparecer o aviso de administrador)\n" +
                "• tirar o HarleAccel da inicialização do Windows\n" +
                (deleteData ? "• apagar as suas configurações, histórico e conversas\n" : "") +
                "\nDepois o app fecha. Continuar?",
                "Desinstalar o HarleAccel", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;

            UninstallBtn.IsEnabled = false;
            try
            {
                // 1) inicialização
                AppState.Current.Settings.ApplyOnStartup = false;
                AutoStart.Unregister();

                // 2) driver (desinstalador oficial, conferido por hash)
                if (DriverService.IsDriverInstalled)
                {
                    Ui.SetStatus(UninstallStatus, "Preparando o desinstalador oficial...", true);
                    if (!DriverService.IsDownloaded) await DriverService.DownloadAsync();
                    if (!await DriverService.RunOfficialUninstallerAsync())
                    {
                        Ui.SetStatus(UninstallStatus, "Cancelado no aviso de administrador. Nada foi removido do driver.", false);
                        UninstallBtn.IsEnabled = true;
                        AppState.Save();
                        return;
                    }
                }

                // 3) atalhos do Raw Accel original
                if (UninstallRestoreBox.IsChecked == true) Migration.RestoreShortcuts();

                // 4) dados
                if (deleteData)
                {
                    TryDelete(AppState.Folder);
                    TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HarleAccel"));
                }
                else AppState.Save();

                MessageBox.Show("Pronto! Reinicie o PC para terminar de remover o driver. Depois é só apagar o HarleAccel.exe.",
                    "HarleAccel", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Ui.SetStatus(UninstallStatus, "Erro: " + ex.Message, false);
                UninstallBtn.IsEnabled = true;
            }
        }

        static void TryDelete(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }

        void OpenData_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(AppState.Folder);
            try { Process.Start(new ProcessStartInfo(AppState.Folder) { UseShellExecute = true }); } catch { }
        }
    }
}
