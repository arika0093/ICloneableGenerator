namespace ICloneableGenerator;

/// <summary>
/// Interface for types that support deep cloning.
/// When implemented on a partial class, the source generator will automatically generate the DeepClone method.
/// </summary>
/// <typeparam name="T">The type of the cloneable object.</typeparam>
public interface IDeepCloneable<T>
{
    /// <summary>
    /// Creates a deep clone of the current instance.
    /// All properties and nested objects are cloned recursively.
    /// </summary>
    /// <returns>A deep clone of the current instance.</returns>
    T DeepClone();
}

/// <summary>
/// Interface for types that support shallow cloning.
/// When implemented on a partial class, the source generator will automatically generate the ShallowClone method.
/// </summary>
/// <typeparam name="T">The type of the cloneable object.</typeparam>
public interface IShallowCloneable<T>
{
    /// <summary>
    /// Creates a shallow clone of the current instance.
    /// Only the immediate properties are cloned; nested objects are shared.
    /// </summary>
    /// <returns>A shallow clone of the current instance.</returns>
    T ShallowClone();
}
