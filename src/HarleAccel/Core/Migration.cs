// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace HarleAccel.Core
{
    /// <summary>
    /// Encontra o Raw Accel original de quem já usava e resolve os conflitos com o HarleAccel:
    /// - atalho na inicialização do Windows que reaplica as configurações antigas;
    /// - janela do Raw Accel aberta (ela também aplica as configurações dela);
    /// - driver de outra versão (não aceita as configurações do 1.7.1).
    /// Nada é apagado: os atalhos vão para uma pasta de backup e podem voltar.
    /// </summary>
    public static class Migration
    {
        public sealed class OldShortcut
        {
            public string LinkPath { get; init; } = "";
            public string Target { get; init; } = "";
        }

        public sealed class Report
        {
            public List<OldShortcut> Shortcuts { get; } = new();
            public bool GuiRunning { get; set; }
            public bool? DriverIsOfficial { get; set; }   // null = sem driver
            public string? OldSettingsPath { get; set; }

            public bool HasConflict => Shortcuts.Count > 0 || GuiRunning || DriverIsOfficial == false;
        }

        static readonly string[] Candidates = { "rawaccel", "raw accel", "writer" };

        static string StartupFolder => Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        public static string BackupFolder => Path.Combine(AppState.Folder, "backup-inicializacao");

        public static Report Detect()
        {
            var r = new Report();

            try
            {
                string folder = StartupFolder;
                if (Directory.Exists(folder))
                {
                    foreach (var lnk in Directory.EnumerateFiles(folder, "*.lnk"))
                    {
                        string name = Path.GetFileName(lnk).ToLowerInvariant();
                        if (!Candidates.Any(name.Contains)) continue;
                        string target = ResolveShortcut(lnk) ?? "";
                        string t = target.ToLowerInvariant();
                        // só atalhos que apontam para o Raw Accel (rawaccel.exe ou writer.exe com rawaccel.exe do lado)
                        bool isRa = t.EndsWith("rawaccel.exe") ||
                                    (t.EndsWith("writer.exe") && File.Exists(Path.Combine(Path.GetDirectoryName(target) ?? "", "rawaccel.exe")));
                        if (!isRa) continue;
                        r.Shortcuts.Add(new OldShortcut { LinkPath = lnk, Target = target });

                        string settings = Path.Combine(Path.GetDirectoryName(target) ?? "", "settings.json");
                        if (r.OldSettingsPath == null && File.Exists(settings)) r.OldSettingsPath = settings;
                    }
                }
            }
            catch { }

            try { r.GuiRunning = Process.GetProcessesByName("rawaccel").Length > 0; } catch { }
            r.DriverIsOfficial = DriverService.InstalledDriverIsOfficial;

            if (r.OldSettingsPath == null)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName("rawaccel"))
                    {
                        string? exe = p.MainModule?.FileName;
                        string settings = Path.Combine(Path.GetDirectoryName(exe) ?? "", "settings.json");
                        if (exe != null && File.Exists(settings)) { r.OldSettingsPath = settings; break; }
                    }
                }
                catch { }
            }
            return r;
        }

        /// <summary>Tira os atalhos do Raw Accel antigo da inicialização (move para o backup, não apaga).</summary>
        public static int DisableShortcuts(Report r)
        {
            int moved = 0;
            Directory.CreateDirectory(BackupFolder);
            foreach (var s in r.Shortcuts)
            {
                try
                {
                    string dest = Path.Combine(BackupFolder, Path.GetFileName(s.LinkPath));
                    if (File.Exists(dest)) dest = Path.Combine(BackupFolder, $"{DateTime.Now:yyyyMMddHHmmss}-{Path.GetFileName(s.LinkPath)}");
                    File.Move(s.LinkPath, dest);
                    moved++;
                }
                catch { }
            }
            return moved;
        }

        /// <summary>Devolve os atalhos guardados no backup para a inicialização do Windows.</summary>
        public static int RestoreShortcuts()
        {
            int restored = 0;
            if (!Directory.Exists(BackupFolder)) return 0;
            foreach (var f in Directory.EnumerateFiles(BackupFolder, "*.lnk"))
            {
                try
                {
                    string name = Path.GetFileName(f);
                    int dash = name.IndexOf('-');
                    if (dash == 14 && name[..14].All(char.IsDigit)) name = name[15..];
                    string dest = Path.Combine(StartupFolder, name);
                    if (!File.Exists(dest)) { File.Move(f, dest); restored++; }
                }
                catch { }
            }
            return restored;
        }

        public static bool HasBackup => Directory.Exists(BackupFolder) && Directory.EnumerateFiles(BackupFolder, "*.lnk").Any();

        /// <summary>Fecha a janela do Raw Accel original (pede para fechar; se não fechar, encerra).</summary>
        public static void CloseGui()
        {
            foreach (var p in Process.GetProcessesByName("rawaccel"))
            {
                try
                {
                    if (!p.CloseMainWindow() || !p.WaitForExit(3000)) p.Kill();
                }
                catch { }
                finally { p.Dispose(); }
            }
        }

        // ------------------------------------------------------------------ atalho (.lnk)

        static string? ResolveShortcut(string lnk)
        {
            object? shell = null, link = null;
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return null;
                shell = Activator.CreateInstance(t);
                link = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                return link?.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, link, null) as string;
            }
            catch { return null; }
            finally
            {
                if (link != null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
