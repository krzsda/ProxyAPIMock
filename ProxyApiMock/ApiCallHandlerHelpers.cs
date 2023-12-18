namespace ProxyApiMock
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Serilog;
    using System.Diagnostics;
    using System.Dynamic;
    using System.Reflection;
    using System.Xml;
    using System.Xml.Linq;

    using System.Net.Http;

    public static class ApiCallHandlerHelpers
    {

        public static Task RemoveRestrictedHeaders(HttpRequestMessage request)
        {

            // Add headers to the request removing problematic ones
            foreach (var header in request.Headers)
            {
                // list of headers that should not be forwarded to the real API
                if (header.Key.Contains("Postman")
                    || header.Key.Contains("Host")
                    || header.Key.Contains("Accept-Encoding"))
                {
                    request.Headers.Remove(header.Key);
                    continue;
                }
            }

            return Task.CompletedTask;
        }

        public static string FindValueInBody(string body, string parameter)
        {

            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            body = body.Trim();

            try
            {
                var jsonObj = JObject.Parse(body);
                return FindValueInJObject(jsonObj, parameter);
            }
            catch (JsonReaderException)
            {
            }


            try
            {
                var xmlObj = XDocument.Parse(body);
                var atribute = xmlObj.Descendants()
                    .SelectMany(e => e.Attributes())
                    .FirstOrDefault(a => string.Equals(a.Name.LocalName, parameter, StringComparison.InvariantCultureIgnoreCase));

                if (atribute != null)
                {
                    return atribute.Value;
                }
                var element = xmlObj.Descendants()
                    .FirstOrDefault(e => e.Attributes()
                        .Any(a => a.Name.LocalName.Equals("name", StringComparison.InvariantCultureIgnoreCase) &&
                                  a.Value.Equals(parameter, StringComparison.InvariantCultureIgnoreCase)));
                if (element != null)
                {
                    return element.Value;
                }
            }
            catch (XmlException)
            {
            }

            return string.Empty;
        }

        private static string FindValueInJObject(JToken token, string key)
        {
            if (token.Type == JTokenType.Object)
            {
                foreach (var child in token.Children<JProperty>())
                {
                    if (string.Equals(child.Name, key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return child.Value.ToString();
                    }

                    var result = FindValueInJObject(child.Value, key);
                    if (!string.IsNullOrEmpty(result))
                    {
                        return result;
                    }
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var child in token.Children())
                {
                    var result = FindValueInJObject(child, key);
                    if (!string.IsNullOrEmpty(result))
                    {
                        return result;
                    }
                }
            }

            return string.Empty;
        }

        private static Uri NormalizeStringUri(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL cannot be null or empty.", nameof(url));
            }

            // Replace all backslashes with forward slashes
            string normalizedUrl = url.Replace("\\", "/");

            // Correctly format the protocol part
            if (normalizedUrl.StartsWith("http:/") && !normalizedUrl.StartsWith("http://"))
            {
                normalizedUrl = "http://" + normalizedUrl.Substring(6);
            }
            else if (normalizedUrl.StartsWith("https:/") && !normalizedUrl.StartsWith("https://"))
            {
                normalizedUrl = "https://" + normalizedUrl.Substring(7);
            }

            // Handle double forward slashes in the rest of the URL
            int protocolEnd = normalizedUrl.IndexOf("://") + 3;
            string protocol = normalizedUrl.Substring(0, protocolEnd);
            string restOfUrl = normalizedUrl.Substring(protocolEnd).Replace("//", "/");

            return new Uri(protocol + restOfUrl);
        }

        public static Uri GetUri(string baseUrl, string endpoint)
        {
            return new Uri(NormalizeStringUri(baseUrl), endpoint);
        }
    }
}