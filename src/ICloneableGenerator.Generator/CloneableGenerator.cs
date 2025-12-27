using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ICloneableGenerator.Generator;

[Generator]
public class CloneableGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource("ICloneable.g.cs", SourceText.From(SourceGenerationHelper.InterfaceSource, Encoding.UTF8));
        });

        // Find all partial classes that implement IDeepCloneable<T> or IShallowCloneable<T>
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateClass(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Generate the clone methods
        context.RegisterSourceOutput(classDeclarations, static (spc, source) => Execute(source!, spc));
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration &&
               classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static ClassInfo? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

        if (classSymbol is null)
            return null;

        // Check if the class implements IDeepCloneable<T> or IShallowCloneable<T>
        var deepCloneableInterface = FindCloneableInterface(classSymbol, "ICloneableGenerator.IDeepCloneable");
        var shallowCloneableInterface = FindCloneableInterface(classSymbol, "ICloneableGenerator.IShallowCloneable");

        if (deepCloneableInterface is null && shallowCloneableInterface is null)
            return null;

        // Check if the method is already implemented
        bool hasDeepClone = deepCloneableInterface is not null && HasMethodImplementation(classSymbol, "DeepClone");
        bool hasShallowClone = shallowCloneableInterface is not null && HasMethodImplementation(classSymbol, "ShallowClone");

        if ((deepCloneableInterface is not null && hasDeepClone) &&
            (shallowCloneableInterface is not null && hasShallowClone))
            return null;

        if (deepCloneableInterface is null && shallowCloneableInterface is null)
            return null;

        return new ClassInfo(
            classSymbol.Name,
            GetNamespace(classSymbol),
            classSymbol,
            deepCloneableInterface is not null && !hasDeepClone,
            shallowCloneableInterface is not null && !hasShallowClone
        );
    }

    private static INamedTypeSymbol? FindCloneableInterface(INamedTypeSymbol classSymbol, string interfaceName)
    {
        return classSymbol.AllInterfaces.FirstOrDefault(i =>
            i.OriginalDefinition.ToDisplayString().StartsWith(interfaceName));
    }

    private static bool HasMethodImplementation(INamedTypeSymbol classSymbol, string methodName)
    {
        return classSymbol.GetMembers(methodName).OfType<IMethodSymbol>().Any(m => 
            !m.IsAbstract && m.DeclaringSyntaxReferences.Any());
    }

    private static string? GetNamespace(ISymbol symbol)
    {
        var namespaceSymbol = symbol.ContainingNamespace;
        if (namespaceSymbol is null || namespaceSymbol.IsGlobalNamespace)
            return null;

        return namespaceSymbol.ToDisplayString();
    }

    private static void Execute(ClassInfo classInfo, SourceProductionContext context)
    {
        var source = GenerateCloneMethod(classInfo);
        context.AddSource($"{classInfo.ClassName}.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateCloneMethod(ClassInfo classInfo)
    {
        var sb = new StringBuilder();

        if (classInfo.Namespace is not null)
        {
            sb.AppendLine($"namespace {classInfo.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial class {classInfo.ClassName}");
        sb.AppendLine("{");

        if (classInfo.ShouldGenerateDeepClone)
        {
            sb.AppendLine(GenerateDeepCloneMethod(classInfo));
        }

        if (classInfo.ShouldGenerateShallowClone)
        {
            sb.AppendLine(GenerateShallowCloneMethod(classInfo));
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateDeepCloneMethod(ClassInfo classInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    public {classInfo.ClassName} DeepClone()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {classInfo.ClassName}");
        sb.AppendLine("        {");

        var properties = GetCloneableProperties(classInfo.ClassSymbol);
        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            var comma = i < properties.Count - 1 ? "," : "";
            sb.AppendLine($"            {prop.Name} = {GenerateDeepCloneExpression(prop)}{comma}");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");

        return sb.ToString();
    }

    private static string GenerateShallowCloneMethod(ClassInfo classInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    public {classInfo.ClassName} ShallowClone()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {classInfo.ClassName}");
        sb.AppendLine("        {");

        var properties = GetCloneableProperties(classInfo.ClassSymbol);
        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            var comma = i < properties.Count - 1 ? "," : "";
            sb.AppendLine($"            {prop.Name} = this.{prop.Name}{comma}");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");

        return sb.ToString();
    }

    private static string GenerateDeepCloneExpression(IPropertySymbol property)
    {
        var typeSymbol = property.Type;

        // Check if the type implements IDeepCloneable<T>
        if (typeSymbol is INamedTypeSymbol namedType)
        {
            var deepCloneableInterface = namedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("ICloneableGenerator.IDeepCloneable"));

            if (deepCloneableInterface is not null)
            {
                return $"this.{property.Name}?.DeepClone()";
            }

            // Handle collections
            if (IsCollectionType(namedType))
            {
                return GenerateCollectionDeepClone(property, namedType);
            }
        }

        // For value types and strings, just copy
        if (typeSymbol.IsValueType || typeSymbol.SpecialType == SpecialType.System_String)
        {
            return $"this.{property.Name}";
        }

        // For reference types without IDeepCloneable, shallow copy (same as shallow clone)
        return $"this.{property.Name}";
    }

    private static bool IsCollectionType(INamedTypeSymbol type)
    {
        var typeString = type.OriginalDefinition.ToDisplayString();
        return typeString.StartsWith("System.Collections.Generic.List<") ||
               typeString.StartsWith("System.Collections.Generic.IList<") ||
               typeString.StartsWith("System.Collections.Generic.IEnumerable<") ||
               typeString.StartsWith("System.Collections.Generic.ICollection<");
    }

    private static string GenerateCollectionDeepClone(IPropertySymbol property, INamedTypeSymbol collectionType)
    {
        if (collectionType.TypeArguments.Length == 0)
            return $"this.{property.Name}";

        var elementType = collectionType.TypeArguments[0];
        var elementTypeName = elementType.ToDisplayString();

        // Check if element type implements IDeepCloneable
        if (elementType is INamedTypeSymbol elementNamedType)
        {
            var deepCloneableInterface = elementNamedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("ICloneableGenerator.IDeepCloneable"));

            if (deepCloneableInterface is not null)
            {
                return $"this.{property.Name}?.Select(x => x.DeepClone()).ToList()";
            }
        }

        // For value types or types without IDeepCloneable, just create a new list
        return $"this.{property.Name}?.ToList()";
    }

    private static List<IPropertySymbol> GetCloneableProperties(INamedTypeSymbol classSymbol)
    {
        var properties = new List<IPropertySymbol>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is IPropertySymbol property &&
                !property.IsStatic &&
                !property.IsReadOnly &&
                property.SetMethod is not null &&
                property.SetMethod.DeclaredAccessibility == Accessibility.Public &&
                property.GetMethod is not null)
            {
                properties.Add(property);
            }
        }

        return properties;
    }

    private record ClassInfo(
        string ClassName,
        string? Namespace,
        INamedTypeSymbol ClassSymbol,
        bool ShouldGenerateDeepClone,
        bool ShouldGenerateShallowClone
    );
}

internal static class SourceGenerationHelper
{
    public const string InterfaceSource = @"namespace ICloneableGenerator;

/// <summary>
/// Interface for types that support deep cloning.
/// When implemented on a partial class, the source generator will automatically generate the DeepClone method.
/// </summary>
/// <typeparam name=""T"">The type of the cloneable object.</typeparam>
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
/// <typeparam name=""T"">The type of the cloneable object.</typeparam>
public interface IShallowCloneable<T>
{
    /// <summary>
    /// Creates a shallow clone of the current instance.
    /// Only the immediate properties are cloned; nested objects are shared.
    /// </summary>
    /// <returns>A shallow clone of the current instance.</returns>
    T ShallowClone();
}
";
}
