using System.Collections.Generic;
using System.IO;
using System.Net;
using AppOMatic.Extensions;
using Microsoft.AspNet.Http;
using Newtonsoft.Json;

namespace AppOMatic.Domain
{
	public sealed class DataObject : Dictionary<string, object>
	{
		public Dictionary<string, string> Headers { get; set; }

		public RequestMethod Method { get; set; }

		public HttpStatusCode ResultStatusCode { get; set; } = HttpStatusCode.OK;

		internal void ParseQueryInput(HttpContext context)
		{
			foreach(var item in context.Request.Query)
			{
				switch(item.Value.Count)
				{
					case 0:
						continue;
					case 1:
						this[item.Key.ToCamelCase()] = ParseObject(item.Value[0]);
						break;
					default:
						this[item.Key.ToCamelCase()] = ParseArray(item.Value);
						break;
				}
			}

			ParseMetadata(context);
		}

		private static object ParseObject(string value)
		{
			return JsonConvert.DeserializeObject(value);
		}

		private static object[] ParseArray(string[] values)
		{
			var items = new List<object>();

			foreach(var value in values)
			{
				items.Add(ParseObject(value));
			}

			return items.ToArray();
		}

		internal void ParseBodyInput(HttpContext context)
		{
			if(context.Request.HasFormContentType)
			{
				foreach(var item in context.Request.Form)
				{
					switch(item.Value.Count)
					{
						case 0:
							continue;
						case 1:
							this[item.Key.ToCamelCase()] = ParseObject(item.Value[0]);
							break;
						default:
							this[item.Key.ToCamelCase()] = ParseArray(item.Value);
							break;
					}
				}
			}
			else
			{
				using(var sr = new StreamReader(context.Request.Body))
				{
					var body = sr.ReadToEnd();
					var items = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

					foreach(var item in items)
					{
						this[item.Key.ToCamelCase()] = item.Value;
					}
				}
			}

			ParseMetadata(context);
		}

		private void ParseMetadata(HttpContext context)
		{
			Headers = new Dictionary<string, string>();

			switch(context.Request.Method)
			{
				case "GET":
					Method = ContainsKey("id") ? RequestMethod.RetrieveItem : RequestMethod.RetrieveList;
					break;
				case "POST":
					Method = ContainsKey("items") ? RequestMethod.CreateList : RequestMethod.CreateItem;
					break;
				case "PUT":
					Method = ContainsKey("id") ? RequestMethod.ReplaceItem : RequestMethod.ReplaceList;
					break;
				case "PATCH":
					Method = ContainsKey("id") ? RequestMethod.UpdateItem : RequestMethod.UpdateList;
					break;
				case "DELETE":
					Method = ContainsKey("id") ? RequestMethod.DeleteItem : RequestMethod.DeleteList;
					break;
			}

			foreach(var header in context.Request.Headers)
			{
				Headers[header.Key] = string.Join(";", header.Value);
			}
		}
	}
}
