// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HarleAccel.Core
{
    /// <summary>
    /// Descobre qual jogo está aberto olhando o nome dos processos (só lê a lista, não mexe em nada).
    /// Usado no Discord para mostrar o jogo junto com o status do HarleAccel.
    /// </summary>
    public static class GameDetector
    {
        /// <summary>Nome do processo (sem .exe, minúsculo) → nome que aparece no Discord.</summary>
        public static readonly IReadOnlyDictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["valorant-win64-shipping"] = "Valorant",
            ["cs2"] = "Counter-Strike 2",
            ["fortniteclient-win64-shipping"] = "Fortnite",
            ["r5apex"] = "Apex Legends",
            ["r5apex_dx12"] = "Apex Legends",
            ["overwatch"] = "Overwatch 2",
            ["cod"] = "Call of Duty",
            ["blackops6"] = "Call of Duty",
            ["modernwarfare"] = "Call of Duty",
            ["destiny2"] = "Destiny 2",
            ["project8"] = "Deadlock",
            ["rainbowsix"] = "Rainbow Six Siege",
            ["rainbowsix_vulkan"] = "Rainbow Six Siege",
            ["rainbowsix_be"] = "Rainbow Six Siege",
            ["marvel-win64-shipping"] = "Marvel Rivals",
            ["discovery"] = "The Finals",
            ["tslgame"] = "PUBG",
            ["robloxplayerbeta"] = "Roblox",
            ["aimlab_tb"] = "Aim Lab",
            ["fpsaimtrainer"] = "KovaaK's",
            ["fpsaimtrainer-win64-shipping"] = "KovaaK's",
        };

        /// <summary>Acha o primeiro jogo conhecido numa lista de nomes de processo.</summary>
        public static string? Match(IEnumerable<string> processNames)
        {
            foreach (var n in processNames)
                if (Known.TryGetValue(n, out var game)) return game;
            return null;
        }

        /// <summary>Jogo aberto agora, ou null.</summary>
        public static string? Current()
        {
            var names = new List<string>();
            foreach (var p in Process.GetProcesses())
            {
                try { names.Add(p.ProcessName); } catch { }
                finally { p.Dispose(); }
            }
            return Match(names);
        }
    }
}
