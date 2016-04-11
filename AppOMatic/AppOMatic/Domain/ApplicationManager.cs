using System.Collections.Generic;
using Microsoft.AspNet.Builder;

namespace AppOMatic.Domain
{
    public static class ApplicationManager
    {
	    private static readonly Dictionary<string, Application> _applicationRegistry = new Dictionary<string, Application>();

		public static IApplicationBuilder UseApiOMaticApplication(this IApplicationBuilder builder, Application applicationInstance)
		{
			_applicationRegistry.Add(applicationInstance.Name, applicationInstance);

			return builder;
		}
	}
}
