# IDeepCloneable

Automatic implementation generator for the `IDeepCloneable<T>` interface via source generators.

## Why IDeepCloneable?

**Great for library authors.** IDeepCloneable lets you expose deep-clone support without shipping hand-written clone methods. Consumers simply implement the interface on partial types and get a generated `DeepClone()`.

### Key Benefits

- 🏗️ No manual clone code for base types
- 📚 Friendly for consumers—just add the interface
- 🔧 Override-friendly: custom implementations are respected
- 🎯 NativeAOT compatible (no reflection)
- 📦 Zero runtime dependencies

## Features

- 🚀 Automatic `DeepClone()` generation for partial classes, records, and structs
- 🔍 Deep cloning across nested objects that also implement `IDeepCloneable<T>`
- 📦 Broad collection support (arrays, dictionaries, common and immutable collections)
- 📝 Record support, including init-only properties
- 🛡️ Type-safe compile-time generation
- 👪 Inheritance support—properties across the hierarchy are cloned

## Installation

```bash
dotnet add package IDeepCloneable
```

## Usage

### For Library Authors

Declare your base types with the interface—no implementation needed:

```csharp
using IDeepCloneable;

namespace YourLibrary;

public abstract partial class LibraryConfig : IDeepCloneable<LibraryConfig>
{
    public string Setting { get; set; }
}
```

### For Library Users

Consumers just keep types partial; `DeepClone()` is generated automatically:

```csharp
using YourLibrary;

public partial class MyConfig : LibraryConfig
{
    public int CustomValue { get; set; }
}

var config = new MyConfig { Setting = "base", CustomValue = 42 };
var clone = config.DeepClone();
```

### Deep Cloning Example

```csharp
using IDeepCloneable;

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
```

## Requirements for Code Generation

1. The type (class, record, or struct) is declared as `partial`.
2. It implements `IDeepCloneable<T>` (or a derived interface).
3. A user implementation of `DeepClone()` does not already exist.

## Custom Implementation

Need custom logic? Implement `DeepClone()` yourself—the generator will skip generation.

```csharp
public partial class Person : IDeepCloneable<Person>
{
    public string Name { get; set; }
    public int SecretValue { get; set; }

    public Person DeepClone()
    {
        return new Person { Name = this.Name, SecretValue = 0 };
    }
}
```

## Interface Inheritance

Derived interfaces work as expected:

```csharp
public interface IMyCloneable : IDeepCloneable<MyClass> { }

public partial class MyClass : IMyCloneable
{
    public string Name { get; set; }
}
```

## Advanced Features

### Collections Support

```csharp
public partial class Container : IDeepCloneable<Container>
{
    public int[] Numbers { get; set; }
    public Dictionary<string, Person> Users { get; set; }
    public List<string> Tags { get; set; }
    public ImmutableList<int> Scores { get; set; }
}
```

### Record Types

```csharp
public partial record PersonRecord(string Name, int Age) : IDeepCloneable<PersonRecord>;

public partial record Settings : IDeepCloneable<Settings>
{
    public string Name { get; init; }
    public int Value { get; init; }
}
```

### Structs

```csharp
public partial struct Vector3 : IDeepCloneable<Vector3>
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
```

## How It Works

1. Scans partial classes, records, and structs implementing `IDeepCloneable<T>`.
2. Skips types with an existing `DeepClone()` implementation.
3. Generates `DeepClone()` that recursively clones nested `IDeepCloneable<T>` members.
4. Rebuilds collections with cloned elements when applicable.
5. Handles arrays (including multi-dimensional), dictionaries, and immutable collections.
6. Uses `with` expressions for records with init-only properties when needed.

## License

Apache-2.0
