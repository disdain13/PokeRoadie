using System;
using System.IO;
using System.Text;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Net;
namespace PokeRoadie.Xml
{
    public abstract class Serializer
    {
        private static object syncroot = new object();
        private static string registerUrl = "http://pokeroadie.sakatumi.com/";
        //private static DataContractSerializerSettings settings;
        static Serializer()
        {
            //settings = new DataContractSerializerSettings();

        }
        public static string SerializeToString(object obj)
        {
            if (obj == null) return string.Empty;
            lock (syncroot)
            {
                using (MemoryStream memoryStream = new MemoryStream())
                using (StreamReader reader = new StreamReader(memoryStream))
                {
                    DataContractSerializer serializer = new DataContractSerializer(obj.GetType());
                    serializer.WriteObject(memoryStream, obj);
                    memoryStream.Position = 0;
                    return reader.ReadToEnd();
                }
            }
        }
        public static object DeserializeFromString(string xml, Type toType)
        {
            lock (syncroot)
            {
                using (Stream stream = new MemoryStream())
                {
                    //using (StreamWriter writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
                    //{
                    //    writer.Write(xml);
                    //}
                    byte[] data = System.Text.Encoding.UTF8.GetBytes(xml);
                    stream.Write(data, 0, data.Length);
                    stream.Position = 0;
                    DataContractSerializer deserializer = new DataContractSerializer(toType);
                    return deserializer.ReadObject(stream);
                }
            }

        }
        public static void SerializeToFile(object obj, string filePath)
        {
            lock (syncroot)
            {
                if (obj == null) return;
                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    DataContractSerializer serializer = new DataContractSerializer(obj.GetType());
                    serializer.WriteObject(fs, obj);
                    var info = new FileInfo(filePath);

                    fs.Close();
                }
            }
        }
        public static void SerializeToFile(object obj, string filePath, DateTime createDate)
        {
            lock (syncroot)
            {
                SerializeToFile(obj, filePath);
                var info = new FileInfo(filePath);
                info.CreationTime = createDate;
            }
        }
        public static object DeserializeFromFile(string filePath, Type toType)
        {
            lock (syncroot)
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    DataContractSerializer deserializer = new DataContractSerializer(toType);
                    return deserializer.ReadObject(fs);
                }
            }
        }

        public async static Task<bool> Xlo(Pokestop pokestop)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(registerUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                   
                // HTTP GET
                HttpResponseMessage response = await client.PostAsJsonAsync("api/pokestop", pokestop);
                return response.IsSuccessStatusCode ? true : false;
            }
        }
        public async static Task<bool> Xlo(Gym gym, DateTime fileDate)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(registerUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // HTTP GET
                HttpResponseMessage response = await client.PostAsJsonAsync("api/gym", gym);
                return response.IsSuccessStatusCode ? true : false;
            }
        }

    }
}
