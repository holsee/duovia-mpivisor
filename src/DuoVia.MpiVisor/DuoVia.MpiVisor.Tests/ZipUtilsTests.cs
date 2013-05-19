using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace DuoVia.MpiVisor.Tests
{
    [TestClass]
    public class ZipUtilsTests
    {
        [TestMethod]
        public void PackageAgent_Test()
        {
            var packageBytes = ZipUtils.PackageAgent();
            Assert.IsNotNull(packageBytes);
            Assert.IsTrue(packageBytes.Length > 100);
        }

        [TestMethod]
        public void UnpackPackage_Test()
        {
            var packageBytes = ZipUtils.PackageAgent();
            Assert.IsNotNull(packageBytes);
            Assert.IsTrue(packageBytes.Length > 100);

            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(tempDirectory);
                ZipUtils.UnpackPackage(tempDirectory, packageBytes);

                var files = Directory.GetFiles(tempDirectory);
                Assert.IsNotNull(files);
                Assert.IsTrue(files.Length > 0);
                Assert.IsTrue(files.Any(x => x.EndsWith(".config")));
                Assert.IsTrue(files.Any(x => x.EndsWith(".dll")));
                var configFile = files.Where(x => x.EndsWith(".config")).FirstOrDefault();
                var config = File.ReadAllText(configFile);
                Assert.IsTrue(config.Contains("<configuration>"));
                Assert.IsTrue(config.Contains("<appSettings>"));
            }
            finally
            {
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }
    }
}
