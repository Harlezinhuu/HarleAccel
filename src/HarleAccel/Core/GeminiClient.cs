// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace HarleAccel.Core
{
    /// <summary>Guarda a chave do Gemini criptografada (DPAPI): só este usuário do Windows consegue abrir.</summary>
    public static class SecretStore
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HarleAccel.Gemini.v1");

        public static string Protect(string plain)
        {
            if (string.IsNullOrWhiteSpace(plain)) return "";
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain.Trim()), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }

        public static string Unprotect(string protectedB64)
        {
            if (string.IsNullOrWhiteSpace(protectedB64)) return "";
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedB64), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return ""; }
        }

        public static string GeminiKey => Unprotect(AppState.Current.Settings.GeminiKeyProtected);
        public static bool HasGeminiKey => GeminiKey.Length > 0;
    }

    /// <summary>
    /// Conversa com o Gemini (generateContent + function calling).
    /// A IA só ENTENDE o pedido: as contas e as mudanças são feitas pelas ferramentas do app.
    /// </summary>
    public static class GeminiClient
    {
        public const string DefaultModel = "gemini-3.1-flash-lite"; // modelo com nível grátis confirmado na página de preços do Google

        const string SystemPrompt = """
Você é o Harle IA, o assistente dentro do HarleAccel, um app de aceleração de mouse para Windows criado por Harle.
Responda SEMPRE em português do Brasil, curto e direto (no máximo uns 5 parágrafos curtos), com tom de amigo que entende de mira.

Regras importantes:
1. NUNCA faça contas de sensibilidade de cabeça. Para converter sensibilidade entre jogos ou calcular cm/360, use SEMPRE a ferramenta converter_sensibilidade. O app mostra o resultado num cartão com botão de copiar; você só comenta.
2. Para entender o setup da pessoa (jogo, sens, DPI, curva aplicada, Teste de Perfil, aim tests), use ver_setup_do_usuario antes de dar conselhos sobre a curva.
3. Para sugerir uma mudança na curva, use propor_mudanca_na_curva. Isso NÃO aplica nada: aparece um cartão com os botões Aplicar e Recusar, e só a pessoa decide. Nunca diga que já aplicou. Diga para ela conferir o cartão.
4. Prefira mudanças pequenas e uma de cada vez (por exemplo sens máxima +0.1x ou força ±15%). Explique o porquê em uma frase.
5. Fortnite usa sensibilidade em porcentagem (8% = 8). Jogos suportados: Valorant, Counter-Strike 2, Fortnite, Apex Legends, Overwatch 2, CoD Warzone/BO6, Destiny 2, Deadlock, KovaaK's.
6. Se a pessoa não disser a DPI, a ferramenta usa a DPI salva no app; avise isso.
7. Assuntos fora de mouse, mira, sensibilidade, aceleração, treino de aim e do próprio app: responda que você só ajuda com isso.
8. Conceitos úteis: microajuste ≈ abaixo de 8 cm/s, tracking/controle 8 a 40 cm/s, flick acima de 40 cm/s. Em 1x a sens é a do jogo. A aceleração usa o driver oficial do Raw Accel.
9. Telas do app que você pode abrir com abrir_tela: inicio, sensibilidade, curva, historico, teste_de_perfil, buscador_de_sens, aim_test, diagnostico, treino, configuracoes.
10. O setup traz o diagnóstico dos aim tests: "passou do alvo" demais = sens alta naquela faixa; "parou antes" demais = sens baixa. Alvos perto = sens base; alvos longe = flicks (onde a curva age). Para achar a sens base, recomende o Buscador de sens.
""";

        static JsonArray Tools()
        {
            var gameEnum = new JsonArray(Games.All.Select(g => (JsonNode)g.Name).ToArray());
            return new JsonArray
            {
                new JsonObject
                {
                    ["functionDeclarations"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "converter_sensibilidade",
                            ["description"] = "Converte a sensibilidade de um jogo para outro (ou para todos) mantendo o mesmo cm/360, e calcula cm/360 e eDPI. Faz a conta exata.",
                            ["parameters"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["jogo_origem"] = new JsonObject { ["type"] = "string", ["enum"] = gameEnum.DeepClone(), ["description"] = "Jogo da sensibilidade informada." },
                                    ["sens"] = new JsonObject { ["type"] = "number", ["description"] = "Sensibilidade no jogo de origem. Fortnite em porcentagem (8% = 8)." },
                                    ["dpi"] = new JsonObject { ["type"] = "integer", ["description"] = "DPI do mouse, se a pessoa disse. Se não, deixe vazio." },
                                    ["jogo_destino"] = new JsonObject { ["type"] = "string", ["enum"] = gameEnum.DeepClone(), ["description"] = "Jogo para onde converter. Vazio = todos os jogos." },
                                },
                                ["required"] = new JsonArray { "jogo_origem", "sens" },
                            },
                        },
                        new JsonObject
                        {
                            ["name"] = "ver_setup_do_usuario",
                            ["description"] = "Mostra o setup atual da pessoa: jogo, sens, DPI, cm/360, curva aplicada com multiplicadores, resultado do Teste de Perfil e últimos aim tests.",
                        },
                        new JsonObject
                        {
                            ["name"] = "propor_mudanca_na_curva",
                            ["description"] = "Propõe uma mudança na curva de aceleração. NÃO aplica: mostra um cartão para a pessoa clicar em Aplicar ou Recusar. Mande só os campos que mudam.",
                            ["parameters"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["estilo"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "sem aceleração", "linear", "natural", "flicker", "tracker", "hibrido" }, ["description"] = "Trocar o tipo da curva ou usar um estilo pronto." },
                                    ["comeca_em_cm_s"] = new JsonObject { ["type"] = "number", ["description"] = "Velocidade (cm/s) em que a aceleração começa. 0 a 40." },
                                    ["sens_maxima"] = new JsonObject { ["type"] = "number", ["description"] = "Teto do multiplicador de sens, ex.: 1.6. Entre 1.05 e 3." },
                                    ["forca_percentual"] = new JsonObject { ["type"] = "number", ["description"] = "Quanto mudar a força/velocidade da subida, em %, ex.: 15 ou -20." },
                                    ["vertical"] = new JsonObject { ["type"] = "number", ["description"] = "Multiplicador da sens vertical (Y/X), 0.5 a 1.5. 0.9 = vertical 10% mais lenta." },
                                    ["rotacao"] = new JsonObject { ["type"] = "number", ["description"] = "Rotação em graus, -15 a 15." },
                                    ["angle_snapping"] = new JsonObject { ["type"] = "number", ["description"] = "Angle snapping do driver em graus (0 = desligado, recomendado para jogos)." },
                                    ["motivo"] = new JsonObject { ["type"] = "string", ["description"] = "Explicação curta, em português, do porquê da mudança." },
                                },
                                ["required"] = new JsonArray { "motivo" },
                            },
                        },
                        new JsonObject
                        {
                            ["name"] = "abrir_tela",
                            ["description"] = "Abre uma tela do HarleAccel para a pessoa.",
                            ["parameters"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["tela"] = new JsonObject
                                    {
                                        ["type"] = "string",
                                        ["enum"] = new JsonArray { "inicio", "sensibilidade", "curva", "historico", "teste_de_perfil", "buscador_de_sens", "aim_test", "diagnostico", "treino", "configuracoes" },
                                    },
                                },
                                ["required"] = new JsonArray { "tela" },
                            },
                        },
                    },
                },
            };
        }

        /// <summary>Executa uma ferramenta pedida pela IA. Retorna a resposta para a IA e, se houver, um cartão para o chat.</summary>
        public delegate (JsonObject Response, ChatItem? Card) ToolRunner(string name, JsonObject args);

        public sealed class GeminiException : Exception
        {
            public GeminiException(string msg) : base(msg) { }
        }

        public static async Task<string> SendAsync(IReadOnlyList<ChatItem> history, string userText, ToolRunner runTool, CancellationToken ct)
        {
            string key = SecretStore.GeminiKey;
            if (key.Length == 0) throw new GeminiException("Configure a sua chave do Gemini em Configurações.");
            string model = string.IsNullOrWhiteSpace(AppState.Current.Settings.GeminiModel) ? DefaultModel : AppState.Current.Settings.GeminiModel.Trim();

            var contents = BuildContext(history);
            contents.Add(new JsonObject { ["role"] = "user", ["parts"] = new JsonArray { new JsonObject { ["text"] = userText } } });

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent";

            for (int round = 0; round < 6; round++)
            {
                var body = new JsonObject
                {
                    ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = SystemPrompt } } },
                    ["contents"] = contents.DeepClone(),
                    ["tools"] = Tools(),
                    ["generationConfig"] = new JsonObject { ["temperature"] = 0.4, ["maxOutputTokens"] = 2048 },
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
                };
                req.Headers.Add("x-goog-api-key", key);

                HttpResponseMessage resp;
                try { resp = await http.SendAsync(req, ct); }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new GeminiException("O Gemini demorou demais para responder. Tente de novo."); }
                catch (HttpRequestException) { throw new GeminiException("Sem conexão com o Gemini. Confira a internet."); }

                string raw = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode) throw new GeminiException(FriendlyError((int)resp.StatusCode, raw, model));

                var json = JsonNode.Parse(raw);
                var content = json?["candidates"]?[0]?["content"] as JsonObject;
                if (content == null)
                {
                    string reason = json?["candidates"]?[0]?["finishReason"]?.GetValue<string>()
                                    ?? json?["promptFeedback"]?["blockReason"]?.GetValue<string>() ?? "sem resposta";
                    throw new GeminiException($"O Gemini não respondeu ({reason}). Tente reformular.");
                }

                var parts = content["parts"] as JsonArray ?? new JsonArray();
                var calls = parts.OfType<JsonObject>().Where(p => p["functionCall"] != null).ToList();

                if (calls.Count == 0)
                {
                    var text = string.Join("", parts.OfType<JsonObject>()
                        .Where(p => p["text"] != null && p["thought"]?.GetValue<bool>() != true)
                        .Select(p => p["text"]!.GetValue<string>()));
                    return text.Trim();
                }

                // devolve a resposta do modelo EXATAMENTE como veio (inclui as assinaturas de pensamento)
                contents.Add(content.DeepClone());

                var responses = new JsonArray();
                foreach (var c in calls)
                {
                    var fc = c["functionCall"]!.AsObject();
                    string name = fc["name"]?.GetValue<string>() ?? "";
                    var args = fc["args"] as JsonObject ?? new JsonObject();
                    JsonObject result;
                    try { result = runTool(name, (JsonObject)args.DeepClone()).Response; }
                    catch (Exception ex) { result = new JsonObject { ["erro"] = ex.Message }; }

                    var fr = new JsonObject { ["name"] = name, ["response"] = result };
                    if (fc["id"] != null) fr["id"] = fc["id"]!.DeepClone();
                    responses.Add(new JsonObject { ["functionResponse"] = fr });
                }
                contents.Add(new JsonObject { ["role"] = "user", ["parts"] = responses });
            }

            return "Me perdi no meio do caminho. Pode repetir o pedido de um jeito mais simples?";
        }

        /// <summary>Últimas mensagens do chat como contexto (texto simples).</summary>
        static JsonArray BuildContext(IReadOnlyList<ChatItem> history)
        {
            var list = new List<(string Role, string Text)>();
            foreach (var item in history.TakeLast(14))
            {
                string role = item.Role == "user" ? "user" : "model";
                string text = item.Kind switch
                {
                    "conversion" when item.Conversion != null =>
                        $"[Cartão de conversão mostrado: {item.Conversion.From} {item.Conversion.SensText} com {item.Conversion.Dpi} DPI ({item.Conversion.Cm360} cm/360) → " +
                        string.Join(", ", item.Conversion.Items.Take(4).Select(i => $"{i.Game} {i.SensText}")) + "]",
                    "permission" when item.Proposal != null =>
                        $"[Proposta de curva: {string.Join("; ", item.Proposal.Changes)}. Situação: {item.Proposal.State switch { "applied" => "a pessoa APLICOU", "refused" => "a pessoa RECUSOU", "undone" => "a pessoa aplicou e depois DESFEZ", _ => "aguardando a pessoa" }}]",
                    _ => item.Text,
                };
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (list.Count > 0 && list[^1].Role == role) list[^1] = (role, list[^1].Text + "\n" + text);
                else list.Add((role, text));
            }
            // a conversa precisa começar pelo usuário
            while (list.Count > 0 && list[0].Role != "user") list.RemoveAt(0);
            // e a última precisa ser do modelo (o pedido novo entra depois)
            if (list.Count > 0 && list[^1].Role == "user") list.RemoveAt(list.Count - 1);

            var arr = new JsonArray();
            foreach (var (role, text) in list)
                arr.Add(new JsonObject { ["role"] = role, ["parts"] = new JsonArray { new JsonObject { ["text"] = text } } });
            return arr;
        }

        static string FriendlyError(int status, string raw, string model)
        {
            string msg = "";
            try { msg = JsonNode.Parse(raw)?["error"]?["message"]?.GetValue<string>() ?? ""; } catch { }
            return status switch
            {
                400 when msg.Contains("API key", StringComparison.OrdinalIgnoreCase) => "A chave do Gemini é inválida. Confira em Configurações.",
                401 or 403 => "O Gemini recusou a chave (sem permissão). Gere uma nova no Google AI Studio.",
                404 => $"O modelo “{model}” não foi encontrado. Troque o modelo em Configurações.",
                429 => "Você chegou no limite grátis do Gemini por agora. Espere um pouco e tente de novo (as conversões continuam funcionando sem IA). O HarleAccel nunca cobra nada.",
                >= 500 => "O Gemini está com problema agora. Tente de novo em alguns minutos.",
                _ => "Erro do Gemini: " + (msg.Length > 0 ? msg : $"HTTP {status}"),
            };
        }
    }
}
