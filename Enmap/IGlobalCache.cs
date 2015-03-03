using System;
using System.Threading.Tasks;

namespace Enmap
{
    public interface IGlobalCache
    {
        bool IsCacheable(Type sourceType, Type destinationType);
        Task<object[]> GetByIds(Type sourceType, Type destinationType, Array ids, MapperContext context); 
    }
}