using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
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
    //DOCUMENT ID IMPLEMENTATION FINISH!!!!
    public partial class Validation
    {
        private string databaseReadString;
        private string databaseWriteString;
        private List<OData> ODataList = new List<OData>();
        private List<RestAPI> RestApiList = new List<RestAPI>();
        private List<OAuth> OAuthList = new List<OAuth>();


        //DO NOT forget to dispsoe database (remove using statment for now)
        //TO IMPROVE PERFORMANCE; savechanges only once as possible and savechanges async (while doing the parsing, look at github)
        //maybe save items in collections (addrange) not sure
        //Contains might cause performance issues, might need to change if program is so slow
        //Will performance imrpove if other log is saved to arraylist and inserted as addrange?
        public Validation(string DatabaseToRead, string DatabaseToWrite)
        {
            this.databaseReadString = DatabaseToRead;
            this.databaseWriteString = DatabaseToWrite;
        }

        public void ParseAndWriteData()
        {
            using (var databaseRead = new WebRequestEntities(databaseReadString))
            {
                //Check if database exists
                if (databaseRead.Database.Exists() == false)
                {
                    throw new Exception("Database to read does not exist");
                }
                using (var databaseWrite = new WebRequestEntities(databaseWriteString))
                {
                    if (databaseWrite.Database.Exists() == false)
                    {
                        throw new Exception("Database to write does not exist");
                    }

                    //Due to the large dataset, 1,000,000
                    foreach (var data in databaseRead.Logs.Take(500000))
                    {
                        if (data.Path != null)
                        {
                            if (data.Path.Contains("odata"))
                            {
                                OData odata = CreateOData(data);
                                ODataList.Add(odata);
                                databaseWrite.ODatas.Add(odata);
                                
                                
                            }
                            if (data.Path.Contains("entity") && !data.Path.Contains("identity")) //if body starts with <, it is soap!
                            {
                                if (data.Body == null)
                                {
                                    RestAPI restapi = CreateRestAPI(data);
                                    RestApiList.Add(restapi);
                                    databaseWrite.RestAPIs.Add(restapi);
                                    
                                }

                                if (data.Body.ToCharArray().Length > 1 && data.Body.ToCharArray()[0] != '<')
                                {
                                    RestAPI restapi = CreateRestAPI(data);
                                    RestApiList.Add(restapi);
                                    databaseWrite.RestAPIs.Add(restapi);
                                    
                                }

                            }
                            //What should be the specific fields for that????
                            if (data.Path.Contains("identity"))
                            {                         
                                OAuth ouath = CreateOAuth(data);
                                OAuthList.Add(ouath);
                                databaseWrite.OAuths.Add(ouath); 
                      
                            }
                            else
                            {
                                //databaseWrite.Logs.Add(data);
                                
                            }
                        }
                        else
                        {
                            //This assumes that requests comes before the responses in terms of ordering in AcuProx
                            if (ODataList.Where(odata => odata.ProcGUID == data.ProcGUID).Any())
                            {
                                OData odata = CreateOData(data);
                                ODataList.Add(odata);
                                databaseWrite.ODatas.Add(odata);
                            }
                            if (RestApiList.Where(odata => odata.ProcGUID == data.ProcGUID).Any())
                            {
                                RestAPI restapi = CreateRestAPI(data);
                                RestApiList.Add(restapi);
                                databaseWrite.RestAPIs.Add(restapi);
                            }
                            if (OAuthList.Where(odata => odata.ProcGUID == data.ProcGUID).Any())
                            {
                                OAuth oauth = CreateOAuth(data);
                                OAuthList.Add(oauth);
                                databaseWrite.OAuths.Add(oauth);
                            }
                            else
                            {

                                //databaseWrite.Logs.Add(data);
                            }

                        }
                    }
                    databaseWrite.SaveChanges();
                }
            }
        }
 
        //For testing purposes, prevents double entries. Reset database everytime parsin method is called!
        public void ClearData(bool log=false, bool odata=false, bool restapi=false, bool oauth = false)
        {
            using (var databaseWrite = new WebRequestEntities(databaseWriteString))
            {
                if (log == true)
                {
                    databaseWrite.Logs.RemoveRange(databaseWrite.Logs);
                }
                if (odata == true)
                {
                    databaseWrite.ODatas.RemoveRange(databaseWrite.ODatas);
                }
                if (restapi == true)
                {
                    databaseWrite.RestAPIs.RemoveRange(databaseWrite.RestAPIs);
                }
                if (oauth == true)
                {
                    databaseWrite.OAuths.RemoveRange(databaseWrite.OAuths);
                }
                
                databaseWrite.SaveChanges();
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

            odata.GIName = ComposeGI(data, odata);
            odata.DACName = ComposeDAC(data, odata);
            

            odata.Select = getParameters(data, "select");
            odata.Filters = getParameters(data, "filter");
            odata.Skip = getParameters(data, "skip");
            odata.Top = getParameters(data, "top");
            odata.Expands = getParameters(data, "expand");
            odata.Sorting = getParameters(data, "orderby");
            if (data.StatusCode!=null && Int16.Parse(data.StatusCode) > 400)
            {
                odata.ErrorCode = data.StatusCode;
                odata.ErrorMessage = data.Body;
            }
            ComposeNumberOfDetails(data, odata);
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
            restapi.Filters = getParameters(data, "filter");
            //add a keyfield to database table. After Entity name for ex Stock Item. after entity name if you add more fields it can also be considered as a filter
            //then in the validations part, check if keyfield and filter parameters are applied and whether it acts as a repetition of filters
            restapi.Select = getParameters(data, "select");
            restapi.Expands = getParameters(data, "expand");
            restapi.Custom = getParameters(data, "custom");
            restapi.Skip = getParameters(data, "skip");
            restapi.Top = getParameters(data, "top");
            //ADD those 6 parameters to the table, onyl for REST table!
            RestComposeEndpointAndKeyField(restapi, data.Path);
            
            if(data.StatusCode != null && Int16.Parse(data.StatusCode) > 400) 
            {
                restapi.ErrorCode = data.StatusCode;
                restapi.ErrorMessage = data.Body;
            }
            ComposeNumberOfDetails(data, null, restapi);
            restapi.AccessScope = ComposeAccessScope(data); //QUESTION
            restapi.Duration = data.Duration;
            restapi.DocumentID = GetDocumentID(data);
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

        //CHECK!!!
        private void RestComposeEndpointAndKeyField(RestAPI restapi, string path)
        {
            if (path != null)
            {
                Regex regex = new Regex("/");
                var pathElement = regex.Split(path);
                if (path.Contains("Login") || path.Contains("logout") || path.Contains("login"))
                {
                    restapi.EndpointName = pathElement[3];
                    restapi.EndpointVersion = null;
                    restapi.EndpointEntity = pathElement[4];//everything that comes after the 4th elenet (considered as a filter)
                   

                }
                else
                {
                    restapi.EndpointName = pathElement[3];
                    restapi.EndpointVersion = pathElement[4];
                    restapi.EndpointEntity = pathElement[5];//everything that comes after the 4th elenet (considered as a filter)
                    if (pathElement.Length > 6)
                    {
                        string keyfield = pathElement[5];
                        for (var i = 6; i < pathElement.Length; i++)
                        {
                            keyfield =  pathElement[i];
                        }
                        restapi.KeyField = keyfield;
                    }
                }
            }

        }

        private string GetDocumentID(Log data)
        {
            if (data.Path != null && data.Path.Contains("id:"))
            {
                var stringStartWithSessionID = data.Cookies.Substring(data.Cookies.IndexOf("id:"));
                var splitString = stringStartWithSessionID.Split('{'); //Assuming it is always at the end 
                return splitString[0];
            }
            else
            {
                return null;
            }
        }

        private string ComposeGI(Log data, OData odata)
        {
            if (data.Path != null)
            {
                if (data.Path.Contains("%"))
                {
                    return data.Path.Substring(data.Path.LastIndexOf("/"));
                }
            }
            return null;

        }

        private void ComposeNumberOfDetails(Log data, OData odata=null, RestAPI restapi=null)
        {
            var pattern = @"\[(.*?)\]"; //seperates everythings inside brackets 
            var matches = Regex.Matches(data.Body, pattern);
            var count =((short)matches.Count);
            if (odata != null)
            {
                odata.NumberOfDetails = count;
            }
            if(restapi != null)
            {
                restapi.NumberOfDetails = count;
            }
            
        }

        private string ComposeAccessScope(Log data)
        {
            if (data.Path != null)
            {
                //Assuming that access scope comes after "?" symbol
                if (data.Path.Contains("authorize"))
                {
                    return data.QueryString;
                }
            }
            return null;
        }

        private string ComposeDAC(Log data, OData odata)
        {
            if (data.Path != null)
            {
                if (data.Path.Contains("PX.")) // gets DAC from PX to parameter, assumes it will be always at the end !!!?????
                {
                    return data.Path.Substring(data.Path.LastIndexOf("/"));
                }

            }
            return null;
        }
       
        
        private string ComposeSessionID(Log data)
        {
            if (data.Cookies.Contains("ASP.NET"))
            {
                var stringStartWithSessionID = data.Cookies.Substring(data.Cookies.IndexOf("SessionId"));
                var splitString = stringStartWithSessionID.Split(',');
                return splitString[0];
                
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

        private string getParameters(Log data, string key)
        {
            var parameters = ComposeParameters(data);
            if (parameters.ContainsKey(key))
            {
                return parameters[key];
            }
            else
            {
                return null;
            }
        }

        //Returns 1 if true, 0 if false.
        private short DecideIsAcumaticaRequest(Log data)
        {
            if (data.Host != null)
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
            return 0;
        }

        private string ComposeAuthenticaton(Log data, OData odata=null, RestAPI restapi = null)
        {
            if(odata != null)
            {
                if (data.Headers.Contains("token"))
                {
                    return "OAuth";
                }
                else 
                {
                    return "BasicAuthentication";
                }
            }
            if(restapi != null && data.Headers != null)
            {
                if (data.Headers.Contains("token"))
                {
                    return "OAuth";
                }
                if (data.Cookies != null && data.Cookies.Contains("ASP.NET"))
                {
                    return "Cookies";
                }
            }
            return null;
        }

        private string ComposeTypeOfWebService(Log data, OData odata = null, RestAPI restapi = null)
        {
            if (odata != null && odata.Path !=null)
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
            if(restapi != null && restapi.Path != null)
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

        
      


    }
}
