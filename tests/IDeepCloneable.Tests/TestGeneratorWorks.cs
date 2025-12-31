using IDeepCloneable;

namespace TestGeneratorWorks;

public partial class TestClass : IDeepCloneable<TestClass>
{
    public string Name { get; set; } = "";
}
