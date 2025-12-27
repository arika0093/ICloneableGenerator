namespace ICloneableGenerator.Tests;

/// <summary>
/// Tests for basic ShallowClone functionality.
/// </summary>
public class ShallowCloneTests
{
    [Fact]
    public void ShallowClone_SimpleClass_ClonesAllProperties()
    {
        // Arrange
        var original = new SimpleShallowClass
        {
            Name = "Test",
            Age = 25,
            IsActive = true
        };

        // Act
        var clone = original.ShallowClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
        clone.Age.ShouldBe(original.Age);
        clone.IsActive.ShouldBe(original.IsActive);
    }

    [Fact]
    public void ShallowClone_NestedObject_SharesReference()
    {
        // Arrange
        var nested = new NestedShallowClass { Value = "Shared" };
        var original = new ClassWithNestedShallow
        {
            Name = "Parent",
            Nested = nested
        };

        // Act
        var clone = original.ShallowClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Nested.ShouldBeSameAs(original.Nested); // Shallow clone shares reference
    }

    [Fact]
    public void ShallowClone_NestedObjectModification_AffectsBothInstances()
    {
        // Arrange
        var original = new ClassWithNestedShallow
        {
            Name = "Parent",
            Nested = new NestedShallowClass { Value = "Original" }
        };

        // Act
        var clone = original.ShallowClone();
        clone.Nested.Value = "Modified";

        // Assert
        original.Nested.Value.ShouldBe("Modified"); // Both share the same nested object
    }

    [Fact]
    public void ShallowClone_NullNestedObject_HandlesCorrectly()
    {
        // Arrange
        var original = new ClassWithNestedShallow
        {
            Name = "Parent",
            Nested = null
        };

        // Act
        var clone = original.ShallowClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Nested.ShouldBeNull();
    }
}

// Test classes
public partial class SimpleShallowClass : IShallowCloneable<SimpleShallowClass>
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

public partial class NestedShallowClass
{
    public string Value { get; set; } = "";
}

public partial class ClassWithNestedShallow : IShallowCloneable<ClassWithNestedShallow>
{
    public string Name { get; set; } = "";
    public NestedShallowClass? Nested { get; set; }
}
