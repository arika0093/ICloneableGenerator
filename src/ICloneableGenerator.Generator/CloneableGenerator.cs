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
                predicate: static (s, _) => IsCandidateType(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Generate the clone methods
        context.RegisterSourceOutput(classDeclarations, static (spc, source) => Execute(source!, spc));
    }

    private static bool IsCandidateType(SyntaxNode node)
    {
        return (node is ClassDeclarationSyntax classDeclaration &&
                classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)) ||
               (node is RecordDeclarationSyntax recordDeclaration &&
                recordDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)) ||
               (node is StructDeclarationSyntax structDeclaration &&
                structDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static ClassInfo? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var typeDeclaration = context.Node as TypeDeclarationSyntax;
        if (typeDeclaration is null)
            return null;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

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

        // Determine type keyword
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
            deepCloneableInterface is not null && !hasDeepClone,
            shallowCloneableInterface is not null && !hasShallowClone,
            typeKeyword
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
            using System.Collections.Immutable;

            {{namespaceDecl}}

            partial {{classInfo.TypeKeyword}} {{classInfo.ClassName}}
            {
            {{deepCloneMethod}}{{shallowCloneMethod}}
            }
            """;
    }

    private static string GenerateDeepCloneMethod(ClassInfo classInfo)
    {
        var properties = GetCloneableProperties(classInfo.ClassSymbol);
        
        // Check if we need special handling for init-only properties
        bool hasInitOnlyProperties = properties.Any(p => p.SetMethod?.IsInitOnly == true);
        
        // Check if any property needs special handling (requires statements before initialization)
        var needsStatements = properties.Any(p => p.Type is IArrayTypeSymbol arrayType && arrayType.Rank > 1);
        
        if (needsStatements || hasInitOnlyProperties)
        {
            // For types with init-only properties or complex arrays, we need special construction
            // For records and classes with init-only properties, use the with expression approach
            if (classInfo.ClassSymbol.IsRecord && hasInitOnlyProperties)
            {
                // Use record's with-expression for cloning
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
                // Generate method with statements for complex cases
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
            // Use object initializer for simple cases
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

        // Handle arrays
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return GenerateArrayDeepClone(property, arrayType);
        }

        // Check if the type implements IDeepCloneable<T>
        if (typeSymbol is INamedTypeSymbol namedType)
        {
            var deepCloneableInterface = namedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("ICloneableGenerator.IDeepCloneable"));

            if (deepCloneableInterface is not null)
            {
                return $"this.{property.Name}?.{DeepCloneMethodName}()";
            }

            // Handle dictionaries
            if (IsDictionaryType(namedType))
            {
                return GenerateDictionaryDeepClone(property, namedType);
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

    private static bool IsDictionaryType(INamedTypeSymbol type)
    {
        // Check if type implements IDictionary<TKey, TValue>
        // This covers Dictionary, ImmutableDictionary, ReadOnlyDictionary, and any custom implementations
        return type.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IDictionary<TKey, TValue>");
    }

    private static bool IsCollectionType(INamedTypeSymbol type)
    {
        // Check if type implements IEnumerable<T>
        return type.AllInterfaces.Any(i => 
            i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
    }

    private static string GenerateArrayDeepClone(IPropertySymbol property, IArrayTypeSymbol arrayType)
    {
        var elementType = arrayType.ElementType;
        var propertyName = property.Name;
        
        // For multi-dimensional arrays, we need special handling
        if (arrayType.Rank > 1)
        {
            // Multi-dimensional array - use Array.Clone() cast to the correct type
            // Build type string without using ToDisplayString() which includes brackets
            var elementTypeName = elementType.ToDisplayString();
            var rankCommas = new string(',', arrayType.Rank - 1);
            var arrayTypeName = $"{elementTypeName}[{rankCommas}]";
            return $"this.{propertyName} != null ? ({arrayTypeName})this.{propertyName}.Clone() : null";
        }

        // Single-dimensional array
        // Check if element type implements IDeepCloneable
        if (elementType is INamedTypeSymbol elementNamedType)
        {
            var deepCloneableInterface = elementNamedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("ICloneableGenerator.IDeepCloneable"));

            if (deepCloneableInterface is not null)
            {
                return $"this.{propertyName}?.Select(x => x?.{DeepCloneMethodName}()).ToArray()";
            }
        }

        // For value types or types without IDeepCloneable, clone the array
        if (elementType.IsValueType || elementType.SpecialType == SpecialType.System_String)
        {
            var arrayTypeName = $"{elementType.ToDisplayString()}[]";
            return $"this.{propertyName} != null ? ({arrayTypeName})this.{propertyName}.Clone() : null";
        }

        // For reference types, create a new array with the same elements (shallow copy of elements)
        var refArrayTypeName = $"{elementType.ToDisplayString()}[]";
        return $"this.{propertyName} != null ? ({refArrayTypeName})this.{propertyName}.Clone() : null";
    }

    private static string GenerateDictionaryDeepClone(IPropertySymbol property, INamedTypeSymbol dictionaryType)
    {
        if (dictionaryType.TypeArguments.Length < 2)
            return $"this.{property.Name}";

        var keyType = dictionaryType.TypeArguments[0];
        var valueType = dictionaryType.TypeArguments[1];
        var propertyName = property.Name;
        var typeName = dictionaryType.OriginalDefinition.ToDisplayString();

        // Check if value type implements IDeepCloneable
        bool valueIsCloneable = false;
        if (valueType is INamedTypeSymbol valueNamedType)
        {
            var deepCloneableInterface = valueNamedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("ICloneableGenerator.IDeepCloneable"));
            valueIsCloneable = deepCloneableInterface is not null;
        }

        // Handle ImmutableDictionary
        if (typeName.StartsWith("System.Collections.Immutable.ImmutableDictionary<"))
        {
            if (valueIsCloneable)
            {
                return $"this.{propertyName}?.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value?.{DeepCloneMethodName}())";
            }
            return $"this.{propertyName}";
        }

        // Handle ReadOnlyDictionary
        if (typeName.StartsWith("System.Collections.ObjectModel.ReadOnlyDictionary<"))
        {
            if (valueIsCloneable)
            {
                return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ReadOnlyDictionary<{keyType.ToDisplayString()}, {valueType.ToDisplayString()}>(this.{propertyName}.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.{DeepCloneMethodName}())) : null";
            }
            return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ReadOnlyDictionary<{keyType.ToDisplayString()}, {valueType.ToDisplayString()}>(new System.Collections.Generic.Dictionary<{keyType.ToDisplayString()}, {valueType.ToDisplayString()}>(this.{propertyName})) : null";
        }

        // Handle regular Dictionary
        if (valueIsCloneable)
        {
            return $"this.{propertyName}?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.{DeepCloneMethodName}())";
        }

        // For value types or types without IDeepCloneable, create a new dictionary with existing items
        return $"this.{propertyName} != null ? new System.Collections.Generic.Dictionary<{keyType.ToDisplayString()}, {valueType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static string GenerateCollectionDeepClone(IPropertySymbol property, INamedTypeSymbol collectionType)
    {
        if (collectionType.TypeArguments.Length == 0)
            return $"this.{property.Name}";

        var elementType = collectionType.TypeArguments[0];
        var propertyName = property.Name;
        var typeName = collectionType.OriginalDefinition.ToDisplayString();

        // Check if element type implements IDeepCloneable
        bool isCloneable = false;
        if (elementType is INamedTypeSymbol elementNamedType)
        {
            var deepCloneableInterface = elementNamedType.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString().StartsWith("ICloneableGenerator.IDeepCloneable"));
            isCloneable = deepCloneableInterface is not null;
        }

        // Handle specific collection types
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

        // Default: List<T> or IEnumerable<T>
        return GenerateDefaultListClone(propertyName, elementType, isCloneable);
    }

    private static string GenerateStackClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.Generic.Stack<{elementType.ToDisplayString()}>(this.{propertyName}.Reverse().Select(x => x?.{DeepCloneMethodName}())) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.Generic.Stack<{elementType.ToDisplayString()}>(this.{propertyName}.Reverse()) : null";
    }

    private static string GenerateQueueClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.Generic.Queue<{elementType.ToDisplayString()}>(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}())) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.Generic.Queue<{elementType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static string GenerateHashSetClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.Generic.HashSet<{elementType.ToDisplayString()}>(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}())) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.Generic.HashSet<{elementType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static string GenerateSortedSetClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.Generic.SortedSet<{elementType.ToDisplayString()}>(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}())) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.Generic.SortedSet<{elementType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static string GenerateObservableCollectionClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ObservableCollection<{elementType.ToDisplayString()}>(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}())) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ObservableCollection<{elementType.ToDisplayString()}>(this.{propertyName}) : null";
    }

    private static string GenerateReadOnlyCollectionClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ReadOnlyCollection<{elementType.ToDisplayString()}>(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}()).ToList()) : null";
        }
        return $"this.{propertyName} != null ? new System.Collections.ObjectModel.ReadOnlyCollection<{elementType.ToDisplayString()}>(this.{propertyName}.ToList()) : null";
    }

    private static string GenerateImmutableListClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName}?.Select(x => x?.{DeepCloneMethodName}()).ToImmutableList()";
        }
        return $"this.{propertyName}?.ToImmutableList()";
    }

    private static string GenerateImmutableArrayClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName}.IsDefault ? default : this.{propertyName}.Select(x => x?.{DeepCloneMethodName}()).ToImmutableArray()";
        }
        return $"this.{propertyName}.IsDefault ? default : this.{propertyName}.ToImmutableArray()";
    }

    private static string GenerateImmutableHashSetClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName}?.Select(x => x?.{DeepCloneMethodName}()).ToImmutableHashSet()";
        }
        return $"this.{propertyName}?.ToImmutableHashSet()";
    }

    private static string GenerateImmutableQueueClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName} == null ? System.Collections.Immutable.ImmutableQueue<{elementType.ToDisplayString()}>.Empty : System.Collections.Immutable.ImmutableQueue.CreateRange(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}()))";
        }
        return $"this.{propertyName} == null ? System.Collections.Immutable.ImmutableQueue<{elementType.ToDisplayString()}>.Empty : System.Collections.Immutable.ImmutableQueue.CreateRange(this.{propertyName})";
    }

    private static string GenerateImmutableStackClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName} == null ? System.Collections.Immutable.ImmutableStack<{elementType.ToDisplayString()}>.Empty : System.Collections.Immutable.ImmutableStack.CreateRange(this.{propertyName}.Select(x => x?.{DeepCloneMethodName}()))";
        }
        return $"this.{propertyName} == null ? System.Collections.Immutable.ImmutableStack<{elementType.ToDisplayString()}>.Empty : System.Collections.Immutable.ImmutableStack.CreateRange(this.{propertyName})";
    }

    private static string GenerateDefaultListClone(string propertyName, ITypeSymbol elementType, bool isCloneable)
    {
        if (isCloneable)
        {
            return $"this.{propertyName}?.Select(x => x?.{DeepCloneMethodName}()).ToList()";
        }
        // For value types or types without IDeepCloneable, create a new list with existing items
        return $"this.{propertyName} != null ? new System.Collections.Generic.List<{elementType.ToDisplayString()}>(this.{propertyName}) : null";
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
                    property.GetMethod is not null &&
                    !properties.Any(p => p.Name == property.Name)) // Avoid duplicates
                {
                    // Include properties with:
                    // 1. Public setter
                    // 2. Public init accessor
                    bool hasPublicSetter = property.SetMethod is not null && 
                                          property.SetMethod.DeclaredAccessibility == Accessibility.Public;
                    
                    bool hasPublicInit = property.SetMethod is not null && 
                                        property.SetMethod.IsInitOnly &&
                                        property.SetMethod.DeclaredAccessibility == Accessibility.Public;

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
        bool ShouldGenerateShallowClone,
        string TypeKeyword  // "class", "record", "struct", or "record struct"
    );
}

