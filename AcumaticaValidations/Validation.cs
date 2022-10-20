using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AcumaticaValidations
{
    public partial class Validation
    {
        //Check for repeated request???
        public void ValidateOData()
        {
            using (var web = new WebRequestEntities(databaseWriteString))
            {
                if (web.ODatas.Count<OData>() > 10000)
                {
                    Console.WriteLine("Amount of records shouldn't be more than 10,000");
                }
                var duplicates = web.ODatas.GroupBy(i => i.ProcGUID)
                     .Where(x => x.Count() > 2)
                     .Select(val => val.Key);
                if (duplicates.Any())
                {
                    Console.WriteLine("Repeated requests exist! The ProcGUID of the duplicate requests:");

                }
                foreach (var dup in duplicates)
                {
                    Console.WriteLine(dup.HasValue.ToString());
                }
                int metadataCount = 0; //add metadaata info to GI or DAC
                foreach (var data in web.ODatas)
                {
                    if (data.ErrorCode != null && Int16.Parse(data.ErrorCode) == 401) // 401 unauthorized 
                    {
                        Console.WriteLine("Request with ID, " + data.Id + "is not properly identified.");
                    }
                    TimeSpan twentySeconds = new TimeSpan(0, 0, 0, 20);
                    if (data.TypeOfRequest == 2 && twentySeconds.CompareTo(data.Duration) < 0)//response and if duration>20
                    {
                        Console.WriteLine("Request with ID, " + data.Id + "exceeds 20 seconds.");
                    }
                    if (data.Path != null && data.Path.Contains("metadata"))
                    {
                        metadataCount++;
                    }
                }
                if (metadataCount > 1)
                {
                    Console.Write("$metadata request happens multiple times");
                }
            }
        }


        //when using token make sure that every batch of request end with logout request???
        //check for repeated request (identical request in large amount)??? what should be the max accepted number? 20, 50 ?
        public void ValidateRestAPI()
        {
            using (var web = new WebRequestEntities(databaseWriteString))
            {
                if (web.RestAPIs.Count<RestAPI>() > 5000)
                {
                    Console.WriteLine("Amount of records shouldn't be more than 5,000");
                }
                bool detailNumber = web.RestAPIs.Any(data => data.NumberOfDetails > 200);
                if (detailNumber)
                {
                    Console.WriteLine("insert or update with details should not contains more than 200 details per request");
                }
                bool oldEndpointVersion = web.RestAPIs.Any(data => (data.EndpointVersion != "20.200.001" || data.EndpointVersion != "22.200.001"));
                if (oldEndpointVersion)
                {
                    Console.WriteLine("Old version of endpoint is being used");
                }

                //combine elements with same session id. Then check if the number of login and logout are equal to each other.
                //every login has its logout.
                //If ConcurrentAccess scope is enabled we need to make sure they are logging out
                var sameSesId = web.RestAPIs.GroupBy(i => i.SessionID);
                bool equalLoginLogout = true;
                foreach (var group in sameSesId)
                {
                    int login = 0;
                    int logout = 0;
                    foreach (var data in group)
                    {
                        if (data.Path != null)
                        {
                            if (data.Path.Contains("login") || data.Path.Contains("Login")) //NOT SURE??
                            {
                                login++;
                            }
                            if (data.Path.Contains("logout"))
                            {
                                logout++;
                            }
                        }

                    }
                    if (login != logout)
                    {
                        equalLoginLogout = false;
                    }
                }
                if (!equalLoginLogout)
                {
                    Console.WriteLine("Every login must have logout!");
                }
                List<string> AcceptedEndpoints = new List<string>();
                AcceptedEndpoints.Add("default");
                AcceptedEndpoints.Add("ecommerce");
                AcceptedEndpoints.Add("manufacturing");
                AcceptedEndpoints.Add("devicehub");
                AcceptedEndpoints.Add("pos");
                /*
                //Getting error here!
                bool validEndpoint = web.RestAPIs.Any(data => !AcceptedEndpoints.Contains(data.EndpointName, StringComparer.OrdinalIgnoreCase));
                if (validEndpoint)
                {
                    Console.WriteLine(" the body should contain either ID or full set of keys for the document.");
                }
                */
            }
        }

        

        }
    }
