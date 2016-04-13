using AppOMatic.Domain;

namespace WebApplication.Api
{
	public sealed class SampleApiApplication : Application
	{
		public SampleApiApplication(string connectionString)
		{
			Name = "SampleApi";
			Version = "1.0.0";
			LifecycleStage = ApplicationLifecycleStage.Development;

			EndPoints = new[]
			{
				new EndPoint
				{
					Name = "ToDo",
					Steps = new Step[]
					{
						new SqlServerTableStep {Name = "ToDo", ConnectionString = connectionString},
					}
				},

				new EndPoint
				{
					Name = "TodoWithCategories",
					Steps = new Step[]
					{
						new SqlServerTableStep {Name = "TodoWithCategories", ConnectionString = connectionString},
					}
				}
			};
		}
	}
}
