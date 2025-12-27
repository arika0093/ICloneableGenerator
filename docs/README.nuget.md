# ICloneableGenerator

Automatic implementation generator for `IDeepCloneable<T>` and `IShallowCloneable<T>` interfaces via source generators.

## Quick Start

```csharp
using ICloneableGenerator;

// Deep cloning - recursively clones all nested objects
public partial class Person : IDeepCloneable<Person>
{
    public string Name { get; set; }
    public Address Address { get; set; }
}

// Shallow cloning - shares references to nested objects
public partial class Config : IShallowCloneable<Config>
{
    public string Setting { get; set; }
    public int Value { get; set; }
}
```

The source generator automatically creates `DeepClone()` and `ShallowClone()` methods for partial classes implementing the interfaces.

## Features

- ✅ Automatic clone implementation via source generators
- ✅ Deep and shallow cloning support
- ✅ NativeAOT compatible (no reflection)
- ✅ Zero runtime dependencies
- ✅ Type safe with compile-time checking

For full documentation, visit: https://github.com/arika0093/ICloneableGenerator
