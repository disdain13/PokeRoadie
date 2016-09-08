#region " Imports "

using System;
using System.IO;
using System.Xml.Serialization;
using System.Threading.Tasks;
using PokeRoadie.Api.Logging;

#endregion

namespace PokeRoadie
{
    public class Session
    {

        #region " Members "

        private SessionData _current;
        private const string sessionFileName = "Session.xml";

        #endregion
        #region " Properties "

        public Context Context { get; private set; }

        public object SyncRoot { get; private set; }

        [XmlIgnore()]
        public SessionData Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Load(Path.Combine(Context.Directories.TempDirectory, sessionFileName));
                return _current;
            }
            set
            {
                _current = value;
            }
        }

        #endregion
        #region " Constructors "

        public Session(Context context)
        {
            SyncRoot = new object();
            Context = context;
        }

        #endregion
        #region " Methods "

        public bool Save()
        {
            if (Current != null) return Save(Current);
            return false;
        }
        public bool Save(SessionData session)
        {
            if (!Directory.Exists(Context.Directories.TempDirectory)) Directory.CreateDirectory(Context.Directories.TempDirectory);
            string fileName = sessionFileName;
            string filePath = Path.Combine(Context.Directories.TempDirectory, fileName);
            try
            {
                lock (SyncRoot)
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                    using (FileStream s = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        var x = new System.Xml.Serialization.XmlSerializer(typeof(SessionData));
                        x.Serialize(s, session);
                        s.Close();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Write($"Could not save session to {filePath}. Error: " + e.ToString(), LogLevel.Error);
            }
            return false;
        }

        public SessionData Load(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    lock (SyncRoot)
                    {
                        if (!Directory.Exists(Context.Directories.TempDirectory)) Directory.CreateDirectory(Context.Directories.TempDirectory);
                        using (FileStream s = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            var x = new System.Xml.Serialization.XmlSerializer(typeof(SessionData));
                            var session = (SessionData)x.Deserialize(s);
                            s.Close();
                            return session;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write($"The {filePath} file could not be loaded, a new session will be created. {ex.Message} {ex.ToString()}", LogLevel.Warning);
                }
            }
            return Create();
        }

        public SessionData Create()
        {
            var session = new SessionData();
            session.StartDate = DateTime.Now;
            return session;
        }

        public async Task Check(bool isStart = false)
        {
            var maxTimespan = TimeSpan.Parse(Context.Settings.MaxRunTimespan);
            var minBreakTimespan = TimeSpan.Parse(Context.Settings.MinBreakTimespan);
            var nowdate = DateTime.Now;
            var session = Context.Session.Current;
            var endDate = session.StartDate.Add(maxTimespan);
            var totalEndDate = endDate.Add(minBreakTimespan);

            //session is still active
            if (session.PlayerName == Context.Settings.Username && endDate > nowdate)
            {
                if (Context.Session.Current.CatchEnabled && Context.Session.Current.CatchCount >= Context.Settings.MaxPokemonCatches)
                {
                    Context.Session.Current.CatchEnabled = false;
                    Logger.Write($"Limit reached! The bot caught {Context.Session.Current.CatchCount} pokemon since {session.StartDate}.", LogLevel.Warning);
                }
                if (Context.Session.Current.VisitEnabled && Context.Session.Current.VisitCount >= Context.Settings.MaxPokestopVisits)
                {
                    Context.Session.Current.VisitEnabled = false;
                    Logger.Write($"Limit reached! The bot visited {Context.Session.Current.VisitCount} pokestops since {session.StartDate}.", LogLevel.Warning);
                }
                if (!Context.Session.Current.CatchEnabled && !Context.Session.Current.VisitEnabled)
                {
                    var diff = totalEndDate.Subtract(nowdate);
                    Logger.Write($"All limits reached! Visited {Context.Session.Current.VisitCount} pokestops, caught {Context.Session.Current.CatchCount} pokemon since {session.StartDate}. Waiting until {totalEndDate.ToShortTimeString()}...", LogLevel.Warning);
                    await Task.Delay(diff);
                    var s = Context.Session.Create();
                    s.PlayerName = Context.Settings.Username;
                    Context.Session.Current = s;
                    if (!isStart) Context.Logic.NeedsNewLogin = true;
                }
                return;
            }

            //session has expired
            if (totalEndDate < nowdate)
            {
                var s = Context.Session.Create();
                s.PlayerName = Context.Settings.Username;
                Context.Session.Current = s;
                if (!isStart) Context.Logic.NeedsNewLogin = true;
                return;
            }

            //session expired, but break not completed   
            if (endDate < nowdate && totalEndDate > nowdate)
            {
                //must wait the difference before start
                var diff = totalEndDate.Subtract(nowdate);
                Logger.Write($"Session ended {endDate.ToShortTimeString()}. Breaking until {totalEndDate.ToShortTimeString()}...", LogLevel.Warning);
                await Task.Delay(diff);
                var s = Context.Session.Create();
                s.PlayerName = Context.Settings.Username;
                Context.Session.Current = s;
                if (!isStart) Context.Logic.NeedsNewLogin = true;
                return;
            }
        }

        #endregion

    }
}
