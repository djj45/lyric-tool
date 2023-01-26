using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace LyricTool
{
    public class Start
    {
        public static void Main()
        {
            Console.WriteLine("https://github.com/djj45/lyric-tool");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("请输入网易云音乐或者qq音乐的链接");
            Console.ForegroundColor = ConsoleColor.White;

            while (true)
            {
                string url = Console.ReadLine();

                if (Lyric.GetMusic(url) is Music music)
                {
                    List<string> list = music.TLLyricList;
                    Srt.WriteSrt(list, "", music.SongName, music.Singer);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("输入链接错误");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
        }
    }

    public abstract class Music
    {
        public abstract List<string> LyricList { get; }
        public abstract List<string> TLyricList { get; }
        public abstract List<string> LTLyricList { get; }
        public abstract List<string> TLLyricList { get; }
        public abstract List<string> LTRangeLyricList { get; }
        public abstract List<string> TLRangeLyricList { get; }

        public abstract string SongName { get; }
        public abstract string Singer { get; }
    }

    public class NetEase : Music
    {
        readonly string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.60 Safari/537.36";
        readonly int _timeOut = 2000;

        string Id { get; set; }
        string LyricUrl { get; set; }
        string InfoUrl { get; set; }
        string LyricData { get; set; }
        string InfoData { get; set; }

        JObject LyricJson => JObject.Parse(LyricData);
        JObject InfoJson => JObject.Parse(InfoData);

        string Lrc
        {
            get
            {
                string lrc = LyricJson["lrc"]["lyric"].ToString();
                if (lrc[lrc.Length - 1] != '\n')
                {
                    return lrc + "\n";
                }
                else
                {
                    return lrc;
                }
            }
        }
        string TLrc
        {
            get
            {
                string tlrc = LyricJson["tlyric"]["lyric"].ToString();
                if (tlrc == "")
                {
                    return tlrc;
                }
                if (tlrc[tlrc.Length - 1] != '\n')
                {
                    return tlrc + "\n";
                }
                else
                {
                    return tlrc;
                }
            }
        }
        string LTLyric => Lrc + TLrc;
        string TLLyric => TLrc + Lrc;

        public override List<string> LyricList => Lyric.ToList(Lrc, false);
        public override List<string> TLyricList => Lyric.ToList(TLrc, false);
        public override List<string> LTLyricList => Lyric.ToList(LTLyric, false);
        public override List<string> TLLyricList => Lyric.ToList(TLLyric, false);
        public override List<string> LTRangeLyricList => Lyric.ToRangeList(LyricList, TLyricList, Lyric.RangeMode.LT);
        public override List<string> TLRangeLyricList => Lyric.ToRangeList(LyricList, TLyricList, Lyric.RangeMode.TL);

        public override string SongName => InfoJson["songs"][0]["name"].ToString();
        public override string Singer => InfoJson["songs"][0]["artists"][0]["name"].ToString();

        public NetEase(string id)
        {
            Id = id;
            LyricUrl = "http://music.163.com/api/song/lyric?os=pc&id=" + Id + "&lv=-1&tv=-1";
            InfoUrl = "http://music.163.com/api/song/detail/?id=" + Id + "&ids=[" + Id + "]";
            LyricData = Request(LyricUrl);
            InfoData = Request(InfoUrl);
        }

        string Request(string url)
        {
            try
            {
                HttpWebRequest Req = (HttpWebRequest)WebRequest.Create(url);
                Req.UserAgent = _userAgent;
                Req.Timeout = _timeOut;

                HttpWebResponse Resp = (HttpWebResponse)Req.GetResponse();

                using (StreamReader sr = new StreamReader(Resp.GetResponseStream()))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static class Lyric
    {
        static string GetResponseUri(string url)
        {
            string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.60 Safari/537.36";
            string _referer = "https://y.qq.com";
            int _timeOut = 2000;
            try
            {
                HttpWebRequest Req = (HttpWebRequest)WebRequest.Create(url);
                Req.UserAgent = _userAgent;
                Req.Timeout = _timeOut;
                Req.Referer = _referer;

                HttpWebResponse Resp = (HttpWebResponse)Req.GetResponse();

                return Resp.ResponseUri.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static string GetId(string url, string rule)
        {
            Regex reg = new Regex(rule);
            Match match = reg.Match(url);

            return match.Groups[1].Value;
        }

        public static object GetMusic(string url)
        {
            //https://music.163.com/#/song?id=1469327796
            //https://music.163.com/song?id=1384482575&userid=1439471016
            //https://y.qq.com/n/ryqq/songDetail/004emQMs09Z1lz
            //https://c.y.qq.com/base/fcgi-bin/u?__=Xvhn4w
            string Id;
            if (url.Contains("c.y.qq.com"))
            {
                url = GetResponseUri(url);
                Id = GetId(url, "songid=([0-9]+)");
                if (Id != "")
                {
                    return new QQ(Id);
                }
            }
            else if (url.Contains("y.qq.com"))
            {
                Id = GetId(url, "songDetail/([0-9a-zA-Z]+)");
                if (Id != "")
                {
                    return new QQ(Id);
                }
            }
            else
            {
                Id = GetId(url, "id=([0-9]+)");
                if (Id != "")
                {
                    return new NetEase(Id);
                }
            }

            return "error";
        }

        public enum RangeMode
        {
            //T:translated lyric    L:lyric
            LT,//交错优先原文
            TL,//交错优先译文
        }

        public static bool IsLyric(string lyric)
        {
            return int.TryParse(lyric.Split(':')[0].Split('[')[1], out _) && lyric.Split(']')[1] != "//\n" && lyric.Split(']')[1] != "\n";
        }

        public static List<string> ToList(string lyric, bool remove)
        {
            List<string> list = new List<string>();
            List<string> removeInfoList = new List<string>();
            string s = "";

            foreach (char item in lyric)
            {
                s += item;

                if (item == '\n')
                {
                    list.Add(s);
                    s = "";
                }
            }

            if (remove)
            {
                foreach (string item in list)
                {
                    if (IsLyric(item))
                    {
                        removeInfoList.Add(item);
                    }
                }

                return removeInfoList;
            }

            return list;
        }

        public static void WriteLrc(string path, string songName, string singer, List<string> lyricList)
        {
            using (StreamWriter lrc = File.CreateText(path + GetValidName(songName) + "-" + GetValidName(singer) + ".lrc"))
            {
                foreach (string lyric in lyricList)
                {
                    lrc.Write(lyric);
                }
            }
        }

        public static string GetTime(string lyric)
        {
            return lyric.Split('[')[1].Split(']')[0];
        }

        public static List<string> ToRangeList(List<string> lyric, List<string> tlyric, RangeMode rangeMode)
        {
            List<string> rangeList = new List<string>();

            foreach (string lrc in lyric)
            {
                int index = tlyric.FindIndex(tlrc => GetTime(tlrc) == GetTime(lrc));
                if (index != -1)
                {
                    if (rangeMode == RangeMode.LT)
                    {
                        rangeList.Add(lrc);
                        rangeList.Add(tlyric[index]);
                    }
                    else
                    {
                        rangeList.Add(tlyric[index]);
                        rangeList.Add(lrc);
                    }
                }
                else
                {
                    rangeList.Add(lrc);
                }
            }

            return rangeList;
        }

        public static string GetValidName(string _name)
        {
            string name = "";
            string invalidName = new string(Path.GetInvalidFileNameChars());

            foreach (var item in _name)
            {
                if (!invalidName.Contains(item.ToString()))
                {
                    name += item;
                }
                else
                {
                    name += "x";
                }
            }

            return name;
        }
    }

    public static class Srt
    {
        public static List<string> interLudeList = new List<string>();

        public static string GetTime(string lyric)
        {
            return lyric.Split('[')[1].Split(']')[0];
        }

        public static string GetLyric(string lyric)
        {
            return lyric.Split(']')[1];
        }

        public static double TransTime(string lyric)
        {
            string[] timeSplit = lyric.Split(':', '.');

            return double.Parse(timeSplit[0]) * 60 + double.Parse(timeSplit[1]) + double.Parse(timeSplit[2]) * Math.Pow(10, -timeSplit[2].Length);
        }

        public static string ExtendTime(string time)
        {
            string milliSecond = time.Split('.')[1];
            double extendTime = TransTime(time) + 8;
            string minute = ((int)extendTime / 60).ToString();
            string second = ((int)extendTime % 60).ToString();

            while (minute.Length <= 1)
                minute = "0" + minute;
            while (second.Length <= 1)
                second = "0" + second;

            return minute + ":" + second + "," + milliSecond;
        }

        public static bool IsLyric(string lyric)
        {
            return int.TryParse(lyric.Split(':')[0].Split('[')[1], out _);
        }

        public static string HandleLyric(string lyric)
        {
            return lyric.TrimEnd('\n', '/') + "\\n\n";
        }

        public static void CheckTime(ref string preTime, ref string time, ref bool flag, ref string lyric)
        {
            if (TransTime(preTime) - TransTime(time) > 60)
            {
                flag = true;
            }
            if (TransTime(time) - TransTime(preTime) > 8)
            {
                interLudeList.Add(preTime + " --> " + time + " " + lyric);
            }
        }

        public static void WriteSrt(List<string> list, string path, string songName, string singer)
        {
            int count = 0;
            string preTime = "";
            string time = "";
            string prelyric = "";
            bool flag = false;

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(songName + "-" + singer);
            Console.ForegroundColor = ConsoleColor.White;

            using (StreamWriter srt = File.CreateText(path + Lyric.GetValidName(songName) + "-" + Lyric.GetValidName(singer) + ".srt"))
            {
                if (list.Count != 0)
                {
                    foreach (string line in list)
                    {
                        if (!IsLyric(line))
                            continue;
                        if (count == 0)
                        {
                            preTime = GetTime(line);
                            prelyric = HandleLyric(GetLyric(line));
                            count++;
                            continue;
                        }

                        time = GetTime(line);
                        CheckTime(ref preTime, ref time, ref flag, ref prelyric);

                        if (flag)
                        {
                            time = ExtendTime(preTime);
                        }

                        srt.WriteLine(count);
                        srt.WriteLine("00:" + preTime.Replace('.', ',') + " --> " + "00:" + time.Replace('.', ','));
                        srt.WriteLine(prelyric);

                        if (flag)
                        {
                            flag = false;
                            time = GetTime(line);
                        }

                        preTime = time;
                        prelyric = HandleLyric(GetLyric(line));
                        count++;
                    }

                    srt.WriteLine(count);
                    srt.WriteLine("00:" + preTime.Replace('.', ',') + " --> " + "00:" + ExtendTime(preTime).Replace('.', ','));
                    srt.WriteLine(prelyric);

                    foreach (string item in interLudeList)
                    {
                        Console.Write(item);
                    }

                    interLudeList.Clear();
                }
            };
        }
    }

    public class QQ : Music
    {

        readonly string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.60 Safari/537.36";
        readonly string _referer = "https://y.qq.com";
        readonly string _songmidParams = "callback=MusicJsonCallback_lrc&g_tk=5381&format=jsonp&songmid=";
        readonly string _musicidParams = "callback=MusicJsonCallback_lrc&g_tk=5381&format=jsonp&musicid=";
        readonly int _timeOut = 2000;

        string Id { get; set; }
        string LyricUrl { get; set; }
        string InfoUrl { get; set; }
        public string LyricData { get; set; }
        public string InfoData { get; set; }

        public JObject LyricJson => JObject.Parse(LyricData.Replace("MusicJsonCallback_lrc(", "").TrimEnd(')'));
        public JObject InfoJson => JObject.Parse(InfoData.Replace("MusicJsonCallback_lrc(", "").TrimEnd(')'));

        public string Base64Lrc => LyricJson["lyric"].ToString();
        public string Base64TLrc => LyricJson["trans"].ToString();

        string _lrc;
        string _tlrc;
        public string Lrc
        {
            get
            {
                if (_lrc[_lrc.Length - 1] != '\n')
                {
                    return _lrc + "\n";
                }
                else
                {
                    return _lrc;
                }
            }
            set
            {
                _lrc = value;
            }
        }

        public string TLrc
        {
            get
            {
                if (_tlrc == "")
                {
                    return _tlrc;
                }
                if (_tlrc[_tlrc.Length - 1] != '\n')
                {
                    return _tlrc + "\n";
                }
                else
                {
                    return _tlrc;
                }
            }
            set
            {
                _tlrc = value;
            }
        }

        string LTLyric => Lrc + TLrc;
        string TLLyric => TLrc + Lrc;

        public override List<string> LyricList => Lyric.ToList(Lrc, false);
        public override List<string> TLyricList => Lyric.ToList(TLrc, false);
        public override List<string> LTLyricList => Lyric.ToList(LTLyric, false);
        public override List<string> TLLyricList => Lyric.ToList(TLLyric, false);
        public override List<string> LTRangeLyricList => Lyric.ToRangeList(LyricList, TLyricList, Lyric.RangeMode.LT);
        public override List<string> TLRangeLyricList => Lyric.ToRangeList(LyricList, TLyricList, Lyric.RangeMode.TL);

        public override string SongName => InfoJson["data"][0]["name"].ToString();
        public override string Singer => InfoJson["data"][0]["singer"][0]["name"].ToString();
        byte[] ByteLrc => Convert.FromBase64String(Base64Lrc);
        byte[] ByteTLrc => Convert.FromBase64String(Base64TLrc);

        public QQ(string id)
        {
            Id = id;

            if (!int.TryParse(id, out int _))
            {
                LyricUrl = "https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg?" + _songmidParams + Id;
                InfoUrl = "https://c.y.qq.com/v8/fcg-bin/fcg_play_single_song.fcg?" + _songmidParams + Id;
            }
            else
            {
                LyricUrl = "https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg?" + _musicidParams + Id;
                InfoUrl = "https://c.y.qq.com/v8/fcg-bin/fcg_play_single_song.fcg?" + _songmidParams.Replace("songmid", "songid") + Id;
            }

            LyricData = Request(LyricUrl);
            InfoData = Request(InfoUrl);
            Lrc = Encoding.UTF8.GetString(ByteLrc);
            TLrc = Encoding.UTF8.GetString(ByteTLrc);
        }

        string Request(string url)
        {
            try
            {
                HttpWebRequest Req = (HttpWebRequest)WebRequest.Create(url);
                Req.UserAgent = _userAgent;
                Req.Timeout = _timeOut;
                Req.Referer = _referer;

                HttpWebResponse Resp = (HttpWebResponse)Req.GetResponse();

                using (StreamReader sr = new StreamReader(Resp.GetResponseStream()))
                {
                    return sr.ReadToEnd();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
