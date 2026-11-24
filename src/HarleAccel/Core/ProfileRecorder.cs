// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace HarleAccel.Core
{
    /// <summary>
    /// Grava o movimento do mouse durante o Teste de Perfil, somando em janelas de 10 ms.
    /// Só conta enquanto o jogo está em foco e com o cursor escondido.
    /// </summary>
    public sealed class ProfileRecorder : IDisposable
    {
        readonly RawMouseListener _listener;
        readonly DispatcherTimer _timer;
        readonly int _dpi;
        readonly long _t0;

        readonly List<MoveBin> _bins = new();
        long _curIndex = -1;
        int _curDx, _curDy;

        // estimativa ao vivo (sem desfazer curva) só para a barra de progresso
        double _segPeak;
        long _lastBinIndex = -10;
        int _liveFlicks;

        public bool InGame { get; private set; }
        public double ActiveSeconds { get; private set; }
        public double MovingSeconds => _bins.Count * ProfileAnalyzer.BinSeconds;
        public int LiveFlicks => _liveFlicks;
        public IReadOnlyList<MoveBin> Bins => _bins;

        public event Action? Tick;

        public ProfileRecorder(int dpi)
        {
            _dpi = Math.Max(1, dpi);
            _t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            _listener = new RawMouseListener();
            _listener.Moved += OnMove;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += (_, _) =>
            {
                InGame = RawMouseListener.IsLikelyInGame();
                if (InGame) ActiveSeconds += 0.25;
                Tick?.Invoke();
            };
            _timer.Start();
        }

        void OnMove(int dx, int dy, long ts)
        {
            if (!InGame) return;
            double t = (ts - _t0) / RawMouseListener.TicksPerSecond;
            long index = (long)Math.Floor(t / ProfileAnalyzer.BinSeconds);
            if (index != _curIndex) Flush(index);
            _curDx += dx;
            _curDy += dy;
        }

        void Flush(long newIndex)
        {
            if (_curIndex >= 0 && (_curDx != 0 || _curDy != 0))
            {
                _bins.Add(new MoveBin(_curIndex * ProfileAnalyzer.BinSeconds, _curDx, _curDy));

                double cmPerSec = Math.Sqrt((double)_curDx * _curDx + (double)_curDy * _curDy) / _dpi
                                  * SensMath.CmPerInch / ProfileAnalyzer.BinSeconds;
                bool contiguous = _curIndex - _lastBinIndex <= 3;
                if (!contiguous || cmPerSec < 2)
                {
                    if (_segPeak >= 40) _liveFlicks++;
                    _segPeak = 0;
                }
                if (cmPerSec >= 2) _segPeak = Math.Max(_segPeak, cmPerSec);
                _lastBinIndex = _curIndex;
            }
            _curIndex = newIndex;
            _curDx = _curDy = 0;
        }

        public void Stop()
        {
            _timer.Stop();
            Flush(-1);
            _listener.Moved -= OnMove;
        }

        public void Dispose()
        {
            _timer.Stop();
            _listener.Dispose();
        }
    }
}
