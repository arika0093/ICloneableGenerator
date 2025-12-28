using System.Collections.Generic;
using System.Linq;

namespace ICloneableGenerator.Tests;

/// <summary>
/// Tests for collection cloning functionality.
/// </summary>
public class CollectionCloneTests
{
    [Fact]
    public void DeepClone_ListOfCloneables_CreatesDeepCopy()
    {
        // Arrange
        var original = new ClassWithList
        {
            Name = "Parent",
            Items = new List<NestedClass>
            {
                new NestedClass { Value = "Item1" },
                new NestedClass { Value = "Item2" }
            }
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeSameAs(original.Items);
        clone.Items.Count.ShouldBe(2);
        
        // Each item should be a separate instance
        clone.Items[0].ShouldNotBeSameAs(original.Items[0]);
        clone.Items[0].Value.ShouldBe("Item1");
        clone.Items[1].ShouldNotBeSameAs(original.Items[1]);
        clone.Items[1].Value.ShouldBe("Item2");
    }

    [Fact]
    public void DeepClone_ListModification_DoesNotAffectOriginal()
    {
        // Arrange
        var original = new ClassWithList
        {
            Name = "Parent",
            Items = new List<NestedClass>
            {
                new NestedClass { Value = "Original" }
            }
        };

        // Act
        var clone = original.DeepClone();
        clone.Items[0].Value = "Modified";

        // Assert
        original.Items[0].Value.ShouldBe("Original");
    }

    [Fact]
    public void DeepClone_NullList_HandlesCorrectly()
    {
        // Arrange
        var original = new ClassWithList
        {
            Name = "Parent",
            Items = null
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldBeNull();
    }

    [Fact]
    public void DeepClone_ListOfValueTypes_CreatesNewList()
    {
        // Arrange
        var original = new ClassWithValueList
        {
            Name = "Parent",
            Numbers = new List<int> { 1, 2, 3 }
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Numbers.ShouldNotBeSameAs(original.Numbers);
        clone.Numbers.ShouldBe(new List<int> { 1, 2, 3 });
    }
}

// Test classes
public partial class ClassWithList : IDeepCloneable<ClassWithList>
{
    public string Name { get; set; } = "";
    public List<NestedClass>? Items { get; set; }
}

public partial class ClassWithValueList : IDeepCloneable<ClassWithValueList>
{
    public string Name { get; set; } = "";
    public List<int>? Numbers { get; set; }
}
