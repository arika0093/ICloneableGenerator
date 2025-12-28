namespace ICloneableGenerator.Tests;

/// <summary>
/// Tests for generator behavior with partial classes and existing implementations.
/// </summary>
public class GeneratorBehaviorTests
{
    [Fact]
    public void PartialClass_ImplementsInterface_GeneratesMethod()
    {
        // Arrange
        var original = new PartialTestClass { Name = "Test" };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
    }

    [Fact]
    public void ManualImplementation_IsUsed()
    {
        // Arrange
        var original = new ManualImplementationClass { Name = "Test", CustomValue = 42 };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
        clone.CustomValue.ShouldBe(100); // Custom implementation sets this to 100
    }

    [Fact]
    public void BothInterfaces_GeneratesBothMethods()
    {
        // Arrange
        var original = new BothInterfacesClass { Name = "Test", Value = 123 };

        // Act
        var deepClone = original.DeepClone();
        var shallowClone = original.ShallowClone();

        // Assert
        deepClone.ShouldNotBeSameAs(original);
        deepClone.Name.ShouldBe(original.Name);
        deepClone.Value.ShouldBe(original.Value);

        shallowClone.ShouldNotBeSameAs(original);
        shallowClone.Name.ShouldBe(original.Name);
        shallowClone.Value.ShouldBe(original.Value);
    }

    [Fact]
    public void InterfaceInheritance_GeneratesMethod()
    {
        // Arrange
        var original = new DerivedInterfaceClass { Name = "Test" };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
    }

    [Fact]
    public void AbstractClass_ImplementsInterface_GeneratesMethod()
    {
        // Arrange
        var original = new ConcreteClass { Name = "Test", Value = 42 };

        // Act
        var clone = (ConcreteClass)original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
        clone.Value.ShouldBe(original.Value);
    }
}

// Test classes
public partial class PartialTestClass : IDeepCloneable<PartialTestClass>
{
    public string Name { get; set; } = "";
}

public partial class ManualImplementationClass : IDeepCloneable<ManualImplementationClass>
{
    public string Name { get; set; } = "";
    public int CustomValue { get; set; }

    public ManualImplementationClass DeepClone()
    {
        return new ManualImplementationClass
        {
            Name = this.Name,
            CustomValue = 100, // Custom implementation
        };
    }
}

public partial class BothInterfacesClass
    : IDeepCloneable<BothInterfacesClass>,
        IShallowCloneable<BothInterfacesClass>
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

public interface ICustomCloneable : IDeepCloneable<DerivedInterfaceClass> { }

public partial class DerivedInterfaceClass : ICustomCloneable
{
    public string Name { get; set; } = "";
}

public abstract partial class AbstractBaseClass : IDeepCloneable<AbstractBaseClass>
{
    public string Name { get; set; } = "";

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
