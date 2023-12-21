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

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Url")]
        public string Url { get; set; }
    }

    public class Request
    {
        
        [JsonProperty("endpoint")]
        public string? Endpoint { get; set; }

        [JsonProperty("mockparams")]
        public Dictionary<string, string>? Params { get; set; }

        [JsonProperty("response")]
        public Response? Response { get; set; }

        [JsonIgnore()]
        public Dictionary<string, string>? Variables { get; set; }
    }

    public class Response
    {

        [JsonProperty("headers")]
        public Dictionary<string, string>? Headers { get; set; }

        [JsonProperty("status-code")]
        public int? StatusCode { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("statusInt")]
        public int? Status { get; set; }
    }

    public class ApiRequests
    {
        [JsonProperty("Requests")]
        public List<Request> Requests { get; set; }

        [JsonProperty("variables_for_response_body")]
        public Dictionary<string, string>? VariablesForResponseBody { get; set; }
    }
}
