using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace IDeepCloneable.Generator;

[Generator]
public class CloneableGenerator : IIncrementalGenerator
{
    private const string DeepCloneMethodName = "DeepClone";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all partial types that implement IDeepCloneable<T>
        var classDeclarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateType(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx)
            )
            .Where(static m => m is not null);

        context.RegisterSourceOutput(
            classDeclarations,
            static (spc, source) => Execute(source!, spc)
        );
    }

    private static bool IsCandidateType(SyntaxNode node)
    {
        return (
                node is ClassDeclarationSyntax classDeclaration
                && classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            )
            || (
                node is RecordDeclarationSyntax recordDeclaration
                && recordDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            )
            || (
                node is StructDeclarationSyntax structDeclaration
                && structDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            );
    }

    private static ClassInfo? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var typeDeclaration = context.Node as TypeDeclarationSyntax;
        if (typeDeclaration is null)
            return null;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

        if (classSymbol is null || classSymbol.IsAbstract)
            return null;

        var deepCloneableInterface = FindCloneableInterface(
            classSymbol,
            "IDeepCloneable.IDeepCloneable"
        );

        if (deepCloneableInterface is null)
            return null;

        bool hasDeepClone = HasMethodImplementation(classSymbol, DeepCloneMethodName);
        if (hasDeepClone)
            return null;

        string typeKeyword;
        if (classSymbol.IsRecord)
        {
            typeKeyword = classSymbol.IsValueType ? "record struct" : "record";
        }
        else
        {
            typeKeyword = classSymbol.IsValueType ? "struct" : "class";
        }

        return new ClassInfo(
            classSymbol.Name,
            GetNamespace(classSymbol),
            classSymbol,
            true,
            typeKeyword
        );
    }

    private static INamedTypeSymbol? FindCloneableInterface(
        INamedTypeSymbol classSymbol,
        string interfaceName
    )
    {
        // Check if any interface is IDeepCloneable<T>
        return classSymbol.AllInterfaces.FirstOrDefault(i =>
        {
            if (i.OriginalDefinition.Name != "IDeepCloneable")
                return false;
            
            var ns = i.OriginalDefinition.ContainingNamespace;
            if (ns == null)
                return false;
                
            return ns.ToDisplayString() == "IDeepCloneable";
        });
    }

    private static bool HasMethodImplementation(INamedTypeSymbol classSymbol, string methodName)
    {
        return classSymbol
            .GetMembers(methodName)
            .OfType<IMethodSymbol>()
            .Any(m => !m.IsAbstract && m.DeclaringSyntaxReferences.Any());
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
        var deepCloneMethod = classInfo.ShouldGenerateDeepClone
            ? GenerateDeepCloneMethod(classInfo)
            : string.Empty;

        if (classInfo.Namespace is not null)
        {
            // Use block-scoped namespace to avoid .NET 10 issues with file-scoped namespaces
            return $$"""
                using System.Linq;
                using System.Collections.Immutable;

                namespace {{classInfo.Namespace}}
                {
                    partial {{classInfo.TypeKeyword}} {{classInfo.ClassName}}
                    {
                    {{deepCloneMethod}}
                    }
                }
                """;
        }
        else
        {
            return $$"""
                using System.Linq;
                using System.Collections.Immutable;

                partial {{classInfo.TypeKeyword}} {{classInfo.ClassName}}
                {
                {{deepCloneMethod}}
                }
                """;
        }
    }

    private static string GenerateDeepCloneMethod(ClassInfo classInfo)
    {
        var properties = GetCloneableProperties(classInfo.ClassSymbol);

        bool hasInitOnlyProperties = properties.Any(p => p.SetMethod?.IsInitOnly == true);
        bool needsStatements = properties.Any(p =>
            p.Type is IArrayTypeSymbol arrayType && arrayType.Rank > 1
        );

        if (needsStatements || hasInitOnlyProperties)
        {
            if (classInfo.ClassSymbol.IsRecord && hasInitOnlyProperties)
            {
                var assignments = new List<string>();
                foreach (var property in properties)
                {
                    var expression = GenerateDeepCloneExpression(property);
                    assignments.Add($"            {property.Name} = {expression}");
                }

                var withAssignments = string.Join(",\n", assignments);

                return $@"    public {classInfo.ClassName} {DeepCloneMethodName}()
    {{
        return this with
        {{
{withAssignments}
        }};
    }}

";
            }
            else
            {
                var statements = new List<string>();
                statements.Add($"        var clone = new {classInfo.ClassName}();");

                foreach (var property in properties)
                {
                    var expression = GenerateDeepCloneExpression(property);
                    statements.Add($"        clone.{property.Name} = {expression};");
                }

                statements.Add("        return clone;");

                var methodBody = string.Join("\n", statements);

                return $@"    public {classInfo.ClassName} {DeepCloneMethodName}()
    {{
{methodBody}
    }}

";
            }
        }
        else
        {
            var propertyAssignments = string.Join(
                ",\n",
                properties.Select(p => $"        {p.Name} = {GenerateDeepCloneExpression(p)}")
            );

            return $$"""
                    public {classInfo.ClassName} {DeepCloneMethodName}()
                    {
                        return new {classInfo.ClassName}
                        {
                {{propertyAssignments}}
                        };
                    }

                """;
        }
    }

    private static string GenerateDeepCloneExpression(IPropertySymbol property)
    {
        var typeSymbol = property.Type;

        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return GenerateArrayDeepClone(property, arrayType);
        }

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            var deepCloneableInterface = namedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("IDeepCloneable.IDeepCloneable")
            );

            if (deepCloneableInterface is not null)
            {
                return $"this.{property.Name}?.{DeepCloneMethodName}()";
            }

            if (IsDictionaryType(namedType))
            {
                return GenerateDictionaryDeepClone(property, namedType);
            }

            if (IsCollectionType(namedType))
            {
                return GenerateCollectionDeepClone(property, namedType);
            }
        }

        if (typeSymbol.IsValueType || typeSymbol.SpecialType == SpecialType.System_String)
        {
            return $"this.{property.Name}";
        }

        return $"this.{property.Name}";
    }

    private static bool IsDictionaryType(INamedTypeSymbol type)
    {
        return type.AllInterfaces.Any(i =>
            i.OriginalDefinition.ToDisplayString()
            == "System.Collections.Generic.IDictionary<TKey, TValue>"
        );
    }

    private static bool IsCollectionType(INamedTypeSymbol type)
    {
        return type.AllInterfaces.Any(i =>
            i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>"
        );
    }

    private static string GenerateArrayDeepClone(
        IPropertySymbol property,
        IArrayTypeSymbol arrayType
    )
    {
        var elementType = arrayType.ElementType;
        var propertyName = property.Name;

        if (arrayType.Rank > 1)
        {
            var elementTypeName = elementType.ToDisplayString();
            var rankCommas = new string(',', arrayType.Rank - 1);
            var arrayTypeName = $"{elementTypeName}[{rankCommas}]";
            return $"this.{propertyName} != null ? ({arrayTypeName})this.{propertyName}.Clone() : null";
        }

        if (elementType is INamedTypeSymbol elementNamedType)
        {
            var deepCloneableInterface = elementNamedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("IDeepCloneable.IDeepCloneable")
            );

            if (deepCloneableInterface is not null)
            {
                return $"this.{propertyName}?.Select(x => x?.{DeepCloneMethodName}()).ToArray()";
            }
        }

        if (elementType.IsValueType || elementType.SpecialType == SpecialType.System_String)
        {
            var arrayTypeName = $"{elementType.ToDisplayString()}[]";
            return $"this.{propertyName} != null ? ({arrayTypeName})this.{propertyName}.Clone() : null";
        }

        var refArrayTypeName = $"{elementType.ToDisplayString()}[]";
        return $"this.{propertyName} != null ? ({refArrayTypeName})this.{propertyName}.Clone() : null";
    }

    private static string GenerateDictionaryDeepClone(
        IPropertySymbol property,
        INamedTypeSymbol dictionaryType
    )
    {
        if (dictionaryType.TypeArguments.Length < 2)
            return $"this.{property.Name}";

        var keyType = dictionaryType.TypeArguments[0];
        var valueType = dictionaryType.TypeArguments[1];
        var propertyName = property.Name;
        var typeName = dictionaryType.OriginalDefinition.ToDisplayString();

        bool valueIsCloneable = false;
        if (valueType is INamedTypeSymbol valueNamedType)
        {
            var deepCloneableInterface = valueNamedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("IDeepCloneable.IDeepCloneable")
            );
            valueIsCloneable = deepCloneableInterface is not null;
        }

        if (typeName.StartsWith("System.Collections.Immutable.ImmutableDictionary<"))
        {
            if (valueIsCloneable)
            {
                return $"this.{propertyName}?.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value?.{DeepCloneMethodName}())";
            }
            return $"this.{propertyName}";
        }

        if (typeName.StartsWith("System.Collections.ObjectModel.ReadOnlyDictionary<"))
        {
            if (valueIsCloneable)
            {
                return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ReadOnlyDictionary<{keyType.ToDisplayString()}, {valueType.ToDisplayString()}>(this.{propertyName}.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.{DeepCloneMethodName}())) : null";
            }
            return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ReadOnlyDictionary<{keyType.ToDisplayString()}, {valueType.ToDisplayString()}>(new System.Collections.Generic.Dictionary<{keyType.ToDisplayString()}, {valueType.ToDisplayString()}>(this.{propertyName})) : null";
        }

        if (valueIsCloneable)
        {
            return $"this.{propertyName}?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.{DeepCloneMethodName}())";
        }

        return $"this.{propertyName} != null ? new System.Collections.Generic.Dictionary<{keyType.ToDisplayString()}, {valueType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static string GenerateCollectionDeepClone(
        IPropertySymbol property,
        INamedTypeSymbol collectionType
    )
    {
        if (collectionType.TypeArguments.Length == 0)
            return $"this.{property.Name}";

        var elementType = collectionType.TypeArguments[0];
        var propertyName = property.Name;
        var typeName = collectionType.OriginalDefinition.ToDisplayString();

        bool isCloneable = false;
        if (elementType is INamedTypeSymbol elementNamedType)
        {
            var deepCloneableInterface = elementNamedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("IDeepCloneable.IDeepCloneable")
            );
            isCloneable = deepCloneableInterface is not null;
        }

        if (typeName == "System.Collections.Generic.Stack<T>")
            return GenerateStackClone(propertyName, elementType, isCloneable);

        if (typeName == "System.Collections.Generic.Queue<T>")
            return GenerateQueueClone(propertyName, elementType, isCloneable);

        if (typeName == "System.Collections.Generic.HashSet<T>")
            return GenerateHashSetClone(propertyName, elementType, isCloneable);

        if (typeName == "System.Collections.Generic.SortedSet<T>")
            return GenerateSortedSetClone(propertyName, elementType, isCloneable);

        if (typeName == "System.Collections.ObjectModel.ObservableCollection<T>")
            return GenerateObservableCollectionClone(propertyName, elementType, isCloneable);

        if (typeName == "System.Collections.ObjectModel.ReadOnlyCollection<T>")
            return GenerateReadOnlyCollectionClone(propertyName, elementType, isCloneable);

        if (typeName.StartsWith("System.Collections.Immutable.ImmutableList<"))
            return GenerateImmutableListClone(propertyName, elementType, isCloneable);

        if (typeName.StartsWith("System.Collections.Immutable.ImmutableArray<"))
            return GenerateImmutableArrayClone(propertyName, elementType, isCloneable);

        if (typeName.StartsWith("System.Collections.Immutable.ImmutableHashSet<"))
            return GenerateImmutableHashSetClone(propertyName, elementType, isCloneable);

        if (typeName.StartsWith("System.Collections.Immutable.ImmutableQueue<"))
            return GenerateImmutableQueueClone(propertyName, elementType, isCloneable);

        if (typeName.StartsWith("System.Collections.Immutable.ImmutableStack<"))
            return GenerateImmutableStackClone(propertyName, elementType, isCloneable);

        return GenerateDefaultListClone(propertyName, elementType, isCloneable);
    }

    private static string GenerateStackClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.Generic.Stack<{elementType.ToDisplayString()}>(this.{propertyName}.Reverse().Select(x => x?.{DeepCloneMethodName}())) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.Generic.Stack<{elementType.ToDisplayString()}>(this.{propertyName}.Reverse()) : null";
    }

    private static string GenerateQueueClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.Generic.Queue<{elementType.ToDisplayString()}>(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}())) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.Generic.Queue<{elementType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static string GenerateHashSetClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.Generic.HashSet<{elementType.ToDisplayString()}>(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}())) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.Generic.HashSet<{elementType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static string GenerateSortedSetClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.Generic.SortedSet<{elementType.ToDisplayString()}>(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}())) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.Generic.SortedSet<{elementType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static string GenerateObservableCollectionClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ObservableCollection<{elementType.ToDisplayString()}>(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}())) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ObservableCollection<{elementType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static string GenerateReadOnlyCollectionClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ReadOnlyCollection<{elementType.ToDisplayString()}>(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}()).ToList()) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ReadOnlyCollection<{elementType.ToDisplayString()}>(this.{propertyName}.ToList()) : null";
    }

    private static string GenerateImmutableListClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName}?.Select(x => x?.{DeepCloneMethodName}()).ToImmutableList()";
        }
        return $"this.{propertyName}?.ToImmutableList()";
    }

    private static string GenerateImmutableArrayClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName}.IsDefault ? default : this.{propertyName}.Select(x => x?.{DeepCloneMethodName}()).ToImmutableArray()";
        }
        return $"this.{propertyName}.IsDefault ? default : this.{propertyName}.ToImmutableArray()";
    }

    private static string GenerateImmutableHashSetClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName}?.Select(x => x?.{DeepCloneMethodName}()).ToImmutableHashSet()";
        }
        return $"this.{propertyName}?.ToImmutableHashSet()";
    }

    private static string GenerateImmutableQueueClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName} == null ? System.Collections.Immutable.ImmutableQueue<{elementType.ToDisplayString()}>.Empty : System.Collections.Immutable.ImmutableQueue.CreateRange(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}()))";
        }
        return $"this.{propertyName} == null ? System.Collections.Immutable.ImmutableQueue<{elementType.ToDisplayString()}>.Empty : System.Collections.Immutable.ImmutableQueue.CreateRange(this.{propertyName})";
    }

    private static string GenerateImmutableStackClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName} == null ? System.Collections.Immutable.ImmutableStack<{elementType.ToDisplayString()}>.Empty : System.Collections.Immutable.ImmutableStack.CreateRange(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}()))";
        }
        return $"this.{propertyName} == null ? System.Collections.Immutable.ImmutableStack<{elementType.ToDisplayString()}>.Empty : System.Collections.Immutable.ImmutableStack.CreateRange(this.{propertyName})";
    }

    private static string GenerateDefaultListClone(
        string propertyName,
        ITypeSymbol elementType,
        bool isCloneable
    )
    {
        if (isCloneable)
        {
            return $"this.{propertyName}?.Select(x => x?.{DeepCloneMethodName}()).ToList()";
        }
        return $"this.{propertyName} != null ? new System.Collections.Generic.List<{elementType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static List<IPropertySymbol> GetCloneableProperties(INamedTypeSymbol classSymbol)
    {
        var properties = new List<IPropertySymbol>();

        var currentType = classSymbol;
        while (currentType is not null)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (
                    member is IPropertySymbol property
                    && !property.IsStatic
                    && property.GetMethod is not null
                    && !properties.Any(p => p.Name == property.Name)
                )
                {
                    bool hasPublicSetter =
                        property.SetMethod is not null
                        && property.SetMethod.DeclaredAccessibility == Accessibility.Public;

                    bool hasPublicInit =
                        property.SetMethod is not null
                        && property.SetMethod.IsInitOnly
                        && property.SetMethod.DeclaredAccessibility == Accessibility.Public;

                    if (hasPublicSetter || hasPublicInit)
                    {
                        properties.Add(property);
                    }
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
        string TypeKeyword
    );
}
