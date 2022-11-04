using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcumaticaValidations
{
    public partial class Parsing
    {

        //decide if body or query string is empty

        

        private string searchStringInBodyOrQueryString(Log data, String stringToSearch)
        {
            if (data.Body != null && data.EventType==1 && data.Body.Contains(stringToSearch))
            {
                var stringStartWithSessionID = data.Body.Substring(data.Body.IndexOf(stringToSearch) + stringToSearch.Length);
                var splitString = stringStartWithSessionID.Split('&'); //Assuming it is always at the end 
                return splitString[0];
            }
            if(data.QueryString != null && data.QueryString.Contains(stringToSearch))
            {
                var stringStartWithSessionID = data.QueryString.Substring(data.QueryString.IndexOf(stringToSearch) + stringToSearch.Length);
                var splitString = stringStartWithSessionID.Split('&'); //Assuming it is always at the end 
                return splitString[0];
            }
            if (data.Body != null && data.EventType == 2 && data.Body.Contains(stringToSearch))
            {
                var stringStartWithSessionID = data.Body.Substring(data.Body.IndexOf(stringToSearch) + stringToSearch.Length);
                var splitString = stringStartWithSessionID.Split(','); //Assuming it is always at the end 
                return splitString[0];
            }
            else
            {
                return null;
            }
        }

    }
}
