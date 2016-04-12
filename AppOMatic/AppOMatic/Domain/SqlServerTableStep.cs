﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Threading.Tasks;
using AppOMatic.Extensions;
using System.Linq;

namespace AppOMatic.Domain
{
	public class SqlServerTableStep : Step
	{
		private static readonly Dictionary<string, List<string>> _databaseSchema = new Dictionary<string, List<string>>();

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

			switch(dobj.Method)
			{
				case RequestMethod.CreateItem:
					await CreateItemAsync(dobj, command).ConfigureAwait(false);
					break;
				case RequestMethod.CreateList:
					await CreateItemsAsync(dobj, command).ConfigureAwait(false);
					break;
				case RequestMethod.DeleteItem:
					throw new NotImplementedException();
					break;
				case RequestMethod.DeleteList:
					throw new NotImplementedException();
					break;
				case RequestMethod.ReplaceItem:
					throw new NotImplementedException();
					break;
				case RequestMethod.ReplaceList:
					throw new NotImplementedException();
					break;
				case RequestMethod.RetrieveItem:
					await FetchByIdAsync(dobj, command).ConfigureAwait(false);
					break;
				case RequestMethod.RetrieveList:
					if(dobj.ContainsKey("pageSize"))
					{
						await FetchPageAsync(dobj, command).ConfigureAwait(false);
					}
					else
					{
						await FetchAllAsync(dobj, command).ConfigureAwait(false);
					}
					break;
				case RequestMethod.UpdateItem:
					throw new NotImplementedException();
					break;
				case RequestMethod.UpdateList:
					throw new NotImplementedException();
					break;
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

		#region Retrieve

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

		#endregion

		#region Create

		private async Task<List<string>> GetTableColumnsAsync(SqlCommand command)
		{
			var columnsKey = $"{ConnectionString}:{Name}";
			List<string> columns;

			if(_databaseSchema.TryGetValue(columnsKey, out columns) == false)
			{
				command.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName ORDER BY ORDINAL_POSITION";
				command.Parameters.Clear();
				command.Parameters.AddWithValue("tableName", Name);
				columns = new List<string>();

				using(var dr = await command.ExecuteReaderAsync().ConfigureAwait(false))
				{
					while(await dr.ReadAsync().ConfigureAwait(false))
					{
						columns.Add(dr.GetString(0));
					}
				}

				_databaseSchema[columnsKey] = columns;
			}

			return columns;
		}

		protected virtual void PrepareCreateItemQuery(DataObject dobj, SqlCommand command, List<string> columns)
		{
			var cols = new List<string>();

			foreach(var col in columns)
			{
				if(dobj.Keys.Any(c => string.Equals(c, col, StringComparison.CurrentCultureIgnoreCase)))
				{
					cols.Add(col);
				}
			}

			command.CommandText = string.Format("INSERT INTO [{0}] ({1}) OUTPUT INSERTED.Id VALUES ({2})", Name, string.Join(", ", cols), string.Join(", ", cols.Select(s => "@" + s)));
			PrepareParameters(dobj, command);
		}

		private async Task CreateItemAsync(DataObject dobj, SqlCommand command)
		{
			var columns = await GetTableColumnsAsync(command).ConfigureAwait(false);

			PrepareCreateItemQuery(dobj, command, columns);

			var insertedId = await command.ExecuteScalarAsync().ConfigureAwait(false);

			dobj["id"] = insertedId;
		}

		private async Task CreateItemsAsync(DataObject dobj, SqlCommand command)
		{
			var columns = await GetTableColumnsAsync(command).ConfigureAwait(false);
			var items = dobj.GetArray("items");

			if(items == null)
			{
				return;
			}

			foreach(var item in items)
			{
				var d = new DataObject();

				foreach(var kv in item)
				{
					d[kv.Key] = kv.Value;
				}

				PrepareCreateItemQuery(d, command, columns);

				item["id"] = await command.ExecuteScalarAsync().ConfigureAwait(false);
			}

			dobj["items"] = items;
		}

		#endregion

	}
}
