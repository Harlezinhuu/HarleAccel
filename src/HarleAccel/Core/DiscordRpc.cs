// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace HarleAccel.Core
{
    /// <summary>
    /// Discord Rich Presence direto pelo pipe local do Discord (sem biblioteca externa).
    /// Precisa do "Application ID" de um app criado em discord.com/developers (com uma imagem chamada "logo").
    /// </summary>
    public static class DiscordRpc
    {
        static NamedPipeClientStream? _pipe;
        static readonly SemaphoreSlim Lock = new(1, 1);
        static readonly long StartUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        static string? _lastDetails, _lastState;
        static long _lastStart;

        public static bool Connected => _pipe?.IsConnected == true;

        /// <summary>
        /// Atualiza o status. Falhas são silenciosas (Discord fechado, ID errado...).
        /// Com <paramref name="gameStart"/>, o tempo conta a partir do jogo e a lista de membros
        /// mostra a linha de cima (o nome do jogo) em vez de "HarleAccel".
        /// </summary>
        public static async Task SetAsync(string details, string state, long? gameStart = null)
        {
            var s = AppState.Current.Settings;
            if (!s.DiscordEnabled || string.IsNullOrWhiteSpace(s.DiscordClientId))
            {
                await ClearAsync();
                return;
            }
            long start = gameStart ?? StartUnix;
            if (details == _lastDetails && state == _lastState && start == _lastStart && Connected) return;

            await Lock.WaitAsync();
            try
            {
                if (!Connected && !await ConnectAsync(s.DiscordClientId.Trim())) return;

                var activity = new JsonObject
                {
                    ["details"] = details,
                    ["state"] = state,
                    ["timestamps"] = new JsonObject { ["start"] = start },
                    ["assets"] = new JsonObject
                    {
                        ["large_image"] = "logo",
                        ["large_text"] = "HarleAccel · Criado por Harle",
                    },
                };
                // 2 = a lista de membros mostra o "details" (o jogo). Discord antigo ignora e mostra o nome do app.
                if (gameStart != null) activity["status_display_type"] = 2;

                var payload = new JsonObject
                {
                    ["cmd"] = "SET_ACTIVITY",
                    ["nonce"] = Guid.NewGuid().ToString(),
                    ["args"] = new JsonObject
                    {
                        ["pid"] = Environment.ProcessId,
                        ["activity"] = activity,
                    },
                };
                await WriteFrameAsync(1, payload.ToJsonString());
                await ReadFrameAsync(); // lê a resposta para o pipe não encher
                _lastDetails = details;
                _lastState = state;
                _lastStart = start;
            }
            catch
            {
                Disconnect();
            }
            finally
            {
                Lock.Release();
            }
        }

        public static async Task ClearAsync()
        {
            if (!Connected) return;
            await Lock.WaitAsync();
            try
            {
                var payload = new JsonObject
                {
                    ["cmd"] = "SET_ACTIVITY",
                    ["nonce"] = Guid.NewGuid().ToString(),
                    ["args"] = new JsonObject { ["pid"] = Environment.ProcessId },
                };
                await WriteFrameAsync(1, payload.ToJsonString());
            }
            catch { }
            finally
            {
                Disconnect();
                Lock.Release();
            }
        }

        static async Task<bool> ConnectAsync(string clientId)
        {
            for (int i = 0; i < 10; i++)
            {
                var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    await pipe.ConnectAsync(300);
                    _pipe = pipe;
                    var hello = new JsonObject { ["v"] = 1, ["client_id"] = clientId };
                    await WriteFrameAsync(0, hello.ToJsonString());
                    await ReadFrameAsync(); // READY (ou erro)
                    return true;
                }
                catch
                {
                    pipe.Dispose();
                    _pipe = null;
                }
            }
            return false;
        }

        static async Task WriteFrameAsync(int op, string json)
        {
            if (_pipe == null) throw new IOException("Discord não conectado.");
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] frame = new byte[8 + body.Length];
            BitConverter.GetBytes(op).CopyTo(frame, 0);
            BitConverter.GetBytes(body.Length).CopyTo(frame, 4);
            body.CopyTo(frame, 8);
            await _pipe.WriteAsync(frame);
            await _pipe.FlushAsync();
        }

        static async Task<string> ReadFrameAsync()
        {
            if (_pipe == null) throw new IOException("Discord não conectado.");
            var header = new byte[8];
            using var cts = new CancellationTokenSource(2000);
            await _pipe.ReadExactlyAsync(header, cts.Token);
            int len = BitConverter.ToInt32(header, 4);
            if (len < 0 || len > 65536) throw new IOException("Resposta inválida do Discord.");
            var body = new byte[len];
            await _pipe.ReadExactlyAsync(body, cts.Token);
            return Encoding.UTF8.GetString(body);
        }

        static void Disconnect()
        {
            try { _pipe?.Dispose(); } catch { }
            _pipe = null;
            _lastDetails = _lastState = null;
        }
    }
}
