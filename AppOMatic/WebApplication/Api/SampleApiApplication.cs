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

		    EndPoints = new EndPoint[]
		    {
			    new ToDoEndPoint(connectionString),
		    };
	    }
    }
}
