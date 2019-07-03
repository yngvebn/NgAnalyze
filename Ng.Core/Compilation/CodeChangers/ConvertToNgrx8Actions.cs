using System;
using System.Collections.Generic;
using System.Linq;
using Ng.Core.Compilation.v2;

namespace Ng.Core.Compilation.CodeChangers
{
    public class ConvertToNgrx8Actions: IChangeCode<TypescriptFile>
    {
        private readonly string[] _pathsToActionFiles;

        public ConvertToNgrx8Actions(params string[] pathsToActionFiles)
        {
            _pathsToActionFiles = pathsToActionFiles.Select(c => c.ToLower()).ToArray();
        }

        public void PerformChange(IEnumerable<TypescriptFile> root)
        {
            var filesToLookIn = root.Where(f => _pathsToActionFiles.Contains(f.Filename.ToLower())).ToList();

        }
    }

    public interface IChangeCode<T>
    {
        void PerformChange(IEnumerable<T> root);
    }
}