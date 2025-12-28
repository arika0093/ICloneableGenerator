using System.Linq;

namespace ICloneableGenerator.Tests;

/// <summary>
/// Tests for array cloning functionality.
/// </summary>
public class ArrayCloneTests
{
    [Fact]
    public void DeepClone_IntArray_ClonesArray()
    {
        // Arrange
        var original = new ClassWithIntArray { Name = "Test", Numbers = new[] { 1, 2, 3, 4, 5 } };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Numbers.ShouldNotBeNull();
        clone.Numbers.ShouldNotBeSameAs(original.Numbers);
        clone.Numbers.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void DeepClone_StringArray_ClonesArray()
    {
        // Arrange
        var original = new ClassWithStringArray
        {
            Name = "Test",
            Items = new[] { "one", "two", "three" },
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        clone.Items.ShouldNotBeSameAs(original.Items);
        clone.Items.ShouldBe(new[] { "one", "two", "three" });
    }

    [Fact]
    public void DeepClone_ArrayOfCloneables_CreatesDeepCopy()
    {
        // Arrange
        var original = new ClassWithCloneableArray
        {
            Name = "Parent",
            Items = new[]
            {
                new SimpleClass { Name = "Item1", Age = 1 },
                new SimpleClass { Name = "Item2", Age = 2 },
            },
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        clone.Items.ShouldNotBeSameAs(original.Items);
        clone.Items.Length.ShouldBe(2);

        // Each item should be a separate instance
        clone.Items[0].ShouldNotBeNull();
        clone.Items[0].ShouldNotBeSameAs(original.Items[0]);
        clone.Items[0]?.Name.ShouldBe("Item1");
        clone.Items[1].ShouldNotBeNull();
        clone.Items[1].ShouldNotBeSameAs(original.Items[1]);
        clone.Items[1]?.Name.ShouldBe("Item2");
    }

    [Fact]
    public void DeepClone_ArrayModification_DoesNotAffectOriginal()
    {
        // Arrange
        var original = new ClassWithCloneableArray
        {
            Name = "Parent",
            Items = new[]
            {
                new SimpleClass { Name = "Original", Age = 10 },
            },
        };

        // Act
        var clone = original.DeepClone();
        clone.Items.ShouldNotBeNull();
        if (clone.Items[0] != null)
        {
            clone.Items[0].Name = "Modified";
        }

        // Assert
        original.Items[0].Name.ShouldBe("Original");
    }

    [Fact]
    public void DeepClone_NullArray_HandlesCorrectly()
    {
        // Arrange
        var original = new ClassWithIntArray { Name = "Test", Numbers = null };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Numbers.ShouldBeNull();
    }

    [Fact]
    public void DeepClone_EmptyArray_HandlesCorrectly()
    {
        // Arrange
        var original = new ClassWithIntArray { Name = "Test", Numbers = System.Array.Empty<int>() };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Numbers.ShouldNotBeNull();
        clone.Numbers.Length.ShouldBe(0);
    }

    [Fact]
    public void DeepClone_MultiDimensionalArray_ClonesArray()
    {
        // Arrange
        var original = new ClassWithMultiDimensionalArray
        {
            Name = "Test",
            Matrix = new int[,]
            {
                { 1, 2 },
                { 3, 4 },
            },
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Matrix.ShouldNotBeNull();
        clone.Matrix.ShouldNotBeSameAs(original.Matrix);
        clone.Matrix[0, 0].ShouldBe(1);
        clone.Matrix[0, 1].ShouldBe(2);
        clone.Matrix[1, 0].ShouldBe(3);
        clone.Matrix[1, 1].ShouldBe(4);
    }
}

// Test classes
public partial class ClassWithIntArray : IDeepCloneable<ClassWithIntArray>
{
    public string Name { get; set; } = "";
    public int[]? Numbers { get; set; }
}

public partial class ClassWithStringArray : IDeepCloneable<ClassWithStringArray>
{
    public string Name { get; set; } = "";
    public string[]? Items { get; set; }
}

public partial class ClassWithCloneableArray : IDeepCloneable<ClassWithCloneableArray>
{
    public string Name { get; set; } = "";
    public SimpleClass?[]? Items { get; set; }
}

public partial class ClassWithMultiDimensionalArray : IDeepCloneable<ClassWithMultiDimensionalArray>
{
    public string Name { get; set; } = "";
    public int[,]? Matrix { get; set; }
}
