using System.Linq.Expressions;

namespace NAPS2.Config.Model;

public class MemoryConfigScope<TConfig> : ConfigScope<TConfig>
{
    private readonly ConfigStorage<TConfig> _storage = new();

    public MemoryConfigScope() : base(ConfigScopeMode.ReadWrite)
    {
    }

    protected override bool TryGetInternal(ConfigLookup lookup, out object? value)
    {
        return _storage.TryGet(lookup, out value);
    }

    protected override bool SetInternal<T>(Expression<Func<TConfig, T>> accessor, T value)
    {
        _storage.Set(accessor, value);
        return true;
    }

    protected override bool RemoveInternal<T>(Expression<Func<TConfig, T>> accessor)
    {
        _storage.Remove(accessor);
        return true;
    }

    protected override bool CopyFromInternal(ConfigStorage<TConfig> source)
    {
        _storage.CopyFrom(source);
        return true;
    }
}