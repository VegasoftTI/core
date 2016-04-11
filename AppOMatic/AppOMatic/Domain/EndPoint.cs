using System.Threading.Tasks;
using Microsoft.AspNet.Http;

namespace AppOMatic.Domain
{
    public class EndPoint : BaseDomainObject
    {
	    private string _route;

	    public string Route
	    {
		    get
		    {
			    return _route ?? Name?.ToLower();
		    }
		    set
		    {
				_route = value?.ToLower();
			}
	    }

	    internal Task HandleAsync(HttpContext context)
	    {
		    return Task.FromResult(0);
	    }
    }
}
