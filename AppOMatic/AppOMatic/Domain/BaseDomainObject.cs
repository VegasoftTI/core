using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Newtonsoft.Json;

// ReSharper disable UnusedParameter.Global

namespace AppOMatic.Domain
{
	public abstract class BaseDomainObject
	{
		public string Name { get; set; }

		internal static Task WriteErrorResponseAsync(HttpContext context, HttpStatusCode statusCode)
		{
			context.Response.StatusCode = (int)statusCode;
			context.Response.ContentType = "application/json";

			return context.Response.WriteAsync(JsonConvert.SerializeObject(new { statusCode = statusCode, message = statusCode.ToString() }));
		}

		internal virtual void Validate()
		{
			ValidateNullOrEmpty(Name, nameof(Name));
		}

		protected void ValidateNullOrEmpty(string value, string propertyName)
		{
			if(string.IsNullOrEmpty(value))
			{
				throw new ArgumentNullException($"{GetType().Name}.{propertyName} should not be null or empty");
			}
		}

		protected void ValidateRegEx(string value, string pattern, string propertyName, bool ignoreCase = true)
		{
			var regEx = new Regex(pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);

			if(regEx.Match(value).Success == false)
			{
				throw new ArgumentOutOfRangeException($"{GetType().Name}.{propertyName} should match /{pattern}/");
			}
		}

		protected void ValidateIsNot<T>(T value, T forbiddenValue, string propertyName)
		{
			if(value.Equals(forbiddenValue))
			{
				throw new ArgumentOutOfRangeException($"{GetType().Name}.{propertyName} should have any value except [{forbiddenValue}]");
			}
		}

		protected void ValidateIsNotEmpty<T>(IList<T> list, string propertyName)
		{
			if(list == null)
			{
				throw new ArgumentNullException($"{GetType().Name}.{propertyName} should not be null");
			}

			if(list.Count == 0)
			{
				throw new ArgumentException($"{GetType().Name}.{propertyName} should not be empty");
			}
		}

		protected static void ValidateChildren(IEnumerable<BaseDomainObject> children)
		{
			foreach(var child in children)
			{
				child.Validate();
			}
		}
	}
}
