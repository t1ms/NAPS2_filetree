using System.Linq.Expressions;

namespace NAPS2.Config.Model;

public class DefaultsConfigScope<TConfig> : ConfigScope<TConfig>
{
    private readonly ConfigStorage<TConfig> _storage;

    public DefaultsConfigScope(TConfig defaults) : base(ConfigScopeMode.ReadOnly)
    {
        _storage = new ConfigStorage<TConfig>();
        _storage.Set(c => c, defaults);
    }

    protected override bool TryGetInternal(ConfigLookup lookup, out object? value)
    {
        return _storage.TryGet(lookup, out value);
    }

    protected override bool SetInternal<T>(Expression<Func<TConfig, T>> accessor, T value) =>
        throw new InvalidOperationException();

    protected override bool RemoveInternal<T>(Expression<Func<TConfig, T>> accessor) =>
        throw new InvalidOperationException();

    protected override bool CopyFromInternal(ConfigStorage<TConfig> source) =>
        throw new InvalidOperationException();
}