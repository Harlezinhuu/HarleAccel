// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    /// <summary>Cronômetro pequeno que fica por cima do aim trainer durante o treino.</summary>
    public sealed class TrainingTimerWindow : Window
    {
        readonly Routine _routine;
        readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
        int _index;
        TimeSpan _left;
        DateTime _lastTick;
        bool _paused, _askingScore;

        readonly TextBlock _blockText, _scenarioText, _focusText, _clockText, _nextText;
        readonly Button _pauseBtn, _nextBtn;
        readonly StackPanel _scorePanel, _runPanel;
        readonly TextBox _scoreBox;

        public TrainingSession? Session { get; private set; }
        public bool Finished { get; private set; }

        public TrainingTimerWindow(Routine routine)
        {
            _routine = routine;
            Session = new TrainingSession { Platform = routine.Platform, Routine = routine.Title };

            Title = "Treino · HarleAccel";
            Width = 400;
            SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = SystemParameters.WorkArea.Right - Width - 20;
            Top = SystemParameters.WorkArea.Top + 20;
            Background = Ui.Res("Card");
            BorderBrush = Ui.Res("Accent");
            BorderThickness = new Thickness(1);
            FontFamily = (FontFamily)Application.Current.FindResource("UiFont");
            MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            _blockText = Ui.Text("", 11, "Muted", FontWeights.SemiBold);
            _scenarioText = Ui.Text("", 17, "Silver", FontWeights.Bold);
            _focusText = Ui.Text("", 12, "Muted");
            _clockText = Ui.Text("", 40, "AccentHi", FontWeights.Bold);
            _clockText.Margin = new Thickness(0, 6, 0, 2);
            _nextText = Ui.Text("", 11, "Muted");

            _pauseBtn = new Button { Content = "Pausar", Margin = new Thickness(0, 0, 6, 0) };
            _pauseBtn.Click += (_, _) => { _paused = !_paused; _pauseBtn.Content = _paused ? "Continuar" : "Pausar"; };
            _nextBtn = new Button { Content = "Próximo bloco", Margin = new Thickness(0, 0, 6, 0) };
            _nextBtn.Click += (_, _) => EndBlock();
            var stopBtn = new Button { Content = "Encerrar", Style = (Style)Application.Current.FindResource("Ghost") };
            stopBtn.Click += (_, _) => Close();

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            buttons.Children.Add(_pauseBtn);
            buttons.Children.Add(_nextBtn);
            buttons.Children.Add(stopBtn);

            _runPanel = new StackPanel();
            _runPanel.Children.Add(_clockText);
            _runPanel.Children.Add(_nextText);
            _runPanel.Children.Add(buttons);

            _scoreBox = new TextBox { Margin = new Thickness(0, 6, 0, 8) };
            var saveBtn = new Button { Content = "Salvar e seguir", Style = (Style)Application.Current.FindResource("Primary") };
            saveBtn.Click += (_, _) => SaveScoreAndContinue();
            _scoreBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) SaveScoreAndContinue(); };
            _scorePanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
            _scorePanel.Children.Add(Ui.Text("Score nesse cenário (opcional, o melhor da série):", 12, "Silver"));
            _scorePanel.Children.Add(_scoreBox);
            _scorePanel.Children.Add(saveBtn);

            var root = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
            root.Children.Add(_blockText);
            root.Children.Add(_scenarioText);
            root.Children.Add(_focusText);
            root.Children.Add(_runPanel);
            root.Children.Add(_scorePanel);
            Content = root;

            _timer.Tick += OnTick;
            Loaded += (_, _) => { StartBlock(0); _timer.Start(); };
            Closed += (_, _) => _timer.Stop();
        }

        void StartBlock(int i)
        {
            _index = i;
            var b = _routine.Blocks[i];
            _left = TimeSpan.FromMinutes(b.Minutes);
            _lastTick = DateTime.Now;
            _askingScore = false;
            _scorePanel.Visibility = Visibility.Collapsed;
            _runPanel.Visibility = Visibility.Visible;
            _blockText.Text = $"BLOCO {i + 1} DE {_routine.Blocks.Count} · {b.Category.ToUpperInvariant()} · {_routine.Platform.ToUpperInvariant()}";
            _scenarioText.Text = b.Scenario;
            _focusText.Text = b.Focus;
            _nextText.Text = i + 1 < _routine.Blocks.Count ? "Depois: " + _routine.Blocks[i + 1].Scenario : "Último bloco!";
            try { Clipboard.SetText(b.Scenario); } catch { }
            UpdateClock();
        }

        void OnTick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            if (!_paused && !_askingScore) _left -= now - _lastTick;
            _lastTick = now;
            if (!_askingScore && _left <= TimeSpan.Zero) EndBlock();
            UpdateClock();
        }

        void UpdateClock()
        {
            var t = _left < TimeSpan.Zero ? TimeSpan.Zero : _left;
            _clockText.Text = $"{(int)t.TotalMinutes}:{t.Seconds:00}";
        }

        void EndBlock()
        {
            if (_askingScore) return;
            try { SystemSounds.Asterisk.Play(); } catch { }
            _askingScore = true;
            _runPanel.Visibility = Visibility.Collapsed;
            _scorePanel.Visibility = Visibility.Visible;
            _scoreBox.Text = "";
            Activate();
            _scoreBox.Focus();
        }

        void SaveScoreAndContinue()
        {
            if (Ui.TryNum(_scoreBox.Text, out double score) && score > 0)
                Session!.Scores.Add(new TrainingScore { Scenario = _routine.Blocks[_index].Scenario, Score = score });

            if (_index + 1 < _routine.Blocks.Count) StartBlock(_index + 1);
            else
            {
                Finished = true;
                Close();
            }
        }
    }
}
