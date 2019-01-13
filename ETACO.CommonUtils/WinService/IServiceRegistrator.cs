namespace ETACO.CommonUtils.WinService
{
    public interface IServiceRegistrator
    {
        string Register(string serviceName);
        void UnRegister(string serviceName);
    }
}
