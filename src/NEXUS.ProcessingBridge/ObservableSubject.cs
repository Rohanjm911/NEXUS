namespace NEXUS.ProcessingBridge;

public class ObservableSubject<T> : IObservable<T>, IDisposable
{
    private readonly List<IObserver<T>> _observers = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    public IDisposable Subscribe(IObserver<T> observer)
    {
        lock (_lock)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }
        }

        return new Unsubscriber(_observers, observer, _lock);
    }

    public void OnNext(T value)
    {
        List<IObserver<T>> copy;
        lock (_lock)
        {
            if (_isDisposed) return;
            copy = new List<IObserver<T>>(_observers);
        }

        foreach (var obs in copy)
        {
            try
            {
                obs.OnNext(value);
            }
            catch
            {
                // Never allow observer exception to break publisher
            }
        }
    }

    public void OnCompleted()
    {
        List<IObserver<T>> copy;
        lock (_lock)
        {
            if (_isDisposed) return;
            copy = new List<IObserver<T>>(_observers);
        }

        foreach (var obs in copy)
        {
            try
            {
                obs.OnCompleted();
            }
            catch { }
        }
    }

    public void OnError(Exception error)
    {
        List<IObserver<T>> copy;
        lock (_lock)
        {
            if (_isDisposed) return;
            copy = new List<IObserver<T>>(_observers);
        }

        foreach (var obs in copy)
        {
            try
            {
                obs.OnError(error);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _isDisposed = true;
            _observers.Clear();
        }
    }

    private class Unsubscriber : IDisposable
    {
        private readonly List<IObserver<T>> _observers;
        private readonly IObserver<T> _observer;
        private readonly object _lock;

        public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer, object syncLock)
        {
            _observers = observers;
            _observer = observer;
            _lock = syncLock;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _observers.Remove(_observer);
            }
        }
    }
}

public static class ObservableExtensions
{
    public static IDisposable Subscribe<T>(this IObservable<T> observable, Action<T> onNext)
    {
        return observable.Subscribe(new AnonymousObserver<T>(onNext));
    }

    private class AnonymousObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        public AnonymousObserver(Action<T> onNext) => _onNext = onNext;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => _onNext(value);
    }
}
