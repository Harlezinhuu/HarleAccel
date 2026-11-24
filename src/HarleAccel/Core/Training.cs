// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HarleAccel.Core
{
    public sealed class RoutineBlock
    {
        public string Category { get; set; } = "";
        public string Scenario { get; set; } = "";
        public int Minutes { get; set; }
        public string Focus { get; set; } = "";
    }

    public sealed class Routine
    {
        public string Id { get; set; } = "";
        public string Platform { get; set; } = "Aim Lab";   // "Aim Lab" | "KovaaK's"
        public string Level { get; set; } = "Iniciante";    // Iniciante | Intermediário | Avançado
        public string Style { get; set; } = "Híbrido";      // Flicker | Tracker | Híbrido | Completa
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Credit { get; set; } = "";
        public string? ShareCode { get; set; }
        public string? Link { get; set; }
        public List<RoutineBlock> Blocks { get; set; } = new();
        public int Minutes => Blocks.Sum(b => b.Minutes);
    }

    public sealed class RoutineLibrary
    {
        public int Version { get; set; } = 1;
        public string Updated { get; set; } = "";
        public List<Routine> Routines { get; set; } = new();
    }

    /// <summary>
    /// Biblioteca de rotinas de treino. Os cenários são das rotinas públicas do Voltaic
    /// ("Fundamental KvKs Routines" e "Fundamental Aimlabs Training"), que ficam com o crédito.
    /// O Aim Lab é grátis, então todo perfil tem rotina no Aim Lab. O KovaaK's (pago) é opcional.
    /// </summary>
    public static class TrainingLibrary
    {
        public static readonly string[] Levels = { "Iniciante", "Intermediário", "Avançado" };
        public static readonly string[] Styles = { "Flicker", "Tracker", "Híbrido" };
        public static readonly string[] Platforms = { "Aim Lab", "KovaaK's" };

        const string Warm = "Aquecimento", TS = "Tracking suave", TR = "Tracking reativo",
                     FD = "Flick dinâmico", PE = "Precisão (estático)", TA = "Troca de alvo", VE = "Velocidade";

        static readonly Dictionary<string, string> Focus = new()
        {
            [Warm] = "Devagar e sem pressa: só acordar a mão com a curva nova.",
            [TS] = "Mantenha a mira colada no alvo, sem trancos. É aqui que a mão aprende a parte lenta da curva.",
            [TR] = "Acompanhe as mudanças de direção. Reaja, não tente adivinhar.",
            [FD] = "Flicks de distâncias variadas. Repare como a curva ajuda nos movimentos longos.",
            [PE] = "Acertar vem antes da velocidade. Microajuste limpo no fim de cada flick.",
            [TA] = "Troque de alvo de forma fluida, sem parar o mouse entre um e outro.",
            [VE] = "Agora sim, velocidade. Tente manter a precisão acima de ~85%.",
        };

        // cenários por plataforma -> nível -> categoria (2 opções cada)
        static readonly Dictionary<string, Dictionary<string, Dictionary<string, string[]>>> Pools = new()
        {
            ["Aim Lab"] = new()
            {
                ["Iniciante"] = new()
                {
                    [Warm] = new[] { "VT Centering 90 Easy No Strafes" },
                    [TS] = new[] { "VT Smoothpill Bronze", "VT Minisphere Vertical Bronze" },
                    [TR] = new[] { "VT Kindaclose Fast Strafes Bronze", "VT Air Close Long Strafes Bronze" },
                    [FD] = new[] { "VT mpXY Bronze", "VT A90 Bronze" },
                    [PE] = new[] { "VT ww3t TE", "VT 1w4ts 30% larger" },
                    [TA] = new[] { "VT A90T Bronze", "VT pillTS 90 Static Easy" },
                    [VE] = new[] { "VT MPspeed 2bp Bronze" },
                },
                ["Intermediário"] = new()
                {
                    [Warm] = new[] { "VT Thinbot Raspberry Gold" },
                    [TS] = new[] { "VT Smoothpill Gold", "VT Minisphere Gold" },
                    [TR] = new[] { "VT Kindaclose Fast Strafes", "VT Air AIO Gold" },
                    [FD] = new[] { "VT mpXY Gold", "VT Popcorn Gold" },
                    [PE] = new[] { "VT ww3t smaller", "1w4ts Voltaic" },
                    [TA] = new[] { "VT A180T Gold", "VT pillTS Gold" },
                    [VE] = new[] { "VT MPspeed 2bp Gold" },
                },
                ["Avançado"] = new()
                {
                    [Warm] = new[] { "VT Thin Gauntlet" },
                    [TS] = new[] { "VT Smoothsphere Easy", "VT Controlsphere Platinum" },
                    [TR] = new[] { "VT Close Fast Strafes Invincible", "VT Air AIO Easy" },
                    [FD] = new[] { "mpXY Voltaic Easy", "VT Popcorn Platinum" },
                    [PE] = new[] { "VT ww4ts", "VT 1w4ts clusters" },
                    [TA] = new[] { "VT A360T Easy", "VT pillTS 360 Easy" },
                    [VE] = new[] { "VT MPspeed 2bp" },
                },
            },
            ["KovaaK's"] = new()
            {
                ["Iniciante"] = new()
                {
                    [Warm] = new[] { "Centering 90 Easy No Strafes" },
                    [TS] = new[] { "Smoothbot Bronze", "PGTI Vertical Bronze" },
                    [TR] = new[] { "Kindaclose Fast Strafes Bronze", "Air Voltaic Close Long Strafes Easy" },
                    [FD] = new[] { "Pasu Voltaic Bronze", "B90 Voltaic Bronze" },
                    [PE] = new[] { "ww3t Voltaic Larger", "1w4ts 30% larger" },
                    [TA] = new[] { "B90T Voltaic Easy Slow", "patTS Voltaic Bronze" },
                    [VE] = new[] { "Tamspeed 2bp Bronze" },
                },
                ["Intermediário"] = new()
                {
                    [Warm] = new[] { "Thin Gauntlet Gold" },
                    [TS] = new[] { "Smoothbot Voltaic Easy", "PGTI Voltaic Gold" },
                    [TR] = new[] { "Kindaclose Fast Strafes", "Air Voltaic Easy" },
                    [FD] = new[] { "Pasu Voltaic Easy", "Popcorn Voltaic Easy" },
                    [PE] = new[] { "ww3t Voltaic Smaller", "1w4ts Voltaic" },
                    [TA] = new[] { "B180T Voltaic Easy", "patTS Voltaic Easy" },
                    [VE] = new[] { "Tamspeed 2bp Gold" },
                },
                ["Avançado"] = new()
                {
                    [Warm] = new[] { "Thin Gauntlet V2" },
                    [TS] = new[] { "Smoothsphere Easy", "Controlsphere Platinum" },
                    [TR] = new[] { "Close Fast Strafes Invincible", "Air Voltaic AIO Easy" },
                    [FD] = new[] { "Pasu Voltaic Intermediate", "Popcorn Voltaic Intermediate" },
                    [PE] = new[] { "Wide Wall 4 Targets Small", "1w2ts reload smallflicks" },
                    [TA] = new[] { "B360T Voltaic Easy", "patTS Voltaic 360 Easy" },
                    [VE] = new[] { "Tamspeed 2bp Platinum" },
                },
            },
        };

        // minutos por categoria (fora os 4 de aquecimento): total de 36 minutos
        static readonly Dictionary<string, (string Cat, int Min)[]> StyleMinutes = new()
        {
            // flicker já flicka bem: mais controle e tracking para a mão aprender a parte lenta da curva
            ["Flicker"] = new[] { (TS, 8), (TR, 6), (FD, 6), (PE, 6), (TA, 4), (VE, 2) },
            // tracker: mais flick e precisão para pegar o lado rápido da curva
            ["Tracker"] = new[] { (FD, 8), (PE, 8), (TA, 6), (TS, 4), (TR, 4), (VE, 2) },
            ["Híbrido"] = new[] { (TS, 6), (TR, 6), (FD, 6), (PE, 6), (TA, 4), (VE, 4) },
        };

        // rotinas completas do Voltaic (65 min), como referência
        static readonly Dictionary<string, (string? Code, string? Link)> FullVoltaic = new()
        {
            ["Aim Lab|Iniciante"] = (null, "https://tiny.one/ALbronze"),
            ["Aim Lab|Intermediário"] = (null, "https://tiny.one/ALgold"),
            ["Aim Lab|Avançado"] = (null, "https://tiny.one/ALplatinum"),
            ["KovaaK's|Iniciante"] = ("KOVAAKSFLANKINGGREENPISTOL", null),
            ["KovaaK's|Intermediário"] = ("KOVAAKSFLICKINGHORIZONTALCHAMPION", null),
            ["KovaaK's|Avançado"] = ("KOVAAKSGEARINGHORIZONTALWEAPONLEVEL", null),
        };

        static readonly Dictionary<string, string> VoltaicRank = new()
        {
            ["Iniciante"] = "Bronze",
            ["Intermediário"] = "Gold",
            ["Avançado"] = "Platinum",
        };

        public static RoutineLibrary BuildDefault()
        {
            var lib = new RoutineLibrary { Version = 1, Updated = "2026-11-24" };

            foreach (var platform in Platforms)
            foreach (var level in Levels)
            {
                var pool = Pools[platform][level];
                foreach (var style in Styles)
                {
                    var r = new Routine
                    {
                        Id = $"{platform}-{level}-{style}".ToLowerInvariant().Replace(" ", "").Replace("'", ""),
                        Platform = platform,
                        Level = level,
                        Style = style,
                        Title = $"Adaptação {style} · {level}",
                        Description = "Rotina de 36 minutos para acostumar a mão com a curva nova, com o foco no que o seu estilo mais precisa.",
                        Credit = $"Cenários das rotinas públicas do Voltaic (nível {VoltaicRank[level]}).",
                    };
                    r.Blocks.Add(new RoutineBlock { Category = Warm, Scenario = pool[Warm][0], Minutes = 4, Focus = Focus[Warm] });
                    foreach (var (cat, min) in StyleMinutes[style])
                    {
                        var opts = pool[cat];
                        if (min >= 4 && opts.Length > 1)
                        {
                            r.Blocks.Add(new RoutineBlock { Category = cat, Scenario = opts[0], Minutes = min / 2, Focus = Focus[cat] });
                            r.Blocks.Add(new RoutineBlock { Category = cat, Scenario = opts[1], Minutes = min - min / 2, Focus = Focus[cat] });
                        }
                        else
                        {
                            r.Blocks.Add(new RoutineBlock { Category = cat, Scenario = opts[0], Minutes = min, Focus = Focus[cat] });
                        }
                    }
                    lib.Routines.Add(r);
                }

                var (code, link) = FullVoltaic[$"{platform}|{level}"];
                lib.Routines.Add(new Routine
                {
                    Id = $"{platform}-{level}-voltaic".ToLowerInvariant().Replace(" ", "").Replace("'", ""),
                    Platform = platform,
                    Level = level,
                    Style = "Completa",
                    Title = $"Rotina completa Voltaic {VoltaicRank[level]} (65 min)",
                    Description = "A rotina fundamental original do Voltaic, para quem quer um treino geral e longo.",
                    Credit = "Voltaic (voltaic.gg)",
                    ShareCode = code,
                    Link = link,
                    Blocks = new List<RoutineBlock>
                    {
                        new() { Category = "Rotina completa", Scenario = code != null ? $"Cole o código {code} na aba Online Playlist" : "Abra o link da playlist no Aim Lab", Minutes = 65, Focus = "Siga a ordem da playlist." },
                    },
                });
            }
            return lib;
        }

        // ------------------------------------------------------------------ carregar (remoto -> cache -> padrão)

        static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        static string CachePath => Path.Combine(AppState.Folder, "routines-cache.json");

        public static RoutineLibrary Current { get; private set; } = BuildDefault();

        public static string ToJson(RoutineLibrary lib) => JsonSerializer.Serialize(lib, Opts);

        /// <summary>
        /// A biblioteca vem da internet, então é conferida antes de ser usada:
        /// tamanhos limitados, links só https de sites conhecidos e códigos só com letras e números.
        /// </summary>
        internal static bool IsValid(RoutineLibrary? lib)
        {
            if (lib == null || lib.Routines.Count == 0 || lib.Routines.Count > 200) return false;
            if (!lib.Routines.Any(r => r.Platform == "Aim Lab")) return false;
            static bool ShortText(string? s, int max) => s == null || s.Length <= max;
            foreach (var r in lib.Routines)
            {
                if (!Platforms.Contains(r.Platform)) return false;
                if (r.Blocks.Count == 0 || r.Blocks.Count > 40) return false;
                if (!ShortText(r.Id, 80) || !ShortText(r.Title, 120) || !ShortText(r.Description, 400) || !ShortText(r.Credit, 200)) return false;
                if (r.Link != null && !SafeLinks.IsAllowed(r.Link)) return false;
                if (r.ShareCode != null && !System.Text.RegularExpressions.Regex.IsMatch(r.ShareCode, "^[A-Za-z0-9]{1,80}$")) return false;
                foreach (var b in r.Blocks)
                {
                    if (b.Minutes < 1 || b.Minutes > 120) return false;
                    if (!ShortText(b.Scenario, 120) || !ShortText(b.Category, 60) || !ShortText(b.Focus, 300)) return false;
                }
            }
            return true;
        }

        /// <summary>Tenta baixar a biblioteca atualizada do GitHub. Se não der, usa o cache ou a padrão.</summary>
        public static async Task LoadAsync()
        {
            try
            {
                if (File.Exists(CachePath))
                {
                    var cached = JsonSerializer.Deserialize<RoutineLibrary>(await File.ReadAllTextAsync(CachePath), Opts);
                    if (IsValid(cached) && cached!.Version >= Current.Version) Current = cached;
                }
            }
            catch { }

            try
            {
                string url = AppState.Current.Settings.RoutinesUrl;
                if (string.IsNullOrWhiteSpace(url) || !SafeLinks.IsAllowed(url)) return;
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = 512 * 1024 };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HarleAccel");
                string json = await http.GetStringAsync(url);
                var remote = JsonSerializer.Deserialize<RoutineLibrary>(json, Opts);
                if (IsValid(remote) && remote!.Version >= Current.Version)
                {
                    Current = remote;
                    Directory.CreateDirectory(AppState.Folder);
                    await File.WriteAllTextAsync(CachePath, json);
                }
            }
            catch { /* sem internet ou arquivo ainda não publicado: fica a padrão */ }
        }

        public static Routine? Find(string platform, string level, string style) =>
            Current.Routines.FirstOrDefault(r => r.Platform == platform && r.Level == level && r.Style == style);
    }
}
