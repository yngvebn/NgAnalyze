using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Zu.TypeScript.TsTypes;

namespace Ng.Core
{
    public class ImportedModule : IEqualityComparer<ImportedModule>, IEquatable<ImportedModule>
    {
        public bool IsLocalImport { get; }
        [JsonIgnore]
        private readonly ImportSpecifier _importSpecifier;
        [JsonIgnore]
        private StringLiteral _path;

        private readonly string _absolutePath;

        [JsonIgnore]
        private readonly NamespaceImport _namespaceImport;

        public string As => _namespaceImport?.Children.OfKind(SyntaxKind.Identifier).First().IdentifierStr;
        private readonly string _name = "*";
        public string Name => ExtractName(IsNamespaceImport || _importSpecifier == null ? _name : _importSpecifier.GetText());
        public string Alias => ExtractAlias(IsNamespaceImport || _importSpecifier == null ? _name : _importSpecifier.GetText());

        private string ExtractAlias(string s)
        {
            var segments = s.Split(' ');
            return segments.Length > 1 && segments.Contains("as") ? segments.Last() : null;
        }

        private string ExtractName(string s)
        {
            var segments = s.Split(' ');
            return segments.First();
        }

        public string Path { get; private set; }
        public bool IsNamespaceImport => _namespaceImport != null;
        public string ImportToken => !string.IsNullOrEmpty(_absolutePath) && !string.IsNullOrEmpty(Name) ? $"{_absolutePath}#{Name}" : null;

        public ImportedModule(ImportSpecifier importSpecifier, StringLiteral path, string absolutePath = null)
        {
            _importSpecifier = importSpecifier;
            _path = path;
            _absolutePath = absolutePath;

            Path = _path.GetText().Replace("'", "");
        }


        public ImportedModule(NamespaceImport namespaceImport, StringLiteral path, string absolutePath = null)
        {
            _namespaceImport = namespaceImport;
            _path = path;
            _absolutePath = absolutePath;

            Path = _path.GetText().Replace("'", "");
        }

        public ImportedModule(string type, string from, bool isLocalImport = false)
        {
            IsLocalImport = isLocalImport;

            _name = type;
            Path = from;
        }
        public ImportedModule(string type, string from, string absolutePath)
        {
            IsLocalImport = !string.IsNullOrEmpty(absolutePath);
            _absolutePath = absolutePath;

            _name = type;
            Path = from;
        }

        public bool Equals(ImportedModule x, ImportedModule y)
        {
            return x.Name.Equals(y.Name) && x.Path.Equals(y.Path);

        }

        public int GetHashCode(ImportedModule obj)
        {
            return $"{obj.Name}#{obj.Path}".GetHashCode();
        }


        public bool Equals(ImportedModule other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(As, other.As) && string.Equals(Name, other.Name) && string.Equals(Path, other.Path);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ImportedModule) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (As != null ? As.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Path != null ? Path.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}