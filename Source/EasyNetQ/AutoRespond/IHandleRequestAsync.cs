using System.Threading.Tasks;

namespace EasyNetQ.AutoRespond
{
    public interface IHandleRequestAsync<in TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        Task<TResponse> HandleAsync(TRequest request);
    }
}