namespace EasyNetQ.AutoRespond
{
    public interface IHandleRequest<in TRequest, out TResponse>
        where TRequest : class
        where TResponse : class
    {
        TResponse Handle(TRequest request);
    }
}