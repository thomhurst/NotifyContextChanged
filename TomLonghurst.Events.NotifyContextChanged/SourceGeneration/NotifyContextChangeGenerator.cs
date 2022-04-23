﻿using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TomLonghurst.Events.NotifyContextChanged.SourceGeneration;

[Generator]
public class NotifyContextChangeGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new FieldSyntaxReciever());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxContextReceiver is not FieldSyntaxReciever syntaxReciever)
        {
            return;
        }
        
        foreach (var containingClassGroup in syntaxReciever.IdentifiedFields.GroupBy(x => x.ContainingType)) 
        {
            var containingClass = containingClassGroup.Key;
            var namespaceSymbol = containingClass.ContainingNamespace;
            var source = GenerateClass(context, containingClass, namespaceSymbol, containingClassGroup.ToList());
            context.AddSource($"{containingClass.Name}_NotifyContextChanged.generated", SourceText.From(source, Encoding.UTF8));
        }
    }
    
    private string GenerateClass(GeneratorExecutionContext context, INamedTypeSymbol @class, INamespaceSymbol @namespace, List<IFieldSymbol> fields) {
        var classBuilder = new StringBuilder();
        var notifyPropertyChangedSymbol = context.Compilation.GetTypeByMetadataName(typeof(INotifyContextChanged<>).FullName);
        var callerMemberSymbol = context.Compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.CallerMemberNameAttribute");
        
        classBuilder.AppendLine("using System;");
        classBuilder.AppendLine($"using {notifyPropertyChangedSymbol.ContainingNamespace};");
        classBuilder.AppendLine($"using {callerMemberSymbol.ContainingNamespace};");
        classBuilder.AppendLine($"namespace {@namespace.ToDisplayString()}");
        classBuilder.AppendLine("{");

        var interfacesWritten = WriteInterfaces(classBuilder, fields).ToList();
        
        classBuilder.AppendLine($"public partial class {@class.Name}");
        classBuilder.AppendLine(":");
        
        var commaSeparatedListOfInterfaces = interfacesWritten.Aggregate((a, x) => $"{a}, {x}");
        classBuilder.AppendLine(commaSeparatedListOfInterfaces); 
        classBuilder.AppendLine("{");

        foreach(var field in fields) {
            var fullyQualifiedFieldType = GetFullyQualifiedFieldType(field);
            var fieldName = field.Name;
            var propertyName = NormalizePropertyName(fieldName);
            classBuilder.AppendLine($"public {fullyQualifiedFieldType} {propertyName}");
            classBuilder.AppendLine("{");
            classBuilder.AppendLine($"get => {fieldName};");
            classBuilder.AppendLine("set");
            classBuilder.AppendLine("{");
            classBuilder.AppendLine($"if({fieldName} == value)");
            classBuilder.AppendLine("{");
            classBuilder.AppendLine("\treturn;");
            classBuilder.AppendLine("}");
            classBuilder.AppendLine($"var previousValue = {fieldName};");
            classBuilder.AppendLine($"{fieldName} = value;");
            classBuilder.AppendLine($"Notify{propertyName}ContextChanged(previousValue, value);");
            classBuilder.AppendLine($"On{field.Type.Name}ContextChanged(previousValue, value);");
            classBuilder.AppendLine("}");
            classBuilder.AppendLine("}");
            
            classBuilder.AppendLine(GenerateContextChangeImplementation(propertyName, fullyQualifiedFieldType));
        }
        
        WriteInterfaceImplementations(classBuilder, fields);
        
        classBuilder.AppendLine("}");
        classBuilder.AppendLine("}");

        return classBuilder.ToString();
    }

    private void WriteInterfaceImplementations(StringBuilder classBuilder, List<IFieldSymbol> fields)
    {
        List<string> fullyQualifiedTypesWritten = new();

        foreach (var field in fields)
        {
            var fullyQualifiedFieldType = GetFullyQualifiedFieldType(field);
            var simpleFieldType = field.Type.Name;
            if (fullyQualifiedTypesWritten.Contains(fullyQualifiedFieldType))
            {
                continue;
            }
            
            fullyQualifiedTypesWritten.Add(fullyQualifiedFieldType);
            
            classBuilder.AppendLine($"public event ContextChangedEventHandler<{fullyQualifiedFieldType}> On{simpleFieldType}ContextChangeEvent;");
            classBuilder.AppendLine($"public void On{simpleFieldType}ContextChanged({fullyQualifiedFieldType} previousValue, {fullyQualifiedFieldType} newValue, [CallerMemberName] string propertyName = null)");
            classBuilder.AppendLine("{");
            classBuilder.AppendLine($"On{simpleFieldType}ContextChangeEvent?.Invoke(this, new ContextChangedEventArgs<{fullyQualifiedFieldType}>(propertyName, previousValue, newValue));");
            classBuilder.AppendLine("}");
        }
    }

    private static IEnumerable<string> WriteInterfaces(StringBuilder classBuilder, List<IFieldSymbol> fields)
    {
        List<string> fullyQualifiedTypesWritten = new();

        foreach (var field in fields)
        {
            var fullyQualifiedFieldType = GetFullyQualifiedFieldType(field);
            var simpleFieldType = field.Type.Name;
            if (fullyQualifiedTypesWritten.Contains(fullyQualifiedFieldType))
            {
                continue;
            }
            
            fullyQualifiedTypesWritten.Add(fullyQualifiedFieldType);

            var interfaceName = $"INotify{simpleFieldType}ContextChanged";
            classBuilder.AppendLine($"public interface {interfaceName}");
            classBuilder.AppendLine("{");
            classBuilder.AppendLine($"event ContextChangedEventHandler<{fullyQualifiedFieldType}> On{simpleFieldType}ContextChangeEvent;");
            classBuilder.AppendLine($"void On{simpleFieldType}ContextChanged({fullyQualifiedFieldType} previousValue, {fullyQualifiedFieldType} newValue, [CallerMemberName] string propertyName = null);");
            classBuilder.AppendLine("}");

            yield return interfaceName;
        }
    }

    private static string GetFullyQualifiedFieldType(IFieldSymbol field)
    {
        return $"{field.Type.ContainingNamespace.ToDisplayString()}.{field.Type.Name}";
    }

    private string NormalizePropertyName(string fieldName) {
        return Regex.Replace(fieldName, "_[a-z]", delegate(Match m) {
            return m.ToString().TrimStart('_').ToUpper();
        });
    }
    private string GenerateContextChangeImplementation(string propertyName, string fieldType)
    {
        return $@"
        public event ContextChangedEventHandler<{fieldType}> On{propertyName}ContextChange;
        
        public void Notify{propertyName}ContextChanged({fieldType} previousValue, {fieldType} newValue, [CallerMemberName] string propertyName = """") 
        {{
            On{propertyName}ContextChange?.Invoke(this, new ContextChangedEventArgs<{fieldType}>(propertyName, previousValue, newValue));
        }}
        ";
    }
}