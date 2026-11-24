// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;

namespace HarleAccel.Core
{
    public static class SensMath
    {
        public const double CmPerInch = 2.54;

        /// <summary>Centímetros de mouse para girar 360° no jogo.</summary>
        public static double Cm360(double dpi, double sens, double yaw)
        {
            double d = dpi * sens * yaw;
            return d <= 0 ? double.NaN : 360.0 * CmPerInch / d;
        }

        public static double Cm360(double dpi, double sens, Game game) => Cm360(dpi, sens, game.Yaw);

        public static double EDpi(double dpi, double sens) => dpi * sens;

        /// <summary>Converte a sens mantendo o mesmo cm/360 (mesma DPI).</summary>
        public static double Convert(double sens, Game from, Game to) => sens * from.Yaw / to.Yaw;

        /// <summary>Sens necessária no jogo para chegar num cm/360.</summary>
        public static double SensForCm360(double cm360, double dpi, Game game) =>
            360.0 * CmPerInch / (cm360 * dpi * game.Yaw);

        /// <summary>
        /// Faixa aproximada (não é regra): jogos táticos costumam usar sens mais baixa
        /// que battle royale e hero shooters.
        /// </summary>
        public static (string Label, string Detail) Classify(double cm360, GameKind kind)
        {
            if (double.IsNaN(cm360) || double.IsInfinity(cm360)) return ("—", "Preencha DPI e sensibilidade.");

            (double high, double low) = kind switch
            {
                GameKind.Tatico => (25.0, 45.0),
                _ => (20.0, 35.0),
            };

            if (cm360 < high)
                return ("ALTA", $"Abaixo de ~{high:0} cm/360 é considerado rápido para esse tipo de jogo. Ótimo para virar rápido, mais difícil para microajuste.");
            if (cm360 > low)
                return ("BAIXA", $"Acima de ~{low:0} cm/360 é considerado lento. Muito controle na mira, mas exige movimentos grandes de braço.");
            return ("MÉDIA", $"Entre ~{high:0} e ~{low:0} cm/360: faixa equilibrada entre controle e velocidade.");
        }

        public static string Fmt(double v, int decimals = 2) =>
            double.IsNaN(v) || double.IsInfinity(v) ? "—"
            : Math.Round(v, decimals).ToString(decimals <= 0 ? "0" : "0." + new string('#', decimals));
    }
}
