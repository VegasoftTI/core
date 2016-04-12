using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AppOMatic.Domain
{
    public abstract class Step : BaseDomainObject
    {
	    internal abstract Task HandleAsync(DataObject dobj, IDictionary<string, object> endPointContext);

		internal virtual Task InitializeContextAsync(Dictionary<string, object> endPointContext)
	    {
		    return Task.FromResult(0);
	    }

		internal virtual Task TerminateContextAsync(Dictionary<string, object> endPointContext, Exception runException)
	    {
			return Task.FromResult(0);
		}
	}
}
