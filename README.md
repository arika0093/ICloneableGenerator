# ICloneableGenerator

Automatic implementation generator for `IDeepCloneable<T>` and `IShallowCloneable<T>` interfaces via source generators.

## Features

- 🚀 **Automatic Clone Implementation**: Automatically generates `DeepClone()` and `ShallowClone()` methods for partial classes
- 🔍 **Deep & Shallow Cloning**: Support for both deep and shallow cloning strategies
- 🎯 **NativeAOT Compatible**: Works with NativeAOT without reflection
- 📦 **Zero Runtime Dependencies**: Uses source generators for compile-time code generation
- 🛡️ **Type Safe**: Fully type-safe implementation with compile-time checking

## Installation

Install the package via NuGet:

```bash
dotnet add package ICloneableGenerator
```

## Usage

### Deep Cloning

For deep cloning (copies all properties and nested objects recursively):

```csharp
using ICloneableGenerator;

public partial class Person : IDeepCloneable<Person>
{
    public string Name { get; set; }
    public int Age { get; set; }
    public Address Address { get; set; }
}

public partial class Address : IDeepCloneable<Address>
{
    public string Street { get; set; }
    public string City { get; set; }
}

// Usage
var person = new Person 
{ 
    Name = "John", 
    Age = 30,
    Address = new Address { Street = "Main St", City = "NYC" }
};

var clone = person.DeepClone();
// clone is a completely independent copy
clone.Address.City = "Boston"; // Does not affect original
```

### Shallow Cloning

For shallow cloning (copies immediate properties only, nested objects are shared):

```csharp
using ICloneableGenerator;

public partial class Person : IShallowCloneable<Person>
{
    public string Name { get; set; }
    public int Age { get; set; }
    public Address Address { get; set; }
}

// Usage
var person = new Person 
{ 
    Name = "John", 
    Age = 30,
    Address = new Address { Street = "Main St", City = "NYC" }
};

var clone = person.ShallowClone();
// clone shares nested object references with original
clone.Address.City = "Boston"; // Affects original too!
```

### Requirements for Code Generation

The source generator will automatically generate clone methods when:

1. ✅ The class is declared as `partial`
2. ✅ It implements `IDeepCloneable<T>` or `IShallowCloneable<T>` (or derived interfaces)
3. ✅ The implementation does not already exist

### Custom Implementation

If you need custom cloning logic, you can implement the method yourself:

```csharp
public partial class Person : IDeepCloneable<Person>
{
    public string Name { get; set; }
    public int SecretValue { get; set; }

    // Custom implementation - generator will not override
    public Person DeepClone()
    {
        return new Person
        {
            Name = this.Name,
            SecretValue = 0 // Custom logic: don't clone secret
        };
    }
}
```

### Interface Inheritance

The generator works with derived interfaces:

```csharp
public interface IMyCloneable : IDeepCloneable<MyClass>
{
    // Additional methods
}

public partial class MyClass : IMyCloneable
{
    public string Name { get; set; }
    // DeepClone() will be auto-generated
}
```

## How It Works

The source generator:

1. Scans for partial classes implementing `IDeepCloneable<T>` or `IShallowCloneable<T>`
2. Checks if the method is already implemented
3. Generates the appropriate clone method implementation
4. For deep cloning, recursively clones nested objects that also implement `IDeepCloneable<T>`
5. Handles nullable types and collections appropriately

## License

This project is licensed under the Apache-2.0 License.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request