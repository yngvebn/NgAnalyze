using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Ng.Contracts
{
    public class NgModule : NgClass
    {
        public NgClass[] Providers { get; private set; }
        public NgClass[] Declarations { get; private set; }
        public NgClass[] Imports { get; private set; }
        public NgClass[] Exports { get; private set; }
        public NgClass[] EntryComponents { get; private set; }
        public NgClass[] BootstrapClasses { get; private set; }
        public string Id { get; set; }

    }

    public class NgComponent : NgClass
    {

    }

    public class NgDirective : NgClass
    {

    }

    public class NgPipe : NgClass
    {

    }

    public class NgClass
    {

    }

    public class NgInterface
    {

    }

    public class TsConfig
    {
        public string[] Exclude
        {
            get
            {
                List<string> list = new List<string>();
                list.AddRange(_this.Exclude);
                if (Extended != null)
                {
                    list.AddRange(Extended.Exclude);
                }
                return list.Distinct().Select(MakeLocalPath).ToArray();
            }
        }


        public string[] Include
        {
            get
            {
                List<string> list = new List<string>();
                list.AddRange(_this.Include);
                if (Extended != null)
                {
                    list.AddRange(Extended.Include);
                }
                return list.Distinct().Select(MakeLocalPath).ToArray();
            }
        }

        private string MakeLocalPath(string arg)
        {
            return new Uri(new Uri(_directory, UriKind.Absolute), new Uri(arg, UriKind.Relative)).LocalPath;
        }

        private TsConfigFile _this;
        public TsConfig Extended { get; private set; }

        private string _directory;

        public TsConfig(string file)
        {
            var config = JsonConvert.DeserializeObject<TsConfigFile>(File.ReadAllText(file));
            _this = config;
            _directory = Path.GetDirectoryName(file) + "\\";
            if (!string.IsNullOrEmpty(config.Extends))
            {
                var extendedFileName = new Uri(new Uri(_directory, UriKind.Absolute), new Uri(config.Extends, UriKind.Relative));
                Extended = new TsConfig(extendedFileName.LocalPath);
            }
        }
    }

    public class TsConfigFile
    {
        public string[] Exclude { get; set; }
        public string[] Include { get; set; }
        public string Extends { get; set; }
    }
}
