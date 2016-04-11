using System;
using System.Text.RegularExpressions;

// ReSharper disable UnusedParameter.Global

namespace AppOMatic.Domain
{
	public abstract class BaseDomainObject
	{
		public string Name { get; set; }

		protected virtual void Validate()
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
	}
}
