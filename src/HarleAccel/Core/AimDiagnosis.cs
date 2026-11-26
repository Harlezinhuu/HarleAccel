// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Linq;

namespace HarleAccel.Core
{
    /// <summary>Como o mouse chegou num alvo.</summary>
    public enum Approach { Direct, Over, Under, Unknown }

    /// <summary>
    /// Diagnóstico de sensibilidade pelo caminho do mouse:
    /// - passou do alvo e voltou (overshoot) = sens alta naquela velocidade;
    /// - parou antes e completou no microajuste (undershoot) = sens baixa naquela velocidade;
    /// - caiu direto no alvo = no ponto.
    /// Alvos "perto" medem a sens base (movimento lento) e alvos "longe" medem os flicks (onde a aceleração age).
    /// </summary>
    public static class AimDiagnosis
    {
        public readonly record struct Sample(double T, double X, double Y);

        /// <summary>Distância (px) que separa alvo perto de alvo longe no aim test.</summary>
        public const double NearMaxPx = 240;

        /// <summary>
        /// Classifica a aproximação de um alvo.
        /// start = onde o cursor estava quando o alvo apareceu; target = centro; radius = raio do alvo.
        /// </summary>
        public static Approach Classify(IReadOnlyList<Sample> path, double startX, double startY,
                                        double targetX, double targetY, double radius)
        {
            double dx = targetX - startX, dy = targetY - startY;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < radius * 2.5 || path.Count < 4) return Approach.Unknown;
            double ux = dx / dist, uy = dy / dist;

            // progresso na direção do alvo e velocidade em cada ponto
            var prog = new double[path.Count];
            var speed = new double[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                prog[i] = (path[i].X - startX) * ux + (path[i].Y - startY) * uy;
                if (i > 0)
                {
                    double dt = Math.Max(1e-4, path[i].T - path[i - 1].T);
                    double mx = path[i].X - path[i - 1].X, my = path[i].Y - path[i - 1].Y;
                    speed[i] = Math.Sqrt(mx * mx + my * my) / dt;
                }
            }

            // passou do alvo em algum momento antes de clicar?
            if (prog.Max() > dist + radius) return Approach.Over;

            // fim do movimento principal: primeira desaceleração forte depois do pico de velocidade
            int peak = Array.IndexOf(speed, speed.Max());
            if (peak <= 0) return Approach.Unknown;
            int end = path.Count - 1;
            for (int i = peak + 1; i < path.Count; i++)
                if (speed[i] < speed[peak] * 0.2) { end = i; break; }

            double ex = path[end].X - targetX, ey = path[end].Y - targetY;
            double err = Math.Sqrt(ex * ex + ey * ey);
            if (err <= radius * 1.2) return Approach.Direct;
            return prog[end] < dist ? Approach.Under : Approach.Over;
        }

        public static void Count(AimResult r, Approach a, bool near)
        {
            switch (a)
            {
                case Approach.Over: if (near) r.NearOver++; else r.FarOver++; break;
                case Approach.Under: if (near) r.NearUnder++; else r.FarUnder++; break;
                case Approach.Direct: if (near) r.NearDirect++; else r.FarDirect++; break;
            }
        }

        public enum Verdict { NotEnough, High, Low, Balanced }

        public static Verdict Judge(int over, int under, int direct)
        {
            int n = over + under + direct;
            if (n < 5) return Verdict.NotEnough;
            double o = (double)over / n, u = (double)under / n;
            if (o - u >= 0.2) return Verdict.High;
            if (u - o >= 0.2) return Verdict.Low;
            return Verdict.Balanced;
        }

        /// <summary>Junta vários testes (ex.: as últimas rodadas) num só para julgar com mais dados.</summary>
        public static AimResult Sum(IEnumerable<AimResult> results)
        {
            var s = new AimResult();
            foreach (var r in results)
            {
                s.Hits += r.Hits; s.Misses += r.Misses; s.Seconds += r.Seconds;
                s.NearOver += r.NearOver; s.NearUnder += r.NearUnder; s.NearDirect += r.NearDirect;
                s.FarOver += r.FarOver; s.FarUnder += r.FarUnder; s.FarDirect += r.FarDirect;
            }
            return s;
        }

        static string Pct(int part, int n) => n == 0 ? "0%" : $"{100.0 * part / n:0}%";

        /// <summary>Explicação em português, com sugestão. hasCurve = tem aceleração ativa.</summary>
        public static List<string> Explain(AimResult r, bool hasCurve)
        {
            var lines = new List<string>();
            int nn = r.NearOver + r.NearUnder + r.NearDirect;
            int nf = r.FarOver + r.FarUnder + r.FarDirect;

            var near = Judge(r.NearOver, r.NearUnder, r.NearDirect);
            var far = Judge(r.FarOver, r.FarUnder, r.FarDirect);

            if (near != Verdict.NotEnough)
                lines.Add($"Alvos perto (sens base): passou do alvo em {Pct(r.NearOver, nn)}, parou antes em {Pct(r.NearUnder, nn)}, direto em {Pct(r.NearDirect, nn)}.");
            if (far != Verdict.NotEnough)
                lines.Add($"Alvos longe (flicks): passou do alvo em {Pct(r.FarOver, nf)}, parou antes em {Pct(r.FarUnder, nf)}, direto em {Pct(r.FarDirect, nf)}.");

            switch (near)
            {
                case Verdict.High: lines.Add("→ Nos movimentos curtos você costuma passar do alvo: a sua sens base parece ALTA. Experimente uns 5 a 10% a menos (o Buscador de sens acha o ponto)."); break;
                case Verdict.Low: lines.Add("→ Nos movimentos curtos você costuma parar antes: a sua sens base parece BAIXA. Experimente uns 5 a 10% a mais."); break;
                case Verdict.Balanced: lines.Add("→ Nos movimentos curtos está equilibrado: a sens base parece no ponto."); break;
            }

            switch (far)
            {
                case Verdict.High:
                    lines.Add(hasCurve
                        ? "→ Nos flicks você passa do alvo: a aceleração está forte demais. Diminua a sens máxima ou a força na tela Curva."
                        : "→ Nos flicks você passa do alvo: para movimentos longos a sens está alta.");
                    break;
                case Verdict.Low:
                    lines.Add(hasCurve
                        ? "→ Nos flicks você para antes: dá para subir um pouco a sens máxima da curva."
                        : "→ Nos flicks você para antes: uma curva leve de aceleração ajudaria a chegar sem perder o controle no movimento curto.");
                    break;
                case Verdict.Balanced: lines.Add("→ Nos flicks está equilibrado."); break;
            }

            if (near == Verdict.NotEnough && far == Verdict.NotEnough)
                lines.Add("Poucos alvos para diagnosticar. Faça um teste mais longo.");
            return lines;
        }
    }

    /// <summary>
    /// Buscador de sens no método PSA (Perfect Sensitivity Approximation), às cegas:
    /// cada rodada compara uma versão mais baixa e uma mais alta em volta do ponto atual,
    /// você fica com a melhor e o intervalo fecha pela metade. 5 rodadas = faixa de uns ±2%.
    /// O valor é um multiplicador sobre a sens atual.
    /// </summary>
    public sealed class SensFinder
    {
        public const int Rounds = 5;

        public double Center { get; private set; } = 1.0;
        public double Step { get; private set; } = 0.5;
        public int Round { get; private set; }

        public bool Done => Round >= Rounds;

        public double Low => Math.Clamp(Center * (1 - Step), 0.25, 4);
        public double High => Math.Clamp(Center * (1 + Step), 0.25, 4);

        /// <summary>choice: -1 = a mais baixa, +1 = a mais alta, 0 = iguais (fica no meio).</summary>
        public void Choose(int choice)
        {
            if (Done) return;
            if (choice < 0) Center = Low;
            else if (choice > 0) Center = High;
            Step /= 2;
            Round++;
        }

        /// <summary>Faixa final: a sens boa está aqui dentro.</summary>
        public (double Low, double High) Range => (Center * (1 - Step), Center * (1 + Step));
    }
}
