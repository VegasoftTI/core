# Automatic REST Web API

1. Consider the following ToDo table:

<pre>
Id	Title	Completed
--	------	---------
 1	Test 1	0
 2	Test 2  1
 </pre>

 2. Startup.cs:

<code>
public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
{
	...
	app.UseApiOMaticApplication(new SampleApiApplication(Configuration["Data:DefaultConnection:ConnectionString"]));
	...
}
</code>

3. Application settings:

<code>
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
</code>

4. Calling http://localhost/SampleApi/1.0.0/development/todo?pageSize=2&pageNumber=1

<pre>
{
	"pageSize": 2,
	"pageNumber": 1,
	"totalRows": 2,
	"totalPages": 1,
	"items": 
	[
		{
			"id": 1,
			"title": "Test 1",
			"completed": false
		},
		{
			"id": 2,
			"title": "Test 2",
			"completed": true
		}
	]
}
</pre>

Calling http://localhost:20066/SampleApi/1.0.0/development/todo?id=2

<pre>
{
	"id": 2,
	"item": 
	{
		"id": 2,
		"title": "Test 2",
		"completed": true
	}
}
</pre>

Calling http://localhost:20066/SampleApi/1.0.0/development/todo

<pre>
{
	"items":
	[
		{
			"id": 1,
			"ownerId": 1,
			"title": "Teste",
			"completed": false
		},
		{
			"id": 2,
			"ownerId": 1,
			"title": "Teste 2",
			"completed": true
		}
	]
}
</pre>