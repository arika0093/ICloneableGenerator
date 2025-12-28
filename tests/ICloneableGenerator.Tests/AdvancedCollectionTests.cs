using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;

namespace ICloneableGenerator.Tests;

/// <summary>
/// Tests for advanced collection types (Stack, Queue, HashSet, Immutable collections, etc.).
/// </summary>
public class AdvancedCollectionTests
{
    [Fact]
    public void DeepClone_Stack_ClonesCorrectly()
    {
        // Arrange
        var original = new ClassWithStack
        {
            Name = "Test",
            Items = new Stack<int>(new[] { 1, 2, 3 }),
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        clone.Items.ShouldNotBeSameAs(original.Items);
        clone.Items.Count.ShouldBe(3);

        // Verify order (LIFO)
        clone.Items.Pop().ShouldBe(3);
        clone.Items.Pop().ShouldBe(2);
        clone.Items.Pop().ShouldBe(1);
    }

    [Fact]
    public void DeepClone_Queue_ClonesCorrectly()
    {
        // Arrange
        var original = new ClassWithQueue
        {
            Name = "Test",
            Items = new Queue<int>(new[] { 1, 2, 3 }),
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        clone.Items.ShouldNotBeSameAs(original.Items);
        clone.Items.Count.ShouldBe(3);

        // Verify order (FIFO)
        clone.Items.Dequeue().ShouldBe(1);
        clone.Items.Dequeue().ShouldBe(2);
        clone.Items.Dequeue().ShouldBe(3);
    }

    [Fact]
    public void DeepClone_HashSet_ClonesCorrectly()
    {
        // Arrange
        var original = new ClassWithHashSet
        {
            Name = "Test",
            Items = new HashSet<int> { 1, 2, 3, 4, 5 },
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        clone.Items.ShouldNotBeSameAs(original.Items);
        clone.Items.Count.ShouldBe(5);
        clone.Items.ShouldBe(new HashSet<int> { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void DeepClone_SortedSet_ClonesCorrectly()
    {
        // Arrange
        var original = new ClassWithSortedSet
        {
            Name = "Test",
            Items = new SortedSet<int> { 3, 1, 4, 1, 5, 9 }, // Duplicates are removed
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        clone.Items.ShouldNotBeSameAs(original.Items);
        clone.Items.Count.ShouldBe(5);
        clone.Items.ToArray().ShouldBe(new[] { 1, 3, 4, 5, 9 }); // Sorted order
    }

    [Fact]
    public void DeepClone_ObservableCollection_ClonesCorrectly()
    {
        // Arrange
        var original = new ClassWithObservableCollection
        {
            Name = "Test",
            Items = new ObservableCollection<int> { 1, 2, 3 },
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        clone.Items.ShouldNotBeSameAs(original.Items);
        clone.Items.Count.ShouldBe(3);
        clone.Items.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void DeepClone_ReadOnlyCollection_ClonesCorrectly()
    {
        // Arrange
        var original = new ClassWithReadOnlyCollection
        {
            Name = "Test",
            Items = new ReadOnlyCollection<int>(new List<int> { 1, 2, 3 }),
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        clone.Items.ShouldNotBeSameAs(original.Items);
        clone.Items.Count.ShouldBe(3);
        clone.Items.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void DeepClone_ImmutableList_ClonesCorrectly()
    {
        // Arrange
        var original = new ClassWithImmutableList
        {
            Name = "Test",
            Items = ImmutableList.Create(1, 2, 3),
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        // Note: Immutable collections may share instances for same values, that's OK
        clone.Items.Count.ShouldBe(3);
        clone.Items.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void DeepClone_ImmutableArray_ClonesCorrectly()
    {
        // Arrange
        var original = new ClassWithImmutableArray
        {
            Name = "Test",
            Items = ImmutableArray.Create(1, 2, 3),
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.IsDefault.ShouldBeFalse();
        clone.Items.Length.ShouldBe(3);
        clone.Items.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void DeepClone_ImmutableHashSet_ClonesCorrectly()
    {
        // Arrange
        var original = new ClassWithImmutableHashSet
        {
            Name = "Test",
            Items = ImmutableHashSet.Create(1, 2, 3),
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        // Note: Immutable collections may share instances for same values, that's OK
        clone.Items.Count.ShouldBe(3);
        clone.Items.OrderBy(x => x).ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void DeepClone_ImmutableDictionary_ClonesCorrectly()
    {
        // Arrange
        var original = new ClassWithImmutableDictionary
        {
            Name = "Test",
            Items = ImmutableDictionary
                .Create<string, int>()
                .Add("one", 1)
                .Add("two", 2)
                .Add("three", 3),
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        clone.Items.Count.ShouldBe(3);
        clone.Items["one"].ShouldBe(1);
        clone.Items["two"].ShouldBe(2);
        clone.Items["three"].ShouldBe(3);
    }

    [Fact]
    public void DeepClone_StackOfCloneables_CreatesDeepCopy()
    {
        // Arrange
        var original = new ClassWithCloneableStack
        {
            Name = "Test",
            Items = new Stack<SimpleClass>(
                new[]
                {
                    new SimpleClass { Name = "Item3", Age = 3 },
                    new SimpleClass { Name = "Item2", Age = 2 },
                    new SimpleClass { Name = "Item1", Age = 1 },
                }
            ),
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeSameAs(original);
        clone.Items.ShouldNotBeNull();
        clone.Items.ShouldNotBeSameAs(original.Items);

        // Verify deep clone of elements
        var clonedItem = clone.Items.Pop();
        clonedItem.ShouldNotBeNull();
        clonedItem.Name.ShouldBe("Item1");

        // Modify cloned item
        clonedItem.Name = "Modified";

        // Original should be unchanged
        original.Items.Peek().Name.ShouldBe("Item1");
    }
}

// Test classes
public partial class ClassWithStack : IDeepCloneable<ClassWithStack>
{
    public string Name { get; set; } = "";
    public Stack<int>? Items { get; set; }
}

public partial class ClassWithQueue : IDeepCloneable<ClassWithQueue>
{
    public string Name { get; set; } = "";
    public Queue<int>? Items { get; set; }
}

public partial class ClassWithHashSet : IDeepCloneable<ClassWithHashSet>
{
    public string Name { get; set; } = "";
    public HashSet<int>? Items { get; set; }
}

public partial class ClassWithSortedSet : IDeepCloneable<ClassWithSortedSet>
{
    public string Name { get; set; } = "";
    public SortedSet<int>? Items { get; set; }
}

public partial class ClassWithObservableCollection : IDeepCloneable<ClassWithObservableCollection>
{
    public string Name { get; set; } = "";
    public ObservableCollection<int>? Items { get; set; }
}

public partial class ClassWithReadOnlyCollection : IDeepCloneable<ClassWithReadOnlyCollection>
{
    public string Name { get; set; } = "";
    public ReadOnlyCollection<int>? Items { get; set; }
}

public partial class ClassWithImmutableList : IDeepCloneable<ClassWithImmutableList>
{
    public string Name { get; set; } = "";
    public ImmutableList<int>? Items { get; set; }
}

public partial class ClassWithImmutableArray : IDeepCloneable<ClassWithImmutableArray>
{
    public string Name { get; set; } = "";
    public ImmutableArray<int> Items { get; set; }
}

public partial class ClassWithImmutableHashSet : IDeepCloneable<ClassWithImmutableHashSet>
{
    public string Name { get; set; } = "";
    public ImmutableHashSet<int>? Items { get; set; }
}

public partial class ClassWithImmutableDictionary : IDeepCloneable<ClassWithImmutableDictionary>
{
    public string Name { get; set; } = "";
    public ImmutableDictionary<string, int>? Items { get; set; }
}

public partial class ClassWithCloneableStack : IDeepCloneable<ClassWithCloneableStack>
{
    public string Name { get; set; } = "";
    public Stack<SimpleClass>? Items { get; set; }
}
