using System;
using System.Collections;
using DokanNet;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.IO;
using System.Linq;

namespace DokanNetMirror
{
    class Programm
    {
        static void Main(string[] args)
        {
            try
            {
                Mirror mirror = new Mirror("E:\\testdrive");

                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    mirror.Mount("s:\\", DokanOptions.DebugMode, 5);
                }, null);



                Console.WriteLine("Success");
                Console.ReadLine();
            }
            catch (DokanException ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
