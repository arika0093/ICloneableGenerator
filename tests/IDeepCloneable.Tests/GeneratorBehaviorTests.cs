using IDeepCloneable;

namespace IDeepCloneable.Tests;

/// <summary>
/// Tests for generator behavior with partial classes and existing implementations.
/// </summary>
public class GeneratorBehaviorTests
{
    [Fact]
    public void PartialClass_ImplementsInterface_GeneratesMethod()
    {
        var original = new PartialTestClass { Name = "Test" };

        var clone = original.DeepClone();

        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
    }

    [Fact]
    public void ManualImplementation_IsUsed()
    {
        var original = new ManualImplementationClass { Name = "Test", CustomValue = 42 };

        var clone = original.DeepClone();

        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
        clone.CustomValue.ShouldBe(100);
    }

    [Fact]
    public void InterfaceInheritance_GeneratesMethod()
    {
        var original = new DerivedInterfaceClass { Name = "Test" };

        var clone = original.DeepClone();

        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
    }

    [Fact]
    public void AbstractClass_ImplementsInterface_GeneratesMethod()
    {
        var original = new ConcreteClass { Name = "Test", Value = 42 };

        var clone = (ConcreteClass)original.DeepClone();

        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
        clone.Value.ShouldBe(original.Value);
    }
}

public partial class PartialTestClass : IDeepCloneable<PartialTestClass>
{
    public string Name { get; set; } = string.Empty;
}

public partial class ManualImplementationClass : IDeepCloneable<ManualImplementationClass>
{
    public string Name { get; set; } = string.Empty;
    public int CustomValue { get; set; }

    public ManualImplementationClass DeepClone()
    {
        return new ManualImplementationClass
        {
            Name = this.Name,
            CustomValue = 100,
        };
    }
}

public interface ICustomCloneable : IDeepCloneable<DerivedInterfaceClass> { }

public partial class DerivedInterfaceClass : ICustomCloneable
{
    public string Name { get; set; } = string.Empty;
}

public abstract partial class AbstractBaseClass : IDeepCloneable<AbstractBaseClass>
{
    public string Name { get; set; } = string.Empty;

    public abstract AbstractBaseClass DeepClone();
}

public partial class ConcreteClass : AbstractBaseClass
{
    public int Value { get; set; }

    public override AbstractBaseClass DeepClone()
    {
        return new ConcreteClass { Name = this.Name, Value = this.Value };
    }
}
