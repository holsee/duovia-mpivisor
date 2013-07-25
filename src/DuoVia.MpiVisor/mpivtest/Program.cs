using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mpivtest
{
    class Program
    {
        static void Main(string[] args)
        {
            var list = new List<string>(args);
            list.Add(DateTime.Now.ToString("yyyyMMdd-hhmmss"));
            var testFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"mpivtest.txt");
            File.AppendAllLines(testFile, list);
        }
    }
}
