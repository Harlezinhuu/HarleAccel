// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;

namespace HarleAccel.Core
{
    /// <summary>Resultado de um aim test.</summary>
    public sealed class AimResult
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public string Kind { get; set; } = "teste";   // "teste" | "A/B"
        public string Curve { get; set; } = "";       // descrição da curva usada
        public string CurveKey { get; set; } = "";    // JSON da curva (para agrupar)
        public int Hits { get; set; }
        public int Misses { get; set; }
        public double Seconds { get; set; }
        public double AvgMsPerHit { get; set; }

        public double Accuracy => Hits + Misses == 0 ? 0 : (double)Hits / (Hits + Misses);

        /// <summary>Pontuação: acertos por minuto ponderados pela precisão.</summary>
        public double Score => Seconds <= 0 ? 0 : Math.Round(Hits / Seconds * 60 * Accuracy * 10);
    }

    /// <summary>Uma sessão de treino registrada.</summary>
    public sealed class TrainingSession
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public string Platform { get; set; } = "Aim Lab";
        public string Routine { get; set; } = "";
        public List<TrainingScore> Scores { get; set; } = new();
    }

    public sealed class TrainingScore
    {
        public string Scenario { get; set; } = "";
        public double Score { get; set; }
    }

    /// <summary>Configurações gerais.</summary>
    public sealed class AppSettings
    {
        public bool CheckUpdates { get; set; } = true;

        /// <summary>Reaplicar a curva quando o Windows inicia (o driver não guarda a curva sozinho).</summary>
        public bool ApplyOnStartup { get; set; } = true;

        /// <summary>Vidro fosco: desfoca o que está atrás da janela.</summary>
        public bool GlassBlur { get; set; } = true;

        /// <summary>Quanto o fundo da janela cobre (0.5 = bem transparente, 1 = sólido).</summary>
        public double WindowOpacity { get; set; } = 0.75;
        public bool DiscordEnabled { get; set; }
        public string DiscordClientId { get; set; } = "";
        public string RoutinesUrl { get; set; } = UpdateChecker.DefaultRoutinesUrl;

        /// <summary>Chave do Gemini criptografada com o DPAPI do Windows (só abre neste usuário/PC).</summary>
        public string GeminiKeyProtected { get; set; } = "";
        public string GeminiModel { get; set; } = GeminiClient.DefaultModel;
    }
}
