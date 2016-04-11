using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;

namespace AppOMatic.Domain
{
	public static class ApplicationManagerExtensions
	{
		private static bool _middlewareRegistered;

		public static IApplicationBuilder UseApiOMaticApplication(this IApplicationBuilder builder, Application applicationInstance)
		{
			applicationInstance.Validate();
			ApplicationManager.RegisterApplication(applicationInstance.RootRoute, applicationInstance);

			if(_middlewareRegistered == false)
			{
				_middlewareRegistered = true;
				builder.UseMiddleware<ApplicationManager>();
			}

			return builder;
		}
	}

    public class ApplicationManager
    {
	    private static readonly Dictionary<string, Application> _applicationRegistry = new Dictionary<string, Application>();

	    internal static void RegisterApplication(string route, Application instance)
	    {
		    _applicationRegistry.Add(route, instance);
	    }

		private readonly RequestDelegate _next;

		public ApplicationManager(RequestDelegate next)
		{
			_next = next;
		}

	    // ReSharper disable once ConsiderUsingAsyncSuffix
		public async Task Invoke(HttpContext context)
		{
			if(context.Request.Path.HasValue == false)
			{
				await  _next(context).ConfigureAwait(false);
				return;
			}

			if(context.Request.Path.Value.Length < 2)
			{
				await _next(context).ConfigureAwait(false);
				return;
			}

			var path = context.Request.Path.Value.ToLower().Substring(1);

			foreach(var route in _applicationRegistry.Keys)
			{
				if(path.StartsWith(route))
				{
					var application = _applicationRegistry[route];

					if(await application.HandleAsync(context).ConfigureAwait(false))
					{
						return;
					}
				}
			}

			await _next(context).ConfigureAwait(false);
		}
	}
}
