// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using Microsoft.Win32;

namespace HarleAccel.Core
{
    /// <summary>
    /// O driver do Raw Accel não guarda a curva quando o PC reinicia.
    /// Por isso o HarleAccel se registra para rodar no login com "--apply":
    /// ele confere os arquivos oficiais, reaplica a curva salva e fecha, sem abrir janela.
    /// Fica só no registro do seu usuário (HKCU), não precisa de admin.
    /// </summary>
    public static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "HarleAccel";
        public const string ApplyArg = "--apply";

        public static string Command(string exePath) => $"\"{exePath}\" {ApplyArg}";

        public static bool IsRegistered
        {
            get
            {
                try
                {
                    using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                    return k?.GetValue(ValueName) is string;
                }
                catch { return false; }
            }
        }

        /// <summary>Registra (ou atualiza o caminho, se o .exe mudou de lugar).</summary>
        public static bool Register()
        {
            try
            {
                string? exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return false;
                using var k = Registry.CurrentUser.CreateSubKey(RunKey, true);
                string cmd = Command(exe);
                if (k.GetValue(ValueName) as string != cmd) k.SetValue(ValueName, cmd);
                return true;
            }
            catch { return false; }
        }

        public static void Unregister()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
                k?.DeleteValue(ValueName, false);
            }
            catch { }
        }

        /// <summary>Deixa o registro igual à configuração do app.</summary>
        public static void Sync()
        {
            if (AppState.Current.Settings.ApplyOnStartup) Register();
            else Unregister();
        }
    }
}
