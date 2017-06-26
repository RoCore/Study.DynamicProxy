namespace FastProxy.Definitions
{
    public interface IInterceptor
    {
        object Invoke(InterceptorValues callDescription);
    }
}
