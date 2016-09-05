#region " Imports "

using System;
using System.IO;
using PokemonGo.RocketAPI.Logging;

#endregion

namespace PokeRoadie
{
    public class Session
    {
         
        public Context Context { get; private set; }

        public Session(Context context)
        {
            Context = context;
            //if (!string.IsNullOrWhiteSpace(context.Settings.Username)) SessionFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", context.Settings.Username);
        }


        //public bool SaveSession(SessionData session)
        //{
        //    if (!Directory.Exists(temp_path)) Directory.CreateDirectory(temp_path);
        //    string fileName = "Session.xml";
        //    string filePath = Path.Combine(temp_path, fileName);
        //    try
        //    {
        //        lock (sessionRoot)
        //        {
        //            if (File.Exists(filePath)) File.Delete(filePath);
        //            using (FileStream s = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
        //            {
        //                var x = new System.Xml.Serialization.XmlSerializer(typeof(SessionData));
        //                x.Serialize(s, session);
        //                s.Close();
        //                return true;
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Write($"Could not save {filePath}. Error: " + e.ToString(), LogLevel.Error);
        //    }
        //    return false;
        //}

        //public SessionData LoadSession(string filePath)
        //{
        //    if (File.Exists(filePath))
        //    {
        //        try
        //        {
        //            lock (sessionRoot)
        //            {
        //                if (!Directory.Exists(temp_path)) Directory.CreateDirectory(temp_path);
        //                using (FileStream s = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        //                {
        //                    var x = new System.Xml.Serialization.XmlSerializer(typeof(SessionData));
        //                    var session = (SessionData)x.Deserialize(s);
        //                    s.Close();
        //                    return session;
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Logger.Write($"The {filePath} file could not be loaded, a new session will be created. {ex.Message} {ex.ToString()}", LogLevel.Warning);
        //        }
        //    }
        //    return NewSession();
        //}

        public SessionData NewSession()
        {
            var session = new SessionData();
            session.StartDate = DateTime.Now;
            return session;
        }
    }
}
