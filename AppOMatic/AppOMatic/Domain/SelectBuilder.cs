using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AppOMatic.Domain
{
	public sealed class SelectBuilder
	{
		private string _selectClause;
		private string _fromClause;
		private int _skipRows;
		private int _fetchRows;
		private readonly List<string> _whereClauses = new List<string>();
		private readonly List<string> _orderByClauses = new List<string>();

		public SelectBuilder(DataObject dobj)
		{
			ParseOrderBy(dobj);
		}

		private void ParseOrderBy(DataObject dobj)
		{
			var orderBy = dobj.Get<string>("orderBy");

			if(orderBy == null)
			{
				return;
			}

			var clauses = orderBy.Split(',');

			foreach(var clause in clauses)
			{
				var parts = clause.Trim().Replace("  ", " ").Split(' ');

				if(parts.Length == 1)
				{
					parts = new[] { parts[0], "asc" };
				}
				else if(parts.Length != 2)
				{
					throw new ArgumentException($"Invalid Order By clause: {clause}");
				}

				if(parts[1] != "asc" && parts[1] != "desc")
				{
					throw new ArgumentException($"Invalid Order By clause: {clause}");
				}

				if(ValidateFieldName(parts[0]) == false)
				{
					throw new ArgumentException($"Invalid Order By clause: {clause}");
				}

				_orderByClauses.Add($"[{parts[0]}] {parts[1]}");
            }
		}

		private static readonly Regex _fieldNameValidator = new Regex("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static bool ValidateFieldName(string fieldName)
		{
			if(string.IsNullOrEmpty(fieldName))
			{
				return false;
			}

			fieldName = fieldName.Trim();

			return _fieldNameValidator.Match(fieldName).Success;
		}

		public SelectBuilder Select(string selectClause)
		{
			_selectClause = selectClause;
			return this;
		}

		public SelectBuilder From(string fromClause)
		{
			_fromClause = fromClause;
			return this;
		}

		public SelectBuilder Skip(int skipRows)
		{
			_skipRows = skipRows;
			return this;
		}

		public SelectBuilder Fetch(int fetchRows)
		{
			_fetchRows = fetchRows;
			return this;
		}

		public SelectBuilder Where(params string[] whereClauses)
		{
			_whereClauses.AddRange(whereClauses);
			return this;
		}

		public SelectBuilder OrderBy(params string[] orderByClauses)
		{
			_orderByClauses.AddRange(orderByClauses);
			return this;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();

			sb.Append("SELECT ");
			sb.Append(_selectClause);
			sb.Append(" FROM ");
			sb.Append(_fromClause);

			if(_fetchRows > 0 && _orderByClauses.Count == 0)
			{
				_orderByClauses.Add("[id]");
			}

			if(_whereClauses.Count > 0)
			{
				sb.Append(" WHERE ");
				sb.Append(string.Join(" AND ", _whereClauses));
			}

			if(_orderByClauses.Count > 0)
			{
				sb.Append(" ORDER BY ");
				sb.Append(string.Join(", ", _orderByClauses));
			}

			if(_fetchRows > 0)
			{
				sb.AppendFormat(" OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY", _skipRows, _fetchRows);
			}

			return sb.ToString();
		}
	}
}
