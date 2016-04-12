using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Newtonsoft.Json;

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

		public Step[] Steps { get; set; }

		internal override void Validate()
		{
			base.Validate();

			ValidateIsNotEmpty(Steps, nameof(Steps));
			ValidateChildren(Steps);
		}

		internal async Task HandleAsync(HttpContext context)
		{
			DataObject dobj;

			switch(context.Request.Method)
			{
				case "GET":
					dobj = new DataObject();
					dobj.ParseQueryInput(context);
					break;
				case "POST":
					dobj = new DataObject();
					dobj.ParseBodyInput(context);
					break;
				case "PUT":
					dobj = new DataObject();
					dobj.ParseBodyInput(context);
					break;
				case "PATCH":
					dobj = new DataObject();
					dobj.ParseBodyInput(context);
					break;
				case "DELETE":
					dobj = new DataObject();
					dobj.ParseQueryInput(context);
					break;
				default:
					await WriteErrorResponseAsync(context, HttpStatusCode.MethodNotAllowed).ConfigureAwait(false);
					return;
			}

			var endPointContext = new Dictionary<string, object>();

			foreach(var step in Steps)
			{
				await step.InitializeContextAsync(endPointContext).ConfigureAwait(false);
			}

			Exception runException = null;

			foreach(var step in Steps)
			{
				try
				{
					await step.HandleAsync(dobj, endPointContext).ConfigureAwait(false);
				}
				catch(Exception ex)
				{
					runException = ex;
					break;
				}
			}

			foreach(var step in Steps)
			{
				await step.TerminateContextAsync(endPointContext, runException).ConfigureAwait(false);
			}

			if(runException != null)
			{
				throw runException;
			}

			context.Response.StatusCode = (int)dobj.ResultStatusCode;
			context.Response.ContentType = "application/json";

			var result = new Dictionary<string, object>(dobj.Count);

			foreach(var kv in dobj)
			{
				result[kv.Key] = kv.Value;
			}

			await context.Response.WriteAsync(JsonConvert.SerializeObject(result)).ConfigureAwait(false);
		}
	}
}
