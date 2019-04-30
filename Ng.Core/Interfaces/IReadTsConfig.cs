using Ng.Contracts;

namespace Ng.Core.Interfaces
{
    public interface IReadTsConfig
    {
        TsConfig LoadFromFile(string path);
    }
}
