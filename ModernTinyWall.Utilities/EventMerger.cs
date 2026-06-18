using System;
using System.Threading;

namespace ModernTinyWall.Utilities
{
    public sealed class EventMerger : Disposable
    {
        private readonly int _maxEventLatencyMs;
        private readonly Timer _delayTimer;
        private readonly Lock _locker = new();

        public event EventHandler? Event;
        private bool _timerActive;

        public EventMerger(int maxLatencyMs)
        {
            _maxEventLatencyMs = maxLatencyMs;
            _delayTimer = new Timer(DelayExpired);
        }

        public void Pulse()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_locker)
            {
                if (_timerActive) return;

                _timerActive = true;
                _delayTimer.Change(_maxEventLatencyMs, Timeout.Infinite);
            }
        }

        private void DelayExpired(object? args)
        {
            lock (_locker)
            {
                _timerActive = false;
            }

            ThreadPool.QueueUserWorkItem(o => Event?.Invoke(this, EventArgs.Empty));
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
            {
                _delayTimer.Change(Timeout.Infinite, Timeout.Infinite);

                using var wh = new ManualResetEvent(false);
                _delayTimer.Dispose(wh);
                wh.WaitOne();
            }

            base.Dispose(disposing);
        }
    }
}
