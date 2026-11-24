// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System.Collections.Generic;
using System.Linq;

namespace HarleAccel.Core
{
    /// <summary>Categoria usada para dizer se a sensibilidade está baixa, média ou alta.</summary>
    public enum GameKind { Tatico, BattleRoyale, Hero }

    /// <summary>
    /// Um jogo e o "yaw" dele: quantos graus a câmera gira por contagem do mouse com sens 1.
    /// Fontes: valores públicos usados pelos conversores de sensibilidade (mouse-sensitivity, csdb etc).
    /// </summary>
    public sealed record Game(string Name, double Yaw, GameKind Kind, string SensHint);

    public static class Games
    {
        public static readonly IReadOnlyList<Game> All = new List<Game>
        {
            new("Valorant",              0.07,     GameKind.Tatico,       "ex.: 0.40"),
            new("Counter-Strike 2",      0.022,    GameKind.Tatico,       "ex.: 1.20"),
            new("Fortnite (%)",          0.005555, GameKind.BattleRoyale, "em %, ex.: 8.0"),
            new("Apex Legends",          0.022,    GameKind.BattleRoyale, "ex.: 1.50"),
            new("Overwatch 2",           0.0066,   GameKind.Hero,         "ex.: 5.00"),
            new("CoD: Warzone / BO6",    0.0066,   GameKind.BattleRoyale, "ex.: 6.00"),
            new("Destiny 2",             0.0066,   GameKind.Hero,         "ex.: 6.00"),
            new("Deadlock",              0.022,    GameKind.Hero,         "ex.: 1.50"),
            new("KovaaK's (escala CS)",  0.022,    GameKind.Tatico,       "ex.: 1.20"),
        };

        public static Game ByName(string? name) =>
            All.FirstOrDefault(g => g.Name == name) ?? All[0];

        /// <summary>Apelidos que as pessoas usam no chat (já em minúsculas e sem acento).</summary>
        public static readonly IReadOnlyList<(string Alias, string Game)> Aliases = new List<(string, string)>
        {
            ("valorant", "Valorant"), ("valo", "Valorant"), ("val", "Valorant"),
            ("counter-strike 2", "Counter-Strike 2"), ("counter strike", "Counter-Strike 2"), ("cs2", "Counter-Strike 2"),
            ("csgo", "Counter-Strike 2"), ("cs:go", "Counter-Strike 2"), ("cs", "Counter-Strike 2"),
            ("fortnite", "Fortnite (%)"), ("fort", "Fortnite (%)"), ("fn", "Fortnite (%)"),
            ("apex legends", "Apex Legends"), ("apex", "Apex Legends"),
            ("overwatch 2", "Overwatch 2"), ("overwatch", "Overwatch 2"), ("ow2", "Overwatch 2"), ("ow", "Overwatch 2"),
            ("warzone", "CoD: Warzone / BO6"), ("black ops 6", "CoD: Warzone / BO6"), ("bo6", "CoD: Warzone / BO6"),
            ("call of duty", "CoD: Warzone / BO6"), ("cod", "CoD: Warzone / BO6"),
            ("destiny 2", "Destiny 2"), ("destiny", "Destiny 2"),
            ("deadlock", "Deadlock"),
            ("kovaaks", "KovaaK's (escala CS)"), ("kovaak", "KovaaK's (escala CS)"), ("kovacs", "KovaaK's (escala CS)"),
        };

        /// <summary>Acha um jogo pelo nome ou apelido (ex.: "valo", "fn", "cs2").</summary>
        public static Game? Find(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = Normalize(text);
            var exact = All.FirstOrDefault(g => Normalize(g.Name) == t);
            if (exact != null) return exact;
            foreach (var (alias, game) in Aliases)
                if (t == alias || t.StartsWith(alias + " ") || t.Contains(alias)) return ByName(game);
            return null;
        }

        public static string Normalize(string s)
        {
            var d = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in d)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Trim();
        }
    }
}
