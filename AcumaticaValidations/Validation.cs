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

        //QUESTION EFFICIENCY: which way it woudl be more efficent? Reading from arraylist or reading from database?

        //Should handle differentiating between OData, Rest, and non-accepted requests
        //ISSUE: how to distingusih REST? Is there even a rest example in this database, AcuProx
        //NOT READ AND WRITE SHOULD HAPPEN AT THE SAME TIME!
        public void parseData()
        {
            using (databaseRead)
            {
                //Check if database exists
                if(databaseRead.Database.Exists() == false)
                {
                    throw new Exception("Database does not exist");
                }
                foreach (var data in databaseRead.Logs)
                {
                    if (data.Path != null)
                    {
                        if (data.Path.Contains("odata"))
                        {
                            OData odata = CreateOData(data);                            
                            this.ODataList.Add(odata);

                            
                            //this.ODataList.Add((OData)data);

                            Console.WriteLine("odata");
                        }

                        if (data.Path.Contains("entity")) //may not be unique to REST, might also be capturing the SOAP, doesnt start with <
                        {
                            //this.RestApiList.Add((RestAPI) data);
                        }
                        else
                        {
                            //this.NonAcceptedList.Add((Other) data);
                        }
                    }
                    else
                    {
                        //this.NonAcceptedList.Add((Other)data);
                    }
                }
            }
        }

       

        public void writeData(){
            using (databaseWrite)
            {
                foreach (var data in ODataList)
                {
                    databaseWrite.ODatas.Add(data);
                    databaseWrite.SaveChanges();
                }
            }
        
        }
        private OData CreateOData(Log data)
        {
            OData odata = new OData();
            odata.Id = data.Id;
            odata.TypeOfWebService = ComposeTypeOfWebService(data, odata);
            odata.IsAcumaticaRequest = null; //NOT SURE?????
            odata.TypeOfRequest = data.EventType;
            odata.SiteURL = data.Host; // IS this TRUE?
            odata.SessionID = null; //found in cookies but cookies are empty in ACUPROX, what is the format? ??Header authorization (basic auth part)
            odata.AuthenticationType = ComposeAuthenticaton(data, odata);
            
            if (data.Path.Contains("%")) // Is GI and DAC mutally exclusive?
            {
                odata.GIName = data.Path.Substring(data.Path.LastIndexOf("/"));
            }
            odata.GIName = null; //?????
            if (data.Path.Contains("PX.")) // gets DAC from PX to parameter, assumes it will be always at the end
            {
                //int pFrom = data.Path.IndexOf("PX.") + "PX.".Length;
                //int pTo = data.Path.LastIndexOf("?");

                //String result = data.Path.Substring(pFrom, pTo - pFrom);
                odata.DACName = data.Path.Substring(data.Path.LastIndexOf("/"));
            }
            
            if(data.QueryString != null)
            {
                Regex regex = new Regex("\\$");
                var parameters = regex.Split(data.QueryString);
                foreach(var param in parameters)
                {
                    if (param.Contains("filter"))
                    {
                        odata.Filters = param;
                    }
                    if (param.Contains("select"))
                    {
                        odata.Select = param;
                    }

                    if (param.Contains("expand"))
                    {
                        odata.Expands = param;
                    }
                    if (param.Contains("top"))//does that fit into SORTING Column?
                    {
                        odata.Sorting = param;
                    }
                }
            }
            if (data.EventType == 3)
            {
                odata.ErrorCode = data.StatusCode;
                odata.ErrorMessage = data.Body;
            }
            

            //Should access scope be considered in querystring since it comes after "?" symbol ????
            if (data.Path.Contains("authorize"))
            {
                odata.AccessScope = data.QueryString; //NOT SURE
            }
            var pattern = @"\[(.*?)\]"; //seperates everythings inside brackets //WAITING A RESPONSE (amount of details or number of elements in a body?)
            var matches = Regex.Matches(data.Body, pattern);
            odata.NumberOfDetails = ((short)matches.Count); //it was char in database make sure you uptade the program as well!!!
            odata.Duration = data.Duration;
            return odata;

        }

        public RestAPI CreateRestAPI(Log data)
        {
            RestAPI restapi = new RestAPI();
            restapi.Id = data.Id;
            restapi.TypeOfWebService = ComposeTypeOfWebService(data, null, restapi);
            restapi.IsAcumaticaRequest = null; //????
            restapi.TypeOfRequest = data.EventType;
            restapi.SiteURL = data.Host;
            restapi.SessionID = null; //?????  in cookies if request is a response.  
            restapi.AuthenticationType = ComposeAuthenticaton(data, null, restapi);
            restapi.Filters = ComposeParameters(data)["filter"];
            //add a keyfield to database table. After Entity name for ex Stock Item. after entity name if you add more fields it can also be considered as a filter
            restapi.Select = ComposeParameters(data)["select"];
            restapi.Expands = ComposeParameters(data)["expand"];

            Regex regex = new Regex("/");
            var pathElement = regex.Split(data.Path);
            restapi.EndpointName = pathElement[2];
            restapi.EndpointVersion = pathElement[3];
            restapi.EndpointEntity = pathElement[4];//NOTE add the following filtering case to KEYFIELD table! eveything that comes after the 4th elenet
            if(data.EventType == 3)
            {
                restapi.ErrorCode = data.StatusCode;
                restapi.ErrorMessage = data.Body;
            }
            restapi.AccessScope = null; //QUESTION
            restapi.Duration = data.Duration;
            restapi.DocumentID// in the body there should be a field called ID (BUT I dont know the structure!, wait until you get the new log with rest API)
            return restapi;


        }

        private Dictionary<string, string> ComposeParameters(Log data)
        {
            Dictionary<string, string> parameterList = new Dictionary<string, string>);
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

                    if (param.Contains("expand"))
                    {
                        parameterList.Add("expand", param);
                    }
                    if (param.Contains("top"))//does that fit into SORTING Column?
                    {
                        parameterList.Add("top", param);
                    }
                }
            }
            return parameterList;
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
                if (restapi.SessionID.Contains("Cookies"){
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

        }


    }
}
