using System.Threading.Tasks;

namespace EasyNetQ.AutoRespond
{
    public interface IAutoResponderRequestDispatcher
    {
        TResponse Dispatch<TRequest, TResponse, TResponder>(TRequest request)
            where TRequest : class
            where TResponse : class
            where TResponder : class, IHandleRequest<TRequest, TResponse>;

        Task<TResponse> DispatchAsync<TRequest, TResponse, TAsyncResponder>(TRequest request)
            where TRequest : class
            where TResponse : class
            where TAsyncResponder : class, IHandleRequestAsync<TRequest, TResponse>;
    }
}