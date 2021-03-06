﻿using System.Diagnostics;
using Zu.TypeScript.TsTypes;

namespace Ng.Core
{
    [DebuggerDisplay("{Compilation.FileName}")]
    public class Usage
    {
        public TypescriptCompilation Compilation { get; set; }
        public INode Node { get; set; }
        public TypeScriptClass Lookup { get; set; }
        public bool IsNamespaceImport { get; set; }
    }
}