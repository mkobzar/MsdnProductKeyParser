using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace xmlToJson
{
    class parseKeys
    {
        static List<KeysSet> KeysSets = new List<KeysSet>();
        static int KnownKeysCount = 0;
        static int KnownProductCount = 0;
        static List<string> CsvKeys = new List<string>();
        static string CvsHeader = "name,key,type,ClaimedDate,id,note";

        static void Main(string[] args)
        {
            try
            {
                var folder = args != null && args.Count() > 0 && Directory.Exists(args[0]) ? args[0] : Directory.GetCurrentDirectory();
                var direcoryInfo = new DirectoryInfo(folder);
                var xmlFiles = direcoryInfo.GetFiles("*.xml", SearchOption.TopDirectoryOnly);

                if (xmlFiles.Length == 0)
                {
                    Console.WriteLine($"{folder} does not contain any xml files\nProvide folder path with key files");
                    return;
                }

                var csvFile = $"{direcoryInfo.FullName}\\keys.csv";
                if (File.Exists(csvFile))
                    ParseCsvFile(csvFile);
                StoreKnownKeys();
                foreach (var fileInfo in xmlFiles)
                {
                    var file = fileInfo.FullName;
                    ParseXmlFile(file);
                }

                if (KeysSets.Count == 0)
                {
                    Console.WriteLine("no keys found in given folder");
                    return;
                }

                Integrate();

                var totalKeysCount = KeysSets.SelectMany(x => x.Keys).Distinct().Count();
                var totalProductCount = KeysSets.SelectMany(x => x.Products).Distinct().Count();

                Console.WriteLine($"total distinct key count: {totalKeysCount} and total distinct product count: {totalProductCount}\n" +
                    $"found {totalKeysCount - KnownKeysCount} new keys for new {totalProductCount - KnownProductCount} procucts");

                var jsonStr = JsonConvert.SerializeObject(KeysSets, Newtonsoft.Json.Formatting.Indented);
                var jsonFile = $"{direcoryInfo.FullName}\\keys_{DateTime.Now.ToString("yyyy-dd-MM_HH-mm-ss")}.json";
                if (File.Exists(jsonFile)) File.Delete(jsonFile);
                File.WriteAllText(jsonFile, jsonStr);
                CsvKeys = CsvKeys.OrderBy(x => x).ToList();
                CsvKeys.Insert(0, CvsHeader);
                File.WriteAllLines(csvFile, CsvKeys);
                Console.WriteLine($"file exported and saved as {jsonFile} and {csvFile}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        

        static void StoreKnownKeys()
        {
            Integrate();
            KnownKeysCount = KeysSets.SelectMany(x => x.Keys).Distinct().Count();
            KnownProductCount = KeysSets.SelectMany(x => x.Products).Distinct().Count();
        }

        static void Integrate()
        {
            var newKeysSets = new List<KeysSet>();
            foreach (var keysSet in KeysSets)
            {
                keysSet.Keys = keysSet.Keys.Distinct().ToList();
                keysSet.Products = keysSet.Products.Distinct().OrderBy(x => x).ToList();

                var n = newKeysSets.FirstOrDefault(x => IsListsOverlapping(keysSet.Keys, x.Keys) || IsListsOverlapping(keysSet.Products, x.Products));
                if (n != null)
                {
                    var nk = keysSet.Keys.Except(n.Keys).ToList();
                    var np = keysSet.Products.Except(n.Products).ToList();
                    if (nk.Count > 0)
                    {
                        n.Keys.AddRange(nk);
                    }
                    if (np.Count > 0)
                    {
                        n.Products.AddRange(np);
                    }
                }
                else
                {
                    newKeysSets.Add(keysSet);
                }
            }
            KeysSets = newKeysSets;
        }

        static void ParseCsvFile(string file)
        {
            try
            {
                var fileContent = File.ReadAllLines(file);
                if (fileContent == null || fileContent.Length == 0)
                {
                    Console.WriteLine($"{file} Content is empty");
                    return;
                }
                for (var i = 1; i < fileContent.Length; i++)
                {
                    if (!CsvKeys.Contains(fileContent[i]))
                        CsvKeys.Add(fileContent[i]);
                    var chunks = fileContent[i].Split(',');
                    if (chunks.Length < 2) continue;
                    var name = $"{chunks[0]}; {chunks[2]}";
                    var key = chunks[1];
                    if (KeysSets.Any(x => x.Keys.Contains(key) && x.Products.Contains(name)))
                    {
                        // ignore
                    }
                    else if (KeysSets.Any(x => x.Products.Contains(name)))
                    {
                        // find by name. add key 
                        KeysSets.FirstOrDefault(x => x.Products.Contains(name)).Keys.Add(key);
                    }
                    else
                    {
                        // new group
                        KeysSets.Add(new KeysSet { Products = new List<string>() { name }, Keys = new List<string>() { key } });
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{file} {e.Message}");
            }
        }

        static void ParseXmlFile(string file)
        {
            try
            {
                var yourKey = ReadXmlFile(file);
                if (yourKey == null) return;
                Console.WriteLine(file);
                yourKey.Product_Key.ForEach(p =>
                {
                    p.Key.ForEach(k =>
                    {
                        var key = k.Text?.Trim();
                        if (!string.IsNullOrWhiteSpace(key) && !key.Contains("<") && !key.Contains("key"))
                        {
                            var name = p.Name.Trim().Replace(',', ';');
                            var keyRetrievalNote = p.KeyRetrievalNote?.Trim().Replace(',', ';');
                            var csv = $"{name},{key},{k.Type?.Trim()},{k.ClaimedDate?.Trim()},{k.ID?.Trim()},{keyRetrievalNote}";

                            if (!CsvKeys.Contains(csv))
                                CsvKeys.Add(csv);
                            name = $"{name}; {k.Type}";
                            if (KeysSets.Any(x => x.Keys.Contains(key) && x.Products.Contains(name)))
                            {
                                // ignore
                            }
                            else if (KeysSets.Any(x => x.Products.Contains(name)))
                            {
                                // find by name. add key 
                                KeysSets.FirstOrDefault(x => x.Products.Contains(name)).Keys.Add(key);
                            }
                            else
                            {
                                // new group
                                KeysSets.Add(new KeysSet { Products = new List<string>() { name }, Keys = new List<string>() { key } });
                            }
                        }
                    });
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"{file} {e.Message}");
            }
        }

        static bool IsListsOverlapping(IEnumerable<string> list1, IEnumerable<string> list2)
        {
            if (list1 == null || list2 == null)
                return false;
            return list1.Any(x => list2.Any(y => string.Equals(x, y, StringComparison.InvariantCultureIgnoreCase)));
        }

        static YourKey ReadXmlFile(string file)
        {
            var fileContent = File.ReadAllText(file);
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                Console.WriteLine($"{file} Content is empty");
                return null;
            }

            var doc = new XmlDocument();
            doc.LoadXml(fileContent);

            var json = JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.Indented, false);

            if (string.IsNullOrWhiteSpace(json))
            {
                Console.WriteLine($"{file} after JsonConvert, JSON content is empty");
                return null;
            }

            json = json.Replace("@", "").Replace("#", "");

            var keysFile = JsonConvert.DeserializeObject<KeysFile>(json);
            if (keysFile?.root?.YourKey != null)
            {
                if (keysFile?.root?.YourKey?.Product_Key != null && keysFile.root.YourKey.Product_Key.Any())
                    return keysFile.root?.YourKey;
            }
            else if (keysFile?.root?.YourKey == null)
            {
                var yourKey = JsonConvert.DeserializeObject<Root>(json);
                if (yourKey?.YourKey?.Product_Key != null && yourKey.YourKey.Product_Key.Any())
                {
                    return yourKey.YourKey;
                }
            }

            Console.WriteLine($"{file} does not contains any keys or could not Deserialize it");
            return null;
        }
    }

    public class SingleValueArrayConverter<T> : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            object retVal = new Object();
            if (reader.TokenType == JsonToken.StartObject)
            {
                T instance = (T)serializer.Deserialize(reader, typeof(T));
                retVal = new List<T>() { instance };
            }
            else if (reader.TokenType == JsonToken.StartArray)
            {
                retVal = serializer.Deserialize(reader, objectType);
            }
            return retVal;
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }
    }

    public class Key
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public string Type { get; set; }
        public string ClaimedDate { get; set; }
        public string Text { get; set; }
    }

    public class Product_Key
    {
        [JsonConverter(typeof(SingleValueArrayConverter<Key>))]
        public List<Key> Key { get; set; }
        public string Name { get; set; }
        public string KeyRetrievalNote { get; set; }
    }

    public class YourKey
    {
        public List<Product_Key> Product_Key { get; set; }
    }

    public class KeysFile
    {
        public Root root { get; set; }
    }

    public class Root
    {
        public YourKey YourKey { get; set; }
    }

    public class KeysSet
    {
        public List<string> Products { get; set; }
        public List<string> Keys { get; set; }
    }
}
