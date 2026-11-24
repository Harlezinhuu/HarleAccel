// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HarleAccel.Controls;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    public partial class ChatView : UserControl, IRefreshable
    {
        bool _busy;
        CancellationTokenSource? _cts;

        static readonly string[] SuggestionTexts =
        {
            "Converte 8% do Fortnite pro Valorant (800 DPI)",
            "Explica a minha curva",
            "Minha mira passa do alvo nos flicks, o que mudo?",
            "Qual estilo de curva combina comigo?",
        };

        static readonly Dictionary<string, (string Key, string Label)> Screens = new()
        {
            ["inicio"] = ("home", "Início"),
            ["sensibilidade"] = ("sens", "Sensibilidade"),
            ["curva"] = ("curve", "Curva"),
            ["historico"] = ("history", "Histórico"),
            ["teste_de_perfil"] = ("profile", "Teste de Perfil"),
            ["aim_test"] = ("aim", "Aim Test e A/B"),
            ["diagnostico"] = ("tools", "Diagnóstico do mouse"),
            ["treino"] = ("training", "Treino"),
            ["configuracoes"] = ("settings", "Configurações"),
        };

        public ChatView()
        {
            InitializeComponent();
            ChatStore.Load();
            foreach (var s in SuggestionTexts)
            {
                var b = new Button { Content = s, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(12, 6, 12, 6), FontSize = 12, FontWeight = FontWeights.Normal };
                b.Click += (_, _) => { Input.Text = s; Send(); };
                Suggestions.Children.Add(b);
            }
        }

        public void Refresh()
        {
            bool ai = SecretStore.HasGeminiKey;
            ModeDot.Fill = Ui.Res(ai ? "Ok" : "Warn");
            ModeText.Text = ai
                ? $"Gemini conectado ({AppState.Current.Settings.GeminiModel}) · conversões são feitas pelo app"
                : "Modo sem IA: entende conversões de sensibilidade e “explica minha curva”. Para conversa livre, coloque a chave grátis do Gemini em Configurações.";
            RenderAll();
            Input.Focus();
        }

        // ------------------------------------------------------------------ envio

        void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                Send();
            }
        }

        void Send_Click(object sender, RoutedEventArgs e) => Send();

        async void Send()
        {
            string text = Input.Text.Trim();
            if (text.Length == 0 || _busy) return;
            if (text.Length > 1500) text = text[..1500]; // mensagens curtas: protege a cota e evita abuso
            Input.Text = "";
            Add(new ChatItem { Role = "user", Text = text });

            // 1) pedidos comuns: o app resolve sozinho (sem internet, sem gastar a cota)
            var req = LocalParser.TryParse(text);
            if (req != null)
            {
                var conv = AssistantTools.Convert(req.From, req.Sens, req.Dpi, req.To);
                Add(new ChatItem { Kind = "conversion", Conversion = conv });
                return;
            }

            bool hasKey = SecretStore.HasGeminiKey;
            if (!hasKey && LocalParser.AsksToExplainCurve(text))
            {
                Add(new ChatItem { Text = AssistantTools.ExplainApplied(), OpenScreen = "curva" });
                return;
            }
            if (!hasKey)
            {
                Add(new ChatItem
                {
                    Text = "Sem IA configurada, eu entendo:\n" +
                           "• conversões, por exemplo “8% fortnite 800 dpi pro valorant” ou “0.4 valorant pro cs2”\n" +
                           "• “explica minha curva”\n\n" +
                           "Para perguntas livres e sugestões de ajuste, cole a sua chave grátis do Gemini em Configurações.",
                    OpenScreen = "configuracoes",
                });
                return;
            }

            // 2) conversa livre: Gemini entende, as ferramentas do app fazem o trabalho
            _busy = true;
            SendBtn.IsEnabled = false;
            var thinking = Bubble("Pensando...", false, muted: true);
            Messages.Children.Add(thinking);
            ScrollDown();
            _cts = new CancellationTokenSource();

            var history = ChatStore.Items.Take(ChatStore.Items.Count - 1).ToList(); // sem a mensagem que acabou de entrar
            try
            {
                string reply = await GeminiClient.SendAsync(history, text, RunTool, _cts.Token);
                Messages.Children.Remove(thinking);
                if (reply.Length > 6000) reply = reply[..6000] + "…";
                if (!string.IsNullOrWhiteSpace(reply)) Add(new ChatItem { Text = reply });
            }
            catch (GeminiClient.GeminiException ex)
            {
                Messages.Children.Remove(thinking);
                Add(new ChatItem { Text = "⚠️ " + ex.Message, OpenScreen = ex.Message.Contains("Configurações") ? "configuracoes" : null });
            }
            catch (OperationCanceledException)
            {
                Messages.Children.Remove(thinking);
            }
            catch (Exception ex)
            {
                Messages.Children.Remove(thinking);
                Add(new ChatItem { Text = "⚠️ Algo deu errado: " + ex.Message });
            }
            finally
            {
                _busy = false;
                SendBtn.IsEnabled = true;
                Input.Focus();
            }
        }

        /// <summary>Ferramentas que a IA pode usar. Tudo roda aqui no app.</summary>
        (JsonObject Response, ChatItem? Card) RunTool(string name, JsonObject args)
        {
            switch (name)
            {
                case "converter_sensibilidade":
                {
                    var from = Games.Find(args["jogo_origem"]?.GetValue<string>());
                    if (from == null) return (new JsonObject { ["erro"] = "Jogo de origem não suportado." }, null);
                    double sens = 0;
                    try { sens = args["sens"]!.GetValue<double>(); } catch { }
                    if (sens <= 0) return (new JsonObject { ["erro"] = "Sensibilidade inválida." }, null);
                    int? dpi = null;
                    try { if (args["dpi"] != null) dpi = (int)Math.Round(args["dpi"]!.GetValue<double>()); } catch { }
                    var to = Games.Find(args["jogo_destino"]?.GetValue<string>());
                    var conv = AssistantTools.Convert(from, sens, dpi, to);
                    var card = new ChatItem { Kind = "conversion", Conversion = conv };
                    Add(card);
                    return (AssistantTools.ConversionToJson(conv), card);
                }
                case "ver_setup_do_usuario":
                    return (AssistantTools.SetupSummary(), null);

                case "propor_mudanca_na_curva":
                {
                    var prop = AssistantTools.ProposeChange(args, out var err);
                    if (prop == null) return (new JsonObject { ["erro"] = err }, null);
                    var card = new ChatItem { Kind = "permission", Proposal = prop };
                    Add(card);
                    return (new JsonObject
                    {
                        ["status"] = "aguardando a pessoa clicar em Aplicar ou Recusar no cartão",
                        ["mudancas"] = new JsonArray(prop.Changes.Select(c => (JsonNode)c).ToArray()),
                        ["curva_nova"] = prop.After.Describe(),
                        ["driver_ativo"] = DriverService.Status == DriverStatus.Active,
                    }, card);
                }
                case "abrir_tela":
                {
                    string tela = args["tela"]?.GetValue<string>() ?? "";
                    if (!Screens.ContainsKey(tela)) return (new JsonObject { ["erro"] = "Tela desconhecida." }, null);
                    var card = new ChatItem { Text = "Atalho:", OpenScreen = tela };
                    Add(card);
                    return (new JsonObject { ["status"] = "botão para abrir a tela mostrado no chat" }, card);
                }
                default:
                    return (new JsonObject { ["erro"] = "Ferramenta desconhecida." }, null);
            }
        }

        // ------------------------------------------------------------------ renderização

        void Add(ChatItem item)
        {
            ChatStore.Items.Add(item);
            ChatStore.Save();
            Messages.Children.Add(Render(item));
            ScrollDown();
        }

        void RenderAll()
        {
            Messages.Children.Clear();
            if (ChatStore.Items.Count == 0)
            {
                Messages.Children.Add(Bubble(
                    "Oi! Eu sou o Harle IA. Posso converter a sua sensibilidade entre jogos, explicar a sua curva e sugerir ajustes. " +
                    "Quando eu sugerir uma mudança na curva, ela só é aplicada se você clicar em Aplicar.", false));
            }
            foreach (var item in ChatStore.Items) Messages.Children.Add(Render(item));
            ScrollDown();
        }

        void ScrollDown() => Dispatcher.BeginInvoke(() => Scroller.ScrollToEnd(), System.Windows.Threading.DispatcherPriority.Background);

        UIElement Render(ChatItem item) => item.Kind switch
        {
            "conversion" when item.Conversion != null => ConversionCard(item.Conversion),
            "permission" when item.Proposal != null => PermissionCard(item),
            _ => TextItem(item),
        };

        UIElement TextItem(ChatItem item)
        {
            var b = Bubble(item.Text, item.Role == "user");
            if (item.OpenScreen != null && Screens.TryGetValue(item.OpenScreen, out var sc) && b.Child is StackPanel sp)
            {
                var btn = new Button { Content = "Abrir " + sc.Label, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
                btn.Click += (_, _) => MainWindow.Instance?.Navigate(sc.Key);
                sp.Children.Add(btn);
            }
            return b;
        }

        Border Bubble(string text, bool user, bool muted = false)
        {
            var sp = new StackPanel();
            if (!string.IsNullOrEmpty(text))
            {
                var tb = Ui.Text(text, 14, muted ? "Muted" : "Silver");
                tb.LineHeight = 21;
                sp.Children.Add(tb);
            }
            return new Border
            {
                Background = Ui.Res(user ? "AccentSoft" : "Card"),
                BorderBrush = Ui.Res(user ? "Accent" : "Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(user ? 80 : 0, 0, user ? 0 : 80, 10),
                HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 680,
                Child = sp,
            };
        }

        Border CardShell(UIElement content) => new()
        {
            Background = Ui.Res("Card"),
            BorderBrush = Ui.Res("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 80, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 420,
            MaxWidth = 680,
            Child = content,
        };

        UIElement ConversionCard(ConversionResult c)
        {
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text("CONVERSÃO DE SENSIBILIDADE", 11, "Muted", FontWeights.SemiBold));
            sp.Children.Add(Ui.Text($"{c.From} {c.SensText} · {c.Dpi} DPI{(c.DpiFromBase ? " (a DPI salva no app)" : "")} · {c.Cm360:0.#} cm/360", 14, "Silver", FontWeights.SemiBold));
            if (c.Note != null) sp.Children.Add(Ui.Text(c.Note, 12, "Warn"));

            var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int row = 0;
            foreach (var i in c.Items)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var name = Ui.Text(i.Game, i.Highlight ? 15 : 13, "Silver", i.Highlight ? FontWeights.SemiBold : FontWeights.Normal);
                var sens = Ui.Text(i.SensText, i.Highlight ? 20 : 14, "AccentHi", FontWeights.Bold);
                var edpi = Ui.Text($"eDPI {i.EDpi:0.#}", 11, "Muted");
                var copy = new Button { Content = "Copiar", Style = (Style)FindResource(i.Highlight ? "Primary" : "Ghost"), Padding = new Thickness(10, 5, 10, 5), Tag = i.Sens.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                copy.Click += (s, _) => { try { Clipboard.SetText((string)((Button)s).Tag); ((Button)s).Content = "Copiado ✓"; } catch { } };
                foreach (var (el, col) in new (FrameworkElement, int)[] { (name, 0), (sens, 1), (edpi, 2), (copy, 3) })
                {
                    Grid.SetRow(el, row); Grid.SetColumn(el, col);
                    el.Margin = new Thickness(0, 3, 0, 3);
                    grid.Children.Add(el);
                }
                row++;
            }
            sp.Children.Add(grid);
            return CardShell(sp);
        }

        UIElement PermissionCard(ChatItem item)
        {
            var prop = item.Proposal!;
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text("PERMISSÃO: MUDANÇA NA CURVA", 11, "Warn", FontWeights.SemiBold));
            if (!string.IsNullOrWhiteSpace(prop.Reason)) sp.Children.Add(Ui.Text(prop.Reason, 13, "Silver"));
            foreach (var ch in prop.Changes) sp.Children.Add(Ui.Text("• " + ch, 13, "AccentHi", FontWeights.SemiBold));

            var before = prop.Before;
            var after = prop.After;
            if (CurveMath.CanPlot(after.X))
            {
                var chart = new CurveChart { Height = 190, Margin = new Thickness(0, 10, 0, 6) };
                chart.SetCurves(s => after.MultX(s) * after.SensMultiplier,
                                Math.Abs(after.YxRatio - 1) > 1e-6 ? s => after.MultY(s) * after.SensMultiplier : null,
                                CurveMath.CanPlot(before.X) ? s => before.MultX(s) * before.SensMultiplier : null, 120);
                sp.Children.Add(chart);
                sp.Children.Add(Ui.Text("Linha pontilhada = curva de antes · linha azul = curva proposta", 11, "Muted"));
            }

            var status = Ui.Text("", 12, "Muted");
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            sp.Children.Add(buttons);
            sp.Children.Add(status);

            void Build()
            {
                buttons.Children.Clear();
                switch (prop.State)
                {
                    case "pending":
                        var apply = new Button { Content = "Aplicar", Style = (Style)FindResource("Primary"), Margin = new Thickness(0, 0, 8, 0) };
                        var refuse = new Button { Content = "Recusar" };
                        apply.Click += async (_, _) =>
                        {
                            apply.IsEnabled = refuse.IsEnabled = false;
                            Ui.SetStatus(status, "Aplicando...", true);
                            var p = after.Clone();
                            p.Dpi = AppState.Current.Dpi;
                            var (ok, msg) = await DriverService.ApplyAsync(p);
                            if (!ok) { Ui.SetStatus(status, msg, false); apply.IsEnabled = refuse.IsEnabled = true; return; }
                            var st = AppState.Current;
                            st.Applied = p.Clone();
                            st.Profile = p.Clone();
                            st.AddHistory("Harle IA: " + string.Join(", ", prop.Changes), p);
                            AppState.Save();
                            prop.State = "applied";
                            ChatStore.Save();
                            Ui.SetStatus(status, "Aplicada. Se não gostar, é só desfazer.", true);
                            Build();
                        };
                        refuse.Click += (_, _) =>
                        {
                            prop.State = "refused";
                            ChatStore.Save();
                            Ui.SetStatus(status, "", true);
                            Build();
                        };
                        buttons.Children.Add(apply);
                        buttons.Children.Add(refuse);
                        if (DriverService.Status != DriverStatus.Active)
                            status.Text = "O driver não está ativo agora, então aplicar vai falhar. Termine o Assistente primeiro.";
                        break;

                    case "applied":
                        buttons.Children.Add(Ui.Text("✓ Aplicada", 13, "Ok", FontWeights.SemiBold));
                        var undo = new Button { Content = "Desfazer", Margin = new Thickness(12, 0, 0, 0) };
                        undo.Click += async (_, _) =>
                        {
                            undo.IsEnabled = false;
                            var p = before.Clone();
                            p.Dpi = AppState.Current.Dpi;
                            var (ok, msg) = await DriverService.ApplyAsync(p);
                            if (!ok) { Ui.SetStatus(status, msg, false); undo.IsEnabled = true; return; }
                            var st = AppState.Current;
                            st.Applied = p.Clone();
                            st.Profile = p.Clone();
                            AppState.Save();
                            prop.State = "undone";
                            ChatStore.Save();
                            Ui.SetStatus(status, "Voltou para a curva de antes.", true);
                            Build();
                        };
                        buttons.Children.Add(undo);
                        break;

                    case "refused":
                        buttons.Children.Add(Ui.Text("Recusada", 13, "Muted", FontWeights.SemiBold));
                        break;

                    case "undone":
                        buttons.Children.Add(Ui.Text("Desfeita (voltou para a curva de antes)", 13, "Muted", FontWeights.SemiBold));
                        break;
                }
            }
            Build();
            return CardShell(sp);
        }

        void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) _cts?.Cancel();
            ChatStore.Clear();
            RenderAll();
        }
    }
}
