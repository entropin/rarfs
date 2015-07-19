using System;
using System.Collections;
using DokanNet;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.IO;
using System.Linq;
using SharpCompress;
using SharpCompress.Reader;
using SharpCompress.Common;
using FileAccess = DokanNet.FileAccess;
using System.Text;
using SharpCompress.Archive.Rar;

namespace DokanNetMirror
{
    class Programm
    {
        static void Main(string[] args)
        {

            try
            {
                rarfs rarfs = new rarfs("E:");

                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    rarfs.Mount("R:\\", DokanOptions.DebugMode, 5);
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
