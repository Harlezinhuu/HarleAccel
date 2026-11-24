// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace HarleAccel.Core
{
    public enum DriverStatus
    {
        NotDownloaded,   // não baixou o pacote oficial
        NotInstalled,    // baixou, mas o driver não está no Windows
        NeedsRestart,    // instalado, mas ainda não carregou (precisa reiniciar)
        Active,          // funcionando
    }

    /// <summary>
    /// Cuida do driver OFICIAL do Raw Accel: baixa do GitHub deles, confere o SHA-256,
    /// roda o instalador oficial e aplica configurações com o writer.exe oficial.
    /// O HarleAccel nunca mexe no kernel por conta própria.
    /// </summary>
    public static class DriverService
    {
        public const string Version = "1.7.1";
        public const string DownloadUrl =
            "https://github.com/RawAccelOfficial/rawaccel/releases/download/v1.7.1/RawAccel_v1.7.1.zip";

        /// <summary>SHA-256 do RawAccel_v1.7.1.zip publicado no GitHub oficial.</summary>
        public const string Sha256 = "770fe3ae0919ca3c4d412f58c985eb27f5434decad809f7e8206de4e8852eec4";

        /// <summary>
        /// SHA-256 de cada arquivo do pacote oficial v1.7.1. Conferidos ANTES de rodar qualquer coisa,
        /// para ninguém trocar o instalador (que roda como admin) por outro programa.
        /// </summary>
        static readonly (string File, string Hash)[] OfficialFiles =
        {
            ("installer.exe", "b3b4948de9470a5727390ed016507b59d2c628463e42aaf1b75e906ee04f58a9"),
            ("writer.exe", "b53e29c7689b9d72838ffbfd1502b8d0407160d3f77b54eb15a7c632f689aa04"),
            ("wrapper.dll", "e17d540fb0bb5e47ba7c8e01aff39f1570bfb7ef673a1ddc0bccd2a26f9efb78"),
            ("Newtonsoft.Json.dll", "b624949df8b0e3a6153fdfb730a7c6f4990b6592ee0d922e1788433d276610f3"),
            (@"driver\rawaccel.sys", "8a62c4deef2774b43a7363b352eda79897533a1080c9c26ffeff0559e43358d7"),
        };

        /// <summary>Confere se os arquivos da pasta são exatamente os oficiais. Retorna o nome do arquivo alterado, ou null se está tudo certo.</summary>
        public static string? FindTamperedFile(string dir, params string[] only)
        {
            foreach (var (file, hash) in OfficialFiles)
            {
                if (only.Length > 0 && !only.Contains(file)) continue;
                string path = Path.Combine(dir, file);
                try
                {
                    using var fs = File.OpenRead(path);
                    string h = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
                    if (h != hash) return file;
                }
                catch { return file; }
            }
            return null;
        }

        static string LocalRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HarleAccel");

        static string DownloadedDir => Path.Combine(LocalRoot, "RawAccel");

        /// <summary>Pasta do Raw Accel em uso (a escolhida pela pessoa ou a baixada pelo app).</summary>
        public static string RawAccelDir
        {
            get
            {
                var custom = AppState.Current.CustomRawAccelDir;
                if (!string.IsNullOrWhiteSpace(custom) && IsValidRawAccelDir(custom)) return custom;
                return DownloadedDir;
            }
        }

        public static bool IsValidRawAccelDir(string dir) =>
            File.Exists(Path.Combine(dir, "writer.exe")) &&
            File.Exists(Path.Combine(dir, "wrapper.dll")) &&
            File.Exists(Path.Combine(dir, "installer.exe"));

        public static bool IsDownloaded => IsValidRawAccelDir(RawAccelDir);

        public static bool IsDriverInstalled =>
            File.Exists(Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\drivers\rawaccel.sys"));

        public static bool IsDriverActive
        {
            get
            {
                try
                {
                    using var h = CreateFileW(@"\\.\rawaccel", 0, 0, IntPtr.Zero, 3 /*OPEN_EXISTING*/, 0, IntPtr.Zero);
                    return !h.IsInvalid;
                }
                catch { return false; }
            }
        }

        public static DriverStatus Status
        {
            get
            {
                if (IsDriverActive) return IsDownloaded ? DriverStatus.Active : DriverStatus.NotDownloaded;
                if (!IsDownloaded) return DriverStatus.NotDownloaded;
                if (!IsDriverInstalled) return DriverStatus.NotInstalled;
                return DriverStatus.NeedsRestart;
            }
        }

        public static string StatusText(DriverStatus s) => s switch
        {
            DriverStatus.NotDownloaded => IsDriverActive
                ? "Driver ativo, mas falta baixar os arquivos do Raw Accel para o app conseguir aplicar curvas."
                : "Driver não baixado.",
            DriverStatus.NotInstalled => "Pacote oficial baixado e verificado. Falta instalar o driver.",
            DriverStatus.NeedsRestart => "Driver instalado. Reinicie o PC para ele começar a funcionar.",
            DriverStatus.Active => $"Driver oficial do Raw Accel ativo.",
            _ => "",
        };

        // ------------------------------------------------------------------ download

        /// <summary>Baixa o pacote oficial, confere o hash e extrai. Lança exceção com mensagem amigável se algo falhar.</summary>
        public static async Task DownloadAsync(IProgress<double>? progress = null)
        {
            Directory.CreateDirectory(LocalRoot);
            string zipPath = Path.Combine(LocalRoot, $"RawAccel_v{Version}.zip");

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HarleAccel/0.4");
                using var resp = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? 1_542_808;

                await using var src = await resp.Content.ReadAsStreamAsync();
                await using var dst = File.Create(zipPath);
                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buffer)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n));
                    read += n;
                    progress?.Report(Math.Min(1.0, (double)read / total));
                }
            }

            // confere o SHA-256 antes de usar qualquer coisa do zip
            string hash;
            await using (var fs = File.OpenRead(zipPath))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();

            if (hash != Sha256)
            {
                try { File.Delete(zipPath); } catch { }
                throw new InvalidDataException(
                    "O arquivo baixado não bate com o hash oficial do Raw Accel. " +
                    "Por segurança ele foi apagado e nada foi instalado.");
            }

            string extractTmp = Path.Combine(LocalRoot, "extract_tmp");
            if (Directory.Exists(extractTmp)) Directory.Delete(extractTmp, true);
            ZipFile.ExtractToDirectory(zipPath, extractTmp);

            string inner = Path.Combine(extractTmp, "RawAccel");
            if (!IsValidRawAccelDir(inner) || FindTamperedFile(inner) != null)
                throw new InvalidDataException("O pacote do Raw Accel não tem os arquivos oficiais esperados.");

            if (Directory.Exists(DownloadedDir)) Directory.Delete(DownloadedDir, true);
            Directory.Move(inner, DownloadedDir);
            Directory.Delete(extractTmp, true);
            try { File.Delete(zipPath); } catch { }

            AppState.Current.CustomRawAccelDir = null;
            AppState.Save();
        }

        // ------------------------------------------------------------------ instalação

        /// <summary>
        /// Roda o installer.exe oficial como administrador (aparece o aviso do Windows).
        /// Retorna false se a pessoa cancelou o pedido de admin.
        /// </summary>
        public static async Task<bool> RunOfficialInstallerAsync()
        {
            string dir = RawAccelDir;
            // confere o instalador e o driver logo antes de rodar como admin
            string? bad = FindTamperedFile(dir, "installer.exe", @"driver\rawaccel.sys");
            if (bad != null)
                throw new InvalidDataException($"O arquivo {bad} não é o original do Raw Accel {Version}. Por segurança o instalador não foi executado. Baixe o pacote de novo pelo assistente.");

            // O instalador oficial usa caminho relativo (driver\rawaccel.sys), por isso entra na pasta antes.
            string cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var psi = new ProcessStartInfo(cmd, $"/c cd /d \"{dir}\" && \"{Path.Combine(dir, "installer.exe")}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal,
            };
            try
            {
                using var p = Process.Start(psi);
                if (p != null) await p.WaitForExitAsync();
                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return false; // cancelou o UAC
            }
        }

        // ------------------------------------------------------------------ aplicar

        public static string AppliedSettingsPath => Path.Combine(LocalRoot, "settings.json");

        /// <summary>Envia a configuração para o driver usando o writer.exe oficial.</summary>
        public static async Task<(bool Ok, string Message)> ApplyAsync(AccelProfile profile)
        {
            string? err = RawAccelJson.Validate(profile);
            if (err != null) return (false, err);

            if (!IsDownloaded) return (false, "Baixe o Raw Accel oficial primeiro (Assistente → Driver).");
            if (!IsDriverActive)
                return (false, IsDriverInstalled
                    ? "O driver está instalado, mas só começa a funcionar depois de reiniciar o PC."
                    : "O driver ainda não está instalado (Assistente → Driver).");

            string? bad = FindTamperedFile(RawAccelDir, "writer.exe", "wrapper.dll", "Newtonsoft.Json.dll");
            if (bad != null)
                return (false, $"O arquivo {bad} não é o original do Raw Accel {Version}. Por segurança nada foi aplicado. Baixe o pacote de novo pelo Assistente.");

            Directory.CreateDirectory(LocalRoot);
            string json = RawAccelJson.Build(profile);
            await File.WriteAllTextAsync(AppliedSettingsPath, json);

            var psi = new ProcessStartInfo(Path.Combine(RawAccelDir, "writer.exe"), $"\"{AppliedSettingsPath}\"")
            {
                WorkingDirectory = RawAccelDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                using var p = Process.Start(psi);
                if (p == null) return (false, "Não consegui abrir o writer.exe do Raw Accel.");
                await p.WaitForExitAsync();
                if (p.ExitCode != 0)
                    return (false, "O Raw Accel recusou a configuração (a janela dele mostrou o motivo).");
            }
            catch (Exception ex)
            {
                return (false, "Erro ao aplicar: " + ex.Message);
            }

            // o driver só aceita uma escrita por segundo
            await Task.Delay(1000);
            return (true, "Curva aplicada no driver.");
        }

        // ------------------------------------------------------------------ modo neutro para testes

        /// <summary>
        /// Deixa o driver sem curva enquanto um teste mede o mouse "cru".
        /// Retorna true se mudou alguma coisa (aí é preciso chamar <see cref="EndNeutralAsync"/>).
        /// </summary>
        public static async Task<bool> BeginNeutralAsync()
        {
            var st = AppState.Current;
            if (st.Applied == null || st.Applied.IsNeutral || !IsDriverActive || !IsDownloaded) return false;
            var (ok, _) = await ApplyAsync(AccelProfile.Neutral(st.Dpi));
            if (!ok) return false;
            st.NeutralActive = true;
            AppState.Save();
            return true;
        }

        /// <summary>Volta a curva que estava aplicada antes do teste.</summary>
        public static async Task EndNeutralAsync()
        {
            var st = AppState.Current;
            if (!st.NeutralActive) return;
            if (st.Applied != null) await ApplyAsync(st.Applied);
            st.NeutralActive = false;
            AppState.Save();
        }

        // ------------------------------------------------------------------ P/Invoke

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sec,
                                                uint disposition, uint flags, IntPtr template);
    }
}
