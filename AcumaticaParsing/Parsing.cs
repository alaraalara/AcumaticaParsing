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
    //DOCUMENT ID Is Missing!
    public partial class Parsing
    {
        private string databaseReadString;
        private string databaseWriteString;
        private List<OData> ODataList = new List<OData>();
        private List<RestAPI> RestApiList = new List<RestAPI>();
        private List<OAuth> OAuthList = new List<OAuth>();
        private List<Log> OtherList = new List<Log>();



        public Parsing(string DatabaseToRead, string DatabaseToWrite)
        {
            this.databaseReadString = DatabaseToRead;
            this.databaseWriteString = DatabaseToWrite;
        }

        public void ParseAndWriteData()
        {
            
            WebRequestEntities databaseWrite = null;

            using (var databaseRead = new WebRequestEntities(databaseReadString)) 
            {
                //Check if database exists
                if (databaseRead.Database.Exists() == false)
                {
                    throw new Exception("Database to read does not exist");
                }
                try 
                {
                    databaseWrite = new WebRequestEntities(databaseWriteString);
                    databaseWrite.Configuration.AutoDetectChangesEnabled = false;
                    int count = 0;

                    if (databaseWrite.Database.Exists() == false)
                    {
                        throw new Exception("Database to write does not exist");
                    }
                   
                    foreach (var data in databaseRead.Logs.AsNoTracking())
                    {
                        count++;
                        databaseWrite = RefreshContext(databaseWrite, count);
                        if (data.Path != null)
                        {
                            ParseNonNullPath(databaseWrite, data);
                        }
                        else
                        {
                            ParseNullPath(databaseWrite, data);
                        }
                    }
                    //databaseWrite.Logs.AddRange(OtherList);                    
                    databaseWrite.SaveChanges();
                }
                finally {
                    databaseWrite.Dispose();
                }
            }
        }

        //In order to improve performance and run out of storage (Save and dispose is called every 100 request)
        private WebRequestEntities RefreshContext(WebRequestEntities context, int count)
        {

            if (count % 100 == 0)
            {
                context.SaveChanges();
                context.Dispose();
                context = new WebRequestEntities(databaseWriteString);
                context.Configuration.AutoDetectChangesEnabled = false;
            }

            return context;
        }

        private void ParseNonNullPath(WebRequestEntities databaseWrite, Log data)
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
                OAuth oauth = CreateOAuth(data);
                OAuthList.Add(oauth);
                databaseWrite.OAuths.Add(oauth);

            }
            else
            {
                OtherList.Add(data);
                databaseWrite.Logs.Add(data);

            }
        }

        private void ParseNullPath(WebRequestEntities databaseWrite, Log data)
        {
            //For requests:
            //when response is found, it should remove its requst (otherwise has exponential growth as it goes towards the end of the list)
            var odataRequest = ODataList.Where(d => d.ProcGUID == data.ProcGUID);

            var restRequest = RestApiList.Where(d => d.ProcGUID == data.ProcGUID);

            var oauthRequest = OAuthList.Where(d => d.ProcGUID == data.ProcGUID);

            if (odataRequest.Any())
            {
                OData odata = CreateOData(data);
                databaseWrite.ODatas.Add(odata);
                ODataList.Remove(odataRequest.ElementAt(0));

            }
            if (restRequest.Any())
            {
                RestAPI restapi = CreateRestAPI(data);
                databaseWrite.RestAPIs.Add(restapi);
                RestApiList.Remove(restRequest.ElementAt(0));

            }
            if (oauthRequest.Any())
            {
                OAuth oauth = CreateOAuth(data);
                databaseWrite.OAuths.Add(oauth);
                OAuthList.Remove(oauthRequest.ElementAt(0));
            }
            else
            {
                OtherList.Add(data);
                databaseWrite.Logs.Add(data);
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
            RestComposeEndpointAndKeyField(restapi, data.Path);
            
            if(data.StatusCode != null && Int16.Parse(data.StatusCode) > 400) 
            {
                restapi.ErrorCode = data.StatusCode;
                restapi.ErrorMessage = data.Body;
            }
            ComposeNumberOfDetails(data, null, restapi);
            restapi.AccessScope = ComposeAccessScope(data); 
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
        


    }
}
