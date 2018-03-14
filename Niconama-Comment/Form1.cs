using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Niconama_Comment
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dataGridView1, true, null);
            dataGridView1.Columns[0].Width = 50;
            dataGridView1.Columns[1].Width = 200;
            dataGridView1.Columns[2].Width = 80;
            dataGridView1.Columns[3].Width = 80;
            dataGridView1.Columns[4].Width = 580;
            foreach (DataGridViewColumn c in dataGridView1.Columns) c.SortMode = DataGridViewColumnSortMode.Automatic;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }

        string lv = "", ticket = "", thread = "", address = "", port = "", open_time = "", start_time = "", when = "";
        NetworkStream ns;

        CookieContainer cookie_container = new CookieContainer();

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();
            Task.Run(() =>
            {
                try
                {
                    #region ログイン
                    using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = cookie_container })
                    using (HttpClient c = new HttpClient(handler) { BaseAddress = new Uri("https://secure.nicovideo.jp/secure/login?site=niconico") }) { HttpResponseMessage result = c.PostAsync("", new FormUrlEncodedContent(new Dictionary<string, string> { { "next_url", "" }, { "mail", "メルアド" }, { "password", "パスワード" } })).Result; }
                    #endregion

                    #region GetLv
                    if (lv == "")
                    {
                        using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = cookie_container })
                        using (HttpClient c = new HttpClient(handler) { BaseAddress = new Uri("URL (例: http://live.nicovideo.jp/watch/ch2598539)") })
                        {
                            HttpResponseMessage ss = c.GetAsync("").Result;
                            lv = new Regex("<meta property=\"og:url\" content=\"http://live.nicovideo.jp/watch/(?<text>.*?)\"", RegexOptions.IgnoreCase | RegexOptions.Multiline).Match(ss.Content.ReadAsStringAsync().Result).Groups["text"].Value;
                        }
                    }
                    #endregion

                    #region GetPlayerStatus
                    using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = cookie_container })
                    using (HttpClient c = new HttpClient(handler) { BaseAddress = new Uri("http://live.nicovideo.jp/api/getplayerstatus?v=" + lv) })
                    {
                        HttpResponseMessage ss = c.GetAsync("").Result;
                        string gps = ss.Content.ReadAsStringAsync().Result.Replace("\n", "");
                        Match gps_match = new Regex(@"<addr>(.*?)</addr><port>(.*?)</port><thread>(.*?)</thread>", RegexOptions.None).Match(gps);
                        address = gps_match.Groups[1].Value;
                        port = gps_match.Groups[2].Value;
                        thread = gps_match.Groups[3].Value;
                        Match time_match = new Regex(@"<open_time>(.*?)</open_time><start_time>(.*?)</start_time>", RegexOptions.None).Match(gps);
                        open_time = time_match.Groups[1].Value;
                        start_time = time_match.Groups[2].Value;
                    }
                    #endregion

                    string request = "<thread thread=\"" + thread + "\" version=\"20061206\" res_from=\"-1000\" /> ";
                    TcpClient tcp = new TcpClient(address, int.Parse(port));
                    ns = tcp.GetStream();
                    byte[] sendBytes = Encoding.UTF8.GetBytes(request);
                    sendBytes[sendBytes.Length - 1] = 0;
                    ns.Write(sendBytes, 0, sendBytes.Length);

                    int resSize = 0;
                    string merge = "";
                    while (ns.CanRead)
                    {
                        byte[] resBytes = new byte[500];
                        resSize = ns.Read(resBytes, 0, resBytes.Length);
                        if (resSize == 0) break;
                        string message = Encoding.UTF8.GetString(resBytes);
                        string[] elements = message.Split(new string[] { "\0" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string r in elements)
                        {
                            if (r.StartsWith("<thread")) { ticket = new Regex("ticket=\"(.*?)\"", RegexOptions.None).Match(r).Groups[1].Value; Console.WriteLine(r); }
                            string receive = r;
                            if (!receive.EndsWith("</chat>"))
                            {
                                merge += receive;
                                continue;
                            }
                            if (!receive.StartsWith("<chat")) { receive = merge + receive; merge = ""; }
                            else if (receive.EndsWith("</chat>")) { receive = merge + receive; merge = ""; }
                            else if (merge != "") { receive = merge + receive; merge = ""; }
                            if (receive.StartsWith("<chat") && receive.EndsWith("</chat>"))
                            {
                                string comment = new Regex(">(.*)<", RegexOptions.None).Match(receive).Groups[1].Value.Replace("&lt;", "<").Replace("&gt;", ">");
                                if (comment != "")
                                {
                                    if (when == "") when = new Regex("date=\"(.*?)\"", RegexOptions.None).Match(receive).Groups[1].Value;
                                    if (comment.Contains("/hb ifseetno")) continue;
                                    double no = double.Parse(new Regex("no=\"(.*?)\"", RegexOptions.None).Match(receive).Groups[1].Value);
                                    string vpos = DateTimeOffset.FromUnixTimeSeconds((long)(double.Parse(new Regex("date=\"(.*?)\"", RegexOptions.None).Match(receive).Groups[1].Value) - double.Parse(open_time))).ToString("HH:mm:ss");
                                    string date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(new Regex("date=\"(.*?)\"", RegexOptions.None).Match(receive).Groups[1].Value)).LocalDateTime.ToString("HH:mm:ss");
                                    string id = new Regex("user_id=\"(.*?)\"", RegexOptions.None).Match(receive).Groups[1].Value;

                                    Invoke(new Action(() =>
                                    {
                                        dataGridView1.Rows.Add(no, id, date, vpos, comment);
                                        try { dataGridView1.FirstDisplayedScrollingRowIndex = dataGridView1.Rows.Count - 1; } catch { }
                                        dataGridView1.Sort(dataGridView1.CurrentCell.OwningColumn, ListSortDirection.Ascending);
                                    }));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }
            });
        }

        private void button1_Click(object sender, EventArgs e /* 過去コメ */)
        {
            Task.Run(() =>
            {
                string waybackkey = "";
                using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = cookie_container })
                using (HttpClient c = new HttpClient(handler)) waybackkey = c.GetStringAsync("http://watch.live.nicovideo.jp/api/getwaybackkey?thread=" + thread).Result.Replace("waybackkey=", "");
                byte[] sendBytes_ = Encoding.UTF8.GetBytes("<thread thread=\"" + thread + "\" version=\"20061206\" res_from=\"-1000\" waybackkey=\"" + waybackkey + "\"  user_id=\"64924091\" when=\"" + when + "\"/> ");
                when = "";
                sendBytes_[sendBytes_.Length - 1] = 0;
                ns.Write(sendBytes_, 0, sendBytes_.Length);
            });
        }

        private void button2_Click(object sender, EventArgs e /* 投稿 */)
        {
            Task.Run(() =>
            {
                long unixtime = (long)DateTime.Now.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds - (long)double.Parse(open_time);
                using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = cookie_container })
                using (HttpClient c = new HttpClient(handler))
                {
                    string postkey = c.GetStringAsync("http://ow.live.nicovideo.jp/api/getpostkey?thread=" + thread).Result.Replace("postkey=", "");
                    byte[] sendBytes_ = Encoding.UTF8.GetBytes("<chat thread=\"" + thread + "\" ticket=\"" + ticket + "\" vpos=\"" + unixtime + "\" postkey=\"" + postkey + "\" mail=\"184\" user_id=\"" + "64924091" + "\" premium=\"\">" + textBox1.Text + "</chat> ");
                    sendBytes_[sendBytes_.Length - 1] = 0;
                    ns.Write(sendBytes_, 0, sendBytes_.Length);
                }
            });
        }

        private void button3_Click(object sender, EventArgs e /* 接続 */)
        {
            if (ns != null) ns.Close();
            lv = textBox2.Text;
            timer1.Start();
            timer2.Start();
        }

        private void timer2_Tick(object sender, EventArgs e /* HeartBeat */)
        {
            using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = cookie_container })
            using (HttpClient c = new HttpClient(handler))
            {
                HttpResponseMessage heartbeat = c.PostAsync("http://ow.live.nicovideo.jp/api/heartbeat", new FormUrlEncodedContent(new Dictionary<string, string> { { "v", lv } })).Result;
                Console.WriteLine(heartbeat.Content.ReadAsStringAsync().Result);
            }
        }
    }
}