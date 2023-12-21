namespace ProxyApiMock.Interfaces
{
    public interface IMockApi
    {
        public Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request);

        public int Port { get; }
    }

}
