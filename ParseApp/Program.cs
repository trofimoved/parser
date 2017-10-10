using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.IO;

namespace ParseApp
{
    public class Location
    {
        public string LocationId { get; set; }
        public string LocationName { get; set; }
        public string ParentLocationId { get; set; }
        [ScriptIgnore]
        public string Side { get; set; }
        [ScriptIgnore]
        public string Last { get; set; }
        public string Gender { get; set; }

        public List<Location> ChildLocations { get; set; }
        public List<Symptom> Symptoms { get; set; }

        public Location()
        {
            ChildLocations = new List<Location>();
            Symptoms = new List<Symptom>();
        }
    }

    public class Symptom
    {
        public string SymptomId { get; set; }
        public string SymptomName { get; set; }
        public string ParentSymptomId { get; set; }
        [ScriptIgnore]
        public string Last { get; set; }
        public string Gender { get; set; }
        public string LocationId { get; set; }

        public List<Symptom> ChildSymtoms { get; set; }

        public Symptom()
        {
            ChildSymtoms = new List<Symptom>();
        }
    }

    public class LocationList
    {
        public string LocationId { get; set; }
        public string LocationName { get; set; }
        public string ParentLocationId { get; set; }        
        public string Gender { get; set; }

        public List<SymptomList> Symptoms { get; set; }
        public LocationList()
        {
            Symptoms = new List<SymptomList>();
        }

    }

    public class SymptomList
    {
        public string SymptomId { get; set; }
        public string SymptomName { get; set; }
        public string ParentSymptomId { get; set; }    
        public string Gender { get; set; }
        public string LocationId { get; set; }
    }

    class Program
    {

        static List<Location> getLocations(string gender, string side, string parent, out List<string> frontSide, List<string> backSide = null)
        {
            List<Location> result = new List<Location>();
            string myUri = "https://online-diagnos.ru/index.php?option=com_socdiagnostics&controller=ajaxlocation&task=get_location";
            HttpClient myHttpClient = new HttpClient();

            string symGender = gender == "m" ? "sex_man" : "sex_girl";
            frontSide = new List<string>();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("gender", gender),
                new KeyValuePair<string, string>("side", side),
                new KeyValuePair<string, string>("parent", parent),
                new KeyValuePair<string, string>("age", "gr_5")
            });

            var response = myHttpClient.PostAsync(myUri, formContent).Result;
            if (response.IsSuccessStatusCode)
            {
                JObject myJObject = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                var tokens = myJObject.SelectTokens("$.data");
                foreach (var token in tokens.Children())
                {

                    string id = token.SelectToken("$.id").ToString();
                    string last = token.SelectToken("$.last").ToString();
                    string name = token.SelectToken("$.name").ToString();
                    //Console.WriteLine(name);
                    List<Location> childLocs = new List<Location>();

                    if (last == "0")
                    {
                        List<string> frontside;
                        childLocs = getLocations(gender, "front", id, out frontside);
                        childLocs.AddRange(getLocations(gender, "back", id, out frontside, frontside));
                    }
                    
                    if (backSide == null || (backSide != null && !backSide.Contains(id)))
                    {
                        frontSide.Add(id);
                        result.Add(new Location
                        {
                            LocationId = id,
                            LocationName = name,
                            Last = last,
                            ParentLocationId = parent,
                            Gender = gender,
                            Side = side,
                            ChildLocations = childLocs,
                            Symptoms = getSymptoms(symGender, id)
                        });
                    }

                }
            }

            return result;
        }

        static List<Symptom> getSymptoms(string gender, string location, string parent = null)
        {
            List<Symptom> result = new List<Symptom>();
            string myUri = "https://online-diagnos.ru/index.php?option=com_socdiagnostics&controller=ajaxdiagnostics&task=get_root_symptome";
            HttpClient myHttpClient = new HttpClient();
            string param;
            if (String.IsNullOrEmpty(parent))
                param = String.Format("{{\"sex\":\"{0}\",\"symptoms\":[],\"location\":\"{1}\"}}", gender, location);
            else
                param = String.Format("{{\"sex\":\"{0}\",\"symptoms\":[],\"location\":\"{1}\",\"parent\":{2}}}", gender, location, parent);

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("params", param)
            });

            var response = myHttpClient.PostAsync(myUri, formContent).Result;

            if (response.IsSuccessStatusCode)
            {
                JObject myJObject = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                var tokens = myJObject.SelectTokens("$.root");
                foreach (var token in tokens.Children())
                {

                    string id = token.SelectToken("$.id").ToString();
                    string last = token.SelectToken("$.last").ToString() == "False" ? "0" : "1";
                    string name = token.SelectToken("$.name").ToString();
                    Draw(name);

                    List<Symptom> childLocs = new List<Symptom>();

                    if (last == "0")
                    {
                        childLocs = getSymptoms(gender, location, id);
                    }

                    result.Add(new Symptom
                    {
                        SymptomId = id,
                        SymptomName = name,
                        Last = last,
                        ParentSymptomId = parent,
                        Gender = gender == "sex_man" ? "m" : "w",
                        LocationId = location,
                        ChildSymtoms = childLocs
                    });
                }
            }

            return result;
        }

        static List<LocationList> getLocationsList(List<Location> locations)
        {
            List<LocationList> result = new List<LocationList>();
            foreach (var location in locations)
            {
                if (location.ChildLocations.Count() == 0)
                {
                    //LocationList locationItem = new LocationList();
                    List<SymptomList> symptomList = new List<SymptomList>();
                    foreach (var symptom in location.Symptoms)
                    {
                        symptomList.Add(new SymptomList
                        {
                            SymptomId = symptom.SymptomId,
                            SymptomName = symptom.SymptomName,
                            LocationId = symptom.LocationId,
                            Gender = symptom.Gender,
                            ParentSymptomId = symptom.ParentSymptomId
                        });
                        if (symptom.ChildSymtoms.Count() != 0)
                        {
                            foreach (var csymptom in symptom.ChildSymtoms)
                            {
                                symptomList.Add(new SymptomList
                                {
                                    SymptomId = csymptom.SymptomId,
                                    SymptomName = csymptom.SymptomName,
                                    LocationId = csymptom.LocationId,
                                    Gender = csymptom.Gender,
                                    ParentSymptomId = csymptom.ParentSymptomId
                                });
                                if (csymptom.ChildSymtoms.Count() != 0)
                                {
                                    foreach (var ccsymptom in csymptom.ChildSymtoms)
                                    {
                                        symptomList.Add(new SymptomList
                                        {
                                            SymptomId = ccsymptom.SymptomId,
                                            SymptomName = ccsymptom.SymptomName,
                                            LocationId = ccsymptom.LocationId,
                                            Gender = ccsymptom.Gender,
                                            ParentSymptomId = ccsymptom.ParentSymptomId
                                        });
                                    }
                                }
                            }
                        }
                    }

                    result.Add(new LocationList
                    {
                        LocationId = location.LocationId,
                        LocationName = location.LocationName,
                        ParentLocationId = location.ParentLocationId != location.LocationId ? location.ParentLocationId : null,
                        Gender = location.Gender,
                        Symptoms = symptomList
                    });
                }
                else
                {
                    result.AddRange(getLocationsList(location.ChildLocations));
                }
            }
            return result;
        }

        static List<Location> loadFromFile(string path)
        {
            try
            {
                string text = "";
                using (StreamReader sr = new StreamReader($@"{path}Структура.json", Encoding.Unicode))
                {
                    string line;                    
                    while ((line = sr.ReadLine()) != null)
                    {
                        text += line;
                    }
                }
                return JsonConvert.DeserializeObject<List<Location>>(text);
            }
            catch(Exception ex)
            {
                return new List<Location>();
            }
        }

        static void Main(string[] args)
        {
            List<Location> locationsMan = new List<Location>();
            List<Location> locationsWoman = new List<Location>();

            List<LocationList> locationsListMan = new List<LocationList>();
            List<LocationList> locationsListWoman = new List<LocationList>();

            //string comand = "";
            //do
            //{
            //    comand = Read(Console.ReadLine());
            //}
            //while (String.IsNullOrEmpty(comand));

            List<string> outList;

            //locationsMan = loadFromFile(@"C:\Users\User\Desktop\");

            locationsMan = getLocations("m", "front", "0", out outList);
            locationsWoman = getLocations("w", "front", "0", out outList);

            locationsListMan = getLocationsList(locationsMan).OrderBy(x => Convert.ToInt32(x.LocationId)).ToList();
            locationsListWoman = getLocationsList(locationsWoman).OrderBy(x => Convert.ToInt32(x.LocationId)).ToList();

            //ToConsole(locationsMan);
            //ToConsole(locationsWoman);

            locationsMan.AddRange(locationsWoman);
            locationsListMan.AddRange(locationsListWoman);
            Console.WriteLine();
            try
            {
                //using (StreamWriter sw = new StreamWriter($"{Directory.GetCurrentDirectory()}\\Структруа.json", false, Encoding.Unicode))
                //{                
                //    sw.WriteLine(new JavaScriptSerializer().Serialize(locationsMan));
                //}
                //Console.WriteLine($"Файл {Directory.GetCurrentDirectory()}\\Структура.json сохранен");

                using (StreamWriter sw = new StreamWriter($"{Directory.GetCurrentDirectory()}\\Список_Локализации_Симптомы.json", false, Encoding.Unicode))
                {                    
                    sw.WriteLine(new JavaScriptSerializer().Serialize(locationsListMan));
                }
                Console.WriteLine($"Файл {Directory.GetCurrentDirectory()}\\Список_Локализации_Симптомы.json сохранен");
            }
            catch
            {
                ReSave(locationsListMan);
            }


            Console.WriteLine("\nДля выхода из программы нажмите любую кнопку");
            Console.ReadKey();
        }

        static int _symptCount = 0;

        static void ToConsole(List<Location> locations, string tab = "")
        {
            tab += "  ";

            foreach (var location in locations)
            {
                Console.WriteLine(tab + "id: {0} | name: {1} | side: {2}", location.LocationId, location.LocationName, location.Side);
                if (location.Symptoms.Count() != 0)
                    ToConsole(location.Symptoms, tab);
                if (location.ChildLocations.Count() != 0)
                    ToConsole(location.ChildLocations, tab);
            }
        }

        static void ToConsole(List<Symptom> symptoms, string tab = "")
        {
            tab += "  ";

            foreach (var symptom in symptoms)
            {
                Console.WriteLine(tab + "id: {0} | name: {1}", symptom.SymptomId, symptom.SymptomName);
                if (symptom.ChildSymtoms.Count() != 0)
                    ToConsole(symptom.ChildSymtoms, tab);
            }
        }

        static void Draw(string name)
        {
            _symptCount++;
            Console.WriteLine(_symptCount + "  " + name);
        }

        static void ReSave(List<LocationList> locationsListMan)
        {
            bool saved = false;
            Console.WriteLine($@"Не удалось сохранить файл {Directory.GetCurrentDirectory()}\\Список_Локализации_Симптомы.json");
            do
            {
                string path = "";
                Console.WriteLine("Введите директорию для файла Список_Локализации_Симптомы.json");
                path = Console.ReadLine();
                try
                {
                    using (StreamWriter sw = new StreamWriter($@"{path}Список_Локализации_Симптомы.json", false, Encoding.Unicode))
                    {
                        sw.WriteLine(new JavaScriptSerializer().Serialize(locationsListMan));
                    }
                    Console.WriteLine($"Файл {path}Список_Локализации_Симптомы.json сохранен");
                    saved = true;
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Не удалось сохранить файл {path}Список_Локализации_Симптомы.json");
                }
            }
            while (!saved);

        }

        static string Read(string line)
        {
            if (Regex.IsMatch(line, @"^-[a-zA-Z]"))
            {
                line = line.Remove(0, 1);
                if (line == "q")
                {
                    return "0";
                }
                return "";
            }
            else
            {
                Console.WriteLine("not a comand");
                return "";
            }
        }

    }
}
