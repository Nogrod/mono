using System;
using System.Reflection;

public sealed class Isolated<T> : IDisposable where T : MarshalByRefObject
{
	private AppDomain _domain;
	private T _value;

	public Isolated(params object[] args)
	{
		_domain = AppDomain.CreateDomain("Isolated:" + Guid.NewGuid(), null, AppDomain.CurrentDomain.SetupInformation);
		var type = typeof(T);
		_value = (T)_domain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName, false, BindingFlags.Default, null, args, null, null);
	}

	public T Value
	{
		get
		{
			return _value;
		}
	}

	public void Dispose()
	{
	    if (_domain == null) return;
	    AppDomain.Unload(_domain);
	    _domain = null;
	    _value = null;
	}
}

