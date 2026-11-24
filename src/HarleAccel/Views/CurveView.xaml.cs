// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HarleAccel.Core;
using Microsoft.Win32;

namespace HarleAccel.Views
{
    public partial class CurveView : UserControl, IRefreshable
    {
        bool _loading = true; // fica true até o construtor terminar
        AxisCurve? _lastParametric; // última curva Linear/Natural (para "copiar curva anterior")
        readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };

        // faixas dos sliders (escala logarítmica)
        const double LinMin = 0.002, LinMax = 0.15;
        const double NatMin = 0.01, NatMax = 1.0;

        AccelProfile Work => AppState.Current.Profile;

        public CurveView()
        {
            InitializeComponent();
            _loading = false;
            _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); AppState.Save(); };
            Chart.HoverSpeedChanged += s => ExplainText.Text = s is double v ? ExplainAt(v) : ExplainSummary();
            Unloaded += (_, _) => { if (_saveTimer.IsEnabled) { _saveTimer.Stop(); AppState.Save(); } };
        }

        public void Refresh()
        {
            Work.Dpi = AppState.Current.Dpi;
            Status.Text = "";
            LoadControls();
        }

        bool EditingY => AxisY.IsChecked == true && Work.SeparateY;

        AxisCurve CurAxis => EditingY ? Work.Y : Work.X;

        void SetCurAxis(AxisCurve a)
        {
            if (EditingY) Work.Y = a; else Work.X = a;
        }

        // ------------------------------------------------------------------ carregar controles

        void LoadControls()
        {
            _loading = true;
            var a = CurAxis;

            AxisY.IsEnabled = Work.SeparateY;
            if (!Work.SeparateY) AxisX.IsChecked = true;
            AxisHint.Visibility = Work.SeparateY ? Visibility.Collapsed : Visibility.Visible;

            StyleBox.SelectedIndex = a.Style switch
            {
                CurveStyle.None => 0,
                CurveStyle.Linear => 1,
                CurveStyle.Natural => 2,
                CurveStyle.Custom => 3,
                _ => -1,
            };

            ParamPanel.Visibility = a.Style is CurveStyle.Linear or CurveStyle.Natural ? Visibility.Visible : Visibility.Collapsed;
            PointsPanel.Visibility = a.Style == CurveStyle.Custom ? Visibility.Visible : Visibility.Collapsed;

            StyleHint.Text = a.Style switch
            {
                CurveStyle.None => "A sens é sempre a mesma, em qualquer velocidade. Bom para comparar ou para o desktop.",
                CurveStyle.Linear => "A sens sobe em linha reta conforme a mão acelera, até a sens máxima. Simples e previsível.",
                CurveStyle.Natural => "Sobe rápido no começo e vai suavizando até o limite. Sensação mais “orgânica”.",
                CurveStyle.Custom => "Você define cada ponto. Para quem sabe exatamente o que quer.",
                _ => $"Curva importada no modo “{a.Mode}” do Raw Accel. O HarleAccel aplica ela igualzinho, mas ainda não desenha esse modo. Escolha um estilo acima para editar.",
            };

            if (a.Style is CurveStyle.Linear or CurveStyle.Natural)
            {
                _lastParametric = a.Clone();
                StartSlider.Value = Math.Clamp(a.InputOffset * SensMath.CmPerInch, 0, 40);
                if (a.Style == CurveStyle.Linear)
                {
                    RateLabel.Text = "FORÇA";
                    RateSlider.Value = ToSlider(a.Acceleration, LinMin, LinMax);
                    MaxSlider.Value = Math.Clamp(a.CapMode == "output" && a.CapY > 0 ? a.CapY : 2, 1.05, 4);
                }
                else
                {
                    RateLabel.Text = "VELOCIDADE DA SUBIDA";
                    RateSlider.Value = ToSlider(a.DecayRate, NatMin, NatMax);
                    MaxSlider.Value = Math.Clamp(a.Limit, 1.05, 4);
                }
            }

            if (a.Style == CurveStyle.Custom)
            {
                PointsBox.Text = AxisCurve.PointsToText(a.GetPoints());
                PointsError.Text = "";
            }

            RatioSlider.Value = Math.Clamp(Work.YxRatio, 0.5, 1.5);
            RotSlider.Value = Math.Clamp(Work.Rotation, -15, 15);
            SnapSlider.Value = Math.Clamp(Work.SnapDegrees, 0, 45);
            var rep = AppState.Current.LastReport;
            RecBtn.IsEnabled = rep?.Recommended != null;
            PresetHint.Text = rep != null
                ? $"Seu Teste de Perfil deu {rep.Style}. Os estilos prontos são pontos de partida genéricos."
                : "Pontos de partida genéricos. O Teste de Perfil recomenda uma curva feita para você.";
            SeparateBox.IsChecked = Work.SeparateY;

            _loading = false;
            UpdateLabels();
            Redraw();
        }

        static double ToSlider(double v, double min, double max) =>
            Math.Clamp(Math.Log(v / min) / Math.Log(max / min) * 100, 0, 100);

        static double FromSlider(double t, double min, double max) =>
            min * Math.Pow(max / min, t / 100.0);

        void UpdateLabels()
        {
            StartVal.Text = $"{StartSlider.Value:0.#} cm/s";
            var a = CurAxis;
            RateVal.Text = a.Style == CurveStyle.Natural
                ? Ui.N(FromSlider(RateSlider.Value, NatMin, NatMax), "0.###")
                : Ui.N(FromSlider(RateSlider.Value, LinMin, LinMax), "0.####");
            MaxVal.Text = $"{MaxSlider.Value:0.00}x";

            double r = RatioSlider.Value;
            RatioVal.Text = $"{r:0.00}x";
            RotVal.Text = $"{RotSlider.Value:0.#}°";
            SnapVal.Text = SnapSlider.Value < 0.5 ? "desligado" : $"{SnapSlider.Value:0}°";
            SnapWarn.Foreground = Ui.Res(SnapSlider.Value >= 0.5 ? "Warn" : "Muted");
            int pct = (int)Math.Round(Math.Abs(1 - r) * 100);
            RatioHint.Text = pct == 0 ? "Vertical igual ao horizontal."
                : r < 1 ? $"Vertical {pct}% mais lenta que a horizontal (ajuda a não errar a altura da cabeça)."
                : $"Vertical {pct}% mais rápida que a horizontal.";
        }

        // ------------------------------------------------------------------ eventos dos controles

        void Axis_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading || StyleBox == null) return;
            LoadControls();
        }

        void Style_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            var cur = CurAxis;
            double start = cur.Style is CurveStyle.Linear or CurveStyle.Natural ? cur.InputOffset * SensMath.CmPerInch : 5;
            double max = cur.Style == CurveStyle.Linear ? cur.CapY : cur.Style == CurveStyle.Natural ? cur.Limit : 1.6;
            if (max <= 1.05) max = 1.6;

            AxisCurve next = StyleBox.SelectedIndex switch
            {
                0 => AxisCurve.NoAccel(),
                1 => AxisCurve.Linear(start, 0.03, max),
                2 => AxisCurve.Natural(start, 0.15, max),
                3 => MakeCustom(cur),
                _ => cur,
            };
            SetCurAxis(next);
            ScheduleSave();
            LoadControls();
        }

        AxisCurve MakeCustom(AxisCurve from)
        {
            var c = new AxisCurve();
            var src = CurveMath.CanPlot(from) && from.Style != CurveStyle.None ? from : _lastParametric;
            if (src != null)
                c.SetPoints(new[] { (1.0, 1.0) }.Concat(CurveMath.Sample(src, 120, 12)));
            else
                c.SetPoints(new[] { (1.0, 1.0), (10.0, 1.0), (40.0, 1.3), (100.0, 1.6) });
            return c;
        }

        void Param_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || MaxSlider == null) return;
            var a = CurAxis;
            double start = StartSlider.Value;
            double max = MaxSlider.Value;

            if (a.Style == CurveStyle.Linear)
            {
                a.InputOffset = start / SensMath.CmPerInch;
                a.Acceleration = FromSlider(RateSlider.Value, LinMin, LinMax);
                a.CapMode = "output";
                a.CapY = max;
            }
            else if (a.Style == CurveStyle.Natural)
            {
                a.InputOffset = start / SensMath.CmPerInch;
                a.DecayRate = FromSlider(RateSlider.Value, NatMin, NatMax);
                a.Limit = max;
            }
            _lastParametric = a.Clone();
            UpdateLabels();
            Redraw();
            ScheduleSave();
        }

        void Points_Apply(object sender, RoutedEventArgs e)
        {
            var pts = AxisCurve.ParsePoints(PointsBox.Text, out var err);
            if (pts == null) { PointsError.Text = err; return; }
            PointsError.Text = "";
            CurAxis.SetPoints(pts);
            PointsBox.Text = AxisCurve.PointsToText(CurAxis.GetPoints());
            Redraw();
            ScheduleSave();
        }

        void Points_FromCurve(object sender, RoutedEventArgs e)
        {
            if (_lastParametric == null)
            {
                PointsError.Text = "Ainda não há uma curva Linear/Natural para copiar.";
                return;
            }
            var c = new AxisCurve();
            c.SetPoints(new[] { (1.0, 1.0) }.Concat(CurveMath.Sample(_lastParametric, 120, 12)));
            SetCurAxis(c);
            LoadControls();
            ScheduleSave();
        }

        void Ratio_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || RatioHint == null) return;
            Work.YxRatio = Math.Round(RatioSlider.Value, 2);
            UpdateLabels();
            Redraw();
            ScheduleSave();
        }

        void Adv_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || SnapWarn == null) return;
            Work.Rotation = Math.Round(RotSlider.Value * 2) / 2;
            Work.SnapDegrees = Math.Round(SnapSlider.Value);
            UpdateLabels();
            ExplainText.Text = ExplainSummary();
            ScheduleSave();
        }

        /// <summary>Estilos prontos: pontos de partida genéricos.</summary>
        public static AccelProfile StylePreset(string style, int dpi) => AssistantTools.StylePreset(style, dpi);

        void Preset_Click(object sender, RoutedEventArgs e)
        {
            var st = AppState.Current;
            string tag = (string)((Button)sender).Tag;
            AccelProfile? p = tag == "rec" ? st.LastReport?.Recommended?.Clone() : StylePreset(tag, st.Dpi);
            if (p == null) return;
            p.Dpi = st.Dpi;
            st.Profile = p;
            AxisX.IsChecked = true;
            ScheduleSave();
            LoadControls();
            Ui.SetStatus(Status, "Carregado no editor. Clique em Aplicar para usar.", true);
        }

        void Separate_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            bool on = SeparateBox.IsChecked == true;
            if (on && !Work.SeparateY) Work.Y = Work.X.Clone(); // começa igual ao horizontal
            Work.SeparateY = on;
            if (on) AxisY.IsChecked = true;
            ScheduleSave();
            LoadControls();
        }

        void ScheduleSave()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        // ------------------------------------------------------------------ gráfico e explicação

        void Redraw()
        {
            var p = Work;
            if (!CurveMath.CanPlot(p.X))
            {
                Chart.SetMessage($"Curva no modo “{p.X.Mode}” (importada do Raw Accel).\nEla pode ser aplicada normalmente, mas o gráfico desse modo chega numa próxima versão.");
                ExplainText.Text = ExplainSummary();
                return;
            }

            double sm = p.SensMultiplier;
            bool showY = p.SeparateY || Math.Abs(p.YxRatio - 1) > 1e-6;
            Func<double, double>? compare = null;
            var applied = AppState.Current.Applied;
            if (applied != null && CurveMath.CanPlot(applied.X) && RawAccelJson.Build(applied) != RawAccelJson.Build(p))
                compare = s => applied.MultX(s) * applied.SensMultiplier;

            Chart.SetCurves(s => p.MultX(s) * sm, showY ? s => p.MultY(s) * sm : null, compare, 120);
            var rep = AppState.Current.LastReport;
            Chart.SetHistogram(rep != null ? ProfileAnalyzer.Edges : null, rep?.Histogram);
            ExplainText.Text = ExplainSummary();
        }

        static string Zone(double v) =>
            v < Controls.CurveChart.MicroMax ? "microajuste"
            : v < Controls.CurveChart.TrackMax ? "tracking / controle"
            : "flick";

        string ExplainAt(double v)
        {
            var p = Work;
            var st = AppState.Current;
            double mx = p.MultX(v) * p.SensMultiplier;
            if (double.IsNaN(mx)) return ExplainSummary();

            bool pct = st.Game.Name.StartsWith("Fortnite");
            string sensFmt(double s) => Ui.N(s, pct ? "0.0#" : "0.###") + (pct ? " %" : "");
            double baseCm = st.Cm360;

            string s = $"A {v:0} cm/s ({Zone(v)}), a sens horizontal fica {Ui.N(mx, "0.00")}x: " +
                       $"{st.Game.Name} {sensFmt(st.Sens)} vira {sensFmt(st.Sens * mx)} efetiva " +
                       $"(cm/360 {Ui.N(baseCm, "0.#")} → {Ui.N(baseCm / mx, "0.#")}).";

            if (p.SeparateY || Math.Abs(p.YxRatio - 1) > 1e-6)
            {
                double my = p.MultY(v) * p.SensMultiplier;
                s += $"\nNa vertical: {Ui.N(my, "0.00")}x.";
            }
            return s;
        }

        string ExplainSummary()
        {
            var p = Work;
            if (!CurveMath.CanPlot(p.X))
                return "Curva avançada importada do Raw Accel. Ela é mantida exatamente como estava.";
            if (p.X.Style == CurveStyle.None && !p.SeparateY)
            {
                string r = Math.Abs(p.YxRatio - 1) > 1e-6 ? $" A vertical fica {Ui.N(p.YxRatio, "0.00")}x da horizontal." : "";
                return "Sem aceleração: a sens é sempre 1x (igual à do jogo), em qualquer velocidade." + r;
            }

            double sm = p.SensMultiplier;
            string line(double v, string name) => $"• {name} ({v:0} cm/s): {Ui.N(p.MultX(v) * sm, "0.00")}x";
            var parts = new[] { line(4, "Microajuste"), line(25, "Controle"), line(60, "Flick") }.ToList();

            double? startAt = null, capAt = null;
            double maxM = Enumerable.Range(1, 300).Select(i => p.MultX(i * 0.5)).Max();
            for (double v = 0.5; v <= 150; v += 0.5)
            {
                double m = p.MultX(v);
                if (startAt == null && m > 1.01) startAt = v;
                if (capAt == null && m >= maxM - 0.01 && maxM > 1.02) { capAt = v; break; }
            }
            if (startAt != null) parts.Add($"A aceleração começa a aparecer por volta de {startAt:0} cm/s.");
            if (capAt != null && capAt < 149) parts.Add($"Chega no máximo ({Ui.N(maxM * sm, "0.00")}x) perto de {capAt:0} cm/s e para de subir.");
            if (Math.Abs(p.YxRatio - 1) > 1e-6) parts.Add($"Vertical: {Ui.N(p.YxRatio, "0.00")}x da horizontal.");
            if (Math.Abs(sm - 1) > 1e-3) parts.Add($"Multiplicador geral importado do Raw Accel: {Ui.N(sm, "0.###")}x.");
            parts.Add("Passe o mouse no gráfico para ver o efeito na sua sens de jogo.");
            return string.Join("\n", parts);
        }

        // ------------------------------------------------------------------ ações

        async void Apply_Click(object sender, RoutedEventArgs e)
        {
            ApplyBtn.IsEnabled = false;
            Ui.SetStatus(Status, "Aplicando...", true);
            Work.Dpi = AppState.Current.Dpi;
            var (ok, msg) = await DriverService.ApplyAsync(Work);
            ApplyBtn.IsEnabled = true;
            Ui.SetStatus(Status, msg, ok);
            if (!ok) return;

            AppState.Current.Applied = Work.Clone();
            AppState.Current.AddHistory(Work.Describe(), Work);
            AppState.Save();
            Redraw();
        }

        async void Disable_Click(object sender, RoutedEventArgs e)
        {
            var off = new AccelProfile { Name = "Sem aceleração", Dpi = AppState.Current.Dpi };
            Ui.SetStatus(Status, "Desativando...", true);
            var (ok, msg) = await DriverService.ApplyAsync(off);
            Ui.SetStatus(Status, ok ? "Aceleração desativada. Sua sens voltou ao normal." : msg, ok);
            if (!ok) return;
            AppState.Current.Applied = off;
            AppState.Current.AddHistory("Sem aceleração", off);
            AppState.Save();
            Redraw();
        }

        void Import_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Escolha o settings.json do Raw Accel (fica na pasta do rawaccel.exe)",
                Filter = "Configuração do Raw Accel (settings.json)|*.json|Todos os arquivos|*.*",
                FileName = "settings.json",
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var st = AppState.Current;
                if (new FileInfo(dlg.FileName).Length > 2 * 1024 * 1024)
                    throw new FormatException("o arquivo é grande demais para ser um settings.json do Raw Accel.");
                var p = RawAccelJson.Parse(File.ReadAllText(dlg.FileName), st.Dpi, out var warning);

                if (p.Dpi != st.Dpi)
                {
                    var r = MessageBox.Show(
                        $"Esse arquivo usa {p.Dpi} DPI, e a sua base no HarleAccel está com {st.Dpi} DPI.\n\nUsar {p.Dpi} DPI como a sua DPI?",
                        "HarleAccel", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (r == MessageBoxResult.Yes) st.Dpi = p.Dpi;
                    else p.Dpi = st.Dpi;
                }

                st.AddHistory("Antes de importar: " + st.Profile.Describe(), st.Profile);
                st.Profile = p;
                AppState.Save();
                AxisX.IsChecked = true;
                LoadControls();
                Ui.SetStatus(Status, warning ?? "Importado! Confira o gráfico e clique em Aplicar.", warning == null);
            }
            catch (Exception ex)
            {
                Ui.SetStatus(Status, "Não consegui importar: " + ex.Message, false);
            }
        }

        void Export_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Salvar settings.json para o Raw Accel",
                Filter = "settings.json|*.json",
                FileName = "settings.json",
            };
            if (dlg.ShowDialog() != true) return;

            string? err = RawAccelJson.Validate(Work);
            if (err != null) { Ui.SetStatus(Status, err, false); return; }

            try
            {
                Work.Dpi = AppState.Current.Dpi;
                File.WriteAllText(dlg.FileName, RawAccelJson.Build(Work));
                Ui.SetStatus(Status, "Exportado. Coloque o arquivo na pasta do rawaccel.exe para usar no Raw Accel original.", true);
            }
            catch (Exception ex)
            {
                Ui.SetStatus(Status, "Erro ao exportar: " + ex.Message, false);
            }
        }
    }
}
