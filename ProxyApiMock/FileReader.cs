using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProxyApiMock.Interfaces;

namespace ProxyApiMock
{
    public class FileReader : IFileReader
    {
        public async Task<string> ReadAllTextAsync(string path)
        {
            return await File.ReadAllTextAsync(path);
        }
    }
}
