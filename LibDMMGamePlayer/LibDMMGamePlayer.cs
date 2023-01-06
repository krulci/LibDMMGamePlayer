using System.Data;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Data.SQLite;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using LibDMMGamePlayer.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LibDMMGamePlayer
{
    public class LibDMMGamePlayer
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly string cookieDbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dmmgameplayer5\\Network\\Cookies");
        private readonly HttpClient _client;
        public LibDMMGamePlayer(IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _client = _httpClientFactory.CreateClient("DMMGamePlayer");
        }
        public async Task<string?> GetTokenizedLoginUrl()
        {
            using HttpResponseMessage response = await _client.GetAsync("https://apidgp-gameplayer.games.dmm.com/v5/loginurl");
            JToken result = JToken.Parse(await response.Content.ReadAsStringAsync());
            return result?["data"]?["url"]?.ToString();
        }
        public async Task<CookieContainer?> UpdateCookies(string? url)
        {
            if (url == null) return null;
            CookieContainer returnedContainer = new();
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            this.AddHeadersWithCookies(request);
            HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            Uri? redirectUri;
            while (response.Headers.TryGetValues("set-cookie", out IEnumerable<string>? setCookieValues))
            {
                if (response.RequestMessage == null) break;
                if (response.RequestMessage.RequestUri == null) break;
                Uri responseUri = response.RequestMessage.RequestUri;
                CookieContainer cookieContainer = new();
                foreach (string setCookieValue in setCookieValues)
                {
                    cookieContainer.SetCookies(responseUri, setCookieValue);
                }
                CookieCollection cookies = cookieContainer.GetAllCookies();
                foreach (Cookie cookie in cookies.Cast<Cookie>())
                {
                    if (cookie.Name == "login_secure_id")
                    {
                        returnedContainer.Add(new Uri("http://apidgp-gameplayer.games.dmm.com"), cookie);
                    }
                    if (cookie.Name == "login_session_id")
                    {
                        returnedContainer.Add(new Uri("http://apidgp-gameplayer.games.dmm.com"), cookie);
                    }
                }
                this.SaveCookies(cookies);
                if (request.RequestUri == null) break;
                if ((redirectUri = LibDMMGamePlayer.GetUriForRedirect(request.RequestUri, response)) != null)
                {
                    response.Dispose();
                    HttpRequestMessage redirectedRequest = new(HttpMethod.Get, url)
                    {
                        RequestUri = redirectUri
                    };
                    this.AddHeadersWithCookies(redirectedRequest, cookieContainer);
                    response = await _client.SendAsync(redirectedRequest);
                }
                else
                {
                    break;
                }
            }
            response.Dispose();
            returnedContainer.Add(new Uri("http://apidgp-gameplayer.games.dmm.com"), new Cookie("age_check_done", "0"));
            return returnedContainer;
        }
        public async Task<string?> GetExecutionArguments(CookieContainer? CookieContainer, string product_id = "priconner")
        {
            if (CookieContainer == null) return null;
            LaunchhModel launchhModel = new()
            {
                product_id = product_id,
                game_type = "GCL",
                game_os = "win",
                launch_type = "LIB",
                mac_address = GenRandomAddress(),
                hdd_serial = GenRandomHex(),
                motherboard = GenRandomHex(),
                user_os = "win",
            };
            using HttpRequestMessage request = new(HttpMethod.Post, new Uri("https://apidgp-gameplayer.games.dmm.com/v5/launch/cl"));
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Accept-Language", "en-US");
            request.Headers.Add("User-Agent", "DMMGamePlayer5-Win/5.1.35 Electron/20.3.4");
            request.Headers.Add("Client-App", "DMMGamePlayer5");
            request.Headers.Add("Client-version", "5.1.35");
            request.Headers.Add("Sec-Fetch-Dest", "empty");
            request.Headers.Add("Sec-Fetch-Mode", "no-cors");
            request.Headers.Add("Sec-Fetch-Site", "none");
            request.Headers.Add("Connection", "keep-alive");
            request.Headers.Add("Host", "apidgp-gameplayer.games.dmm.com");
            request.Headers.Add("cookie",
                string.Join("; ", CookieContainer.GetAllCookies()
                .OfType<Cookie>()
                .Where(c => c.Name != "has_alttime")
                .Select(c => $"{c.Name}={c.Value}")));
            request.Content = new StringContent(JsonConvert.SerializeObject(launchhModel), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _client.SendAsync(request);
            JToken result = JToken.Parse(await response.Content.ReadAsStringAsync());
            if (result.Value<int>("result_code") == 203)
            {
                _logger.LogError("login required");
                return null;
            }
            else
            {
                return result?["data"]?["execute_args"]?.ToString();
            }
        }

        #region Private Methods
        private static string GenRandomHex()
        {
            // Convert the input string to a byte array and compute the hash.
            byte[] data = SHA256.HashData(BitConverter.GetBytes(new Random().NextDouble()));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
        private static string GenRandomAddress()
        {
            string hex = LibDMMGamePlayer.GenRandomHex();
            string address = "";
            for (int x = 0; x < 12; x++)
            {
                address += hex[x];
                if (x % 2 == 1)
                {
                    address += ":";
                }
            }
            return address.Substring(0, address.Length - 1);
        }
        private void SaveCookies(CookieCollection cookies)
        {
            Process[] localByName = Process.GetProcessesByName("DMMGamePlayer");
            if (localByName.Length != 0)
            {
                _logger.LogError("Found processes locking cookie file; Attempting to kill process");
                for (int i = 0; i < localByName.Length; i++)
                {
                    localByName[i].Kill();
                }
                _logger.LogInformation("Successfully killed all processes locking cookie file");
            }
            string connString = string.Format("Data Source={0}", cookieDbPath);
            using SQLiteConnection db = new(connString);
            db.Open();

            foreach (Cookie cookie in cookies.Cast<Cookie>())
            {
                string commandText = "UPDATE cookies SET value = @value, path = @path, expires_utc = @expires WHERE name = @name";
                using SQLiteCommand command = new(commandText, db);
                command.Parameters.AddWithValue("@name", cookie.Name);
                command.Parameters.AddWithValue("@value", cookie.Value);
                command.Parameters.AddWithValue("@path", cookie.Path);
                command.Parameters.AddWithValue("@expires", cookie.Expires.ToWebkitTimestamp());
                command.ExecuteNonQuery();
            }
        }
        private CookieContainer LoadCookies()
        {
            CookieContainer container = new() { PerDomainCapacity = 50 };
            string connString = string.Format("Data Source={0}", cookieDbPath);
            using (SQLiteConnection db = new(connString))
            {
                db.Open();
                using SQLiteCommand cmd = new("SELECT * FROM cookies", db);
                using SQLiteDataReader reader = cmd.ExecuteReader();
                int i = 1;
                while (reader.Read())
                {
                    string name = reader.GetString(3);
                    string value = reader.GetString(4);
                    string domain = reader.GetString(1);
                    string path = reader.GetString(6);
                    bool secure = reader.GetBoolean(8);
                    DateTime expires = reader.GetInt64(7).FromWebkitTimestamp();

                    Cookie cookie = new(name, value, path, domain)
                    {
                        Secure = secure,
                        Expires = expires,
                    };
                    container.Add(cookie);
                    i++;
                }
            }

            return container;
        }
        private void AddHeadersWithCookies(HttpRequestMessage request, CookieContainer? cookieSetters = null)
        {
            CookieContainer cookieContainer = LoadCookies();
            if (cookieSetters != null)
            {
                foreach (Cookie cookie in cookieSetters.GetAllCookies().Cast<Cookie>())
                {
                    cookieContainer.Add(cookie);
                }
            }
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Accept-Language", "en-US");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) dmmgameplayer5/5.1.35 Chrome/104.0.5112.124 Electron/20.3.4 Safari/537.36");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-Site", "none");
            request.Headers.Add("Sec-Fetch-User", "?1");
            request.Headers.Add("Connection", "keep-alive");
            request.Headers.Add("Cookie",
                string.Join("; ", cookieContainer.GetCookies(new Uri("https://" + request.RequestUri?.Host))
                .OfType<Cookie>()
                .Select(c => $"{c.Name}={c.Value}")));
        }
        private static Uri? GetUriForRedirect(Uri requestUri, HttpResponseMessage response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Moved:
                case HttpStatusCode.Found:
                case HttpStatusCode.SeeOther:
                case HttpStatusCode.TemporaryRedirect:
                case HttpStatusCode.MultipleChoices:
                case HttpStatusCode.PermanentRedirect:
                    break;

                default:
                    return null;
            }

            Uri? location = response.Headers.Location;
            if (location == null)
            {
                return null;
            }

            // Ensure the redirect location is an absolute URI.
            if (!location.IsAbsoluteUri)
            {
                location = new Uri(requestUri, location);
            }

            // Per https://tools.ietf.org/html/rfc7231#section-7.1.2, a redirect location without a
            // fragment should inherit the fragment from the original URI.
            string requestFragment = requestUri.Fragment;
            if (!string.IsNullOrEmpty(requestFragment))
            {
                string redirectFragment = location.Fragment;
                if (string.IsNullOrEmpty(redirectFragment))
                {
                    location = new UriBuilder(location) { Fragment = requestFragment }.Uri;
                }
            }

            return location;
        }
        #endregion
    }
    public static class ExtensionMethods
    {
        public static DateTime FromWebkitTimestamp(this long timestamp)
            => new DateTime(1601, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddTicks(timestamp * TimeSpan.TicksPerMicrosecond);
        public static long ToWebkitTimestamp(this DateTime timestamp)
            => (timestamp - new DateTime(1601, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).Ticks / TimeSpan.TicksPerMicrosecond;
    }
}