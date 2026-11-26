// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using HarleAccel.Core;
using Xunit;

// AppState.Current é global: os testes rodam um de cada vez
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HarleAccel.Tests
{
    /// <summary>
    /// A matemática das curvas tem que ser IDÊNTICA à do Raw Accel.
    /// Os valores de referência saem do C++ original (tests/reference/ref.cpp).
    /// </summary>
    public class CurveMathTests
    {
        static readonly string[] CapModes = { "in_out", "input", "output" };

        public static IEnumerable<object[]> Reference() =>
            File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "rawaccel_reference.txt"))
                .Where(l => l.Trim().Length > 0)
                .Select(l => new object[] { l });

        [Theory]
        [MemberData(nameof(Reference))]
        public void Igual_ao_Raw_Accel_original(string line)
        {
            var p = line.Split(' ');
            var inv = CultureInfo.InvariantCulture;
            string mode = p[0];
            bool gain = p[1] == "g1";
            AxisCurve a;
            double x, expected;

            switch (mode)
            {
                case "classic":
                    int cm = int.Parse(p[2][1..]);
                    x = double.Parse(p[3], inv);
                    expected = double.Parse(p[4], inv);
                    a = new AxisCurve
                    {
                        Mode = "classic", Gain = gain, InputOffset = 2, Acceleration = 0.03,
                        ExponentClassic = 2.3, CapX = 15, CapY = 1.6, CapMode = CapModes[cm],
                    };
                    break;
                case "natural":
                    x = double.Parse(p[2], inv);
                    expected = double.Parse(p[3], inv);
                    a = new AxisCurve { Mode = "natural", Gain = gain, InputOffset = 1.5, DecayRate = 0.2, Limit = 1.8 };
                    break;
                default:
                    x = double.Parse(p[2], inv);
                    expected = double.Parse(p[3], inv);
                    a = new AxisCurve { Mode = "lut", Gain = gain, Data = new float[] { 1, 1, 4, 1.1f, 16, 1.5f, 40, 2 } };
                    break;
            }

            Assert.Equal(expected, CurveMath.Multiplier(a, x), 8);
        }

        [Fact]
        public void Velocidade_lenta_fica_em_1x()
        {
            var a = AxisCurve.Linear(5, 0.03, 1.6);
            Assert.Equal(1.0, CurveMath.MultiplierAtCmS(a, 3), 9);
            Assert.True(CurveMath.MultiplierAtCmS(a, 60) > 1.2);
        }
    }

    public class SensMathTests
    {
        [Fact]
        public void Cm360_Valorant()
            => Assert.Equal(40.82, SensMath.Cm360(800, 0.4, Games.ByName("Valorant")), 2);

        [Fact]
        public void Cm360_Fortnite_em_porcentagem()
            => Assert.Equal(25.72, SensMath.Cm360(800, 8, Games.ByName("Fortnite (%)")), 2);

        [Fact]
        public void Valorant_para_CS2()
            => Assert.Equal(1.273, SensMath.Convert(0.4, Games.ByName("Valorant"), Games.ByName("Counter-Strike 2")), 3);

        [Fact]
        public void Fortnite_para_Valorant()
            => Assert.Equal(0.635, SensMath.Convert(8, Games.ByName("Fortnite (%)"), Games.ByName("Valorant")), 3);

        [Fact]
        public void Conversao_ida_e_volta_mantem_o_valor()
        {
            var a = Games.ByName("Apex Legends");
            var b = Games.ByName("Overwatch 2");
            Assert.Equal(1.7, SensMath.Convert(SensMath.Convert(1.7, a, b), b, a), 9);
        }
    }

    public class RawAccelJsonTests
    {
        [Fact]
        public void Build_e_Parse_ida_e_volta()
        {
            var p = new AccelProfile { Dpi = 800, X = AxisCurve.Linear(5, 0.025, 1.6), YxRatio = 0.85, Rotation = -2.5 };
            string json = RawAccelJson.Build(p);
            var back = RawAccelJson.Parse(json, 1600, out var warn);
            Assert.Null(warn);
            Assert.Equal(800, back.Dpi);
            Assert.Equal(0.85, back.YxRatio, 9);
            Assert.Equal(-2.5, back.Rotation, 9);
            Assert.Equal(json, RawAccelJson.Build(back));
        }

        [Fact]
        public void Base_sens_fica_igual_output_dpi_igual_a_dpi()
        {
            var json = JsonNode.Parse(RawAccelJson.Build(new AccelProfile { Dpi = 1600 }))!;
            Assert.Equal(1600, json["profiles"]![0]!["Output DPI"]!.GetValue<double>());
            Assert.Equal(1600, json["defaultDeviceConfig"]!["DPI (normalizes input speed unit: counts/ms -> in/s)"]!.GetValue<int>());
        }

        [Fact]
        public void Validacao_pega_erros_antes_do_driver()
        {
            Assert.NotNull(RawAccelJson.Validate(new AccelProfile { Dpi = 0 }));
            Assert.NotNull(RawAccelJson.Validate(new AccelProfile { Dpi = 800, X = AxisCurve.Natural(3, 0.1, 1.0) }));
            Assert.NotNull(RawAccelJson.Validate(new AccelProfile { Dpi = 800, SnapDegrees = 60 }));
            Assert.Null(RawAccelJson.Validate(new AccelProfile { Dpi = 800, X = AxisCurve.Linear(5, 0.03, 1.5) }));
        }
    }

    public class ChatSemIATests
    {
        public ChatSemIATests()
        {
            AppState.Current.GameName = "Counter-Strike 2";
            AppState.Current.Dpi = 800;
        }

        [Theory]
        [InlineData("eu jogo fortnite com 8% e 800 dpi, converte pro valorant", "Fortnite (%)", 8, 800, "Valorant")]
        [InlineData("8% fortnite 800 dpi pro valorant", "Fortnite (%)", 8, 800, "Valorant")]
        [InlineData("converte 0.4 do valo pro cs2", "Valorant", 0.4, null, "Counter-Strike 2")]
        [InlineData("minha sens no cs é 1,2 com 400 dpi, quanto fica no apex?", "Counter-Strike 2", 1.2, 400, "Apex Legends")]
        [InlineData("jogo no fortnite com 7.5% e quero a mesma sens no overwatch 2", "Fortnite (%)", 7.5, null, "Overwatch 2")]
        [InlineData("converte 1.5 pro valorant", "Counter-Strike 2", 1.5, null, "Valorant")]
        public void Entende_pedidos_de_conversao(string text, string from, double sens, int? dpi, string to)
        {
            var r = LocalParser.TryParse(text);
            Assert.NotNull(r);
            Assert.Equal(from, r!.From.Name);
            Assert.Equal(sens, r.Sens, 6);
            Assert.Equal(dpi, r.Dpi);
            Assert.Equal(to, r.To?.Name);
        }

        [Theory]
        [InlineData("qual a melhor curva pra mim?")]
        [InlineData("oi tudo bem")]
        [InlineData("minha mira treme nos flicks")]
        public void Deixa_o_resto_para_a_IA(string text) => Assert.Null(LocalParser.TryParse(text));

        [Fact]
        public void Fortnite_em_decimal_vira_porcentagem()
        {
            var c = AssistantTools.Convert(Games.ByName("Fortnite (%)"), 0.08, 800, Games.ByName("Valorant"));
            Assert.Equal(8, c.Sens, 6);
            Assert.NotNull(c.Note);
            Assert.Equal(0.635, c.Items.Single().Sens, 3);
        }

        [Fact]
        public void Proposta_nao_aplica_nada_e_valida()
        {
            AppState.Current.Applied = new AccelProfile { Dpi = 800, X = AxisCurve.Linear(5, 0.025, 1.5) };
            var args = JsonNode.Parse("{\"sens_maxima\":1.7,\"vertical\":\"0.9\",\"motivo\":\"teste\"}")!.AsObject();
            var prop = AssistantTools.ProposeChange(args, out var err);
            Assert.Null(err);
            Assert.Equal("pending", prop!.State);
            Assert.Equal(1.7, prop.After.X.CapY, 9);
            Assert.Equal(0.9, prop.After.YxRatio, 9);
            Assert.Equal(1.5, AppState.Current.Applied!.X.CapY, 9); // a curva aplicada não mudou
        }

        [Fact]
        public void Proposta_vazia_e_recusada()
        {
            var prop = AssistantTools.ProposeChange(JsonNode.Parse("{\"motivo\":\"nada\"}")!.AsObject(), out var err);
            Assert.Null(prop);
            Assert.NotNull(err);
        }
    }

    public class TesteDePerfilTests
    {
        static List<MoveBin> Simulate(string style, AccelProfile? curve, int seed = 1)
        {
            var rnd = new Random(seed);
            var bins = new List<MoveBin>();
            double t = 0, tilt = 3 * Math.PI / 180;
            const int dpi = 800;
            double Cps(double cms) => cms / 2.54 * dpi * 0.01;

            for (int seg = 0; seg < 3000; seg++)
            {
                double r = rnd.NextDouble();
                double flickP = style == "flick" ? 0.25 : style == "track" ? 0.04 : 0.12;
                double microP = style == "flick" ? 0.45 : 0.3;
                double speed; int len;
                if (r < flickP) { speed = 60 + rnd.NextDouble() * 100; len = 8 + rnd.Next(10); }
                else if (r < flickP + microP) { speed = 1 + rnd.NextDouble() * 6; len = 20 + rnd.Next(40); }
                else { speed = 10 + rnd.NextDouble() * 25; len = 30 + rnd.Next(60); }
                double ang = rnd.NextDouble() < 0.75 ? (rnd.NextDouble() < 0.5 ? 0 : Math.PI) + tilt : rnd.NextDouble() * 2 * Math.PI;

                for (int i = 0; i < len; i++)
                {
                    double prof = r < flickP ? Math.Sin(Math.PI * (i + 0.5) / len) : 1;
                    double vx = speed * prof * Math.Cos(ang), vy = speed * prof * Math.Sin(ang);
                    double m = curve == null ? 1 : curve.MultX(Math.Sqrt(vx * vx + vy * vy));
                    int dx = (int)Math.Round(Cps(vx) * m), dy = (int)Math.Round(Cps(vy) * m);
                    if (dx != 0 || dy != 0) bins.Add(new MoveBin(t, dx, dy));
                    t += 0.01;
                }
                t += 0.05 + rnd.NextDouble() * 0.3;
            }
            return bins;
        }

        [Theory]
        [InlineData("flick", "Flicker")]
        [InlineData("track", "Tracker")]
        [InlineData("hybrid", "Híbrido")]
        public void Detecta_o_estilo(string sim, string expected)
        {
            var bins = Simulate(sim, null);
            var r = ProfileAnalyzer.Analyze(bins, 800, bins[^1].T, null, Games.ByName("Valorant"), 40.8);
            Assert.Equal(expected, r.Style);
            Assert.NotNull(r.Recommended);
            Assert.Null(RawAccelJson.Validate(r.Recommended!));
            Assert.Equal(1.0, r.Recommended!.MultX(2), 6); // microajuste continua 1x
        }

        [Fact]
        public void Mede_a_inclinacao_da_mao()
        {
            var bins = Simulate("hybrid", null);
            var r = ProfileAnalyzer.Analyze(bins, 800, bins[^1].T, null, Games.ByName("Valorant"), 40.8);
            Assert.InRange(r.TiltDeg, 2.0, 4.0);
            Assert.InRange(r.Recommended!.Rotation, -4.0, -2.0);
        }

        [Fact]
        public void Desfaz_a_curva_que_estava_ativa()
        {
            var curve = new AccelProfile { Dpi = 800, X = AxisCurve.Linear(5, 0.03, 1.6) };
            var raw = Simulate("hybrid", null);
            var accelerated = Simulate("hybrid", curve);
            var a = ProfileAnalyzer.Analyze(raw, 800, raw[^1].T, null, Games.ByName("Valorant"), 40.8);
            var b = ProfileAnalyzer.Analyze(accelerated, 800, accelerated[^1].T, curve, Games.ByName("Valorant"), 40.8);
            Assert.Equal(a.P95, b.P95, 0);
        }
    }

    public class TreinoTests
    {
        [Fact]
        public void Todo_perfil_tem_rotina_no_Aim_Lab_que_e_gratis()
        {
            var lib = TrainingLibrary.BuildDefault();
            foreach (var level in TrainingLibrary.Levels)
            foreach (var style in TrainingLibrary.Styles)
                Assert.NotNull(lib.Routines.FirstOrDefault(r => r.Platform == "Aim Lab" && r.Level == level && r.Style == style));
        }

        [Fact]
        public void Rotinas_de_adaptacao_tem_36_minutos()
        {
            var lib = TrainingLibrary.BuildDefault();
            Assert.All(lib.Routines.Where(r => r.Style != "Completa"), r => Assert.Equal(36, r.Minutes));
        }

        [Fact]
        public void Arquivo_routines_json_do_repo_bate_com_o_codigo()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "../../../../../routines/routines.json");
            if (!File.Exists(path)) return; // fora do repositório
            Assert.Equal(TrainingLibrary.ToJson(TrainingLibrary.BuildDefault()).Trim(), File.ReadAllText(path).Trim());
        }
    }
}

namespace HarleAccel.Tests
{
    public class SegurancaTests
    {
        [Theory]
        [InlineData("https://github.com/Harlezinhuu/HarleAccel/releases", true)]
        [InlineData("https://discord.gg/rDb9raznQ7", true)]
        [InlineData("https://tiny.one/ALbronze", true)]
        [InlineData("http://github.com/x", false)]               // sem https
        [InlineData("file:///C:/Windows/System32/cmd.exe", false)]
        [InlineData("C:\\Windows\\System32\\calc.exe", false)]
        [InlineData("https://github.com.site-falso.com/x", false)]
        [InlineData("https://site-qualquer.com", false)]
        public void So_abre_links_seguros(string url, bool ok) => Assert.Equal(ok, SafeLinks.IsAllowed(url));

        [Fact]
        public void Biblioteca_padrao_passa_na_validacao() => Assert.True(TrainingLibrary.IsValid(TrainingLibrary.BuildDefault()));

        [Fact]
        public void Biblioteca_com_link_malicioso_e_recusada()
        {
            var lib = TrainingLibrary.BuildDefault();
            lib.Routines[0].Link = "file:///C:/Windows/System32/cmd.exe";
            Assert.False(TrainingLibrary.IsValid(lib));
        }

        [Fact]
        public void Biblioteca_com_codigo_estranho_e_recusada()
        {
            var lib = TrainingLibrary.BuildDefault();
            lib.Routines[0].ShareCode = "abc & del *.*";
            Assert.False(TrainingLibrary.IsValid(lib));
        }
    }
}

namespace HarleAccel.Tests
{
    public class DiagnosticoDeMiraTests
    {
        const double R = 15;

        /// <summary>Movimento em linha reta de 0 até "stop" (perfil de velocidade em sino), e depois até "end" devagar.</summary>
        static List<AimDiagnosis.Sample> Path(double stop, double end, double dist = 300)
        {
            var list = new List<AimDiagnosis.Sample>();
            double t = 0;
            const int n = 30;
            for (int i = 0; i <= n; i++)
            {
                double x = stop * (1 - Math.Cos(Math.PI * i / n)) / 2; // acelera e freia
                list.Add(new AimDiagnosis.Sample(t, x, 0));
                t += 0.008;
            }
            // pausa curta e correção lenta (microajuste)
            for (int i = 1; i <= 15; i++)
            {
                t += 0.02;
                list.Add(new AimDiagnosis.Sample(t, stop + (end - stop) * i / 15.0, 0));
            }
            return list;
        }

        [Fact]
        public void Caiu_direto_no_alvo() =>
            Assert.Equal(Approach.Direct, AimDiagnosis.Classify(Path(300, 300), 0, 0, 300, 0, R));

        [Fact]
        public void Passou_do_alvo_e_voltou() =>
            Assert.Equal(Approach.Over, AimDiagnosis.Classify(Path(350, 300), 0, 0, 300, 0, R));

        [Fact]
        public void Parou_antes_e_completou() =>
            Assert.Equal(Approach.Under, AimDiagnosis.Classify(Path(240, 300), 0, 0, 300, 0, R));

        [Fact]
        public void Alvo_colado_nao_conta() =>
            Assert.Equal(Approach.Unknown, AimDiagnosis.Classify(Path(20, 20), 0, 0, 20, 0, R));

        [Fact]
        public void Julga_sens_alta_baixa_e_equilibrada()
        {
            Assert.Equal(AimDiagnosis.Verdict.High, AimDiagnosis.Judge(over: 7, under: 1, direct: 4));
            Assert.Equal(AimDiagnosis.Verdict.Low, AimDiagnosis.Judge(over: 1, under: 7, direct: 4));
            Assert.Equal(AimDiagnosis.Verdict.Balanced, AimDiagnosis.Judge(over: 3, under: 3, direct: 6));
            Assert.Equal(AimDiagnosis.Verdict.NotEnough, AimDiagnosis.Judge(1, 1, 1));
        }

        [Fact]
        public void Explicacao_sugere_baixar_a_curva_quando_passa_nos_flicks()
        {
            var r = new AimResult { NearDirect = 8, NearOver = 1, NearUnder = 1, FarOver = 7, FarDirect = 2, FarUnder = 1 };
            var text = string.Join(" ", AimDiagnosis.Explain(r, hasCurve: true));
            Assert.Contains("sens base parece no ponto", text);
            Assert.Contains("aceleração está forte demais", text);
        }

        [Fact]
        public void Buscador_fecha_o_intervalo_pela_metade()
        {
            var f = new SensFinder();
            Assert.Equal(0.5, f.Low, 9);
            Assert.Equal(1.5, f.High, 9);
            f.Choose(+1);                       // escolheu a mais alta
            Assert.Equal(1.5, f.Center, 9);
            Assert.Equal(1.5 * 0.75, f.Low, 9); // agora ±25%
            f.Choose(-1);
            f.Choose(0);                        // iguais: fica no meio
            f.Choose(+1);
            f.Choose(-1);
            Assert.True(f.Done);
            var (lo, hi) = f.Range;
            Assert.True((hi - lo) / f.Center < 0.04); // faixa final de uns ±2%
        }

        [Fact]
        public void Buscador_sempre_na_mesma_direcao_nao_explode()
        {
            var f = new SensFinder();
            for (int i = 0; i < SensFinder.Rounds; i++) f.Choose(+1);
            Assert.InRange(f.Center, 1.5, 4.0);
        }
    }
}
