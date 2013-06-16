using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace DuoVia.MpiVisor
{
    public static class AgentPackager
    {
        /// <summary>
        /// Reads DLLs, EXEs and CONFIG files and compresses them into a byte array.
        /// Files read and compressed from agent app domain base directory. Includes subdirectories.
        /// </summary>
        /// <returns></returns>
        public static byte[] PackageAgent()
        {
            var rootDir = AppDomain.CurrentDomain.BaseDirectory;
            
            //TODO - add manifest or other mechanism to allow more than assemblies and config files to be deployed
            var exeFiles = Directory.GetFiles(rootDir, "*.exe", SearchOption.AllDirectories);
            var dllFiles = Directory.GetFiles(rootDir, "*.dll", SearchOption.AllDirectories);
            var configFiles = Directory.GetFiles(rootDir, "*.config", SearchOption.AllDirectories);

            var allFiles = exeFiles.Concat(dllFiles).Concat(configFiles).Where(n => !n.Contains(".vshost.")).ToArray();
            byte[] compressedObjectBytes = CompressFiles(rootDir, allFiles);
            return compressedObjectBytes;
        }

        /// <summary>
        /// Unpack the package bytes to target directory.
        /// </summary>
        /// <param name="targetDir"></param>
        /// <param name="package"></param>
        public static void UnpackPackage(string targetDir, byte[] package)
        {
            var uncompressedFiles = UncompressFiles(package);
            foreach (var kvp in uncompressedFiles)
            {
                var fileName = Path.Combine(targetDir, kvp.Key);
                var dirName = Path.GetDirectoryName(fileName);
                if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName); //probably overkill
                File.WriteAllBytes(fileName, kvp.Value);
            }
        }

        private static byte[] CompressFiles(string rootDir, string[] allFiles)
        {
            rootDir = rootDir.TrimEnd(Path.DirectorySeparatorChar);
            var lengthOfRootDir = rootDir.Length;
            BinaryFormatter formatter = new BinaryFormatter();
            var fileBag = new Dictionary<string, byte[]>();
            foreach (var file in allFiles)
            {
                var pathName = file.Substring(lengthOfRootDir).TrimStart(Path.DirectorySeparatorChar);
                byte[] bytes = File.ReadAllBytes(file);
                fileBag.Add(pathName, bytes);
            }

            byte[] compressedObjectBytes;
            using (var msCompressed = new MemoryStream())
            {
                using (var msObj = new MemoryStream())
                {
                    formatter.Serialize(msObj, fileBag);
                    msObj.Seek(0, SeekOrigin.Begin);

                    using (GZipStream gzs = new GZipStream(msCompressed, CompressionMode.Compress))
                    {
                        msObj.CopyTo(gzs);
                    }
                }
                compressedObjectBytes = msCompressed.ToArray();
            }
            return compressedObjectBytes;
        }

        private static Dictionary<string, byte[]> UncompressFiles(byte[] compressedBytes)
        {
            using (var msObj = new MemoryStream())
            {
                using (var msCompressed = new MemoryStream(compressedBytes))
                using (var gzs = new GZipStream(msCompressed, CompressionMode.Decompress))
                {
                    gzs.CopyTo(msObj);
                }
                msObj.Seek(0, SeekOrigin.Begin);
                BinaryFormatter formatter = new BinaryFormatter();
                var dsObj = (Dictionary<string, byte[]>)formatter.Deserialize(msObj);
                return dsObj;
            }
        }
    }
}
