public static class TestMain
{
    public static int Main(string[] args) => new NUnitLite.AutoRun(typeof(TestMain).Assembly).Execute(args);
}
