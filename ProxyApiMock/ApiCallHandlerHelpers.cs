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
    using System.Text.RegularExpressions;

    public static class ApiCallHandlerHelpers
    {
        public static Uri GetUri(string baseUrl, string endpoint)
        {
            return new Uri(NormalizeStringUri(baseUrl), endpoint);
        }


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
                return FindValueInJObject(jsonObj, parameter); // Implement this method for JSON
            }
            catch (JsonReaderException)
            {
                // Not JSON, try XML
            }

            try
            {
                var xmlObj = XDocument.Parse(body);
                // First, try to find it as an attribute
                return FindValueInBodyXml(xmlObj, parameter);
            }
            catch (XmlException)
            {
                // Invalid XML
            }

            return string.Empty;
        }

        private static string? FindValueInBodyXml(XDocument xmlObj, string parameter)
        {
            // Search for the attribute irrespective of case
            var attribute = xmlObj.Descendants()
                          .Attributes()
                          .FirstOrDefault(attr => string.Equals(attr.Name.LocalName, parameter, StringComparison.InvariantCultureIgnoreCase));
            if (attribute != null)
            {
                return attribute.Value;
            }

            // If not found as an attribute, try to find it as an element, also irrespective of case
            var element = xmlObj.Descendants()
                                .FirstOrDefault(el => string.Equals(el.Name.LocalName, parameter, StringComparison.InvariantCultureIgnoreCase));
            if (element != null)
            {
                return element.Value;
            }

            // Search for an element with an attribute 'name' matching the parameter
            element = xmlObj.Descendants()
                                .FirstOrDefault(el => el.Attributes()
                                                        .Any(attr => string.Equals(attr.Name.LocalName, "name", StringComparison.InvariantCultureIgnoreCase) &&
                                                                     string.Equals(attr.Value, parameter, StringComparison.InvariantCultureIgnoreCase)));
            if (element != null)
            {
                return element.Value.Trim();
            }

            return null;
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

        public static string FindTextInstance(string input)
        {
            var pattern = @"text\/([^;]+)";
            var match = Regex.Match(input, pattern);

            if (match.Success && match.Groups.Count > 1)
            {
                return "text/" + match.Groups[1].Value.Trim();
            }

            return null;
        }
    }
}