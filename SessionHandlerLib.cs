using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
/**/
namespace SessionHandler
{

    [Serializable]
    public class SessionData
    {
        public string Session_ID;
        public string Token;
        public string TimeStamp;

        public string SessionValues = "";

        [NonSerialized]
        public JObject SessionMap = new JObject();

        public bool HasKey(string key)
        {
            return SessionMap.ContainsKey(key);
        }

        public void AddValue(string key, string val)
        {
            if (this.SessionMap.ContainsKey(key))
            {
                this.SessionMap[key] = val;
                return;
            }
            this.SessionMap.Add(key, val);
        }

        public JToken GetValue(string key, string val)
        {
            return this.SessionMap.GetValue(key);
        }

        //data
    }

    public class TimeSticker
    {
        public string TimeStamp;
    }

    public class SessionManager
    {
        public string SessionLocation = "./Sessions";
        //folders: TimeStamp > Foldername (TimeStamp_)

        //dequeue the current timestamp when the timeframe changes
        public ConcurrentQueue<string> TimeStamps = new ConcurrentQueue<string>();

        public ConcurrentDictionary<string, string> SessionMatch = new ConcurrentDictionary<string, string>();

        public ConcurrentQueue<SessionData> SessionCreateQueue = new ConcurrentQueue<SessionData>();

        public SessionManager()
        {
            SetSessionLocation(this.SessionLocation);

            int TimeFrame = 15;

            // this.TimeStamps.Enqueue(GetTimeStamp(TimeFrame));

            ManualResetEvent constructing = new ManualResetEvent(false);
            bool isGoing = false;
            Task.Run(() =>
            {
                Task.Delay(500).Wait();
                while (true)
                {
                    string stamp = GetTimeStamp(TimeFrame);

                    if (this.TimeStamps.Contains(stamp))
                    {
                        Task.Delay(100).Wait();
                    }
                    else
                    {
                        string hold;
                        if (this.TimeStamps.TryPeek(out hold))
                        {
                            if (hold == stamp)
                            {
                                continue;
                            }
                            else
                            {
                                if (this.TimeStamps.Count > 4)
                                {
                                    this.TimeStamps.TryDequeue(out hold);
                                    //      Console.WriteLine("Removed old timestamp");
                                }
                            }
                        }
                        this.TimeStamps.Enqueue(stamp);

                        //    Console.WriteLine("Changed Stamp!" + new DateTime(long.Parse(stamp)));
                    }
                    //Set this so that the task actually gets to a point that the rest of the object is able to function.
                    if (!isGoing)
                    {
                        Console.WriteLine("SETTING SETTING SETTING SETTING SETTING");
                    }
                    constructing.Set();
                }
            });
            //Wait for the prior task to set up the initial timestamp.
            Console.WriteLine("WAITING WAITING WAITING WAITING ");
            constructing.WaitOne();
            isGoing = true;
            Console.WriteLine("GOING GOING GOING GOING GOING ");

            Task.Run(() =>
            {

                string stamp;

                while (true)
                {
                    SessionData work;
                    if (SessionCreateQueue.TryDequeue(out work))
                    {
                        if (this.TimeStamps.TryPeek(out stamp))
                        {
                            Task.Run(() => { SaveNewSessionData(work, stamp); });
                        }
                        //Console.WriteLine(stamp);
                        //save work
                    }
                    else
                    {
                        Task.Delay(50).Wait();
                        continue;
                    }
                }
            });

        }

        public string GetTimeStamp(int TimeFrame)
        {

            //it's 10:22 and we want an interval based on our TimeFrame as time.
            //our target is 10:15

            DateTime now = DateTime.UtcNow;
            now = now.Subtract(TimeSpan.FromMinutes(now.Minute % TimeFrame)).Subtract(TimeSpan.FromSeconds(now.Second));
            return DateTime.Parse(now.ToString("MM/dd/yy H:mm:ss")).Ticks + "";
        }

        public void SaveSessionData(SessionData sdata, string filename)
        {
            Console.WriteLine(filename);
            sdata.SessionValues = sdata.SessionMap.ToString(Formatting.None);
            try
            {
                BinaryFormatter bf = new BinaryFormatter();
                using (MemoryStream ms = new MemoryStream())
                {
                    bf.Serialize(ms, sdata);
                    File.WriteAllBytesAsync(filename, ms.ToArray());
                }
            }
            catch (Exception e)
            {
                //   Console.WriteLine(e);
            }
        }

        public void SaveNewSessionData(SessionData sdata, string stamp)
        {
            Console.WriteLine(this.SessionLocation + "/" + stamp + "_" + sdata.Session_ID + ".ses");
            sdata.SessionValues = sdata.SessionMap.ToString(Formatting.None);
            try
            {
                BinaryFormatter bf = new BinaryFormatter();
                using (MemoryStream ms = new MemoryStream())
                {
                    bf.Serialize(ms, sdata);
                    string filename = this.SessionLocation + "/" + stamp + "_" + sdata.Session_ID + ".ses";
                    File.WriteAllBytesAsync(filename, ms.ToArray());
                }
            }
            catch (Exception e)
            {
                //   Console.WriteLine(e);
            }
        }

        public void SetSessionLocation(string dir)
        {
            this.SessionLocation = dir;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        //session_ids , session_data
        public ConcurrentDictionary<string, SessionData> Session_Map = new ConcurrentDictionary<string, SessionData>();

        //TimeStamp:Session_ID
        public ConcurrentQueue<string> Session_History = new ConcurrentQueue<string>();

        public string CreateGUID()
        {
            string guid = "";

            guid = Guid.NewGuid().ToString();

            return guid;
        }

        public string CreateToken(string guid)
        {
            return new Random().Next(99999999, 999999999) + "" + new Random().Next(99999999, 999999999);
        }

        public void GetFileLocation(string session_id, out string generated_file, out string current_file)
        {

            string filename;
            string current_stamp = this.TimeStamps.LastOrDefault();
            string nFilename = this.SessionLocation + "/" + current_stamp + "_" + session_id + ".ses";

            string existing_file = null;

            for (int i = 0; i < 4; i++)
            {
                if (this.TimeStamps.Count >= i + 1)
                {
                    string stamp = this.TimeStamps.ElementAt(this.TimeStamps.Count - (i + 1));
                    //    Console.WriteLine("counting: " + (this.SessionLocation + "/" + stamp + "_" + session_id + ".ses"));
                    if (File.Exists(filename = (this.SessionLocation + "/" + stamp + "_" + session_id + ".ses")))
                    {
                        //       Console.WriteLine("counting: " + stamp);
                        existing_file = filename;
                        break;
                    }
                }
                else
                {
                    continue;
                }
            }

            generated_file = nFilename;
            current_file = existing_file;
        }

        public void CommitSession(SessionData sd)
        {
            string gen_file;
            string current_file;
            this.GetFileLocation(sd.Session_ID, out gen_file, out current_file);

            if(gen_file != null)
            {
                if (current_file != null)
                {
                    File.Delete(current_file);
                }
                //setting current_file to generated file (gen_file) in order to preserve efficiency and readability
                //for SaveSessionData after the if statement.
                current_file = gen_file;
            }
            SaveSessionData(sd, current_file);
        }

        public async Task<SessionData> CheckSession(string session_id, string token)
        {
            string[] resp = { "", "" };

            if (session_id == null && token != null)
            {
                Console.WriteLine("failed0");
                resp[0] = "failed_token";
                return new SessionData { Token = resp[0] };
            }

            if (session_id == null)
            {
                session_id = CreateGUID();

                //create file
                //store pair in dictionary
                this.SessionMatch.TryAdd(session_id, token = this.CreateToken(session_id));
                SessionData sd;
                this.SessionCreateQueue.Enqueue(sd = new SessionData
                {
                    Session_ID = session_id,
                    Token = token,
                    TimeStamp = DateTime.UtcNow.Ticks + ""
                });
                //    Console.WriteLine("ADDING: " + sd.Token);
                return sd;
            }
            else
            {
                string token_match;
                this.SessionMatch.TryGetValue(session_id, out token_match);

                if (token_match != null && token_match != token)
                {
                    Console.WriteLine("failed0");
                    resp[0] = "failed_token";
                    return new SessionData { Token = resp[0] };
                }

                string existing_file;
                string nFilename;
                this.GetFileLocation(session_id, out nFilename, out existing_file);
                //Console.WriteLine(existing_file + "\n" + nFilename);
                if (existing_file != null)
                {
                    byte[] bytes = File.ReadAllBytes(existing_file);
                    if (existing_file != nFilename)
                    {
                        //Console.WriteLine("replacing file");
                        File.WriteAllBytes(nFilename, bytes);
                        File.Delete(existing_file);
                    }
                    BinaryFormatter bf = new BinaryFormatter();
                    using (MemoryStream ms = new MemoryStream(bytes))
                    {
                        SessionData sd = (SessionData)bf.Deserialize(ms);
                        sd.SessionMap = JObject.Parse(sd.SessionValues);

                        //    Console.WriteLine("Data: " + sd.TestObject); ;
                        if (sd.Token == token)
                        {
                            return sd;
                        }
                        else
                        {
                            Console.WriteLine("failed1");
                            resp[0] = "failed_token";
                            return new SessionData { Token = resp[0] };
                        }
                    }
                }
                else
                {
                    return this.CheckSession(null, null).Result;
                }
            }
        }
    }

    public class SessionService
    {

        public SessionData SessionData = null;
        public SessionManager SessionManager = null;

        //TODO enable logging of incorrect sessions
        public SessionService(IHttpContextAccessor context, SessionManager session)
        {
            this.SessionManager = session;
            SessionData session_data = default(SessionData);

            //Console.WriteLine(context.HttpContext.Request.Cookies[nameof(session_data.Session_ID).ToLower()]);

            session_data = session.CheckSession(context.HttpContext.Request.Cookies[nameof(session_data.Session_ID).ToLower()], context.HttpContext.Request.Cookies[nameof(session_data.Token).ToLower()]).Result;

            Console.WriteLine(session_data.Session_ID + ":" + session_data.Token);

            if (session_data.Token == "failed_token")
            {
                session_data = session.CheckSession(null, null).Result;
            }

            this.SessionData = session_data;

            context.HttpContext.Response.Cookies.Append(nameof(session_data.Session_ID).ToLower(), session_data.Session_ID);
            context.HttpContext.Response.Cookies.Append(nameof(session_data.Token).ToLower(), session_data.Token);
        }

    }
}

