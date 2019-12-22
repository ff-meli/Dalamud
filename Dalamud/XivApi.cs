using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Dalamud
{
    class XivApi
    {
        private const string URL = "http://xivapi.com/";

        private static readonly Dictionary<string, JObject> cachedResponses = new Dictionary<string, JObject>();

        public static async Task<JObject> GetWorld(int world)
        {
            var res = await Get("World/" + world);

            return res;
        }

        public static async Task<JObject> GetClassJob(int id)
        {
            var res = await Get("ClassJob/" + id);

            return res;
        }

        public static async Task<JObject> GetFate(int id)
        {
            var res = await Get("Fate/" + id);

            return res;
        }

        public static async Task<JObject> GetCharacterSearch(string name, string world)
        {
            var res = await Get("character/search" + $"?name={name}&server={world}");

            return res;
        }

        public static async Task<JObject> GetContentFinderCondition(int contentFinderCondition) {
            return await Get("ContentFinderCondition/" + contentFinderCondition);
        }

        public static async Task<JObject> Search(string query, string indexes, string columns, int limit = 100) {

            // this is ugly, but it's less ugly than making this tree with actual classes...
            var postData = new
            {
                indexes = indexes,
                columns = columns,
                body = new
                {
                    query = new
                    {
                        wildcard = new
                        {
                            // This uses NameCombined_en because that is what the xivapi sample uses.
                            // "Name" no longer works as a search key, though there are language variants that do (eg, Name_en, Name_fr).
                            // NameLocale also works, but is a single string containing all 4 languages, so it may result in some false positives.

                            // If we want to support localization so users can query items their own language, we'll need to 
                            // subclass DefaultContractResolver and override CreateProperty() to change the name of this property
                            // in the json that is sent in the api request

                            // lowercased because it fails otherwise...
                            NameCombined_en = $"*{query.ToLowerInvariant()}*"
                        }
                    },
                    from = 0,
                    size = limit
                }
            };

            return await Post("search", postData);
        }

        public static async Task<JObject> GetMarketInfoWorld(int itemId, string worldName) {
            return await Get($"market/{worldName}/item/{itemId}", true);
        }

        public static async Task<JObject> GetMarketInfoDc(int itemId, string dcName) {
            return await Get($"market/item/{itemId}?dc={dcName}", true);
        }

        public static async Task<JObject> GetItem(int itemId) {
            return await Get($"Item/{itemId}", true);
        }

        public static async Task<dynamic> Get(string endpoint, bool noCache = false)
        {
            Log.Verbose("XIVAPI FETCH: {0}", endpoint);

            if (cachedResponses.ContainsKey(endpoint) && !noCache)
                return cachedResponses[endpoint];

            var client = new HttpClient();
            var response = await client.GetAsync(URL + endpoint);
            var result = await response.Content.ReadAsStringAsync();

            var obj = JObject.Parse(result);

            if (!noCache)
                cachedResponses.Add(endpoint, obj);

            return obj;
        }

        // no cache handling on here since it's a bit odd for post data in general, but we'd also have to
        // store and compare the entire postData object
        public static async Task<dynamic> Post(string endpoint, object postData)
        {
            Log.Verbose("XIVAPI POST: {0}", endpoint);

            var json = JsonConvert.SerializeObject(postData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using (var client = new HttpClient())
            {
                var response = await client.PostAsync(URL + endpoint, content);
                var result = await response.Content.ReadAsStringAsync();

                return JObject.Parse(result);
            }
        }
    }
}
