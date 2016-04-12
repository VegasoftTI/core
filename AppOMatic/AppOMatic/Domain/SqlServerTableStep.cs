using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Threading.Tasks;
using AppOMatic.Extensions;

namespace AppOMatic.Domain
{
	public class SqlServerTableStep : Step
	{
		public string ConnectionString { get; set; }

		internal override void Validate()
		{
			base.Validate();

			ValidateNullOrEmpty(ConnectionString, nameof(ConnectionString));
		}

		internal override async Task InitializeContextAsync(Dictionary<string, object> endPointContext)
		{
			var key = $"SqlServerTableStep:{ConnectionString}";
			var ckey = key + ":Count";

			if(endPointContext.ContainsKey(key))
			{
				var counter = (int)endPointContext[ckey];

				counter++;
				endPointContext[ckey] = counter;
			}
			else
			{
				var connection = new SqlConnection(ConnectionString);

				await connection.OpenAsync().ConfigureAwait(false);

				var transaction = connection.BeginTransaction();
				var command = new SqlCommand { Connection = connection, Transaction = transaction };

				endPointContext.Add(key, command);
				endPointContext.Add(ckey, 1);
			}
		}

		internal override Task TerminateContextAsync(Dictionary<string, object> endPointContext, Exception runException)
		{
			var key = $"SqlServerTableStep:{ConnectionString}";
			var ckey = key + ":Count";

			var counter = (int)endPointContext[ckey];

			counter--;

			if(counter > 0)
			{
				endPointContext[ckey] = counter;
				return Task.FromResult(0);
			}

			var command = (SqlCommand)endPointContext[key];
			var transaction = command.Transaction;
			var connection = command.Connection;

			if(runException != null)
			{
				command.Transaction.Rollback();
			}
			else
			{
				command.Transaction.Commit();
			}

			command.Dispose();
			transaction.Dispose();
			connection.Dispose();

			endPointContext.Remove(key);
			endPointContext.Remove(ckey);
			return Task.FromResult(0);
		}

		internal override async Task HandleAsync(DataObject dobj, IDictionary<string, object> endPointContext)
		{
			var command = (SqlCommand)endPointContext[$"SqlServerTableStep:{ConnectionString}"];

			if(dobj.ContainsKey("id"))
			{
				await FetchByIdAsync(dobj, command).ConfigureAwait(false);
			}
			else if(dobj.ContainsKey("pageSize"))
			{
				await FetchPageAsync(dobj, command).ConfigureAwait(false);
			}
			else
			{
				await FetchAllAsync(dobj, command).ConfigureAwait(false);
			}
		}

		protected virtual void PrepareParameters(DataObject dobj, SqlCommand command)
		{
			command.Parameters.Clear();

			foreach(var item in dobj)
			{
				command.Parameters.AddWithValue(item.Key, item.Value);
			}
		}

		protected virtual void PrepareFetchByIdQuery(DataObject dobj, SqlCommand command)
		{
			command.CommandText = $"SELECT TOP 1 * FROM [{Name}] WHERE [Id] = @id";
			PrepareParameters(dobj, command);
		}

		private async Task FetchByIdAsync(DataObject dobj, SqlCommand command)
		{
			PrepareFetchByIdQuery(dobj, command);

			using(var dr = await command.ExecuteReaderAsync().ConfigureAwait(false))
			{
				if(await dr.ReadAsync().ConfigureAwait(false))
				{
					var row = new Dictionary<string, object>();

					for(var ct = 0; ct < dr.FieldCount; ct++)
					{
						row.Add(dr.GetName(ct).ToCamelCase(), dr.IsDBNull(ct) ? null : dr.GetValue(ct));
					}

					dobj["item"] = row;
				}
				else
				{
					dobj["item"] = null;
					dobj.ResultStatusCode = HttpStatusCode.NotFound;
				}
			}
		}

		protected virtual void PrepareFetchPageQuery(DataObject dobj, SqlCommand command, int pageSize, int pageNumber)
		{
			var skip = (pageNumber - 1) * pageSize;

			command.CommandText = $"SELECT COUNT(*) FROM [{Name}]; SELECT * FROM [{Name}] ORDER BY [Id] OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY";
			PrepareParameters(dobj, command);
		}

		private async Task FetchPageAsync(DataObject dobj, SqlCommand command)
		{
			var pageSize = dobj.Get<int>("pageSize");
			var pageNumber = dobj.Get<int>("pageNumber");

			if(pageSize < 2)
			{
				throw new ArgumentOutOfRangeException($"pageSize should be >= 2, but it is {pageSize}");
			}

			if(pageNumber < 1)
			{
				throw new ArgumentOutOfRangeException($"pageNumber should be >= 1, but it is {pageNumber}");
			}

			PrepareFetchPageQuery(dobj, command, pageSize, pageNumber);

			using(var dr = await command.ExecuteReaderAsync().ConfigureAwait(false))
			{
				await dr.ReadAsync().ConfigureAwait(false);

				var totalRows = dr.GetInt32(0);

				dobj["totalRows"] = totalRows;
				dobj["totalPages"] = Convert.ToInt32(Math.Ceiling(Convert.ToSingle(totalRows) / pageSize));
				await dr.NextResultAsync().ConfigureAwait(false);

				var result = new List<Dictionary<string, object>>();

				while(await dr.ReadAsync().ConfigureAwait(false))
				{
					var row = new Dictionary<string, object>();

					for(var ct = 0; ct < dr.FieldCount; ct++)
					{
						row.Add(dr.GetName(ct).ToCamelCase(), dr.IsDBNull(ct) ? null : dr.GetValue(ct));
					}

					result.Add(row);
				}

				dobj["items"] = result;
				dobj.ResultStatusCode = result.Count > 0 ? HttpStatusCode.OK : HttpStatusCode.NoContent;
			}
		}

		protected virtual void PrepareFetchAllQuery(DataObject dobj, SqlCommand command)
		{
			command.CommandText = $"SELECT * FROM [{Name}]";
			PrepareParameters(dobj, command);
		}

		private async Task FetchAllAsync(DataObject dobj, SqlCommand command)
		{
			PrepareFetchAllQuery(dobj, command);

			var result = new List<Dictionary<string, object>>();

			using(var dr = await command.ExecuteReaderAsync().ConfigureAwait(false))
			{
				while(await dr.ReadAsync().ConfigureAwait(false))
				{
					var row = new Dictionary<string, object>();

					for(var ct = 0; ct < dr.FieldCount; ct++)
					{
						row.Add(dr.GetName(ct).ToCamelCase(), dr.IsDBNull(ct) ? null : dr.GetValue(ct));
					}

					result.Add(row);
				}
			}

			dobj["items"] = result;
			dobj.ResultStatusCode = result.Count > 0 ? HttpStatusCode.OK : HttpStatusCode.NoContent;
		}
	}
}
