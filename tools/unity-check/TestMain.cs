public static class TestMain
{
    public static int Main(string[] args) => args.Length > 0 && args[0] == "dump-levels"
        ? LevelDump.Run(args)
        : new NUnitLite.AutoRun(typeof(TestMain).Assembly).Execute(args);
}
