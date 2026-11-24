// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace HarleAccel.Core
{
    /// <summary>
    /// Uma configuração completa do HarleAccel (o que vai para o driver).
    /// </summary>
    public sealed class AccelProfile
    {
        public string Name { get; set; } = "HarleAccel";
        public int Dpi { get; set; } = 800;

        /// <summary>Curva horizontal (ou de ambos os eixos, quando SeparateY = false).</summary>
        public AxisCurve X { get; set; } = AxisCurve.NoAccel();

        /// <summary>Curva vertical, só usada quando SeparateY = true (modo "por eixo" do Raw Accel).</summary>
        public AxisCurve Y { get; set; } = AxisCurve.NoAccel();

        public bool SeparateY { get; set; }

        /// <summary>Rotação em graus (corrige mão que move o mouse "torto"). 0 = desligado.</summary>
        public double Rotation { get; set; }

        /// <summary>Angle snapping do driver em graus (0 a 45). 0 = desligado (recomendado para jogos).</summary>
        public double SnapDegrees { get; set; }

        /// <summary>Multiplicador geral de sens dentro do Raw Accel (1 = não mexe na sens do jogo).</summary>
        public double SensMultiplier { get; set; } = 1.0;

        /// <summary>Multiplicador da sens vertical (Y/X). 0.8 = vertical 20% mais lenta.</summary>
        public double YxRatio { get; set; } = 1.0;

        /// <summary>
        /// Perfil original importado do Raw Accel (JSON). Campos que o HarleAccel não edita
        /// (rotação, pesos, suavização...) são preservados a partir dele.
        /// </summary>
        public string? BaseProfileJson { get; set; }

        public AccelProfile Clone()
        {
            var p = (AccelProfile)MemberwiseClone();
            p.X = X.Clone();
            p.Y = Y.Clone();
            return p;
        }

        public string Describe()
        {
            string s = X.Describe();
            if (SeparateY) s += " | Vertical: " + Y.Describe();
            if (Math.Abs(YxRatio - 1) > 1e-6) s += $" | Vertical {YxRatio.ToString("0.##", CultureInfo.InvariantCulture)}x";
            if (Math.Abs(Rotation) > 1e-6) s += $" | Rotação {Rotation.ToString("0.#", CultureInfo.InvariantCulture)}°";
            if (SnapDegrees > 0) s += $" | Snap {SnapDegrees.ToString("0", CultureInfo.InvariantCulture)}°";
            return s;
        }

        /// <summary>Perfil neutro: sem aceleração, sem rotação, sem snap, sens 1x.</summary>
        public static AccelProfile Neutral(int dpi) => new() { Name = "Sem aceleração", Dpi = dpi };

        /// <summary>True se esse perfil não muda nada no mouse.</summary>
        [JsonIgnore]
        public bool IsNeutral =>
            X.Style == CurveStyle.None && (!SeparateY || Y.Style == CurveStyle.None) &&
            Math.Abs(YxRatio - 1) < 1e-6 && Math.Abs(Rotation) < 1e-6 && SnapDegrees == 0 &&
            Math.Abs(SensMultiplier - 1) < 1e-6;

        /// <summary>Multiplicador horizontal numa velocidade (cm/s).</summary>
        public double MultX(double cmS) => CurveMath.MultiplierAtCmS(X, cmS);

        /// <summary>Multiplicador vertical final numa velocidade (cm/s), já com a proporção Y/X.</summary>
        public double MultY(double cmS) => CurveMath.MultiplierAtCmS(SeparateY ? Y : X, cmS) * YxRatio;
    }

    /// <summary>
    /// Lê e escreve o settings.json no formato do Raw Accel 1.7 (o mesmo que o writer.exe oficial aceita).
    /// </summary>
    public static class RawAccelJson
    {
        public const string RaVersion = "1.7.0";

        const string KDomain = "Stretches domain for horizontal vs vertical inputs";
        const string KRange = "Stretches accel range for horizontal vs vertical inputs";
        const string KArgsX = "Whole or horizontal accel parameters";
        const string KArgsY = "Vertical accel parameters";
        const string KSpeed = "Input speed calculation parameters";
        const string KWhole = "Whole/combined accel (set false for 'by component' mode)";
        const string KOutDpi = "Output DPI";
        const string KYx = "Y/X output DPI ratio (vertical sens multiplier)";
        const string KLr = "L/R output DPI ratio (left sens multiplier)";
        const string KUd = "U/D output DPI ratio (up sens multiplier)";
        const string KRot = "Degrees of rotation";
        const string KSnap = "Degrees of angle snapping";
        const string KCapSpeed = "Input Speed Cap";
        const string KDevDpi = "DPI (normalizes input speed unit: counts/ms -> in/s)";
        const string KDevPoll = "Polling rate Hz (keep at 0 for automatic adjustment)";
        const string KGain = "Gain / Velocity";
        const string KCap = "Cap / Jump";
        const string KCapMode = "Cap mode";

        static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

        // ------------------------------------------------------------------ escrita

        public static string Build(AccelProfile p)
        {
            JsonObject prof = p.BaseProfileJson != null
                ? (JsonNode.Parse(p.BaseProfileJson) as JsonObject ?? DefaultProfile())
                : DefaultProfile();

            prof["name"] = string.IsNullOrWhiteSpace(p.Name) ? "HarleAccel" : p.Name;
            prof[KArgsX] = AxisToJson(p.X);
            prof[KArgsY] = AxisToJson(p.SeparateY ? p.Y : p.X);

            if (prof[KSpeed] is not JsonObject speed)
            {
                speed = DefaultSpeedArgs();
                prof[KSpeed] = speed;
            }
            speed[KWhole] = !p.SeparateY;

            // Output DPI = DPI do mouse -> sens 1x na velocidade baixa (a sens do jogo fica igual).
            prof[KOutDpi] = p.Dpi * (p.SensMultiplier > 0 ? p.SensMultiplier : 1.0);
            prof[KYx] = p.YxRatio;
            prof[KRot] = p.Rotation;
            prof[KSnap] = Math.Clamp(p.SnapDegrees, 0, 45);

            var root = new JsonObject
            {
                ["### Accel modes ###"] = "classic | jump | natural | synchronous | power | lut | noaccel",
                ["### Cap modes ###"] = "in_out | input | output",
                ["version"] = RaVersion,
                ["defaultDeviceConfig"] = new JsonObject
                {
                    ["disable"] = false,
                    [KDevDpi] = p.Dpi,
                    [KDevPoll] = 0,
                },
                ["profiles"] = new JsonArray { prof },
                ["devices"] = new JsonArray(),
            };

            return root.ToJsonString(Pretty);
        }

        static JsonObject DefaultSpeedArgs() => new()
        {
            [KWhole] = true,
            ["lpNorm"] = 2.0,
            ["Time in ms after which an input is weighted at half its original value."] = 0.0,
            ["Time in ms after which scale is weighted at half its original value."] = 0.0,
            ["Time in ms after which an output is weighted at half its original value."] = 0.0,
        };

        static JsonObject DefaultProfile() => new()
        {
            ["name"] = "HarleAccel",
            [KDomain] = new JsonObject { ["x"] = 1.0, ["y"] = 1.0 },
            [KRange] = new JsonObject { ["x"] = 1.0, ["y"] = 1.0 },
            [KArgsX] = AxisToJson(AxisCurve.NoAccel()),
            [KArgsY] = AxisToJson(AxisCurve.NoAccel()),
            [KSpeed] = DefaultSpeedArgs(),
            [KOutDpi] = 1000.0,
            [KYx] = 1.0,
            [KLr] = 1.0,
            [KUd] = 1.0,
            [KRot] = 0.0,
            [KSnap] = 0.0,
            [KCapSpeed] = 0.0,
        };

        static JsonObject AxisToJson(AxisCurve a)
        {
            var data = new JsonArray();
            if (a.Mode == "lut")
                foreach (var f in a.Data) data.Add((double)f);

            return new JsonObject
            {
                ["mode"] = a.Mode,
                [KGain] = a.Gain,
                ["inputOffset"] = a.InputOffset,
                ["outputOffset"] = a.OutputOffset,
                ["acceleration"] = a.Acceleration,
                ["decayRate"] = a.DecayRate,
                ["gamma"] = a.Gamma,
                ["motivity"] = a.Motivity,
                ["exponentClassic"] = a.ExponentClassic,
                ["scale"] = a.Scale,
                ["exponentPower"] = a.ExponentPower,
                ["limit"] = a.Limit,
                ["syncSpeed"] = a.SyncSpeed,
                ["smooth"] = a.Smooth,
                [KCap] = new JsonObject { ["x"] = a.CapX, ["y"] = a.CapY },
                [KCapMode] = a.CapMode,
                ["data"] = data,
            };
        }

        // ------------------------------------------------------------------ leitura

        /// <summary>Importa um settings.json do Raw Accel. Usa o primeiro perfil.</summary>
        public static AccelProfile Parse(string json, int fallbackDpi, out string? warning)
        {
            warning = null;
            var root = JsonNode.Parse(json) as JsonObject
                       ?? throw new FormatException("O arquivo não é um JSON válido.");

            var profiles = root["profiles"] as JsonArray;
            var prof = profiles?.FirstOrDefault() as JsonObject
                       ?? throw new FormatException("Nenhum perfil encontrado no settings.json do Raw Accel.");

            int dpi = fallbackDpi;
            bool hadDpi = false;
            if (root["defaultDeviceConfig"] is JsonObject dev && dev[KDevDpi] is JsonNode dn)
            {
                int d = (int)Math.Round(Num(dn, 0));
                if (d > 0) { dpi = d; hadDpi = true; }
            }
            double outDpi = Num(prof[KOutDpi], 1000);
            double sensMult = hadDpi ? outDpi / dpi : outDpi / 1000.0;
            if (!hadDpi)
                warning = "Esse settings.json não tinha DPI configurada no Raw Accel. O HarleAccel usa a sua DPI, " +
                          "então as velocidades da curva podem mudar um pouco. Confira o gráfico antes de aplicar.";

            var x = AxisFromJson(prof[KArgsX] as JsonObject);
            var y = AxisFromJson(prof[KArgsY] as JsonObject);
            bool whole = (prof[KSpeed] as JsonObject)?[KWhole]?.GetValue<bool>() ?? true;

            var p = new AccelProfile
            {
                Name = prof["name"]?.GetValue<string>() ?? "Importado do Raw Accel",
                Dpi = dpi,
                X = x,
                Y = y,
                SeparateY = !whole,
                YxRatio = Num(prof[KYx], 1),
                SensMultiplier = sensMult > 0 ? sensMult : 1,
                Rotation = Num(prof[KRot], 0),
                SnapDegrees = Num(prof[KSnap], 0),
                BaseProfileJson = prof.ToJsonString(),
            };
            if (p.YxRatio <= 0) p.YxRatio = 1;
            return p;
        }

        static AxisCurve AxisFromJson(JsonObject? o)
        {
            var a = new AxisCurve();
            if (o == null) return a;

            a.Mode = o["mode"]?.GetValue<string>() ?? "noaccel";
            a.Gain = o[KGain]?.GetValue<bool>() ?? true;
            a.InputOffset = Num(o["inputOffset"], a.InputOffset);
            a.OutputOffset = Num(o["outputOffset"], a.OutputOffset);
            a.Acceleration = Num(o["acceleration"], a.Acceleration);
            a.DecayRate = Num(o["decayRate"], a.DecayRate);
            a.Gamma = Num(o["gamma"], a.Gamma);
            a.Motivity = Num(o["motivity"], a.Motivity);
            a.ExponentClassic = Num(o["exponentClassic"], a.ExponentClassic);
            a.Scale = Num(o["scale"], a.Scale);
            a.ExponentPower = Num(o["exponentPower"], a.ExponentPower);
            a.Limit = Num(o["limit"], a.Limit);
            a.SyncSpeed = Num(o["syncSpeed"], a.SyncSpeed);
            a.Smooth = Num(o["smooth"], a.Smooth);
            if (o[KCap] is JsonObject cap)
            {
                a.CapX = Num(cap["x"], a.CapX);
                a.CapY = Num(cap["y"], a.CapY);
            }
            a.CapMode = o[KCapMode]?.GetValue<string>() ?? a.CapMode;
            if (o["data"] is JsonArray arr)
                a.Data = arr.Select(n => (float)Num(n, 0)).ToArray();
            return a;
        }

        static double Num(JsonNode? n, double fallback)
        {
            if (n == null) return fallback;
            try { return n.GetValue<double>(); }
            catch
            {
                try { return n.GetValue<int>(); }
                catch { return fallback; }
            }
        }

        // ------------------------------------------------------------------ validação

        /// <summary>Mesmas regras do validador do Raw Accel, para avisar antes de enviar.</summary>
        public static string? Validate(AccelProfile p)
        {
            if (p.Dpi <= 0) return "Informe a DPI do mouse na tela de Sensibilidade.";
            if (p.YxRatio <= 0) return "A proporção vertical precisa ser maior que zero.";
            if (p.SnapDegrees < 0 || p.SnapDegrees > 45) return "O angle snapping precisa ficar entre 0 e 45 graus.";
            foreach (var (axis, label) in new[] { (p.X, "horizontal"), (p.SeparateY ? p.Y : p.X, "vertical") })
            {
                string? e = ValidateAxis(axis);
                if (e != null) return $"Curva {label}: {e}";
            }
            return null;
        }

        static string? ValidateAxis(AxisCurve a)
        {
            if (a.Mode == "lut")
            {
                if (a.Data.Length < 4) return "a curva personalizada precisa de pelo menos 2 pontos.";
                if (a.Data.Length > 514) return "máximo de 257 pontos.";
            }
            if (a.InputOffset < 0 || a.OutputOffset < 0) return "o início não pode ser negativo.";
            if (a.Acceleration <= 0) return "a força precisa ser maior que zero.";
            if (a.DecayRate <= 0) return "a taxa precisa ser maior que zero.";
            if (a.ExponentClassic <= 1) return "o expoente precisa ser maior que 1.";
            if (a.Limit <= 0) return "o limite precisa ser maior que zero.";
            if (a.Mode == "natural" && Math.Abs(a.Limit - 1) < 1e-6) return "a sens máxima precisa ser diferente de 1x.";
            if (a.CapX < 0 || a.CapY < 0) return "o teto não pode ser negativo.";
            if (a.Motivity <= 1) return "motivity precisa ser maior que 1.";
            if (a.Smooth < 0 || a.Smooth > 1) return "smooth precisa estar entre 0 e 1.";
            return null;
        }
    }
}
