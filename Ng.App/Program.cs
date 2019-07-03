using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Ng.Contracts;
using Ng.Core;
using Ng.Core.Compilation.CodeChangers;
using Ng.Core.Compilation.v2;
using Ng.Core.Conversion;
using Zu.TypeScript;
using Zu.TypeScript.Change;
using Zu.TypeScript.TsTypes;

namespace Ng.App
{
    public class Change
    {
        public TypescriptCompilation Compilation { get; }
        public ChangeAST ChangeAst { get; set; }
        public string FileName => Compilation.FileName;
        public List<ImportedModule> ImportsToAdd = new List<ImportedModule>();
        public List<ImportedModule> ImportsToRemove = new List<ImportedModule>();

        public Change(TypescriptCompilation compilation)
        {
            Compilation = compilation;
            ChangeAst = new ChangeAST();
        }

        public void ChangeNode(INode actionNode, string conversionOutput)
        {
            ChangeAst.ChangeNode(actionNode, conversionOutput);
        }

        public string GetChangedSource()
        {
            Compilation.AddRemoveImports(ChangeAst, ImportsToAdd.Distinct(), ImportsToRemove.Distinct());
            return ChangeAst.GetChangedSource(Compilation.Ast.SourceStr);
        }

        public void AddRemoveImports(IEnumerable<ImportedModule> distinct, IEnumerable<ImportedModule> removedUsagesImports)
        {
            this.ImportsToAdd.AddRange(distinct);
            this.ImportsToRemove.AddRange(removedUsagesImports);
        }

        public void Append(string union)
        {
            ChangeAst.InsertAfter(Compilation.Ast.RootNode.Children.Last(), union);
        }

        public void Delete(INode typeAlias)
        {
            ChangeAst.Delete(typeAlias);
        }
    }

    public class Changes
    {
        private readonly Dictionary<string, Change> _dictionary;

        public Changes()
        {
            _dictionary = new Dictionary<string, Change>();
        }

        public IEnumerable<Change> All => _dictionary.Values;

        public Change Get(TypescriptCompilation compilation)
        {
            if (_dictionary.ContainsKey(compilation.FileName)) return _dictionary[compilation.FileName];

            _dictionary.Add(compilation.FileName, new Change(compilation));

            return _dictionary[compilation.FileName];
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            //var specs = Directory.GetFiles(
            //    @"C:\Arbeid\etoto\code\beta.rikstoto.no\src\Rikstoto.Toto\App\Components\", "*component.spec.ts", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();
            //foreach (var file in Directory.GetFiles(
            //    @"C:\Arbeid\etoto\code\beta.rikstoto.no\src\Rikstoto.Toto\App\Components\", "*component.ts",
            //    SearchOption.AllDirectories))
            //{
            //    if (!specs.Contains(Path.GetFileNameWithoutExtension(file) + ".spec.ts"))
            //    {
            //        GenerateSpecIfApplicable(file);
            //    }
            //}

            //string fileName =
            //    @"C:\Arbeid\etoto\code\beta.rikstoto.no\src\Rikstoto.Toto\App\Components\MyBets\MyBetsDate\MyBetsRaceday\MyBetsBet\MyBetsBetDetails\Prize\my-bet-prize.component.ts";
            //TypeScriptAST ast = new TypeScriptAST(File.ReadAllText(fileName), fileName);
            var path = Path.GetFullPath(@"..\..\..\TestProject\tsconfig.json");// 
            //path = @"C:\Arbeid\etoto\code\beta.rikstoto.no\src\Rikstoto.Toto\tsconfig.test.json";
            //path = @"C:\github\beta.rikstoto.no\src\Rikstoto.Toto\tsconfig.test.json";
            TsConfig tsConfig = new TsConfigReader().LoadFromFile(path);
            AutoMapper.Mapper.Initialize(config => Mapping.Configuration(config, tsConfig.RootDir));
            TsProject<TypescriptFile> projectV2 = TsProject<TypescriptFile>.Load<TypescriptFile>(tsConfig, TypescriptFile.Load);

            projectV2.RunCodeChanger(new ConvertToNgrx8Actions(@"C:\github\ng-analyze\TestProject\src\actions.ts"));


            List<HtmlDocument> templates = projectV2.RunTraverser(new ExtractHtmlDocuments()).ToList();
            List<Angular.ComponentOptions> components = projectV2.RunTraverser(new ExtractAllComponentOptions()).ToList();

            foreach (var component in components)
            {
                var usagesInTemplates =
                    templates.Where(t => t.DocumentNode.GetElementsByTagName(component.Selector).Any());
                if(!usagesInTemplates.Any())
                    Console.WriteLine($"{component.Selector} does not seem to be in use");
            }

            File.WriteAllText(@"..\..\..\TestProject\compiled.json", JsonConvert.SerializeObject(projectV2, Formatting.Indented));
            //TsProject<TypescriptCompilation> project = TsProject<TypescriptCompilation>.Load(tsConfig, (TypescriptCompilation.CreateCompiled));
            Console.Clear();
            Console.WriteLine($"Compilation done - {projectV2.Files.Count} files");

            //var allClasses = project.Compiled.SelectMany(p => p.Classes);

            ////var projectWideImports = project.Compiled.SelectMany(c => c.Imports).Select(i => i.AbsolutePath).Distinct().ToList();
            ////var allFilenames = project.Files.Select(c => c.SourceStr).Distinct().ToList();
            ////var filesNotReferencced = allFilenames.Where(filename =>
            ////    projectWideImports.All(import => import != filename)).ToList();

            //ImportedModule storeActionType = new ImportedModule("Action", "@ngrx/store");
            //ImportedModule effectsActions = new ImportedModule("Actions", "@ngrx/effects");
            //var allActions = allClasses.Where(c => c.Inherits.Any(i => i.Equals(storeActionType)))
            //    .Where(a => a.FileName.EndsWith("form-rows-gallop.actions.ts"))
            //    .ToList();
            //var allUsagesOfActions = project.FindUsages(effectsActions);
            //var changes = new Changes();
            //foreach (var group in allActions.GroupBy(a => a.FileName))
            //{
            //    var file = group.First().Compilation;
            //    var change = changes.Get(file);
            //    List<ImportedModule> newImports = new List<ImportedModule>();
            //    List<ImportedModule> removedImports = new List<ImportedModule>();
            //    string union =
            //        $"\n\nconst all = union({{ {string.Join(", ", group.Select(action => action.Name.ToCamelCase()))} }});";

            //    newImports.Add(new ImportedModule("union", "@ngrx/store"));
            //    change.Append(union);

            //    var typeAlias = file.TypeAliases.LastOrDefault();
            //    var unionUsages = project.FindUsages(typeAlias);
            //    var typeAliasName = typeAlias?.Name;
            //    if (typeAlias != null)
            //    {
            //        if (typeAliasName.Equals("Actions"))
            //        {
            //            typeAliasName = Path.GetFileNameWithoutExtension(file.FileName).Replace(".", "-")
            //                .ConvertDashToCamelCase();
            //            Console.WriteLine($"Name Actions not recommended for type union. Will rename to {Path.GetFileNameWithoutExtension(file.FileName).Replace(".", "-").ConvertDashToCamelCase()}");
            //        }
            //        change.Delete(typeAlias.Node);
            //        string newTypeAlias = $"\nexport type {typeAliasName} = typeof all;\n";
            //        change.Append(newTypeAlias);
            //    }
            //    ImportedModule typeAliasImport = new ImportedModule(typeAliasName, file.FileName, true);

            //    foreach (var action in group)
            //    {
            //        var conversion = new ClassToCreateAction().Convert(action);
            //        change.ChangeNode(action.Node, conversion.Output);
            //        newImports.AddRange(conversion.RequiredImports);
            //        removedImports.AddRange(conversion.RemovedImports);
            //        var usages = project.FindUsages(action).ToList();
            //        var importsToAdd = new List<ImportedModule>();
            //        var importsToRemove = new List<ImportedModule>();

            //        foreach (var usagesByFile in usages.GroupBy(a => a.Compilation.FileName))
            //        {
            //            var usagesFile = usagesByFile.First().Compilation;
            //            var changesInUsages = changes.Get(usagesFile);
            //            var effectsActionsInFile = allUsagesOfActions.Where(c => c.Compilation.FileName == usagesFile.FileName);

            //            List<ImportedModule> newUsagesImports = new List<ImportedModule>();
            //            List<ImportedModule> removedUsagesImports = new List<ImportedModule>();
            //            if (typeAlias != null)
            //            {
            //                foreach (var actionsReference in effectsActionsInFile)
            //                {
            //                    try
            //                    {
            //                        changesInUsages.ChangeNode(actionsReference.Node, $"Actions<{typeAliasName}>");
            //                        newUsagesImports.Add(typeAliasImport);
            //                    }
            //                    catch
            //                    {
            //                        // fails second time, but that's okay.
            //                    }
            //                }
            //            }
                      


            //            foreach (var usage in usagesByFile)
            //            {
            //                var usageConversion = new UsageToNewAction().Convert(usage);
            //                changesInUsages.ChangeNode(usageConversion.NodeToTarget as Node, usageConversion.Output);
            //                newUsagesImports.AddRange(usageConversion.RequiredImports);
            //                removedUsagesImports.AddRange(usageConversion.RemovedImports);
            //            }
            //            importsToAdd.AddRange(newUsagesImports);
            //            importsToRemove.AddRange(removedUsagesImports);
            //            changesInUsages.AddRemoveImports(newUsagesImports.Distinct(), removedUsagesImports);


            //            //File.WriteAllText(Path.Combine(Path.GetDirectoryName(usagesFile.FileName), $"{Path.GetFileNameWithoutExtension(usagesFile.FileName)}.ts"), newUsagesSource);
            //        }

            //    }
            //    // TODO: Create actions-union

            //    foreach (var queuedChange in changes.All)
            //    {
            //        //usagesFile.AddRemoveImports(changesInUsages.ChangeAst, newUsagesImports.Distinct(), removedUsagesImports);
            //        var newUsagesSource = queuedChange.GetChangedSource();
            //        //File.WriteAllText(usagesFile.FileName, newUsagesSource);

            //        File.WriteAllText(Path.Combine(Path.GetDirectoryName(queuedChange.FileName), $"{Path.GetFileName(queuedChange.FileName)}"), newUsagesSource);
            //    }
            //    change.AddRemoveImports(newImports.Distinct(), removedImports.Distinct());

            //    var newSource = change.GetChangedSource();
            //    //File.WriteAllText(Path.Combine(Path.GetDirectoryName(file.FileName), $"{Path.GetFileNameWithoutExtension(file.FileName)}.ts"), newSource);
            //    File.WriteAllText(Path.Combine(Path.GetDirectoryName(file.FileName), $"{Path.GetFileName(file.FileName)}"), newSource);
            //}

            //////foreach (var inherits in firstAction.Classes.First().Inherits)
            //////{
            //////    Console.WriteLine(inherits);
            //////}


            ////var firstUsage = usages.First();
            ////var change = new ChangeAST();
            ////change.ChangeNode(firstUsage.Node, "/* New value goes here */");
            ////var newSource = change.GetChangedSource(firstUsage.Compilation.Ast.SourceStr);
            ////File.WriteAllText(firstUsage.Compilation.FileName, newSource);
            //Console.WriteLine("Done!");
            //Console.ReadLine();
        }

        private static void GenerateSpecIfApplicable(string file)
        {
            TypeScriptAST ast = new TypeScriptAST(File.ReadAllText(file), file);
            var constructor = ast.RootNode.GetDescendants().OfType<ConstructorDeclaration>().SingleOrDefault();
            if (constructor != null)
            {
                Console.WriteLine("Found constructor. Checking for parameters");
                if (constructor.Children.OfType<ParameterDeclaration>().Any())
                {
                    Console.WriteLine("No parameterless constructor. Skipping");
                    return;
                }
            }

            var methods = ast.GetDescendants().OfType<MethodDeclaration>();
            if (!methods.Any())
            {
                Console.WriteLine("No methods. Skipping");
                return;

            }
            Console.WriteLine($"Create spec for {file}");
            // Collect information
            var relativeFileNameForImport = $"./{Path.GetFileNameWithoutExtension(file)}";
            var classDeclaration = ast.RootNode.GetDescendants().OfType<ClassDeclaration>().SingleOrDefault();
            if (classDeclaration.Children.Any(c => c.Kind == SyntaxKind.AbstractKeyword))
            {
                Console.WriteLine("Abstract - skipping");
                return;
            }
            var className = classDeclaration.IdentifierStr;
            StringBuilder spec = new StringBuilder();

            spec.AppendLine($"import {{ {className} }} from '{relativeFileNameForImport}';");
            spec.AppendLine("");
            spec.AppendLine($"describe('{className}', () => {{");
            spec.AppendLine($"\tlet instance: {className} = null;");
            spec.AppendLine($"\tbeforeEach(() => {{");
            spec.AppendLine($"\t\tinstance = new {className}();");
            spec.AppendLine($"\t}});");
            spec.AppendLine("");

            spec.AppendLine($"\tit('can be created', () => {{");
            spec.AppendLine($"\t\texpect(instance).toBeTruthy();");
            spec.AppendLine($"\t}});");
            foreach (var method in methods.Where(m => m.Children.Any(c => c.Kind == SyntaxKind.PublicKeyword)))
            {
                spec.AppendLine("");
                spec.AppendLine($"\tdescribe('{method.IdentifierStr}', () => {{");


                var parameterDeclarations = method.Children.OfType<ParameterDeclaration>().ToList();
                if (parameterDeclarations.Count() > 1)
                {
                    spec.AppendLine($"\t\tit('Can be invoked', () => {{");
                    foreach (var param in parameterDeclarations)
                    {
                        spec.AppendLine($"\t\t\t// {param.IdentifierStr}: {param.Last.Kind}");
                    }
                    spec.AppendLine($"\t\t}});");

                }
                else if (!parameterDeclarations.Any())
                {
                    spec.AppendLine($"\t\tit('Can be invoked', () => {{");
                    spec.AppendLine($"\t\t\tinstance.{method.IdentifierStr}();");
                    spec.AppendLine($"\t\t}});");
                }
                else
                {
                    foreach (var testValue in GetTestValues(parameterDeclarations.Single()))
                    {
                        spec.AppendLine($"\t\tit(`Can be invoked with {testValue}`, () => {{");
                        spec.AppendLine($"\t\t\tconst result = instance.{method.IdentifierStr}({testValue});");
                        spec.AppendLine($"\t\t\texpect(result);");
                        spec.AppendLine($"\t\t}});");
                    }
                }


                spec.AppendLine($"\t}});");

            }
            spec.AppendLine($"}});");

            var fileContents = spec.ToString().Replace("\t", "    ");
            var saveToPath = Path.GetDirectoryName(file);
            var specFilename = Path.GetFileNameWithoutExtension(file) + ".spec.ts";
            File.WriteAllText(Path.Combine(saveToPath, specFilename), fileContents);

        }

        private static IEnumerable GetTestValues(ParameterDeclaration single)
        {
            Random r = new Random();
            yield return "undefined";
            yield return "null";
            if (single.Last.Kind == SyntaxKind.NumberKeyword)
            {
                yield return Int16.MaxValue;
                yield return Int16.MinValue;
                yield return 0;
                yield return r.Next() * 1000;
            }
            else if (single.Last.Kind == SyntaxKind.BooleanKeyword)
            {
                yield return "false";
                yield return "true";
            }
            else if (single.Last.Kind == SyntaxKind.StringKeyword)
            {
                yield return $"'{Guid.NewGuid().ToString("N").Substring(r.Next(1), r.Next(22))}'";
                yield return $"'{Guid.NewGuid().ToString("N").Substring(r.Next(1), r.Next(22))}'";
                yield return $"'{Guid.NewGuid().ToString("N").Substring(r.Next(1), r.Next(22))}'";
            }
        }
    }

    public static class HtmlNodeExtensions
    {
        public static IEnumerable<HtmlNode> GetElementsByName(this HtmlNode parent, string name)
        {
            return parent.Descendants().Where(node => node.Name == name);
        }

        public static IEnumerable<HtmlNode> GetElementsByTagName(this HtmlNode parent, string name)
        {
            return parent.Descendants(name);
        }
    }
}
