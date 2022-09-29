using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcumaticaValidations
{
    public class Validation
    {
        private WebRequestEntities databaseRead;
        private WebRequestEntities databaseWrite;
        private List<OData> ODataList = new List<OData>();
        private List<RestAPI> RestApiList = new List<RestAPI>();
        private List<Other> NonAcceptedList = new List<Other>();

        public Validation(WebRequestEntities DatabaseToRead, WebRequestEntities DatabaseToWrite)
        {
            this.databaseRead = DatabaseToRead;
            this.databaseWrite = DatabaseToWrite;
        }

        public void ParseAndWriteData()
        {
            using (databaseRead)
            {
                //Check if database exists
                if (databaseRead.Database.Exists() == false)
                {
                    throw new Exception("Database to read does not exist");
                }
                using (databaseWrite)
                {
                    if (databaseWrite.Database.Exists() == false)
                    {
                        throw new Exception("Database to write does not exist");
                    }
                    foreach (var data in databaseRead.Logs)
                    {
                        if (data.Path != null)
                        {
                            if (data.Path.Contains("odata"))
                            {
                                OData odata = CreateOData(data);
                                databaseWrite.ODatas.Add(odata);
                                databaseWrite.SaveChanges();
                            }

                            if (data.Path.Contains("entity") && data.Body.ToCharArray()[0] != '<') //if body starts with <, it is soap!
                            {
                                RestAPI restapi = CreateRestAPI(data);
                                databaseWrite.RestAPIs.Add(restapi);
                                databaseWrite.SaveChanges();
                            }
                            if (data.Path.Contains("identity"))
                            {
                                OAuth ouath = CreateOAuth(data);
                                databaseWrite.OAuths.Add(ouath);
                                databaseWrite.SaveChanges();
                            }
                            else
                            {
                                Other other = createOtherLog(data);
                                databaseWrite.Other.Add(other);
                                databaseWrite.SaveChanges();
                            }
                        }
                        else
                        {
                            Other other = createOtherLog(data);
                            databaseWrite.Other.Add(other);
                            databaseWrite.SaveChanges();
                        }
                    }
                }
            }
        }

        private OData CreateOData(Log data)
        {
            OData odata = new OData();
            odata.Id = data.Id;
            odata.Path = data.Path;
            odata.ProcGUID = data.ProcGUID;
            odata.TypeOfWebService = ComposeTypeOfWebService(data, odata);
            odata.IsAcumaticaRequest = DecideIsAcumaticaRequest(data);
            odata.TypeOfRequest = data.EventType;
            odata.SiteURL = data.Host;
            odata.SessionID = ComposeSessionID(data);  
            odata.AuthenticationType = ComposeAuthenticaton(data, odata);
            if (data.Path.Contains("%"))
            {
                odata.GIName = data.Path.Substring(data.Path.LastIndexOf("/"));
            }
            if (data.Path.Contains("PX.")) // gets DAC from PX to parameter, assumes it will be always at the end
            {
                odata.DACName = data.Path.Substring(data.Path.LastIndexOf("/"));
            }
            odata.Select = ComposeParameters(data)["select"];
            odata.Filters = ComposeParameters(data)["filter"];
            odata.Skip = ComposeParameters(data)["skip"];
            odata.Top = ComposeParameters(data)["top"];
            odata.Expands = ComposeParameters(data)["expand"];
            odata.Sorting = ComposeParameters(data)["orderby"];
            if (Int16.Parse(data.StatusCode) > 400)
            {
                odata.ErrorCode = data.StatusCode;
                odata.ErrorMessage = data.Body;
            }
            //Assuming that access scope comes after "?" symbol
            if (data.Path.Contains("authorize"))
            {
                odata.AccessScope = data.QueryString; //NOT SURE
            }
            var pattern = @"\[(.*?)\]"; //seperates everythings inside brackets 
            var matches = Regex.Matches(data.Body, pattern);
            odata.NumberOfDetails = ((short)matches.Count); //it was char in database make sure you uptade the program as well!!!
            odata.Duration = data.Duration;
            return odata;

        }

        private RestAPI CreateRestAPI(Log data)
        {
            
            RestAPI restapi = new RestAPI();
            restapi.Id = data.Id;
            restapi.ProcGUID = data.ProcGUID;
            restapi.Path = data.Path;
            restapi.TypeOfWebService = ComposeTypeOfWebService(data, null, restapi);
            restapi.IsAcumaticaRequest = DecideIsAcumaticaRequest(data);
            restapi.TypeOfRequest = data.EventType;
            restapi.SiteURL = data.Host;
            restapi.SessionID = ComposeSessionID(data);
            restapi.AuthenticationType = ComposeAuthenticaton(data, null, restapi);
            restapi.Filters = ComposeParameters(data)["filter"];
            //add a keyfield to database table. After Entity name for ex Stock Item. after entity name if you add more fields it can also be considered as a filter
            //then in the validations part, check if keyfield and filter parameters are applied and whether it acts as a repetition of filters
            restapi.Select = ComposeParameters(data)["select"];
            restapi.Expands = ComposeParameters(data)["expand"];
            restapi.Custom = ComposeParameters(data)["custom"];
            restapi.Skip = ComposeParameters(data)["skip"];
            restapi.Top = ComposeParameters(data)["top"];
            //ADD those 6 parameters to the table, onyl for REST table!

            Regex regex = new Regex("/");
            var pathElement = regex.Split(data.Path);
            restapi.EndpointName = pathElement[2];
            restapi.EndpointVersion = pathElement[3];
            restapi.EndpointEntity = pathElement[4];//everything that comes after the 4th elenet (considered as a filter)
            if(pathElement.Length > 5)
            {
                string keyfield = pathElement[5];
                for (var i = 6; i<pathElement.Length; i++)
                {
                    keyfield = ", " + pathElement[i];
                }
                restapi.KeyField = keyfield;
            }
            if(Int16.Parse(data.StatusCode) > 400) 
            {
                restapi.ErrorCode = data.StatusCode;
                restapi.ErrorMessage = data.Body;
            }
            restapi.AccessScope = null; //QUESTION
            restapi.Duration = data.Duration;
            restapi.DocumentID = null;// in the body there should be a field called ID (BUT I dont know the structure!, wait until you get the new log with rest API)
            return restapi;
        }

        private OAuth CreateOAuth(Log data)
        {
            OAuth oauth = new OAuth();
            oauth.Id = data.Id;
            oauth.Duration = data.Duration;
            oauth.QueryString = data.QueryString;
            oauth.StatusCode = data.StatusCode;
            oauth.Body = data.Body;
            oauth.Cookies = data.Cookies;
            oauth.DT = data.DT;
            oauth.EventType = data.EventType;
            oauth.Headers = data.Headers;
            oauth.Host = data.Host;
            oauth.Path = data.Path;
            oauth.ProcGUID = data.ProcGUID;
            return oauth;
        }
        private Other createOtherLog(Log data)
        {
            Other other = new Other();
            other.Id = data.Id;
            other.Duration = data.Duration;
            other.QueryString = data.QueryString;
            other.StatusCode = data.StatusCode;
            other.Body = data.Body;
            other.Cookies = data.Cookies;
            other.DT = data.DT;
            other.EventType = data.EventType;
            other.Headers = data.Headers;
            other.Host = data.Host;
            other.Path = data.Path;
            other.ProcGUID = data.ProcGUID;
            return other;
        }

        private string ComposeSessionID(Log data)
        {
            if (data.Cookies.Contains("ASP"))
            {
                var stringStartWithSessionID = data.Cookies.Substring(data.Cookies.IndexOf("ASP"));
                var indexOfNextAvailableComma = stringStartWithSessionID.IndexOf(",");
                if(indexOfNextAvailableComma != -1) //it ends with session ID
                {
                    return stringStartWithSessionID;
                }
                else
                {
                    return stringStartWithSessionID.Substring(0, indexOfNextAvailableComma);
                }
            }
            else
            {
                return null;
            }
        }

        //there should be mpore parameters, look at the teams messages
        private Dictionary<string, string> ComposeParameters(Log data)
        {
            Dictionary<string, string> parameterList = new Dictionary<string, string>();
            if (data.QueryString != null)
            {
                Regex regex = new Regex("\\$");
                var parameters = regex.Split(data.QueryString);
                foreach (var param in parameters)
                {
                    if (param.Contains("filter"))
                    {
                        parameterList.Add("filter", param);
                    }
                    if (param.Contains("select"))
                    {
                        parameterList.Add("select", param);
                    }
                    if (param.Contains("custom"))
                    {
                        parameterList.Add("custom", param);
                    }
                    if (param.Contains("expand"))
                    {
                        parameterList.Add("expand", param);
                    }
                    if (param.Contains("top"))
                    {
                        parameterList.Add("top", param);
                    }
                    if (param.Contains("skip"))
                    {
                        parameterList.Add("skip", param);
                    }
                    if (param.Contains("orderby"))
                    {
                        parameterList.Add("orderby", param);
                    }
                }
            }
            return parameterList;
        }

        //Returns 1 if true, 0 if false.
        private short DecideIsAcumaticaRequest(Log data)
        {
            if (data.Host.Equals("isvtest.acumatica.com"))
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        private string ComposeAuthenticaton(Log data, OData odata=null, RestAPI restapi = null)
        {
            if(odata != null)
            {
                if (data.Headers.Contains("token"))
                {
                    return "OAuth";
                }
                if (odata.SessionID != null)
                {
                    return "BasicAuthentication";
                }
            }
            if(restapi != null)
            {
                if (data.Headers.Contains("token"))
                {
                    return "OAuth";
                }
                if (restapi.SessionID.Contains("Cookies")){
                    return "Cookies";
                }
            }
            return null;
        }

        private string ComposeTypeOfWebService(Log data, OData odata = null, RestAPI restapi = null)
        {
            if (odata != null)
            {
                if (data.Path.Contains("ODatav4"))
                {
                    return "ODatav4";
                }
                else
                {
                    return "OData";
                }
            }
            if(restapi != null)
            {
                if (data.Headers.Contains("identity"))
                {
                    return "Oauth";
                }
                else
                {
                    return "REST";
                }
            }
            return null;
        }

        
        public void ValidateOData()
        {
            using (databaseWrite)
            {
                if (databaseWrite.ODatas.Count<OData>() > 10000)
                {
                    Console.WriteLine("Amount of records shouldn't be more than 10,000");
                }
                var duplicates = databaseWrite.ODatas.GroupBy(i => i.ProcGUID)
                     .Where(x => x.Count() > 1)
                     .Select(val => val.Key);
                if (duplicates.Any())
                {
                    Console.WriteLine("Repeated requests exist! The ProcGUID of the duplicate requests:");
                }
                foreach(var dup in duplicates)
                {
                    Console.WriteLine(dup.HasValue.ToString());
                }
                int metadataCount = 0; //add metadaata info to GI or DAC
                foreach (var data in databaseWrite.ODatas)
                {
                    if(Int16.Parse(data.ErrorCode) == 401) // 401 unauthorized 
                    {
                        Console.WriteLine("Request with ID, " +data.Id + "is not properly identified.");
                    }
                    TimeSpan twentySeconds = new TimeSpan(0,0,0,20);
                    if (data.TypeOfRequest == 2 && twentySeconds.CompareTo(data.Duration)<0)//response and if duration>20
                    {
                        Console.WriteLine("Request with ID, " + data.Id + "exceeds 20 seconds.");
                    }
                    if (data.Path.Contains("metadata"))
                    {
                        metadataCount++;
                    }
                }
                if(metadataCount > 1)
                {
                    Console.Write("$metadata request happens multiple times");
                }
            }
        }

        public void ValidateRestAPI()
        {
            using (databaseWrite)
            {
                //is #login = # logout enough indicator for every login has its chekcout?

            }
        }


    }
}
