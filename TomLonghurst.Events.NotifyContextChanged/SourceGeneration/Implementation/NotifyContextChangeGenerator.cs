﻿using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TomLonghurst.Events.NotifyContextChanged.Extensions;

namespace TomLonghurst.Events.NotifyContextChanged.SourceGeneration.Implementation;

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

        var fieldsThatNeedInterfacesGenerating = new List<IFieldSymbol>();
        foreach (var containingClassGroup in syntaxReciever.IdentifiedFields.GroupBy(x => x.ContainingType)) 
        {
            var containingClass = containingClassGroup.Key;
            var namespaceSymbol = containingClass.ContainingNamespace;
            var fields = containingClassGroup.ToList();
            
            var source = GenerateClass(context, containingClass, namespaceSymbol, fields);
            fieldsThatNeedInterfacesGenerating.AddRange(fields);
            context.AddSource($"{containingClass.Name}_NotifyContextChanged.generated", SourceText.From(source, Encoding.UTF8));
        }

        var interfaceSource = WriteInterfaces(context, fieldsThatNeedInterfacesGenerating);
        context.AddSource($"INotifyContextChangedInterfaces_NotifyContextChanged.generated", SourceText.From(interfaceSource, Encoding.UTF8));
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
        
        classBuilder.AppendLine($"public partial class {@class.Name}");
        classBuilder.AppendLine(":");
        
        var commaSeparatedListOfInterfaces = fields.Select(x => x.Type.GetSimpleTypeName()).Select(simpleFieldType => $"INotifyType{simpleFieldType}ContextChanged").Distinct().Aggregate((a, x) => $"{a}, {x}");
        classBuilder.AppendLine(commaSeparatedListOfInterfaces); 
        classBuilder.AppendLine("{");

        foreach(var field in fields) {
            var fullyQualifiedFieldType = field.Type.GetFullyQualifiedType();
            var simpleFieldType = field.Type.GetSimpleTypeName();
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
            classBuilder.AppendLine($"OnType{simpleFieldType}ContextChanged(previousValue, value);");
            classBuilder.AppendLine("}");
            classBuilder.AppendLine("}");
            
            classBuilder.AppendLine(GenerateClassContextChangeImplementation(propertyName, fullyQualifiedFieldType, field));
        }
        
        WriteInterfaceImplementations(classBuilder, fields);
        
        classBuilder.AppendLine("}");
        classBuilder.AppendLine("}");
        
        return classBuilder.ToString();
    }
    private void WriteInterfaceImplementations(StringBuilder classBuilder, List<IFieldSymbol> fields)
    {
        var interfacesCreated = new List<string>();

        foreach (var field in fields)
        {
            var fullyQualifiedFieldType = field.Type.GetFullyQualifiedType();;
            var simpleFieldType = field.Type.GetSimpleTypeName();

            var interfaceName = $"INotifyType{simpleFieldType}ContextChanged";
            
            if (interfacesCreated.Contains(interfaceName))
            {
                continue;
            }
            
            interfacesCreated.Add(interfaceName);
            
            classBuilder.AppendLine($"public event ContextChangedEventHandler<{fullyQualifiedFieldType}> OnType{simpleFieldType}ContextChange;");
            classBuilder.AppendLine($"private void OnType{simpleFieldType}ContextChanged({fullyQualifiedFieldType} previousValue, {fullyQualifiedFieldType} newValue, [CallerMemberName] string propertyName = null)");
            classBuilder.AppendLine("{");
            classBuilder.AppendLine($"OnType{simpleFieldType}ContextChange?.Invoke(this, new ContextChangedEventArgs<{fullyQualifiedFieldType}>(propertyName, previousValue, newValue));");
            classBuilder.AppendLine("}");
        }
    }

    private static string WriteInterfaces(GeneratorExecutionContext context,
        List<IFieldSymbol> fields)
    { 
        var interfacesCreated = new List<string>();
    
        var classBuilder = new StringBuilder();
        var notifyPropertyChangedSymbol = context.Compilation.GetTypeByMetadataName(typeof(INotifyContextChanged<>).FullName);
        var callerMemberSymbol = context.Compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.CallerMemberNameAttribute");
        
        classBuilder.AppendLine("using System;");
        classBuilder.AppendLine($"using {notifyPropertyChangedSymbol.ContainingNamespace};");
        classBuilder.AppendLine($"using {callerMemberSymbol.ContainingNamespace};");
        classBuilder.AppendLine($"namespace {notifyPropertyChangedSymbol.ContainingNamespace.ToDisplayString()}");
        classBuilder.AppendLine("{");

        foreach (var field in fields)
        {
            var fullyQualifiedFieldType = field.Type.GetFullyQualifiedType();;
            var simpleFieldType = field.Type.GetSimpleTypeName();

            var interfaceName = $"INotifyType{simpleFieldType}ContextChanged";
            
            if (interfacesCreated.Contains(interfaceName))
            {
                continue;
            }
            
            interfacesCreated.Add(interfaceName);
            
            classBuilder.AppendLine($"public interface {interfaceName}");
            classBuilder.AppendLine("{");
            classBuilder.AppendLine($"event ContextChangedEventHandler<{fullyQualifiedFieldType}> OnType{simpleFieldType}ContextChange;");
            classBuilder.AppendLine("}");
        }

        classBuilder.AppendLine("}");
        
        return classBuilder.ToString();
    }

    private string NormalizePropertyName(string fieldName) {
        return Regex.Replace(fieldName, "_[a-z]", delegate(Match m) {
            return m.ToString().TrimStart('_').ToUpper();
        });
    }
    private string GenerateClassContextChangeImplementation(string propertyName, string fieldType, IFieldSymbol field)
    {
        return $@"
        public event ContextChangedEventHandler<{fieldType}> On{propertyName}ContextChange;
        
        private void Notify{propertyName}ContextChanged({fieldType} previousValue, {fieldType} newValue, [CallerMemberName] string propertyName = """") 
        {{
            On{propertyName}ContextChange?.Invoke(this, new ContextChangedEventArgs<{fieldType}>(propertyName, previousValue, newValue));
        }}
        ";
    }
}