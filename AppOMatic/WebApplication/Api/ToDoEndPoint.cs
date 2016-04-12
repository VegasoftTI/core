using AppOMatic.Domain;

namespace WebApplication.Api
{
	public sealed class ToDoEndPoint : EndPoint
	{
		public ToDoEndPoint(string connectionString)
		{
			Name = "ToDo";

			Steps = new Step[]
			{
				new SqlServerTableStep { Name = "ToDo", ConnectionString = connectionString },
			};
		}
	}
}
