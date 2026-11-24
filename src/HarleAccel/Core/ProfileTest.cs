// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HarleAccel.Core
{
    /// <summary>Movimento somado numa janela de 10 ms.</summary>
    public readonly record struct MoveBin(double T, int Dx, int Dy);

    /// <summary>Resultado do Teste de Perfil (fica salvo).</summary>
    public sealed class ProfileReport
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public int Dpi { get; set; }
        public double MovingSeconds { get; set; }
        public double ActiveSeconds { get; set; }

        public double MicroPct { get; set; }
        public double TrackPct { get; set; }
        public double FlickPct { get; set; }

        public double P50 { get; set; }
        public double P90 { get; set; }
        public double P95 { get; set; }
        public double P99 { get; set; }

        public int FlickCount { get; set; }
        public double FlicksPerMin { get; set; }
        public double FlickPeakMedian { get; set; }

        public double VerticalShare { get; set; }
        public double VerticalMicroPct { get; set; }
        public double TiltDeg { get; set; }
        public int TiltSamples { get; set; }

        /// <summary>Fração do tempo em movimento por faixa de velocidade (bordas em <see cref="ProfileAnalyzer.Edges"/>).</summary>
        public double[] Histogram { get; set; } = Array.Empty<double>();

        public string Style { get; set; } = "Híbrido";
        public List<string> Insights { get; set; } = new();

        public AccelProfile? Recommended { get; set; }
        public List<string> RecommendationWhy { get; set; } = new();
    }

    /// <summary>
    /// Analisa como a pessoa mexe o mouse e recomenda uma curva.
    /// As regras são heurísticas (pontos de partida), e o teste A/B existe para confirmar.
    /// </summary>
    public static class ProfileAnalyzer
    {
        public const double BinSeconds = 0.010;

        /// <summary>Bordas do histograma em cm/s.</summary>
        public static readonly double[] Edges = { 0, 1, 2, 3, 4, 6, 8, 10, 13, 16, 20, 25, 30, 40, 50, 60, 80, 100, 130, 160, 200, 300 };

        public const double MinMovingSeconds = 300; // 5 minutos de mouse em movimento
        public const int MinFlicks = 80;

        /// <summary>Progresso (0 a 1) até ter dados suficientes.</summary>
        public static double Progress(double movingSeconds, int flicks) =>
            0.5 * Math.Min(1.0, movingSeconds / MinMovingSeconds) + 0.5 * Math.Min(1.0, flicks / (double)MinFlicks);

        /// <summary>
        /// Velocidade da mão (cm/s) a partir dos counts. Se havia uma curva ativa durante o teste,
        /// <paramref name="activeCurve"/> desfaz o efeito dela (o driver já entrega counts acelerados).
        /// </summary>
        public static double HandSpeed(double counts, int dpi, AccelProfile? activeCurve)
        {
            double observed = counts / dpi * SensMath.CmPerInch / BinSeconds;
            if (activeCurve == null || activeCurve.IsNeutral) return observed;

            // observed = v * m(v) * sensMult -> resolve v por bisseção
            double sm = activeCurve.SensMultiplier > 0 ? activeCurve.SensMultiplier : 1;
            double lo = 0, hi = Math.Max(1, observed * 4);
            for (int i = 0; i < 50; i++)
            {
                double mid = (lo + hi) / 2;
                double m = activeCurve.MultX(mid);
                if (double.IsNaN(m)) return observed;
                if (mid * m * sm > observed) hi = mid; else lo = mid;
            }
            return (lo + hi) / 2;
        }

        public static ProfileReport Analyze(IReadOnlyList<MoveBin> bins, int dpi, double activeSeconds,
                                            AccelProfile? activeCurve, Game game, double baseCm360)
        {
            var r = new ProfileReport { Dpi = dpi, ActiveSeconds = activeSeconds };
            var speeds = new List<double>(bins.Count);
            double sumAbsX = 0, sumAbsY = 0;
            int vertMicro = 0, vertMoving = 0;
            double tiltWeighted = 0, tiltWeight = 0;
            int tiltN = 0;

            foreach (var b in bins)
            {
                double counts = Math.Sqrt((double)b.Dx * b.Dx + (double)b.Dy * b.Dy);
                if (counts <= 0) continue;
                double v = HandSpeed(counts, dpi, activeCurve);
                speeds.Add(v);
                sumAbsX += Math.Abs(b.Dx);
                sumAbsY += Math.Abs(b.Dy);

                if (b.Dy != 0)
                {
                    vertMoving++;
                    if (Math.Abs(b.Dy) <= Math.Abs(b.Dx) * 0.6 || v < 8) vertMicro++;
                }

                // inclinação natural da mão: só movimentos quase horizontais e com distância suficiente
                if (Math.Abs(b.Dx) >= 4)
                {
                    double ang = Math.Atan((double)b.Dy / b.Dx) * 180 / Math.PI;
                    if (Math.Abs(ang) <= 20)
                    {
                        tiltWeighted += ang * counts;
                        tiltWeight += counts;
                        tiltN++;
                    }
                }
            }

            r.MovingSeconds = speeds.Count * BinSeconds;
            if (speeds.Count == 0) return r;

            var sorted = speeds.OrderBy(v => v).ToArray();
            double P(double q) => sorted[Math.Clamp((int)Math.Round(q * (sorted.Length - 1)), 0, sorted.Length - 1)];
            r.P50 = P(0.50); r.P90 = P(0.90); r.P95 = P(0.95); r.P99 = P(0.99);

            r.MicroPct = 100.0 * speeds.Count(v => v < 8) / speeds.Count;
            r.FlickPct = 100.0 * speeds.Count(v => v >= 40) / speeds.Count;
            r.TrackPct = 100.0 - r.MicroPct - r.FlickPct;

            // histograma
            var hist = new double[Edges.Length - 1];
            foreach (var v in speeds)
            {
                int i = Array.FindLastIndex(Edges, e => v >= e);
                if (i >= hist.Length) i = hist.Length - 1;
                if (i >= 0) hist[i] += 1;
            }
            r.Histogram = hist.Select(h => h / speeds.Count).ToArray();

            // flicks: trechos contínuos de movimento cujo pico passa de 40 cm/s
            var peaks = new List<double>();
            double segPeak = 0;
            double lastT = double.NegativeInfinity;
            int idx = 0;
            foreach (var b in bins)
            {
                double counts = Math.Sqrt((double)b.Dx * b.Dx + (double)b.Dy * b.Dy);
                if (counts <= 0) continue;
                double v = speeds[idx++];
                bool contiguous = b.T - lastT <= BinSeconds * 3.5;
                if (!contiguous || v < 2)
                {
                    if (segPeak >= 40) peaks.Add(segPeak);
                    segPeak = 0;
                }
                if (v >= 2) segPeak = Math.Max(segPeak, v);
                lastT = b.T;
            }
            if (segPeak >= 40) peaks.Add(segPeak);
            r.FlickCount = peaks.Count;
            r.FlicksPerMin = activeSeconds > 0 ? peaks.Count / (activeSeconds / 60.0) : 0;
            r.FlickPeakMedian = peaks.Count > 0 ? peaks.OrderBy(x => x).ElementAt(peaks.Count / 2) : 0;

            r.VerticalShare = (sumAbsX + sumAbsY) > 0 ? sumAbsY / (sumAbsX + sumAbsY) : 0;
            r.VerticalMicroPct = vertMoving > 0 ? 100.0 * vertMicro / vertMoving : 0;
            r.TiltSamples = tiltN;
            r.TiltDeg = tiltWeight > 0 ? tiltWeighted / tiltWeight : 0;

            Recommend(r, game, baseCm360);
            return r;
        }

        // ------------------------------------------------------------------ recomendação

        static string F(double v, string fmt = "0") => v.ToString(fmt, CultureInfo.InvariantCulture);

        static void Recommend(ProfileReport r, Game game, double baseCm360)
        {
            // estilo
            if (r.FlicksPerMin >= 15) r.Style = "Flicker";
            else if (r.FlicksPerMin < 6 && r.TrackPct >= 50) r.Style = "Tracker";
            else r.Style = "Híbrido";

            r.Insights.Clear();
            r.Insights.Add($"{F(r.MicroPct)}% do tempo em microajuste, {F(r.TrackPct)}% em tracking/controle e {F(r.FlickPct)}% em flicks.");
            r.Insights.Add($"{F(r.FlicksPerMin, "0.#")} flicks por minuto, com pico típico de {F(r.FlickPeakMedian)} cm/s.");
            if (r.MicroPct >= 40) r.Insights.Add("Microajuste forte: boa parte da sua mira é feita com movimentos bem lentos. A curva precisa deixar essa faixa intocada.");
            if (r.P99 >= 120) r.Insights.Add("Você faz movimentos muito rápidos de braço: aceleração ajuda a cobrir essa distância com menos esforço.");
            r.Insights.Add($"O vertical é {F(r.VerticalShare * 100)}% do seu movimento total.");

            // curva base: Linear (gain) mantendo a faixa lenta em 1x
            double start = Math.Clamp(r.P50, 3, 15);
            double max = r.Style switch { "Flicker" => 1.7, "Tracker" => 1.3, _ => 1.45 };
            if (baseCm360 > 45) max += 0.2;
            else if (baseCm360 < 25) max -= 0.15;
            max = Math.Clamp(max, 1.15, 2.2);

            // velocidade onde a curva deve "trabalhar": os flicks típicos
            double target = Math.Max(Math.Max(r.FlickPeakMedian, r.P99 * 0.8), start + 20);
            double wanted = 1 + 0.75 * (max - 1);
            double lo = 0.0005, hi = 2.0;
            for (int i = 0; i < 60; i++)
            {
                double mid = Math.Sqrt(lo * hi);
                double m = CurveMath.MultiplierAtCmS(AxisCurve.Linear(start, mid, max), target);
                if (m < wanted) lo = mid; else hi = mid;
            }
            double accel = Math.Round(Math.Sqrt(lo * hi), 4);

            var rec = new AccelProfile
            {
                Name = "Recomendada pelo Teste de Perfil",
                Dpi = r.Dpi,
                X = AxisCurve.Linear(start, accel, max),
            };

            var why = new List<string>
            {
                $"Estilo detectado: {r.Style}.",
                $"A aceleração começa em {F(start, "0.#")} cm/s: metade do seu movimento fica abaixo disso e continua 1x (sua sens normal).",
                $"Teto de {F(max, "0.00")}x, porque {(r.Style == "Flicker" ? "você vive de flicks e se beneficia de virar mais rápido" : r.Style == "Tracker" ? "tracker precisa de consistência, então a curva é contida" : "você mistura tracking e flicks")}" +
                    (baseCm360 > 45 ? ", e a sua sens é baixa (mais espaço para ganhar)." : baseCm360 < 25 ? ", e a sua sens já é alta (menos aceleração)." : "."),
                $"Nos seus flicks típicos (~{F(target)} cm/s) a sens chega perto de {F(wanted, "0.00")}x.",
            };

            // split X/Y
            if (r.VerticalShare < 0.20) { rec.YxRatio = 0.85; why.Add($"Vertical em 0.85x: só {F(r.VerticalShare * 100)}% do seu movimento é vertical, então um vertical mais calmo ajuda a segurar a altura da mira."); }
            else if (r.VerticalShare < 0.30) { rec.YxRatio = 0.9; why.Add($"Vertical em 0.90x: o vertical é {F(r.VerticalShare * 100)}% do seu movimento, quase tudo ajuste fino."); }
            else why.Add("Vertical igual ao horizontal: você usa bastante o eixo vertical.");

            // rotação
            if (r.TiltSamples >= 300 && Math.Abs(r.TiltDeg) >= 2)
            {
                rec.Rotation = Math.Round(Math.Clamp(-r.TiltDeg, -10, 10), 1);
                why.Add($"Rotação de {F(rec.Rotation, "0.#")}°: quando você mexe \"reto pro lado\", a sua mão inclina uns {F(Math.Abs(r.TiltDeg), "0.#")}°. A rotação compensa isso.");
            }

            r.Recommended = rec;
            r.RecommendationWhy = why;
        }
    }
}
