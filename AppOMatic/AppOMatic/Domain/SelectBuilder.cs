using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AppOMatic.Domain
{
	public sealed class SelectBuilder
	{
		private string _selectClause;
		private string _forcedSelectClause;
		private string _fromClause;
		private int _skipRows;
		private int _fetchRows;
		private readonly List<string> _whereClauses = new List<string>();
		private readonly List<string> _orderByClauses = new List<string>();
		private string _parsedWhere;

		public SelectBuilder(DataObject dobj)
		{
			ParseOrderBy(dobj);
			ParseSelect(dobj);
			ParseWhere(dobj);
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

		private void ParseSelect(DataObject dobj)
		{
			var select = dobj.Get<string>("select");

			if(select == null)
			{
				return;
			}

			var fields = select.Split(',');
			var fieldList = new List<string>();

			foreach(var field in fields)
			{
				var fieldName = field.Trim();

				if(ValidateFieldName(fieldName) == false)
				{
					throw new ArgumentException($"Invalid Select clause: {fieldName}");
				}

				fieldList.Add($"[{fieldName}]");
			}

			_forcedSelectClause = string.Join(", ", fieldList);
		}

		private static readonly CultureInfo _enUs = new CultureInfo("en-US");

		private void ParseWhere(DataObject dobj)
		{
			var filter = dobj.Get<string>("filter");
			var clause = new List<string>();

			if(filter == null)
			{
				return;
			}

			filter = filter.Trim();

			while(filter.Length > 0)
			{
				var fieldName = GetNextToken(ref filter);

				if(ValidateFieldName(fieldName) == false)
				{
					throw new ArgumentException($"Invalid field name: {fieldName}");
				}

				clause.Add($"[{fieldName}]");

				var op = GetNextToken(ref filter).ToLower();

				switch(op)
				{
					case "eq":
						clause.Add("=");
						break;
					case "lt":
						clause.Add("<");
						break;
					case "gt":
						clause.Add(">");
						break;
					case "le":
						clause.Add("<=");
						break;
					case "ge":
						clause.Add(">=");
						break;
					case "ne":
						clause.Add("<>");
						break;
					default:
						throw new ArgumentException($"Invalid operator {op}");
				}

				var value = GetNextToken(ref filter);

				if(value == "")
				{
					throw new ArgumentException($"Invalid value for {fieldName} {op}");
				}

				if(value.StartsWith("'") && value.EndsWith("'"))
				{
					clause.Add(value);
				}
				else
				{
					decimal dValue;

					if(decimal.TryParse(value, NumberStyles.Float, _enUs, out dValue) == false)
					{
						throw new ArgumentOutOfRangeException($"Value {value} is not a numeric or string value");
					}

					clause.Add(dValue.ToString(_enUs));
				}

				var continuation = GetNextToken(ref filter);

				switch(continuation.ToLower())
				{
					case "":
						break;
					case "and":
						clause.Add("AND");
						break;
					case "or":
						clause.Add("OR");
						break;
					default:
						throw new ArgumentException($"Unexpected token {continuation}");
				}
			}

			_parsedWhere = string.Join(" ", clause);
		}

		private static string GetNextToken(ref string line)
		{
			line = line.TrimStart();

			if(line.Length == 0)
			{
				return "";
			}

			var token = new StringBuilder();
			var insideQuote = false;

			while(line.Length > 0)
			{
				var c = line.Substring(0, 1);

				switch(c)
				{
					case "'":
						if(insideQuote == false)
						{
							insideQuote = true;
						}
						else
						{
							token.Append(c);
							line = line.Substring(1);

							return token.ToString();
						}
						break;
					case "\\":
						line = line.Substring(1);
						continue;
					case " ":
						if(insideQuote == false)
						{
							return token.ToString();
						}
						break;
				}

				token.Append(c);
				line = line.Substring(1);
			}

			return token.ToString();
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
			sb.Append(_forcedSelectClause ?? _selectClause);
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

				if(_parsedWhere != null)
				{
					sb.Append(" AND ");
					sb.Append(_parsedWhere);
				}
			}
			else
			{
				if(_parsedWhere != null)
				{
					sb.Append(" WHERE ");
					sb.Append(_parsedWhere);
				}
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
