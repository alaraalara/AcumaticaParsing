using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AcumaticaValidations
{
    public partial class Parsing
    {

        private void RestComposeEndpointAndKeyField(RestAPI restapi, string path)
        {
            if (path != null)
            {
                Regex regex = new Regex("/");
                var pathElement = regex.Split(path);
                if (path.Contains("Login") || path.Contains("logout") || path.Contains("login") || path.Contains("Logout"))
                {
                    restapi.EndpointName = pathElement[3];
                    restapi.EndpointVersion = null;
                    restapi.EndpointEntity = pathElement[4];//everything that comes after the 4th elenet (considered as a filter)

                }
                else
                {
                    restapi.EndpointName = pathElement[3];
                    restapi.EndpointVersion = pathElement[4];
                    if (pathElement.Length > 5)
                    {
                        restapi.EndpointEntity = pathElement[5];//everything that comes after the 4th elenet (considered as a filter)
                    }
                    if (pathElement.Length > 6)
                    {
                        string keyfield = pathElement[5];
                        for (var i = 6; i < pathElement.Length; i++)
                        {
                            keyfield = pathElement[i];
                        }
                        restapi.KeyField = keyfield;
                    }
                }
            }

        }

        //ALSO store the keyfield in the body of a request
        private string GetDocumentID(Log data)
        {
            if (data.EventType == 2 && data.Body!=null && data.Body.Contains("\"id\":"))
            {
                var stringStartWithSessionID = data.Body.Substring(data.Body.IndexOf("\"id\":"));
                var splitString = stringStartWithSessionID.Split(','); //Assuming it is always at the end 
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

        private void ComposeNumberOfDetails(Log data, OData odata = null, RestAPI restapi = null)
        {
            var pattern = @"\[(.*?)\]"; //seperates everythings inside brackets 
            var matches = Regex.Matches(data.Body, pattern);
            var count = ((short)matches.Count);
            if (odata != null)
            {
                odata.NumberOfDetails = count;
            }
            if (restapi != null)
            {
                restapi.NumberOfDetails = count;
            }

        }

        private string ComposeAccessScope(Log data)
        {
            if (data.QueryString != null)
            {
                //IT IS  in the body of a requets
                //Assuming that access scope comes after "?" symbol
                //
                if (data.QueryString.Contains("scope"))
                {
                    var stringStartWithAccessScope = data.Cookies.Substring(data.Cookies.IndexOf("scope=") + 6);
                    var splitString = stringStartWithAccessScope.Split(',');
                    return splitString[0];
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


        //NOTE: the token for now is stored in session ID!
        private string ComposeSessionID(Log data)
        {
            if (data.Cookies.Contains("ASP.NET"))
            {
                var stringStartWithSessionID = data.Cookies.Substring(data.Cookies.IndexOf("SessionId"));
                var splitString = stringStartWithSessionID.Split(',');
                return splitString[0];

            }
            if (data.Headers.Contains("Authorization:Bearer"))
            {
                var stringStartWithSessionID = data.Headers.Substring(data.Headers.IndexOf("Bearer"));               
                var splitString = stringStartWithSessionID.Split(',');
                return splitString[0];

            }
            else
            {
                return null;
            }
        }

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

        private string ComposeAuthenticaton(Log data, OData odata = null, RestAPI restapi = null)
        {
            if (odata != null)
            {
                if (data.Headers.Contains("Bearer"))
                {
                    return "OAuth";
                }
                else
                {
                    return "BasicAuthentication";
                }
            }
            if (restapi != null && data.Headers != null)
            {
                if (data.Headers.Contains("Bearer"))
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
            if (odata != null && odata.Path != null)
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
            if (restapi != null && restapi.Path != null)
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
