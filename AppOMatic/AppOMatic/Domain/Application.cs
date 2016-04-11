namespace AppOMatic.Domain
{
    public abstract class Application : BaseDomainObject
    {
		public string Version { get; set; }

		public ApplicationLifecycleStage LifecycleStage { get; set; }

		protected override void Validate()
		{
			base.Validate();

			ValidateNullOrEmpty(Version, nameof(Version));
			ValidateRegEx(Version, @"(\d+\.\d+\.\d+(\-)?(alpha|beta|RC)(\.)?\d+)?", nameof(Version));
			ValidateIsNot(LifecycleStage, ApplicationLifecycleStage.Undefined, nameof(LifecycleStage));
		}
	}
}
