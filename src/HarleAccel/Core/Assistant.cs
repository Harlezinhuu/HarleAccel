// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HarleAccel.Core
{
    // ====================================================================== conversão

    public sealed class ConvertedSens
    {
        public string Game { get; set; } = "";
        public double Sens { get; set; }
        public string SensText { get; set; } = "";
        public double EDpi { get; set; }
        public bool Highlight { get; set; }
    }

    public sealed class ConversionResult
    {
        public string From { get; set; } = "";
        public double Sens { get; set; }
        public string SensText { get; set; } = "";
        public int Dpi { get; set; }
        public bool DpiFromBase { get; set; }
        public double Cm360 { get; set; }
        public string? Note { get; set; }
        public List<ConvertedSens> Items { get; set; } = new();
    }

    // ====================================================================== mensagens do chat

    public sealed class ChatItem
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public string Role { get; set; } = "assistant";   // user | assistant
        public string Text { get; set; } = "";
        public string Kind { get; set; } = "text";        // text | conversion | permission
        public ConversionResult? Conversion { get; set; }
        public CurveProposal? Proposal { get; set; }

        /// <summary>Tela do app que a mensagem oferece abrir (botão), ou null.</summary>
        public string? OpenScreen { get; set; }
    }

    public sealed class CurveProposal
    {
        public AccelProfile Before { get; set; } = new();
        public AccelProfile After { get; set; } = new();
        public List<string> Changes { get; set; } = new();
        public string Reason { get; set; } = "";
        public string State { get; set; } = "pending";    // pending | applied | refused | undone
    }

    public static class ChatStore
    {
        static string FilePath => Path.Combine(AppState.Folder, "chat.json");
        static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

        public static List<ChatItem> Items { get; private set; } = new();

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    Items = JsonSerializer.Deserialize<List<ChatItem>>(File.ReadAllText(FilePath), Opts) ?? new();
            }
            catch { Items = new(); }
        }

        public static void Save()
        {
            try
            {
                if (Items.Count > 150) Items = Items.Skip(Items.Count - 150).ToList();
                Directory.CreateDirectory(AppState.Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(Items, Opts));
            }
            catch { }
        }

        public static void Clear()
        {
            Items.Clear();
            Save();
        }
    }

    // ====================================================================== ferramentas (fazem as contas de verdade)

    public static class AssistantTools
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool IsFortnite(Game g) => g.Name.StartsWith("Fortnite");

        public static string FormatSens(Game g, double sens) =>
            IsFortnite(g) ? sens.ToString("0.0#", Inv) + " %" : sens.ToString("0.###", Inv);

        /// <summary>Converte a sens de um jogo para outro(s) mantendo o mesmo cm/360.</summary>
        public static ConversionResult Convert(Game from, double sens, int? dpi, Game? to)
        {
            var st = AppState.Current;
            var r = new ConversionResult { From = from.Name };

            if (IsFortnite(from) && sens > 0 && sens < 1)
            {
                r.Note = $"Interpretei {sens.ToString("0.###", Inv)} como {(sens * 100).ToString("0.##", Inv)}% (o Fortnite mostra a sens em porcentagem).";
                sens *= 100;
            }

            r.Sens = sens;
            r.SensText = FormatSens(from, sens);
            r.DpiFromBase = dpi is null or <= 0;
            r.Dpi = r.DpiFromBase ? st.Dpi : dpi!.Value;
            r.Cm360 = Math.Round(SensMath.Cm360(r.Dpi, sens, from), 1);

            var targets = to != null && to != from ? new[] { to } : Games.All.Where(g => g != from).ToArray();
            foreach (var g in targets)
            {
                double conv = SensMath.Convert(sens, from, g);
                r.Items.Add(new ConvertedSens
                {
                    Game = g.Name,
                    Sens = Math.Round(conv, IsFortnite(g) ? 2 : 3),
                    SensText = FormatSens(g, conv),
                    EDpi = Math.Round(r.Dpi * (IsFortnite(g) ? conv / 100 : conv), 1),
                    Highlight = to != null,
                });
            }
            return r;
        }

        public static JsonObject ConversionToJson(ConversionResult r) => new()
        {
            ["jogo_origem"] = r.From,
            ["sens_origem"] = r.SensText,
            ["dpi"] = r.Dpi,
            ["dpi_veio_da_base_do_usuario"] = r.DpiFromBase,
            ["cm360"] = r.Cm360,
            ["observacao"] = r.Note,
            ["resultados"] = new JsonArray(r.Items.Select(i => (JsonNode)new JsonObject
            {
                ["jogo"] = i.Game,
                ["sens"] = i.SensText,
                ["edpi"] = i.EDpi,
            }).ToArray()),
            ["mostrado_ao_usuario"] = "Os resultados já aparecem num cartão com botão de copiar. Não repita a tabela inteira; comente em uma ou duas frases.",
        };

        /// <summary>Resumo do setup atual para a IA entender o contexto.</summary>
        public static JsonObject SetupSummary()
        {
            var st = AppState.Current;
            var applied = st.Applied;
            var o = new JsonObject
            {
                ["jogo_principal"] = st.Game.Name,
                ["sens_no_jogo"] = FormatSens(st.Game, st.Sens),
                ["dpi"] = st.Dpi,
                ["cm360"] = Math.Round(st.Cm360, 1),
                ["driver"] = DriverService.StatusText(DriverService.Status),
                ["curva_aplicada"] = applied == null ? "nenhuma (sem aceleração)" : applied.Describe(),
                ["curva_no_editor"] = st.Profile.Describe(),
                ["aceleracao_do_windows_ligada"] = SystemCheck.IsEnhancePointerPrecisionOn(),
            };
            if (applied != null && CurveMath.CanPlot(applied.X))
            {
                o["multiplicador_a_4_cm_s"] = Math.Round(applied.MultX(4) * applied.SensMultiplier, 2);
                o["multiplicador_a_25_cm_s"] = Math.Round(applied.MultX(25) * applied.SensMultiplier, 2);
                o["multiplicador_a_60_cm_s"] = Math.Round(applied.MultX(60) * applied.SensMultiplier, 2);
                o["multiplicador_a_100_cm_s"] = Math.Round(applied.MultX(100) * applied.SensMultiplier, 2);
                o["vertical_y_x"] = applied.YxRatio;
                o["rotacao_graus"] = applied.Rotation;
                o["angle_snapping_graus"] = applied.SnapDegrees;
            }
            var rep = st.LastReport;
            if (rep != null)
            {
                o["teste_de_perfil"] = new JsonObject
                {
                    ["data"] = rep.Date.ToString("dd/MM/yyyy"),
                    ["estilo"] = rep.Style,
                    ["microajuste_pct"] = Math.Round(rep.MicroPct),
                    ["tracking_pct"] = Math.Round(rep.TrackPct),
                    ["flick_pct"] = Math.Round(rep.FlickPct),
                    ["flicks_por_minuto"] = Math.Round(rep.FlicksPerMin, 1),
                    ["pico_tipico_flick_cm_s"] = Math.Round(rep.FlickPeakMedian),
                    ["vertical_pct"] = Math.Round(rep.VerticalShare * 100),
                    ["curva_recomendada"] = rep.Recommended?.Describe(),
                };
            }
            var tests = st.AimResults.Where(r => r.Kind == "teste").TakeLast(5).ToList();
            if (tests.Count > 0)
                o["ultimos_aim_tests"] = new JsonArray(tests.Select(t => (JsonNode)$"{t.Date:dd/MM} {t.Score:0} pts, {t.Accuracy * 100:0}% precisão, curva: {t.Curve}").ToArray());
            return o;
        }

        /// <summary>
        /// Monta uma PROPOSTA de mudança na curva. Nada é aplicado aqui: a pessoa precisa clicar em Aplicar.
        /// </summary>
        public static CurveProposal? ProposeChange(JsonObject args, out string? error)
        {
            error = null;
            var st = AppState.Current;
            var before = (st.Applied ?? st.Profile).Clone();
            before.Dpi = st.Dpi;
            var p = before.Clone();
            var changes = new List<string>();

            string? style = args["estilo"]?.GetValue<string>()?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(style))
            {
                switch (Games.Normalize(style))
                {
                    case "sem aceleracao":
                    case "nenhuma":
                    case "none":
                        p.X = AxisCurve.NoAccel(); p.SeparateY = false; changes.Add("Curva: sem aceleração"); break;
                    case "linear":
                        if (p.X.Style != CurveStyle.Linear) { p.X = AxisCurve.Linear(StartOf(p.X), 0.03, MaxOf(p.X)); changes.Add("Estilo: Linear"); }
                        break;
                    case "natural":
                        if (p.X.Style != CurveStyle.Natural) { p.X = AxisCurve.Natural(StartOf(p.X), 0.15, MaxOf(p.X)); changes.Add("Estilo: Natural"); }
                        break;
                    case "flicker":
                    case "tracker":
                    case "hibrido":
                        string name = style.StartsWith("f") ? "Flicker" : style.StartsWith("t") ? "Tracker" : "Híbrido";
                        var preset = StylePreset(name, st.Dpi);
                        p.X = preset.X; p.YxRatio = preset.YxRatio; p.SeparateY = false;
                        changes.Add($"Estilo pronto: {name}");
                        break;
                }
            }

            if (Num(args, "comeca_em_cm_s") is double start)
            {
                start = Math.Clamp(start, 0, 40);
                if (p.X.Style is CurveStyle.Linear or CurveStyle.Natural)
                {
                    changes.Add($"Começa a acelerar em {StartOf(p.X):0.#} → {start:0.#} cm/s");
                    p.X.InputOffset = start / SensMath.CmPerInch;
                }
            }

            if (Num(args, "sens_maxima") is double max)
            {
                max = Math.Clamp(max, 1.05, 3.0);
                if (p.X.Style == CurveStyle.Linear) { changes.Add($"Sens máxima {p.X.CapY:0.00}x → {max:0.00}x"); p.X.CapMode = "output"; p.X.CapY = max; }
                else if (p.X.Style == CurveStyle.Natural) { changes.Add($"Sens máxima {p.X.Limit:0.00}x → {max:0.00}x"); p.X.Limit = max; }
                else if (p.X.Style == CurveStyle.None) { p.X = AxisCurve.Linear(5, 0.025, max); changes.Add($"Curva Linear começando em 5 cm/s, máximo {max:0.00}x"); }
            }

            if (Num(args, "forca_percentual") is double pct)
            {
                pct = Math.Clamp(pct, -80, 200);
                double f = 1 + pct / 100;
                if (p.X.Style == CurveStyle.Linear) { p.X.Acceleration = Math.Clamp(p.X.Acceleration * f, 0.001, 1); changes.Add($"Força {(pct >= 0 ? "+" : "")}{pct:0}%"); }
                else if (p.X.Style == CurveStyle.Natural) { p.X.DecayRate = Math.Clamp(p.X.DecayRate * f, 0.005, 2); changes.Add($"Velocidade da subida {(pct >= 0 ? "+" : "")}{pct:0}%"); }
            }

            if (Num(args, "vertical") is double yx)
            {
                yx = Math.Round(Math.Clamp(yx, 0.5, 1.5), 2);
                changes.Add($"Vertical {p.YxRatio:0.00}x → {yx:0.00}x");
                p.YxRatio = yx;
            }

            if (Num(args, "rotacao") is double rot)
            {
                rot = Math.Round(Math.Clamp(rot, -15, 15), 1);
                changes.Add($"Rotação {p.Rotation:0.#}° → {rot:0.#}°");
                p.Rotation = rot;
            }

            if (Num(args, "angle_snapping") is double snap)
            {
                snap = Math.Round(Math.Clamp(snap, 0, 45));
                changes.Add($"Angle snapping {p.SnapDegrees:0}° → {snap:0}°");
                p.SnapDegrees = snap;
            }

            if (changes.Count == 0)
            {
                error = "Nenhuma mudança válida foi pedida (ou a curva atual não aceita esse ajuste).";
                return null;
            }

            p.Name = "HarleAccel";
            string? invalid = RawAccelJson.Validate(p);
            if (invalid != null) { error = invalid; return null; }

            return new CurveProposal
            {
                Before = before,
                After = p,
                Changes = changes,
                Reason = args["motivo"]?.GetValue<string>() ?? "",
            };
        }

        static double StartOf(AxisCurve a) =>
            a.Style is CurveStyle.Linear or CurveStyle.Natural ? a.InputOffset * SensMath.CmPerInch : 5;

        static double MaxOf(AxisCurve a) =>
            a.Style == CurveStyle.Linear ? a.CapY : a.Style == CurveStyle.Natural ? a.Limit : 1.5;

        static double? Num(JsonObject o, string key)
        {
            var n = o[key];
            if (n == null) return null;
            try { return n.GetValue<double>(); } catch { }
            try { return n.GetValue<int>(); } catch { }
            try
            {
                var s = n.GetValue<string>().Replace(',', '.').Replace("x", "").Replace("%", "").Trim();
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            }
            catch { }
            return null;
        }

        /// <summary>Estilos prontos (os mesmos da tela Curva).</summary>
        public static AccelProfile StylePreset(string style, int dpi) => style switch
        {
            "Flicker" => new AccelProfile { Name = "Estilo Flicker", Dpi = dpi, X = AxisCurve.Linear(6, 0.04, 1.8) },
            "Tracker" => new AccelProfile { Name = "Estilo Tracker", Dpi = dpi, X = AxisCurve.Natural(10, 0.08, 1.3), YxRatio = 0.9 },
            _ => new AccelProfile { Name = "Estilo Híbrido", Dpi = dpi, X = AxisCurve.Linear(5, 0.025, 1.5) },
        };

        /// <summary>Explicação simples da curva aplicada (usada no modo sem IA).</summary>
        public static string ExplainApplied()
        {
            var st = AppState.Current;
            var p = st.Applied;
            if (p == null || p.IsNeutral) return "Agora você está sem aceleração: a sua sens é sempre a mesma do jogo, em qualquer velocidade.";
            if (!CurveMath.CanPlot(p.X)) return $"A sua curva é do tipo “{p.X.Mode}”, importada do Raw Accel. Ela está aplicada normalmente.";
            double sm = p.SensMultiplier;
            string s = $"Sua curva: {p.Describe()}.\n" +
                       $"• Microajuste (4 cm/s): {p.MultX(4) * sm:0.00}x\n" +
                       $"• Controle (25 cm/s): {p.MultX(25) * sm:0.00}x\n" +
                       $"• Flick (60 cm/s): {p.MultX(60) * sm:0.00}x\n" +
                       $"• Flick forte (100 cm/s): {p.MultX(100) * sm:0.00}x";
            if (Math.Abs(p.YxRatio - 1) > 1e-6) s += $"\nA vertical fica em {p.YxRatio:0.00}x da horizontal.";
            return s;
        }
    }

    // ====================================================================== entendimento local (sem IA)

    /// <summary>
    /// Entende os pedidos mais comuns sem internet e sem chave,
    /// por exemplo "8% fortnite 800 dpi pro valorant" ou "converte 0.4 do valo pro cs2".
    /// </summary>
    public static class LocalParser
    {
        public sealed record Request(Game From, double Sens, int? Dpi, Game? To);

        static readonly string[] IntentWords =
        {
            "convert", "passa", "passar", "pro ", "pra ", "para ", "equivalente", "quanto", "cm/360", "cm360",
            "fica", "mesma sens", "igual", "transforma", "traduz", "no ", "em ",
        };

        static readonly string[] TargetPrefixes = { "pro ", "pra ", "para o ", "para ", "pro o ", "to ", "-> ", "→ ", "p/ " };

        public static Request? TryParse(string text)
        {
            string t = " " + Games.Normalize(text) + " ";

            // jogos citados (apelidos mais longos primeiro), sem sobreposição
            var taken = new bool[t.Length];
            var mentions = new List<(int Pos, int End, Game Game)>();
            foreach (var (alias, gameName) in Games.Aliases.OrderByDescending(a => a.Alias.Length))
            {
                foreach (Match m in Regex.Matches(t, $@"(?<![a-z0-9]){Regex.Escape(alias)}(?![a-z0-9])"))
                {
                    if (Enumerable.Range(m.Index, m.Length).Any(i => taken[i])) continue;
                    for (int i = m.Index; i < m.Index + m.Length; i++) taken[i] = true;
                    mentions.Add((m.Index, m.Index + m.Length, Games.ByName(gameName)));
                }
            }
            mentions = mentions.OrderBy(m => m.Pos).ToList();
            var games = mentions.Select(m => m.Game).Distinct().ToList();
            if (games.Count == 0) return null;

            // DPI
            int? dpi = null;
            var dm = Regex.Match(t, @"(\d{3,5})\s*(?:de\s*)?dpi|dpi\s*(?:de|:|=)?\s*(\d{3,5})");
            if (dm.Success)
            {
                dpi = int.Parse(dm.Groups[1].Success && dm.Groups[1].Value != "" ? dm.Groups[1].Value : dm.Groups[2].Value);
                for (int i = dm.Index; i < dm.Index + dm.Length; i++) taken[i] = true;
            }

            // números restantes
            var nums = new List<(double V, bool Pct)>();
            foreach (Match m in Regex.Matches(t, @"(?<![\d/])(\d+(?:[.,]\d+)?)\s*(%)?"))
            {
                if (taken[m.Index]) continue;
                if (m.Index > 0 && t[m.Index - 1] == '/') continue; // cm/360
                if (!double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) continue;
                nums.Add((v, m.Groups[2].Success));
            }

            if (dpi == null)
            {
                var big = nums.FirstOrDefault(n => !n.Pct && n.V >= 100 && n.V <= 64000 && n.V == Math.Floor(n.V));
                if (big.V > 0 && nums.Count >= 2) { dpi = (int)big.V; nums.Remove(big); }
            }

            var sensCand = nums.Where(n => n.V > 0 && (n.Pct || n.V < 100)).ToList();
            if (sensCand.Count == 0) return null;
            double sens = sensCand[0].V;

            bool intent = games.Count >= 2 || IntentWords.Any(w => t.Contains(w));
            if (!intent) return null;

            // descobre o destino: jogo precedido de "pro", "pra", "para"...
            Game? to = null;
            Game? from = null;
            foreach (var m in mentions)
            {
                string before = t.Substring(Math.Max(0, m.Pos - 8), Math.Min(8, m.Pos));
                if (to == null && TargetPrefixes.Any(p => before.EndsWith(p))) to = m.Game;
            }
            from = mentions.Select(m => m.Game).FirstOrDefault(g => g != to);

            if (from == null)
            {
                // só citou o destino: usa o jogo principal da pessoa como origem
                var baseGame = AppState.Current.Game;
                if (to != null && baseGame != to) from = baseGame;
                else return null;
            }
            if (to == null && games.Count >= 2) to = mentions.Select(m => m.Game).Last(g => g != from);

            return new Request(from, sens, dpi, to);
        }

        public static bool AsksToExplainCurve(string text)
        {
            string t = Games.Normalize(text);
            return (t.Contains("explica") || t.Contains("como esta") || t.Contains("o que faz") || t.Contains("qual e"))
                   && (t.Contains("curva") || t.Contains("aceleracao"));
        }
    }
}
