using System;
using System.Collections.Generic;
using Zu.TypeScript.TsTypes;

namespace Ng.Core
{
    public class ImportedModule : IEqualityComparer<ImportedModule>, IEquatable<ImportedModule>
    {
        private ImportSpecifier _importSpecifier;
        private StringLiteral _path;
        private NamespaceImport i;
        public string As { get; private set; }
        public string Name { get; private set; }
        public string Path { get; private set; }
        public bool IsNamespaceImport => !string.IsNullOrEmpty(As);

        public ImportedModule(ImportSpecifier importSpecifier, StringLiteral path)
        {
            _importSpecifier = importSpecifier;
            _path = path;

            Path = _path.GetText().Replace("'", "");
            Name = _importSpecifier.GetText();
        }

        public ImportedModule(string type, string from)
        {
            Name = type;
            Path = from;
        }

        public ImportedModule(NamespaceImport i, StringLiteral path)
        {
            Name = "*";
            As = i.IdentifierStr;
            Path = path.GetText().Replace("'", "");
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