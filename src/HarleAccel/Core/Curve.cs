// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace HarleAccel.Core
{
    /// <summary>Estilos que o editor do HarleAccel sabe editar.</summary>
    public enum CurveStyle { None, Linear, Natural, Custom, Advanced }

    /// <summary>
    /// Parâmetros de UM eixo, espelhando os campos do Raw Accel (accel_args).
    /// Guardamos tudo para importar/exportar sem perder nada.
    /// Unidade de velocidade interna: polegadas/segundo (in/s), igual ao Raw Accel com DPI configurada.
    /// </summary>
    public sealed class AxisCurve
    {
        public string Mode { get; set; } = "noaccel"; // classic | jump | natural | synchronous | power | lut | noaccel
        public bool Gain { get; set; } = true;
        public double InputOffset { get; set; } = 0;
        public double OutputOffset { get; set; } = 0;
        public double Acceleration { get; set; } = 0.005;
        public double DecayRate { get; set; } = 0.1;
        public double Gamma { get; set; } = 1;
        public double Motivity { get; set; } = 1.5;
        public double ExponentClassic { get; set; } = 2;
        public double Scale { get; set; } = 1;
        public double ExponentPower { get; set; } = 0.05;
        public double Limit { get; set; } = 1.5;
        public double SyncSpeed { get; set; } = 5;
        public double Smooth { get; set; } = 0.5;
        public double CapX { get; set; } = 15;
        public double CapY { get; set; } = 1.5;
        public string CapMode { get; set; } = "output"; // in_out | input | output
        public float[] Data { get; set; } = Array.Empty<float>();

        [JsonIgnore]
        public CurveStyle Style => Mode switch
        {
            "noaccel" => CurveStyle.None,
            "classic" => CurveStyle.Linear,
            "natural" => CurveStyle.Natural,
            "lut" => CurveStyle.Custom,
            _ => CurveStyle.Advanced,
        };

        public AxisCurve Clone()
        {
            var c = (AxisCurve)MemberwiseClone();
            c.Data = (float[])Data.Clone();
            return c;
        }

        // ---------- presets simples (ponto de partida) ----------

        public static AxisCurve NoAccel() => new() { Mode = "noaccel" };

        /// <summary>Linear (classic, expoente 2, modo gain) com teto de sens.</summary>
        public static AxisCurve Linear(double startCmS, double accel, double maxMult) => new()
        {
            Mode = "classic",
            Gain = true,
            ExponentClassic = 2,
            InputOffset = startCmS / SensMath.CmPerInch,
            Acceleration = accel,
            CapMode = "output",
            CapY = maxMult,
        };

        public static AxisCurve Natural(double startCmS, double decay, double maxMult) => new()
        {
            Mode = "natural",
            Gain = true,
            InputOffset = startCmS / SensMath.CmPerInch,
            DecayRate = decay,
            Limit = maxMult,
        };

        // ---------- pontos da curva personalizada ----------

        /// <summary>Pontos (cm/s, multiplicador) da curva personalizada.</summary>
        public List<(double CmS, double Mult)> GetPoints()
        {
            var list = new List<(double, double)>();
            for (int i = 0; i + 1 < Data.Length; i += 2)
            {
                double x = Data[i] * SensMath.CmPerInch;
                double y = Gain ? Data[i + 1] / Math.Max(Data[i], 1e-9f) : Data[i + 1];
                list.Add((x, y));
            }
            return list;
        }

        public void SetPoints(IEnumerable<(double CmS, double Mult)> pts)
        {
            var sorted = pts.Where(p => p.CmS > 0 && p.Mult > 0).OrderBy(p => p.CmS).ToList();
            var data = new List<float>();
            foreach (var p in sorted)
            {
                data.Add((float)(p.CmS / SensMath.CmPerInch));
                data.Add((float)p.Mult);
            }
            Mode = "lut";
            Gain = false; // y = multiplicador direto
            Data = data.ToArray();
        }

        public static string PointsToText(IEnumerable<(double CmS, double Mult)> pts) =>
            string.Join("; ", pts.Select(p =>
                p.CmS.ToString("0.#", CultureInfo.InvariantCulture) + ":" +
                p.Mult.ToString("0.###", CultureInfo.InvariantCulture)));

        /// <summary>Formato: "10:1; 30:1.2; 60:1.5". Retorna null se inválido.</summary>
        public static List<(double, double)>? ParsePoints(string text, out string? error)
        {
            error = null;
            var list = new List<(double, double)>();
            foreach (var raw in text.Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;
                var xy = part.Split(':');
                if (xy.Length != 2 ||
                    !double.TryParse(xy[0].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                    !double.TryParse(xy[1].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    error = $"Ponto inválido: \"{part}\". Use velocidade:multiplicador, ex.: 30:1.2";
                    return null;
                }
                if (x <= 0 || y <= 0) { error = "Velocidade e multiplicador precisam ser maiores que zero."; return null; }
                list.Add((x, y));
            }
            if (list.Count < 2) { error = "A curva personalizada precisa de pelo menos 2 pontos."; return null; }
            if (list.Count > 257) { error = "Máximo de 257 pontos."; return null; }
            if (list.Select(p => p.Item1).Distinct().Count() != list.Count) { error = "Há velocidades repetidas."; return null; }
            return list;
        }

        public string Describe()
        {
            var inv = CultureInfo.InvariantCulture;
            return Style switch
            {
                CurveStyle.None => "Sem aceleração",
                CurveStyle.Linear => $"Linear · começa em {(InputOffset * SensMath.CmPerInch).ToString("0.#", inv)} cm/s · máx {CapY.ToString("0.##", inv)}x",
                CurveStyle.Natural => $"Natural · começa em {(InputOffset * SensMath.CmPerInch).ToString("0.#", inv)} cm/s · máx {Limit.ToString("0.##", inv)}x",
                CurveStyle.Custom => $"Personalizada · {Data.Length / 2} pontos",
                _ => $"Avançada ({Mode})",
            };
        }
    }

    /// <summary>
    /// Matemática das curvas portada do Raw Accel (MIT, Copyright (c) 2020 a1xd).
    /// Retorna o multiplicador de sensibilidade para uma velocidade em in/s.
    /// </summary>
    public static class CurveMath
    {
        public static bool CanPlot(AxisCurve a) => a.Style != CurveStyle.Advanced;

        /// <summary>Multiplicador em uma velocidade em cm/s (NaN se o modo não é suportado no gráfico).</summary>
        public static double MultiplierAtCmS(AxisCurve a, double cmPerSec) => Multiplier(a, cmPerSec / SensMath.CmPerInch);

        public static double Multiplier(AxisCurve a, double x)
        {
            try
            {
                return a.Mode switch
                {
                    "noaccel" => 1,
                    "classic" => a.Gain ? ClassicGain(a, x) : ClassicLegacy(a, x),
                    "natural" => a.Gain ? NaturalGain(a, x) : NaturalLegacy(a, x),
                    "lut" => Lookup(a, x),
                    _ => double.NaN,
                };
            }
            catch
            {
                return double.NaN;
            }
        }

        // ---------------- classic ----------------

        static double BaseFn(double x, double accelRaised, AxisCurve a) =>
            accelRaised * Math.Pow(x - a.InputOffset, a.ExponentClassic) / x;

        static double ClassicLegacy(AxisCurve a, double x)
        {
            double accelRaised;
            double cap = double.MaxValue;
            double sign = 1;
            double p = a.ExponentClassic;

            switch (a.CapMode)
            {
                case "in_out":
                    cap = a.CapY - 1;
                    if (cap < 0) { cap = -cap; sign = -sign; }
                    {
                        double acc = Math.Pow(a.CapX * cap * Math.Pow(a.CapX - a.InputOffset, -p), 1 / (p - 1));
                        accelRaised = Math.Pow(acc, p - 1);
                    }
                    break;
                case "input":
                    accelRaised = Math.Pow(a.Acceleration, p - 1);
                    if (a.CapX > 0) cap = BaseFn(a.CapX, accelRaised, a);
                    break;
                default:
                    accelRaised = Math.Pow(a.Acceleration, p - 1);
                    if (a.CapY > 0)
                    {
                        cap = a.CapY - 1;
                        if (cap < 0) { cap = -cap; sign = -sign; }
                    }
                    break;
            }

            if (x <= a.InputOffset) return 1;
            return sign * Math.Min(BaseFn(x, accelRaised, a), cap) + 1;
        }

        static double GainFn(double x, double accel, double power, double offset) =>
            power * Math.Pow(accel * (x - offset), power - 1);

        static double GainInverse(double y, double accel, double power, double offset) =>
            (accel * offset + Math.Pow(y / power, 1 / (power - 1))) / accel;

        static double GainAccel(double x, double y, double power, double offset) =>
            -Math.Pow(y / power, 1 / (power - 1)) / (offset - x);

        static double ClassicGain(AxisCurve a, double x)
        {
            double p = a.ExponentClassic;
            double accelRaised;
            double capX = double.MaxValue, capY = double.MaxValue;
            double constant = 0;
            double sign = 1;

            switch (a.CapMode)
            {
                case "in_out":
                    capX = a.CapX;
                    capY = a.CapY - 1;
                    if (capY < 0) { capY = -capY; sign = -sign; }
                    {
                        double acc = GainAccel(capX, capY, p, a.InputOffset);
                        accelRaised = Math.Pow(acc, p - 1);
                    }
                    constant = (BaseFn(capX, accelRaised, a) - capY) * capX;
                    break;
                case "input":
                    accelRaised = Math.Pow(a.Acceleration, p - 1);
                    if (a.CapX > 0)
                    {
                        capX = a.CapX;
                        capY = GainFn(capX, a.Acceleration, p, a.InputOffset);
                        constant = (BaseFn(capX, accelRaised, a) - capY) * capX;
                    }
                    break;
                default:
                    accelRaised = Math.Pow(a.Acceleration, p - 1);
                    if (a.CapY > 0)
                    {
                        capY = a.CapY - 1;
                        if (capY == 0)
                        {
                            capX = 0;
                        }
                        else
                        {
                            if (capY < 0) { capY = -capY; sign = -sign; }
                            capX = GainInverse(capY, a.Acceleration, p, a.InputOffset);
                            constant = (BaseFn(capX, accelRaised, a) - capY) * capX;
                        }
                    }
                    break;
            }

            if (x <= a.InputOffset) return 1;
            double output = x < capX ? BaseFn(x, accelRaised, a) : constant / x + capY;
            return sign * output + 1;
        }

        // ---------------- natural ----------------

        static double NaturalLegacy(AxisCurve a, double x)
        {
            double offset = a.InputOffset;
            double limit = a.Limit - 1;
            double accel = a.DecayRate / Math.Abs(limit);
            if (x <= offset) return 1;
            double offsetX = offset - x;
            double decay = Math.Exp(accel * offsetX);
            return limit * (1 - (offset - decay * offsetX) / x) + 1;
        }

        static double NaturalGain(AxisCurve a, double x)
        {
            double offset = a.InputOffset;
            double limit = a.Limit - 1;
            double accel = a.DecayRate / Math.Abs(limit);
            double constant = -limit / accel;
            if (x <= offset) return 1;
            double offsetX = offset - x;
            double decay = Math.Exp(accel * offsetX);
            double output = limit * (decay / accel - offsetX) + constant;
            return output / x + 1;
        }

        // ---------------- lookup (curva personalizada) ----------------

        static double Lookup(AxisCurve a, double x)
        {
            var d = a.Data;
            int size = d.Length / 2;
            if (size < 1) return 1;
            if (x <= 0) return 0;

            int lo = 0, hi = size - 2;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                double px = d[mid * 2], py = d[mid * 2 + 1];
                if (x < px) hi = mid - 1;
                else if (x > px) lo = mid + 1;
                else return a.Gain ? py / x : py;
            }

            if (lo > 0)
            {
                double ax = d[(lo - 1) * 2], ay = d[(lo - 1) * 2 + 1];
                double bx = d[lo * 2], by = d[lo * 2 + 1];
                double t = (x - ax) / (bx - ax);
                double y = Lerp(ay, by, t);
                return a.Gain ? y / x : y;
            }

            double y0 = d[1];
            return a.Gain ? y0 / d[0] : y0;
        }

        static double Lerp(double a, double b, double t)
        {
            double x = a + t * (b - a);
            if ((t > 1) == (a < b)) return Math.Max(x, b);
            return Math.Min(x, b);
        }

        /// <summary>Amostra uma curva qualquer em pontos (para virar curva personalizada).</summary>
        public static List<(double CmS, double Mult)> Sample(AxisCurve a, double maxCmS = 150, int count = 16)
        {
            var list = new List<(double, double)>();
            for (int i = 1; i <= count; i++)
            {
                double v = maxCmS * i / count;
                double m = MultiplierAtCmS(a, v);
                if (double.IsNaN(m)) m = 1;
                list.Add((Math.Round(v, 1), Math.Round(m, 3)));
            }
            return list;
        }
    }
}
