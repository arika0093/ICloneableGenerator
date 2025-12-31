using IDeepCloneable;

namespace IDeepCloneable.Tests;

/// <summary>
/// Tests for basic DeepClone functionality.
/// </summary>
public class DeepCloneTests
{
    [Fact]
    public void DeepClone_SimpleClass_ClonesAllProperties()
    {
        var original = new SimpleClass
        {
            Name = "Test",
            Age = 25,
            IsActive = true,
        };

        var clone = original.DeepClone();

        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe(original.Name);
        clone.Age.ShouldBe(original.Age);
        clone.IsActive.ShouldBe(original.IsActive);
    }

    [Fact]
    public void DeepClone_ModifyingClone_DoesNotAffectOriginal()
    {
        var original = new SimpleClass { Name = "Original", Age = 30 };

        var clone = original.DeepClone();
        clone.Name = "Modified";
        clone.Age = 40;

        original.Name.ShouldBe("Original");
        original.Age.ShouldBe(30);
    }

    [Fact]
    public void DeepClone_NestedObject_CreatesDeepCopy()
    {
        var original = new ClassWithNested
        {
            Name = "Parent",
            Nested = new NestedClass { Value = "Nested Value" },
        };

        var clone = original.DeepClone();

        clone.ShouldNotBeSameAs(original);
        clone.Nested.ShouldNotBeSameAs(original.Nested);
        clone.Nested.Value.ShouldBe(original.Nested.Value);
    }

    [Fact]
    public void DeepClone_NestedObjectModification_DoesNotAffectOriginal()
    {
        var original = new ClassWithNested
        {
            Name = "Parent",
            Nested = new NestedClass { Value = "Original Nested" },
        };

        var clone = original.DeepClone();
        clone.Nested.Value = "Modified Nested";

        original.Nested.Value.ShouldBe("Original Nested");
    }

    [Fact]
    public void DeepClone_NullNestedObject_HandlesCorrectly()
    {
        var original = new ClassWithNested { Name = "Parent", Nested = null };

        var clone = original.DeepClone();

        clone.ShouldNotBeSameAs(original);
        clone.Nested.ShouldBeNull();
    }
}

public partial class SimpleClass : IDeepCloneable<SimpleClass>
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

public partial class NestedClass : IDeepCloneable<NestedClass>
{
    public string Value { get; set; } = string.Empty;
}

public partial class ClassWithNested : IDeepCloneable<ClassWithNested>
{
    public string Name { get; set; } = string.Empty;
    public NestedClass? Nested { get; set; }
}
