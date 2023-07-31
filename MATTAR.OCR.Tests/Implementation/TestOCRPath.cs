using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MATTAR.OCR.Tests.Implementation
{
    public class TestOCRPath : IOCRPath
    {
        public string GetRootPath()
        {
            var rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            return rootPath
                ?? throw new DirectoryNotFoundException();
        }

        public string GetTempPath()
        {
            var tempPath = Path.Combine(GetRootPath(), "tmp");
            // if tempPath doesn't exist, create it
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            return tempPath
                ?? throw new DirectoryNotFoundException();
        }
    }
}
