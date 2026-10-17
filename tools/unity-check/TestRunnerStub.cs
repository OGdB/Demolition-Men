// Compile-check stand-in for UnityEngine.TestRunner (not published as a reference assembly).
namespace UnityEngine.TestTools
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class UnityTestAttribute : NUnit.Framework.CombiningStrategyAttribute
    {
        public UnityTestAttribute() : base(new NUnit.Framework.Internal.Builders.CombinatorialStrategy(), new NUnit.Framework.Internal.Builders.ParameterDataSourceProvider()) { }
    }
}
