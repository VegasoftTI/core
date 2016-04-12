using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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
			var command = (SqlCommand) endPointContext[$"SqlServerTableStep:{ConnectionString}"];

			if(dobj.ContainsKey("id"))
			{
				await FetchByIdAsync(dobj, command).ConfigureAwait(false);
			}
			else if(dobj.ContainsKey("pagesize"))
			{
				await FetchPageAsync(dobj, command).ConfigureAwait(false);
			}
			else
			{
				await FetchAllAsync(dobj, command).ConfigureAwait(false);
			}
		}

		private Task FetchByIdAsync(DataObject dobj, SqlCommand command)
		{
			return Task.FromResult(0);
		}

		private Task FetchPageAsync(DataObject dobj, SqlCommand command)
		{
			return Task.FromResult(0);
		}

		protected virtual void PrepareFetchAllQuery(DataObject dobj, SqlCommand command)
		{
			command.CommandText = $"SELECT * FROM [{Name}]";
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

			dobj["Result"] = result;
		}
	}
}
