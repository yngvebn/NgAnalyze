using System.Linq;
using Zu.TypeScript.TsTypes;

namespace Ng.Core
{
    public class TypeScriptProperty
    {
        public string Name => _arg.Children.OfType<Identifier>().First().GetText();
        public string Type => _arg.Children.Last().GetText();
        private PropertyDeclaration _arg;

        public TypeScriptProperty(PropertyDeclaration arg)
        {
            _arg = arg;
      
        }

        public static TypeScriptProperty Create(PropertyDeclaration arg)
        {
            return new TypeScriptProperty(arg);
        }
    }
}