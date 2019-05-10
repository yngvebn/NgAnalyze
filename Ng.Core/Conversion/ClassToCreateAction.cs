using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Zu.TypeScript.TsTypes;

namespace Ng.Core.Conversion
{

    public class UsageToNewAction : IConvert<Usage>
    {
        public ConversionResult Convert(Usage usage)
        {
            List<ImportedModule> requiredImports = new List<ImportedModule>();
            List<ImportedModule> removedImports = new List<ImportedModule>();
            var nodeToConvert = usage.Node;
            StringBuilder sb = new StringBuilder();
            if (usage.IsNamespaceImport)
            {

                var newKeyword = usage.Node.GetDescendants().OfType<NewExpression>().FirstOrDefault();
                var modified = newKeyword.GetText()
                    .Replace($"new {newKeyword.Expression.GetText()}", newKeyword.Expression.GetText())
                    .Replace(usage.Lookup.Name, usage.Lookup.Name.ToCamelCase());
                nodeToConvert = newKeyword;
                sb.Append(modified);
                //var newCallExpression = "";
                //var expression = new CallExpression()
                //{
                //    Children = newKeyword.Children
                //};
                //changesInUsages.ChangeNode(newKeyword, newCallExpression);

                //var identifier = usage.Node.GetDescendants().OfType<Identifier>()
                //    .FirstOrDefault(ident => ident.IdentifierStr.Equals(action.Name));

                //identifier.Parent.Children.Add(new Identifier()
                //{
                //    Text = action.Name.ToCamelCase()
                //});
                //identifier.Parent.Children.Remove(identifier);
                //var s = identifier.GetText();
                // no need to change the import statement

            }
            else
            {
                var newKeyword = usage.Node.GetDescendants().OfType<NewExpression>().FirstOrDefault();
                var modified = newKeyword.GetText()
                    .Replace($"new {newKeyword.Expression.GetText()}", newKeyword.Expression.GetText())
                    .Replace(usage.Lookup.Name, usage.Lookup.Name.ToCamelCase());
                var existingImport = usage.Compilation.Imports.SelectMany(i => i.ImportedModules)
                    .First(module => module.Name.Equals(usage.Lookup.Name));
                nodeToConvert = newKeyword;
                requiredImports.Add(new ImportedModule(usage.Lookup.Name.ToCamelCase(), existingImport.Path));
                removedImports.Add(new ImportedModule(usage.Lookup.Name, existingImport.Path));
                sb.Append(modified);
            }
            return ConversionResult.Create(nodeToConvert, sb.ToString(), requiredImports, removedImports);
        }
    }

    public class ClassToCreateAction : IConvert<TypeScriptClass>
    {
        public ConversionResult Convert(TypeScriptClass obj)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            List<ImportedModule> requiredImports = new List<ImportedModule>();
            requiredImports.Add(new ImportedModule("createAction", "@ngrx/store"));
            if (obj.ConstructorArguments.Any())
            {
                //requiredImports.Add(new ImportedModule("props", "@ngrx/store"));
            }

            sb.Append(GetInitializer(obj, obj.ConstructorArguments.Any()));
            if (obj.ConstructorArguments.Any())
            {
                sb.AppendLine(",");
                sb.AppendLine(GetPropsObject(obj));
                sb.AppendLine(");");
            }
            else
            {
                sb.Append(");");
            }

            sb.AppendLine();
            return ConversionResult.Create(obj.Node, sb.ToString(), requiredImports, new List<ImportedModule>() { new ImportedModule("Action", "@ngrx/store") });

        }

        private string GetPropsObject(TypeScriptClass typeScriptClass)
        {
            //if (typeScriptClass.ConstructorArguments.Any(c => c.Initializer != null))
            //{
            return $"\t({GetPropertiesWithIntitializers(typeScriptClass.ConstructorArguments)}) => ({{ {string.Join(", ", typeScriptClass.ConstructorArguments.Select(c => c.Name))} }})";
            //}
            //else
            //{
            //    return $"\tprops<{{ { GetPropertiesWithIntitializers(typeScriptClass.ConstructorArguments)} }}>()";
            //}

        }

        private string GetPropertiesWithIntitializers(IEnumerable<Parameter> constructorArguments)
        {
            return string.Join(", ", constructorArguments.Select(argument => argument.SerializeToString()));
        }

        private string GetInitializer(TypeScriptClass typeScriptClass, bool hasArguments)
        {
            var type = typeScriptClass.Properties.SingleOrDefault(t => t.Name.Equals("type"));
            return $"export const {typeScriptClass.Name.ToCamelCase()} = createAction({(hasArguments ? "\n\t" : "")}{type.Type}";
        }
    }

    public interface IConvert<in T>
    {
        ConversionResult Convert(T obj);
    }

    public class ConversionResult
    {
        public INode NodeToTarget { get; }
        public string Output { get; }
        public IEnumerable<ImportedModule> RequiredImports { get; }
        public IEnumerable<ImportedModule> RemovedImports { get; }

        public ConversionResult(INode nodeToTarget, string output, IEnumerable<ImportedModule> requiredImports, IEnumerable<ImportedModule> removedImports)
        {
            NodeToTarget = nodeToTarget;
            Output = output;
            RequiredImports = requiredImports;
            RemovedImports = removedImports;
        }

        public static ConversionResult Create(INode nodeToTarget, string output, IEnumerable<ImportedModule> requiredImports, IEnumerable<ImportedModule> removedImports = null)
        {
            return new ConversionResult(nodeToTarget, output, requiredImports, removedImports);
        }
    }

    public static class StringExtensions
    {
        public static string ToCamelCase(this string str)
        {
            return Char.ToLowerInvariant(str[0]) + str.Substring(1);
        }
    }
}