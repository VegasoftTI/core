using System;
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
					{
						await CreateItemAsync(dobj, command).ConfigureAwait(false);
						break;
					}

				case RequestMethod.CreateList:
					{
						await CreateItemsAsync(dobj, command).ConfigureAwait(false);
						break;
					}

				case RequestMethod.DeleteItem:
					{
						await DeleteItemAsync(dobj, command).ConfigureAwait(false);
						break;
					}

				case RequestMethod.DeleteList:
					{
						await DeleteItemsAsync(dobj, command).ConfigureAwait(false);
						break;
					}

				case RequestMethod.ReplaceItem:
					{
						await UpdateItemAsync(dobj, command, true).ConfigureAwait(false);
						break;
					}

				case RequestMethod.ReplaceList:
					{
						await UpdateItemsAsync(dobj, command, true).ConfigureAwait(false);
						break;
					}

				case RequestMethod.RetrieveItem:
					{
						await FetchByIdAsync(dobj, command).ConfigureAwait(false);
						break;
					}

				case RequestMethod.RetrieveList:
					{
						if(dobj.ContainsKey("pageSize"))
						{
							await FetchPageAsync(dobj, command).ConfigureAwait(false);
						}
						else
						{
							await FetchAllAsync(dobj, command).ConfigureAwait(false);
						}

						break;
					}

				case RequestMethod.UpdateItem:
					{
						await UpdateItemAsync(dobj, command, false).ConfigureAwait(false);
						break;
					}

				case RequestMethod.UpdateList:
					{
						await UpdateItemsAsync(dobj, command, false).ConfigureAwait(false);
						break;

					}
			}
		}

		protected virtual void PrepareParameters(DataObject dobj, SqlCommand command)
		{
			command.Parameters.Clear();

			foreach(var item in dobj)
			{
				command.Parameters.AddWithValue(item.Key, item.Value);
			}

			dobj.Clear();
		}

		#region Retrieve

		protected virtual void PrepareFetchByIdQuery(DataObject dobj, SqlCommand command)
		{
			var selectBuilder = new SelectBuilder(dobj);

			selectBuilder.Select("TOP 1 *");
			selectBuilder.From($"[{Name}]");
			selectBuilder.Where("[Id] = @id");

			command.CommandText = selectBuilder.ToString();
			PrepareParameters(dobj, command);
		}

		private async Task FetchByIdAsync(DataObject dobj, SqlCommand command)
		{
			var id = dobj.Get<object>("id");

			if(id == null)
			{
				throw new ArgumentNullException(nameof(id), "Required argument id was not provided");
			}

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

			var selectBuilder = new SelectBuilder(dobj);

			selectBuilder.Select("COUNT(*)");
			selectBuilder.From($"[{Name}]");

			var countRowsQuery = selectBuilder.ToString();

			selectBuilder.Select("*");
			selectBuilder.Skip(skip);
			selectBuilder.Fetch(pageSize);

			command.CommandText = countRowsQuery + "; " + selectBuilder;
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
			var selectBuilder = new SelectBuilder(dobj);

			selectBuilder.Select("*");
			selectBuilder.From($"[{Name}]");

			command.CommandText = selectBuilder.ToString();
			PrepareParameters(dobj, command);
		}

		protected virtual void PrepareCountQuery(DataObject dobj, SqlCommand command)
		{
			var selectBuilder = new SelectBuilder(dobj);

			selectBuilder.Select("COUNT(*)");
			selectBuilder.From($"[{Name}]");

			command.CommandText = selectBuilder.ToString();
			PrepareParameters(dobj, command);
		}

		private async Task FetchAllAsync(DataObject dobj, SqlCommand command)
		{
			var count = dobj.Get<object>("count");

			if(count == null)
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
			else
			{
				PrepareCountQuery(dobj, command);
				dobj["totalRows"] = await command.ExecuteScalarAsync().ConfigureAwait(false);
			}
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
				throw new ArgumentNullException(nameof(items), "Argument items should contain an array of items to create");
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

		#region Delete

		protected virtual void PrepareDeleteItemQuery(DataObject dobj, SqlCommand command)
		{
			command.CommandText = $"DELETE FROM [{Name}] WHERE [Id] = @id";
			PrepareParameters(dobj, command);
		}

		private async Task DeleteItemAsync(DataObject dobj, SqlCommand command)
		{
			var id = dobj.Get<object>("id");

			if(id == null)
			{
				throw new ArgumentNullException(nameof(id), "Required argument id was not provided");
			}

			PrepareDeleteItemQuery(dobj, command);
			dobj["affectedRows"] = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		protected virtual void PrepareDeleteItemsQuery(SqlCommand command, List<object> ids)
		{
			command.Parameters.Clear();

			var paramNames = new List<string>();

			for(var ct = 0; ct < ids.Count; ct++)
			{
				command.Parameters.AddWithValue($"p{ct}", ids[ct]);
				paramNames.Add($"@p{ct}");
			}

			command.CommandText = $"DELETE FROM [{Name}] WHERE [Id] IN ({string.Join(",", paramNames)})";
		}

		private async Task DeleteItemsAsync(DataObject dobj, SqlCommand command)
		{
			var ids = dobj.GetSimpleArray("ids");

			if(ids == null || ids.Count == 0)
			{
				throw new ArgumentNullException(nameof(ids), "Required argument ids was not provided");
			}

			PrepareDeleteItemsQuery(command, ids);
			dobj["affectedRows"] = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		#endregion

		#region Update

		protected virtual void PrepareUpdateItemQuery(DataObject dobj, SqlCommand command, List<string> columns, bool replace)
		{
			var cols = new List<string>();

			foreach(var col in columns)
			{
				if(col.ToLower() == "id")
				{
					continue;
				}

				if(dobj.Keys.Any(c => string.Equals(c, col, StringComparison.CurrentCultureIgnoreCase)))
				{
					cols.Add($"{col} = @{col}");
				}
				else if(replace)
				{
					cols.Add($"{col} = NULL");
				}
			}

			command.CommandText = string.Format("UPDATE [{0}] SET {1} WHERE [Id] = @id", Name, string.Join(", ", cols));
			PrepareParameters(dobj, command);
		}

		private async Task UpdateItemAsync(DataObject dobj, SqlCommand command, bool replace)
		{
			var id = dobj.Get<object>("id");

			if(id == null)
			{
				throw new ArgumentNullException(nameof(id), "Required argument id was not provided");
			}

			if(dobj.Count == 1)
			{
				throw new ArgumentException("Cannot update entity without empty field information");
			}

			var columns = await GetTableColumnsAsync(command).ConfigureAwait(false);

			PrepareUpdateItemQuery(dobj, command, columns, replace);

			dobj["affectedRows"] = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		private async Task UpdateItemsAsync(DataObject dobj, SqlCommand command, bool replace)
		{
			var columns = await GetTableColumnsAsync(command).ConfigureAwait(false);
			var items = dobj.GetArray("items");

			if(items == null)
			{
				throw new ArgumentNullException(nameof(items), $"Argument items should contain an array of items to {(replace ? "replace" : "update")}");
			}

			var affectedRows = 0;

			foreach(var item in items)
			{
				var d = new DataObject();

				foreach(var kv in item)
				{
					d[kv.Key] = kv.Value;
				}

				PrepareUpdateItemQuery(d, command, columns, replace);
				affectedRows += await command.ExecuteNonQueryAsync().ConfigureAwait(false);
			}

			dobj["affectedRows"] = affectedRows;
		}

		#endregion

	}
}
