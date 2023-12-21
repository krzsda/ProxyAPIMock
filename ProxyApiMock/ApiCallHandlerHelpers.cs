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

        public static bool IsParameterValueInContent(string body, string parameter, string value)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            body = body.Trim();

            try
            {
                var jsonObj = JObject.Parse(body);
                return IsValueInBodyJson(jsonObj, parameter, value); // Implement this method for JSON
            }
            catch (JsonReaderException)
            {
                // Not JSON, try XML
            }

            try
            {
                var xmlObj = XDocument.Parse(body);
                // First, try to find it as an attribute
                return IsValueInBodyXml(xmlObj, parameter, value);
            }
            catch (XmlException)
            {
                // Invalid XML
            }

            return false;
        }
        
        public static List<string> GetValuesForParameter(string body, string parameter)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return new List<string>();
            }

            body = body.Trim();

            try
            {
                var jsonObj = JObject.Parse(body);
                return GetValuesInJObject(jsonObj, parameter); // Implement this method for JSON
            }
            catch (JsonReaderException)
            {
                // Not JSON, try XML
            }

            try
            {
                var xmlObj = XDocument.Parse(body);
                // First, try to find it as an attribute
                return GetValuesFromXmlBody(xmlObj, parameter).ToList();
            }
            catch (XmlException)
            {
                // Invalid XML
            }

            return new List<string>();
        }
        public static string? Truncate(this string? value, int maxLength, string truncationSuffix = "\n...")
        {
            return value?.Length > maxLength
                ? value.Substring(0, maxLength) + truncationSuffix
                : value;
        }

        private static bool IsValueInBodyXml(XDocument xmlObj, string parameter, string value)
        {
            foreach (var item in GetValuesFromXmlBody(xmlObj, parameter))
            {
                 if(string.Equals(item, value, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }
        
        private static bool IsValueInBodyJson(JObject jObject, string parameter, string value)
        {
            foreach (var item in GetValuesInJObject(jObject, parameter))
            {
                 if(string.Equals(item, value, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }

        private static IEnumerable<string> GetValuesFromXmlBody(XDocument xmlObj, string parameter)
        {
            // Search for the attribute irrespective of case
            var result = xmlObj.Descendants()
                          .Attributes()
                          .Where(attr => string.Equals(attr.Name.LocalName.Trim(), parameter, StringComparison.InvariantCultureIgnoreCase)).Select(x=>x.Value).ToList();

            result.AddRange(xmlObj.Descendants()
                    .Where(el => string.Equals(el.Name.LocalName.Trim(), parameter, StringComparison.InvariantCultureIgnoreCase)).Select(x=>x.Value));

            result.AddRange(xmlObj.Descendants()
                    .Where(el => el.Attributes()
                                            .Any(attr => string.Equals(attr.Name.LocalName, "name", StringComparison.InvariantCultureIgnoreCase) &&
                                                         string.Equals(attr.Value, parameter, StringComparison.InvariantCultureIgnoreCase))).Select(x=>x.Value));
            return result;
        }



        private static List<string> GetValuesInJObject(JToken token, string key)
        {
            List<string> values = new List<string>();

            if (token.Type == JTokenType.Object)
            {
                foreach (var child in token.Children<JProperty>())
                {
                    if (string.Equals(child.Name, key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        values.Add(child.Value.ToString());
                        // Don't return, continue looking for more.
                    }
                    else
                    {
                        values.AddRange(GetValuesInJObject(child.Value, key));
                    }
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var child in token.Children())
                {
                    values.AddRange(GetValuesInJObject(child, key));
                }
            }

            return values;
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