﻿using Microsoft.CodeAnalysis;
using TomLonghurst.Events.NotifyValueChanged.SourceGeneration;
using TomLonghurst.Events.NotifyValueChanged.SourceGeneration.Implementation;

namespace TomLonghurst.Events.NotifyValueChanged.Extensions;

internal static class SymbolExtensions
{
    public static string GetFullyQualifiedType(this ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormats.NamespaceAndType);
    }
    
    public static string GetSimpleTypeName(this ITypeSymbol type)
    {
        var simpleFieldName = GetFullyQualifiedType(type).Split('.').Last();

        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            simpleFieldName = $"Nullable{simpleFieldName}".Replace("?", string.Empty);
        }

        var typeArguments = GetGenericTypeArguments(type).ToList();

        if (!typeArguments.Any())
        {
            return simpleFieldName;   
        }

        if (simpleFieldName.Contains('<') && simpleFieldName.Contains('>'))
        {
            var firstDiamondBracketIndex = simpleFieldName.IndexOf('<');
            var lastDiamondBracketIndex = simpleFieldName.LastIndexOf('>');

            return simpleFieldName.Replace(simpleFieldName.Substring(firstDiamondBracketIndex, lastDiamondBracketIndex - firstDiamondBracketIndex+1), string.Join("", typeArguments));
        }

        return simpleFieldName;
    }

    public static ITypeSymbol GetSymbolType(this ISymbol symbol)
    {
        return symbol switch
        {
            IPropertySymbol propertySymbol => propertySymbol.Type,
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            _ => throw new ArgumentException($"{symbol} is neither a Field or a Property", nameof(symbol))
        };
    }

    public static AttributeData? GetNotifyValueChangeAttribute(this ISymbol symbol)
    {
        return symbol.GetAttributes().FirstOrDefault(x => x.AttributeClass.ToDisplayString(SymbolDisplayFormats.NamespaceAndType) == typeof(NotifyValueChangeAttribute).FullName);
    }

    private static IEnumerable<string> GetGenericTypeArguments(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedTypeSymbol || !namedTypeSymbol.TypeArguments.Any())
        {
            yield break;
        }
        
        foreach (var typeArgument in namedTypeSymbol.TypeArguments)
        {
            yield return GetSimpleTypeName(typeArgument);
        }
    }
}