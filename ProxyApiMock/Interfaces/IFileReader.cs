namespace ProxyApiMock.Interfaces
{
    public interface IFileReader
    {
        Task<string> ReadAllTextAsync(string path);

    }
}
