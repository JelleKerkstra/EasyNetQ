using System.Threading.Tasks;

namespace EasyNetQ.AutoRespond
{
    public interface IAutoResponderRequestDispatcher
    {
        Task<TResponse> DispatchAsync<TRequest, TResponse, TAsyncResponder>(TRequest request)
            where TRequest : class
            where TResponse : class
            where TAsyncResponder : class, IHandleRequestAsync<TRequest, TResponse>;
    }
}