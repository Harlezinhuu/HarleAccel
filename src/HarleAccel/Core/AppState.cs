// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HarleAccel.Core
{
    public sealed class HistoryEntry
    {
        public DateTime Date { get; set; } = DateTime.Now;
        public string Title { get; set; } = "";
        public AccelProfile Profile { get; set; } = new();
    }

    /// <summary>Tudo que o app guarda entre uma abertura e outra (%AppData%\HarleAccel).</summary>
    public sealed class AppState
    {
        // base de sensibilidade
        public int Dpi { get; set; } = 800;
        public string GameName { get; set; } = "Valorant";
        public double Sens { get; set; } = 0.4;

        public bool WizardDone { get; set; }

        /// <summary>Pasta de um Raw Accel que a pessoa já tinha (opcional). Se vazio, usa a pasta baixada pelo app.</summary>
        public string? CustomRawAccelDir { get; set; }

        /// <summary>Curva sendo editada.</summary>
        public AccelProfile Profile { get; set; } = new();

        /// <summary>Última curva enviada ao driver (null = nunca aplicou).</summary>
        public AccelProfile? Applied { get; set; }

        public List<HistoryEntry> History { get; set; } = new();

        /// <summary>Último Teste de Perfil.</summary>
        public ProfileReport? LastReport { get; set; }

        public List<AimResult> AimResults { get; set; } = new();

        public List<TrainingSession> Training { get; set; } = new();

        public AppSettings Settings { get; set; } = new();

        /// <summary>
        /// True enquanto o app deixou o driver neutro para um teste (DPI real, angle snapping, Teste de Perfil).
        /// Se o app fechar no meio, a curva é restaurada na próxima abertura.
        /// </summary>
        public bool NeutralActive { get; set; }

        // ------------------------------------------------------------------

        public static string Folder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HarleAccel");

        static string FilePath => Path.Combine(Folder, "state.json");

        static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, IncludeFields = false };

        public static AppState Current { get; private set; } = new();

        public static event Action? Changed;

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    Current = JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath), Opts) ?? new AppState();
            }
            catch
            {
                // arquivo corrompido: guarda uma cópia e começa do zero
                try { File.Copy(FilePath, FilePath + ".bak", true); } catch { }
                Current = new AppState();
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Opts));
                File.Move(tmp, FilePath, true);
            }
            catch { /* salvar é melhor esforço */ }
            Changed?.Invoke();
        }

        public static void NotifyChanged() => Changed?.Invoke();

        [JsonIgnore]
        public Game Game => Games.ByName(GameName);

        [JsonIgnore]
        public double Cm360 => SensMath.Cm360(Dpi, Sens, Game);

        public void AddHistory(string title, AccelProfile p)
        {
            History.Insert(0, new HistoryEntry { Title = title, Profile = p.Clone(), Date = DateTime.Now });
            if (History.Count > 60) History = History.Take(60).ToList();
        }

        public void AddAim(AimResult r)
        {
            AimResults.Add(r);
            if (AimResults.Count > 300) AimResults = AimResults.Skip(AimResults.Count - 300).ToList();
        }
    }
}
