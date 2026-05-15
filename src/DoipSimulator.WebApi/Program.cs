namespace DoipSimulator.WebApi;

public static class Program
{
    public static WebApplication CreateApp(string[] args, WebApiRuntimeOptions options)
    {
        return WebApiApplication.Create(args, options);
    }
}
