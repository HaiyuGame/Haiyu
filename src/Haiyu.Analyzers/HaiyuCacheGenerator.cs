using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Haiyu.Analyzers;

[Generator]
public sealed class HaiyuCacheGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "Cacheing.Contracts.HaiyuCacheAttribute";
    private const string OwnerMetadataName = "Cacheing.Contracts.IHaiyuCacheOwner";

    private static readonly DiagnosticDescriptor ContainingTypeMustBePartial = new(
        "HYC001", "Cache owner must be partial",
        "Type '{0}' must be partial to use HaiyuCacheAttribute", "HaiyuCache",
        DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor TargetNameRequired = new(
        "HYC003", "TargetName is required",
        "Property '{0}' must specify a non-empty TargetName", "HaiyuCache",
        DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidExpiration = new(
        "HYC004", "ExpirationSeconds is invalid",
        "Property '{0}' must specify an ExpirationSeconds value greater than zero", "HaiyuCache",
        DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor OwnerInterfaceRequired = new(
        "HYC005", "Cache owner interface is required",
        "Type '{0}' must implement IHaiyuCacheOwner", "HaiyuCache",
        DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor UnsupportedProperty = new(
        "HYC006", "Cache property is not supported",
        "Property '{0}' must be a non-static, non-indexed partial property with get and set accessors",
        "HaiyuCache", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor GeneratedMemberConflict = new(
        "HYC007", "Generated cache member conflicts with an existing member",
        "Cannot generate cache helpers for '{0}' because member '{1}' already exists",
        "HaiyuCache", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var properties = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeMetadataName,
            static (node, _) => node is PropertyDeclarationSyntax,
            static (ctx, ct) => CreateCandidate(ctx, ct));

        context.RegisterSourceOutput(properties, static (spc, candidate) => Emit(spc, candidate));
    }

    private static Candidate CreateCandidate(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        var syntax = (PropertyDeclarationSyntax)context.TargetNode;
        var property = (IPropertySymbol)context.TargetSymbol;
        var containingType = property.ContainingType;
        var attribute = context.Attributes[0];
        var diagnostics = new List<Diagnostic>();
        var location = syntax.Identifier.GetLocation();

        if (!IsPartial(containingType))
            diagnostics.Add(Diagnostic.Create(ContainingTypeMustBePartial, location, containingType.Name));

        if (property.IsStatic || property.IsIndexer || property.GetMethod is null || property.SetMethod is null
            || property.SetMethod.IsInitOnly || property.RefKind != RefKind.None)
        {
            diagnostics.Add(Diagnostic.Create(UnsupportedProperty, location, property.Name));
        }

        var targetName = GetNamedString(attribute, "TargetName") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetName))
            diagnostics.Add(Diagnostic.Create(TargetNameRequired, location, property.Name));

        var expirationSeconds = GetNamedInt(attribute, "ExpirationSeconds", 300);
        if (expirationSeconds <= 0)
            diagnostics.Add(Diagnostic.Create(InvalidExpiration, location, property.Name));

        if (!Implements(containingType, OwnerMetadataName))
            diagnostics.Add(Diagnostic.Create(OwnerInterfaceRequired, location, containingType.Name));

        var key = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;
        if (string.IsNullOrWhiteSpace(key)) key = property.Name;

        foreach (var generatedName in GeneratedMemberNames(property.Name))
        {
            if (containingType.GetMembers(generatedName).Length > 0)
                diagnostics.Add(Diagnostic.Create(GeneratedMemberConflict, location, property.Name, generatedName));
        }

        var nullableTypeFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );

        return new Candidate(
            property,
            syntax,
            targetName,
            key!,
            expirationSeconds,
            property.Type.ToDisplayString(nullableTypeFormat),
            diagnostics.ToImmutableArray());
    }

    private static void Emit(SourceProductionContext context, Candidate candidate)
    {
        foreach (var diagnostic in candidate.Diagnostics)
            context.ReportDiagnostic(diagnostic);

        if (candidate.Diagnostics.Any(static x => x.Severity == DiagnosticSeverity.Error)) return;

        var source = GenerateSource(candidate);
        var hintName = GetHintName(candidate.Property);
        context.AddSource(hintName, source);
    }

    private static string GenerateSource(Candidate candidate)
    {
        var property = candidate.Property;
        var containingTypes = GetContainingTypes(property.ContainingType);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");

        var ns = property.ContainingNamespace.IsGlobalNamespace
            ? null
            : property.ContainingNamespace.ToDisplayString();
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        var indent = string.Empty;
        foreach (var type in containingTypes)
        {
            sb.Append(indent).Append(GetAccessibility(type.DeclaredAccessibility));
            if (type.IsStatic) sb.Append("static ");
            else
            {
                if (type.IsAbstract) sb.Append("abstract ");
                if (type.IsSealed) sb.Append("sealed ");
            }
            sb.Append("partial ").Append(GetTypeKind(type)).Append(' ').Append(type.Name);
            AppendTypeParameters(sb, type);
            sb.AppendLine();
            sb.Append(indent).AppendLine("{");
            indent += "    ";
        }

        var propertyName = property.Name;
        var typeName = candidate.TypeName;
        var target = Escape(candidate.TargetName);
        var key = Escape(candidate.Key);

        sb.Append(indent).Append("public global::System.Threading.Tasks.Task<").Append(typeName)
            .Append("> Get").Append(propertyName).AppendLine("Async(");
        sb.Append(indent).AppendLine("    string instanceKey,");
        sb.Append(indent).Append("    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task<")
            .Append(typeName).AppendLine(">> factory,");
        sb.Append(indent).AppendLine("    global::Cacheing.Contracts.HaiyuCacheMode mode = global::Cacheing.Contracts.HaiyuCacheMode.Default,");
        sb.Append(indent).AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).Append("    return CacheService.GetOrCreateAsync<").Append(typeName).AppendLine(">");
        sb.Append(indent).AppendLine("    (");
        sb.Append(indent).Append("        \"").Append(target).AppendLine("\",");
        sb.Append(indent).Append("        \"").Append(key).AppendLine("\",");
        sb.Append(indent).AppendLine("        instanceKey,");
        sb.Append(indent).Append("        global::System.TimeSpan.FromSeconds(").Append(candidate.ExpirationSeconds).AppendLine("),");
        sb.Append(indent).AppendLine("        factory,");
        sb.Append(indent).AppendLine("        mode,");
        sb.Append(indent).AppendLine("        cancellationToken");
        sb.Append(indent).AppendLine("    );");
        sb.Append(indent).AppendLine("}");
        sb.AppendLine();

        sb.Append(indent).Append("public async global::System.Threading.Tasks.Task<").Append(typeName)
            .Append("> Load").Append(propertyName).AppendLine("Async(");
        sb.Append(indent).AppendLine("    string instanceKey,");
        sb.Append(indent).Append("    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task<")
            .Append(typeName).AppendLine(">> factory,");
        sb.Append(indent).AppendLine("    global::Cacheing.Contracts.HaiyuCacheMode mode = global::Cacheing.Contracts.HaiyuCacheMode.Default,");
        sb.Append(indent).AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).Append("    var value = await Get").Append(propertyName)
            .AppendLine("Async(instanceKey, factory, mode, cancellationToken).ConfigureAwait(false);");
        sb.Append(indent).Append("    ").Append(propertyName).AppendLine(" = value;");
        sb.Append(indent).AppendLine("    return value;");
        sb.Append(indent).AppendLine("}");
        sb.AppendLine();

        sb.Append(indent).Append("public global::System.Threading.Tasks.Task<").Append(typeName)
            .Append("> Refresh").Append(propertyName).AppendLine("Async(");
        sb.Append(indent).AppendLine("    string instanceKey,");
        sb.Append(indent).Append("    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task<")
            .Append(typeName).AppendLine(">> factory,");
        sb.Append(indent).AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).Append("    return Load").Append(propertyName)
            .AppendLine("Async(instanceKey, factory, global::Cacheing.Contracts.HaiyuCacheMode.Refresh, cancellationToken);");
        sb.Append(indent).AppendLine("}");
        sb.AppendLine();

        sb.Append(indent).Append("public void Set").Append(propertyName).Append("Cache(string instanceKey, ")
            .Append(typeName).AppendLine(" value)");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).Append("    CacheService.Set(\"").Append(target).Append("\", \"").Append(key)
            .Append("\", instanceKey, value, global::System.TimeSpan.FromSeconds(")
            .Append(candidate.ExpirationSeconds).AppendLine("));");
        sb.Append(indent).AppendLine("}");
        sb.AppendLine();

        sb.Append(indent).Append("public bool Remove").Append(propertyName).AppendLine("Cache(string instanceKey)");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).Append("    return CacheService.Remove(\"").Append(target).Append("\", \"")
            .Append(key).AppendLine("\", instanceKey);");
        sb.Append(indent).AppendLine("}");
        sb.AppendLine();

        sb.Append(indent).Append("public bool Is").Append(propertyName).AppendLine("CacheExpired(string instanceKey)");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).Append("    return CacheService.IsExpired(\"").Append(target).Append("\", \"")
            .Append(key).AppendLine("\", instanceKey);");
        sb.Append(indent).AppendLine("}");

        for (var i = containingTypes.Count - 1; i >= 0; i--)
        {
            indent = indent.Substring(0, indent.Length - 4);
            sb.Append(indent).AppendLine("}");
        }

        return sb.ToString();
    }

    private static bool IsPartial(INamedTypeSymbol type)
    {
        return type.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is TypeDeclarationSyntax declaration
            && declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static bool Implements(INamedTypeSymbol type, string metadataName)
    {
        return type.AllInterfaces.Any(x => x.ToDisplayString() == metadataName);
    }

    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (var pair in attribute.NamedArguments)
            if (pair.Key == name) return pair.Value.Value as string;
        return null;
    }

    private static int GetNamedInt(AttributeData attribute, string name, int defaultValue)
    {
        foreach (var pair in attribute.NamedArguments)
            if (pair.Key == name && pair.Value.Value is int value) return value;
        return defaultValue;
    }

    private static IEnumerable<string> GeneratedMemberNames(string propertyName)
    {
        yield return "Get" + propertyName + "Async";
        yield return "Load" + propertyName + "Async";
        yield return "Refresh" + propertyName + "Async";
        yield return "Set" + propertyName + "Cache";
        yield return "Remove" + propertyName + "Cache";
        yield return "Is" + propertyName + "CacheExpired";
    }

    private static List<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol type)
    {
        var result = new List<INamedTypeSymbol>();
        for (var current = type; current is not null; current = current.ContainingType)
            result.Add(current);
        result.Reverse();
        return result;
    }

    private static void AppendTypeParameters(StringBuilder sb, INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0) return;
        sb.Append('<').Append(string.Join(", ", type.TypeParameters.Select(x => x.Name))).Append('>');
    }

    private static string GetAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public ",
            Accessibility.Internal => "internal ",
            Accessibility.Private => "private ",
            Accessibility.Protected => "protected ",
            Accessibility.ProtectedAndInternal => "private protected ",
            Accessibility.ProtectedOrInternal => "protected internal ",
            _ => string.Empty
        };
    }

    private static string GetTypeKind(INamedTypeSymbol type)
    {
        if (type.IsRecord) return type.TypeKind == TypeKind.Struct ? "record struct" : "record";
        return type.TypeKind == TypeKind.Struct ? "struct" : "class";
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string GetHintName(IPropertySymbol property)
    {
        var identity = property.ContainingType.ToDisplayString() + "." + property.Name;
        var safe = new string(identity.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return safe + ".HaiyuCache.g.cs";
    }

    private sealed class Candidate
    {
        public Candidate(IPropertySymbol property, PropertyDeclarationSyntax syntax, string targetName,
            string key, int expirationSeconds, string typeName, ImmutableArray<Diagnostic> diagnostics)
        {
            Property = property;
            Syntax = syntax;
            TargetName = targetName;
            Key = key;
            ExpirationSeconds = expirationSeconds;
            TypeName = typeName;
            Diagnostics = diagnostics;
        }

        public IPropertySymbol Property { get; }
        public PropertyDeclarationSyntax Syntax { get; }
        public string TargetName { get; }
        public string Key { get; }
        public int ExpirationSeconds { get; }
        public string TypeName { get; }
        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }
}
