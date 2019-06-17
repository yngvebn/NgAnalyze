using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ng.Contracts;
using Ng.Core;
using Ng.Core.Compilation.v2;
using NUnit.Framework;

namespace Ng.Tests
{
    [TestFixture]
    public class Class1
    {
        private TsConfig _tsConfig;
        private TsProject<TypescriptFile> _project;

        [SetUp]
        public void LoadProject()
        {
            var path = Path.GetFullPath(@"C:\github\NgAnalyze\TestProject\tsconfig.json");
            _tsConfig = new TsConfigReader().LoadFromFile(path);

            AutoMapper.Mapper.Initialize(config => Mapping.Configuration(config, _tsConfig.RootDir));
            _project = TsProject<TypescriptFile>.Load(_tsConfig, TypescriptFile.Load);
        }

        [Test]
        public void CanAnalyzeTsProject()
        {
            Assert.That(_project.Compiled.Count, Is.EqualTo(4));
        }

        [TestCase(@"C:\github\NgAnalyze\TestProject\src\actions.ts", "OtherAction", 4)]
        public void CanFindReferences(string path, string type, int numberOfReferences)
        {
            var importToken = Token.Create(path, type);
            IEnumerable<TypescriptConstruct> references = _project.RunTraverser(new FindAllReferences(importToken));
            Assert.That(references.Count(), Is.EqualTo(numberOfReferences));
        }
    }
}
