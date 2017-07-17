namespace FastProxy.Definitions
{
    public interface IInterceptor
    {
        object Invoke(InterceptionInformation callDescription);
    }
}
