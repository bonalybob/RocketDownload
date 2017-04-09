﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text.RegularExpressions;
using SPORTident;
using SPORTident.Communication;
using LiteDB;
using Microsoft.Win32;
using System.Xml;
using System.IO;

namespace Nevis
{
    public partial class MainWindow : Window
    {
        public Reader reader;
        public string password = "orienteer";
        public string databaselocation = "";
        public int complete = 0;
        public int idnumber = 0;
        public MainWindow()
        {

            InitializeComponent();
            reader = new Reader
            {
                WriteBackupFile = true,
                BackupFileName = System.IO.Path.Combine(Environment.CurrentDirectory, $@"backup\{DateTime.Now:yyyy-MM-dd}_stamps.bak")
            };
            backupNo.IsEnabled = false;
            backupButton.IsEnabled = false;
            Download.IsEnabled = false;
            Results.IsEnabled = false;
            Entries.IsEnabled = false;
            Courses.IsEnabled = false;
            SafetyCheck.IsEnabled = false;
            backupidlabel.IsEnabled = false;
            archive.IsEnabled = false;
            menu.Visibility = Visibility.Hidden;
            Download.Visibility = Visibility.Hidden;
            Entries.Visibility = Visibility.Hidden;
            Results.Visibility = Visibility.Hidden;
            Database.Visibility = Visibility.Hidden;
            Courses.Visibility = Visibility.Hidden;
            SafetyCheck.Visibility = Visibility.Hidden;
            textDatabase.ToolTip = "Please select an Event";
            UITitle.Text = "Welcome to Nevis";
            UITitle.Visibility = Visibility.Hidden;
            comboboxPortsList.Items.Add("Ports List");
            comboboxPortsList.SelectedIndex = 0;
            reader.DeviceConfigurationRead += new DeviceConfigurationReadEventHandler(reader_DeviceConfigurationRead);
            reader.InputDeviceChanged += new ReaderDeviceChangedEventHandler(reader_InputDeviceChanged);
            reader.InputDeviceStateChanged += new ReaderDeviceStateChangedEventHandler(reader_InputDeviceStateChanged);
            reader.CardRead += new SPORTident.DataReadCompletedEventHandler(reader_CardRead);
        }

        public class Entry
        {
            public int Id { get; set; }
            public string Sicard { get; set; }
            public string Name { get; set; }
            public string Course { get; set; }
            public string Class { get; set; }
            public string Club { get; set; }
            public string Start { get; set; }
            public string Time { get; set; }
            public string LastSeenAt { get; set; }
            public string LastSeenTime { get; set; }
            public bool Downloaded { get; set; }

        }
        public class Course
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Distance { get; set; }
            public string Climb { get; set; }
            public string[] ControlCodes { get; set; }

        }
        public class Archive
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Sicard { get; set; }
            public string Club { get; set; }
            public string Class { get; set; }

        }
        public class Event
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Date { get; set; }
        }

        private void reader_DeviceConfigurationRead(object sender, StationConfigurationEventArgs e)
        {
            writeLogColor(listboxLog, "-----  Connected to Station  -----", "#0000ff");

        }

        private void reader_InputDeviceStateChanged(object sender, ReaderDeviceStateChangedEventArgs e)
        {
            // writeLogColorNL(listboxLog, "InputDeviceStateChanged: " + e.PreviousState + " => " + e.CurrentState, "#0000ff");
        }

        private void reader_InputDeviceChanged(object sender, ReaderDeviceChangedEventArgs e)
        {
            //writeLogColorNL(listboxLog, "InputDeviceChanged: " + e.PreviousDevice + " => " + e.CurrentDevice, "#0000ff");
        }



        private void clearSIDevices()
        {
            comboboxPortsList.Items.Clear();
        }

        private void getSIDevices()
        {

            ReaderDeviceInfo.GetAvailableDeviceList();
            foreach (ReaderDeviceInfo device in ReaderDeviceInfo.AvailableDevices)
            {
                comboboxPortsList.Items.Add(device);

            }
            if (comboboxPortsList.Items.Count > 0)
            {

                comboboxPortsList.SelectedIndex = 0;
            }
            else
            {
                comboboxPortsList.Items.Add("No Stations Found");
                comboboxPortsList.SelectedIndex = 0;
            }
        }

        private void connect()
        {
            ReaderDeviceInfo readerDeviceInfo = (ReaderDeviceInfo)(comboboxPortsList.SelectedItem);
            reader.InputDevice = readerDeviceInfo;
            reader.OutputDevice = new ReaderDeviceInfo(ReaderDeviceType.None);
            reader.OpenInputDevice();
            reader.OpenOutputDevice();

            comboboxPortsList.IsEnabled = false;
            buttonRefresh.IsEnabled = false;
            buttonConnect.IsEnabled = false;




        }

        private void disconnect()
        {
            comboboxPortsList.IsEnabled = true;
            buttonRefresh.IsEnabled = true;
            buttonConnect.IsEnabled = true;


            reader.CloseInputDevice();
        }

        private void buttonRefresh_Click(object sender, RoutedEventArgs e)
        {

            clearSIDevices();
            getSIDevices();
        }

        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                connect();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void reader_CardRead(object sender, SportidentDataEventArgs e)
        {
            SportidentCard card = e.Cards[0];
            var startfull = Regex.Split(Convert.ToString(card.StartPunch), "000;")[1];
            var finishfull = Regex.Split(Convert.ToString(card.FinishPunch), "000;")[1];
            DateTime start = new DateTime(2000, 1, 1, Convert.ToInt32(startfull.Split(':')[0]), Convert.ToInt32(startfull.Split(':')[1]), Convert.ToInt32(startfull.Split(':')[2]));
            DateTime end = new DateTime(2000, 1, 1, Convert.ToInt16(finishfull.Split(':')[0]), Convert.ToInt16(finishfull.Split(':')[1]), Convert.ToInt16(finishfull.Split(':')[2]));
            var totaltime = end.Subtract(start);

            var errors = "";
            complete = 0;
            var i = 0;
            var j = 0;

            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                var done = 0;
                var entries = db.GetCollection<Entry>("entries");
                var courses = db.GetCollection<Course>("courses");
                var results = entries.Find(Query.EQ("Sicard", card.Siid));
                var resultlist = from result in results select result;
                string[] controllist = { };
                foreach (var result in resultlist)
                {
                    if (result.Course != null)
                    {
                        var results1 = courses.Find(Query.EQ("Name", result.Course));
                        var resultlist1 = from result1 in results1 select result1;
                        foreach (var result1 in resultlist1)
                        {
                            controllist = result1.ControlCodes;
                            break;
                        }
                        if (controllist.Length <= card.ControlPunchList.ToArray().Length)
                        {
                            foreach (var control in controllist)
                            {
                                if (i + 1 < controllist.Length && j + 1 < card.ControlPunchList.ToArray().Length)
                                {
                                    if (Convert.ToString(card.ControlPunchList.ElementAtOrDefault(j)).Split(';')[1] == controllist[i])
                                    {
                                    }

                                    else if (Convert.ToString(card.ControlPunchList.ElementAtOrDefault(j)).Split(';')[1] == controllist[i + 1])
                                    {
                                        errors += " M" + Convert.ToString(i + 1);
                                        i += 1;

                                    }
                                    else if (Convert.ToString(card.ControlPunchList.ElementAtOrDefault(j + 1)).Split(';')[1] == controllist[i])
                                    {
                                        j += 1;

                                    }


                                    else
                                    {
                                        errors += " W" + Convert.ToString(i + 1);

                                    }
                                }
                                else
                                {
                                    if (Convert.ToString(card.ControlPunchList.ElementAtOrDefault(j)).Split(';')[1] == controllist[i])
                                    {

                                    }

                                    else
                                    {
                                        errors += " W" + Convert.ToString(i + 1);

                                    }
                                }
                                if (i + 1 > controllist.Length)
                                {
                                    break;
                                }
                                if (j + 1 > card.ControlPunchList.ToArray().Length)
                                {
                                    break;
                                }
                                i += 1;
                                j += 1;
                            }
                        }
                        else
                        {
                            foreach (var punch in card.ControlPunchList)
                            {
                                if (i + 1 < controllist.Length && j + 1 < card.ControlPunchList.ToArray().Length)
                                {
                                    if (Convert.ToString(card.ControlPunchList.ElementAtOrDefault(j)).Split(';')[1] == controllist[i])
                                    {

                                    }

                                    else if (Convert.ToString(card.ControlPunchList.ElementAtOrDefault(j)).Split(';')[1] == controllist[i + 1])
                                    {
                                        errors += " M" + Convert.ToString(i + 1);
                                        i += 1;

                                    }
                                    else if (Convert.ToString(card.ControlPunchList.ElementAtOrDefault(j + 1)).Split(';')[1] == controllist[i])
                                    {
                                        j += 1;

                                    }


                                    else
                                    {
                                        errors += " W" + Convert.ToString(i + 1);

                                    }
                                }
                                else
                                {
                                    if (Convert.ToString(card.ControlPunchList.ElementAtOrDefault(j)).Split(';')[1] == controllist[i])
                                    {

                                    }

                                    else
                                    {
                                        errors += " W" + Convert.ToString(i + 1);

                                    }
                                }
                                if (i + 1 > controllist.Length)
                                {
                                    break;
                                }
                                if (j + 1 > card.ControlPunchList.ToArray().Length)
                                {
                                    break;
                                }
                                i += 1;
                                j += 1;


                            }
                            if (card.ControlPunchList.ToArray().Length - controllist.Length + 1 == 0)
                            {
                                errors += "M" + Convert.ToString(controllist.Length + card.ControlPunchList.ToArray().Length - controllist.Length + 1);
                            }
                            else
                            {
                                errors += "M" + Convert.ToString(controllist.Length + card.ControlPunchList.ToArray().Length - controllist.Length + 1) + "-" + controllist.Length;
                            }
                        }

                        var entry = new Entry { };

                        if (errors == "")
                        {

                            entry.Name = result.Name;
                            entry.Course = result.Course;
                            entry.Class = result.Class;
                            entry.Club = result.Club;
                            entry.Start = result.Start;
                            entry.Time = Convert.ToString(totaltime);
                            entry.Sicard = card.Siid;
                            entry.Downloaded = true;
                            entry.Id = result.Id;
                            writeLogColorNL(listboxLog, card.Siid + " - " + entry.Name + " - " + entry.Course + " - " + Convert.ToString(totaltime), "#00d807");


                        }
                        else
                        {
                            entry.Name = result.Name;
                            entry.Course = result.Course;
                            entry.Class = result.Class;
                            entry.Club = result.Club;
                            entry.Start = result.Start;

                            entry.Time = errors;
                            entry.Sicard = card.Siid;
                            entry.Downloaded = true;

                            entry.Id = result.Id;
                            writeLogColorNL(listboxLog, card.Siid + " - " + entry.Name + " - " + entry.Course + " - " + errors, "#ff0000");

                        }

                        entries.Update(entry);
                        done = 1;
                        break;
                    }
                    else
                    {
                        var entry = new Entry
                        {
                            Name = result.Name,
                            Class = result.Class,
                            Club = result.Club,
                            Start = result.Start,
                            Time = Convert.ToString(totaltime),
                            Sicard = card.Siid,
                            Downloaded = true,
                            Id = result.Id,
                        };
                        entry.LastSeenTime = result.LastSeenTime;
                        entry.LastSeenAt = result.LastSeenAt;
                        entries.Update(entry);
                        writeLogColorNL(listboxLog, card.Siid + " - " + entry.Name + " - " + entry.Course + " - " + Convert.ToString(totaltime), "Black");
                        done = 1;
                        break;
                    }
                }


                if (done == 0)
                {
                    var entry1 = new Entry
                    {
                        Name = card.PersonalData.FirstName + ' ' + card.PersonalData.LastName,
                        Course = "Unknown",
                        Class = "",
                        Club = "",
                        Start = "",
                        Time = Convert.ToString(totaltime),
                        Sicard = card.Siid,
                        Downloaded = true,
                    };
                    entries.Insert(entry1);
                    writeLogColorNL(listboxLog, card.Siid + " - " + card.PersonalData.FirstName + ' ' + card.PersonalData.LastName + " - " + Convert.ToString(totaltime), "Black");



                }




            }

        }

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            var done = 0;
            var name = textName.Text;
            var si = textSI.Text;
            var course = textCourse.Text;
            var club = textClub.Text;
            var clas = textClass.Text;
            var start = textStart.Text;
            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                var entries = db.GetCollection<Entry>("entries");




                var results = entries.Find(Query.EQ("Id", idnumber));
                var resultlist = from result in results select result;


                foreach (var result in resultlist)
                {

                    var entry = new Entry
                    {
                        Name = name,
                        Course = course,
                        Class = clas,
                        Club = club,
                        Start = start,
                        Sicard = si,
                    };
                    entry.LastSeenTime = result.LastSeenTime;
                    entry.LastSeenAt = result.LastSeenAt;
                    entry.Time = result.Time;
                    entry.Downloaded = result.Downloaded;
                    entry.Id = idnumber;
                    entries.Update(entry);

                    done = 1;
                    break;
                }
                if (done == 0)
                {
                    var entry1 = new Entry
                    {
                        Name = name,
                        Course = course,
                        Class = clas,
                        Club = club,
                        Start = start,
                        Sicard = si,
                        Time = "",
                        Downloaded = false,
                        LastSeenAt = "",
                        LastSeenTime = ""
                    };
                    if (idnumber != 0) { entry1.Id = idnumber; entries.Update(entry1); }
                    else { entries.Insert(entry1); }


                }


                clear();

            }
        }



        private void Search_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                if (textName.Text == "")
                {
                    var entries = db.GetCollection<Entry>("entries");
                    var results = entries.Find(Query.EQ("Sicard", textSI.Text));
                    var resultlist = from result in results select result;
                    foreach (var result in resultlist)
                    {
                        textName.Text = result.Name;
                        textSI.Text = result.Sicard;
                        textCourse.Text = result.Course;
                        textClass.Text = result.Class;
                        textStart.Text = result.Start;
                        textClub.Text = result.Club;
                        textTime.Text = result.Time;
                        idnumber = result.Id;
                    }
                }
                else if (textSI.Text == "")
                {
                    var entries = db.GetCollection<Entry>("entries");
                    var results = entries.Find(Query.Contains("Name", textName.Text));
                    var resultlist = from result in results select result;
                    foreach (var result in resultlist)
                    {
                        textName.Text = result.Name;
                        textSI.Text = result.Sicard;
                        textCourse.Text = result.Course;
                        textClass.Text = result.Class;
                        textStart.Text = result.Start;
                        textClub.Text = result.Club;
                        textTime.Text = result.Time;
                        idnumber = result.Id;
                    }
                }


            }
        }

        private void refresh_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                var course = "";
                resultsClear();
                var entries = db.GetCollection<Entry>("entries");
                var results = entries.Find(Query.EQ("Downloaded", true)).OrderBy(x => x.Course).ThenBy(x => x.Time);
                var resultlist = from result in results select result;

                foreach (var result in resultlist)
                {
                    if (result.Course != course)
                    {
                        if (result.Course == "")
                        {
                            result.Course = "Unknown";
                        };
                        resultsLog(result.Course + ":");
                        resultsLog(Convert.ToString("    " + result.Sicard + " - " + result.Name + " - " + result.Time));
                        course = result.Course;
                    }
                    else
                    {
                        resultsLog(Convert.ToString("    " + result.Sicard + " - " + result.Name + " - " + result.Time));

                    }
                }

            }
        }
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            clear();
        }
        private void clear()
        {
            textName.Text = "";
            textSI.Text = "";
            textCourse.Text = "";
            textClass.Text = "";
            textStart.Text = "";
            textClub.Text = "";
            textTime.Text = "";
            idnumber = 0;
        }
        private void browse_Click(object sender, RoutedEventArgs e)
        {

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = false;
            openFileDialog.CheckPathExists = false;
            openFileDialog.Title = "Open or Create a Event";
            openFileDialog.Filter = "Nevis Event File (*.evnt)|*.evnt|Old Nevis Database File (*.db)|*.db|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                menu.Visibility = Visibility.Visible;
                textDatabase.ToolTip = "Event: " + openFileDialog.FileName;
                databaselocation1.Text = openFileDialog.FileName;
                databaselocation = openFileDialog.FileName;
                Download.IsEnabled = true;
                Results.IsEnabled = true;
                Entries.IsEnabled = true;
                Courses.IsEnabled = true;
                backupNo.IsEnabled = true;
                backupButton.IsEnabled = true;
                SafetyCheck.IsEnabled = true;
                backupidlabel.IsEnabled = true;
                archive.IsEnabled = true;



            }
        }

        private void htmlresults_Click(object sender, RoutedEventArgs e)
        {

            var eventname = System.IO.Path.GetFileNameWithoutExtension(databaselocation);
            var style = "<style>footer,header{width:100%;box-shadow:rgba(0,0,0,.156863) 0 2px 5px 0,rgba(0,0,0,.117647) 0 2px 10px 0;text-align:center}*{margin:0}header{background-color:#1e88e5;top:0;padding-top:.5%;padding-bottom:.5%}body,html{margin:0;padding:0;height:100%;width:100%;font-family:Roboto,Segoe-UI,San-Francisco,sans-serif}footer{background-color:#1976d2;bottom:0}footer p{padding:10px;color:#fff}#course-info{margin-top:.5%}@media(max-width:550px){#page-title,h1{font-weight:500}main{width:90%;margin-left:5%}td,th,tr{border-bottom:1px solid #d0d0d0;padding:3%;text-align:left!important}h1{margin-top:3%}#page-title{color:#fff;margin:0;padding:0}}@media(min-width:550px){#page-title,h1{font-weight:500}main{width:90%;margin-left:5%}td,th,tr{border-bottom:1px solid #d0d0d0;padding:1%;text-align:left!important}h1{margin-top:1%}#page-title{color:#fff;margin:0;padding:0}}table{width:98%;margin-top:1%;margin-bottom:3%;border-collapse:collapse;border-spacing:0;border-radius:5px;margin-left:1%}tr{transition:background-color .5s ease}th{font-weight:700;text-align:left}tr:hover{background-color:#e8e8e8}th{border-bottom:1.25px solid #d0d0d0}</style>";
            DateTime date = DateTime.Now;
            var head = $"<!DOCTYPE HTML><html lang=\"en\"><head> <title>{eventname} - Results</title> <meta charset=\"utf - 8\"> <meta name=\"description\" content=\"Results for {eventname}\"> <meta name=\"viewport\" content=\"width=device-width, initial-scale=1,minimum-scale=1\">  {style}</head><body> <header> <h1 id=\"page-title\">{eventname} - Results</h1> </header> <main>";
            var footer = $"</table></div></main> <footer> <p>Results compiled on {date.ToString("dd/MM/yyyy")} at {date.ToString("H:mm:ss")} using Nevis</p></footer></body></html>";
            var resultswrite = "";
            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                var course = "";
                var entries = db.GetCollection<Entry>("entries");
                var results = entries.Find(Query.EQ("Downloaded", true)).OrderBy(x => x.Course).ThenBy(x => x.Time);
                var resultlist = from result in results select result;
                var a = 1;
                var distance = "";
                var climb = "";
                foreach (var result in resultlist)
                {
                    if (result.Course == "") { }
                    else if (result.Course != course)
                    {
                        if (a == 1)
                        {
                            var courses = db.GetCollection<Course>("courses");

                            var results1 = courses.Find(Query.EQ("Name", result.Course));
                            var resultlist1 = from result1 in results1 select result1;

                            foreach (var result1 in resultlist1)
                            {
                                distance = result1.Distance;
                                climb = result1.Climb;
                                break;
                            }
                            resultswrite += $" <div class=\"course\"> <h1 id=\"course-title\">{result.Course}</h1> <p id=\"course-info\"> {distance}km {climb}m</p><table><tr> <th>Pos.</th> <th>Name</th> <th>Club</th> <th>Class</th> <th>Time</th> </tr> ";
                            resultswrite += $"<tr> <td> {a}</td><td> {result.Name}</td><td> {result.Club} </td><td> {result.Class} </td><td>{result.Time} </td></tr>";
                        }
                        else
                        {
                            var courses = db.GetCollection<Course>("courses");

                            var results1 = courses.Find(Query.EQ("Name", result.Course));
                            var resultlist1 = from result1 in results1 select result1;
                            if (result.Course != "Unknown")
                            {
                                foreach (var result1 in resultlist1)
                                {
                                    distance = result1.Distance;
                                    climb = result1.Climb;
                                    break;
                                }
                                a = 1;
                                resultswrite += $" </table></div><div class=\"course\"> <h1 id=\"course-title\">{result.Course}</h1> <p id=\"course-info\"> {distance}km {climb}m</p><table><tr> <th>Pos.</th> <th>Name</th> <th>Club</th> <th>Class</th> <th>Time</th> </tr> ";
                                resultswrite += $"<tr> <td> {a}</td><td> {result.Name}</td><td> {result.Club} </td><td> {result.Class} </td><td>{result.Time} </td></tr>";


                            }
                            else
                            {
                                resultswrite += $" </table></div><div class=\"course\"> <h1 id=\"course-title\">{result.Course}</h1><table><tr> <th>Pos.</th> <th>Name</th> <th>Club</th> <th>Class</th> <th>Time</th> </tr> ";
                                resultswrite += $"<tr> <td> {a}</td><td> {result.Name}</td><td> {result.Club} </td><td> {result.Class} </td><td>{result.Time} </td></tr>";
                            }

                        }
                        course = result.Course;
                        a += 1;
                    }
                    else
                    {
                        resultswrite += $"<tr> <td> {a}</td><td> {result.Name}</td><td> {result.Club} </td><td> {result.Class} </td><td>{result.Time} </td></tr>";
                        a += 1;
                    }
                }
            }
            var text = head + resultswrite + footer;
            File.WriteAllText(@databaselocation.Split('.')[0] + ".html", text);
            Alert alert = new Alert();
            alert.Title = "";
            alert.password.Visibility = Visibility.Hidden;
            alert.progress.Visibility = Visibility.Hidden;
            alert.Enter.IsEnabled = false;
            alert.alert.Visibility = Visibility.Visible;
            alert.textBlock.Text = "File Written to: " + databaselocation.Split('.')[0] + ".html";
            alert.Show();



        }
        private void AddCourse_Click(object sender, RoutedEventArgs e)
        {
            var name = CourseName.Text;
            var nos = textCourseNos.Text;
            List<string> controls = new List<string>();
            int lineCount = textCourseNos.LineCount;

            for (int line = 0; line < lineCount; line++)
            {
                if (textCourseNos.GetLineText(line) != "")
                {
                    controls.Add(textCourseNos.GetLineText(line));
                }

            }

            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                var course = new Course
                {
                    Name = name,

                };
                var courses = db.GetCollection<Course>("courses");
                var results = courses.Find(Query.EQ("Name", name));
                var resultlist = from result in results select result;
                var done = 0;
                foreach (var result in resultlist)
                {
                    var course1 = new Course
                    {
                        Name = name,
                        ControlCodes = controls.ToArray(),
                        Climb = CourseClimb.Text,
                        Distance = CourseDistance.Text,
                    };

                    course1.Id = result.Id;
                    courses.Update(course1);
                    done = 1;
                    break;
                }
                if (done == 0)
                {
                    var course1 = new Course
                    {
                        Name = name,
                        ControlCodes = controls.ToArray(),
                        Climb = CourseClimb.Text,
                        Distance = CourseDistance.Text,
                    };
                    courses.Insert(course1);


                }

                CourseName.Text = "";
                CourseDistance.Text = "";
                CourseClimb.Text = "";
                textCourseNos.Text = "";
            }
        }

        private void SearchCourse_Click(object sender, RoutedEventArgs e)
        {
            CourseDistance.Text = "";
            CourseClimb.Text = "";
            textCourseNos.Text = "";
            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {

                var courses = db.GetCollection<Course>("courses");

                var results = courses.Find(Query.EQ("Name", CourseName.Text));
                var resultlist = from result in results select result;
                var text = "";

                foreach (var result in resultlist)
                {
                    CourseName.Text = result.Name;
                    CourseClimb.Text = result.Climb;
                    CourseDistance.Text = result.Distance;


                    textCourseNos.Text = "";
                    foreach (var control in result.ControlCodes)
                    {
                        text += control + "\n";

                    }

                    textCourseNos.Text = text;
                    break;
                }




            }
        }
        private void AddXMLCourse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Title = "Open Courses from IOF 3.0 XML";
            openFileDialog.Filter = "XML File (*.xml)|*.xml|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
                {
                    var courses = db.GetCollection<Course>("courses");
                    var xmlfile = openFileDialog.FileName;
                    XmlDocument doc = new XmlDocument();
                    doc.Load(xmlfile);
                    var courseno = 0;
                    foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                    {
                        if (node.Name == "RaceCourseData")
                        {
                            foreach (XmlNode node1 in node.ChildNodes)
                            {
                                if (node1.Name == "Course")
                                {
                                    var course2 = new Course { };
                                    List<string> controls = new List<string>();
                                    course2.Name = node1["Name"].InnerText;
                                    var thousand = Convert.ToInt32(node1["Length"].InnerText) / 1000;
                                    CourseDistance.Text = Convert.ToString(thousand) + "." + Convert.ToString(Convert.ToInt32(node1["Length"].InnerText) - thousand * 1000);

                                    course2.Climb = node1["Climb"].InnerText;
                                    foreach (XmlNode node2 in node1.ChildNodes)
                                    {

                                        if (node2.Name == "CourseControl")
                                        {
                                            if (node2["Control"].InnerText != "S")
                                            {
                                                if (node2["Control"].InnerText != "F")
                                                {
                                                    controls.Add(node2["Control"].InnerText);
                                                }

                                            }

                                        }
                                    };
                                    course2.ControlCodes = controls.ToArray();
                                    courses.Insert(course2);
                                    courseno = courseno + 1;
                                    textCourseNos.AppendText("Added Course: " + course2.Name + "\n");

                                }
                            }
                        }
                    }
                    Alert alert = new Alert();
                    alert.Title = "";
                    alert.password.Visibility = Visibility.Hidden;
                    alert.progress.Visibility = Visibility.Hidden;
                    alert.Enter.IsEnabled = false;
                    alert.alert.Visibility = Visibility.Visible;
                    alert.textBlock.Text = courseno.ToString() + " Courses imported";
                    alert.Show();
                }
            }
        }

        private void writeLogColorNL(RichTextBox box, string text, string color)
        {
            listboxLog.Dispatcher.Invoke(() =>
            {
                BrushConverter bc = new BrushConverter();
                TextRange tr = new TextRange(box.Document.ContentEnd.InsertLineBreak(), box.Document.ContentEnd);

                tr.Text = text;
                try
                {
                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, bc.ConvertFromString(color));
                    listboxLog.ScrollToEnd();
                }
                catch (FormatException) { }
            });
        }
        private void writeLogColor(RichTextBox box, string text, string color)
        {
            listboxLog.Dispatcher.Invoke(() =>
            {
                BrushConverter bc = new BrushConverter();
                TextRange tr = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd);

                tr.Text = text;
                try
                {
                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, bc.ConvertFromString(color));
                    listboxLog.ScrollToEnd();
                }
                catch (FormatException) { }
            });
        }
        private void entriesLog(string text)
        {
            comboboxPortsList.Dispatcher.Invoke(() =>
            {
                entries.AppendText(text);


            });

        }
        private void resultsLog(string text)
        {
            comboboxPortsList.Dispatcher.Invoke(() =>
            {
                textResults.AppendText(text + "\n");


            });

        }
        private void resultsClear()
        {
            comboboxPortsList.Dispatcher.Invoke(() =>
            {
                textResults.Text = "";

            });

        }
        private void entriesClear()
        {
            comboboxPortsList.Dispatcher.Invoke(() =>
            {
                entries.Text = "";

            });

        }
        private void safetyClear()
        {
            comboboxPortsList.Dispatcher.Invoke(() =>
            {
                textSafety1.Text = "";

            });

        }
        private void safetyLog(string text)
        {
            comboboxPortsList.Dispatcher.Invoke(() =>
            {
                textSafety1.AppendText(text + "\n");


            });

        }
        private void backupButton_Click(object sender, RoutedEventArgs e)
        {
            if (backupNo.Text == null)
            {
                backupNo.Text = "1";
            }
            var newloc = databaselocation.Split('.')[0] + "-backup-" + backupNo.Text + ".evnt";
            File.Copy(databaselocation, newloc);
            Alert alert = new Alert();
            alert.Title = "";
            alert.progress.Visibility = Visibility.Hidden;
            alert.Enter.IsEnabled = false;
            alert.password.Visibility = Visibility.Hidden;
            alert.alert.Visibility = Visibility.Visible;
            alert.textBlock.Text = "Event backed up to: " + newloc;
            alert.Show();

        }

        private void EntriesRefresh_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                var course = "";
                entriesClear();
                var entries = db.GetCollection<Entry>("entries");
                var results = entries.FindAll().OrderBy(x => x.Course);
                var resultlist = from result in results select result;

                foreach (var result in resultlist)
                {
                    if (result.Course != course)
                    {
                        entriesLog(result.Course + ":\n");
                        entriesLog(Convert.ToString("    " + result.Sicard + " - " + result.Name + " - " + result.Class + " - " + result.Club + "\n"));
                        course = result.Course;
                    }
                    else
                    {
                        entriesLog(Convert.ToString("    " + result.Sicard + " - " + result.Name + " - " + result.Class + " - " + result.Club + "\n"));

                    }
                }
            }
        }

        private void importsafety_Click(object sender, RoutedEventArgs e)
        {
            string line;
            var counter = 0;
            safetyClear();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Title = "Open Safety Check Data from SiConfig CSV";
            openFileDialog.Filter = "CSV File(*.csv)|*.csv|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                System.IO.StreamReader file = new System.IO.StreamReader(openFileDialog.FileName);
                while ((line = file.ReadLine()) != null)
                {
                    using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
                    {
                        if (line != "#,SIID,Control time,")
                        {
                            var entriesdata = db.GetCollection<Entry>("entries");
                            var results = entriesdata.Find(Query.EQ("Sicard", line.Split(',')[1]));
                            var resultlist = from result in results select result;

                            if (resultlist.ToArray().Length < 1)
                            {
                                var entry = new Entry { };
                                entry.Sicard = line.Split(',')[1];
                                entry.LastSeenAt = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                                entry.LastSeenTime = Regex.Split(line.Split(',')[2], "   ")[1];
                                entriesdata.Insert(entry);
                                counter = counter + 1;
                                safetyLog(System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName) + " - " + line.Split(',')[1] + " - " + Regex.Split(line.Split(',')[2], "   ")[1]);

                            }
                            foreach (var result in resultlist)
                            {
                                var entry = new Entry { };
                                entry.Name = result.Name;
                                entry.Sicard = result.Sicard;
                                entry.Course = result.Course;
                                entry.Class = result.Class;
                                entry.Start = result.Start;
                                entry.Club = result.Club;
                                entry.Time = result.Time;
                                entry.Downloaded = result.Downloaded;
                                entry.LastSeenAt = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                                entry.LastSeenTime = Regex.Split(line.Split(',')[2], "   ")[1];
                                entry.Id = result.Id;
                                entriesdata.Update(entry);
                                counter = counter + 1;
                                safetyLog(System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName) + " - " + line.Split(',')[1] + " - " + Regex.Split(line.Split(',')[2], "   ")[1]);
                            }

                        }
                    }
                }

                file.Close();
                Alert alert = new Alert();
                alert.Title = "";
                alert.password.Visibility = Visibility.Hidden;
                alert.progress.Visibility = Visibility.Hidden;
                alert.Enter.IsEnabled = false;
                alert.alert.Visibility = Visibility.Visible;
                alert.textBlock.Text = counter + " Punches imported as safety data";
                alert.Show();
            };
        }

        private void refreshsafety_Click(object sender, RoutedEventArgs e)
        {
            safetyClear();
            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                var entriesdata = db.GetCollection<Entry>("entries");
                var results = entriesdata.Find(Query.EQ("Downloaded", false)).OrderBy(x => x.LastSeenTime);
                var resultlist = from result in results select result;
                foreach (var result in resultlist)
                {
                    safetyLog(result.Name + " - " + result.Sicard + " - " + result.Course + " - " + result.LastSeenAt + " - " + result.LastSeenTime);

                }
            }
        }

        private void liveresults_Click(object sender, RoutedEventArgs e)
        {
            LiveResults liveresults = new LiveResults();
            liveresults.Show();
        }
        private Nevis.MainWindow.Archive archiveadd(string line)
        {
            
                var archives = new Archive();
                archives.Sicard = line.Split(',')[0];
                archives.Name = line.Split(',')[3];
                archives.Club = line.Split(',')[7];
                if (line.Split(',')[4] == "m")
                {
                    archives.Class = "M";
                }
                if (line.Split(',')[4] == "f")
                {
                    archives.Class = "W";
                }
                if (line.Split(',')[5].Length == 4)
                {
                    DateTime date = DateTime.Now;
                    var compdate = Convert.ToInt32(date.ToString(line.Split(',')[5]));
                    var currentdate = Convert.ToInt32(date.ToString("yyyy"));
                    var diff = currentdate - compdate;
                    if (line.Split(',')[4] == "m")
                    {
                        if (diff <= 10)
                        {
                            archives.Class = "M10";
                        }
                        else if (diff <= 12) { archives.Class = "M12"; }
                        else if (diff <= 14) { archives.Class = "M14"; }
                        else if (diff <= 16) { archives.Class = "M16"; }
                        else if (diff <= 18) { archives.Class = "M18"; }
                        else if (diff <= 20) { archives.Class = "M20"; }
                        else if (diff <= 21) { archives.Class = "M21"; }
                        else if (diff <= 35) { archives.Class = "M35"; }
                        else if (diff <= 40) { archives.Class = "M40"; }
                        else if (diff <= 45) { archives.Class = "M45"; }
                        else if (diff <= 50) { archives.Class = "M50"; }
                        else if (diff <= 55) { archives.Class = "M55"; }
                        else if (diff <= 60) { archives.Class = "M60"; }
                        else if (diff <= 65) { archives.Class = "M65"; }
                        else if (diff <= 70) { archives.Class = "M70"; }
                        else if (diff <= 75) { archives.Class = "M75"; }
                        else if (diff <= 80) { archives.Class = "M80"; }
                        else if (diff <= 85) { archives.Class = "M85"; }
                        else if (diff <= 90) { archives.Class = "M90"; }
                        else if (diff <= 95) { archives.Class = "M95"; }
                        else if (diff <= 100) { archives.Class = "M100"; }
                    }
                    if (line.Split(',')[4] == "f")
                    {
                        if (diff <= 10)
                        {
                            archives.Class = "W10";
                        }
                        else if (diff <= 12) { archives.Class = "W12"; }
                        else if (diff <= 14) { archives.Class = "W14"; }
                        else if (diff <= 16) { archives.Class = "W16"; }
                        else if (diff <= 18) { archives.Class = "W18"; }
                        else if (diff <= 20) { archives.Class = "W20"; }
                        else if (diff <= 21) { archives.Class = "W21"; }
                        else if (diff <= 35) { archives.Class = "W35"; }
                        else if (diff <= 40) { archives.Class = "W40"; }
                        else if (diff <= 45) { archives.Class = "W45"; }
                        else if (diff <= 50) { archives.Class = "W50"; }
                        else if (diff <= 55) { archives.Class = "W55"; }
                        else if (diff <= 60) { archives.Class = "W60"; }
                        else if (diff <= 65) { archives.Class = "W65"; }
                        else if (diff <= 70) { archives.Class = "W70"; }
                        else if (diff <= 75) { archives.Class = "W75"; }
                        else if (diff <= 80) { archives.Class = "W80"; }
                        else if (diff <= 85) { archives.Class = "W85"; }
                        else if (diff <= 90) { archives.Class = "W90"; }
                        else if (diff <= 95) { archives.Class = "W95"; }
                        else if (diff <= 100) { archives.Class = "W100"; }
                    }

                }


                return (archives);
            } 
        private void archive_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                string line;
                var counter = 0;
                safetyClear();
                var archive = db.GetCollection<Archive>("archive");
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.CheckFileExists = true;
                openFileDialog.CheckPathExists = true;
                openFileDialog.Title = "Open SiCard Archive";
                openFileDialog.Filter = "CSV File(*.csv)|*.csv|All files (*.*)|*.*";
                Alert alert = new Alert();
                alert.Title = "";
                alert.password.Visibility = Visibility.Hidden;
                alert.Enter.IsEnabled = false;
                alert.OK.IsEnabled = false;
                alert.progress.Visibility = Visibility.Visible;
                alert.alert.Visibility = Visibility.Hidden;
                alert.OK1.IsEnabled = false;
                
                if (openFileDialog.ShowDialog() == true)
                {
                   
                    string filex = new StreamReader(openFileDialog.FileName).ReadToEnd();
                    string[] lines = filex.Split('\n');
                    int countOfLines = lines.GetLength(0);
                    countOfLines = countOfLines - 2;

                    alert.progressbar.Maximum = countOfLines;
                    alert.people.Text = "0 of " + countOfLines.ToString() + " Card imported from Archive";

                    StreamReader file = new StreamReader(openFileDialog.FileName);
                    while ((line = file.ReadLine()) != null)
                    {

                        if (line != "\"CardNumber\",\"CardLabel\",\"CardStatus\",\"Name\",\"Sex\",\"DateOfBirth\",\"MembershipNo\",\"Club\"")
                        {
                            archive.Insert(archiveadd(line));
                            counter = counter + 1;
                            alert.people.Text = counter + " of " + countOfLines.ToString() + " Card imported from Archive";
                            alert.progressbar.Value = alert.progressbar.Value + 1;
                        }
                        }
                        alert.progress.Visibility = Visibility.Hidden;
                        alert.alert.Visibility = Visibility.Visible;
                        alert.textBlock.Text = counter.ToString() + " cards imported from the Archive";
                        alert.OK.IsEnabled = true;
                        alert.Show();
                        
                    

                }
            }
        }
        private void ArchiveSearch_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                if (textName.Text == "")
                {
                    var archives = db.GetCollection<Archive>("archive");
                    var results = archives.Find(Query.EQ("Sicard", textSI.Text));
                    var resultlist = from result in results select result;
                    foreach (var result in resultlist)
                    {
                        textName.Text = result.Name.Split('"')[1];
                        textSI.Text = result.Sicard;
                        textClub.Text = result.Club.Split('"')[1];
                        textClass.Text = result.Class;

                    }
                }
                else if (textSI.Text == "")
                {
                    var entries = db.GetCollection<Archive>("archive");
                    var results = entries.Find(Query.Contains("Name", textName.Text));
                    var resultlist = from result in results select result;
                    foreach (var result in resultlist)
                    {
                        textName.Text = result.Name.Split('"')[1];
                        textSI.Text = result.Sicard;
                        textClub.Text = result.Club.Split('"')[1];
                        textClass.Text = result.Class;

                    }
                }


            }
        }
        private void downloadButton_Click(object sender, RoutedEventArgs e)
        {
            Intro.Visibility = Visibility.Hidden;
            Download.Visibility = Visibility.Visible;
            Entries.Visibility = Visibility.Hidden;
            Results.Visibility = Visibility.Hidden;
            Database.Visibility = Visibility.Hidden;
            Courses.Visibility = Visibility.Hidden;
            SafetyCheck.Visibility = Visibility.Hidden;
            UITitle.Text = "Download";
            UITitle.Visibility = Visibility.Visible;
            menu.Visibility = Visibility.Visible;
            downloadButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            entriesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            resultsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            coursesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            safetyButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            settingsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            downloadButton.Background = new SolidColorBrush(Color.FromArgb(255, 193, 234, 254));
            clear();
        }

        private void entriesButton_Click(object sender, RoutedEventArgs e)
        {
            Intro.Visibility = Visibility.Hidden;
            Download.Visibility = Visibility.Hidden;
            Entries.Visibility = Visibility.Visible;
            Results.Visibility = Visibility.Hidden;
            Database.Visibility = Visibility.Hidden;
            Courses.Visibility = Visibility.Hidden;
            SafetyCheck.Visibility = Visibility.Hidden;
            UITitle.Text = "Entries";
            UITitle.Visibility = Visibility.Visible;
            menu.Visibility = Visibility.Visible;
            downloadButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            entriesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            resultsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            coursesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            safetyButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            settingsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            entriesButton.Background = new SolidColorBrush(Color.FromArgb(255, 193, 234, 254));
            clear();

        }

        private void resultsButton_Click(object sender, RoutedEventArgs e)
        {
            Intro.Visibility = Visibility.Hidden;
            Download.Visibility = Visibility.Hidden;
            Entries.Visibility = Visibility.Hidden;
            Results.Visibility = Visibility.Visible;
            Database.Visibility = Visibility.Hidden;
            Courses.Visibility = Visibility.Hidden;
            SafetyCheck.Visibility = Visibility.Hidden;
            UITitle.Text = "Results";
            UITitle.Visibility = Visibility.Visible;
            menu.Visibility = Visibility.Visible;
            downloadButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            entriesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            resultsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            coursesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            safetyButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            settingsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            resultsButton.Background = new SolidColorBrush(Color.FromArgb(255, 193, 234, 254));
            clear();
        }

        private void coursesButton_Click(object sender, RoutedEventArgs e)
        {
            Intro.Visibility = Visibility.Hidden;
            Download.Visibility = Visibility.Hidden;
            Entries.Visibility = Visibility.Hidden;
            Results.Visibility = Visibility.Hidden;
            Database.Visibility = Visibility.Hidden;
            Courses.Visibility = Visibility.Visible;
            SafetyCheck.Visibility = Visibility.Hidden;
            UITitle.Text = "Courses";
            UITitle.Visibility = Visibility.Visible;
            menu.Visibility = Visibility.Visible;
            downloadButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            entriesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            resultsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            coursesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            safetyButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            settingsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            coursesButton.Background = new SolidColorBrush(Color.FromArgb(255, 193, 234, 254));
            clear();
        }

        private void safetyButton_Click(object sender, RoutedEventArgs e)
        {
            Intro.Visibility = Visibility.Hidden;
            Download.Visibility = Visibility.Hidden;
            Entries.Visibility = Visibility.Hidden;
            Results.Visibility = Visibility.Hidden;
            Database.Visibility = Visibility.Hidden;
            Courses.Visibility = Visibility.Hidden;
            SafetyCheck.Visibility = Visibility.Visible;
            UITitle.Text = "Safety Check";
            UITitle.Visibility = Visibility.Visible;
            menu.Visibility = Visibility.Visible;
            downloadButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            entriesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            resultsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            coursesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            safetyButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            settingsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            safetyButton.Background = new SolidColorBrush(Color.FromArgb(255, 193, 234, 254));
            clear();

        }

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            Intro.Visibility = Visibility.Hidden;
            Download.Visibility = Visibility.Hidden;
            Entries.Visibility = Visibility.Hidden;
            Results.Visibility = Visibility.Hidden;
            Database.Visibility = Visibility.Visible;
            Courses.Visibility = Visibility.Hidden;
            SafetyCheck.Visibility = Visibility.Hidden;
            UITitle.Text = "Settings";
            UITitle.Visibility = Visibility.Visible;
            menu.Visibility = Visibility.Visible;
            downloadButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            entriesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            resultsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            coursesButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            safetyButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            settingsButton.Background = new SolidColorBrush(Color.FromArgb(255, 225, 245, 255));
            settingsButton.Background = new SolidColorBrush(Color.FromArgb(255, 193, 234, 254));
            clear();
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
        private void AddXMLCompetitors_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Title = "Open Entries from IOF 3.0 XML";
            openFileDialog.Filter = "XML File (*.xml)|*.xml|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
                {
                    var entries = db.GetCollection<Entry>("entries");
                    var xmlfile = openFileDialog.FileName;
                    XmlDocument doc = new XmlDocument();
                    doc.Load(xmlfile);
                    var courseno = 0;
                    foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                    {
                        if (node.Name == "PersonEntry")
                        {
                            var entry2 = new Entry { };
                            var given = "";
                            var family = "";
                            foreach (XmlNode node1 in node.ChildNodes)
                            {
                                if (node1.Name == "Person")
                                {
                                    foreach (XmlNode node2 in node1.ChildNodes)
                                    {
                                        if (node2.Name == "Name")
                                        {
                                            foreach (XmlNode node3 in node2.ChildNodes)
                                            {
                                                if (node3.Name == "Given")
                                                {
                                                    given = node3.InnerText;
                                                }
                                                if (node3.Name == "Family")
                                                {
                                                    family = node3.InnerText;
                                                }
                                            }
                                            entry2.Name = given + " " + family;
                                        }
                                    }
                                }
                                if (node1.Name == "ControlCard") { entry2.Sicard = node1.InnerText; }
                                if (node1.Name == "Organisation") { entry2.Club = node1["Name"].InnerText; }
                                if (node1.Name == "Class") { entry2.Course = node1["Name"].InnerText; }
                            }
                            if (entry2.Sicard == null)
                            {
                                entriesLog(entry2.Name + " needs to hire a SiCard");
                                entry2.Sicard = "Hire";
                            }
                            entries.Insert(entry2);
                            courseno = courseno + 1;

                        }


                    }


                    Alert alert = new Alert();
                    alert.Title = "";
                    alert.progress.Visibility = Visibility.Hidden;
                    alert.password.Visibility = Visibility.Hidden;
                    alert.Enter.IsEnabled = false;
                    alert.alert.Visibility = Visibility.Visible;
                    alert.textBlock.Text = courseno.ToString() + " Entries imported";
                    alert.Show();


                }
            }
        }

        private void xmlresults_Click(object sender, RoutedEventArgs e)
        {

            var eventname = System.IO.Path.GetFileNameWithoutExtension(databaselocation);
            DateTime date = DateTime.Now;
            var head = $"< Event xmlns: xsd = \"http://www.w3.org/2001/XMLSchema\" xmlns: xsi = \"http://www.w3.org/2001/XMLSchema-instance\" CreatedBy = \"Nevis\" Date = \"\" > < Name >{eventname}</ Name >< Date >{date.ToString("dd-MM-yyyy H:mm:ss")}</ Date ><Results>";
            var footer = "</Event>";
            var resultswrite = "";
            using (var db = new LiteDatabase("filename=" + databaselocation + ";password=" + password + ";"))
            {
                var course = "";
                var entries = db.GetCollection<Entry>("entries");
                var results = entries.Find(Query.EQ("Downloaded", true)).OrderBy(x => x.Course).ThenBy(x => x.Time);
                var resultlist = from result in results select result;
                var a = 1;
                var distance = "";
                var climb = "";
                foreach (var result in resultlist)
                {

                    if (result.Course != course)
                    {
                        if (a == 1)
                        {
                            var courses = db.GetCollection<Course>("courses");

                            var results1 = courses.Find(Query.EQ("Name", result.Course));
                            var resultlist1 = from result1 in results1 select result1;

                            foreach (var result1 in resultlist1)
                            {
                                distance = result1.Distance;
                                climb = result1.Climb;
                                break;
                            }
                            if (resultswrite != "")
                            {
                                resultswrite += $" </CourseResults> <CourseResults Name=\"{ result.Course}\" Distance=\"{distance} \" Climb=\"{ climb} \"> ";
                                resultswrite += $" <Person>< Name >{ result.Name} </ Name > < Position > { a}</ Position > < Club > { result.Club} </ Club >< Class > { result.Time} </ Class > < SiCard > { result.Sicard} </ SiCard > < Time > { result.Time} </ Time ></Person>";
                            }
                            else
                            {
                                resultswrite += $" <CourseResults Name=\"{ result.Course}\" Distance=\"{distance} \" Climb=\"{ climb} \"> ";
                                resultswrite += $" <Person>< Name >{ result.Name} </ Name > < Position > { a}</ Position > < Club > { result.Club} </ Club >< Class > { result.Time} </ Class > < SiCard > { result.Sicard} </ SiCard > < Time > { result.Time} </ Time ></Person>";
                            }
                        }
                        else
                        {
                            var courses = db.GetCollection<Course>("courses");

                            var results1 = courses.Find(Query.EQ("Name", result.Course));
                            var resultlist1 = from result1 in results1 select result1;
                            if (result.Course != "Unknown")
                            {
                                foreach (var result1 in resultlist1)
                                {
                                    distance = result1.Distance;
                                    climb = result1.Climb;
                                    break;
                                }
                                a = 1;
                                resultswrite += $" <CourseResults Name=\"{ result.Course}\" Distance=\"{distance} \" Climb=\"{ climb} \"> ";
                                resultswrite += $" <Person>< Name >{ result.Name} </ Name > < Position > { a}</ Position > < Club > { result.Club} </ Club >< Class > { result.Time} </ Class > < SiCard > { result.Sicard} </ SiCard > < Time > { result.Time} </ Time ></Person>";


                            }
                            else
                            {
                                resultswrite += $" <CourseResults Name=\"{ result.Course}\" Distance=\"{distance} \" Climb=\"{ climb} \"> ";
                                resultswrite += $"<Person> < Name >{ result.Name} </ Name > < Position > { a}</ Position > < Club > { result.Club} </ Club >< Class > { result.Time} </ Class > < SiCard > { result.Sicard} </ SiCard > < Time > { result.Time} </ Time ></Person>";
                            }

                        }
                        course = result.Course;
                        a += 1;
                    }
                    else
                    {
                        resultswrite += $"<Person> < Name >{ result.Name} </ Name > < Position > { a}</ Position > < Club > { result.Club} </ Club >< Class > { result.Time} </ Class > < SiCard > { result.Sicard} </ SiCard > < Time > { result.Time} </ Time ></Person>";
                        a += 1;
                    }
                }
            }
            var text = head + resultswrite + footer;
            File.WriteAllText(@databaselocation.Split('.')[0] + ".xml", text);
            resultsLog("\nFile Written to: " + databaselocation.Split('.')[0] + ".xml");
        }


    }
}






