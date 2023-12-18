using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyApiMock
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class Service
    {
        private Guid guid;
        public Guid Id
        {
            get
            {
                guid = guid != Guid.Empty ? guid : Guid.NewGuid();
                return guid;
            }
        }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Url")]
        public string Url { get; set; }
    }

    public class Request
    {
        private Guid guid;
        public Guid Id
        {
            get
            {
                guid = guid != Guid.Empty ? guid : Guid.NewGuid();
                return guid;
            }
        }
        
        [JsonProperty("endpoint")]
        public string? Endpoint { get; set; }

        [JsonProperty("mockparams")]
        public Dictionary<string, string>? Params { get; set; }

        [JsonProperty("response")]
        public Response? Response { get; set; }
    }

    public class Response
    {
        private Guid guid;
        public Guid Id
        {
            get 
            {
                guid = guid != Guid.Empty ? guid : Guid.NewGuid();
                return guid; 
            }
        }

        [JsonProperty("headers")]
        public Dictionary<string, string>? Headers { get; set; }

        [JsonProperty("status-code")]
        public int? StatusCode { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }
    }

    public class ApiRequests
    {
        [JsonProperty("Requests")]
        public Request[] Requests { get; set; }
    }
}
