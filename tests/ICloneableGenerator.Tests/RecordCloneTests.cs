namespace ICloneableGenerator.Tests;

/// <summary>
/// Tests for record types with DeepClone functionality.
/// </summary>
public class RecordCloneTests
{
    [Fact]
    public void DeepClone_SimpleRecord_ClonesCorrectly()
    {
        // Arrange
        var original = new SimpleRecord("Alice", 30);

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeNull();
        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe("Alice");
        clone.Age.ShouldBe(30);
    }

    [Fact]
    public void DeepClone_RecordWithProperties_ClonesCorrectly()
    {
        // Arrange
        var original = new PersonRecord
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 25
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeNull();
        clone.ShouldNotBeSameAs(original);
        clone.FirstName.ShouldBe("John");
        clone.LastName.ShouldBe("Doe");
        clone.Age.ShouldBe(25);
    }

    [Fact]
    public void DeepClone_RecordWithNestedRecord_CreatesDeepCopy()
    {
        // Arrange
        var original = new PersonWithAddressRecord
        {
            Name = "Jane",
            Address = new AddressRecord
            {
                Street = "Main St",
                City = "NYC",
                ZipCode = "10001"
            }
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeNull();
        clone.ShouldNotBeSameAs(original);
        clone.Address.ShouldNotBeNull();
        clone.Address.ShouldNotBeSameAs(original.Address);
        clone.Address.Street.ShouldBe("Main St");
        clone.Address.City.ShouldBe("NYC");
    }

    [Fact]
    public void DeepClone_RecordWithInitOnlyProperties_ClonesCorrectly()
    {
        // Arrange
        var original = new RecordWithInitProps
        {
            Name = "Test",
            Value = 42
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeNull();
        clone.ShouldNotBeSameAs(original);
        clone.Name.ShouldBe("Test");
        clone.Value.ShouldBe(42);
    }

    [Fact]
    public void DeepClone_RecordStruct_ClonesCorrectly()
    {
        // Arrange
        var original = new PointRecordStruct(10.5, 20.5);

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.X.ShouldBe(10.5);
        clone.Y.ShouldBe(20.5);
    }

    [Fact]
    public void DeepClone_RecordWithCollection_ClonesCorrectly()
    {
        // Arrange
        var original = new RecordWithCollection
        {
            Name = "Test",
            Tags = new System.Collections.Generic.List<string> { "tag1", "tag2", "tag3" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        clone.ShouldNotBeNull();
        clone.ShouldNotBeSameAs(original);
        clone.Tags.ShouldNotBeNull();
        clone.Tags.ShouldNotBeSameAs(original.Tags);
        clone.Tags.ShouldBe(new[] { "tag1", "tag2", "tag3" });
    }
}

// Test records
public partial record SimpleRecord(string Name, int Age) : IDeepCloneable<SimpleRecord>;

public partial record PersonRecord : IDeepCloneable<PersonRecord>
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int Age { get; set; }
}

public partial record AddressRecord : IDeepCloneable<AddressRecord>
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string ZipCode { get; set; } = "";
}

public partial record PersonWithAddressRecord : IDeepCloneable<PersonWithAddressRecord>
{
    public string Name { get; set; } = "";
    public AddressRecord? Address { get; set; }
}

public partial record RecordWithInitProps : IDeepCloneable<RecordWithInitProps>
{
    public string Name { get; init; } = "";
    public int Value { get; init; }
}

public partial record struct PointRecordStruct(double X, double Y) : IDeepCloneable<PointRecordStruct>;

public partial record RecordWithCollection : IDeepCloneable<RecordWithCollection>
{
    public string Name { get; init; } = "";
    public System.Collections.Generic.List<string>? Tags { get; init; }
}
