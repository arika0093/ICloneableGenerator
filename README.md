# ICloneableGenerator

Automatic implementation generator for `IDeepCloneable<T>` and `IShallowCloneable<T>` interfaces via source generators.

## Why ICloneableGenerator?

**Perfect for 3rd-party library authors!** ICloneableGenerator enables library developers to provide cloning functionality without implementing clone methods in their base classes. Users of your library can simply implement the interface and get automatic clone implementations via source generation.

### Key Benefits for Library Authors

- 🏗️ **No Implementation Required**: Define interfaces without implementing clone methods
- 📚 **User-Friendly**: Library users get automatic cloning by just adding the interface
- 🔧 **Flexible**: Users can override with custom implementations when needed
- 🎯 **NativeAOT Compatible**: Works with NativeAOT without reflection

## Features

- 🚀 **Automatic Clone Implementation**: Automatically generates `DeepClone()` and `ShallowClone()` methods for partial classes, records, and structs
- 🔍 **Deep & Shallow Cloning**: Support for both deep and shallow cloning strategies
- 📦 **Advanced Collections**: Full support for arrays, dictionaries, and all common collection types (List, Stack, Queue, HashSet, etc.)
- 🔒 **Immutable Collections**: Support for ImmutableList, ImmutableArray, ImmutableDictionary, and more
- 📝 **Record Support**: Works with record types and record structs, including init-only properties
- 🎯 **NativeAOT Compatible**: Works with NativeAOT without reflection
- 📦 **Zero Runtime Dependencies**: Uses source generators for compile-time code generation
- 🛡️ **Type Safe**: Fully type-safe implementation with compile-time checking
- 👪 **Inheritance Support**: Clones properties from entire inheritance chain, even if child classes don't implement the interface

## Installation

Install the package via NuGet:

```bash
dotnet add package ICloneableGenerator
```

## Usage

### For Library Authors

Define your library classes with the cloneable interfaces without implementation:

```csharp
using ICloneableGenerator;

namespace YourLibrary;

// Library base class - just declare the interface
public abstract partial class LibraryConfig : IDeepCloneable<LibraryConfig>
{
    public string Setting { get; set; }
    // No need to implement DeepClone() - users will get it automatically!
}

// Library users can extend and get cloning for free
```

### For Library Users

When using a 3rd-party library that uses ICloneableGenerator:

```csharp
using YourLibrary;

// Just make your class partial - DeepClone() is auto-generated!
public partial class MyConfig : LibraryConfig
{
    public int CustomValue { get; set; }
    // DeepClone() automatically clones both base and derived properties
}

var config = new MyConfig 
{ 
    Setting = "base",
    CustomValue = 42 
};
var clone = config.DeepClone(); // Works automatically!
```

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

1. ✅ The type (class, record, or struct) is declared as `partial`
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

## Advanced Features

### Collections Support

ICloneableGenerator supports a wide variety of collection types with deep cloning:

#### Arrays
```csharp
public partial class DataContainer : IDeepCloneable<DataContainer>
{
    public int[] Numbers { get; set; }
    public string[] Names { get; set; }
    public Person[] People { get; set; }  // Deep clones array elements
    public int[,] Matrix { get; set; }     // Multi-dimensional arrays
}
```

#### Dictionaries
```csharp
public partial class Configuration : IDeepCloneable<Configuration>
{
    public Dictionary<string, int> Settings { get; set; }
    public Dictionary<string, Person> Users { get; set; }  // Deep clones values
}
```

#### Common Collections
```csharp
public partial class Container : IDeepCloneable<Container>
{
    public List<int> Numbers { get; set; }
    public Stack<string> History { get; set; }
    public Queue<Task> Tasks { get; set; }
    public HashSet<string> Tags { get; set; }
    public SortedSet<int> Priorities { get; set; }
}
```

#### Observable and ReadOnly Collections
```csharp
public partial class ViewModel : IDeepCloneable<ViewModel>
{
    public ObservableCollection<Item> Items { get; set; }
    public ReadOnlyCollection<string> Constants { get; set; }
}
```

#### Immutable Collections
```csharp
public partial class ImmutableData : IDeepCloneable<ImmutableData>
{
    public ImmutableList<int> Numbers { get; set; }
    public ImmutableArray<string> Names { get; set; }
    public ImmutableDictionary<string, int> Scores { get; set; }
    public ImmutableHashSet<string> Tags { get; set; }
}
```

### Record Types Support

ICloneableGenerator fully supports C# records and record structs:

#### Record Classes
```csharp
public partial record Person(string Name, int Age) : IDeepCloneable<Person>;

public partial record PersonWithAddress : IDeepCloneable<PersonWithAddress>
{
    public string Name { get; init; }
    public Address Address { get; init; }  // Deep clones nested records
}
```

#### Record Structs
```csharp
public partial record struct Point(double X, double Y) : IDeepCloneable<Point>;
```

#### Init-Only Properties
```csharp
public partial record Configuration : IDeepCloneable<Configuration>
{
    public string Name { get; init; }
    public int Value { get; init; }
    // Uses 'with' expression for efficient cloning
}
```

### Struct Support

Regular structs are also supported:

```csharp
public partial struct Vector3 : IDeepCloneable<Vector3>
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
```

## How It Works

The source generator:

1. Scans for partial classes, records, and structs implementing `IDeepCloneable<T>` or `IShallowCloneable<T>`
2. Checks if the method is already implemented
3. Generates the appropriate clone method implementation
4. For deep cloning, recursively clones nested objects that also implement `IDeepCloneable<T>`
5. Handles collections by creating new collection instances with cloned elements (when elements are cloneable)
6. Uses efficient `with` expressions for records with init-only properties
7. Properly handles arrays (including multi-dimensional), dictionaries, and specialized collection types
8. Supports both mutable and immutable collection types

## License

This project is licensed under the Apache-2.0 License.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request