using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AcumaticaValidations
//note: add a helper to create a connection strign with specific format
//add function to create a table if it doesnt exists.
{
    public class Program
    {
        static void Main(string[] args)
        {
            WebRequestEntities validations = new WebRequestEntities("name=Validations");
           
            Log odataLogs;

            WebRequestEntities context = new WebRequestEntities("name=AcuProx");
            Log log;

            /*
            using (context)
            {
            
                Console.WriteLine(context.Database.Exists());
                Console.WriteLine(validations.Database.Exists());

                var odata = context.Logs.SqlQuery("Select * from Log where Path like '%/odata/%'").ToList<Log>();
                foreach (Log logOdata in odata)
                {
                   
                    validations.Logs.Add(logOdata);
                    
                    validations.SaveChanges();
                }
                //check if it has been added!
                foreach(var validatedData in validations.Logs)
                {
                    Console.WriteLine(validatedData.Headers);
                }               

            }

            */

            Validation example = new Validation(context, validations);
            example.parseData();
            example.writeData();

          





        }


    }
    }

