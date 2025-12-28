namespace ICloneableGenerator.Tests;

/// <summary>
/// Tests for basic DeepClone functionality.
/// </summary>
public class DeepCloneTests
{
    [Fact]
    public void DeepClone_SimpleClass_ClonesAllProperties()
    {
        // Arrange
        var original = new SimpleClass
        {
            Name = "Test",
            Age = 25,
            IsActive = true,
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
        clone.Age.ShouldBe(original.Age);
        clone.IsActive.ShouldBe(original.IsActive);
    }

    [Fact]
    public void DeepClone_ModifyingClone_DoesNotAffectOriginal()
    {
        // Arrange
        var original = new SimpleClass { Name = "Original", Age = 30 };

        // Act
        var clone = original.DeepClone();
        clone.Name = "Modified";
        clone.Age = 40;

        // Assert
        original.Name.ShouldBe("Original");
        original.Age.ShouldBe(30);
    }

    [Fact]
    public void DeepClone_NestedObject_CreatesDeepCopy()
    {
        // Arrange
        var original = new ClassWithNested
        {
            Name = "Parent",
            Nested = new NestedClass { Value = "Nested Value" },
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Nested.ShouldNotBeSameAs(original.Nested);
        clone.Nested.Value.ShouldBe(original.Nested.Value);
    }

    [Fact]
    public void DeepClone_NestedObjectModification_DoesNotAffectOriginal()
    {
        // Arrange
        var original = new ClassWithNested
        {
            Name = "Parent",
            Nested = new NestedClass { Value = "Original Nested" },
        };

        // Act
        var clone = original.DeepClone();
        clone.Nested.Value = "Modified Nested";

        // Assert
        original.Nested.Value.ShouldBe("Original Nested");
    }

    [Fact]
    public void DeepClone_NullNestedObject_HandlesCorrectly()
    {
        // Arrange
        var original = new ClassWithNested { Name = "Parent", Nested = null };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Nested.ShouldBeNull();
    }
}

// Test classes
public partial class SimpleClass : IDeepCloneable<SimpleClass>
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

public partial class NestedClass : IDeepCloneable<NestedClass>
{
    public string Value { get; set; } = "";
}

public partial class ClassWithNested : IDeepCloneable<ClassWithNested>
{
    public string Name { get; set; } = "";
    public NestedClass? Nested { get; set; }
}
