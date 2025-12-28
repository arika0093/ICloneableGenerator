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
    private const string DeepCloneMethodName = "DeepClone";
    private const string ShallowCloneMethodName = "ShallowClone";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
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

        // Skip abstract classes - they can't be instantiated
        if (classSymbol.IsAbstract)
            return null;

        // Check if the class implements IDeepCloneable<T> or IShallowCloneable<T>
        var deepCloneableInterface = FindCloneableInterface(classSymbol, "ICloneableGenerator.IDeepCloneable");
        var shallowCloneableInterface = FindCloneableInterface(classSymbol, "ICloneableGenerator.IShallowCloneable");

        if (deepCloneableInterface is null && shallowCloneableInterface is null)
            return null;

        // Check if the method is already implemented
        bool hasDeepClone = deepCloneableInterface is not null && HasMethodImplementation(classSymbol, DeepCloneMethodName);
        bool hasShallowClone = shallowCloneableInterface is not null && HasMethodImplementation(classSymbol, ShallowCloneMethodName);

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
        var namespaceDecl = classInfo.Namespace is not null ? $"namespace {classInfo.Namespace};" : "";
        var deepCloneMethod = classInfo.ShouldGenerateDeepClone ? GenerateDeepCloneMethod(classInfo) : "";
        var shallowCloneMethod = classInfo.ShouldGenerateShallowClone ? GenerateShallowCloneMethod(classInfo) : "";

        return $$"""
            using System.Linq;

            {{namespaceDecl}}

            partial class {{classInfo.ClassName}}
            {
            {{deepCloneMethod}}{{shallowCloneMethod}}
            }
            """;
    }

    private static string GenerateDeepCloneMethod(ClassInfo classInfo)
    {
        var properties = GetCloneableProperties(classInfo.ClassSymbol);
        var propertyAssignments = string.Join(",\n", properties.Select(p => 
            $"        {p.Name} = {GenerateDeepCloneExpression(p)}"));

        return $$"""
                public {{classInfo.ClassName}} {{DeepCloneMethodName}}()
                {
                    return new {{classInfo.ClassName}}
                    {
            {{propertyAssignments}}
                    };
                }

            """;
    }

    private static string GenerateShallowCloneMethod(ClassInfo classInfo)
    {
        var properties = GetCloneableProperties(classInfo.ClassSymbol);
        var propertyAssignments = string.Join(",\n", properties.Select(p => 
            $"        {p.Name} = this.{p.Name}"));

        return $$"""
                public {{classInfo.ClassName}} {{ShallowCloneMethodName}}()
                {
                    return new {{classInfo.ClassName}}
                    {
            {{propertyAssignments}}
                    };
                }

            """;
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
                return $"this.{property.Name}?.{DeepCloneMethodName}()";
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
        // Check if type implements IEnumerable<T>
        return type.AllInterfaces.Any(i => 
            i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
    }

    private static string GenerateCollectionDeepClone(IPropertySymbol property, INamedTypeSymbol collectionType)
    {
        if (collectionType.TypeArguments.Length == 0)
            return $"this.{property.Name}";

        var elementType = collectionType.TypeArguments[0];

        // Check if element type implements IDeepCloneable
        if (elementType is INamedTypeSymbol elementNamedType)
        {
            var deepCloneableInterface = elementNamedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("ICloneableGenerator.IDeepCloneable"));

            if (deepCloneableInterface is not null)
            {
                return $"this.{property.Name}?.Select(x => x.{DeepCloneMethodName}()).ToList()";
            }
        }

        // For value types or types without IDeepCloneable, create a new list with existing items
        return $"this.{property.Name} != null ? new System.Collections.Generic.List<{elementType.ToDisplayString()}>(this.{property.Name}) : null";
    }

    private static List<IPropertySymbol> GetCloneableProperties(INamedTypeSymbol classSymbol)
    {
        var properties = new List<IPropertySymbol>();

        // Get all properties from the entire inheritance chain
        var currentType = classSymbol;
        while (currentType is not null)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member is IPropertySymbol property &&
                    !property.IsStatic &&
                    !property.IsReadOnly &&
                    property.SetMethod is not null &&
                    property.SetMethod.DeclaredAccessibility == Accessibility.Public &&
                    property.GetMethod is not null &&
                    !properties.Any(p => p.Name == property.Name)) // Avoid duplicates
                {
                    properties.Add(property);
                }
            }
            currentType = currentType.BaseType;
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

