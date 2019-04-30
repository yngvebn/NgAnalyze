using Ng.Contracts;
using Ng.Core.Interfaces;

namespace Ng.Core
{
    public class TsConfigReader: IReadTsConfig
    {
        public TsConfig LoadFromFile(string path)
        {
            return new TsConfig(path);
        }
    }
}