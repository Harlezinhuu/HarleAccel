// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace HarleAccel.Core
{
    /// <summary>Confere no GitHub do Harle se existe versão nova do HarleAccel.</summary>
    public static class UpdateChecker
    {
        public const string Repo = "Harlezinhuu/HarleAccel";
        public const string DefaultRoutinesUrl = "https://raw.githubusercontent.com/" + Repo + "/main/routines/routines.json";

        public static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

        public sealed record UpdateInfo(Version Version, string Tag, string Url, string Notes);

        /// <summary>Retorna a versão nova, ou null se não tiver (ou se não der para checar).</summary>
        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HarleAccel/" + CurrentVersion);
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                string json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
                var o = JsonNode.Parse(json);
                string tag = o?["tag_name"]?.GetValue<string>() ?? "";
                string url = o?["html_url"]?.GetValue<string>() ?? "";
                // só aceita link de release deste repositório
                if (!url.StartsWith($"https://github.com/{Repo}/", StringComparison.OrdinalIgnoreCase))
                    url = $"https://github.com/{Repo}/releases";
                string notes = o?["body"]?.GetValue<string>() ?? "";
                if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v)) return null;

                var cur = CurrentVersion;
                var curNorm = new Version(cur.Major, cur.Minor, Math.Max(0, cur.Build));
                var newNorm = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
                return newNorm > curNorm ? new UpdateInfo(newNorm, tag, url, notes) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
