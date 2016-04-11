using System.Collections.Generic;
using AppOMatic.Domain;

namespace WebApplication.Api
{
    public sealed class SampleApiApplication : Application
    {
	    public SampleApiApplication()
	    {
		    Name = "SampleApi";
		    Version = "1.0.0";
		    LifecycleStage = ApplicationLifecycleStage.Development;

		    EndPoints = new List<EndPoint>
		    {
			    new ToDoEndPoint(),
		    };
	    }
    }
}
