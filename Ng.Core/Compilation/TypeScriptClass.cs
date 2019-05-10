using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Zu.TypeScript.TsTypes;

namespace Ng.Core
{

    public static class NodeExtensions
    {
        public static string Serialize(this Modifier modifier)
        {
            return modifier?.GetText();
        }

        public static string Serialize(this TypeNode node)
        {
            return $": {node?.GetText()}";
        }
    }

    public class Parameter
    {
        public TypeNode Type { get; set; }
        public string Name { get; set; }
        public Modifier Modifier { get; set; }
        public Node Initializer { get; set; }
        public bool Nullable { get; set; }

        public static Parameter Create(Zu.TypeScript.TsTypes.ParameterDeclaration declaration)
        {
            return new Parameter(declaration);
        }

        public Parameter(ParameterDeclaration declaration)
        {
            Type = declaration.Children.OfType<TypeNode>().FirstOrDefault();
            Name = declaration.Children.OfType<Identifier>().FirstOrDefault()?.IdentifierStr;
            Modifier = declaration.Children.OfType<Modifier>().FirstOrDefault();
            Nullable = declaration.Children.OfType<QuestionToken>().Any();
            Initializer = declaration.Children.FirstOrDefault(IsInitializer);
        }

        private bool IsInitializer(Node arg)
        {
            return arg is StringLiteral || arg is NumericLiteral || arg.Kind == SyntaxKind.NullKeyword|| arg is BooleanLiteral;

        }

        public string SerializeToString()
        {
            var definition = $"{Name}{(Nullable ? "?" : "")}{Type.Serialize()}";
            definition = Regex.Replace(definition, "\\s\\s+", " ");
            return $"{definition}{(Initializer != null ? $" = {Initializer.GetText()}" : "")}".Trim();
        }
    }

    public class Property
    {

        public string Name => arg.Children.OfType<Identifier>().First().GetText();
        public string Type => arg.Children.OfType<PropertyAccessExpression>().First().GetText();
        private PropertyDeclaration arg;

        public Property(PropertyDeclaration arg)
        {
            this.arg = arg;
        }

        public static Property Create(PropertyDeclaration arg)
        {
            return new Property(arg);
        }
    }

    public class TypeAliasDefinition
    {
        public TypeAliasDeclaration Node { get; }
        public string Name => Node.Children.OfType<Identifier>().First().GetText();

        public TypeAliasDefinition(TypeAliasDeclaration node)
        {
            Node = node;
        }

        public static TypeAliasDefinition Create(TypeAliasDeclaration node)
        {
            return new TypeAliasDefinition(node);
        }
    }

    [DebuggerDisplay("{Name}")]
    public class TypeScriptClass
    {
        public string FileName => ((SourceFile) Node.Parent).FileName;
        public bool Exported => Node.Children.OfType<Modifier>().Any(m => m.Kind == SyntaxKind.ExportKeyword);
        public string Name => Node.Children.OfType<Identifier>().First().GetText();
        public IEnumerable<ImportedModule> Inherits => this.Node.Children.OfType<HeritageClause>()
            .Select(SelectImportFromInheritage);

        public IEnumerable<Parameter> ConstructorArguments => this.Node.GetDescendants()
            .OfType<ConstructorDeclaration>().FirstOrDefault()?.GetDescendants().OfType<ParameterDeclaration>()
            .Select(Parameter.Create);



        private ImportedModule SelectImportFromInheritage(HeritageClause heritageClause)
        {
            var importedModules = _imports.SelectMany(i => i.ImportedModules);
            if (heritageClause.GetDescendants(false).OfType<PropertyAccessExpression>().Any())
            {
                return importedModules.Where(i => !string.IsNullOrEmpty(i.As))
                    .FirstOrDefault(i => i.As.Equals(heritageClause.GetDescendants(false).OfType<PropertyAccessExpression>().First().IdentifierStr));
            }
            else
            {
                var locatedModule = importedModules.FirstOrDefault(i => i.Name.Equals(heritageClause.First.IdentifierStr));
                if (locatedModule == null)
                {
                    Debugger.Break();
                }
                return locatedModule;
            }
        }

        public IEnumerable<TypeScriptProperty> Properties =>
            Node.Children.OfType<PropertyDeclaration>().Select(TypeScriptProperty.Create);

        public ClassDeclaration Node { get; private set; }
        public TypescriptCompilation Compilation { get; }
        private readonly Imports[] _imports;

        public TypeScriptClass(ClassDeclaration node, TypescriptCompilation compilation)
        {
            this.Node = node;
            Compilation = compilation;
            _imports = compilation.Imports;
        }

        public static TypeScriptClass Create(ClassDeclaration declaration, TypescriptCompilation compilation)
        {
            return new TypeScriptClass(declaration, compilation);
        }
    }
}