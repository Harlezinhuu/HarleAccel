// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Diagnostics;
using System.Linq;

namespace HarleAccel.Core
{
    /// <summary>
    /// Só abre links https de sites conhecidos. Links que vêm de fora (rotinas baixadas, GitHub)
    /// nunca conseguem abrir um programa, um arquivo local ou um site qualquer.
    /// </summary>
    public static class SafeLinks
    {
        static readonly string[] AllowedHosts =
        {
            "github.com", "raw.githubusercontent.com", "discord.gg", "discord.com",
            "aistudio.google.com", "ai.google.dev",
            "aimlabs.com", "aimlab.gg", "go.aimlab.gg", "steamcommunity.com",
            "kovaaks.com", "voltaic.gg", "tiny.one", "tinyurl.com", "bit.ly",
        };

        public static bool IsAllowed(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Length > 500) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u)) return false;
            if (u.Scheme != Uri.UriSchemeHttps) return false;
            string host = u.Host.ToLowerInvariant();
            return AllowedHosts.Any(h => host == h || host.EndsWith("." + h));
        }

        /// <summary>Abre no navegador se for permitido. Retorna false se o link foi bloqueado.</summary>
        public static bool Open(string? url)
        {
            if (!IsAllowed(url)) return false;
            try
            {
                Process.Start(new ProcessStartInfo(new Uri(url!.Trim()).AbsoluteUri) { UseShellExecute = true });
                return true;
            }
            catch { return false; }
        }
    }
}
