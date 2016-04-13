using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using AppOMatic.Extensions;
using Microsoft.AspNet.Http;
using Newtonsoft.Json;
using System.Linq;
using Newtonsoft.Json.Linq;

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
			try
			{
				var ovalue = JsonConvert.DeserializeObject(value);

				if(ovalue is long)
				{
					var lvalue = (long) ovalue;

					if(lvalue >= int.MinValue && lvalue <= int.MaxValue)
					{
						return Convert.ToInt32(lvalue);
					}
				}

				return ovalue;
			}
			catch(JsonReaderException)
			{
				return value;
			}
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

		public T Get<T>(string key, T defaultValue = default(T))
		{
			object value;

			if(TryGetValue(key, out value))
			{
				return (T)value;
			}

			key = key.ToLower();

			var ek = Keys.FirstOrDefault(i => i.ToLower() == key);

			if(ek != null)
			{
				return (T)this[ek];
			}

			return defaultValue;
		}

		public List<Dictionary<string, object>> GetArray(string key)
		{
			object value;

			if(TryGetValue(key, out value) == false)
			{
				return null;
			}

			var a = value as JArray;

			if(a == null)
			{
				return null;
			}

			var result = new List<Dictionary<string, object>>();

			foreach(var item in a)
			{
				result.Add(item.ToObject<Dictionary<string, object>>());
			}

			return result;
		}

		public List<object> GetSimpleArray(string key)
		{
			object value;

			if(TryGetValue(key, out value) == false)
			{
				return null;
			}

			var jValue = value as JArray;

			if(jValue == null)
			{
				return null;
			}

			var items = new List<object>();

			foreach(var item in jValue)
			{
				items.Add(item.ToObject<object>());
			}

			return items;
		}
	}
}
