using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;

namespace AppOMatic.Domain
{
	public class Application : BaseDomainObject
	{
		public string Version { get; set; }

		public ApplicationLifecycleStage LifecycleStage { get; set; }

		private string _rootRoute;

		public string RootRoute
		{
			get
			{
				return _rootRoute ?? $"{Name}/{Version}/{LifecycleStage}".ToLower();
			}
			set
			{
				_rootRoute = value?.ToLower();
			}
		}

		public List<EndPoint> EndPoints { get; set; }

		internal override void Validate()
		{
			base.Validate();

			ValidateNullOrEmpty(Version, nameof(Version));
			ValidateRegEx(Version, @"(\d+\.\d+\.\d+(\-)?(alpha|beta|RC)(\.)?\d+)?", nameof(Version));
			ValidateIsNot(LifecycleStage, ApplicationLifecycleStage.Undefined, nameof(LifecycleStage));
			ValidateIsNotEmpty(EndPoints, nameof(EndPoints));
			ValidateChildren(EndPoints);
		}

		internal async Task<bool> HandleAsync(HttpContext context)
		{
			var path = context.Request.Path.Value.ToLower().Substring(RootRoute.Length + 2);

			foreach(var endPoint in EndPoints)
			{
				if(endPoint.Route == path)
				{
					await endPoint.HandleAsync(context).ConfigureAwait(false);
					return true;
				}
			}

			return false;
		}
	}
}
