using Zu.TypeScript.TsTypes;

namespace Ng.Core
{
    public class TypeScriptProperty
    {
        private PropertyDeclaration arg;

        public TypeScriptProperty(PropertyDeclaration arg)
        {
            this.arg = arg;
        }

        public static TypeScriptProperty Create(PropertyDeclaration arg)
        {
            return new TypeScriptProperty(arg);
        }
    }
}